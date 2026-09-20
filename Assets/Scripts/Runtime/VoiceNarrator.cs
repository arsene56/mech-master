using System;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
#endif

namespace MechMaster.Runtime
{
    public sealed class VoiceNarrator : MonoBehaviour
    {
        private bool narrationEnabled = true;
        private string pendingText;
        private float startTime;
        public bool Available { get; private set; }
        public string Status { get; private set; } = "语音初始化中";
        public int SubmittedSpeechCount { get; private set; }
        public bool Enabled
        {
            get => narrationEnabled;
            set { narrationEnabled = value; if (!value) Stop(); }
        }
#if UNITY_EDITOR_WIN
        private Process speechProcess;
        private readonly ConcurrentQueue<string> responses = new ConcurrentQueue<string>();
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR_WIN
            startTime = Time.realtimeSinceStartup;
            Status = "语音初始化中";
            try
            {
                string executable = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                    "Library/MechMaster/WindowsSpeechHost.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("Use the Build Local Speech menu first.", executable);
                speechProcess = new Process { StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
                }};
                speechProcess.OutputDataReceived += (_, args) => { if (args.Data != null) responses.Enqueue(args.Data); };
                speechProcess.ErrorDataReceived += (_, args) => { if (args.Data != null) responses.Enqueue("ERROR|" + args.Data); };
                speechProcess.Start();
                speechProcess.StandardInput.AutoFlush = true;
                speechProcess.BeginOutputReadLine();
                speechProcess.BeginErrorReadLine();
            }
            catch (Exception error)
            {
                Fail(error.Message);
                // A failed Process.Start leaves a Process without an OS handle.
                // Dispose it here so later Stop/Update calls cannot query HasExited.
                OnDisable();
            }
#else
            Status = "当前平台语音待接入";
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR_WIN
            while (responses.TryDequeue(out string response))
            {
                if (response.StartsWith("READY|", StringComparison.Ordinal))
                {
                    Available = true;
                    Status = "系统中文语音 · 就绪";
                    UnityEngine.Debug.Log("MECH_MASTER_NARRATION_READY " + response.Substring(6));
                    if (Enabled && pendingText != null) { string text = pendingText; pendingText = null; Speak(text); }
                }
                else if (response == "STARTED")
                {
                    SubmittedSpeechCount++;
                    UnityEngine.Debug.Log("MECH_MASTER_NARRATION_SPEAKING count=" + SubmittedSpeechCount);
                }
                else if (response == "FINISHED") UnityEngine.Debug.Log("MECH_MASTER_NARRATION_FINISHED");
                else if (response == "CANCELLED") UnityEngine.Debug.Log("MECH_MASTER_NARRATION_CANCELLED");
                else if (response.StartsWith("ERROR|", StringComparison.Ordinal)) Fail(response.Substring(6));
            }
            if (speechProcess != null && speechProcess.HasExited && Available) Fail("Speech helper exited.");
            if (!Available && Status == "语音初始化中" && Time.realtimeSinceStartup - startTime > 10f)
                Fail("Speech startup timed out.");
#endif
        }

        public void Speak(string text)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!Available)
            {
                if (Status == "语音初始化中") pendingText = text;
                return;
            }
#if UNITY_EDITOR_WIN
            Send("speak|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
#endif
        }

        public void Stop()
        {
            pendingText = null;
#if UNITY_EDITOR_WIN
            if (speechProcess != null && !speechProcess.HasExited) Send("stop");
#endif
        }

        private void Fail(string message)
        {
            Available = false;
            pendingText = null;
            Status = "语音不可用，请查看日志";
            UnityEngine.Debug.LogWarning("MECH_MASTER_NARRATION_UNAVAILABLE " + message);
        }

#if UNITY_EDITOR_WIN
        private void Send(string command)
        {
            try { speechProcess?.StandardInput.WriteLine(command); }
            catch (Exception error) { Fail(error.Message); }
        }
#endif

        private void OnApplicationPause(bool paused) { if (paused) Stop(); }
        private void OnDisable()
        {
            Available = false;
            pendingText = null;
#if UNITY_EDITOR_WIN
            if (speechProcess == null) return;
            try
            {
                if (!speechProcess.HasExited)
                {
                    speechProcess.StandardInput.WriteLine("quit");
                    speechProcess.StandardInput.Close();
                    if (!speechProcess.WaitForExit(200)) speechProcess.Kill();
                }
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("Speech shutdown: " + error.Message); }
            finally { speechProcess.Dispose(); speechProcess = null; }
#endif
        }
    }
}

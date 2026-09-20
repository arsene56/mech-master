#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MechMaster.Editor
{
    [InitializeOnLoad]
    public static class LocalSpeechBuilder
    {
        static LocalSpeechBuilder()
        {
            EditorApplication.delayCall += EnsureBuilt;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) EnsureBuilt();
            };
        }

        [MenuItem("机械大师/构建本地中文语音")]
        public static void EnsureBuilt()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            string source = Path.GetFullPath(Path.Combine(root, "Tools/Audio/WindowsSpeechHost.cs")).Replace('/', '\\');
            string output = Path.GetFullPath(Path.Combine(root, "Library/MechMaster/WindowsSpeechHost.exe")).Replace('/', '\\');
            if (File.Exists(output) && File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(source)) return;
            try
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string compiler = Path.Combine(windows, "Microsoft.NET/Framework64/v4.0.30319/csc.exe");
                string[] assemblies = Directory.GetFiles(Path.Combine(windows,
                    "Microsoft.NET/assembly/GAC_MSIL/System.Speech"), "System.Speech.dll", SearchOption.AllDirectories);
                if (assemblies.Length == 0) throw new FileNotFoundException("System.Speech is not installed.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = compiler,
                    Arguments = "/nologo /target:exe /optimize+ /out:\"" + output + "\" /reference:\""
                        + assemblies[0] + "\" \"" + source + "\"",
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true, RedirectStandardError = true
                }))
                {
                    string compilerOutput = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException(compilerOutput);
                }
                UnityEngine.Debug.Log("MECH_MASTER_LOCAL_SPEECH_BUILD_OK");
            }
            catch (Exception error) { UnityEngine.Debug.LogWarning("Local speech build: " + error.Message); }
        }
    }
}
#endif

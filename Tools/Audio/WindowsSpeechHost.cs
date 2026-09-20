using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;

// Built automatically by LocalSpeechBuilder for Windows Editor previews.
// Uses installed Windows voices; no network, shell execution or redistributed voice files.
internal static class WindowsSpeechHost
{
    private static readonly object OutputLock = new object();
    private static void Report(string message)
    {
        lock (OutputLock) { Console.WriteLine(message); Console.Out.Flush(); }
    }

    private static int Main()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            using (var speech = new SpeechSynthesizer())
            {
                var voice = speech.GetInstalledVoices().FirstOrDefault(v =>
                    v.Enabled && v.VoiceInfo.Culture.Name == "zh-CN");
                if (voice == null) throw new InvalidOperationException("No installed zh-CN voice.");
                speech.SelectVoice(voice.VoiceInfo.Name);
                speech.SetOutputToDefaultAudioDevice();
                speech.Volume = 90;
                speech.Rate = -1;
                speech.SpeakStarted += (sender, args) => Report("STARTED");
                speech.SpeakCompleted += (sender, args) => {
                    // System.Speech can populate both Cancelled and Error when a
                    // prompt is interrupted. Cancellation is not a backend failure.
                    if (args.Cancelled || args.Error is OperationCanceledException) Report("CANCELLED");
                    else if (args.Error != null) Report("ERROR|" + args.Error.Message);
                    else Report("FINISHED");
                };
                Report("READY|" + voice.VoiceInfo.Name);
                string command;
                while ((command = Console.ReadLine()) != null)
                {
                    if (command == "quit") break;
                    if (command == "stop") { speech.SpeakAsyncCancelAll(); continue; }
                    if (!command.StartsWith("speak|", StringComparison.Ordinal)) continue;
                    string text = Encoding.UTF8.GetString(Convert.FromBase64String(command.Substring(6)));
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    speech.SpeakAsyncCancelAll();
                    speech.SpeakAsync(text);
                }
                speech.SpeakAsyncCancelAll();
            }
            return 0;
        }
        catch (Exception error) { Report("ERROR|" + error.Message.Replace('\n', ' ')); return 1; }
    }
}

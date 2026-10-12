using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TeamsSpeechRecognition.S03.Recognition
{
    public sealed class TeamsCaptionUpdate
    {
        public string SourceKey { get; set; }
        public string Text { get; set; }
        public string SpeakerName { get; set; }
        public long Observed100ns { get; set; }
        public bool ScopeVerified { get; set; }
        public bool InitialHistory { get; set; }
    }

    /// <summary>Reads changing Teams UI Automation captions without touching the neighboring application.</summary>
    public sealed class TeamsCaptionClient : IDisposable
    {
        private readonly Process process;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly Thread outputThread;
        private bool disposed;

        public TeamsCaptionClient(string scriptPath, long sessionClockOrigin100ns)
        {
            if (String.IsNullOrWhiteSpace(scriptPath)) throw new ArgumentException("Caption reader path is required.", "scriptPath");
            if (!File.Exists(scriptPath)) throw new FileNotFoundException("Teams caption reader was not found.", scriptPath);

            string powerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powerShell)) powerShell = "powershell.exe";
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = powerShell;
            start.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(Path.GetFullPath(scriptPath)) +
                " -SessionClockOrigin100ns " + sessionClockOrigin100ns.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " -ParentProcessId " + Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = new UTF8Encoding(false, true);
            process = new Process { StartInfo = start };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                if (args.Data != null) ReportStatus("Teams caption reader: " + args.Data);
            };
            if (!process.Start()) throw new InvalidOperationException("Could not start Teams caption reader.");
            process.BeginErrorReadLine();
            outputThread = new Thread(ReadUpdates) { IsBackground = true, Name = "Teams live captions" };
            outputThread.Start();
        }

        public event Action<TeamsCaptionUpdate> CaptionUpdated;
        public event Action<string> StatusChanged;

        private void ReadUpdates()
        {
            try
            {
                string line;
                while (!disposed && (line = process.StandardOutput.ReadLine()) != null)
                {
                    try
                    {
                        var record = serializer.DeserializeObject(line) as System.Collections.Generic.IDictionary<string, object>;
                        if (record == null) continue;
                        object status;
                        if (record.TryGetValue("status", out status)) { ReportStatus(Convert.ToString(status)); continue; }
                        object key, text, speaker, observed, verified, history;
                        if (!record.TryGetValue("source_key", out key) || !record.TryGetValue("text", out text) || !record.TryGetValue("observed_100ns", out observed)) continue;
                        record.TryGetValue("speaker", out speaker);
                        record.TryGetValue("scope_verified", out verified);
                        record.TryGetValue("initial_history", out history);
                        TeamsCaptionUpdate update = new TeamsCaptionUpdate
                        {
                            SourceKey = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture),
                            Text = Convert.ToString(text, System.Globalization.CultureInfo.InvariantCulture),
                            SpeakerName = Convert.ToString(speaker, System.Globalization.CultureInfo.InvariantCulture),
                            Observed100ns = Convert.ToInt64(observed, System.Globalization.CultureInfo.InvariantCulture),
                            ScopeVerified = verified is bool && (bool)verified,
                            InitialHistory = history is bool && (bool)history
                        };
                        Action<TeamsCaptionUpdate> handler = CaptionUpdated;
                        if (handler != null) handler(update);
                    }
                    catch (Exception error) { ReportStatus("Caption record ignored: " + error.Message); }
                }
                if (!disposed) ReportStatus("Teams caption reader exited; caption enrichment is unavailable.");
            }
            catch (ObjectDisposedException) { }
            catch (IOException error) { if (!disposed) ReportStatus("Caption reader ended: " + error.Message); }
        }

        private void ReportStatus(string message)
        {
            Action<string> handler = StatusChanged;
            if (handler != null) handler(message);
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (!process.HasExited) process.Kill(); } catch { }
            if (outputThread != null && Thread.CurrentThread != outputThread) outputThread.Join(2000);
            process.Dispose();
        }
    }
}

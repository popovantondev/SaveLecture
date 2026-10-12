using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SaveLecture
{
    public sealed class ArchiveStore : IDisposable
    {
        private readonly string root;
        private readonly string session;
        private readonly bool resumeLecture;
        private readonly string activePath;
        private readonly object gate = new object();
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private FileStream journal;
        private FileStream transcript;
        private readonly List<string> journalPaths = new List<string>();
        private string day;
        public string DayDirectory { get; private set; }
        public string TranscriptPath { get; private set; }
        public string SessionStem { get { return "Session_" + session; } }

        public ArchiveStore(string rootDirectory) : this(rootDirectory, false) { }
        public ArchiveStore(string rootDirectory, bool resume)
        {
            root = rootDirectory; resumeLecture = resume;
            string service = Path.Combine(root, "_service");
            Directory.CreateDirectory(service);
            activePath = Path.Combine(service, "active-lecture.txt");
            string existing = resume && File.Exists(activePath) ? File.ReadAllText(activePath).Trim() : "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(existing, @"^\d{6}-\d{3}-[a-f0-9]{8}$")) existing = "";
            session = existing.Length > 0 ? existing : DateTime.Now.ToString("HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (resume)
            {
                if (existing.Length > 0) RestorePending();
                using (var state = new FileStream(activePath, FileMode.Create, FileAccess.Write, FileShare.Read)) Write(state, session);
            }
        }

        private void RestorePending()
        {
            foreach (string directory in Directory.GetDirectories(root))
            {
                string source = Path.Combine(directory, "_service", SessionStem + ".jsonl");
                if (!File.Exists(source)) continue;
                var latest = new Dictionary<string, IDictionary<string, object>>();
                foreach (string line in File.ReadLines(source))
                {
                    try
                    {
                        var row = json.DeserializeObject(line) as IDictionary<string, object>;
                        if (row != null && row.ContainsKey("key") && row.ContainsKey("text")) latest[Convert.ToString(row["key"])] = row;
                    }
                    catch (ArgumentException) { }
                }
                string textPath = Path.Combine(directory, SessionStem + ".txt");
                string already = File.Exists(textPath) ? File.ReadAllText(textPath) : "";
                foreach (var row in latest.Values)
                {
                    string text = Convert.ToString(row["text"]);
                    string speaker = Convert.ToString(row["speaker"]);
                    string body = (String.IsNullOrWhiteSpace(speaker) ? "" : speaker + ": ") + text;
                    DateTime observed = DateTime.Parse(Convert.ToString(row["received"]), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    if (already.Contains(Format(text, speaker, observed))) continue;
                    using (var output = new FileStream(textPath, FileMode.Append, FileAccess.Write, FileShare.Read)) Write(output, Format(text, speaker, observed));
                }
            }
        }

        private void OpenDay(DateTime now)
        {
            string nextDay = now.ToString("dd.MM.yyyy");
            if (day == nextDay) return;
            CloseStreams();
            string directory = Path.Combine(root, nextDay);
            Directory.CreateDirectory(Path.Combine(directory, "scr"));
            string service = Path.Combine(directory, "_service");
            Directory.CreateDirectory(service);
            string stem = "Session_" + session;
            string journalPath = Path.Combine(service, stem + ".jsonl");
            var nextJournal = new FileStream(journalPath, resumeLecture ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            if (resumeLecture && nextJournal.Length > 0) Write(nextJournal, "\n");
            try
            {
                var nextText = new FileStream(Path.Combine(directory, stem + ".txt"), resumeLecture ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                journal = nextJournal;
                journalPaths.Add(journalPath);
                transcript = nextText;
                DayDirectory = directory;
                TranscriptPath = Path.Combine(directory, stem + ".txt");
                day = nextDay;
            }
            catch { nextJournal.Dispose(); throw; }
        }

        public void AppendRaw(string key, string text, string speaker, DateTime now)
        {
            lock (gate)
            {
                OpenDay(now);
                Write(journal, json.Serialize(new { type = "caption", key = key, text = text, speaker = speaker, received = now.ToString("o") }) + "\n");
            }
        }

        public void AppendText(string text, string speaker, DateTime observed)
        {
            lock (gate)
            {
                // A delayed final line belongs to its observation day, even across midnight.
                string target = Path.Combine(root, observed.ToString("dd.MM.yyyy"));
                Directory.CreateDirectory(target);
                if (day == observed.ToString("dd.MM.yyyy"))
                    Write(transcript, Format(text, speaker, observed));
                else
                {
                    string path = Path.Combine(target, "Session_" + session + ".txt");
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                        Write(stream, Format(text, speaker, observed));
                }
            }
        }

        private static string Format(string text, string speaker, DateTime observed)
        {
            return "[" + observed.ToString("HH:mm:ss") + "] " + (String.IsNullOrWhiteSpace(speaker) ? "" : speaker + ": ") + text.Replace("\r", " ").Replace("\n", " ") + Environment.NewLine;
        }

        private static void Write(FileStream stream, string value)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private void CloseStreams()
        {
            if (journal != null) { journal.Dispose(); journal = null; }
            if (transcript != null) { transcript.Dispose(); transcript = null; }
            day = null;
        }
        public void Dispose() { lock (gate) CloseStreams(); }

        public void MarkClean()
        {
            lock (gate)
                foreach (string path in journalPaths)
                    using (var marker = new FileStream(path + ".complete", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)) marker.Flush(true);
            if (resumeLecture && File.Exists(activePath)) File.Delete(activePath);
        }

        public static int RecoverUnfinished(string rootDirectory)
        {
            int recovered = 0;
            for (int offset = 0; offset < 2; offset++)
            {
                string directory = Path.Combine(rootDirectory, DateTime.Now.AddDays(-offset).ToString("dd.MM.yyyy"));
                if (!Directory.Exists(directory)) continue;
                foreach (string path in Directory.GetFiles(directory, "Session_*.jsonl"))
                {
                    string output = Path.ChangeExtension(path, null) + "_Recovered.txt";
                    if (File.Exists(path + ".complete") || File.Exists(output) || new FileInfo(path).Length == 0) continue;
                    Recover(path, output);
                    recovered++;
                }
            }
            return recovered;
        }

        public static int Recover(string journalPath, string outputPath)
        {
            // Never rewrite an original transcript. A torn final JSON record is ignored.
            int count = 0;
            var parser = new JavaScriptSerializer();
            using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var input = new StreamReader(journalPath, Encoding.UTF8))
            {
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    IDictionary<string, object> row;
                    try { row = parser.DeserializeObject(line) as IDictionary<string, object>; }
                    catch (ArgumentException) { continue; }
                    if (row == null || !row.ContainsKey("text") || !row.ContainsKey("received")) continue;
                    DateTime time = DateTime.Parse(Convert.ToString(row["received"]), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    Write(output, Format(Convert.ToString(row["text"]), row.ContainsKey("speaker") ? Convert.ToString(row["speaker"]) : "", time));
                    count++;
                }
            }
            return count;
        }
    }
}

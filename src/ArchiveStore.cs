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
        sealed class Projection
        {
            public byte[] Prefix;
            public readonly List<CaptionGroup> Items=new List<CaptionGroup>();
        }
        readonly Dictionary<string,Projection> projections=new Dictionary<string,Projection>();
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
            var resolved=new Dictionary<string,IDictionary<string,object>>();
            var versions=new List<IDictionary<string,object>>();
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
                        if (row != null && row.ContainsKey("key") && row.ContainsKey("text")) {
                            if(row.ContainsKey("resolved_text") && !String.IsNullOrEmpty(Convert.ToString(row["resolved_text"]))) {
                                string key=Convert.ToString(row["key"]); versions.Add(row);
                                if(!resolved.ContainsKey(key) || DateTime.Parse(Convert.ToString(row["received"]))>=DateTime.Parse(Convert.ToString(resolved[key]["received"]))) resolved[key]=row;
                            } else latest[Convert.ToString(row["key"])] = row;
                        }
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
            RestoreProjection(resolved,versions);
        }

        void RestoreProjection(Dictionary<string,IDictionary<string,object>> latest,List<IDictionary<string,object>> versions)
        {
            foreach(var row in latest.Values) {
                DateTime time=DateTime.Parse(Convert.ToString(row["first_seen"]),null,System.Globalization.DateTimeStyles.RoundtripKind);
                string path=Path.Combine(root,time.ToString("dd.MM.yyyy"),SessionStem+".txt"); Projection projection;
                if(!projections.TryGetValue(path,out projection)) {
                    string prefix=File.Exists(path)?File.ReadAllText(path):"";
                    foreach(var old in versions) {
                        DateTime first=DateTime.Parse(Convert.ToString(old["first_seen"]),null,System.Globalization.DateTimeStyles.RoundtripKind);
                        if(first.Date!=time.Date) continue;
                        prefix=prefix.Replace(Format(Convert.ToString(old["resolved_text"]),Convert.ToString(old["speaker"]),first),"");
                    }
                    projection=new Projection { Prefix=new UTF8Encoding(false).GetBytes(prefix) }; projections[path]=projection;
                }
                projection.Items.Add(new CaptionGroup { Id=Convert.ToString(row["key"]),Text=Convert.ToString(row["resolved_text"]),Speaker=Convert.ToString(row["speaker"]),Started=time });
            }
            foreach(var pair in projections) {
                pair.Value.Items.Sort(delegate(CaptionGroup a,CaptionGroup b) { return a.Started.CompareTo(b.Started); });
                Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)); WriteProjection(pair.Key,pair.Value);
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

        public void AppendRaw(string key, string text, string speaker, DateTime now,string resolved=null,DateTime? started=null,string source=null)
        {
            lock (gate)
            {
                OpenDay(now);
                Write(journal, json.Serialize(new { type = "caption", key = key, text = text, speaker = speaker, received = now.ToString("o"),resolved_text=resolved,first_seen=started.HasValue?started.Value.ToString("o"):null,source=source }) + "\n");
            }
        }

        // The TXT is a readable projection; raw observations remain append-only.
        // Only records owned by this running session are replaceable, never its prefix.
        public void UpsertCaption(string key,string text,string speaker,DateTime observed)
        {
            lock(gate) {
                string directory=Path.Combine(root,observed.ToString("dd.MM.yyyy")); Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,SessionStem+".txt"); Projection projection;
                if(transcript!=null && String.Equals(path,TranscriptPath,StringComparison.OrdinalIgnoreCase)) { transcript.Dispose(); transcript=null; }
                if(!projections.TryGetValue(path,out projection)) {
                    projection=new Projection { Prefix=File.Exists(path)?File.ReadAllBytes(path):new byte[0] }; projections[path]=projection;
                }
                var item=projection.Items.Find(delegate(CaptionGroup value) { return value.Id==key; });
                if(item==null) { item=new CaptionGroup { Id=key,Started=observed }; projection.Items.Add(item); }
                item.Text=text; item.Speaker=speaker;
                WriteProjection(path,projection);
            }
        }

        void WriteProjection(string path,Projection projection)
        {
            if(transcript!=null && String.Equals(path,TranscriptPath,StringComparison.OrdinalIgnoreCase)) { transcript.Dispose(); transcript=null; }
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read)) {
                    output.Write(projection.Prefix,0,projection.Prefix.Length);
                    foreach(var item in projection.Items) Write(output,Format(item.Text,item.Speaker,item.Started));
                    output.Flush(true);
                }
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            } finally {
                if(File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public void AppendText(string text, string speaker, DateTime observed)
        {
            lock (gate)
            {
                // A delayed final line belongs to its observation day, even across midnight.
                string target = Path.Combine(root, observed.ToString("dd.MM.yyyy"));
                Directory.CreateDirectory(target);
                if (day == observed.ToString("dd.MM.yyyy") && transcript!=null)
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
            var grouped=new Dictionary<string,IDictionary<string,object>>();
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
                    if(row.ContainsKey("resolved_text") && !String.IsNullOrEmpty(Convert.ToString(row["resolved_text"]))) { grouped[Convert.ToString(row["key"])]=row; continue; }
                    DateTime time = DateTime.Parse(Convert.ToString(row["received"]), null, System.Globalization.DateTimeStyles.RoundtripKind);
                    Write(output, Format(Convert.ToString(row["text"]), row.ContainsKey("speaker") ? Convert.ToString(row["speaker"]) : "", time));
                    count++;
                }
                var final=new List<IDictionary<string,object>>(grouped.Values);
                final.Sort(delegate(IDictionary<string,object> a,IDictionary<string,object> b) { return DateTime.Parse(Convert.ToString(a["first_seen"])).CompareTo(DateTime.Parse(Convert.ToString(b["first_seen"]))); });
                foreach(var row in final) {
                    DateTime time=DateTime.Parse(Convert.ToString(row["first_seen"]),null,System.Globalization.DateTimeStyles.RoundtripKind);
                    Write(output,Format(Convert.ToString(row["resolved_text"]),Convert.ToString(row["speaker"]),time)); count++;
                }
            }
            return count;
        }
    }
}

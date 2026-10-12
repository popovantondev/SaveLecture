using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SaveLecture
{
    public sealed class NameMatcher
    {
        internal static string TestPath=null;
        private readonly Regex pattern;
        public NameMatcher(string[] aliases)
        {
            string[] words = aliases.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("#")).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(x => x.Length).ToArray();
            if (words.Length == 0) { pattern=new Regex("(?!)"); return; }
            string alternatives = String.Join("|", words.Select(x => Regex.Escape(x).Replace("\\ ", @"[\s-]*")).ToArray());
            pattern = new Regex(@"(?<!\p{L})(?:" + alternatives + @")(?:[\s,.-]*(?:" + alternatives + @"))*(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }
        public int Count(string text) { return pattern.Matches(text ?? "").Count; }
        public static NameMatcher Load(string path) { return new NameMatcher(File.ReadAllLines(path)); }
        public static string SettingsPath
        {
            get {
                if(TestPath!=null) return TestPath;
                string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SaveLecture","NameAliases.txt");
                if(!File.Exists(path)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    string previous=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NameAliases.txt");
                    File.WriteAllText(path,File.Exists(previous)?File.ReadAllText(previous):"");
                }
                return path;
            }
        }
    }

    public sealed class NameAlertDecider
    {
        private sealed class Seen { public DateTime Time; public int Count; }
        private readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();
        public bool Accept(string key, int matches, DateTime now)
        {
            foreach (string oldKey in seen.Keys.ToArray()) if ((now - seen[oldKey].Time).TotalMinutes > 2) seen.Remove(oldKey);
            if (matches == 0) { seen.Remove(key); return false; }
            Seen previous;
            bool repeated = seen.TryGetValue(key, out previous) && (now - previous.Time).TotalSeconds < 4 && matches <= previous.Count;
            seen[key] = new Seen { Time = now, Count = matches };
            return !repeated;
        }
    }
}

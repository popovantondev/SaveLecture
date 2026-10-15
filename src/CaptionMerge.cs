using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SaveLecture
{
    public sealed class CaptionGroup
    {
        public string Id,Text,Speaker;
        public DateTime Started,Updated;
    }
    // Session's bounded utterance matching, independent of changing UI control IDs.
    public sealed class CaptionMerge
    {
        readonly List<CaptionGroup> groups=new List<CaptionGroup>();
        public void Clear() { groups.Clear(); }
        public static string Normalize(string text)
        { return Regex.Replace((text??"").ToLowerInvariant(),@"[^\p{L}\p{Nd}]+"," ").Trim(); }
        public static bool Related(string first,string second)
        {
            string a=Normalize(first),b=Normalize(second);
            if(a.Length==0 || b.Length==0) return false;
            if(a==b || a.StartsWith(b,StringComparison.Ordinal) || b.StartsWith(a,StringComparison.Ordinal)) return true;
            string[] aw=a.Split(' '),bw=b.Split(' '); int words=0;
            while(words<Math.Min(6,Math.Min(aw.Length,bw.Length)) && aw[words]==bw[words]) words++;
            if(words>=4) return true;
            int common=0,minimum=Math.Min(a.Length,b.Length);
            while(common<minimum && a[common]==b[common]) common++;
            return common>=18 && common>=minimum*.60;
        }
        public static string Preferred(string previous,string current)
        { return Normalize(current).Length>=Normalize(previous).Length-12?current:previous; }
        public CaptionGroup Observe(string source,string text,string speaker,DateTime time)
        {
            for(int i=groups.Count-1;i>=Math.Max(0,groups.Count-10);i--) {
                var item=groups[i]; double age=(time-item.Updated).TotalSeconds;
                bool sameSpeaker=String.IsNullOrWhiteSpace(item.Speaker)||String.IsNullOrWhiteSpace(speaker)||String.Equals(item.Speaker,speaker,StringComparison.OrdinalIgnoreCase);
                if(age<0 || age>30 || !sameSpeaker || !Related(item.Text,text)) continue;
                item.Text=Preferred(item.Text,text); if(!String.IsNullOrWhiteSpace(speaker)) item.Speaker=speaker;
                item.Updated=time; return item;
            }
            var next=new CaptionGroup { Id="utterance-"+Guid.NewGuid().ToString("N"),Text=text,Speaker=speaker,Started=time,Updated=time };
            groups.Add(next); if(groups.Count>20) groups.RemoveAt(0); return next;
        }
    }
}

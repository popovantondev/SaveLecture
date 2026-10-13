using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SaveLecture
{
    public sealed class SlideSelection
    {
        private sealed class Selected
        {
            public string Path;
            public byte[] Feature;
            public readonly List<string> Earlier = new List<string>();
        }
        private readonly List<Selected> selected = new List<Selected>();

        public void Reset() { lock(selected) selected.Clear(); }

        public static bool IsMoreComplete(byte[] previous, byte[] current)
        {
            if (previous == null || current == null || previous.Length != current.Length) return false;
            if(FrameChanges.Occludes(current,previous)) return true;
            if(FrameChanges.Occludes(previous,current)) return false;
            if (MoreCompleteRegion(previous,current,0,0,240,135)) return true;
            // A dark drawing board has bright application chrome. Compare its canvas
            // separately so toolbar tools and pointers do not prevent consolidation.
            return DarkBoard(previous) && DarkBoard(current) && MoreCompleteRegion(previous,current,20,10,220,120);
        }

        static bool DarkBoard(byte[] image)
        {
            if(image.Length!=240*135) return false;
            int header=0, dark=0;
            for(int y=0;y<5;y++) for(int x=20;x<220;x++) if(image[y*240+x]>100) header++;
            for(int y=20;y<110;y++) for(int x=30;x<210;x++) if(image[y*240+x]<40) dark++;
            return header>=650 && dark>=14580;
        }

        static bool MoreCompleteRegion(byte[] previous,byte[] current,int left,int top,int right,int bottom)
        {
            if(previous.Length!=240*135) return false;
            int[] firstHistogram = new int[256], secondHistogram = new int[256];
            for(int y=top;y<bottom;y++) for(int x=left;x<right;x++) {
                int p=y*240+x; firstHistogram[previous[p]]++; secondHistogram[current[p]]++;
            }
            int firstBackground = 0, secondBackground = 0;
            for (int i = 1; i < 256; i++)
            {
                if (firstHistogram[i] > firstHistogram[firstBackground]) firstBackground = i;
                if (secondHistogram[i] > secondHistogram[secondBackground]) secondBackground = i;
            }
            if (Math.Abs(firstBackground - secondBackground) > 15) return false;
            int oldInk = 0, retained = 0, newInk = 0;
            for (int y=top;y<bottom;y++) for(int x=left;x<right;x++)
            {
                int i=y*240+x;
                bool oldContent = Math.Abs(previous[i] - firstBackground) > 35;
                bool newContent = Math.Abs(current[i] - secondBackground) > 35;
                if (newContent) newInk++;
                if (oldContent)
                {
                    oldInk++;
                    if (newContent && Math.Abs(previous[i] - current[i]) < 35) retained++;
                }
            }
            // Conservative adaptation of SlideFilter's latest representative rule:
            // only supersede when the existing marks remain and new marks were added.
            return oldInk >= 10 && retained >= oldInk * 0.998 && newInk >= oldInk + 4;
        }

        public void Add(string path, byte[] feature)
        {
          lock(selected) {
            // Re-observing an archived predecessor must never promote its old path.
            foreach(var item in selected) if(item.Earlier.Contains(path)) return;
            var superseded=new List<Selected>();
            foreach(var item in selected) if(IsMoreComplete(item.Feature,feature)) superseded.Add(item);
            Selected existing=selected.Find(delegate(Selected item) { return item.Path==path; });
            if(superseded.Count>0) {
                Selected representative=existing??superseded[0];
                foreach(var item in superseded) {
                    if(!representative.Earlier.Contains(item.Path)) representative.Earlier.Add(item.Path);
                    if(item!=representative) {
                        foreach(string earlier in item.Earlier) if(!representative.Earlier.Contains(earlier)) representative.Earlier.Add(earlier);
                        selected.Remove(item);
                    }
                }
                representative.Earlier.RemoveAll(delegate(string earlier) { return earlier==path; });
                representative.Path=path; representative.Feature=feature;
                return;
            }
            if(existing!=null) return;
            for (int i = selected.Count - 1; i >= 0; i--)
            {
                Selected item = selected[i];
                if (IsMoreComplete(feature, item.Feature)) { item.Earlier.Add(path); return; }
            }
            selected.Add(new Selected { Path = path, Feature = feature });
          }
        }

        public string[] EarlierVersions(string path)
        {
            lock(selected) {
                foreach(var item in selected) if(item.Path==path) return item.Earlier.ToArray();
                return new string[0];
            }
        }

        public string Representative(string path)
        {
            lock(selected) {
                foreach(var item in selected) if(item.Path==path || item.Earlier.Contains(path)) return item.Path;
                return path;
            }
        }

        public int Save(string directory,Action movedImage=null)
        {
          lock(selected) {
            var locations=new SlideRelocations(directory);
            int moved=0;
            foreach(var item in selected) foreach(string earlier in item.Earlier) if(locations.MoveEarlier(earlier)) { moved++; if(movedImage!=null) movedImage(); }
            directory = Path.Combine(directory, "_service");
            Directory.CreateDirectory(directory);
            var items = new List<object>();
            foreach (Selected item in selected)
            {
                var earlier = new List<string>();
                foreach (string path in item.Earlier) earlier.Add(locations.Relative(locations.Resolve(path)));
                items.Add(new { image = locations.Relative(locations.Resolve(item.Path)), earlier_versions = earlier });
            }
            string target = Path.Combine(directory, "slides_best.json");
            string temporary = target + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(new { rule = "latest_more_complete_preserving_existing_content", items = items }));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            return moved;
          }
        }
    }
}

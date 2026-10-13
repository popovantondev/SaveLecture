using System;

namespace SaveLecture
{
    public static class FrameChanges
    {
        // Direction matters: a flat rectangular dialog can cover existing marks.
        // Its disappearance must not be discarded as the same transient change.
        public static bool Occludes(byte[] clean, byte[] covered)
        {
            if(clean==null || covered==null || clean.Length!=240*135 || covered.Length!=clean.Length) return false;
            var changed=new bool[clean.Length]; var visited=new bool[clean.Length];
            int total=0;
            for(int i=0;i<clean.Length;i++) if(Math.Abs(clean[i]-covered[i])>30) { changed[i]=true; total++; }
            if(total<130 || total>clean.Length*.25) return false;
            var queue=new int[clean.Length];
            int explained=0,explainedArea=0; bool dialog=false;
            for(int start=0;start<changed.Length;start++) {
                if(!changed[start] || visited[start]) continue;
                int head=0,tail=1,left=239,right=0,top=134,bottom=0; queue[0]=start; visited[start]=true;
                while(head<tail) {
                    int p=queue[head++],x=p%240,y=p/240;
                    left=Math.Min(left,x); right=Math.Max(right,x); top=Math.Min(top,y); bottom=Math.Max(bottom,y);
                    for(int yy=Math.Max(0,y-2);yy<=Math.Min(134,y+2);yy++) for(int xx=Math.Max(0,x-2);xx<=Math.Min(239,x+2);xx++) {
                        int next=yy*240+xx;
                        if(changed[next]&&!visited[next]) { visited[next]=true; queue[tail++]=next; }
                    }
                }
                int width=right-left+1,height=bottom-top+1,area=width*height;
                if(width<16 || height<10 || area<130 || area>clean.Length*.25 || tail<area*.40) continue;
                var histogram=new int[256];
                for(int y=top;y<=bottom;y++) for(int x=left;x<=right;x++) histogram[covered[y*240+x]]++;
                int mode=0; for(int i=1;i<256;i++) if(histogram[i]>histogram[mode]) mode=i;
                // Restrict automatic replacement to light dialog/panel surfaces.
                if(mode<90) continue;
                int flat=0,oldFlat=0,edge=0,edgeFlat=0;
                for(int y=top;y<=bottom;y++) for(int x=left;x<=right;x++) {
                    int p=y*240+x; bool uniform=Math.Abs(covered[p]-mode)<22;
                    if(uniform) flat++;
                    if(Math.Abs(clean[p]-mode)<22) oldFlat++;
                    if(x<=left+1 || x>=right-1 || y<=top+1 || y>=bottom-1) { edge++; if(uniform) edgeFlat++; }
                }
                bool panel=left>168 && right>210 && width<45 && height>70 && flat>area*.32 && flat-oldFlat>area*.15;
                bool popup=flat>area*.45 && flat<area*.94 && flat-oldFlat>area*.30 && edgeFlat>edge*.35;
                if(panel || popup) { explained+=tail; explainedArea+=area; if(popup) dialog=true; }
            }
            return dialog && total-explained<(clean.Length-explainedArea)*.035;
        }

        public static bool Transient(byte[] before, byte[] after)
        {
            if (before == null || after == null || before.Length != after.Length) return false;
            int count = 0, positive = 0, negative = 0, minX = 240, maxX = 0, minY = 135, maxY = 0;
            double a = 0, b = 0, aa = 0, bb = 0, ab = 0;
            for (int i = 0; i < before.Length; i++)
            {
                double x = before[i], y = after[i];
                a += x; b += y; aa += x*x; bb += y*y; ab += x*y;
                int delta = after[i] - before[i];
                if (Math.Abs(delta) <= 30) continue;
                count++; if (delta > 0) positive++; else negative++;
                int px = i % 240, py = i / 240;
                minX = Math.Min(minX, px); maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
            }
            if (count == 0) return true; // Minor video compression noise.
            int area = (maxX-minX+1)*(maxY-minY+1);
            // A moving pointer removes its old mark and adds another mark elsewhere.
            // One-sided additions such as new table text are deliberately retained.
            if (count <= 100 && positive >= count*0.30 && negative >= count*0.30 && (maxX-minX >= 6 || maxY-minY >= 6))
            {
                int occupied = 0;
                for (int cx = minX; cx <= maxX; cx++)
                {
                    bool used = false;
                    for (int cy = minY; cy <= maxY; cy++) if (Math.Abs(after[cy*240+cx]-before[cy*240+cx]) > 30) { used = true; break; }
                    if (used) occupied++;
                }
                if (maxX-minX >= maxY-minY && occupied < (maxX-minX+1)*0.65) return true;
                if (maxY-minY > maxX-minX)
                {
                    occupied = 0;
                    for (int cy = minY; cy <= maxY; cy++)
                        for (int cx = minX; cx <= maxX; cx++)
                            if (Math.Abs(after[cy*240+cx]-before[cy*240+cx]) > 30) { occupied++; break; }
                    if (occupied < (maxY-minY+1)*0.65) return true;
                }
            }
            double n = before.Length;
            double denominator = Math.Sqrt(Math.Max(0, (aa-a*a/n)*(bb-b*b/n)));
            double correlation = denominator > 0 ? (ab-a*b/n)/denominator : 0;
            // Geometry thresholds from SlideFilter.looks_like_local_overlay.
            return correlation >= 0.70 && count >= n*0.0015 && count < n*0.20 && area >= n*0.004 && area < n*0.38 && count > area*0.24;
        }
    }
}

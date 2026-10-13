using System;

namespace SaveLecture
{
    // Native adaptation of SlideFilter comparison_score, no subprocess per frame.
    public static class LiveSlideFilter
    {
        public static double DuplicateThreshold = 0.89;
        public static double StableThreshold = 0.94;
        public static int MaxShift = 12;
        static double Correlation(double a, double b, double aa, double bb, double ab, int n)
        {
            double denominator = Math.Sqrt(Math.Max(0, (aa-a*a/n)*(bb-b*b/n)));
            return denominator < 1e-9 ? (Math.Abs(a-b)/n < 2.55 ? 1 : 0) : Math.Max(-1, Math.Min(1, (ab-a*b/n)/denominator));
        }
        static double[] Edges(byte[] a)
        {
            var edges = new double[a.Length];
            for (int y=0;y<135;y++) for(int x=0;x<240;x++)
            {
                int i=y*240+x; double gx=a[y*240+Math.Min(239,x+1)]-a[i], gy=a[Math.Min(134,y+1)*240+x]-a[i];
                edges[i]=Math.Sqrt(gx*gx+gy*gy);
            }
            return edges;
        }
        static double Evaluate(byte[] first, byte[] second, double[] fe, double[] se, int dx, int dy)
        {
            double a=0,b=0,aa=0,bb=0,ab=0, ea=0,eb=0,eaa=0,ebb=0,eab=0;
            int n=0; var histogram=new int[256];
            for(int y=Math.Max(0,-dy);y<Math.Min(135,135-dy);y++)
                for(int x=Math.Max(0,-dx);x<Math.Min(240,240-dx);x++)
                {
                    int i=y*240+x,j=(y+dy)*240+x+dx;
                    double av=first[i],bv=second[j], ev=fe[i],fv=se[j];
                    a+=av;b+=bv;aa+=av*av;bb+=bv*bv;ab+=av*bv;
                    ea+=ev;eb+=fv;eaa+=ev*ev;ebb+=fv*fv;eab+=ev*fv;
                    histogram[Math.Abs(first[i]-second[j])]++;n++;
                }
            int accumulated=0,limit=0;
            for(;limit<255;limit++) { accumulated+=histogram[limit]; if(accumulated>=n*.88) break; }
            return .5*((Correlation(a,b,aa,bb,ab,n)+1)/2)+.3*((Correlation(ea,eb,eaa,ebb,eab,n)+1)/2)+.2*Math.Max(0,1-limit/(255*.34));
        }
        public static double Score(byte[] first, byte[] second)
        {
            if(first==null || second==null) return 0;
            double[] fe=Edges(first),se=Edges(second);
            double best=Evaluate(first,second,fe,se,0,0);
            if(best>=.995) return best;
            int bx=0,by=0;
            int shift=Math.Max(0,Math.Min(24,MaxShift));
            for(int dy=-shift;dy<=shift;dy+=2) for(int dx=-shift;dx<=shift;dx+=2)
            {
                double value=Evaluate(first,second,fe,se,dx,dy);
                if(value>best) { best=value;bx=dx;by=dy; }
            }
            int cx=bx,cy=by;
            for(int dy=Math.Max(-shift,cy-1);dy<=Math.Min(shift,cy+1);dy++) for(int dx=Math.Max(-shift,cx-1);dx<=Math.Min(shift,cx+1);dx++)
                best=Math.Max(best,Evaluate(first,second,fe,se,dx,dy));
            return best;
        }
        public static bool Duplicate(byte[] first, byte[] second)
        {
            if(FrameChanges.Occludes(second,first)) return false;
            if (SlideArchive.Similar(first,second) || FrameChanges.Transient(first,second)) return true;
            // Keep real additions and removals even if the robust score is high.
            if (SlideSelection.IsMoreComplete(first,second) || SlideSelection.IsMoreComplete(second,first)) return false;
            return Score(first,second)>=DuplicateThreshold;
        }
    }
}

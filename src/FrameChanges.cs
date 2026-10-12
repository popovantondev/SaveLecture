using System;

namespace SaveLecture
{
    public static class FrameChanges
    {
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

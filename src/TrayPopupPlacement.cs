using System;
using System.Drawing;

namespace SaveLecture
{
    internal static class TrayPopupPlacement
    {
        public static Point Near(Point anchor,Rectangle workArea,Size menu)
        {
            const int gap=8;
            int x=anchor.X-menu.Width/2;
            int y=anchor.Y-menu.Height-gap;
            if(anchor.Y<=workArea.Top) y=workArea.Top+gap;
            else if(anchor.X<=workArea.Left) { x=workArea.Left+gap; y=anchor.Y-menu.Height/2; }
            else if(anchor.X>=workArea.Right) { x=workArea.Right-menu.Width-gap; y=anchor.Y-menu.Height/2; }
            x=Math.Max(workArea.Left+gap,Math.Min(x,workArea.Right-menu.Width-gap));
            y=Math.Max(workArea.Top+gap,Math.Min(y,workArea.Bottom-menu.Height-gap));
            return new Point(x,y);
        }
    }
}

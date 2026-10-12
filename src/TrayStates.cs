using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;

namespace SaveLecture
{
    public sealed class TrayStates : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
        private readonly Icon[] icons = new Icon[17];
        public TrayStates()
        {
            for (int i = 0; i < icons.Length; i++)
                icons[i] = CreateIcon(i);
        }
        private static Icon CreateIcon(int state)
        {
            int[] sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };
            byte[][] images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
                using (Bitmap bitmap = Render(state, sizes[i]))
                using (var buffer = new MemoryStream())
                { bitmap.Save(buffer, System.Drawing.Imaging.ImageFormat.Png); images[i] = buffer.ToArray(); }
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + sizes.Length * 16;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
                }
                foreach (byte[] bytes in images) writer.Write(bytes);
                buffer.Position = 0;
                using (var icon = new Icon(buffer, 32, 32)) return (Icon)icon.Clone();
            }
        }
        public Icon this[int state] { get { return icons[state]; } }
        public static Bitmap Render(int state, int size)
        {
            using (Bitmap large = Draw(state, size * 4))
            {
                var result = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(large, new Rectangle(0, 0, size, size), 0, 0, large.Width, large.Height, GraphicsUnit.Pixel);
                }
                return result;
            }
        }
        private static Bitmap Draw(int state, int size)
        {
            var bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.ScaleTransform(size / 32f, size / 32f);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var outline = new Pen(Color.FromArgb(225, 15, 31, 48), 1.5f))
                using (var fill = new SolidBrush(state == 1 ? Color.FromArgb(236,172,54) : state == 2 ? Color.FromArgb(75, 112, 148) : state == 3 ? Color.FromArgb(255, 132, 55) : Color.FromArgb(36, 184, 235)))
                {
                    if(state>=5) {
                        using(var track=new Pen(Color.FromArgb(85,151,105,230),3.5f)) g.DrawEllipse(track,5,5,22,22);
                        using(var arc=new Pen(Color.FromArgb(151,105,230),3.5f)) {
                            arc.StartCap=arc.EndCap=LineCap.Round;
                            g.DrawArc(arc,5,5,22,22,(state-5)*30-90,115);
                        }
                    }
                    else if (state == 1)
                    {
                        g.FillRectangle(fill, 5, 3, 8, 26); g.DrawRectangle(outline, 5, 3, 8, 26);
                        g.FillRectangle(fill, 19, 3, 8, 26); g.DrawRectangle(outline, 19, 3, 8, 26);
                    }
                    else if (state == 2) { g.FillRectangle(fill, 3, 3, 26, 26); g.DrawRectangle(outline, 3, 3, 26, 26); }
                    else if (state == 3)
                    {
                        g.FillEllipse(fill, 1, 1, 30, 30); g.DrawEllipse(outline, 1, 1, 30, 30);
                        using (var white = new Pen(Color.White, 3.5f)) { white.StartCap = white.EndCap = LineCap.Round; g.DrawLine(white, 16, 7, 16, 18); }
                        g.FillEllipse(Brushes.White, 14, 23, 4, 4);
                    }
                    else
                    {
                        PointF[] points = new[] { new PointF(4, 2), new PointF(30, 16), new PointF(4, 30) };
                        g.FillPolygon(fill, points); g.DrawPolygon(outline, points);
                    }
                }
            }
            return bitmap;
        }
        public void Dispose() { foreach (Icon icon in icons) icon.Dispose(); }
    }
}

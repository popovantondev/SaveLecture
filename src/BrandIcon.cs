using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace SaveLecture
{
    public static class BrandIcon
    {
        public static Bitmap Render(int size)
        {
            var visual=new System.Windows.Media.DrawingVisual();
            using(var dc=visual.RenderOpen()) dc.DrawImage(BrandDrawing.Image(),new System.Windows.Rect(0,0,size,size));
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap(size,size,96,96,System.Windows.Media.PixelFormats.Pbgra32); image.Render(visual);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using(var stream=new MemoryStream()) { encoder.Save(stream); stream.Position=0; using(var bitmap=new Bitmap(stream)) return new Bitmap(bitmap); }
        }
        public static void Export(string root)
        {
            int[] sizes={16,20,24,32,48,64,128,256}; var images=new byte[sizes.Length][];
            for(int i=0;i<sizes.Length;i++) using(var bitmap=Render(sizes[i])) using(var stream=new MemoryStream())
            { bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png); images[i]=stream.ToArray(); if(sizes[i]==256) File.WriteAllBytes(Path.Combine(root,"assets","brand.png"),images[i]); }
            using(var writer=new BinaryWriter(File.Create(Path.Combine(root,"SaveLecture.ico"))))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); int offset=6+16*sizes.Length;
                for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length; }
                foreach(var bytes in images) writer.Write(bytes);
            }
        }
    }
}

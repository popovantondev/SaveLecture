using System;
using System.Windows;
using System.Windows.Media;

namespace SaveLecture
{
    // The selected Teams/document mark, drawn from curves at the requested size.
    internal static class BrandDrawing
    {
        static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
        static void Shape(DrawingContext dc,string data,Brush fill,Pen outline)
        { dc.DrawGeometry(fill,outline,Geometry.Parse(data)); }
        public static DrawingImage Image()
        {
            var drawing=new DrawingGroup();
            using(var dc=drawing.Open())
            {
                var blue=new LinearGradientBrush(System.Windows.Media.Color.FromRgb(0,112,238),System.Windows.Media.Color.FromRgb(0,84,201),45);
                var violet=new LinearGradientBrush(System.Windows.Media.Color.FromRgb(139,128,255),System.Windows.Media.Color.FromRgb(98,89,224),45);
                var purple=Color("#4942C3"); var white=new Pen(Brushes.White,3.2) { LineJoin=PenLineJoin.Round };
                dc.DrawRoundedRectangle(blue,null,new Rect(0,0,100,100),16,16);
                dc.DrawEllipse(violet,white,new Point(50,30),13,13);
                dc.DrawEllipse(purple,white,new Point(72,35),10,10);
                Shape(dc,"M61,44 L74,44 Q82,44 82,53 L82,63 Q82,73 72,73 L61,73 Z",purple,white);
                Shape(dc,"M31,44 L57,44 Q64,44 64,52 L64,65 C64,87 35,88 31,66 Z",violet,white);
                dc.DrawRoundedRectangle(Color("#413AB4"),white,new Rect(12,34,37,37),5,5);
                Shape(dc,"M22,43 L39,43 L39,48 L33,48 L33,64 L28,64 L28,48 L22,48 Z",Brushes.White,null);
                Shape(dc,"M69,59 L83,59 L91,67 L91,85 Q91,89 87,89 L69,89 Q65,89 65,85 L65,63 Q65,59 69,59 Z",Brushes.White,null);
                Shape(dc,"M81,61 L81,67 Q81,69 83,69 L89,69 Z",Color("#0064DA"),null);
                var line=new Pen(Color("#0064DA"),3.5) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round };
                dc.DrawLine(line,new Point(71,74),new Point(85,74)); dc.DrawLine(line,new Point(71,81),new Point(83,81));
            }
            drawing.Freeze(); var image=new DrawingImage(drawing); image.Freeze(); return image;
        }
    }
}

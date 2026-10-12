using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SaveLecture
{
    internal static class UiGlyph
    {
        static readonly Dictionary<string,string> paths=new Dictionary<string,string> {
            {"back","M22,5 L11,16 L22,27"},
            {"forward","M11,5 L22,16 L11,27"},
            {"plus","M4,16 L28,16 M16,4 L16,28"},
            {"minus","M5,16 L27,16"},
            {"maximize","M6,6 L26,6 L26,26 L6,26 Z"},
            {"restore","M4,10 L22,10 L22,28 L4,28 Z M10,10 L10,4 L28,4 L28,22 L22,22"},
            {"close","M7,7 L25,25 M25,7 L7,25"},
            {"check","M4,17 L12,25 L29,7"},
            {"stop","M5,5 L27,5 L27,27 L5,27 Z"},
            {"play","M7,3 L29,16 L7,29 Z"},
            {"doc","M7,2 L22,2 L29,9 L29,31 L7,31 Z M22,2 L22,9 L29,9 M12,14 L23,14 M12,19 L23,19 M12,24 L19,24"},
            {"image","M3,4 L29,4 L29,28 L3,28 Z M3,23 L11,15 L18,22 L24,16 L29,21 M9,11 A2,2 0 1 0 13,11 A2,2 0 1 0 9,11"},
            {"stats","M5,28 L5,18 M13,28 L13,5 M21,28 L21,12 M29,28 L29,8"},
            {"crop","M3,10 L3,3 L10,3 M24,3 L31,3 L31,10 M31,24 L31,31 L24,31 M10,31 L3,31 L3,24"},
            {"clock","M16,2 A14,14 0 1 0 16,30 A14,14 0 1 0 16,2 M16,7 L16,16 L23,16"},
            {"archive","M3,4 L29,4 L29,11 L3,11 Z M5,11 L5,29 L27,29 L27,11 M12,17 L20,17"},
            {"copy","M10,9 L29,9 L29,29 L10,29 Z M4,23 L4,3 L23,3"},
            {"layers","M2,10 L16,2 L30,10 L16,18 Z M2,17 L16,25 L30,17 M2,24 L16,32 L30,24"},
            {"video","M2,6 L23,6 L23,27 L2,27 Z M23,12 L31,7 L31,26 L23,21"},
            {"open","M18,3 L30,3 L30,15 M30,3 L14,19 M11,5 L3,5 L3,29 L27,29 L27,21"},
            {"exit","M15,3 L3,3 L3,29 L15,29 M12,16 L31,16 M23,8 L31,16 L23,24"},
            {"pause","M11,5 L11,27 M23,5 L23,27"},
            {"gear","M13,2 L19,2 L20,7 L24,9 L28,7 L31,12 L27,16 L27,19 L31,22 L28,27 L23,25 L20,27 L19,32 L13,32 L12,27 L8,25 L4,27 L1,22 L5,19 L5,15 L1,12 L4,7 L8,9 L12,7 Z M16,11 A6,6 0 1 0 16,23 A6,6 0 1 0 16,11"},
            {"sliders","M3,7 L9,7 M17,7 L31,7 M3,17 L19,17 M27,17 L31,17 M3,27 L9,27 M17,27 L31,27 M9,3 L17,3 L17,11 L9,11 Z M19,13 L27,13 L27,21 L19,21 Z M9,23 L17,23 L17,31 L9,31 Z"},
            {"database","M3,7 C3,0 29,0 29,7 C29,14 3,14 3,7 L3,25 C3,32 29,32 29,25 L29,7 M3,16 C3,23 29,23 29,16"},
            {"info","M16,2 A14,14 0 1 0 16,30 A14,14 0 1 0 16,2 M16,14 L16,24 M16,8 L16,9"},
            {"palette","M16,2 C-2,2 -2,28 12,30 C21,33 19,23 24,23 C37,23 30,2 16,2 Z M8,10 L8,11 M15,7 L15,8 M23,10 L23,11 M7,19 L7,20"}
        };
        public static FrameworkElement Icon(string key,Brush color,double size)
        {
            string data; if(!paths.TryGetValue(key,out data)) data=paths["doc"];
            size=Math.Round(size);
            var canvas=new Canvas { Width=size,Height=size,UseLayoutRounding=true };
            var geometry=Geometry.Parse(data).Clone(); var bounds=geometry.Bounds;
            double ratio=size/34;
            var transform=new TransformGroup(); transform.Children.Add(new TranslateTransform(-bounds.X-bounds.Width/2,-bounds.Y-bounds.Height/2)); transform.Children.Add(new ScaleTransform(ratio,ratio)); transform.Children.Add(new TranslateTransform(size/2,size/2));
            geometry.Transform=transform;
            canvas.Children.Add(new Path { Data=geometry,Fill=key=="play"||key=="stop"?color:null,Stroke=color,StrokeThickness=key=="pause"?3:key=="sliders"?1.3:2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round });
            return new Viewbox { Tag=key,Width=size,Height=size,Child=canvas,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center };
        }
        public static void BindForeground(FrameworkElement icon)
        {
            var canvas=(Canvas)((Viewbox)icon).Child;
            ((Path)canvas.Children[0]).SetBinding(Path.StrokeProperty,new System.Windows.Data.Binding("Foreground") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1) });
        }
        public static StackPanel Content(string label,string icon,Brush color,double size=20)
        {
            var panel=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            panel.HorizontalAlignment=HorizontalAlignment.Center;
            var image=Icon(icon,color,size); image.VerticalAlignment=VerticalAlignment.Center; image.Margin=new Thickness(0,0,12,0); panel.Children.Add(image);
            var text=new TextBlock { Text=label,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,Padding=new Thickness(0) };
            text.RenderTransform=new TranslateTransform(0,-1);
            text.SetBinding(TextBlock.FontSizeProperty,new System.Windows.Data.Binding("FontSize") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Button),1) });
            panel.Children.Add(text); return panel;
        }
        public static void ReplaceGlyphs(DependencyObject root)
        {
            var list=new List<TextBlock>(); Collect(root,list);
            var names=new Dictionary<string,string> { {"▤","doc"},{"▧","image"},{"▥","stats"},{"▣","archive"},{"◷","clock"},{"⚙","gear"},{"◇","layers"},{"▻","video"},{"↗","open"},{"↪","exit"},{"Ⅱ","pause"},{"▱","database"},{"◉","palette"},{"ⓘ","info"},{"☷","sliders"},{"⌗","crop"} };
            foreach(var text in list)
            {
                if(String.IsNullOrEmpty(text.Text)) continue;
                string key; if(!names.TryGetValue(text.Text.Substring(0,1),out key)) continue;
                var parent=VisualTreeHelper.GetParent(text) as Panel; if(parent==null) continue;
                string label=text.Text.Substring(1).Trim();
                if(label=="Дубликаты") key="copy";
                Brush color=label.Length>0 && text.FontWeight==FontWeights.Bold ? new SolidColorBrush(Color.FromRgb(0,131,255)) : text.Foreground;
                if(label=="Текст" || label=="Слайды" || label=="Дубликаты") color=new SolidColorBrush(Color.FromRgb(36,72,111));
                bool large=text.FontWeight==FontWeights.Bold || label=="Текст" || label=="Слайды" || label=="Дубликаты";
                FrameworkElement replacement=label.Length==0?Icon(key,color,Math.Max(20,text.FontSize)):Content(label,key,color,large?26:18);
                var content=replacement as StackPanel;
                if(content!=null) foreach(UIElement child in content.Children)
                {
                    var visual=child as Viewbox; if(visual!=null) visual.Margin=new Thickness(0,0,large?24:12,0);
                    var caption=child as TextBlock;
                    if(caption!=null) { caption.FontSize=text.FontSize; caption.FontWeight=text.FontWeight; caption.Foreground=text.Foreground; }
                }
                replacement.Margin=text.Margin; replacement.VerticalAlignment=text.VerticalAlignment; replacement.HorizontalAlignment=label.Length==0?HorizontalAlignment.Left:text.HorizontalAlignment;
                Grid.SetRow(replacement,Grid.GetRow(text)); Grid.SetColumn(replacement,Grid.GetColumn(text));
                int index=parent.Children.IndexOf(text); parent.Children.RemoveAt(index); parent.Children.Insert(index,replacement);
            }
        }
        static void Collect(DependencyObject root,List<TextBlock> result)
        {
            var text=root as TextBlock; if(text!=null) result.Add(text);
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) Collect(VisualTreeHelper.GetChild(root,i),result);
        }
    }
}

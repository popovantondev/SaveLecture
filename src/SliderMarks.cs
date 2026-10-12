using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SaveLecture
{
    internal sealed class SliderMarks : Canvas
    {
        readonly Slider slider;
        readonly double[] values;
        readonly TextBlock[] labels;
        readonly Border[] ticks;
        internal SliderMarks(Slider owner,double[] stops,string format)
        {
            slider=owner; values=stops; Height=30; Margin=new Thickness(0,0,0,4);
            labels=new TextBlock[stops.Length]; ticks=new Border[stops.Length];
            for(int i=0;i<stops.Length;i++) {
                var label=new TextBlock { Text=UiText.Get(stops[i].ToString(format,UiText.Culture)),FontSize=14,Tag="Readable",Foreground=new SolidColorBrush(Color.FromRgb(83,108,143)) };
                labels[i]=label; Children.Add(label);
                ticks[i]=new Border { Width=1,Height=4,Background=new SolidColorBrush(Color.FromRgb(167,196,227)) }; Children.Add(ticks[i]);
            }
            SizeChanged+=delegate { Position(); }; slider.SizeChanged+=delegate { Position(); }; Loaded+=delegate { Position(); };
        }
        void Position()
        {
            if(ActualWidth<=0) return;
            double start=17,end=Math.Max(start,ActualWidth-17);
            var track=slider.Template==null?null:slider.Template.FindName("PART_Track",slider) as Track;
            if(track!=null && track.ActualWidth>0 && track.Thumb!=null) {
                double left=track.TransformToAncestor(slider).Transform(new Point(0,0)).X;
                start=left+track.Thumb.ActualWidth/2; end=left+track.ActualWidth-track.Thumb.ActualWidth/2;
            }
            for(int i=0;i<values.Length;i++) {
                double x=start+(end-start)*(values[i]-slider.Minimum)/(slider.Maximum-slider.Minimum);
                labels[i].Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
                SetLeft(labels[i],Math.Max(0,Math.Min(ActualWidth-labels[i].DesiredSize.Width,x-labels[i].DesiredSize.Width/2))); SetTop(labels[i],5);
                SetLeft(ticks[i],x); SetTop(ticks[i],0);
            }
        }
    }
}

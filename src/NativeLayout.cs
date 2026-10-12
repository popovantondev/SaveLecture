using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SaveLecture
{
    // Adjust measured sizes, leaving text rendering and the visual transform at native DPI.
    internal sealed class NativeLayout
    {
        sealed class Measure
        {
            public DependencyObject Owner; public DependencyProperty Property; public object Value;
            public void Apply(double density)
            {
                object value=Value;
                if(value is double) {
                    double n=(double)value; if(Double.IsNaN(n) || Double.IsInfinity(n)) return;
                    double scaled=Math.Round(n*density);
                    if(Property==Control.FontSizeProperty || Property==TextBlock.FontSizeProperty || Property==TextBlock.LineHeightProperty) {
                        var element=Owner as FrameworkElement;
                        scaled=Math.Max(element!=null && Equals(element.Tag,"Readable")?14:1,scaled);
                    }
                    value=scaled;
                }
                else if(value is Thickness) { var t=(Thickness)value; value=new Thickness(Math.Round(t.Left*density),Math.Round(t.Top*density),Math.Round(t.Right*density),Math.Round(t.Bottom*density)); }
                else if(value is GridLength) { var g=(GridLength)value; if(!g.IsAbsolute) return; value=new GridLength(Math.Round(g.Value*density)); }
                Owner.SetCurrentValue(Property,value);
            }
        }
        readonly HashSet<DependencyObject> known=new HashSet<DependencyObject>();
        readonly HashSet<DependencyObject> visible=new HashSet<DependencyObject>();
        readonly List<Measure> measures=new List<Measure>();
        double previous=Double.NaN;
        void Remember(DependencyObject element,DependencyProperty property)
        { measures.Add(new Measure { Owner=element,Property=property,Value=element.GetValue(property) }); }
        void Collect(DependencyObject element)
        {
            visible.Add(element);
            var view=element as FrameworkElement;
            var gridOwner=element as Grid;
            if(gridOwner!=null) { foreach(var row in gridOwner.RowDefinitions) visible.Add(row); foreach(var column in gridOwner.ColumnDefinitions) visible.Add(column); }
            if(view!=null && view.TemplatedParent==null && known.Add(element))
            {
                Remember(element,FrameworkElement.WidthProperty); Remember(element,FrameworkElement.HeightProperty); Remember(element,FrameworkElement.MarginProperty);
                var control=element as Control;
                if(control!=null) { Remember(element,Control.PaddingProperty); Remember(element,Control.FontSizeProperty); }
                var text=element as TextBlock;
                if(text!=null) {
                    if(!BindingOperations.IsDataBound(text,TextBlock.FontSizeProperty)) Remember(element,TextBlock.FontSizeProperty);
                    Remember(element,TextBlock.LineHeightProperty); Remember(element,TextBlock.PaddingProperty);
                }
                var border=element as Border; if(border!=null) Remember(element,Border.PaddingProperty);
                var grid=element as Grid;
                if(grid!=null) {
                    foreach(var row in grid.RowDefinitions) Remember(row,RowDefinition.HeightProperty);
                    foreach(var column in grid.ColumnDefinitions) Remember(column,ColumnDefinition.WidthProperty);
                }
            }
            // Vector drawings may resize; do not change their internal coordinate system.
            if(element is Viewbox || element is System.Windows.Controls.Image) return;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++) Collect(VisualTreeHelper.GetChild(element,i));
        }
        public void Update(FrameworkElement view)
        {
            // Minimize/restore can briefly arrange an ElementHost at 1 x 1.
            // Keep the last usable layout until a real viewport is available.
            if(view.ActualWidth<100 || view.ActualHeight<100) return;
            double density=Math.Min(1,Math.Min(view.ActualWidth/1100,view.ActualHeight/800));
            visible.Clear(); int start=measures.Count; Collect(view);
            // Caption rows and dynamic settings pages are replaced during normal use.
            if(known.RemoveWhere(delegate(DependencyObject item) { return !visible.Contains(item); })>0) { measures.RemoveAll(delegate(Measure item) { return !visible.Contains(item.Owner); }); start=0; }
            if(Math.Abs(density-previous)>.0001 || Double.IsNaN(previous)) start=0;
            for(int i=start;i<measures.Count;i++) measures[i].Apply(density);
            previous=density;
        }
    }
}

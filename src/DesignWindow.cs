using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Forms.Integration;
using Forms = System.Windows.Forms;

namespace SaveLecture
{
    internal class DesignWindow : Forms.Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int a,ref int value,int size);
        public readonly FrameworkElement View;
        public Action ReturnRequested;
        public Forms.Form Shell;
        public Func<bool> CanReturn;
        Forms.Control home;
        DesignWindow activePage;
        NativeLayout metrics;
        public void RefreshMetrics() { UiText.Apply(View); if(metrics!=null) { View.UpdateLayout(); metrics.Update(View); } }
        public void Return() { if(CanReturn!=null && !CanReturn()) return; if(ReturnRequested!=null) ReturnRequested(); else Close(); }
        public void ShowPage(DesignWindow page,Action returned)
        {
            if(activePage==page) return;
            if(activePage!=null) { activePage.Return(); if(activePage!=null) return; }
            activePage=page;
            if(home==null) home=Controls[0];
            var host=(ElementHost)home;
            var source=(ElementHost)page.Controls[0];
            var previous=host.Child; var child=source.Child; source.Child=null;
            host.Child=child; page.Shell=this; page.UpdateWindowIcon();
            page.ReturnRequested=delegate
            {
                activePage=null; page.ReturnRequested=null; host.Child=null; source.Child=child; host.Child=previous;
                RefreshView(previous); previous.UpdateLayout(); host.Invalidate(true); host.Update(); returned();
            };
        }
        static void RefreshView(DependencyObject element)
        {
            var visual=element as UIElement;
            if(visual!=null) { visual.InvalidateMeasure(); visual.InvalidateArrange(); visual.InvalidateVisual(); }
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(element);i++)
                RefreshView(System.Windows.Media.VisualTreeHelper.GetChild(element,i));
        }
        public DesignWindow(string layout, int width, int height)
        {
            Text = "SaveLecture"; FormBorderStyle = Forms.FormBorderStyle.None;
            AutoScaleMode = Forms.AutoScaleMode.None; ClientSize = new System.Drawing.Size(width,height);
            MinimumSize = new System.Drawing.Size(width*3/4,height*3/4); StartPosition = Forms.FormStartPosition.CenterScreen;
            bool compact=layout=="main.xaml" || layout=="settings.xaml";
            bool tray=layout=="tray.xaml";
            MinimumSize=new System.Drawing.Size(compact?900:width*3/4,compact?650:height*3/4);
            var area=Forms.Screen.PrimaryScreen.WorkingArea; ClientSize=new System.Drawing.Size(Math.Min(compact?1100:width,area.Width-40),Math.Min(compact?800:height,area.Height-40));
            if(tray) { MinimumSize=new System.Drawing.Size(340,383); ClientSize=MinimumSize; }
            if(compact) MinimumSize=new System.Drawing.Size(Math.Min(900,area.Width-40),Math.Min(650,area.Height-40));
            BackColor = Color.FromArgb(246,250,255);
            Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SaveLecture.ico"),32,32);
            string text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"src","ui",layout));
            View = (FrameworkElement)XamlReader.Parse(text);
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(View,System.Windows.Input.KeyboardNavigationMode.None);
            System.Windows.Input.KeyboardNavigation.SetControlTabNavigation(View,System.Windows.Input.KeyboardNavigationMode.None);
            View.PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e) { if(e.Key==System.Windows.Input.Key.Tab) e.Handled=true; };
            View.PreviewGotKeyboardFocus+=delegate(object sender,System.Windows.Input.KeyboardFocusChangedEventArgs e) { var control=e.NewFocus as Control; if(control!=null) control.FocusVisualStyle=null; };
            System.Windows.Media.RenderOptions.SetClearTypeHint(View,System.Windows.Media.ClearTypeHint.Enabled);
            var brandImage=View.FindName("BrandImage") as System.Windows.Controls.Image;
            if(brandImage!=null) {
                brandImage.Source=BrandDrawing.Image();
                var brandFrame=brandImage.Parent as Border;
                if(brandFrame!=null) brandFrame.Background=System.Windows.Media.Brushes.Transparent;
            }
            bool prepared=false; View.Loaded+=delegate { if(!prepared) { prepared=true; UiGlyph.ReplaceGlyphs(View); } UiText.Apply(View); };
            View.Width=Double.NaN; View.Height=Double.NaN;
            var outer=View as Border;
            if(outer!=null) { outer.CornerRadius=new CornerRadius(8); outer.SizeChanged+=delegate { double radius=WindowState==Forms.FormWindowState.Maximized?0:8; outer.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,outer.ActualWidth,outer.ActualHeight),radius,radius); }; }
            View.MinWidth=0; View.MinHeight=0;
            var content=new Grid { ClipToBounds=true, Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(246,250,255)) }; content.Children.Add(View);
            PresentationSource.AddSourceChangedHandler(View,delegate {
                var source=PresentationSource.FromVisual(View) as System.Windows.Interop.HwndSource;
                if(source!=null && source.CompositionTarget!=null) source.CompositionTarget.BackgroundColor=System.Windows.Media.Color.FromRgb(246,250,255);
            });
            if(compact) {
                metrics=new NativeLayout();
                View.Loaded+=delegate { RefreshMetrics(); };
                View.SizeChanged+=delegate { RefreshMetrics(); };
            }
            Controls.Add(new ElementHost { Dock = Forms.DockStyle.Fill, BackColor=BackColor, Child = content });
            if(compact) Shown+=delegate {
                var screen=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
                Location=new System.Drawing.Point(screen.Left+(screen.Width-Width)/2,screen.Top+(screen.Height-Height)/2);
            };
            if(compact) Resize+=delegate {
                var frame=View as Border; if(frame==null) return;
                bool full=WindowState==Forms.FormWindowState.Maximized;
                frame.CornerRadius=new CornerRadius(full?0:8);
                frame.Clip=new System.Windows.Media.RectangleGeometry(new Rect(0,0,frame.ActualWidth,frame.ActualHeight),full?0:8,full?0:8);
                int rounding=full?1:2; if(IsHandleCreated) DwmSetWindowAttribute(Handle,33,ref rounding,4);
            };
            var ink=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37,63,98));
            foreach(string name in new[]{"Back","Decrease","Increase","Minimize","Maximize","Close"})
            {
                var button=Find<Button>(name); if(button==null) continue;
                string key=name=="Back"?"back":name=="Increase"?"plus":name=="Maximize"?"maximize":name=="Close"?"close":"minus";
                button.Content=UiGlyph.Icon(key,ink,name=="Back"?18:name=="Decrease"||name=="Increase"?20:24);
                if(name=="Minimize" || name=="Maximize" || name=="Close") UiGlyph.BindForeground((FrameworkElement)button.Content);
                if(name=="Decrease" || name=="Increase") button.Padding=new Thickness(0);
            }
            var done=Find<Button>("Done"); if(done!=null) done.Content=UiGlyph.Content("Готово","check",ink,22);
            foreach(string name in new[]{"CaptureHeaderIcon","FilterHeaderIcon","RecordingHeaderIcon"}) {
                var icon=Find<ContentControl>(name); if(icon!=null) icon.Content=UiGlyph.Icon(name=="CaptureHeaderIcon"?"clock":name=="FilterHeaderIcon"?"layers":"video",new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0,131,255)),26);
            }
            if(tray) {
                foreach(string name in new[]{"Open","Stop","Archive","Settings","Exit"}) {
                    var grid=Find<Button>(name).Content as Grid; if(grid==null) continue;
                    string key=name=="Open"?"open":name=="Stop"?"stop":name=="Archive"?"archive":name=="Settings"?"gear":"exit";
                    var color=name=="Open"?new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(7,131,255)):name=="Exit"?new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,70,86)):ink;
                    var glyph=UiGlyph.Icon(key,color,20); glyph.HorizontalAlignment=HorizontalAlignment.Left;
                    grid.Children.RemoveAt(0); grid.Children.Insert(0,glyph);
                    for(int i=1;i<grid.Children.Count;i++) { var textBlock=grid.Children[i] as TextBlock; if(textBlock==null || textBlock.Text!="›") continue;
                        var arrow=UiGlyph.Icon("forward",new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(125,146,177)),14); arrow.HorizontalAlignment=HorizontalAlignment.Right; grid.Children.RemoveAt(i); grid.Children.Insert(i,arrow);
                    }
                }
                SetUpTrayHover();
                TrayMenuState.Update(this,true,false,false,false,false);
            }
            Wire("Minimize", delegate { (Shell??this).WindowState = Forms.FormWindowState.Minimized; });
            Wire("Maximize", delegate {
                var window=(DesignWindow)(Shell??this); window.MaximizedBounds=Forms.Screen.FromHandle(window.Handle).WorkingArea;
                window.WindowState=window.WindowState==Forms.FormWindowState.Maximized?Forms.FormWindowState.Normal:Forms.FormWindowState.Maximized;
            });
            Resize+=delegate { UpdateWindowIcon(); if(activePage!=null) activePage.UpdateWindowIcon(); };
            Wire("Close", delegate { Return(); });
            Wire("Back",delegate { Return(); });
            var header = View.FindName("Header") as UIElement;
            if(header!=null) header.MouseLeftButtonDown += delegate(object sender,System.Windows.Input.MouseButtonEventArgs e)
            {
                DependencyObject current = e.OriginalSource as DependencyObject;
                while(current!=null) { if(current is Button) return; current=current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current); }
                ReleaseCapture(); SendMessage((Shell??this).Handle,0xA1,(IntPtr)2,IntPtr.Zero);
            };
        }
        void UpdateWindowIcon()
        {
            var button=Find<Button>("Maximize"); if(button==null) return;
            bool full=(Shell??this).WindowState==Forms.FormWindowState.Maximized;
            button.Content=UiGlyph.Icon(full?"restore":"maximize",System.Windows.Media.Brushes.Black,24);
            UiGlyph.BindForeground((FrameworkElement)button.Content);
            button.ToolTip=UiText.Get(full?"Восстановить окно":"Развернуть окно");
        }
        void SetUpTrayHover()
        {
            var buttons=new Button[6]; int index=0;
            foreach(string name in new[]{"Open","Start","Stop","Archive","Settings","Exit"}) buttons[index++]=Find<Button>(name);
            Action update=delegate {
                Button hovered=null;
                if(Visible && Forms.Form.ActiveForm==this) {
                    var cursor=Forms.Cursor.Position;
                    if(ClientRectangle.Contains(PointToClient(cursor))) {
                        var point=View.PointFromScreen(new System.Windows.Point(cursor.X,cursor.Y));
                        DependencyObject item=View.InputHitTest(point) as DependencyObject;
                        while(item!=null && !(item is Button)) item=item is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item);
                        hovered=item as Button;
                    }
                }
                foreach(var button in buttons) button.Tag=button==hovered && button.IsEnabled?"Hover":null;
            };
            // ElementHost can retain WPF mouse-over after leaving its native window.
            // Use the actual pointer position; neither focus nor a cached hover stays highlighted.
            var timer=new Forms.Timer { Interval=60 }; timer.Tick+=delegate { update(); };
            View.MouseMove+=delegate { update(); };
            VisibleChanged+=delegate { if(Visible) timer.Start(); else { timer.Stop(); update(); } };
            Deactivate+=delegate { foreach(var button in buttons) button.Tag=null; };
            Disposed+=delegate { timer.Dispose(); };
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); int value=2; DwmSetWindowAttribute(Handle,33,ref value,4);
            // Borderless WPF hosts can expose an empty surface during native restore animations.
            int noTransition=1; DwmSetWindowAttribute(Handle,3,ref noTransition,4);
        }
        protected override void SetVisibleCore(bool value)
        {
            if(value && Controls.Count>0) {
                var host=Controls[0] as ElementHost;
                if(host!=null) {
                    IntPtr handle=host.Handle;
                    var source=PresentationSource.FromVisual(host.Child) as System.Windows.Interop.HwndSource;
                    if(source!=null && source.CompositionTarget!=null) source.CompositionTarget.BackgroundColor=System.Windows.Media.Color.FromRgb(246,250,255);
                    host.Child.Measure(new System.Windows.Size(ClientSize.Width,ClientSize.Height));
                    host.Child.Arrange(new Rect(0,0,ClientSize.Width,ClientSize.Height));
                    host.Child.UpdateLayout();
                }
            }
            base.SetVisibleCore(value);
            if(value && Controls.Count>0) { Controls[0].Invalidate(true); Controls[0].Update(); }
        }
        public void ShowAndActivate()
        {
            Show();
            if(WindowState==Forms.FormWindowState.Minimized) WindowState=Forms.FormWindowState.Normal;
            Activate();
        }
        protected override bool ProcessCmdKey(ref Forms.Message message,Forms.Keys keyData)
        {
            if((keyData & Forms.Keys.KeyCode)==Forms.Keys.Tab) return true;
            return base.ProcessCmdKey(ref message,keyData);
        }
        public T Find<T>(string name) where T:class { return View.FindName(name) as T; }
        public void Wire(string name, Action action)
        {
            Button button=Find<Button>(name); if(button!=null) button.Click += delegate { action(); };
        }
    }
}

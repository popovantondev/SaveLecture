using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SaveLecture
{
    internal sealed class LectureDashboard
    {
        public readonly DesignWindow Window;
        public Func<string,string> ResolveImagePath;
        readonly Dictionary<string,TextBlock> rows = new Dictionary<string,TextBlock>();
        readonly CaptionMerge captionMerge=new CaptionMerge();
        readonly List<string> paths = new List<string>();
        string lastPath;
        int selected = -1;
        string primaryLabel;
        Color? lastStateColor;
        bool refreshingThumbnails;
        public LectureDashboard(Action startOrPause, Action stop, Action archive, Action settings)
        {
            Window = new DesignWindow("main.xaml",1376,1000);
            Window.Wire("Primary",startOrPause); Window.Wire("Stop",stop); Window.Wire("Archive",archive); Window.Wire("Settings",settings);
            Window.Wire("PreviousSlide",delegate { if(selected>0) Select(selected-1); });
            Window.Wire("NextSlide",delegate { if(selected+1<paths.Count) Select(selected+1); });
            Window.Find<Button>("PreviousSlide").Content=UiGlyph.Icon("back",new SolidColorBrush(Color.FromRgb(46,69,100)),16);
            Window.Find<Button>("NextSlide").Content=UiGlyph.Icon("forward",new SolidColorBrush(Color.FromRgb(46,69,100)),16);
            Window.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails").SizeChanged+=delegate { if(selected>=0) RefreshThumbnails(); };
            Window.Find<Button>("Archive").Content=UiGlyph.Content("Записи","archive",new SolidColorBrush(Color.FromRgb(46,69,100)));
            Window.Find<Button>("Settings").Content=UiGlyph.Content("Настройки","gear",new SolidColorBrush(Color.FromRgb(46,69,100)));
            Window.Find<Button>("Settings").Padding=new Thickness(8,6,8,6);
            Window.Find<Button>("Stop").Content=UiGlyph.Content("Стоп","stop",new SolidColorBrush(Color.FromRgb(46,69,100)));
        }
        void Text(string name,string text) { Window.Find<TextBlock>(name).Text=UiText.Get(text); }
        public void Update(bool stopped,bool paused,bool available,bool error,TimeSpan elapsed,int fragments,int slides,int duplicates,bool saving=false,bool? captions=null,bool? sharing=null)
        {
            bool textActive=captions??available, slideActive=sharing??available;
            string activity=slideActive?(textActive?"Слайды и субтитры":"Сохраняются слайды"):textActive?"Сохраняются субтитры":"Ожидание демонстрации экрана";
            string state=stopped?"Запись остановлена":paused?"Запись на паузе":error?"Ошибка сохранения":saving?"Сохранение":activity;
            Text("StateTitle",state); Text("FooterState",stopped?"Запись остановлена":paused?"Пауза":error?"Нужна проверка":saving?"Сохранение":"Сбор включён");
            Text("StateDescription",stopped?"Сбор завершён":paused?"Сбор приостановлен":slideActive?"Демонстрация экрана доступна":lastPath==null?"Ожидание демонстрации экрана":"Ожидание демонстрации · показан сохранённый слайд");
            Text("Elapsed",((int)elapsed.TotalHours).ToString("00")+":"+elapsed.Minutes.ToString("00")+":"+elapsed.Seconds.ToString("00"));
            Text("TextCount",fragments.ToString("N0")); Text("SlideCount",slides.ToString()); Text("DuplicateCount",duplicates.ToString());
            Text("CaptionHint",stopped?"Запись остановлена":paused?"На паузе":textActive?"Поступают субтитры":"Ожидание новых субтитров");
            string label=stopped?"Старт записи":paused?"Продолжить":"Пауза";
            if(label!=primaryLabel)
            {
                primaryLabel=label;
                Window.Find<Button>("Primary").Content=label=="Пауза"?(object)UiGlyph.Content(label,"pause",Brushes.White):(object)UiGlyph.Content(label,"play",Brushes.White);
            }
            Window.Find<Button>("Stop").IsEnabled=!stopped;
            var color = stopped?Color.FromRgb(139,155,177):paused?Color.FromRgb(244,175,54):error?Color.FromRgb(241,74,83):saving?Color.FromRgb(24,173,182):available?Color.FromRgb(38,199,129):Color.FromRgb(151,105,230);
            Window.Find<TextBlock>("StateTitle").Foreground=new SolidColorBrush(!stopped && !paused && !error && !saving && !available?Color.FromRgb(117,72,190):Color.FromRgb(11,25,56));
            if(lastStateColor!=color) {
                lastStateColor=color;
                Window.Find<System.Windows.Shapes.Ellipse>("StateDot").Fill=new SolidColorBrush(color);
                Window.Find<System.Windows.Shapes.Ellipse>("FooterDot").Fill=new SolidColorBrush(color);
                Window.Find<System.Windows.Shapes.Ellipse>("CaptionDot").Fill=new SolidColorBrush(color);
            }
            Window.Find<System.Windows.Shapes.Ellipse>("CaptionDot").Fill=new SolidColorBrush(textActive && !stopped && !paused?Color.FromRgb(38,199,129):Color.FromRgb(139,155,177));
            UiText.Apply(Window.View);
        }
        static void CaptionText(TextBlock text,string body,string speaker)
        {
            text.Inlines.Clear();
            if(!String.IsNullOrEmpty(speaker)) text.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(speaker+": ")));
            text.Inlines.Add(new System.Windows.Documents.Run(body));
        }
        public void Caption(string key,string body,DateTime time,string speaker=null)
        {
            if(speaker==null) {
                int separator=body.IndexOf(": ",StringComparison.Ordinal);
                if(separator>0 && separator<=120) { speaker=body.Substring(0,separator); body=body.Substring(separator+2); }
            }
            var group=captionMerge.Observe(key,body,speaker,time);
            key=group.Id; body=group.Text; speaker=group.Speaker; time=group.Started;
            var panel=Window.Find<StackPanel>("TranscriptRows");
            Window.Find<TextBlock>("EmptyTranscript").Visibility=Visibility.Collapsed;
            TextBlock existing;
            if(rows.TryGetValue(key,out existing)) { CaptionText(existing,body,speaker); return; }
            var line=new Grid { Margin=new Thickness(0,8,0,10) };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(61) }); line.ColumnDefinitions.Add(new ColumnDefinition());
            var stamp=new TextBlock { Text=time.ToString("HH:mm:ss"),FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(133,152,181)),Margin=new Thickness(0,2,6,0) };
            var text=new TextBlock { FontSize=16,TextWrapping=TextWrapping.Wrap,LineHeight=24,Foreground=new SolidColorBrush(Color.FromRgb(68,85,115)) };
            CaptionText(text,body,speaker);
            Grid.SetColumn(text,1); line.Children.Add(stamp); line.Children.Add(text); panel.Children.Add(line); rows[key]=text;
            if(panel.Children.Count>201)
            {
                var old=((Grid)panel.Children[1]).Children[1] as TextBlock;
                foreach(string source in new List<string>(rows.Keys)) if(rows[source]==old) rows.Remove(source);
                panel.Children.RemoveAt(1);
            }
            var scroll=Window.Find<ScrollViewer>("TranscriptScroll");
            Window.RefreshMetrics();
            if(scroll.ScrollableHeight-scroll.VerticalOffset<80) scroll.ScrollToEnd();
        }
        BitmapSource LoadImage(string path,int width)
        {
            string original=path;
            for(int attempt=0;attempt<2;attempt++) {
                if(ResolveImagePath!=null) path=ResolveImagePath(original);
                try {
                    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                        var source=new BitmapImage(); source.BeginInit(); source.CacheOption=BitmapCacheOption.OnLoad; source.DecodePixelWidth=width; source.StreamSource=stream; source.EndInit(); source.Freeze(); return source;
                    }
                } catch(FileNotFoundException) { } catch(DirectoryNotFoundException) { }
            }
            return null;
        }
        public void Slide(string path)
        { Slide(path,new string[0]); }
        public void Slide(string path,string[] earlierVersions)
        {
            if(String.IsNullOrEmpty(path)||lastPath==path||!File.Exists(path)) return;
            lastPath=path;
            int replacement=paths.Count;
            foreach(string earlier in earlierVersions) {
                int previous=paths.IndexOf(earlier);
                if(previous>=0) { replacement=Math.Min(replacement,previous); paths.RemoveAt(previous); }
            }
            int index=paths.IndexOf(path);
            if(index<0)
            {
                index=Math.Min(replacement,paths.Count);
                paths.Insert(index,path);
            }
            Select(index);
        }
        void Select(int index)
        {
            selected=index;
            Window.Find<Image>("SlideImage").Source=LoadImage(paths[index],0);
            Window.Find<TextBlock>("EmptySlide").Visibility=Visibility.Collapsed;
            Text("SlidePosition",(index+1)+" из "+paths.Count);
            Window.Find<Button>("PreviousSlide").IsEnabled=index>0;
            Window.Find<Button>("NextSlide").IsEnabled=index+1<paths.Count;
            RefreshThumbnails();
        }
        void RefreshThumbnails()
        {
            if(refreshingThumbnails) return;
            refreshingThumbnails=true;
            try {
            var panel=Window.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails"); panel.Children.Clear();
            double density=Math.Min(1,Math.Min(Window.View.ActualWidth/1100,Window.View.ActualHeight/800));
            int visible=panel.ActualWidth>0 && density>0?Math.Max(1,Math.Min(12,(int)(panel.ActualWidth/(105*density)))):4;
            panel.Columns=visible;
            int start=Math.Max(0,Math.Min(selected-visible/2,paths.Count-visible));
            for(int i=start;i<Math.Min(paths.Count,start+visible);i++)
            {
                int item=i;
                var button=new Button { Style=(Style)Window.View.FindResource("ThumbnailButton"),Margin=new Thickness(i==start?0:6,0,0,0),Padding=new Thickness(2),BorderBrush=new SolidColorBrush(i==selected?Color.FromRgb(0,131,255):Color.FromRgb(215,227,240)),BorderThickness=new Thickness(i==selected?2:1) };
                var grid=new Grid { ClipToBounds=true }; grid.Children.Add(new Image { Source=LoadImage(paths[i],180),Stretch=Stretch.UniformToFill }); grid.Children.Add(SlideNumber(i+1));
                button.Content=grid; button.Click+=delegate { Select(item); }; panel.Children.Add(button);
            }
            Window.RefreshMetrics();
            } finally { refreshingThumbnails=false; }
        }
        internal static FrameworkElement SlideNumber(int number)
        {
            var badge=new Grid { Width=30,Height=18,Margin=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false };
            badge.Children.Add(new Border { Background=new SolidColorBrush(Color.FromRgb(0,131,255)),CornerRadius=new CornerRadius(9) });
            var text=new TextBlock { Text=number.ToString(),FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextWrapping=TextWrapping.NoWrap };
            badge.Children.Add(new Viewbox { Width=22,Height=14,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=text });
            return badge;
        }
        public void Reset()
        {
            rows.Clear(); captionMerge.Clear(); paths.Clear(); lastPath=null; selected=-1;
            var panel=Window.Find<StackPanel>("TranscriptRows"); while(panel.Children.Count>1) panel.Children.RemoveAt(1);
            Window.Find<TextBlock>("EmptyTranscript").Visibility=Visibility.Visible;
            Window.Find<Image>("SlideImage").Source=null; Window.Find<TextBlock>("EmptySlide").Visibility=Visibility.Visible;
            Window.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails").Children.Clear(); Text("SlidePosition","0 из 0");
            Window.Find<Button>("PreviousSlide").IsEnabled=false; Window.Find<Button>("NextSlide").IsEnabled=false;
        }
    }
}

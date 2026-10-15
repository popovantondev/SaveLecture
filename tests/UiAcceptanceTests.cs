using System;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SaveLecture;
using Forms=System.Windows.Forms;

public static class UiAcceptanceTests
{
    static void Check(bool value,string label) { if(!value) throw new Exception(label); Console.WriteLine("PASS UI: "+label); }
    static void CheckCard(FrameworkElement root,FrameworkElement card)
    {
        var text=root as TextBlock;
        if(text!=null && text.IsVisible && text.ActualHeight>0) {
            var bounds=text.TransformToAncestor(card).TransformBounds(new Rect(0,0,text.ActualWidth,text.ActualHeight));
            if(bounds.Bottom>card.ActualHeight-2 || bounds.Top<0) throw new Exception("Clipped card text: "+text.Text);
        }
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var child=VisualTreeHelper.GetChild(root,i) as FrameworkElement; if(child!=null) CheckCard(child,card);
        }
    }
    static void Pump() { Forms.Application.DoEvents(); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,new Action(delegate{})); }
    static void Render(DesignWindow window,string path,double scale)
    {
        var visual=(FrameworkElement)((System.Windows.Forms.Integration.ElementHost)window.Controls[0]).Child; visual.UpdateLayout();
        var image=new RenderTargetBitmap((int)(visual.ActualWidth*scale),(int)(visual.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        image.Render(visual); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using(var stream=File.Create(path)) encoder.Save(stream);
    }
    static void PrepareOffscreen(DesignWindow window,int width,int height)
    {
        var root=(FrameworkElement)((System.Windows.Forms.Integration.ElementHost)window.Controls[0]).Child;
        window.ClientSize=new System.Drawing.Size(width,height);
        UiGlyph.ReplaceGlyphs(window.View); UiText.Apply(window.View);
        root.Measure(new System.Windows.Size(width,height)); root.Arrange(new Rect(0,0,width,height)); root.UpdateLayout();
        window.RefreshMetrics(); root.UpdateLayout();
        var transcript=window.Find<ScrollViewer>("TranscriptScroll"); if(transcript!=null) { transcript.ScrollToTop(); root.UpdateLayout(); }
        Check(!window.Visible && root.ActualWidth==width && root.ActualHeight==height,"artificial website render stays hidden at its original window proportions");
    }
    static void RenderWebsite(DesignWindow window,string path)
    {
        // Export the rounded window itself, without the opaque native-host backdrop.
        // A transparent two-DIP gutter keeps the whole outline inside the bitmap.
        var frame=(Border)window.View; frame.UpdateLayout(); const double scale=2, gutter=2;
        double width=frame.ActualWidth,height=frame.ActualHeight;
        var content=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        var outline=frame.BorderBrush;
        // Draw the native outline once; clipping its own antialiased curve can leave gaps.
        try { frame.BorderBrush=System.Windows.Media.Brushes.Transparent; content.Render(frame); }
        finally { frame.BorderBrush=outline; }
        var drawing=new DrawingVisual();
        using(var context=drawing.RenderOpen()) {
            var bounds=new Rect(gutter,gutter,width,height);
            double radius=frame.CornerRadius.TopLeft,stroke=frame.BorderThickness.Left;
            context.DrawRoundedRectangle(frame.Background,null,bounds,radius,radius);
            context.PushClip(new RectangleGeometry(bounds,radius,radius));
            context.DrawImage(content,bounds); context.Pop();
            context.DrawRoundedRectangle(null,new System.Windows.Media.Pen(outline,stroke),new Rect(gutter+stroke/2,gutter+stroke/2,width-stroke,height-stroke),radius-stroke/2,radius-stroke/2);
        }
        var image=new RenderTargetBitmap((int)((width+2*gutter)*scale),(int)((height+2*gutter)*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        image.Render(drawing); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using(var stream=File.Create(path)) encoder.Save(stream);
    }
    // Artificial examples. No meeting files, recorder or persisted preferences.
    static LectureDashboard ArchivePreview(bool compact)
    {
        var dashboard=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
        string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-demo-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int[,] sizes={{1600,900},{1024,768},{900,1600},{640,640}};
        for(int i=0;i<4;i++) {
            string path=Path.Combine(directory,i+".png");
            using(var image=new Bitmap(sizes[i,0],sizes[i,1])) {
                DemoSlides.Draw(image,i);
                image.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
            dashboard.Slide(path);
        }
        for(int i=0;i<3;i++) dashboard.Window.Find<Button>("PreviousSlide").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        string[] bodies={"Heute betrachten wir ein einfaches Beispiel: Ein Projekt braucht ein klares Ziel, kleine Schritte und überprüfbare Ergebnisse.","Die Demo enthält ausschließlich erfundene Inhalte. Es wurden keine Gesprächsdaten verwendet.","Compare the examples: a wide slide, a portrait page and a square diagram. Each keeps its full content in the main view.","В этом учебном примере мы определяем цель, сравниваем варианты и сохраняем результат. Все тексты и имена вымышлены.","Zum Schluss prüfen wir Pause, Fortsetzen und Stopp. So bleibt der nächste Schritt klar."};
        for(int i=0;i<bodies.Length;i++) dashboard.Caption("demo-"+i,bodies[i],new DateTime(2026,1,1,10,5+i,0),i%2==0?"Demo speaker A":"Demo speaker B");
        dashboard.Update(true,false,false,false,TimeSpan.Zero,bodies.Length,4,0);
        dashboard.Window.ClientSize=new System.Drawing.Size(compact?900:1100,compact?650:800);
        return dashboard;
    }
    static LectureDashboard FilePreview(string transcript,string imageFolder,bool compact)
    {
        var dashboard=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
        int count=0; DateTime first=DateTime.MinValue,last=DateTime.MinValue;
        foreach(string line in File.ReadAllLines(transcript)) {
            var match=System.Text.RegularExpressions.Regex.Match(line,@"^\[(\d{2}:\d{2}:\d{2})\] (.+)$");
            if(!match.Success) continue;
            var time=DateTime.Parse("2026-09-15 "+match.Groups[1].Value);
            if(first==DateTime.MinValue) first=time; last=time;
            dashboard.Caption((count++).ToString(),match.Groups[2].Value,time);
        }
        string[] images=Directory.GetFiles(imageFolder,"*.jpg"); Array.Sort(images,StringComparer.Ordinal);
        foreach(string image in images) dashboard.Slide(image);
        dashboard.Update(true,false,false,false,last-first,count,images.Length,0);
        dashboard.Window.Text="SaveLecture — предпросмотр ваших файлов";
        dashboard.Window.Find<TextBlock>("StateTitle").Text="Просмотр файлов";
        dashboard.Window.Find<TextBlock>("StateDescription").Text="Последние 200 из "+count+" фрагментов · "+images.Length+" снимков";
        dashboard.Window.Find<TextBlock>("CaptionHint").Text="Из файла";
        dashboard.Window.Find<TextBlock>("FooterState").Text="Предпросмотр";
        dashboard.Window.ClientSize=new System.Drawing.Size(compact?900:1100,compact?650:800);
        dashboard.Window.Shown+=delegate { dashboard.Window.Find<ScrollViewer>("TranscriptScroll").ScrollToTop(); };
        return dashboard;
    }
    [STAThread] public static void Main(string[] args)
    {
        try { Run(args); }
        catch(Exception error) { Console.Error.WriteLine(error); Environment.ExitCode=1; }
    }
    static void Run(string[] args)
    {
        WindowsAutostart.TestDirectory=Path.Combine(Path.GetTempPath(),"SaveLecture-test-startup-"+Guid.NewGuid().ToString("N"));
        WindowsAutostart.TestExecutable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SaveLecture.exe");
        NameMatcher.TestPath=Path.Combine(Path.GetTempPath(),"SaveLecture-demo-aliases-"+Guid.NewGuid().ToString("N")+".txt"); File.WriteAllText(NameMatcher.TestPath,"Demo speaker A\nDemo speaker B\n");
        Forms.Application.EnableVisualStyles();
        if(args.Length>0 && args[0]=="--caption-merge") {
            UiPreferences.TestPath=Path.Combine(Path.GetTempPath(),"SaveLecture-caption-ui-"+Guid.NewGuid().ToString("N")+".json");
            var view=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
            using(view.Window) {
                var time=new DateTime(2026,10,14,8,0,0);
                view.Caption("first","Wir zeigen heute ein kleines Beispiel",time,"Demo speaker");
                view.Caption("second","Wir zeigen heute ein kleines Beispiel, jetzt mit Farben.",time.AddSeconds(2),"Demo speaker");
                view.Caption("third","Wir zeigen heute",time.AddSeconds(3),"Demo speaker");
                var panel=view.Window.Find<StackPanel>("TranscriptRows");
                Check(panel.Children.Count==2,"progressive caption with changing keys and commas uses one visible row");
                var line=(Grid)panel.Children[1];
                Check(((TextBlock)line.Children[0]).Text=="08:00:00","merged caption retains first-seen timestamp");
                var visibleText=(TextBlock)line.Children[1];
                Check(new System.Windows.Documents.TextRange(visibleText.ContentStart,visibleText.ContentEnd).Text.Contains("Farben"),"short redraw keeps the fullest visible text");
                view.Caption("fourth","Wir zeigen heute ein kleines Beispiel, jetzt mit Farben.",time.AddSeconds(4),"Other speaker");
                Check(panel.Children.Count==3,"different speaker gets a separate visible row");
                view.Caption("fifth","Wir wechseln jetzt das Thema.",time.AddSeconds(5),"Demo speaker");
                Check(panel.Children.Count==4,"another utterance from same speaker remains visible");
                view.Reset(); Check(panel.Children.Count==1,"new recording clears caption grouping and visible rows");
            }
            return;
        }
        if(args.Length>0 && args[0]=="--source-status") {
            string statusDirectory=Path.Combine(Path.GetTempPath(),"SaveLecture-source-status-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(statusDirectory);
            UiPreferences.TestPath=Path.Combine(statusDirectory,"preferences.json");
            var statusDashboard=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
            using(statusDashboard.Window) using(var statusTray=new DesignWindow("tray.xaml",340,383)) {
                foreach(string lang in new[]{"ru","de","en"}) {
                    UiText.Language=lang;
                    statusDashboard.Update(false,false,true,false,TimeSpan.Zero,0,0,0,false,true,false);
                    Check(statusDashboard.Window.Find<TextBlock>("StateTitle").Text==UiText.Get("Сохраняются субтитры"),"captions without sharing have an explicit status in "+lang);
                    Check(statusDashboard.Window.Find<TextBlock>("StateDescription").Text==UiText.Get("Ожидание демонстрации экрана"),"missing sharing is explicit in "+lang);
                    statusDashboard.Update(false,false,false,false,TimeSpan.Zero,0,0,0,false,false,false);
                    Check(statusDashboard.Window.Find<TextBlock>("StateTitle").Text==UiText.Get("Ожидание демонстрации экрана"),"idle sources do not claim active recording in "+lang);
                    Check(statusDashboard.Window.Find<TextBlock>("CaptionHint").Text==UiText.Get("Ожидание новых субтитров"),"stale captions show waiting in "+lang);
                    TrayMenuState.Update(statusTray,false,false,false,false,false,false,false);
                    Check(statusTray.Find<TextBlock>("State").Text==UiText.Get("Ждём демонстрацию"),"tray reflects idle sources in "+lang);
                    Check(((SolidColorBrush)statusTray.Find<TextBlock>("State").Foreground).Color==System.Windows.Media.Color.FromRgb(117,72,190) && ((SolidColorBrush)statusTray.Find<Border>("StateBadge").Background).Color==System.Windows.Media.Color.FromRgb(242,235,255),"waiting badge uses purple text and background in "+lang);
                    TrayMenuState.Update(statusTray,false,true,false,false,false,false,false);
                    Check(((SolidColorBrush)statusTray.Find<TextBlock>("State").Foreground).Color==System.Windows.Media.Color.FromRgb(150,102,20),"pause remains yellow in "+lang);
                    statusDashboard.Update(false,false,true,false,TimeSpan.Zero,0,0,0,false,false,true);
                    Check(statusDashboard.Window.Find<TextBlock>("StateTitle").Text==UiText.Get("Сохраняются слайды"),"sharing resumes independently in "+lang);
                }
                UiText.Language="ru";
            }
            return;
        }
        if(args.Length>0 && args[0]=="--relocated-slides") {
            string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-relocated-ui-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            UiPreferences.TestPath=Path.Combine(directory,"preferences.json");
            string first=Path.Combine(directory,"first.png"),final=Path.Combine(directory,"final.png"),removed=Path.Combine(directory,"removed.png");
            using(var picture=new Bitmap(960,540)) { using(var g=System.Drawing.Graphics.FromImage(picture)) { g.Clear(System.Drawing.Color.White); g.FillRectangle(System.Drawing.Brushes.Black,120,120,60,24); } picture.Save(first); picture.Save(final); }
            var view=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
            using(view.Window) {
                view.Slide(first);
                File.Move(first,removed);
                view.ResolveImagePath=delegate(string path) { return path==first?final:path; };
                PrepareOffscreen(view.Window,1100,800);
                PrepareOffscreen(view.Window,1366,728);
                Check(view.Window.Find<System.Windows.Controls.Image>("SlideImage").Source!=null,"relocated image remains readable during gallery resize");
                view.Slide(final,new[]{first});
                Check(view.Window.Find<TextBlock>("SlidePosition").Text=="1 из 1","replacement removes old logical thumbnail after original moves");
                int resolutions=0;
                view.ResolveImagePath=delegate(string path) { resolutions++; return resolutions==1?Path.Combine(directory,"not-found.png"):final; };
                PrepareOffscreen(view.Window,1000,700);
                Check(resolutions>=2,"thumbnail loading retries path resolution when relocation happens during file open");
            }
            return;
        }
        if(args.Length>0 && args[0]=="--progressive-slides") {
            string drawingDirectory=Path.Combine(Path.GetTempPath(),"SaveLecture-progressive-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(drawingDirectory);
            UiPreferences.TestPath=Path.Combine(drawingDirectory,"preferences.json");
            var drawingIndex=new SlideSelection();
            var drawingView=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
            using(drawingView.Window) {
                string lastDrawing=null;
                for(int step=0;step<5;step++) {
                    string file=Path.Combine(drawingDirectory,"drawing-"+step+".png");
                    using(var picture=new Bitmap(960,540)) {
                        using(var g=System.Drawing.Graphics.FromImage(picture)) {
                            g.Clear(System.Drawing.Color.White);
                            for(int mark=0;mark<=step;mark++) g.FillRectangle(System.Drawing.Brushes.Black,120+mark*100,120+mark*45,60,24);
                        }
                        picture.Save(file,System.Drawing.Imaging.ImageFormat.Png);
                        drawingIndex.Add(file,SlideArchive.Feature(picture));
                    }
                    drawingView.Slide(file,drawingIndex.EarlierVersions(file));
                    Check(drawingView.Window.Find<TextBlock>("SlidePosition").Text=="1 из 1","progressive drawing retains a single gallery slot at step "+step);
                    lastDrawing=file;
                }
                Check(drawingIndex.EarlierVersions(lastDrawing).Length==4,"all four earlier drawing originals remain linked");
                Check(drawingIndex.Representative(Path.Combine(drawingDirectory,"drawing-0.png"))==lastDrawing,"revisiting a partial drawing resolves to its final version");
                string distinct=Path.Combine(drawingDirectory,"different.png");
                using(var picture=new Bitmap(960,540)) {
                    using(var g=System.Drawing.Graphics.FromImage(picture)) { g.Clear(System.Drawing.Color.White); g.FillRectangle(System.Drawing.Brushes.Black,700,300,100,50); }
                    picture.Save(distinct,System.Drawing.Imaging.ImageFormat.Png); drawingIndex.Add(distinct,SlideArchive.Feature(picture));
                }
                drawingView.Slide(distinct,drawingIndex.EarlierVersions(distinct));
                Check(drawingView.Window.Find<TextBlock>("SlidePosition").Text=="2 из 2","a different slide receives its own gallery slot");
                PrepareOffscreen(drawingView.Window,1366,728); Render(drawingView.Window,Path.Combine(drawingDirectory,"gallery.png"),1);
                Console.WriteLine("PROGRESSIVE_ARTIFACTS="+drawingDirectory);
            }
            return;
        }
        if(args.Length>0 && args[0]=="--thumbnail-border-render") {
            string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-thumbnail-border-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            UiPreferences.TestPath=Path.Combine(directory,"preferences.json");
            var borderDemo=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
            using(borderDemo.Window) {
                for(int i=0;i<24;i++) {
                    string file=Path.Combine(directory,"slide-"+i+".png");
                    using(var picture=new Bitmap(i%3==1?360:640,i%3==1?640:i%3==2?640:360)) {
                        using(var drawing=System.Drawing.Graphics.FromImage(picture)) drawing.Clear(System.Drawing.Color.Magenta);
                        picture.Save(file,System.Drawing.Imaging.ImageFormat.Png);
                    }
                    borderDemo.Slide(file);
                }
                int failures=0;
                foreach(var size in new[]{new System.Drawing.Size(1100,800),new System.Drawing.Size(1366,728),new System.Drawing.Size(1920,1040),new System.Drawing.Size(1920,1152),new System.Drawing.Size(2560,1400)}) {
                  for(int aspect=0;aspect<3;aspect++) {
                    string aspectFile=Path.Combine(directory,"probe-"+size.Width+"x"+size.Height+"-"+aspect+".png");
                    using(var aspectPicture=new Bitmap(aspect==1?360:640,aspect==0?360:640)) {
                        using(var aspectDrawing=System.Drawing.Graphics.FromImage(aspectPicture)) aspectDrawing.Clear(System.Drawing.Color.Magenta);
                        aspectPicture.Save(aspectFile,System.Drawing.Imaging.ImageFormat.Png);
                    }
                    borderDemo.Slide(aspectFile);
                    PrepareOffscreen(borderDemo.Window,size.Width,size.Height);
                    var panel=borderDemo.Window.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails");
                    var selectedButton=(Button)panel.Children[panel.Children.Count-1];
                    var frame=(Border)selectedButton.Template.FindName("Frame",selectedButton);
                    var root=(FrameworkElement)((System.Windows.Forms.Integration.ElementHost)borderDemo.Window.Controls[0]).Child;
                    var bounds=frame.TransformToAncestor(root).TransformBounds(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));
                    string borderOutput=Path.Combine(directory,"border-"+size.Width+"x"+size.Height+"-"+aspect+".png"); Render(borderDemo.Window,borderOutput,2);
                    selectedButton=(Button)panel.Children[panel.Children.Count-1];
                    frame=(Border)selectedButton.Template.FindName("Frame",selectedButton);
                    bounds=frame.TransformToAncestor(root).TransformBounds(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));
                    var probes=new[]{new System.Windows.Point(bounds.Left+1,bounds.Top+bounds.Height/2),new System.Windows.Point(bounds.Right-1,bounds.Top+bounds.Height/2),new System.Windows.Point(bounds.Left+bounds.Width/2,bounds.Top+1),new System.Windows.Point(bounds.Left+bounds.Width/2,bounds.Bottom-1),new System.Windows.Point(bounds.Right-3,bounds.Top+3),new System.Windows.Point(bounds.Right-3,bounds.Bottom-3)};
                    using(var bitmap=new Bitmap(borderOutput)) {
                        for(int i=0;i<probes.Length;i++) {
                            bool blue=false;
                            for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++) {
                                var pixel=bitmap.GetPixel((int)Math.Round(probes[i].X*2)+x,(int)Math.Round(probes[i].Y*2)+y);
                                if(pixel.R<90 && pixel.G>60 && pixel.G<190 && pixel.B>200) blue=true;
                            }
                            if(!blue) {
                                failures++;
                                Console.WriteLine("FRAME="+bounds+" PANEL="+panel.ActualWidth+"x"+panel.ActualHeight+" PROBE="+probes[i]);
                                for(Visual ancestor=frame;ancestor!=null;ancestor=VisualTreeHelper.GetParent(ancestor) as Visual) {
                                    var element=ancestor as FrameworkElement; var clip=VisualTreeHelper.GetClip(ancestor);
                                    if(element!=null) Console.WriteLine("ANCESTOR="+element.GetType().Name+" "+element.ActualWidth+"x"+element.ActualHeight+" CLIP="+(clip==null?"none":clip.Bounds.ToString()));
                                }
                            }
                            Console.WriteLine((blue?"PASS":"FAIL")+" UI: selected thumbnail blue edge "+i+" at "+size.Width+"x"+size.Height+" aspect "+aspect);
                        }
                    }
                  }
                }
                Console.WriteLine("BORDER_ARTIFACTS="+directory);
                Check(failures==0,"selected last thumbnail keeps all blue sides and right corners with wide, square and portrait images");
            }
            return;
        }
        if(args.Length>0 && args[0]=="--thumbnail-render") {
            string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-thumbnail-demo-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            UiPreferences.TestPath=Path.Combine(directory,"preferences.json");
            using(var preview=ArchivePreview(false).Window) {
                var thumbnails=preview.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails");
                int[] numbers={1,9,99,999};
                foreach(var size in new[]{new System.Drawing.Size(1100,800),new System.Drawing.Size(900,650)}) {
                    PrepareOffscreen(preview,size.Width,size.Height);
                    for(int i=0;i<thumbnails.Children.Count;i++) {
                        var tile=(Grid)((Button)thumbnails.Children[i]).Content;
                        tile.Children.RemoveAt(1); tile.Children.Add(LectureDashboard.SlideNumber(numbers[i]));
                    }
                    preview.RefreshMetrics(); ((System.Windows.Forms.Integration.ElementHost)preview.Controls[0]).Child.UpdateLayout();
                    foreach(Button tileButton in thumbnails.Children) {
                        var tile=(Grid)tileButton.Content; var badge=(Grid)tile.Children[1];
                        var text=(TextBlock)((Viewbox)badge.Children[1]).Child;
                        var bounds=text.TransformToAncestor(badge).TransformBounds(new Rect(0,0,text.ActualWidth,text.ActualHeight));
                        double x=Math.Max(Math.Abs(bounds.Left-badge.ActualWidth/2),Math.Abs(bounds.Right-badge.ActualWidth/2));
                        double y=Math.Max(Math.Abs(bounds.Top-badge.ActualHeight/2),Math.Abs(bounds.Bottom-badge.ActualHeight/2));
                        double capX=Math.Max(0,x-(badge.ActualWidth-badge.ActualHeight)/2);
                        Check(capX*capX+y*y<=badge.ActualHeight*badge.ActualHeight/4,"entire slide number fits inside oval: "+text.Text+" at "+size.Width);
                    }
                    foreach(double scale in new[]{1,1.25,1.5,2}) Render(preview,Path.Combine(directory,"thumbnails-"+size.Width+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"),scale);
                    var rowDrawing=new DrawingVisual();
                    using(var drawing=rowDrawing.RenderOpen()) drawing.DrawRectangle(new VisualBrush(thumbnails),null,new Rect(0,0,thumbnails.ActualWidth,thumbnails.ActualHeight));
                    var rowImage=new RenderTargetBitmap((int)Math.Ceiling(thumbnails.ActualWidth*3),(int)Math.Ceiling(thumbnails.ActualHeight*3),288,288,PixelFormats.Pbgra32);
                    rowImage.Render(rowDrawing); var rowEncoder=new PngBitmapEncoder(); rowEncoder.Frames.Add(BitmapFrame.Create(rowImage));
                    using(var file=File.Create(Path.Combine(directory,"number-row-"+size.Width+".png"))) rowEncoder.Save(file);
                }
            }
            Console.WriteLine("THUMBNAIL_ARTIFACTS="+directory); return;
        }
        if(args.Length>0 && args[0]=="--website-render") {
            string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-website-demo-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            UiPreferences.TestPath=Path.Combine(directory,"preferences.json");
            foreach(string language in new[]{"ru","de","en"}) {
                UiText.Language=language;
                using(var demo=ArchivePreview(false).Window) { PrepareOffscreen(demo,1100,800); RenderWebsite(demo,Path.Combine(directory,"demo-"+language+".png")); }
                using(var websiteSettings=new LectureSettings(3,new UiPreferences { Language=language },directory,delegate{})) {
                    websiteSettings.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PrepareOffscreen(websiteSettings.Window,1100,800); RenderWebsite(websiteSettings.Window,Path.Combine(directory,"filters-"+language+".png"));
                }
                using(var notice=new FirstRunNotice(new UiPreferences { Language=language })) { PrepareOffscreen(notice,660,510); RenderWebsite(notice,Path.Combine(directory,"consent-"+language+".png")); }
            }
            Console.WriteLine("WEBSITE_ARTIFACTS="+directory); return;
        }
        var trayArea=new System.Drawing.Rectangle(0,0,1920,1032); var traySize=new System.Drawing.Size(340,383);
        var trayPoint=TrayPopupPlacement.Near(new System.Drawing.Point(1210,1060),trayArea,traySize);
        Check(trayPoint.X==1040 && trayPoint.Y==641,"tray menu centers over the clicked bottom icon with a taskbar gap");
        foreach(var anchor in new[]{new System.Drawing.Point(8,1060),new System.Drawing.Point(1910,1060),new System.Drawing.Point(-12,450),new System.Drawing.Point(1950,450),new System.Drawing.Point(900,-12)}) {
            var point=TrayPopupPlacement.Near(anchor,trayArea,traySize);
            Check(trayArea.Contains(new System.Drawing.Rectangle(point,traySize)),"tray menu stays inside working area for "+anchor);
        }
        var otherArea=new System.Drawing.Rectangle(-1920,-100,1920,1032);
        var otherPoint=TrayPopupPlacement.Near(new System.Drawing.Point(-900,960),otherArea,traySize);
        Check(otherPoint.X==-1070 && otherArea.Contains(new System.Drawing.Rectangle(otherPoint,traySize)),"tray menu anchors on a monitor with negative coordinates");
        if(args.Length>0 && args[0]=="--confirm-preview") { Forms.Application.Run(new StopConfirmation()); return; }
        if(args.Length>0 && args[0]=="--consent-preview") {
            UiPreferences.TestPath=Path.Combine(Path.GetTempPath(),"SaveLecture-demo-consent-"+Guid.NewGuid().ToString("N"),"preferences.json");
            using(var notice=new FirstRunNotice(new UiPreferences())) Forms.Application.Run(notice); return;
        }
        if(args.Length>0 && args[0]=="--mention-preview") { var popup=new MentionPopup(); popup.ShowInTaskbar=true; popup.Preview("Демо-участник: «Алекс, можешь пояснить этот пример?»"); popup.VisibleChanged+=delegate { if(!popup.Visible) Forms.Application.ExitThread(); }; Forms.Application.Run(popup); return; }
        if(args.Length>0 && args[0]=="--mention-render") {
            using(var popup=new MentionPopup()) {
                popup.Preview("Демо-участник: «Алекс, можешь пояснить этот пример?»"); Pump();
                var button=popup.Find<Button>("Close"); var frame=(Border)button.Template.FindName("Frame",button);
                string directory=Path.Combine(Path.GetTempPath(),"SaveLecture-mention-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                foreach(double scale in new[]{1,1.25,1.5,2}) {
                    Render(popup,Path.Combine(directory,"normal-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"),scale);
                    frame.Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(229,62,78)); button.Foreground=System.Windows.Media.Brushes.White;
                    Pump(); Render(popup,Path.Combine(directory,"hover-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"),scale);
                    frame.ClearValue(Border.BackgroundProperty); button.ClearValue(Control.ForegroundProperty);
                }
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); Check(!popup.Visible,"mention close button dismisses the popup");
                Console.WriteLine("MENTION_ARTIFACTS="+directory);
            } return;
        }
        if(args.Length>0 && args[0]=="--tray-preview") { var menu=new DesignWindow("tray.xaml",400,450); Forms.Application.Run(menu); return; }
        if(args.Length>0 && args[0]=="--content-preview") { using(var preview=ArchivePreview(args.Length>1 && args[1]=="compact").Window) Forms.Application.Run(preview); return; }
        if(args.Length>=3 && args[0]=="--file-preview") {
            using(var preview=FilePreview(args[1],args[2],args.Length>3 && args[3]=="compact").Window) {
                preview.Show(); Pump();
                Check(preview.Find<StackPanel>("TranscriptRows").Children.Count==201,"large file keeps 200 visible fragments");
                Check(preview.Find<ScrollViewer>("TranscriptScroll").ScrollableHeight>0,"large real transcript scrolls");
                var previous=preview.Find<Button>("PreviousSlide");
                while(previous.IsEnabled) previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(preview.Find<TextBlock>("SlidePosition").Text.StartsWith("1 из "),"all imported slides reachable");
                preview.Find<Button>("NextSlide").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(preview.Find<TextBlock>("SlidePosition").Text.StartsWith("2 из "),"next slide navigation responds");
                for(int i=0;i<6;i++) preview.Find<Button>("NextSlide").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(); Render(preview,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests",args.Length>3 && args[3]=="compact"?"input-preview-compact.png":"input-preview-normal.png"),1);
                var originalSize=preview.ClientSize; preview.ClientSize=new System.Drawing.Size(1400,900); Pump();
                var thumbnails=preview.Find<System.Windows.Controls.Primitives.UniformGrid>("Thumbnails");
                Check(thumbnails.Children.Count>=6,"wide window shows more thumbnails");
                var lastThumb=(Button)thumbnails.Children[thumbnails.Children.Count-1];
                double thumbRight=lastThumb.TranslatePoint(new System.Windows.Point(lastThumb.ActualWidth,0),thumbnails).X;
                Check(Math.Abs(thumbRight-thumbnails.ActualWidth)<=2,"thumbnail row fills the available width");
                Render(preview,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests","input-preview-wide.png"),1);
                preview.ClientSize=originalSize; Pump();
                Forms.Application.Run(preview);
            } return;
        }
        string output=Path.Combine(Path.GetTempPath(),"SaveLecture-ui-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
        UiPreferences.TestPath=Path.Combine(output,"launch-test","preferences.json");
        new UiPreferences { ArchiveRoot=Path.Combine(output,"launch-test","recordings"),ConsentNoticeVersion=1 }.Save();
        Program.StopRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        Program.StartRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        Program.ExitRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        Program.ShowRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        Program.TrayRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        Program.SettingsRequested=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.ManualReset);
        using(var hiddenApp=new LectureApp(true,true)) {
            var main=(Forms.Form)typeof(LectureApp).GetField("statusWindow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hiddenApp);
            var tray=(Forms.NotifyIcon)typeof(LectureApp).GetField("tray",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hiddenApp);
            Check(!main.Visible && tray.Visible,"background launch creates a visible tray icon without showing main window");
            Program.ShowRequested.Set(); var deadline=DateTime.UtcNow.AddSeconds(3);
            while(!main.Visible && DateTime.UtcNow<deadline) { Pump(); System.Threading.Thread.Sleep(25); }
            Check(main.Visible,"tray-mode main window opens on explicit show command");
            Check((bool)typeof(LectureApp).GetField("stopping",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hiddenApp),"opening main window does not start a stopped recording");
            hiddenApp.ExitThread();
        }
        using(var normalApp=new LectureApp(true,false)) {
            var main=(Forms.Form)typeof(LectureApp).GetField("statusWindow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(normalApp);
            Check(main.Visible,"first acknowledged launch shows the main window"); normalApp.ExitThread();
        }
        Program.StopRequested.Dispose(); Program.StartRequested.Dispose(); Program.ExitRequested.Dispose(); Program.ShowRequested.Dispose(); Program.TrayRequested.Dispose(); Program.SettingsRequested.Dispose();
        UiPreferences.TestPath=null;
        var tiny=new Border { Child=new TextBlock { Text="Resize",FontSize=14 } }; tiny.Measure(new System.Windows.Size(1,1)); tiny.Arrange(new Rect(0,0,1,1));
        new NativeLayout().Update(tiny); Check(((TextBlock)tiny.Child).FontSize>0,"transient 1 x 1 viewport cannot create zero font size");
        tiny.Measure(new System.Windows.Size(1100,800)); tiny.Arrange(new Rect(0,0,1100,800)); new NativeLayout().Update(tiny); Check(((TextBlock)tiny.Child).FontSize==14,"layout recovers after transient viewport");
        int start=0,stop=0;
        var dashboard=new LectureDashboard(delegate{start++;},delegate{stop++;},delegate{},delegate{});
        using(dashboard.Window)
        {
            dashboard.Window.Show(); Pump();
            var screen=Forms.Screen.FromHandle(dashboard.Window.Handle).WorkingArea;
            Check(Math.Abs(dashboard.Window.Left+(dashboard.Window.Width/2)-(screen.Left+screen.Width/2))<=1 && Math.Abs(dashboard.Window.Top+(dashboard.Window.Height/2)-(screen.Top+screen.Height/2))<=1,"first window opens centered");
            for(int i=0;i<5;i++) { dashboard.Window.WindowState=Forms.FormWindowState.Minimized; Pump(); dashboard.Window.WindowState=Forms.FormWindowState.Normal; Pump(); }
            Check(dashboard.Window.Find<TextBlock>("StateTitle").FontSize>=1,"five minimize and restore cycles keep valid fonts");
            dashboard.Window.WindowState=Forms.FormWindowState.Minimized; Pump(); dashboard.Window.Hide(); dashboard.Window.ShowAndActivate(); Pump();
            Check(dashboard.Window.Visible && dashboard.Window.WindowState==Forms.FormWindowState.Normal,"explicit open restores a hidden minimized window");
            for(int i=0;i<5;i++) { dashboard.Window.Hide(); Pump(); dashboard.Window.ShowAndActivate(); Pump(); }
            var restoreHost=(System.Windows.Forms.Integration.ElementHost)dashboard.Window.Controls[0];
            var source=(System.Windows.Interop.HwndSource)PresentationSource.FromVisual(dashboard.Window.View);
            Check(restoreHost.BackColor==dashboard.Window.BackColor && source.CompositionTarget.BackgroundColor==System.Windows.Media.Color.FromRgb(246,250,255),"native and WPF surfaces retain the light background after hide and restore");
            dashboard.Window.Find<Button>("Maximize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(dashboard.Window.Bounds==screen,"maximized window fits working area");
            Check(((Border)dashboard.Window.View).CornerRadius.TopLeft==0,"maximized border keeps square unclipped corners");
            Check((string)((FrameworkElement)dashboard.Window.Find<Button>("Maximize").Content).Tag=="restore","maximized button uses two overlapping squares");
            Render(dashboard.Window,Path.Combine(output,"main-maximized.png"),1);
            dashboard.Window.Find<Button>("Maximize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(dashboard.Window.View.LayoutTransform.Value.IsIdentity,"main renders text without fractional transform");
            var settingsButton=dashboard.Window.Find<Button>("Settings"); var minimizeButton=dashboard.Window.Find<Button>("Minimize");
            var settingsRight=settingsButton.TranslatePoint(new System.Windows.Point(settingsButton.ActualWidth,0),dashboard.Window.View).X;
            var minimizeLeft=minimizeButton.TranslatePoint(new System.Windows.Point(0,0),dashboard.Window.View).X;
            Check(minimizeLeft-settingsRight>=12,"settings button has a gap before title controls");
            dashboard.Window.Find<Button>("Primary").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(start==1,"primary action wired");
            dashboard.Update(false,false,true,false,TimeSpan.FromMinutes(24),2847,12,1);
            dashboard.Window.Find<Button>("Stop").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(stop==1,"stop action wired");
            dashboard.Caption("speaker-test","Guten",DateTime.Today,"Demo speaker A");
            dashboard.Caption("speaker-test","Guten Morgen",DateTime.Today,"Demo speaker A");
            var speakerLine=(Grid)dashboard.Window.Find<StackPanel>("TranscriptRows").Children[1]; var speakerText=(TextBlock)speakerLine.Children[1];
            Check(speakerText.Inlines.FirstInline is System.Windows.Documents.Bold,"speaker name is bold");
            Check(speakerText.Text=="Demo speaker A: Guten Morgen" && dashboard.Window.Find<StackPanel>("TranscriptRows").Children.Count==2,"caption updates retain speaker and one row");
            for(int i=0;i<260;i++) dashboard.Caption("line"+i,"Тест прокрутки "+i+". Это проверочные данные интерфейса, в архив лекции они не записываются.",DateTime.Today.AddSeconds(i*20));
            Pump();
            Check(dashboard.Window.Find<StackPanel>("TranscriptRows").Children.Count==201,"transcript view stays bounded");
            var scroll=dashboard.Window.Find<ScrollViewer>("TranscriptScroll");
            Check(scroll.Padding.Left>=12 && scroll.Padding.Right>=12,"transcript has inner padding on both sides");
            Check(scroll.FocusVisualStyle==null,"transcript has no dotted focus outline");
            var tab=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,System.Windows.PresentationSource.FromVisual(scroll),0,System.Windows.Input.Key.Tab) { RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            scroll.RaiseEvent(tab); Check(tab.Handled,"TAB is intercepted without navigation");
            Check(scroll.ScrollableHeight>0,"long transcript exposes scrolling"); scroll.ScrollToEnd(); Pump(); Check(scroll.VerticalOffset>0,"scroll position moves");
            dashboard.Update(false,true,true,false,TimeSpan.FromMinutes(24),2847,12,1);
            Check(!dashboard.Window.Find<TextBlock>("StateTitle").Text.Contains("идёт"),"paused state is visible");
            Render(dashboard.Window,Path.Combine(output,"main-100.png"),1); Render(dashboard.Window,Path.Combine(output,"main-125.png"),1.25); Render(dashboard.Window,Path.Combine(output,"main-150.png"),1.5); Render(dashboard.Window,Path.Combine(output,"main-200.png"),2);
            foreach(string expected in new[]{"Ожидание демонстрации экрана","Ошибка сохранения","Сохранение","Запись остановлена"})
            {
                dashboard.Update(expected=="Запись остановлена",false,false,expected=="Ошибка сохранения",TimeSpan.Zero,0,0,0,expected=="Сохранение");
                Check(dashboard.Window.Find<TextBlock>("StateTitle").Text==expected,"visible state: "+expected);
            }
            dashboard.Reset(); Check(dashboard.Window.Find<StackPanel>("TranscriptRows").Children.Count==1,"new lecture resets the view only");
        }
        var preferences=new UiPreferences(); var settings=new LectureSettings(3,preferences,output,delegate{});
        using(settings.Window)
        {
            settings.Window.Show(); Pump(); Check(settings.Window.View.LayoutTransform.Value.IsIdentity,"settings text renders without fractional scaling");
            settings.Window.Find<Button>("Increase").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(settings.Window.Find<TextBox>("Delay").Text=="4","stepper increases");
            settings.Window.Find<Button>("Decrease").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(settings.Window.Find<TextBox>("Delay").Text=="3","stepper decreases");
            foreach(string key in new[]{"Decrease","Increase"}) {
                var button=settings.Window.Find<Button>(key); var glyph=(Viewbox)button.Content;
                Check(button.Padding.Left==0 && glyph.ActualWidth>=15,"stepper glyph has room: "+key);
                Check(glyph.Child is Canvas,"stepper geometry uses fixed canvas: "+key);
                var glyphPath=(System.Windows.Shapes.Path)((Canvas)glyph.Child).Children[0]; var glyphBounds=glyphPath.Data.Bounds;
                var glyphCenter=glyphPath.TransformToAncestor(button).Transform(new System.Windows.Point(glyphBounds.X+glyphBounds.Width/2,glyphBounds.Y+glyphBounds.Height/2));
                Check(Math.Abs(glyphCenter.X-button.ActualWidth/2)<=1 && Math.Abs(glyphCenter.Y-button.ActualHeight/2)<=1,"stepper symbol centered: "+key);
            }
            Check(settings.Window.Find<ScrollViewer>("BodyScroll").ScrollableHeight==0,"general settings fit without scrolling");
            var body=settings.Window.Find<ScrollViewer>("BodyScroll");
            var firstCard=settings.Window.Find<Border>("CaptureSettingsCard"); var lastCard=settings.Window.Find<Border>("RecordingSettingsCard");
            double topGap=firstCard.TranslatePoint(new System.Windows.Point(0,0),body).Y;
            double bottomGap=body.ActualHeight-lastCard.TranslatePoint(new System.Windows.Point(0,lastCard.ActualHeight),body).Y;
            Check(Math.Abs(topGap-bottomGap)<=1,"settings cards have equal top and bottom clearance");
            var clock=settings.Window.Find<ContentControl>("CaptureHeaderIcon"); var labels=settings.Window.Find<StackPanel>("CaptureHeaderLabels");
            double clockCenter=clock.TranslatePoint(new System.Windows.Point(0,clock.ActualHeight/2),firstCard).Y;
            double labelCenter=labels.TranslatePoint(new System.Windows.Point(0,labels.ActualHeight/2),firstCard).Y;
            Check(Math.Abs(clockCenter-labelCenter)<=1,"clock centers across heading and subtitle");
            foreach(string key in new[]{"CaptureSettingsCard","FilterSettingsCard","RecordingSettingsCard"}) {
                var area=settings.Window.Find<Border>(key); CheckCard(area,area); Check(true,"all card text fits: "+key);
            }
            Check(settings.Window.Find<ProgressBar>("DuplicateBar").ActualHeight>=7 && settings.Window.Find<ProgressBar>("StableBar").ActualHeight>=7,"progress bars retain full height");
            var description=settings.Window.Find<TextBlock>("DelayDescription");
            var card=(Border)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(description))));
            var descriptionBounds=description.TransformToAncestor(card).TransformBounds(new Rect(0,0,description.ActualWidth,description.ActualHeight));
            Check(descriptionBounds.Bottom<=card.ActualHeight-8,"delay description has bottom clearance");
            foreach(string key in new[]{"Minimize","Maximize","Close"}) {
                var title=settings.Window.Find<Button>(key); var parent=(StackPanel)title.Parent;
                var titleBounds=title.TransformToAncestor(parent).TransformBounds(new Rect(0,0,title.ActualWidth,title.ActualHeight));
                Check(titleBounds.Bottom<=parent.ActualHeight,"title highlight is not clipped: "+key);
                Check(title.Margin.Bottom==0,"title buttons do not inherit navigation margins: "+key);
            }
            var done=settings.Window.Find<Button>("Done");
            Check(((SolidColorBrush)done.Background).Color==Colors.White,"done has neutral fill");
            var checkContent=(StackPanel)done.Content; var checkIcon=(Viewbox)checkContent.Children[0];
            var checkCanvas=(Canvas)checkIcon.Child; var checkPath=(System.Windows.Shapes.Path)checkCanvas.Children[0];
            var checkBounds=checkPath.Data.Bounds; var checkPoint=checkPath.TransformToAncestor(done).Transform(new System.Windows.Point(checkBounds.X+checkBounds.Width/2,checkBounds.Y+checkBounds.Height/2));
            Check(Math.Abs(checkPoint.Y-done.ActualHeight/2)<=1,"checkmark centered in done button");
            Check(done.FocusVisualStyle==null,"focus outlines disabled");
            var configure=settings.Window.Find<Button>("ConfigureFilter");
            var content=(StackPanel)configure.Content; var caption=(TextBlock)content.Children[1];
            Check(Math.Abs(caption.FontSize-configure.FontSize)<0.1,"button caption uses button font size");
            var naturalLabel=new TextBlock { Text=caption.Text,FontFamily=caption.FontFamily,FontSize=caption.FontSize,FontWeight=caption.FontWeight };
            TextOptions.SetTextFormattingMode(naturalLabel,TextFormattingMode.Display);
            naturalLabel.Measure(new System.Windows.Size(Double.PositiveInfinity,Double.PositiveInfinity));
            Check(caption.ActualWidth>=naturalLabel.DesiredSize.Width-1 && caption.ActualHeight>=naturalLabel.DesiredSize.Height-1,"configure caption fits without cropping");
            double center=content.TranslatePoint(new System.Windows.Point(0,content.ActualHeight/2),configure).Y;
            Check(Math.Abs(center-configure.ActualHeight/2)<=1.1,"icon and caption centered in button");
            Render(settings.Window,Path.Combine(output,"settings-100.png"),1);
            foreach(string key in new[]{"Minimize","Maximize","Close","ConfigureFilter","Done"}) {
                var button=settings.Window.Find<Button>(key); button.Focus(); Pump();
                var frame=(Border)button.Template.FindName("Frame",button);
                var frameBounds=frame.TransformToAncestor(button).TransformBounds(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));
                Check(frameBounds.Top>=0 && frameBounds.Bottom<=button.ActualHeight,"rounded focus frame stays inside: "+key);
                Check(button.IsMouseOver || frame.Background==button.Background,"click focus does not leave hover fill: "+key);
                Render(settings.Window,Path.Combine(output,"focus-"+key+".png"),1);
            }
            settings.Window.Find<TextBox>("Delay").Focus(); Pump(); Render(settings.Window,Path.Combine(output,"settings-125.png"),1.25); Render(settings.Window,Path.Combine(output,"settings-150.png"),1.5); Render(settings.Window,Path.Combine(output,"settings-200.png"),2);
            settings.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); Check(settings.Window.Find<Border>("OtherPage").Visibility==Visibility.Visible,"filter page opens");
            foreach(string name in new[]{"Transcription","Storage","Interface","About","General"}) settings.Window.Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(settings.Window.Find<FrameworkElement>("GeneralPage").Visibility==Visibility.Visible,"all navigation pages respond");
            foreach(string name in new[]{"Slides","Transcription","Storage","Interface","About"})
            {
                settings.Window.Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); Check(settings.Window.Find<ScrollViewer>("BodyScroll").ScrollableHeight==0,"settings page fits: "+name); Render(settings.Window,Path.Combine(output,"settings-"+name+".png"),1);
            }
            settings.Window.ClientSize=new System.Drawing.Size(900,650); Pump();
            foreach(string name in new[]{"General","Slides","Transcription","Storage","Interface","About"}) {
                settings.Window.Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                Check(settings.Window.Find<ScrollViewer>("BodyScroll").ScrollableHeight==0,"small settings fit without scrolling: "+name);
            }
            settings.Window.Find<Button>("General").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(string key in new[]{"CaptureSettingsCard","FilterSettingsCard","RecordingSettingsCard"}) { var area=settings.Window.Find<Border>(key); CheckCard(area,area); Check(true,"small card text fits: "+key); }
            foreach(string key in new[]{"SaveSlides","SaveCaptions","HideToTray"}) {
                var toggle=settings.Window.Find<System.Windows.Controls.CheckBox>(key); var label=settings.Window.Find<TextBlock>(key+"Label");
                var toggleCenter=toggle.TransformToAncestor(settings.Window.View).Transform(new System.Windows.Point(0,toggle.ActualHeight/2));
                var rowLabelCenter=label.TransformToAncestor(settings.Window.View).Transform(new System.Windows.Point(0,label.ActualHeight/2));
                Check(Math.Abs(toggleCenter.Y-rowLabelCenter.Y)<=1,"main recording label centers opposite its switch: "+key);
            }
            Check(settings.Window.Find<TextBlock>("DelayDescription").FontSize%1==0,"small window uses native whole-DIP font size");
            Render(settings.Window,Path.Combine(output,"settings-small.png"),1);
        }
        using(var menu=new DesignWindow("tray.xaml",400,450)) {
            menu.Show(); Pump(); Check(menu.ClientSize.Width==340 && menu.ClientSize.Height==383,"tray enlarged to readable 340 x 383"); Check(menu.View.LayoutTransform.Value.IsIdentity,"tray renders without fractional scaling"); Check(menu.Find<System.Windows.Controls.Image>("BrandImage").Source is DrawingImage,"brand is vector drawing"); foreach(string name in new[]{"Archive","Settings"}) { var item=menu.Find<Button>(name); Check(item.Margin.Top>=6 && item.Margin.Bottom>=6,"menu highlight clears separators: "+name); } var exit=menu.Find<Button>("Exit"); Check(exit.Margin.Bottom>=6,"exit highlight has bottom clearance");
            Check(menu.Find<TextBlock>("RecordLabel").Text=="Старт записи" && !menu.Find<Button>("Stop").IsEnabled,"stopped tray offers start and disables stop");
            TrayMenuState.Update(menu,false,false,false,false,false); Pump();
            var badge=menu.Find<Border>("StateBadge"); var stateLabel=menu.Find<TextBlock>("State"); var badgeTextBounds=stateLabel.TransformToAncestor(badge).TransformBounds(new Rect(0,0,stateLabel.ActualWidth,stateLabel.ActualHeight));
            Check(badgeTextBounds.Right<=badge.ActualWidth-8 && badgeTextBounds.Top>=0 && badgeTextBounds.Bottom<=badge.ActualHeight,"long waiting label stays inside state badge");
            Check(menu.Find<TextBlock>("RecordLabel").Text=="Пауза" && menu.Find<Button>("Stop").IsEnabled,"active tray offers pause and stop");
            Render(menu,Path.Combine(output,"tray-waiting.png"),1);
            TrayMenuState.Update(menu,false,true,true,false,false); Pump(); Check(menu.Find<TextBlock>("RecordLabel").Text=="Продолжить","paused tray offers resume on same button");
            Render(menu,Path.Combine(output,"tray-100.png"),1);
            exit.Tag="Hover"; exit.UpdateLayout(); var exitFrame=(Border)exit.Template.FindName("Frame",exit); Check(((SolidColorBrush)exitFrame.BorderBrush).Color==System.Windows.Media.Color.FromRgb(243,169,178),"exit hover has soft red outline"); Render(menu,Path.Combine(output,"tray-exit-hover.png"),1);
        }
        using(var confirm=new StopConfirmation()) { confirm.Show(); Pump(); Render(confirm,Path.Combine(output,"confirm-stop.png"),1); confirm.Find<Button>("Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(!confirm.Confirmed,"cancel does not approve stopping"); }
        using(var confirm=new StopConfirmation()) { confirm.Show(); Pump(); confirm.Find<Button>("Confirm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(confirm.Confirmed,"stop requires explicit confirmation"); }
        var host=new LectureDashboard(delegate{},delegate{},delegate{},delegate{});
        using(host.Window) using(var page=new LectureSettings(3,new UiPreferences(),output,delegate{}).Window)
        {
            host.Window.Show(); Pump(); int before=Forms.Application.OpenForms.Count; bool returned=false;
            host.Window.ShowPage(page,delegate { returned=true; }); Pump();
            Check(Forms.Application.OpenForms.Count==before,"settings reuse main window");
            Check(page.Shell==host.Window,"page uses main shell");
            var pageContainer=(Grid)((System.Windows.Forms.Integration.ElementHost)host.Window.Controls[0]).Child;
            var bounds=page.View.TransformToAncestor(pageContainer).TransformBounds(new Rect(0,0,page.View.ActualWidth,page.View.ActualHeight));
            Check(Math.Abs(bounds.Width-pageContainer.ActualWidth)<2,"page fills shell without a second frame");
            Check(Math.Abs(bounds.Height-pageContainer.ActualHeight)<2,"page fills shell height");
            page.Return(); Pump(); Check(returned,"back restores dashboard");
            host.Window.ClientSize=new System.Drawing.Size(900,650); Pump();
            var viewport=(Grid)((System.Windows.Forms.Integration.ElementHost)host.Window.Controls[0]).Child;
            var frame=host.Window.View.TransformToAncestor(viewport).TransformBounds(new Rect(0,0,host.Window.View.ActualWidth,host.Window.View.ActualHeight));
            Check(frame.Width<=viewport.ActualWidth+1 && frame.Height<=viewport.ActualHeight+1,"small window fits without outer scrolling");
            var primary=host.Window.Find<Button>("Primary");
            var buttonBounds=primary.TransformToAncestor(viewport).TransformBounds(new Rect(0,0,primary.ActualWidth,primary.ActualHeight));
            Check(buttonBounds.Bottom<=viewport.ActualHeight && buttonBounds.Left>=0,"bottom buttons remain fully inside window");
            Render(host.Window,Path.Combine(output,"main-small.png"),1);
        }
        using(var cleanup=new CleanupPage(output,delegate{})) { cleanup.Folder.Text=Path.Combine(output,"demo-cleanup","scr"); Directory.CreateDirectory(cleanup.Folder.Text); string service=Path.Combine(output,"demo-cleanup","_service","cleanup"); Directory.CreateDirectory(service); File.WriteAllText(Path.Combine(service,"preview.json"),"{\"scanned\":0,\"proposals\":[]}"); cleanup.Show(); Pump(); cleanup.LoadPreview(); Pump(); Check(cleanup.Status.Text.Contains("Проверено"),"cleanup reads existing preview safely"); Render(cleanup,Path.Combine(output,"cleanup-100.png"),1); }
        using(var popup=new MentionPopup()) { popup.Notify("Проверка оформления: Demo speaker A, eine Frage bitte."); Pump(); Render(popup,Path.Combine(output,"mention-100.png"),1); }
        foreach(bool compact in new[]{false,true}) using(var preview=ArchivePreview(compact).Window) {
            preview.Show(); Pump();
            var image=preview.Find<System.Windows.Controls.Image>("SlideImage");
            Check(image.Source!=null && image.Stretch==Stretch.Uniform,"archive slide fits without cropping: "+(compact?"compact":"normal"));
            Render(preview,Path.Combine(output,compact?"content-compact.png":"content-normal.png"),1);
        }
        Console.WriteLine("UI_ARTIFACTS="+output);
        UiPreferences.TestPath=Path.Combine(output,"preferences.json");
        var options=new UiPreferences(); options.Save();
        var startup=new WindowsAutostart();
        Check(!startup.Enabled,"absent startup shortcut is shown as off");
        using(var cancelled=new LectureSettings(3,options,output,delegate{})) {
            cancelled.Window.Show(); Pump(); cancelled.Window.Find<System.Windows.Controls.CheckBox>("WindowsAutostart").IsChecked=true;
            Check(!startup.Enabled && !File.Exists(startup.ShortcutPath),"startup switch does not register before Done");
            cancelled.Window.Find<Button>("Back").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(!startup.Enabled,"back cancels startup selection");
        }
        using(var enable=new LectureSettings(3,options,output,delegate{})) {
            enable.Window.Show(); Pump(); enable.Window.Find<System.Windows.Controls.CheckBox>("WindowsAutostart").IsChecked=true;
            enable.Window.Find<Button>("Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(enable.Applied && startup.Enabled && File.Exists(startup.ShortcutPath),"Done creates a real Windows startup shortcut in the isolated test directory");
            Check(startup.Arguments==WindowsAutostart.BackgroundArgument,"Windows startup shortcut requests tray-only launch");
            startup.EnsureStartInTray(); Check(startup.Arguments==WindowsAutostart.BackgroundArgument,"startup migration does not duplicate the background argument");
        }
        using(var disable=new LectureSettings(3,options,output,delegate{})) {
            disable.Window.Show(); Pump(); Check(disable.Window.Find<System.Windows.Controls.CheckBox>("WindowsAutostart").IsChecked==true,"startup selection is loaded from Windows shortcut on reopening");
            disable.Window.Find<System.Windows.Controls.CheckBox>("WindowsAutostart").IsChecked=false;
            disable.Window.Find<Button>("Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(disable.Applied && !startup.Enabled && !File.Exists(startup.ShortcutPath),"Done removes this copy's startup shortcut when switched off");
        }
        string actualTarget=WindowsAutostart.TestExecutable;
        string otherExecutable=Path.Combine(output,"another copy with spaces","SaveLecture.exe"); Directory.CreateDirectory(Path.GetDirectoryName(otherExecutable)); File.WriteAllText(otherExecutable,"Artificial shortcut target; never launched.");
        WindowsAutostart.TestExecutable=otherExecutable;
        var otherStartup=new WindowsAutostart(); otherStartup.SetEnabled(true);
        Check(otherStartup.Enabled,"startup supports an executable path containing spaces");
        WindowsAutostart.TestExecutable=actualTarget;
        byte[] foreignShortcut=File.ReadAllBytes(startup.ShortcutPath); bool blocked=false;
        try { startup.SetEnabled(false); } catch(IOException) { blocked=true; }
        Check(blocked && Convert.ToBase64String(File.ReadAllBytes(startup.ShortcutPath))==Convert.ToBase64String(foreignShortcut),"disabling preserves another copy's shortcut byte for byte");
        blocked=false; try { startup.SetEnabled(true); } catch(IOException) { blocked=true; }
        Check(blocked && Convert.ToBase64String(File.ReadAllBytes(startup.ShortcutPath))==Convert.ToBase64String(foreignShortcut),"enabling does not overwrite another copy's startup shortcut");
        otherStartup.SetEnabled(false);
        using(var newSettings=new LectureSettings(3,options,output,delegate{})) {
            newSettings.Window.Show(); Pump(); newSettings.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            var panel=newSettings.Window.Find<StackPanel>("PageControls");
            foreach(var child in panel.Children) {
                var slider=child as Slider; if(slider==null || slider.Name!="DuplicatesSlider") continue;
                slider.Value=89; Slider.IncreaseSmall.Execute(null,slider);
                Check(slider.Value==89.5,"slider interaction reaches a half-percent between major marks");
            }
            foreach(var child in panel.Children) { var slider=child as Slider; if(slider==null) continue; if(slider.Name=="DuplicatesSlider") slider.Value=89.5; if(slider.Name=="ShiftSlider") slider.Value=5; if(slider.Name=="IntervalSlider") slider.Value=2.5; }
            Check(options.DuplicateThreshold==.89 && UiPreferences.Load().DuplicateThreshold==.89,"moving sliders does not change active or saved settings before Done");
            newSettings.Window.Find<Button>("Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        }
        var loaded=UiPreferences.Load(); Check(loaded.DuplicateThreshold==.895 && loaded.PixelShiftThreshold==5 && loaded.CaptureIntervalSeconds==2.5,"intermediate filter values survive Apply and reload");
        string savedPreferences=File.ReadAllText(UiPreferences.TestPath);
        using(var cancelled=new LectureSettings(3,options,output,delegate{})) {
            cancelled.Window.Show(); Pump(); cancelled.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(var child in cancelled.Window.Find<StackPanel>("PageControls").Children) { var slider=child as Slider; if(slider!=null) slider.Value=slider.Minimum; }
            cancelled.Window.Find<Button>("Back").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(!cancelled.Applied && options.DuplicateThreshold==.895 && File.ReadAllText(UiPreferences.TestPath)==savedPreferences,"back discards edited sliders and leaves saved settings unchanged");
        }
        using(var cancelled=new LectureSettings(3,options,output,delegate{})) {
            cancelled.Window.Show(); Pump(); cancelled.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(var child in cancelled.Window.Find<StackPanel>("PageControls").Children) { var slider=child as Slider; if(slider!=null) slider.Value=slider.Maximum; }
            cancelled.Window.Find<Button>("Close").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(!cancelled.Applied && options.DuplicateThreshold==.895 && File.ReadAllText(UiPreferences.TestPath)==savedPreferences,"close discards edited sliders and leaves saved settings unchanged");
        }
        using(var languageSettings=new LectureSettings(3,options,output,delegate{})) {
            languageSettings.Window.Show(); Pump(); languageSettings.Window.Find<Button>("Interface").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(var child in languageSettings.Window.Find<StackPanel>("PageControls").Children) {
                var picker=child as LanguagePicker; if(picker==null) continue;
                ((Button)((System.Windows.Controls.Primitives.UniformGrid)picker.Child).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(picker.SelectedLanguage=="de" && options.Language=="ru" && UiText.Language=="ru","language choice previews selection without applying before Done");
            }
            languageSettings.Window.Find<Button>("Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(options.Language=="de" && UiPreferences.Load().Language=="de","language buttons apply and persist through Done");
        }
        using(var newSettings=new LectureSettings(3,options,output,delegate{})) {
            newSettings.Window.Show(); Pump(); newSettings.Window.Find<Button>("Storage").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(var child in newSettings.Window.Find<StackPanel>("PageControls").Children) { var input=child as TextBox; if(input!=null) input.Text=Path.Combine(output,"new archive"); }
            newSettings.Window.Find<Button>("Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        }
        Check(Directory.Exists(options.StoragePath) && UiPreferences.Load().StoragePath==options.StoragePath,"selected writable archive path persists");
        using(var newSettings=new LectureSettings(3,options,output,delegate{},delegate{return false;})) {
            newSettings.Window.Show(); Pump(); newSettings.Window.Find<Button>("Storage").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            foreach(var child in newSettings.Window.Find<StackPanel>("PageControls").Children) { var input=child as TextBox; if(input!=null) Check(!input.IsEnabled,"archive cannot change while recording"); }
        }
        foreach(string language in new[]{"ru","de","en"}) {
            UiText.Language=language;
            UiPreferences.TestPath=Path.Combine(output,"consent-preferences-"+language+".json");
            using(var notice=new FirstRunNotice(new UiPreferences { Language=language })) {
                notice.Show(); Pump(); Check(!notice.Find<Button>("Accept").IsEnabled,"first launch blocks unacknowledged collection: "+language);
                var picker=(LanguagePicker)notice.Find<ContentControl>("LanguageChoice").Content;
                int choice=language=="ru"?1:0;
                var languageButtons=(System.Windows.Controls.Primitives.UniformGrid)picker.Child;
                ((Button)languageButtons.Children[choice]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                Check(UiText.Language==(choice==1?"de":"ru") && !notice.Find<Button>("Accept").IsEnabled,"first-run language buttons update text without accepting consent: "+language);
                ((Button)languageButtons.Children[language=="ru"?0:language=="de"?1:2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                var acknowledge=notice.Find<System.Windows.Controls.CheckBox>("Acknowledged"); var accept=notice.Find<Button>("Accept");
                var consentBounds=acknowledge.TransformToAncestor(notice.View).TransformBounds(new Rect(0,0,acknowledge.ActualWidth,acknowledge.ActualHeight));
                var acceptBounds=accept.TransformToAncestor(notice.View).TransformBounds(new Rect(0,0,accept.ActualWidth,accept.ActualHeight));
                Check(consentBounds.Bottom+12<acceptBounds.Top,"first-run consent stays clear of action buttons: "+language);
                CheckCard(notice.View,notice.View); Render(notice,Path.Combine(output,"consent-"+language+".png"),1);
                var languageHolder=notice.Find<ContentControl>("LanguageChoice"); var holderBounds=languageHolder.TransformToAncestor(notice.View).TransformBounds(new Rect(0,0,languageHolder.ActualWidth,languageHolder.ActualHeight));
                var pickerBounds=picker.TransformToAncestor(notice.View).TransformBounds(new Rect(0,0,picker.ActualWidth,picker.ActualHeight));
                var header=(FrameworkElement)((Grid)languageHolder.Parent).Parent; var headerBounds=header.TransformToAncestor(notice.View).TransformBounds(new Rect(0,0,header.ActualWidth,header.ActualHeight));
                Check(pickerBounds.Bottom<=holderBounds.Bottom+1 && pickerBounds.Bottom<=headerBounds.Bottom+1,"complete language selector fits below the heading: "+language);
                notice.Find<System.Windows.Controls.CheckBox>("Acknowledged").IsChecked=true; Pump(); Check(notice.Find<Button>("Accept").IsEnabled,"acknowledgement enables Continue: "+language);
                notice.Find<Button>("Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                Check(!File.Exists(UiPreferences.TestPath),"cancel does not persist acceptance: "+language);
            }
            using(var notice=new FirstRunNotice(new UiPreferences { Language=language })) {
                notice.Show(); Pump(); notice.Find<System.Windows.Controls.CheckBox>("Acknowledged").IsChecked=true;
                notice.Find<Button>("Accept").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                var accepted=UiPreferences.Load();
                Check(accepted.ConsentNoticeVersion==1 && accepted.Language==language,"explicit acceptance and language survive restart: "+language);
            }
            using(var demo=ArchivePreview(false).Window) {
                demo.Show(); Pump(); demo.Find<ScrollViewer>("TranscriptScroll").ScrollToTop(); Pump();
                var settingsButton=demo.Find<Button>("Settings"); var settingsContent=(StackPanel)settingsButton.Content;
                var caption=(TextBlock)settingsContent.Children[settingsContent.Children.Count-1];
                var natural=new TextBlock { Text=caption.Text,FontFamily=caption.FontFamily,FontSize=caption.FontSize,FontWeight=caption.FontWeight };
                TextOptions.SetTextFormattingMode(natural,TextFormattingMode.Display);
                natural.Measure(new System.Windows.Size(Double.PositiveInfinity,Double.PositiveInfinity));
                Check(natural.DesiredSize.Width<=caption.ActualWidth+1,"settings button shows complete translated caption: "+language+" ("+natural.DesiredSize.Width+" <= "+caption.ActualWidth+")");
                Render(demo,Path.Combine(output,"demo-"+language+".png"),1);
            }
            var localized=new UiPreferences { Language=language };
            using(var newSettings=new LectureSettings(3,localized,output,delegate{})) {
                newSettings.Window.ClientSize=new System.Drawing.Size(900,650); newSettings.Window.Show(); Pump();
                foreach(string name in new[]{"General","Slides","Transcription","Storage","Interface","About"}) {
                    var row=(StackPanel)newSettings.Window.Find<Button>(name).Content; var text=(TextBlock)row.Children[1];
                    var natural=new TextBlock { Text=text.Text,FontFamily=text.FontFamily,FontSize=text.FontSize,FontWeight=text.FontWeight };
                    TextOptions.SetTextFormattingMode(natural,TextFormattingMode.Display); natural.Measure(new System.Windows.Size(Double.PositiveInfinity,Double.PositiveInfinity));
                    Check(text.ActualWidth>=natural.DesiredSize.Width-1,"small settings navigation caption fits: "+language+" / "+name);
                }
                var footer=newSettings.Window.Find<Border>("SettingsFooter"); var cleanup=newSettings.Window.Find<Button>("OldScreens"); var startupSwitch=newSettings.Window.Find<System.Windows.Controls.CheckBox>("WindowsAutostart");
                var done=newSettings.Window.Find<Button>("Done"); var hint=newSettings.Window.Find<TextBlock>("SaveHint");
                var cleanupBounds=cleanup.TransformToAncestor(footer).TransformBounds(new Rect(0,0,cleanup.ActualWidth,cleanup.ActualHeight));
                var switchBounds=startupSwitch.TransformToAncestor(footer).TransformBounds(new Rect(0,0,startupSwitch.ActualWidth,startupSwitch.ActualHeight));
                var hintBounds=hint.TransformToAncestor(footer).TransformBounds(new Rect(0,0,hint.ActualWidth,hint.ActualHeight));
                var doneBounds=done.TransformToAncestor(footer).TransformBounds(new Rect(0,0,done.ActualWidth,done.ActualHeight));
                Check(switchBounds.Left-cleanupBounds.Right>=10,"startup switch stays to the right of manual cleanup with a gap: "+language);
                Check(doneBounds.Left-hintBounds.Right>=10 && doneBounds.Left-hintBounds.Right<=24,"saving hint sits near Done with a clear gap: "+language);
                Check(hintBounds.Left>=switchBounds.Right+10 && hintBounds.Bottom<footer.ActualHeight-3,"footer controls and saving hint fit without overlap: "+language);
                var naturalHint=new TextBlock { Text=hint.Text,FontFamily=hint.FontFamily,FontSize=hint.FontSize };
                TextOptions.SetTextFormattingMode(naturalHint,TextFormattingMode.Display); naturalHint.Measure(new System.Windows.Size(Double.PositiveInfinity,Double.PositiveInfinity));
                Check(hint.TextWrapping==TextWrapping.NoWrap && hint.ActualHeight<=naturalHint.DesiredSize.Height+1 && hint.ActualWidth>=naturalHint.DesiredSize.Width-1,"apply hint is one complete uncut line: "+language);
                CheckCard(footer,footer);
                foreach(string page in new[]{"General","Slides","Transcription","Storage","Interface","About"}) {
                    newSettings.Window.Find<Button>(page).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                    foreach(string key in page=="General"?new[]{"CaptureSettingsCard","FilterSettingsCard","RecordingSettingsCard"}:new[]{"OtherPage"}) { var card=newSettings.Window.Find<Border>(key); CheckCard(card,card); }
                    Render(newSettings.Window,Path.Combine(output,"settings-"+page+"-"+language+".png"),1);
                }
                newSettings.Window.Find<Button>("Slides").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); Render(newSettings.Window,Path.Combine(output,"filters-"+language+".png"),1);
            }
        }
        UiText.Language="ru"; UiPreferences.TestPath=null;
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SaveLecture
{
    internal sealed class CleanupPage : DesignWindow
    {
        public readonly TextBox Folder=new TextBox { FontSize=14,Padding=new Thickness(10),Margin=new Thickness(0,0,0,13) };
        public readonly Slider Threshold=new Slider { Minimum=80,Maximum=100,Value=94,TickFrequency=1,IsSnapToTickEnabled=true,Height=26 };
        public readonly TextBlock Status=new TextBlock { FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,14),Text="Выберите папку scr для предварительного просмотра." };
        public readonly StackPanel Panel;
        readonly StackPanel results=new StackPanel();
        System.Collections.ArrayList proposals;
        int pair;
        public CleanupPage(string archive,Action<string> run) : base("settings.xaml",1264,1008)
        {
            Find<TextBlock>("SectionHeading").Text="Снимки";
            Find<Border>("Sidebar").Visibility=Visibility.Collapsed;
            Grid.SetColumn(Find<ScrollViewer>("BodyScroll"),0); Grid.SetColumnSpan(Find<ScrollViewer>("BodyScroll"),2);
            Find<FrameworkElement>("GeneralPage").Visibility=Visibility.Collapsed;
            Find<Border>("OtherPage").Visibility=Visibility.Visible;
            Find<TextBlock>("PageTitle").Text="Очистить папку вручную";
            Find<TextBlock>("PageDescription").Text="Просмотр дублей и восстановление после очистки";
            Panel=Find<StackPanel>("PageControls"); Panel.Children.Add(Folder);
            var browse=new Button { Content="Выбрать папку",HorizontalAlignment=HorizontalAlignment.Left };
            browse.Click+=delegate { using(var dialog=new System.Windows.Forms.FolderBrowserDialog { SelectedPath=archive }) if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK) Folder.Text=dialog.SelectedPath; }; Panel.Children.Add(browse);
            var label=new TextBlock { FontSize=14,Text="Порог похожести: 94%",Margin=new Thickness(0,16,0,8) }; Panel.Children.Add(label); Panel.Children.Add(Threshold);
            Threshold.ValueChanged+=delegate { label.Text="Порог похожести: "+Threshold.Value.ToString("0")+"%"; };
            var actions=new WrapPanel { Margin=new Thickness(0,16,0,0) }; Panel.Children.Add(actions);
            foreach(string mode in new[]{"preview","apply","undo"})
            {
                string current=mode; var button=new Button { Content=mode=="preview"?"Просмотр":mode=="apply"?"Перенести дубли":"Восстановить",Margin=new Thickness(0,0,10,6) };
                button.Click+=delegate { run(current); }; actions.Children.Add(button);
            }
            Panel.Children.Add(Status); Panel.Children.Add(results);
            Find<Button>("OldScreens").Visibility=Visibility.Collapsed;
            Find<TextBlock>("SaveHint").Visibility=Visibility.Collapsed;
            Find<CheckBox>("WindowsAutostart").Visibility=Visibility.Collapsed;
            CanReturn=delegate { return Panel.IsEnabled; };
            foreach(string name in new[]{"General","Slides","Transcription","Storage","Interface","About"}) Find<Button>(name).Visibility=Visibility.Collapsed;
            Wire("Done",delegate { if(Panel.IsEnabled) Return(); });
        }
        public void LoadPreview()
        {
            results.Children.Clear();
            string path=Path.Combine(Directory.GetParent(Folder.Text).FullName,"_service","cleanup","preview.json");
            var data=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
            var items=data["proposals"] as System.Collections.ArrayList;
            Status.Text="Проверено: "+data["scanned"]+" · предложено перенести: "+(items==null?0:items.Count)+". Слева — дубль, справа — оставляем.";
            if(items==null) return;
            proposals=items; pair=0; ShowPair();
        }
        void ShowPair()
        {
            results.Children.Clear();
            if(proposals==null || proposals.Count==0) return;
            var navigation=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center };
            var previous=new Button { Content="Назад",Padding=new Thickness(10,5,10,5),IsEnabled=pair>0 };
            var next=new Button { Content="Далее",Padding=new Thickness(10,5,10,5),IsEnabled=pair+1<proposals.Count };
            previous.Click+=delegate { pair--; ShowPair(); }; next.Click+=delegate { pair++; ShowPair(); };
            navigation.Children.Add(previous); navigation.Children.Add(new TextBlock { Text=(pair+1)+" из "+proposals.Count,FontSize=13,Margin=new Thickness(13,0,13,0),VerticalAlignment=VerticalAlignment.Center }); navigation.Children.Add(next);
            results.Children.Add(navigation);
            var item=(Dictionary<string,object>)proposals[pair];
            {
                var row=new Grid { Margin=new Thickness(0,10,0,10) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition()); int column=0;
                foreach(string key in new[]{"source","keep"})
                {
                    var card=new StackPanel { Margin=new Thickness(6) };
                    string imagePath=Path.Combine(Folder.Text,(string)item[key]);
                    if(File.Exists(imagePath))
                    {
                        var image=new BitmapImage(); image.BeginInit(); image.UriSource=new Uri(imagePath); image.CacheOption=BitmapCacheOption.OnLoad; image.DecodePixelWidth=600; image.EndInit(); image.Freeze();
                        card.Children.Add(new Image { Source=image,Height=120,Stretch=Stretch.Uniform });
                    }
                    else card.Children.Add(new TextBlock { Text="Снимок уже перемещён или отсутствует. Обновите просмотр.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(13) });
                    card.Children.Add(new TextBlock { Text=(string)item[key],FontSize=10,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0) }); Grid.SetColumn(card,column++); row.Children.Add(card);
                }
                results.Children.Add(row);
                RefreshMetrics();
            }
        }
    }
}


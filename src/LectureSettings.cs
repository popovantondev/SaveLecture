using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SaveLecture
{
    internal sealed class LectureSettings : IDisposable
    {
        public readonly DesignWindow Window;
        readonly UiPreferences preferences;
        readonly UiPreferences original;
        readonly string archive;
        readonly Action oldScreens;
        readonly Func<bool> canChangeStorage;
        readonly WindowsAutostart windowsAutostart=new WindowsAutostart();
        bool originalAutostart;
        Slider duplicates, stable;
        TextBox aliases, folder;
        public int Delay { get; private set; }
        public bool Applied { get; private set; }
        public void Dispose() { Window.Dispose(); }
        public LectureSettings(int delay,UiPreferences settings,string root,Action cleanup,Func<bool> storageAllowed=null)
        {
            Delay=delay; original=settings;
            preferences=settings.Copy(); canChangeStorage=storageAllowed??delegate { return true; };
            archive=root; oldScreens=cleanup;
            Window=new DesignWindow("settings.xaml",1264,1008); Window.Text="SaveLecture — Настройки";
            Window.Find<TextBox>("Delay").Text=delay.ToString();
            Window.Find<CheckBox>("SaveSlides").IsChecked=settings.SaveSlides;
            Window.Find<CheckBox>("SaveCaptions").IsChecked=settings.SaveCaptions;
            Window.Find<CheckBox>("HideToTray").IsChecked=settings.HideToTray;
            var autostart=Window.Find<CheckBox>("WindowsAutostart");
            try { originalAutostart=windowsAutostart.Enabled; autostart.IsChecked=originalAutostart; }
            catch(Exception error) { autostart.IsEnabled=false; autostart.ToolTip=UiText.Get("Не удалось прочитать настройку автозапуска Windows."); AppDiagnostics.Log(error,"Автозапуск Windows"); }
            Window.Wire("Increase",delegate { ChangeDelay(1); }); Window.Wire("Decrease",delegate { ChangeDelay(-1); });
            Window.Wire("OldScreens",cleanup); Window.Wire("Done",Apply);
            Window.Wire("General",delegate { Page("General"); }); Window.Wire("Slides",delegate { Page("Slides"); });
            Window.Wire("Transcription",delegate { Page("Transcription"); }); Window.Wire("Storage",delegate { Page("Storage"); });
            Window.Wire("Interface",delegate { Page("Interface"); }); Window.Wire("About",delegate { Page("About"); });
            Window.Wire("ConfigureFilter",delegate { Page("Slides"); }); RefreshFilter();
            string[] keys={"General","Slides","Transcription","Storage","Interface","About"};
            string[] labels={"Основные","Снимки и слайды","Транскрипция","Хранилище","Интерфейс","О программе"};
            string[] icons={"gear","image","doc","database","palette","info"};
            for(int i=0;i<keys.Length;i++)
            {
                var content=UiGlyph.Content(labels[i],icons[i],new SolidColorBrush(Color.FromRgb(57,82,120)),26);
                ((FrameworkElement)content.Children[0]).Margin=new Thickness(0,0,16,0);
                Window.Find<Button>(keys[i]).Content=content;
            }
            var cleanupContent=UiGlyph.Content("Очистить папку вручную","archive",new SolidColorBrush(Color.FromRgb(46,69,100)),18);
            ((FrameworkElement)cleanupContent.Children[0]).Margin=new Thickness(0,0,10,0);
            Window.Find<Button>("OldScreens").Content=cleanupContent;
            var configure=Window.Find<Button>("ConfigureFilter");
            var configureContent=UiGlyph.Content("Настроить","sliders",new SolidColorBrush(Color.FromRgb(46,69,100)),18);
            ((FrameworkElement)configureContent.Children[0]).Margin=new Thickness(0,0,8,0);
            configure.Padding=new Thickness(10,6,10,6); configure.Content=configureContent;
        }
        void ChangeDelay(int delta)
        {
            int current; if(!Int32.TryParse(Window.Find<TextBox>("Delay").Text,out current)) current=Delay;
            Window.Find<TextBox>("Delay").Text=Math.Max(0,Math.Min(60,current+delta)).ToString();
        }
        void RefreshFilter()
        {
            Window.Find<TextBlock>("DuplicateValue").Text=(preferences.DuplicateThreshold*100).ToString("0.#",UiText.Culture)+"%";
            Window.Find<TextBlock>("StableValue").Text=(preferences.StableThreshold*100).ToString("0.#",UiText.Culture)+"%";
            Window.Find<ProgressBar>("DuplicateBar").Value=preferences.DuplicateThreshold*100;
            Window.Find<ProgressBar>("StableBar").Value=preferences.StableThreshold*100;
            Window.Find<TextBlock>("IntervalSummary").Text="◷   Проверка "+preferences.CaptureIntervalSeconds.ToString("0.#",UiText.Culture)+" сек";
            Window.Find<TextBlock>("ShiftSummary").Text="⌗   Сдвиг до "+preferences.PixelShiftThreshold+" пикселей";
        }
        Slider Option(StackPanel panel,string name,string title,string explanation,double min,double max,double value,double step,string format,double[] marks,Action<double> changed)
        {
            var heading=new Grid { Margin=new Thickness(0,4,0,2) };
            heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            heading.Children.Add(new TextBlock { Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center });
            var current=new TextBlock { Name=name+"Value",Text=UiText.Get(value.ToString(format,UiText.Culture)),FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(7,112,224)) };
            var badge=new Border { Background=new SolidColorBrush(Color.FromRgb(236,246,255)),CornerRadius=new CornerRadius(7),Padding=new Thickness(8,1,8,1),Child=current };
            Grid.SetColumn(badge,1); heading.Children.Add(badge); panel.Children.Add(heading);
            panel.Children.Add(new TextBlock { Text=explanation,FontSize=16,TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(89,111,144)) });
            var slider=new Slider { Name=name,Minimum=min,Maximum=max,Value=value,TickFrequency=step,SmallChange=step,LargeChange=step*2,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,Height=30,Margin=new Thickness(0,1,0,0) };
            slider.ValueChanged+=delegate { current.Text=UiText.Get(slider.Value.ToString(format,UiText.Culture)); changed(slider.Value); RefreshFilter(); Window.RefreshMetrics(); };
            panel.Children.Add(slider); panel.Children.Add(new SliderMarks(slider,marks,format.Replace("0.0","0.#"))); return slider;
        }
        TextBlock Label(string text) { return new TextBlock { Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,13),FontSize=18 }; }
        void Page(string name)
        {
            if(duplicates!=null) { preferences.DuplicateThreshold=duplicates.Value/100; preferences.StableThreshold=stable.Value/100; }
            if(aliases!=null) cachedAliases=aliases.Text;
            if(folder!=null) preferences.ArchiveRoot=folder.Text.Trim();
            duplicates=null; stable=null; aliases=null; folder=null;
            foreach(string key in new[]{"General","Slides","Transcription","Storage","Interface","About"})
                {
                    Window.Find<Button>(key).Background=new SolidColorBrush(key==name?Color.FromRgb(226,241,255):Colors.Transparent);
                    Window.Find<Button>(key).FontWeight=key==name?FontWeights.Bold:FontWeights.Normal;
                }
            Window.Find<FrameworkElement>("GeneralPage").Visibility=name=="General"?Visibility.Visible:Visibility.Collapsed;
            Window.Find<Border>("OtherPage").Visibility=name=="General"?Visibility.Collapsed:Visibility.Visible;
            var panel=Window.Find<StackPanel>("PageControls"); panel.Children.Clear();
            string title="",description="";
            if(name=="Slides")
            {
                title="Снимки и слайды"; description="Сравнение снимков и частота проверки. Изменения сохраняются кнопкой «Готово».";
                duplicates=Option(panel,"DuplicatesSlider","Пропуск похожих слайдов","Ниже порог — меньше повторов. Выше — больше похожих снимков.",80,100,preferences.DuplicateThreshold*100,.5,"0.#'%'",new double[]{80,85,90,95,100},delegate(double v) { preferences.DuplicateThreshold=v/100; });
                duplicates.ToolTip=UiText.Get("Процент — оценка похожести фильтра, а не доля одинаковых пикселей.");
                stable=Option(panel,"StabilitySlider","Стабильность перед снимком","Выше порог — строже проверка изменений перед сохранением.",80,100,preferences.StableThreshold*100,.5,"0.#'%'",new double[]{80,85,90,95,100},delegate(double v) { preferences.StableThreshold=v/100; });
                var shift=Option(panel,"ShiftSlider","Допустимый сдвиг","Смещение в уменьшенном изображении. 0 — отключить компенсацию.",0,24,preferences.PixelShiftThreshold,1,"0' px'",new double[]{0,6,12,18,24},delegate(double v) { preferences.PixelShiftThreshold=(int)v; });
                shift.ToolTip=UiText.Get("Снимки сравниваются в размере 240 × 135. Здесь указаны пиксели этого образца, а не исходного слайда.");
                var interval=Option(panel,"IntervalSlider","Интервал проверки","Пауза между проверками. Меньше — быстрее, но выше нагрузка.",.5,10,preferences.CaptureIntervalSeconds,.5,"0.#' сек'",new double[]{.5,2,4,6,8,10},delegate(double v) { preferences.CaptureIntervalSeconds=v; });
                interval.ToolTip=UiText.Get("Время обработки снимка добавляется к интервалу. Например, 1 сек — пауза в одну секунду после обработки.");
            }
            else if(name=="Transcription")
            {
                title="Транскрипция"; description="Текст берётся из субтитров Teams. Включите их во встрече.";
                panel.Children.Add(Label("Варианты имён для уведомления — по одному в строке"));
                string path=NameMatcher.SettingsPath;
                aliases=new TextBox { Text=cachedAliases??File.ReadAllText(path),AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,Height=260,FontSize=18,Padding=new Thickness(10) }; panel.Children.Add(aliases);
                panel.Children.Add(Label("Совпадение имени в субтитрах вызывает уведомление. Пустой список отключает уведомления."));
            }
            else if(name=="Storage")
            {
                title="Хранилище"; description="Текст и снимки сохраняются на этом компьютере, в папках по датам.";
                panel.Children.Add(Label("Папка для текстов и снимков. Изменяется после Стоп; прежние файлы остаются в прежней папке."));
                folder=new TextBox { Text=String.IsNullOrWhiteSpace(preferences.ArchiveRoot)?archive:preferences.ArchiveRoot,FontSize=18,Padding=new Thickness(10),IsEnabled=canChangeStorage(),Margin=new Thickness(0,0,0,10) }; panel.Children.Add(folder);
                var choose=new Button { Content="Выбрать папку…",Padding=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=canChangeStorage() };
                choose.Click+=delegate { using(var dialog=new System.Windows.Forms.FolderBrowserDialog { SelectedPath=folder.Text,Description="Папка SaveLecture" }) if(dialog.ShowDialog(Window.Shell??Window)==System.Windows.Forms.DialogResult.OK) folder.Text=dialog.SelectedPath; }; panel.Children.Add(choose);
                panel.Children.Add(Label(canChangeStorage()?"TXT и снимки сохраняются локально по датам; данные восстановления — в _service.":"Сначала остановите запись, чтобы сменить папку."));
                var button=new Button { Content="Открыть записи",Padding=new Thickness(13),HorizontalAlignment=HorizontalAlignment.Left }; button.Click+=delegate { System.Diagnostics.Process.Start("explorer.exe",archive); }; panel.Children.Add(button);
            }
            else if(name=="Interface")
            {
                panel.Children.Add(Label("Язык интерфейса"));
                var language=new LanguagePicker(preferences.Language,delegate(string selected) { preferences.Language=selected; }) { HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8) };
                panel.Children.Add(language);
                title="Интерфейс"; description="Язык и управление программой.";
                panel.Children.Add(Label("Язык изменится после нажатия «Готово»."));
                panel.Children.Add(Label("Ctrl+R — старт или продолжение.\nCtrl+P — пауза.\nCtrl+S — завершение с подтверждением."));
                panel.Children.Add(Label("Поведение крестика настраивается в разделе «Основные»."));
            }
            else if(name=="About")
            {
                title="SaveLecture"; description="Субтитры и слайды Teams в локальном архиве.";
                panel.Children.Add(Label("Сохраняет текст субтитров и снимки демонстрации. Аудио и микрофон не записываются."));
                panel.Children.Add(Label("Сбор начинается после запуска программы. Автозапуск при входе в Windows включается отдельно."));
                panel.Children.Add(Label("Учебная предварительная версия. Используйте только с разрешения всех участников."));
                panel.Children.Add(Label("Версия: "+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version));
            }
            Window.Find<TextBlock>("PageTitle").Text=title; Window.Find<TextBlock>("PageDescription").Text=description; RefreshFilter(); Window.RefreshMetrics();
        }
        string cachedAliases;
        void Apply()
        {
            int delay;
            if(!Int32.TryParse(Window.Find<TextBox>("Delay").Text,out delay)||delay<0||delay>60) { System.Windows.Forms.MessageBox.Show(UiText.Get("Введите ожидание от 0 до 60 секунд.")); return; }
            try
            {
                Delay=delay;
                preferences.SaveSlides=Window.Find<CheckBox>("SaveSlides").IsChecked==true;
                preferences.SaveCaptions=Window.Find<CheckBox>("SaveCaptions").IsChecked==true;
                preferences.HideToTray=Window.Find<CheckBox>("HideToTray").IsChecked==true;
                if(duplicates!=null) { preferences.DuplicateThreshold=duplicates.Value/100; preferences.StableThreshold=stable.Value/100; }
                if(aliases!=null) cachedAliases=aliases.Text;
                if(folder!=null) preferences.ArchiveRoot=folder.Text.Trim();
                if(!String.Equals(original.StoragePath,preferences.StoragePath,StringComparison.OrdinalIgnoreCase))
                {
                    if(!canChangeStorage()) { System.Windows.Forms.MessageBox.Show(UiText.Get("Сначала остановите запись, чтобы сменить папку.")); return; }
                    string selected=preferences.StoragePath;
                    if(!Path.IsPathRooted(preferences.ArchiveRoot)) { System.Windows.Forms.MessageBox.Show(UiText.Get("Укажите полный путь к папке.")); return; }
                    Directory.CreateDirectory(selected);
                    string probe=Path.Combine(selected,".savelecture-probe-"+Guid.NewGuid().ToString("N"));
                    using(var stream=new FileStream(probe,FileMode.CreateNew,FileAccess.Write)) { stream.WriteByte(0); stream.Flush(true); }
                    File.Delete(probe);
                }
                if(cachedAliases!=null) File.WriteAllText(NameMatcher.SettingsPath,cachedAliases);
                bool autostart=Window.Find<CheckBox>("WindowsAutostart").IsChecked==true;
                bool autostartChanged=autostart!=originalAutostart;
                if(autostartChanged) windowsAutostart.SetEnabled(autostart);
                try { preferences.Save(); }
                catch {
                    if(autostartChanged) try { windowsAutostart.SetEnabled(originalAutostart); } catch(Exception error) { AppDiagnostics.Log(error,"Не удалось отменить изменение автозапуска Windows."); }
                    throw;
                }
                original.SaveSlides=preferences.SaveSlides; original.SaveCaptions=preferences.SaveCaptions; original.HideToTray=preferences.HideToTray;
                original.DuplicateThreshold=preferences.DuplicateThreshold; original.StableThreshold=preferences.StableThreshold;
                original.ArchiveRoot=preferences.ArchiveRoot; original.PixelShiftThreshold=preferences.PixelShiftThreshold; original.CaptureIntervalSeconds=preferences.CaptureIntervalSeconds;
                original.Language=preferences.Language;
                Applied=true; Window.Return();
            }
            catch(IOException error) { System.Windows.Forms.MessageBox.Show(UiText.Get(error.Message),UiText.Get("Не удалось сохранить настройки")); }
            catch(UnauthorizedAccessException error) { System.Windows.Forms.MessageBox.Show(error.Message,UiText.Get("Нет доступа к папке")); }
            catch(ArgumentException) { System.Windows.Forms.MessageBox.Show(UiText.Get("Проверьте путь к папке.")); }
        }
    }
}

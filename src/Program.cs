using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using TeamsSpeechRecognition.S03.Recognition;

namespace SaveLecture
{
    internal sealed class PendingCaption
    {
        public string Text, Speaker, Written;
        public DateTime Observed;
    }
    internal sealed class ReceivedCaption
    {
        public TeamsCaptionUpdate Update;
        public DateTime Time;
    }

    internal sealed class LectureApp : ApplicationContext
    {
        private string root;
        private readonly NotifyIcon tray;
        private readonly TrayStates trayStates = new TrayStates();
        private volatile bool captionAvailable, sharedAvailable;
        private long lastLiveCaption;
        private bool CaptionsActive { get { long ticks=Interlocked.Read(ref lastLiveCaption); return preferences.SaveCaptions && captionAvailable && ticks>0 && DateTime.UtcNow.Ticks-ticks<TimeSpan.FromSeconds(15).Ticks; } }
        private int displayedState = -1;
        private readonly System.Windows.Forms.Timer waitingTimer=new System.Windows.Forms.Timer { Interval=100 };
        private int waitingFrame;
        private readonly DesignWindow statusWindow;
        private readonly LectureDashboard dashboard;
        private readonly UiPreferences preferences = UiPreferences.Load();
        private DesignWindow trayPopup;
        private readonly Stopwatch sessionClock = new Stopwatch();
        private volatile bool recordingPaused, skipResumeBaseline;
        private readonly ConcurrentQueue<ReceivedCaption> viewInbox = new ConcurrentQueue<ReceivedCaption>();
        private ArchiveStore store;
        private SlideArchive slides;
        private readonly ConcurrentQueue<ReceivedCaption> inbox = new ConcurrentQueue<ReceivedCaption>();
        private readonly Dictionary<string, PendingCaption> pending = new Dictionary<string, PendingCaption>();
        private NameAlertDecider alertDecider = new NameAlertDecider();
        private readonly MentionPopup mentionPopup = new MentionPopup();
        private NameMatcher nameMatcher = NameMatcher.Load(NameMatcher.SettingsPath);
        private readonly object alertGate = new object();
        private readonly object readerGate = new object();
        private Thread captionThread, screenshotThread;
        private bool exiting;
        private ToolStripMenuItem startItem, stopItem;
        private TeamsCaptionClient reader;
        private volatile bool stopping, readerExited;
        private volatile bool captionsFlushed;
        private string captionStatus = "Ожидание субтитров Teams", imageStatus = "Ожидание демонстрации";
        private int fragments, screenshots;
        private readonly object statusGate = new object();
        private readonly System.Windows.Forms.Timer uiTimer;
        private int stabilitySeconds = 3;
        private CleanupWindow cleanupWindow;
        private bool previewAfterStop;
        private readonly string cleanupSettingsPath = Path.Combine(UiPreferences.DataDirectory, "cleanup-preview.txt");
        private readonly string settingsPath = Path.Combine(UiPreferences.DataDirectory, "stability-seconds.txt");

        public LectureApp(bool paused,bool startInTray=false)
        {
            root=preferences.StoragePath;
            Directory.CreateDirectory(root);
            store = new ArchiveStore(root, !paused);
            slides = new SlideArchive(root, store.SessionStem);
            int loaded;
            if (File.Exists(settingsPath) && Int32.TryParse(File.ReadAllText(settingsPath), out loaded)) stabilitySeconds = Math.Max(0, Math.Min(60, loaded));
            slides.StabilitySeconds = stabilitySeconds;
            LiveSlideFilter.DuplicateThreshold = Math.Max(.8, Math.Min(1,preferences.DuplicateThreshold));
            LiveSlideFilter.StableThreshold = Math.Max(.8, Math.Min(1,preferences.StableThreshold));
            LiveSlideFilter.MaxShift=preferences.PixelShiftThreshold;
            dashboard = new LectureDashboard(TogglePause,RequestStop,delegate { Process.Start("explorer.exe",root); },ShowSettings);
            dashboard.ResolveImagePath=delegate(string path) { return slides.ResolveImagePath(path); };
            statusWindow = dashboard.Window;
            statusWindow.FormClosing += delegate(object sender, FormClosingEventArgs args)
            {
                if(exiting) return;
                args.Cancel = true; statusWindow.Hide();
                if(!preferences.HideToTray) statusWindow.BeginInvoke((Action)delegate { ExitThread(); });
            };
            dashboard.Window.View.PreviewKeyDown += delegate(object sender,System.Windows.Input.KeyEventArgs e)
            {
                if((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control)==0) return;
                if(e.Key==System.Windows.Input.Key.R) { if(stopping) StartRecording(); else if(recordingPaused) TogglePause(); e.Handled=true; }
                if(e.Key==System.Windows.Input.Key.P) { TogglePause(); e.Handled=true; }
                if(e.Key==System.Windows.Input.Key.S) { RequestStop(); e.Handled=true; }
            };
            previewAfterStop = false;
            // Ensure background callbacks can safely marshal notifications before the window is shown.
            IntPtr unused = statusWindow.Handle;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Состояние", null, delegate { statusWindow.ShowAndActivate(); });
            menu.Items.Add("Открыть записи", null, delegate { Process.Start("explorer.exe", root); });
            menu.Items.Add("Очистить папку вручную", null, delegate { ShowCleanup(); });
            startItem = (ToolStripMenuItem)menu.Items.Add("Старт", null, delegate { StartRecording(); });
            stopItem = (ToolStripMenuItem)menu.Items.Add("Стоп", null, delegate { RequestStop(); });
            menu.Items.Add("Выход", null, delegate { ExitThread(); });
            Icon appIcon = statusWindow.Icon;
            statusWindow.Icon = appIcon;
            tray = new NotifyIcon { Icon = appIcon, Text = "SaveLecture — запись лекций", Visible = true };
            waitingTimer.Tick+=delegate { if(displayedState==5) { waitingFrame=(waitingFrame+1)%12; tray.Icon=trayStates[5+waitingFrame]; } };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { ShowTrayPopup(); };
            tray.DoubleClick += delegate { if(trayPopup!=null) trayPopup.Hide(); statusWindow.ShowAndActivate(); };
            if (!paused) LaunchWorkers();
            else
            {
                stopping = true;
                captionThread = new Thread(CaptionLoop); screenshotThread = new Thread(ScreenshotLoop);
                startItem.Enabled = true; stopItem.Enabled = false;
                SetCaptionStatus("Запись остановлена — нажмите Старт для новой лекции"); SetImageStatus("Запись остановлена");
                UpdateTrayState();
            }
            uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uiTimer.Tick += delegate
            {
                if (Program.ShowRequested.WaitOne(0)) { Program.ShowRequested.Reset(); statusWindow.ShowAndActivate(); }
                if (Program.TrayRequested.WaitOne(0)) { Program.TrayRequested.Reset(); ShowTrayPopup(); }
                if (Program.SettingsRequested.WaitOne(0)) { Program.SettingsRequested.Reset(); ShowSettings(); }
                ReceivedCaption item;
                int count=0;
                while(count++<300 && viewInbox.TryDequeue(out item)) dashboard.Caption(item.Update.SourceKey,item.Update.Text,item.Time,item.Update.SpeakerName??"");
                bool error;
                lock(statusGate) error=captionStatus.StartsWith("ОШИБКА") || imageStatus.StartsWith("ОШИБКА");
                bool textActive=CaptionsActive, slideActive=preferences.SaveSlides && sharedAvailable;
                dashboard.Update(stopping,recordingPaused,textActive || slideActive,error,sessionClock.Elapsed,fragments,screenshots,slides.Duplicates,!inbox.IsEmpty,textActive,slideActive);
                try { string currentSlide=slides.Representative(slides.LastImagePath); dashboard.Slide(slides.ResolveImagePath(currentSlide),slides.EarlierVersions(currentSlide)); } catch(Exception imageError) { Log(imageError); }
            };
            uiTimer.Tick += delegate
            {
                if (Program.ExitRequested.WaitOne(0)) { ExitThread(); return; }
                if (Program.StopRequested.WaitOne(0)) { Program.StopRequested.Reset(); StopRecording(); }
                if (Program.StartRequested.WaitOne(0)) { Program.StartRequested.Reset(); StartRecording(); }
                UpdateTrayState();
                try
                {
                    string directory = UiPreferences.DataDirectory;
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "status.json");
                    string temporary = path + ".tmp";
                    string value;
                    lock (statusGate) value = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { updated = DateTime.Now.ToString("o"), recording = !stopping && !recordingPaused, paused = recordingPaused, transcript = store.TranscriptPath, caption = captionStatus, screenshot = imageStatus, fragments = fragments, screenshots = screenshots, queued = inbox.Count });
                    File.WriteAllText(temporary, value);
                    if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                }
                catch (IOException) { }
            };
            uiTimer.Start();
            dashboard.Update(stopping,recordingPaused,false,false,sessionClock.Elapsed,fragments,screenshots,0);
            if(!startInTray) statusWindow.ShowAndActivate();
        }

        private void LaunchWorkers()
        {
            captionThread = new Thread(CaptionLoop) { IsBackground = true, Name = "Durable captions" };
            screenshotThread = new Thread(ScreenshotLoop) { IsBackground = true, Name = "Teams shared content" };
            startItem.Enabled = false; stopItem.Enabled = true;
            sessionClock.Start();
            captionThread.Start(); screenshotThread.Start();
            UpdateTrayState();
        }

        private void ShowSettings()
        {
            var settings = new LectureSettings(stabilitySeconds,preferences,root,ShowCleanup,delegate { return stopping && !captionThread.IsAlive && !screenshotThread.IsAlive && (cleanupWindow==null || !cleanupWindow.Busy); });
            ((DesignWindow)statusWindow).ShowPage(settings.Window,delegate
            {
                if(settings.Applied)
                {
                    stabilitySeconds=settings.Delay; slides.StabilitySeconds=stabilitySeconds;
                    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)); File.WriteAllText(settingsPath,stabilitySeconds.ToString());
                    LiveSlideFilter.DuplicateThreshold=preferences.DuplicateThreshold; LiveSlideFilter.StableThreshold=preferences.StableThreshold;
                    LiveSlideFilter.MaxShift=preferences.PixelShiftThreshold;
                    if(stopping && !String.Equals(root,preferences.StoragePath,StringComparison.OrdinalIgnoreCase))
                    {
                        store.Dispose(); root=preferences.StoragePath;
                        store=new ArchiveStore(root,false); slides=new SlideArchive(root,store.SessionStem) { StabilitySeconds=stabilitySeconds };
                        if(cleanupWindow!=null) { cleanupWindow.Dispose(); cleanupWindow=null; }
                    }
                    nameMatcher=NameMatcher.Load(NameMatcher.SettingsPath);
                    UiText.Language=preferences.Language; ((DesignWindow)statusWindow).RefreshMetrics(); displayedState=-1;
                }
                settings.Window.Dispose();
            });
            statusWindow.ShowAndActivate();
        }

        private void ShowTrayPopup()
        {
            if(trayPopup==null || trayPopup.IsDisposed)
            {
                trayPopup=new DesignWindow("tray.xaml",400,450) { ShowInTaskbar=false, TopMost=true,StartPosition=FormStartPosition.Manual };
                trayPopup.Deactivate+=delegate { trayPopup.Hide(); };
                trayPopup.Wire("Open",delegate { trayPopup.Hide(); statusWindow.ShowAndActivate(); });
                trayPopup.Wire("Start",delegate { trayPopup.Hide(); TogglePause(); });
                trayPopup.Wire("Stop",delegate { trayPopup.Hide(); RequestStop(); });
                trayPopup.Wire("Archive",delegate { trayPopup.Hide(); Process.Start("explorer.exe",root); });
                trayPopup.Wire("Settings",delegate { trayPopup.Hide(); ShowSettings(); });
                trayPopup.Wire("Exit",delegate { trayPopup.Hide(); ExitThread(); });
            }
            TrayMenuState.Update(trayPopup,stopping,recordingPaused,CaptionsActive || (preferences.SaveSlides && sharedAvailable),displayedState==3,displayedState==4,CaptionsActive,preferences.SaveSlides && sharedAvailable);
            var anchor=Cursor.Position;
            var area=Screen.FromPoint(anchor).WorkingArea;
            trayPopup.Location=TrayPopupPlacement.Near(anchor,area,trayPopup.Size);
            trayPopup.ShowAndActivate();
        }
        private void TogglePause()
        {
            if(stopping) { StartRecording(); return; }
            recordingPaused=!recordingPaused;
            if(recordingPaused)
            {
                sessionClock.Stop();
                lock(readerGate) { if(reader!=null) { reader.Dispose(); reader=null; } }
            }
            else { skipResumeBaseline=true; sessionClock.Start(); }
            UpdateTrayState();
        }

        private void ShowCleanup()
        {
            if (cleanupWindow == null || cleanupWindow.IsDisposed)
                cleanupWindow = new CleanupWindow(root, delegate { return !stopping || screenshotThread.IsAlive; });
            ((DesignWindow)statusWindow).ShowPage(cleanupWindow.Page,delegate { });
            statusWindow.ShowAndActivate();
        }

        private void UpdateTrayState()
        {
            int state;
            lock (statusGate)
                state = stopping ? 2 : recordingPaused ? 1 : captionStatus.StartsWith("ОШИБКА") || imageStatus.StartsWith("ОШИБКА") ? 3 : !inbox.IsEmpty ? 4 : CaptionsActive || (preferences.SaveSlides && sharedAvailable) ? 0 : 5;
            if(trayPopup!=null && !trayPopup.IsDisposed && trayPopup.Visible) TrayMenuState.Update(trayPopup,stopping,recordingPaused,CaptionsActive || (preferences.SaveSlides && sharedAvailable),state==3,state==4,CaptionsActive,preferences.SaveSlides && sharedAvailable);
            string trayLabel=state==5?"Ожидание демонстрации экрана":state==1?"Пауза":state==0?(preferences.SaveSlides && sharedAvailable?(CaptionsActive?"Слайды и субтитры":"Слайды"):"Субтитры"):state==2?"Остановлено":state==3?"Ошибка":"Сохранение";
            tray.Text="SaveLecture — "+UiText.Get(trayLabel);
            if (displayedState == state) return;
            displayedState = state; tray.Icon = trayStates[state==4?0:state];
            if(state==5) { waitingFrame=0; waitingTimer.Start(); } else waitingTimer.Stop();
        }

        private void StartRecording()
        {
            if (cleanupWindow != null && !cleanupWindow.IsDisposed && cleanupWindow.Busy) { SetCaptionStatus("Подождите завершения очистки"); return; }
            if (!stopping) { if(recordingPaused) TogglePause(); return; }
            if (captionThread.IsAlive || screenshotThread.IsAlive) { SetCaptionStatus("Предыдущая запись ещё завершается"); return; }
            store = new ArchiveStore(root, true);
            slides = new SlideArchive(root, store.SessionStem);
            slides.StabilitySeconds = stabilitySeconds;
            dashboard.Reset(); sessionClock.Reset(); recordingPaused=false; skipResumeBaseline=false;
            reader = null; pending.Clear(); alertDecider = new NameAlertDecider();
            captionsFlushed = false; stopping = false; fragments = 0; screenshots = 0;
            captionAvailable = false; sharedAvailable = false;
            SetCaptionStatus("Ожидание субтитров Teams"); SetImageStatus("Ожидание демонстрации");
            LaunchWorkers();
        }

        private void StopRecording()
        {
            if (stopping) return;
            stopping = true; recordingPaused=false; sessionClock.Stop();
            lock (readerGate) { if (reader != null) reader.Dispose(); }
            captionThread.Join(5000); screenshotThread.Join(5000);
            if (!captionThread.IsAlive)
            {
                try { if (captionsFlushed) store.MarkClean(); } catch (Exception error) { Log(error); }
                store.Dispose();
            }
            mentionPopup.Hide(); startItem.Enabled = true; stopItem.Enabled = false;
            SetCaptionStatus("Запись остановлена — нажмите Старт для новой лекции"); SetImageStatus("Запись остановлена");
            UpdateTrayState();
            if (previewAfterStop && !exiting && !screenshotThread.IsAlive)
            {
                string path = Path.Combine(root, DateTime.Now.ToString("dd.MM.yyyy"), "scr");
                if (Directory.Exists(path)) { ShowCleanup(); cleanupWindow.PreviewFolder(path); }
            }
        }

        private void RequestStop()
        {
            if(stopping) return;
            using(var confirmation=new StopConfirmation()) {
                if(statusWindow.Visible) confirmation.ShowDialog(statusWindow); else confirmation.ShowDialog();
                if(confirmation.Confirmed) StopRecording();
            }
        }

        private void StartReader()
        {
            if (reader != null) reader.Dispose();
            readerExited = false;
            long origin = (long)(Stopwatch.GetTimestamp() * (10000000.0 / Stopwatch.Frequency));
            reader = new TeamsCaptionClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "teams_caption_reader.ps1"), origin);
            reader.CaptionUpdated += delegate(TeamsCaptionUpdate update)
            {
                if(recordingPaused || !preferences.SaveCaptions || (skipResumeBaseline && update.InitialHistory)) return;
                if (!update.ScopeVerified || String.IsNullOrWhiteSpace(update.Text)) return;
                DateTime now = DateTime.Now;
                captionAvailable = true;
                if(!update.InitialHistory) Interlocked.Exchange(ref lastLiveCaption,DateTime.UtcNow.Ticks);
                inbox.Enqueue(new ReceivedCaption { Update = update, Time = now });
                if (!update.InitialHistory) Alert(update, now);
            };
            reader.StatusChanged += delegate(string value)
            {
                Match panel = Regex.Match(value, @"Caption panels visible: (\d+)");
                if (panel.Success) captionAvailable = Int32.Parse(panel.Groups[1].Value) > 0;
                if (value.IndexOf("not running", StringComparison.OrdinalIgnoreCase) >= 0) captionAvailable = false;
                if (value.IndexOf("exited", StringComparison.OrdinalIgnoreCase) >= 0 || value.IndexOf("ended", StringComparison.OrdinalIgnoreCase) >= 0) readerExited = true;
                SetCaptionStatus(value.IndexOf("Caption panels visible: 0", StringComparison.Ordinal) >= 0 ? "Включите субтитры в Teams; данных пока нет" : value);
            };
        }

        private void Alert(TeamsCaptionUpdate update, DateTime now)
        {
            int matches = nameMatcher.Count(update.Text);
            lock (alertGate)
            {
                if (!alertDecider.Accept(update.SourceKey, matches, now)) return;
            }
            statusWindow.BeginInvoke((Action)delegate
            {
                System.Media.SystemSounds.Exclamation.Play();
                mentionPopup.Notify(update.Text);
            });
        }

        private void CaptionLoop()
        {
            DateTime retry = DateTime.MinValue;
            ReceivedCaption current = null;
            while (!stopping || current != null || !inbox.IsEmpty)
            {
                try
                {
                    if (!stopping && !recordingPaused && (reader == null || readerExited) && DateTime.Now >= retry)
                    { retry = DateTime.Now.AddSeconds(5); lock (readerGate) { if (!stopping) StartReader(); } }
                    if (current == null) inbox.TryDequeue(out current);
                    if (current != null)
                    {
                        var update = current.Update;
                        store.AppendRaw(update.SourceKey, update.Text, update.SpeakerName, current.Time);
                        PendingCaption item;
                        if (!pending.TryGetValue(update.SourceKey, out item))
                        { item = new PendingCaption(); pending[update.SourceKey] = item; }
                        // Reused caption controls can start another utterance before the previous one settles.
                        if (item.Text != null && !update.Text.StartsWith(item.Text, StringComparison.Ordinal) && !item.Text.StartsWith(update.Text, StringComparison.Ordinal) && item.Written != item.Text)
                            store.AppendText(item.Text, item.Speaker, item.Observed);
                        item.Text = update.Text; item.Speaker = update.SpeakerName; item.Observed = current.Time;
                        current = null;
                        fragments++;
                        viewInbox.Enqueue(new ReceivedCaption { Update=update,Time=item.Observed });
                        SetCaptionStatus("Сохраняется · последнее получение " + DateTime.Now.ToString("HH:mm:ss"));
                    }
                    FlushPending(stopping);
                }
                catch (Exception error) { SetCaptionStatus("ОШИБКА сохранения/сборщика: " + error.Message); Thread.Sleep(1000); }
                if (!stopping) Thread.Sleep(inbox.IsEmpty ? 100 : 1);
                else if (current != null) break; // Failed disk stays visible; do not hang shutdown indefinitely.
            }
            try { FlushPending(true); captionsFlushed = current == null && inbox.IsEmpty; } catch (Exception error) { Log(error); }
        }

        private void FlushPending(bool all)
        {
            DateTime now = DateTime.Now;
            foreach (string key in new List<string>(pending.Keys))
            {
                var item = pending[key];
                if (item.Written != item.Text && (all || (now - item.Observed).TotalSeconds >= 3))
                { store.AppendText(item.Text, item.Speaker, item.Observed); item.Written = item.Text; }
                if ((now - item.Observed).TotalMinutes > 2) pending.Remove(key);
            }
        }

        private void ScreenshotLoop()
        {
            bool pauseHandled=false;
            while (!stopping)
            {
                if(recordingPaused || !preferences.SaveSlides)
                {
                    if(!pauseHandled) { try { slides.Missing(); } catch(Exception error) { Log(error); } pauseHandled=true; }
                    Thread.Sleep(100); continue;
                }
                pauseHandled=false;
                try
                {
                    using (Bitmap image = WindowCapture.SharedContent())
                    {
                        if (image == null) { slides.Missing(); SetImageStatus("Ожидание демонстрации · Teams не сворачивать"); }
                        else
                        {
                            string path = slides.Observe(image, DateTime.Now);
                            if (path != null) { screenshots++; SetImageStatus("Сохранено " + Path.GetFileName(path)); }
                            else SetImageStatus("Демонстрация найдена · ожидание " + slides.StabilitySeconds + " сек / проверка повторов");
                        }
                    }
                }
                catch (Exception error)
                {
                    try { slides.Missing(); } catch (Exception secondary) { Log(secondary); }
                    SetImageStatus("ОШИБКА скриншотов: " + error.Message);
                }
                int wait=(int)Math.Round(preferences.CaptureIntervalSeconds*1000);
                for (int elapsed=0; elapsed<wait && !stopping && !recordingPaused; elapsed+=100) Thread.Sleep(Math.Min(100,wait-elapsed));
            }
            try { slides.End(DateTime.Now, "recorder_stopped"); slides.Missing(); } catch (Exception error) { Log(error); }
        }
        private void SetCaptionStatus(string value) { lock (statusGate) captionStatus = value; }
        private void SetImageStatus(string value) { lock (statusGate) imageStatus = value; sharedAvailable = value.StartsWith("Демонстрация найдена") || value.StartsWith("Сохранено"); }
        private static void Log(Exception error)
        {
            AppDiagnostics.Log(error,"Запись лекции");
        }
        protected override void ExitThreadCore()
        {
            exiting = true;
            uiTimer.Stop();
            waitingTimer.Stop(); waitingTimer.Dispose();
            StopRecording();
            if(trayPopup!=null) trayPopup.Dispose();
            if(cleanupWindow!=null && !cleanupWindow.Busy) cleanupWindow.Dispose();
            tray.Visible = false; tray.Dispose(); mentionPopup.Dispose(); statusWindow.Dispose(); uiTimer.Dispose();
            trayStates.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        public static EventWaitHandle StopRequested;
        public static EventWaitHandle StartRequested, ExitRequested;
        public static EventWaitHandle ShowRequested;
        public static EventWaitHandle TrayRequested, SettingsRequested;
        [STAThread]
        public static void Main(string[] args)
        {
            SetCurrentProcessExplicitAppUserModelID("Popov.SaveLecture");
            bool startInTray=Array.IndexOf(args,WindowsAutostart.BackgroundArgument)>=0;
            if (args.Length == 3 && args[0] == "--recover") { ArchiveStore.Recover(args[1], args[2]); return; }
            string stopName = "Local\\SaveLecture-Stop-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            if (args.Length == 1 && (args[0] == "--stop" || args[0] == "--start" || args[0] == "--exit" || args[0] == "--show" || args[0] == "--tray" || args[0] == "--settings"))
            {
                string command = args[0] == "--start" ? stopName.Replace("-Stop-", "-Start-") : args[0] == "--exit" ? stopName.Replace("-Stop-", "-Exit-") : stopName;
                if(args[0] == "--show") command = stopName.Replace("-Stop-", "-Show-");
                if(args[0] == "--tray") command = stopName.Replace("-Stop-", "-Tray-");
                if(args[0] == "--settings") command = stopName.Replace("-Stop-", "-Settings-");
                try { using (var stop = EventWaitHandle.OpenExisting(command)) stop.Set(); }
                catch (WaitHandleCannotBeOpenedException) { }
                return;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\SaveLecture-MVP-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value, out created))
            {
                if (!created)
                {
                    if(!startInTray) {
                        try { using (var start = EventWaitHandle.OpenExisting(stopName.Replace("-Stop-", "-Start-"))) start.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                    }
                    return;
                }
                StopRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName);
                StopRequested.Reset();
                StartRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName.Replace("-Stop-", "-Start-")); StartRequested.Reset();
                ExitRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName.Replace("-Stop-", "-Exit-")); ExitRequested.Reset();
                ShowRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName.Replace("-Stop-", "-Show-")); ShowRequested.Reset();
                TrayRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName.Replace("-Stop-", "-Tray-")); TrayRequested.Reset();
                SettingsRequested = new EventWaitHandle(false, EventResetMode.ManualReset, stopName.Replace("-Stop-", "-Settings-")); SettingsRequested.Reset();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppDiagnostics.Install();
                try {
                    var preferences=UiPreferences.Load(); UiText.Language=preferences.Language;
                    bool firstLaunch=preferences.ConsentNoticeVersion<1;
                    if(firstLaunch) using(var notice=new FirstRunNotice(preferences)) if(notice.ShowDialog()!=DialogResult.OK) return;
                    try { new WindowsAutostart().EnsureStartInTray(); } catch(Exception error) { AppDiagnostics.Log(error,"Обновление автозапуска Windows"); }
                    Application.Run(new LectureApp(Array.IndexOf(args, "--paused") >= 0,!firstLaunch));
                }
                catch (Exception error) { AppDiagnostics.Report(error,"Ошибка запуска"); }
            }
        }
    }
}

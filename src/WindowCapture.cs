using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;

namespace SaveLecture
{
    public static class WindowCapture
    {
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);

        public static Bitmap SharedContent()
        {
            IntPtr oldDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try
            {
                var ids = new HashSet<int>();
                foreach (var process in Process.GetProcessesByName("ms-teams")) { using (process) ids.Add(process.Id); }
                foreach (var process in Process.GetProcessesByName("Teams")) { using (process) ids.Add(process.Id); }
                if (ids.Count == 0) return null;
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    if (!ids.Contains(window.Current.ProcessId)) continue;
                    IntPtr handle = new IntPtr(window.Current.NativeWindowHandle);
                    if (handle == IntPtr.Zero || IsIconic(handle)) continue;
                    var names = new OrCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "Ansicht \"Freigegebener Inhalt\""),
                        new PropertyCondition(AutomationElement.NameProperty, "Shared content"),
                        new PropertyCondition(AutomationElement.NameProperty, "Shared content view"));
                    AutomationElement shared = window.FindFirst(TreeScope.Descendants, new AndCondition(names, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group)));
                    if (shared == null) continue;
                    AutomationElement target = null;
                    var candidates = new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "slideshow-app-container"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
                    foreach (AutomationElement element in shared.FindAll(TreeScope.Descendants, candidates))
                    {
                        if (element.Current.AutomationId == "slideshow-app-container") { target = element; break; }
                        string name = element.Current.Name ?? "";
                        if (element.Current.ControlType == ControlType.MenuItem &&
                            (name.IndexOf("freigegebener Inhalt", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("shared content", StringComparison.OrdinalIgnoreCase) >= 0)) target = element;
                    }
                    // Unverified containers may include the local camera; never capture them as slides.
                    if (target == null) continue;
                    var wr = window.Current.BoundingRectangle;
                    var cr = target.Current.BoundingRectangle;
                    if (wr.Width < 100 || wr.Height < 100 || cr.Width < 100 || cr.Height < 100 || wr.X < -10000) continue;
                    var crop = new Rectangle((int)Math.Round(cr.X - wr.X), (int)Math.Round(cr.Y - wr.Y), (int)Math.Round(cr.Width), (int)Math.Round(cr.Height));
                    using (var image = new Bitmap((int)Math.Round(wr.Width), (int)Math.Round(wr.Height)))
                    using (var graphics = Graphics.FromImage(image))
                    {
                        if (crop.X < 0 || crop.Y < 0 || crop.Right > image.Width || crop.Bottom > image.Height) continue;
                        IntPtr dc = graphics.GetHdc();
                        bool ok;
                        try { ok = PrintWindow(handle, dc, 2); } finally { graphics.ReleaseHdc(dc); }
                        if (!ok) continue;
                        return image.Clone(crop, PixelFormat.Format24bppRgb);
                    }
                }
                return null;
            }
            finally { SetThreadDpiAwarenessContext(oldDpi); }
        }
    }

    public sealed class SlideArchive
    {
        private readonly string root;
        private string day;
        private readonly List<byte[]> saved = new List<byte[]>();
        private readonly List<string> savedPaths = new List<string>();
        private readonly string session;
        private readonly DateTime sessionStarted = DateTime.Now;
        private readonly SlideSelection selection = new SlideSelection();
        private string activePath, activeId;
        private DateTime activeSince;
        private byte[] candidate;
        private Bitmap cleanCandidate;
        public volatile int StabilitySeconds = 3;
        public int Duplicates;
        public string LastImagePath { get; private set; }
        public string[] EarlierVersions(string path) { return selection.EarlierVersions(path); }
        public string Representative(string path) { return selection.Representative(path); }
        public string ResolveImagePath(string path)
        {
            if(String.IsNullOrEmpty(path)) return path;
            string representative=selection.Representative(path);
            string dayDirectory=Path.GetDirectoryName(Path.GetDirectoryName(representative));
            return new SlideRelocations(dayDirectory).Resolve(representative);
        }
        private DateTime stableSince;
        private bool candidateSaved;
        public SlideArchive(string rootDirectory) : this(rootDirectory, "Probe-" + Guid.NewGuid().ToString("N")) { }
        public SlideArchive(string rootDirectory, string sessionStem) { root = rootDirectory; session = sessionStem; }

        public static byte[] Feature(Bitmap image)
        {
            using (var small = new Bitmap(240, 135))
            {
                using (var graphics = Graphics.FromImage(small))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.DrawImage(image, 0, 0, small.Width, small.Height);
                }
                var bytes = new byte[small.Width * small.Height];
                for (int y = 0; y < small.Height; y++)
                    for (int x = 0; x < small.Width; x++)
                    {
                        Color c = small.GetPixel(x, y);
                        bytes[y * small.Width + x] = (byte)((c.R * 77 + c.G * 150 + c.B * 29) >> 8);
                    }
                return bytes;
            }
        }

        public static bool Similar(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length) return false;
            long sum = 0; int changed = 0;
            for (int i = 0; i < first.Length; i++)
            {
                int delta = Math.Abs(first[i] - second[i]);
                sum += delta;
                if (delta > 20) changed++;
            }
            return sum < first.Length * 0.8 && changed < first.Length * 0.00025;
        }

        public void Missing() { End(DateTime.Now, "sharing_unavailable"); candidate = null; candidateSaved = false; if (cleanCandidate != null) { cleanCandidate.Dispose(); cleanCandidate = null; } }

        public void End(DateTime now, string reason)
        {
            if (activePath == null) return;
            Timeline("slide_end", now, reason);
            activePath = null;
        }

        private void StartTimeline(string path, DateTime since, DateTime recorded)
        {
            LastImagePath = path;
            activePath = path;
            activeId = Guid.NewGuid().ToString("N");
            activeSince = since;
            Timeline("slide_start", recorded, "stable_" + StabilitySeconds + "_seconds");
        }

        private void Timeline(string type, DateTime now, string reason)
        {
            string directory = Path.GetDirectoryName(Path.GetDirectoryName(activePath));
            directory = Path.Combine(directory, "_service");
            Directory.CreateDirectory(directory);
            string json = new JavaScriptSerializer().Serialize(new {
                type = type, id = activeId, session = session, image = "scr/" + Path.GetFileName(activePath),
                from = activeSince.ToString("o"), until = type == "slide_end" ? now.ToString("o") : null,
                observed = now.ToString("o"), from_session_seconds = (activeSince - sessionStarted).TotalSeconds, reason = reason
            });
            using (var stream = new FileStream(Path.Combine(directory, "slides.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            string readable = "[" + activeSince.ToString("HH:mm:ss") + (type == "slide_end" ? " – " + now.ToString("HH:mm:ss") : " – показывается") + "] " + Path.GetFileName(activePath) + " · " + session + Environment.NewLine;
            File.AppendAllText(Path.Combine(directory, "slides_timeline.txt"), readable, new UTF8Encoding(false));
        }

        public string Observe(Bitmap image, DateTime now)
        {
            string nextDay = now.ToString("dd.MM.yyyy");
            string directory = Path.Combine(root, nextDay, "scr");
            if (day != nextDay)
            {
                Directory.CreateDirectory(directory);
                End(now, "date_rollover");
                saved.Clear();
                savedPaths.Clear();
                selection.Reset();
                var locations=new SlideRelocations(Path.GetDirectoryName(directory));
                string[] files = locations.OriginalPaths();
                foreach (string file in files)
                    using (var old = new Bitmap(locations.Resolve(file)))
                    {
                        byte[] oldFeature = Feature(old);
                        saved.Add(oldFeature); savedPaths.Add(file); selection.Add(file, oldFeature);
                    }
                selection.Save(Path.GetDirectoryName(directory),delegate { System.Threading.Interlocked.Increment(ref Duplicates); });
                day = nextDay;
                Missing();
            }
            byte[] feature = Feature(image);
            // Uniform blank/black capture is not lecture material.
            byte min = 255, max = 0;
            foreach (byte pixel in feature) { min = Math.Min(min, pixel); max = Math.Max(max, pixel); }
            if (max - min < 15) { Missing(); return null; }
            byte centralMax = 0;
            for (int y = 27; y < 108; y++)
                for (int x = 48; x < 192; x++) centralMax = Math.Max(centralMax, feature[y * 240 + x]);
            if (centralMax < 20) { Missing(); return null; }
            bool revealed = candidate != null && FrameChanges.Occludes(feature,candidate);
            bool transient = !revealed && candidate != null && FrameChanges.Transient(candidate, feature);
            bool stable = !revealed && (Similar(candidate, feature) || (candidate != null && !SlideSelection.IsMoreComplete(candidate, feature) && !SlideSelection.IsMoreComplete(feature, candidate) && LiveSlideFilter.Score(candidate, feature) >= LiveSlideFilter.StableThreshold));
            if (!stable && !transient)
            {
                End(now, "content_changed");
                candidate = feature;
                stableSince = now;
                candidateSaved = false;
                if (cleanCandidate != null) cleanCandidate.Dispose();
                cleanCandidate = (Bitmap)image.Clone();
                if (StabilitySeconds > 0) return null;
            }
            if (!transient)
            {
                candidate = feature;
                if (cleanCandidate != null) cleanCandidate.Dispose();
                cleanCandidate = (Bitmap)image.Clone();
            }
            if (transient) feature = candidate;
            if (candidateSaved || (now - stableSince).TotalSeconds < StabilitySeconds) return null;
            for (int index = saved.Count - 1; index >= 0; index--)
                if (LiveSlideFilter.Duplicate(saved[index], feature)) {
                    selection.Add(savedPaths[index],saved[index]);
                    selection.Save(Path.GetDirectoryName(directory),delegate { System.Threading.Interlocked.Increment(ref Duplicates); });
                    System.Threading.Interlocked.Increment(ref Duplicates);
                    StartTimeline(selection.Representative(savedPaths[index]), stableSince, now); candidateSaved = true; return null;
                }
            string stem = now.ToString("HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string target = Path.Combine(directory, stem + ".jpg");
            string temporary = target + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ImageCodecInfo codec = null;
                    foreach (var encoder in ImageCodecInfo.GetImageEncoders()) if (encoder.MimeType == "image/jpeg") codec = encoder;
                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 97L);
                        (cleanCandidate ?? image).Save(stream, codec, parameters);
                    }
                    stream.Flush(true);
                }
                File.Move(temporary, target);
                saved.Add(feature);
                savedPaths.Add(target);
                selection.Add(target, feature);
                selection.Save(Path.GetDirectoryName(directory),delegate { System.Threading.Interlocked.Increment(ref Duplicates); });
                StartTimeline(selection.Representative(target), stableSince, now);
                candidateSaved = true;
                return target;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

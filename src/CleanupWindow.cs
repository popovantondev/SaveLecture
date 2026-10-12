using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SaveLecture
{
    internal sealed class CleanupWindow : IDisposable
    {
        public readonly CleanupPage Page;
        public bool IsDisposed { get { return Page.IsDisposed; } }
        public void Dispose() { Page.Dispose(); }
        readonly Func<bool> recording;
        readonly string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SaveLecture","cleanup-threshold.txt");
        public bool Busy { get { return !Page.Panel.IsEnabled; } }
        public CleanupWindow(string archive,Func<bool> isRecording)
        {
            recording=isRecording; Page=new CleanupPage(archive,Run);
            double loaded; if(File.Exists(settings)&&Double.TryParse(File.ReadAllText(settings),out loaded)) Page.Threshold.Value=Math.Max(80,Math.Min(100,loaded));
            Page.Threshold.ValueChanged+=delegate { try { Directory.CreateDirectory(Path.GetDirectoryName(settings)); File.WriteAllText(settings,Page.Threshold.Value.ToString()); } catch(IOException error) { Page.Status.Text=error.Message; } };
        }
        public void PreviewFolder(string path) { Page.Folder.Text=path; Run("preview"); }
        void Run(string mode)
        {
            if (recording()) { Page.Status.Text = "Нажмите Стоп перед очисткой."; return; }
            if (!Directory.Exists(Page.Folder.Text)) { Page.Status.Text = "Выберите папку scr."; return; }
            string path = Page.Folder.Text, percent = ((double)Page.Threshold.Value/100).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Page.Panel.IsEnabled = false; Page.Status.Text = "Проверяю кадры…";
            new Thread(delegate()
            {
                string result; bool success = false;
                try
                {
                    string python=UiPreferences.Load().PythonExecutable;
                    string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "slide_cleanup.py");
                    var info = new ProcessStartInfo(python, "\"" + script + "\" " + mode + " \"" + path.TrimEnd('\\') + "\" " + percent) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                    using (var process = Process.Start(info))
                    {
                        string error = null;
                        var errorThread = new Thread(delegate() { error = process.StandardError.ReadToEnd(); }); errorThread.Start();
                        result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); errorThread.Join();
                        if (process.ExitCode != 0) throw new IOException(error);
                        success = true;
                    }
                }
                catch (Exception error) { result = error.Message; }
                if (Page.IsDisposed) return;
                (Page.Shell??Page).BeginInvoke((Action)delegate
                {
                    Page.Panel.IsEnabled = true; Page.Status.Text = result;
                    if (success && mode == "preview") { try { Page.LoadPreview(); } catch(Exception error) { Page.Status.Text=error.Message; } }
                });
            }) { IsBackground = true }.Start();
        }
    }
}

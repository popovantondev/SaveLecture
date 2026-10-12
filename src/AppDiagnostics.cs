using System;
using System.IO;
using System.Windows.Forms;

namespace SaveLecture
{
    internal static class AppDiagnostics
    {
        static readonly object gate=new object();
        static DateTime lastNotice=DateTime.MinValue;
        internal static string LogPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SaveLecture","errors.log"); } }
        internal static void Log(Exception error,string context)
        {
            try { lock(gate) { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.AppendAllText(LogPath,DateTime.Now.ToString("o")+" "+context+Environment.NewLine+error+Environment.NewLine); } } catch { }
        }
        internal static void Report(Exception error,string context)
        {
            Log(error,context);
            if((DateTime.Now-lastNotice).TotalSeconds<30) return;
            lastNotice=DateTime.Now;
            MessageBox.Show(UiText.Get(context)+". "+UiText.Get("Подробности сохранены в журнале ошибок.")+"\n\n"+error.Message+"\n\n"+LogPath,UiText.Get("SaveLecture — ошибка"),MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        internal static void Install()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e) { Report(e.Exception,"Ошибка интерфейса"); };
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { e.Handled=true; Report(e.Exception,"Ошибка интерфейса"); };
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e) { Log(e.ExceptionObject as Exception??new Exception(Convert.ToString(e.ExceptionObject)),"Завершение процесса"); };
        }
    }
}

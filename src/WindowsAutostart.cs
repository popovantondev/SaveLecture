using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SaveLecture
{
    internal sealed class WindowsAutostart
    {
        internal const string BackgroundArgument="--background";
        internal static string TestDirectory=null,TestExecutable=null;
        readonly string directory,executable;
        internal string ShortcutPath { get { return Path.Combine(directory,"SaveLecture.lnk"); } }
        internal WindowsAutostart()
        {
            directory=TestDirectory??Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            executable=Path.GetFullPath(TestExecutable??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SaveLecture.exe"));
        }
        static object Call(object owner,string member,BindingFlags flags,params object[] args)
        { return owner.GetType().InvokeMember(member,flags,null,owner,args); }
        static bool SamePath(string left,string right)
        { return !String.IsNullOrWhiteSpace(left) && String.Equals(Path.GetFullPath(left),right,StringComparison.OrdinalIgnoreCase); }
        void Shortcut(Action<object> action)
        {
            object shell=null,link=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
                link=Call(shell,"CreateShortcut",BindingFlags.InvokeMethod,ShortcutPath); action(link);
            }
            catch(TargetInvocationException error) { throw new IOException("Не удалось изменить автозапуск Windows.",error.InnerException??error); }
            catch(COMException error) { throw new IOException("Не удалось изменить автозапуск Windows.",error); }
            finally { if(link!=null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); if(shell!=null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        }
        internal bool Enabled
        {
            get {
                if(!File.Exists(ShortcutPath)) return false;
                bool enabled=false;
                Shortcut(delegate(object link) { enabled=SamePath((string)Call(link,"TargetPath",BindingFlags.GetProperty),executable); });
                return enabled;
            }
        }
        internal string Arguments
        {
            get { string arguments=""; if(File.Exists(ShortcutPath)) Shortcut(delegate(object link) { arguments=(string)Call(link,"Arguments",BindingFlags.GetProperty); }); return arguments; }
        }
        internal void EnsureStartInTray()
        {
            if(!Enabled) return;
            Shortcut(delegate(object link) {
                string arguments=(string)Call(link,"Arguments",BindingFlags.GetProperty)??"";
                if(Array.IndexOf(arguments.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries),BackgroundArgument)>=0) return;
                Call(link,"Arguments",BindingFlags.SetProperty,(arguments+" "+BackgroundArgument).Trim());
                Call(link,"Save",BindingFlags.InvokeMethod);
            });
        }
        internal void SetEnabled(bool enabled)
        {
            if(File.Exists(ShortcutPath)) {
                bool owned=false;
                Shortcut(delegate(object link) { owned=SamePath((string)Call(link,"TargetPath",BindingFlags.GetProperty),executable); });
                if(!owned) throw new IOException("Автозапуск настроен для другой копии SaveLecture. Её ярлык сохранён.");
            }
            if(!enabled) { if(File.Exists(ShortcutPath)) File.Delete(ShortcutPath); return; }
            if(!File.Exists(executable)) throw new IOException("Не найден SaveLecture.exe для автозапуска.");
            Directory.CreateDirectory(directory);
            Shortcut(delegate(object link) {
                Call(link,"TargetPath",BindingFlags.SetProperty,executable);
                Call(link,"WorkingDirectory",BindingFlags.SetProperty,Path.GetDirectoryName(executable));
                Call(link,"Arguments",BindingFlags.SetProperty,BackgroundArgument);
                Call(link,"WindowStyle",BindingFlags.SetProperty,7);
                Call(link,"Description",BindingFlags.SetProperty,"SaveLecture");
                Call(link,"Save",BindingFlags.InvokeMethod);
            });
        }
    }
}

using System;
using System.IO;
using System.Web.Script.Serialization;
namespace SaveLecture
{
    public sealed class UiPreferences
    {
        public bool SaveSlides=true, SaveCaptions=true, HideToTray=true;
        public double DuplicateThreshold=.89, StableThreshold=.94;
        public int PixelShiftThreshold=12;
        public double CaptureIntervalSeconds=1;
        public string ArchiveRoot="";
        public string Language="ru";
        public int ConsentNoticeVersion=0;
        public string PythonExecutable="python.exe";
        public UiPreferences Copy() { return (UiPreferences)MemberwiseClone(); }
        public void Normalize()
        {
            DuplicateThreshold=Limit(DuplicateThreshold,.8,1,.89);
            StableThreshold=Limit(StableThreshold,.8,1,.94);
            CaptureIntervalSeconds=Limit(CaptureIntervalSeconds,.5,10,1);
            PixelShiftThreshold=Math.Max(0,Math.Min(24,PixelShiftThreshold));
            if(Language!="ru" && Language!="de" && Language!="en") Language="ru";
        }
        static double Limit(double value,double min,double max,double fallback)
        { return Double.IsNaN(value)||Double.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value)); }
        public string StoragePath { get { return String.IsNullOrWhiteSpace(ArchiveRoot)?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Archive"):Path.GetFullPath(ArchiveRoot); } }
        internal static string TestPath=null;
        static string PathName { get { return TestPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SaveLecture","preferences.json"); } }
        internal static string DataDirectory { get { return Path.GetDirectoryName(PathName); } }
        public static UiPreferences Load()
        {
            try { var value=new JavaScriptSerializer().Deserialize<UiPreferences>(File.ReadAllText(PathName)) ?? new UiPreferences(); value.Normalize(); return value; }
            catch(Exception) { return new UiPreferences(); }
        }
        public void Save()
        {
            Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp=PathName+".tmp"; File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));
            if(File.Exists(PathName)) File.Replace(temp,PathName,null); else File.Move(temp,PathName);
        }
    }
}

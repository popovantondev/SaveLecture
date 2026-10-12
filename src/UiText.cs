using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SaveLecture
{
    internal static class UiText
    {
        internal static string Language="ru";
        internal static System.Globalization.CultureInfo Culture { get { return System.Globalization.CultureInfo.GetCultureInfo(Language=="en"?"en-US":Language=="de"?"de-DE":"ru-RU"); } }
        static Dictionary<string,string[]> catalog;
        sealed class Source { public Source() {} internal string Original,Translated; }
        static readonly ConditionalWeakTable<DependencyObject,Source> sources=new ConditionalWeakTable<DependencyObject,Source>();
        internal static string Get(string value)
        {
            if(value==null || Language=="ru") return value;
            if(catalog==null) catalog=new JavaScriptSerializer().Deserialize<Dictionary<string,string[]>>(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","localization.json")));
            string[] result;
            if(catalog.TryGetValue(value,out result)) return result[Language=="de"?0:1];
            // Numbers and keyboard hints are kept in the same position as in the source.
            var keys=new List<string>(catalog.Keys); keys.Sort(delegate(string a,string b) { return b.Length.CompareTo(a.Length); });
            foreach(string key in keys) if(key.Length>0 && value.Contains(key)) value=value.Replace(key,catalog[key][Language=="de"?0:1]);
            return value;
        }
        static string Localized(DependencyObject owner,string current)
        {
            var source=sources.GetOrCreateValue(owner);
            if(source.Original==null || current!=source.Translated) source.Original=current;
            source.Translated=Get(source.Original); return source.Translated;
        }
        internal static void Apply(DependencyObject root)
        {
            var element=root as FrameworkElement;
            if(element!=null && element.Name=="TranscriptRows") { var empty=((FrameworkElement)root).FindName("EmptyTranscript") as TextBlock; if(empty!=null) empty.Text=Localized(empty,empty.Text); return; }
            if(element!=null && (element.Name=="Message" || Equals(element.Tag,"UserContent"))) return;
            var text=root as TextBlock;
            if(text!=null) { var value=Localized(text,text.Text); if(text.Text!=value) text.Text=value; }
            var button=root as ContentControl;
            if(button!=null && button.Content is string) button.Content=Localized(button,(string)button.Content);
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) Apply(VisualTreeHelper.GetChild(root,i));
        }
    }
}

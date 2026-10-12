using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SaveLecture
{
    internal sealed class FirstRunNotice : DesignWindow
    {
        internal FirstRunNotice(UiPreferences preferences) : base("first-run.xaml",660,510)
        {
            Text="SaveLecture"; MinimumSize=ClientSize;
            Find<ContentControl>("LanguageChoice").Content=new LanguagePicker(preferences.Language,delegate(string language) { UiText.Language=language; UiText.Apply(View); });
            var accepted=Find<System.Windows.Controls.CheckBox>("Acknowledged");
            accepted.Checked+=delegate { Find<System.Windows.Controls.Button>("Accept").IsEnabled=true; };
            accepted.Unchecked+=delegate { Find<System.Windows.Controls.Button>("Accept").IsEnabled=false; };
            Wire("Cancel",delegate { DialogResult=DialogResult.Cancel; Close(); });
            Wire("Accept",delegate {
                if(accepted.IsChecked!=true) return;
                preferences.Language=UiText.Language; preferences.ConsentNoticeVersion=1;
                try { preferences.Save(); DialogResult=DialogResult.OK; Close(); }
                catch(System.Exception error) { AppDiagnostics.Report(error,"Не удалось сохранить настройки"); }
            });
        }
    }
}

using System.Windows.Forms;
namespace SaveLecture
{
    internal sealed class StopConfirmation : DesignWindow
    {
        internal bool Confirmed;
        internal StopConfirmation() : base("confirm-stop.xaml",420,220)
        {
            Text="SaveLecture — остановить запись?"; ShowInTaskbar=false;
            Wire("Cancel",delegate { DialogResult=DialogResult.Cancel; Close(); });
            Wire("Confirm",delegate { Confirmed=true; DialogResult=DialogResult.OK; Close(); });
        }
        protected override bool ProcessCmdKey(ref Message message,Keys keys)
        {
            if(keys==Keys.Escape) { DialogResult=DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref message,keys);
        }
    }
}

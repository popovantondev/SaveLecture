using System.Drawing;
using System.Windows.Forms;

namespace SaveLecture
{
    internal sealed class MentionPopup : DesignWindow
    {
        private readonly System.Windows.Controls.TextBlock message;
        private readonly Timer timer;
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000; return parameters; }
        }
        public MentionPopup() : base("mention.xaml",420,180)
        {
            Text="Вас упомянули"; ShowInTaskbar=false; TopMost=true; StartPosition=FormStartPosition.Manual;
            message=Find<System.Windows.Controls.TextBlock>("Message"); ReturnRequested=delegate { Hide(); };
            timer = new Timer { Interval = 7000 };
            timer.Tick += delegate { timer.Stop(); Hide(); };
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; Hide(); timer.Stop(); } };
        }
        public void Notify(string text)
        {
            message.Text = "" + text;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
            Show(); timer.Stop(); timer.Start();
        }
        internal void Preview(string text) { Notify(text); timer.Stop(); }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}

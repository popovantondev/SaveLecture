using System.Drawing;
using System.Windows.Forms;
namespace SaveLecture
{
    internal static class UiStyle
    {
        private sealed class StyledButton : Button
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                if (Enabled) { base.OnPaint(e); return; }
                using (var brush = new SolidBrush(Color.FromArgb(38,55,47))) e.Graphics.FillRectangle(brush, ClientRectangle);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.FromArgb(126,145,134), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
        public static Button Button(string text, bool primary)
        {
            var button = new StyledButton { Text = text, Width = 125, Height = 40, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0,0,10,0), BackColor = primary ? Color.FromArgb(36,184,235) : Color.FromArgb(38,55,47), ForeColor = primary ? Color.FromArgb(15,31,48) : Color.FromArgb(232,241,236), Font = new Font("Segoe UI",10,FontStyle.Bold) };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(79,202,244) : Color.FromArgb(53,73,63);
            return button;
        }
    }
}

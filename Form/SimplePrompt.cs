using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
	public static class SimplePrompt
	{
		public static string Show(string title, string label, string defaultText = "")
		{
			using (var f = new Form())
			using (var lbl = new Label())
			using (var txt = new TextBox())
			using (var ok = new Button())
			using (var cancel = new Button())
			{
				f.Text = title;
				f.FormBorderStyle = FormBorderStyle.FixedDialog;
				f.StartPosition = FormStartPosition.CenterParent;
				f.MinimizeBox = f.MaximizeBox = false;
				f.ClientSize = new Size(420, 140);
				f.AcceptButton = ok;
				f.CancelButton = cancel;
				lbl.Text = label;
				lbl.AutoSize = true;
				lbl.Location = new Point(12, 15);
				txt.Text = defaultText;
				txt.Location = new Point(12, 45);
				txt.Width = 394;
				ok.Text = "OK";
				ok.DialogResult = DialogResult.OK;
				ok.Location = new Point(230, 90);
				cancel.Text = "Cancel";
				cancel.DialogResult = DialogResult.Cancel;
				cancel.Location = new Point(320, 90);
				f.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
				DesktopTheme.ApplySettings(f);
				return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;


namespace Aron_V2
{
	public static class Prompt
	{
		public static string Show(string text, string caption, string defaultValue = "")
		{
			using (var form = new Form())
			using (var lbl = new Label())
			using (var txt = new TextBox())
			using (var ok = new Button())
			using (var cancel = new Button())
			{
				form.Text = caption;
				form.FormBorderStyle = FormBorderStyle.FixedDialog;
				form.StartPosition = FormStartPosition.CenterParent;
				form.MinimizeBox = false; form.MaximizeBox = false;
				form.ClientSize = new Size(360, 140);

				lbl.AutoSize = true; lbl.Text = text; lbl.Location = new Point(12, 15);
				txt.Size = new Size(330, 23); txt.Location = new Point(15, 45); txt.Text = defaultValue;
				ok.Text = "确定"; ok.DialogResult = DialogResult.OK; ok.Location = new Point(190, 90);
				cancel.Text = "取消"; cancel.DialogResult = DialogResult.Cancel; cancel.Location = new Point(275, 90);

				form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
				form.AcceptButton = ok; form.CancelButton = cancel;

				return form.ShowDialog() == DialogResult.OK ? txt.Text : string.Empty;
			}
		}
	}
}

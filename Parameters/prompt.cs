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
				form.SuspendLayout();
				form.AutoScaleDimensions = new SizeF(96F, 96F);
				form.AutoScaleMode = AutoScaleMode.Dpi;
				form.Font = new Font("Segoe UI", 9F);
				form.AutoSize = true;
				form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
				form.Text = caption;
				form.FormBorderStyle = FormBorderStyle.FixedDialog;
				form.StartPosition = FormStartPosition.CenterParent;
				form.MinimizeBox = false; form.MaximizeBox = false;
				form.ClientSize = new Size(420, 160);
				var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(420, 0), ColumnCount = 1, Padding = new Padding(16) };
				layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
				for (int row = 0; row < 3; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
				lbl.AutoSize = true; lbl.Text = text; lbl.Margin = new Padding(4, 4, 4, 12);
				txt.Dock = DockStyle.Fill; txt.MinimumSize = new Size(330, 0); txt.Text = defaultValue;
				ok.Text = "OK"; ok.DialogResult = DialogResult.OK;
				cancel.Text = "Cancel"; cancel.DialogResult = DialogResult.Cancel;
				foreach (Button button in new[] { ok, cancel }) { button.AutoSize = true; button.MinimumSize = new Size(80, 32); button.Margin = new Padding(6); }
				var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 12, 0, 0) };
				buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
				layout.Controls.Add(lbl, 0, 0); layout.Controls.Add(txt, 0, 1); layout.Controls.Add(buttons, 0, 2);
				form.Controls.Add(layout);
				form.AcceptButton = ok; form.CancelButton = cancel;
				DesktopTheme.ApplySettings(form);
				UiLanguage.ApplyTo(form);
				form.ResumeLayout(true);

				return form.ShowDialog() == DialogResult.OK ? txt.Text : string.Empty;
			}
		}
	}
}

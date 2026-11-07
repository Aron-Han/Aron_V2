using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class DialogLayoutTests
{
	private static void Layout(Control control)
	{
		foreach (Control child in control.Controls) Layout(child);
		control.PerformLayout();
	}

	private static void Check(Control parent)
	{
		var controls = parent.Controls.Cast<Control>().Where(x => x.Visible && x.Width > 0 && x.Height > 0).ToArray();
		for (int i = 0; i < controls.Length; i++)
		{
			Control item = controls[i];
			if (item.Left < 0 || item.Top < 0 || item.Right > parent.ClientSize.Width || item.Bottom > parent.ClientSize.Height)
				throw new Exception("Clipped control: " + item.Text + " " + item.Bounds + " in " + parent.ClientSize);
			for (int j = i + 1; j < controls.Length; j++)
				if (item.Bounds.IntersectsWith(controls[j].Bounds)) throw new Exception("Overlap: " + item.Text + " / " + controls[j].Text);
			if (item is Button && !string.IsNullOrEmpty(item.Text))
			{
				Size textSize = TextRenderer.MeasureText(item.Text, item.Font);
				if (textSize.Width > item.ClientSize.Width || textSize.Height > item.ClientSize.Height) throw new Exception("Button text clipped: " + item.Text);
			}
			Check(item);
		}
	}

	[STAThread]
	private static void Main(string[] args)
	{
		try { Run(args); }
		catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); Environment.Exit(1); }
	}

	private static void Run(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Assembly app = Assembly.LoadFrom(args[0]);
		Type login = app.GetType("Aron_V2.LoginSimple", true);
		string output = Path.GetFullPath("Tests/UIValidation");
		Directory.CreateDirectory(output);
		foreach (string name in new[] { "LoginSimple", "AdminAuthDialog", "ChangePasswordForm", "RegisterUserForm", "CommunicationSettingForm", "TcpIpSettingForm", "S7InputSettingForm", "S7OutputSettingForm", "ProfinetInputSettingForm", "ProfinetOutputSettingForm" })
		{
			Type type = app.GetType("Aron_V2." + name) ?? login.GetNestedType(name, BindingFlags.NonPublic);
			foreach (float fontScale in new[] { 1F, 1.25F, 1.5F, 2F })
			using (Form form = (Form)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, name == "ChangePasswordForm" ? new object[] { "test-user" } : new object[0], null))
			{
				// Exercise dynamic layout with enlarged text independently of Windows
				// display settings. A manifested process separately tests real system DPI.
				form.Font = new Font("Segoe UI", 9F * fontScale);
				form.ShowInTaskbar = false;
				form.Opacity = 0;
				form.Show();
				Application.DoEvents(); Layout(form); Application.DoEvents();
				Check(form);
				using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
				{
					form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
					bitmap.Save(Path.Combine(output, name + "-font-" + fontScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
				}
				Console.WriteLine("PASS " + name + " font scale " + fontScale + " " + form.ClientSize);
				form.Close();
			}
		}
		Environment.Exit(0);
	}
}

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class MainStatusColumnsTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var style = app.GetType("Aron_V2.DesktopTheme").GetMethod("StyleMainStatus", BindingFlags.NonPublic | BindingFlags.Static);
            using (var form = new Form { ClientSize = new Size(1000, 100), ShowInTaskbar = false, Opacity = 0 })
            using (var strip = new StatusStrip())
            {
                var user = new ToolStripStatusLabel { Text = "Guest", ForeColor = Color.Black };
                var communication = new ToolStripStatusLabel { Text = "Communicate:Profinet", ForeColor = Color.Red };
                strip.Items.AddRange(new ToolStripItem[] { user, communication }); form.Controls.Add(strip);
                style.Invoke(null, new object[] { strip, user, communication });
                form.Show(); Application.DoEvents();
                foreach (int width in new[] { 800, 1280, 1920 })
                {
                    form.ClientSize = new Size(width, 100); Application.DoEvents();
                    Check(user.Bounds.Right < communication.Bounds.Left && strip.DisplayRectangle.Contains(user.Bounds) && strip.DisplayRectangle.Contains(communication.Bounds), "Separated status columns fit at " + width);
                    Check(user.Bounds.Top + user.Height / 2 == communication.Bounds.Top + communication.Height / 2, "Status text shares vertical centerline");
                }
                user.Text = "administrator (Admin)";
                communication.Text = "Communicate:S7"; communication.ForeColor = Color.Green;
                Application.DoEvents();
                Check(user.Text == "administrator (Admin)" && communication.Text == "Communicate:S7" && communication.ForeColor == Color.Green, "Original runtime text and colors retained");
                Check(user.Width >= TextRenderer.MeasureText(user.Text, user.Font).Width + user.Padding.Horizontal, "User column accommodates a longer login name");
                Check(user.Bounds.Right < communication.Bounds.Left, "Live updates keep columns separated");
                Directory.CreateDirectory("Tests/UIValidation");
                using (var bitmap = new Bitmap(strip.Width, strip.Height))
                { strip.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/MainStatusColumns.png"); }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

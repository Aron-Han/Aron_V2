using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class LogDetailsFormTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Flags).GetValue(obj); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void CheckBounds(Control parent)
    {
        var children = parent.Controls.Cast<Control>().Where(c => c.Visible && c.Width > 0 && c.Height > 0).ToArray();
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            Check(child.Left >= 0 && child.Top >= 0 && child.Right <= parent.ClientSize.Width && child.Bottom <= parent.ClientSize.Height, "Fits: " + child.GetType().Name + " " + child.Text.Split('\n')[0].Substring(0, Math.Min(30, child.Text.Split('\n')[0].Length)));
            for (int j = i + 1; j < children.Length; j++) if (child.Bounds.IntersectsWith(children[j].Bounds)) throw new Exception("Overlapping detail controls: " + child.Text + " / " + children[j].Text);
            CheckBounds(child);
        }
    }
    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run(Assembly app)
    {
        Application.EnableVisualStyles();
        var entryType = app.GetType("Aron_V2.LogEntry", true);
        var dialogType = app.GetType("Aron_V2.LogDetailsForm", true);
        string example = "Camera offline; communication stopped until camera recovery: Startup camera check failed: Cam1";
        foreach (string message in new[] { example, example + "\r\n" + string.Join("\r\n", Enumerable.Repeat("System.InvalidOperationException: a complete diagnostic frame with a long path D:\\Project\\Inspection\\Job1\\Cam1\\Pos1\\Inspection.vpp", 250)), new string('X', 5000), string.Empty })
        {
            object entry = entryType.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { message, Color.Orange, new DateTime(2026, 10, 23, 15, 15, 27, 920) });
            using (var dialog = (Form)Activator.CreateInstance(dialogType, Flags, null, new[] { entry }, null))
            {
                dialog.Opacity = 0; dialog.Show(); Application.DoEvents();
                var body = (RichTextBox)Field(dialog, "_message");
                var copy = (Button)Field(dialog, "_copy"); var close = (Button)Field(dialog, "_close");
                Check(body.ReadOnly && body.WordWrap && body.ScrollBars == RichTextBoxScrollBars.Vertical && !body.DetectUrls, "Read-only details wrap with vertical scrolling only");
                Check(body.Text.Replace("\r\n", "\n") == message.Replace("\r\n", "\n"), "Display retains complete text without truncation");
                Check((string)dialogType.GetProperty("FullMessageForCopy", Flags).GetValue(dialog, null) == message, "Copy payload preserves original characters and line endings");
                Check(ReferenceEquals(dialog.AcceptButton, close) && ReferenceEquals(dialog.CancelButton, close), "Enter and Escape both close the details dialog");
                Check(copy.Text == "Copy All" && close.BackColor == Color.FromArgb(26, 119, 207), "Copy/Close actions use approved theme");
                if (message == example)
                {
                    var root = (TableLayoutPanel)dialog.Controls[0];
                    Check(root.GetControlFromPosition(0, 0).Height <= dialog.Font.Height * 3, "Header remains compact rather than inheriting a large default control height");
                    Directory.CreateDirectory("Tests/UIValidation");
                    using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Log-Details-B.png"); }
                    foreach (float scale in new[] { 1F, 1.5F, 2F })
                    {
                        dialog.Font = new Font("Segoe UI", 9F * scale); dialog.Size = dialog.MinimumSize;
                        dialog.PerformLayout(); Application.DoEvents();
                        CheckBounds(dialog);
                        body.Text = example + " " + example + " " + example;
                        Application.DoEvents();
                        Check(body.GetLineFromCharIndex(body.TextLength - 1) > 0, "Overlong single-line text visibly wraps at font scale " + scale);
                        body.Text = example;
                        foreach (var button in new[] { copy, close }) Check(TextRenderer.MeasureText(button.Text, button.Font).Width <= button.ClientSize.Width, "Action label is not clipped");
                    }
                }
                close.PerformClick(); Check(!dialog.Visible, "Close action dismisses details without touching logs/devices");
            }
        }
    }
}

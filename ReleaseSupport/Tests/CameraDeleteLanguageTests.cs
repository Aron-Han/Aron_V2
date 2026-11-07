using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Test the confirmation UI only. No camera config is loaded or removed and
// no VPP, camera acquisition, inspection or PLC API is invoked.
internal static class CameraDeleteLanguageTests
{
    const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var language = app.GetType("Aron_V2.UiLanguage");
            var culture = CultureInfo.CurrentCulture;
            foreach (string code in new[] { "en", "zh-CN", "en" })
            foreach (string action in new[] { "Yes", "No", "Close" })
            {
                language.GetMethod("SetLanguage", Static).Invoke(null, new object[] { code });
                using (var dialog = (Form)language.GetMethod("CreateCameraDeleteConfirmation", Static).Invoke(null, new object[] { "Cam1" }))
                using (var timer = new Timer { Interval = 40 })
                {
                    dialog.Opacity = 0;
                    Exception failure = null;
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        try
                        {
                            var body = (Label)dialog.Controls.Find("ConfirmationMessage", true).Single();
                            var yes = (Button)dialog.Controls.Find("ConfirmationYes", true).Single();
                            var no = (Button)dialog.Controls.Find("ConfirmationNo", true).Single();
                            bool chinese = code == "zh-CN";
                            Check(dialog.Text == (chinese ? "确认" : "Confirm") && yes.Text == (chinese ? "是" : "Yes") && no.Text == (chinese ? "否" : "No"), "Title and buttons follow selected language: " + code);
                            Check(body.Text.Contains("Cam1") && body.Text.Contains(chinese ? "磁盘中的 VPP 文件将保留" : "VPP files on disk will be kept") && body.Text.Contains(chinese ? "不会覆盖" : "will not overwrite"), "Camera identity and VPP-preservation warning retained: " + code);
                            Check(body.Parent.ClientRectangle.Contains(body.Bounds) && yes.Bounds.Right <= yes.Parent.Width && no.Bounds.Right <= no.Parent.Width && !yes.Bounds.IntersectsWith(no.Bounds), "Localized message/buttons fit without clipping");
                            Check(ReferenceEquals(dialog.AcceptButton, yes) && ReferenceEquals(dialog.CancelButton, no), "Default Yes and cancel No contract retained");
                            if (action == "Yes")
                            {
                                Directory.CreateDirectory("Tests/UIValidation");
                                using (var image = new Bitmap(dialog.Width, dialog.Height))
                                { dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/CameraDelete-" + code + ".png"); }
                                yes.PerformClick();
                            }
                            else if (action == "No") no.PerformClick();
                            else dialog.Close();
                        }
                        catch (Exception ex) { failure = ex; dialog.Close(); }
                    };
                    timer.Start();
                    DialogResult result = dialog.ShowDialog();
                    if (failure != null) throw failure;
                    Check(action == "Yes" ? result == DialogResult.Yes : action == "No" ? result == DialogResult.No : result != DialogResult.Yes,
                        "Decision result preserved: " + code + " / " + action);
                }
            }
            Check(ReferenceEquals(culture, CultureInfo.CurrentCulture), "Localization leaves numeric/date parsing culture unchanged");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

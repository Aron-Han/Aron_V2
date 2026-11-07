using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class StartupProgressTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var startup = app.GetType("Aron_V2.StartUP", true);
            var progressType = app.GetType("Aron_V2.ProgressInfo", true);
            using (var form = (Form)Activator.CreateInstance(startup))
            {
                // Test presentation only, never run initialization/camera/PLC.
                form.Shown -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, startup.GetMethod("StartUP_Shown", Private));
                form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
                var display = startup.GetMethod("DisplayProgress", Private);
                Action<int, string> report = (percent, message) => display.Invoke(form, new[] { Activator.CreateInstance(progressType, percent, message) });
                report(55, "CH0 / Job1 / Cam1 / Pos1 [Main] - Loading Inspection: D:\\VPP\\Job1\\Cam1\\Pos1\\Inspection.VPP");
                report(55, "OK CH0 / Job1 / Cam1 / Pos1 - Inspection (2.35s)");
                report(55, "FAIL CH0 / Job1 / Cam1 / Pos1 - Camera: VPP file not found");
                var details = (TextBox)startup.GetField("_details", Private).GetValue(form);
                Check(details.ReadOnly && details.Multiline && details.WordWrap && details.ScrollBars == ScrollBars.Vertical, "Detail history is read-only, wrapped and vertically scrollable");
                Check(details.Text.Contains("Inspection.VPP") && details.Text.Contains("2.35s") && details.Text.Contains("FAIL"), "History retains file, context, elapsed time and failure details");
                report(20, "Earlier progress cannot move the bar backwards");
                var bar = (ProgressBar)startup.GetField("progressBar1", Private).GetValue(form);
                Check(bar.Value == 55, "Progress remains monotonic");
                report(100, "Initialization finished");
                Check(bar.Value == 100, "100 percent is reserved for initialization completion");
                foreach (var size in new[] { new Size(810, 420), new Size(1020, 525), new Size(1200, 650) })
                {
                    form.ClientSize = size; Application.DoEvents();
                    Check(details.Width > 500 && details.Height > 100 && details.Bottom <= details.Parent.ClientSize.Height, "Detail history remains usable at " + size);
                }
                var timer = (Timer)startup.GetField("_elapsedTimer", Private).GetValue(form);
                Check(!timer.Enabled, "UI tests do not start bootstrap or elapsed timer");
                var loader = app.GetType("Aron_V2.Load_Job", true);
                Check(loader.GetMethod("LoadVPP_ForChannel_AllPos", new[] { typeof(int), typeof(string) }) != null, "Existing two-argument job-switch API is retained");
                var observer = loader.GetMethod("ReportVppProgress", BindingFlags.NonPublic | BindingFlags.Static);
                observer.Invoke(null, new object[] { new Action<string>(s => { throw new Exception("observer failure"); }), "test" });
                Check(true, "A failed progress observer cannot interrupt VPP job loading");
                Directory.CreateDirectory("Tests/UIValidation");
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save("Tests/UIValidation/Startup-Details.png");
                }
                form.Close();
            }
            Environment.Exit(0);
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); Environment.Exit(1); }
    }
}

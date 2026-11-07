using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Real display-only control + synthetic bitmap. No VPP, camera acquisition,
// Program.Main, Bootstrap, GoSystem, inspection Run or PLC is invoked.
internal static class DisplayAutoFitProbe
{
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            object[] report = { null };
            if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report)) throw new Exception((string)report[0]);
            using (var host = new Form { ClientSize = new Size(640, 540) })
            using (var view = (UserControl)Activator.CreateInstance(app.GetType("Aron_V2.UserControl_ImageShow")))
            using (var bitmap = new Bitmap(320, 240))
            {
                using (var graphics = Graphics.FromImage(bitmap)) { graphics.Clear(Color.White); graphics.FillRectangle(Brushes.Black, 50, 50, 120, 100); }
                var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
                object image = Activator.CreateInstance(core.GetType("Cognex.VisionPro.CogImage8Grey"), bitmap);
                try
                {
                    object display = view.GetType().GetProperty("CogDisplay").GetValue(view);
                    var autoFit = display.GetType().GetProperty("AutoFit");
                    var imageProperty = display.GetType().GetProperty("Image");
                    imageProperty.SetValue(display, image);
                    view.Dock = DockStyle.Fill; host.Controls.Add(view); host.Show(); Application.DoEvents();
                    foreach (Size size in new[] { new Size(640, 540), new Size(900, 700), new Size(640, 400) })
                    {
                        host.ClientSize = size; Application.DoEvents(); Thread.Sleep(50); Application.DoEvents();
                        if (!(bool)autoFit.GetValue(display) || !ReferenceEquals(imageProperty.GetValue(display), image)) throw new Exception("Display lost AutoFit or its original image");
                        Console.WriteLine("PASS real CogRecordDisplay AutoFit and synthetic source image retained at " + size);
                    }
                    string[] names = { "JobID", "CamID", "PosID", "Channel" };
                    foreach (var identity in new[] { new[] { "SetButton_JobID", "Job1" }, new[] { "SetButton_CamID", "Cam4" }, new[] { "SetButton_PosID", "Pos1" }, new[] { "SetButton_EngineID", "Engine:0" } })
                        view.GetType().GetMethod(identity[0]).Invoke(view, new object[] { Color.Gainsboro, identity[1] });
                    object[] before = Array.ConvertAll(names, n => view.GetType().GetProperty(n).GetValue(view));
                    var language = app.GetType("Aron_V2.UiLanguage");
                    language.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "zh-CN" });
                    language.GetMethod("ApplyTo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { view });
                    for (int i = 0; i < names.Length; i++) if (!Equals(before[i], view.GetType().GetProperty(names[i]).GetValue(view))) throw new Exception("Language changed runtime ID " + names[i]);
                    Console.WriteLine("PASS actual camera view Job/Cam/Pos/Channel getters unchanged by UI translation");
                }
                finally { (image as IDisposable)?.Dispose(); }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

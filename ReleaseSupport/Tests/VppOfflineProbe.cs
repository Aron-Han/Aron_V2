using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// One VPP per process: no Program.Main, FormMain, tool.Run, camera trigger/live,
// GoSystem, camera SDK manager or communication service is ever invoked here.
internal static class VppOfflineProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Console.WriteLine("STAGE dependency preflight");
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            object[] report = { null };
            if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report)) throw new Exception((string)report[0]);
            string file = Path.GetFullPath(args[1]);
            bool camera = string.Equals(Path.GetFileName(file), "Camera.vpp", StringComparison.OrdinalIgnoreCase);
            if (args.Contains("--missing-editor")) { CheckMissingEditor(app, camera); return; }
            var watch = Stopwatch.StartNew();
            Console.WriteLine("STAGE deserialize " + file);
            var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
            object tool;
            try { tool = core.GetType("Cognex.VisionPro.CogSerializer").GetMethod("LoadObjectFromFile", new[] { typeof(string) }).Invoke(null, new object[] { file }); }
            catch (TargetInvocationException ex)
            {
                if (!args.Contains("--expect-load-failure")) throw;
                Console.WriteLine("PASS expected VPP load failure: " + ex.GetBaseException().GetType().Name);
                return;
            }
            if (args.Contains("--expect-load-failure")) { (tool as IDisposable)?.Dispose(); throw new Exception("Invalid VPP unexpectedly loaded."); }
            try
            {
                string expected = camera ? "Cognex.VisionPro.CogAcqFifoTool" : "Cognex.VisionPro.ToolBlock.CogToolBlock";
                if (tool.GetType().FullName != expected) throw new Exception("Wrong VPP type: " + tool.GetType());
                if (args.Contains("--editor")) CheckEditor(app, tool, camera);
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("Cognex.VisionPro")))
                    if (assembly.GetName().Version != new Version(71, 2, 0, 0)) throw new Exception("Mixed VisionPro assembly: " + assembly.FullName);
                Console.WriteLine("PASS " + file + " | " + tool.GetType().Name + " | " + watch.ElapsedMilliseconds + "ms | peakMB=" + Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024);
            }
            finally { (tool as IDisposable)?.Dispose(); }
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); Environment.ExitCode = 1; }
    }

    static void CheckEditor(Assembly app, object tool, bool camera)
    {
        int runs = 0;
        EventHandler onRan = (s, e) => runs++;
        var ran = tool.GetType().GetEvents().FirstOrDefault(e => e.Name == "Ran");
        if (ran != null) ran.AddEventHandler(tool, onRan);
        object config = Activator.CreateInstance(app.GetType("Aron_V2.AppConfig"));
        string name = camera ? "Camera" : "Algorithm";
        using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2." + name), config))
        {
            var ensure = form.GetType().GetMethod(camera ? "EnsureCognexEditor" : "EnsureAlgorithmEditor", Private);
            if (!(bool)ensure.Invoke(form, null)) throw new Exception("Editor initialization failed.");
            object editor = form.GetType().GetField(camera ? "cogAcqFifoEditV21" : "cogToolBlockEditV21", Private).GetValue(form);
            var suspend = editor.GetType().GetProperties().First(p => p.Name == "SuspendElectricRuns" && p.PropertyType == typeof(bool));
            suspend.SetValue(editor, true);
            form.GetType().GetMethod(camera ? "BindCameraSubject" : "BindAlgorithmSubject", Private).Invoke(form, new[] { tool });
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var subject = editor.GetType().GetProperties().First(p => p.Name == "Subject" && p.PropertyType == tool.GetType());
            if (!ReferenceEquals(subject.GetValue(editor), tool) || !(bool)suspend.GetValue(editor) || runs != 0) throw new Exception("Offline editor lost subject or ran its tool.");
            form.GetType().GetMethod(camera ? "ShowCognexEditorFailure" : "ShowAlgorithmEditorFailure", Private).Invoke(form, new object[] { new InvalidOperationException("Simulated disconnected/editor failure") });
            Application.DoEvents();
            if (!form.Visible || form.IsDisposed) throw new Exception("Editor failure closed the form.");
            // A fresh editor can be created after an error, rather than a stale
            // initialized flag keeping an invisible/removed control alive.
            if (!(bool)ensure.Invoke(form, null)) throw new Exception("Editor cannot be retried.");
            Console.WriteLine("PASS " + name + " offline subject binding, guarded failure and retry; no Run/Trigger/Live calls");
            form.Close();
        }
    }

    static void CheckMissingEditor(Assembly app, bool camera)
    {
        string mode = camera ? "Cognex" : "LmiSdk";
        string xml = "<AppConfig><Models><Model Name=\"Job1\"><Cameras><Camera Name=\"Cam4\"><ConnectionMode>" + mode + "</ConnectionMode><Positions><Position Name=\"Pos1\"><General><MainUsed>1</MainUsed><MainChannel>0</MainChannel><SecondUsed>0</SecondUsed></General></Position></Positions></Camera></Cameras></Model></Models></AppConfig>";
        var serializer = new System.Xml.Serialization.XmlSerializer(app.GetType("Aron_V2.AppConfig"));
        object config; using (var reader = new StringReader(xml)) config = serializer.Deserialize(reader);
        app.GetType("Aron_V2.Global").GetField("Vpp_Root").SetValue(null, Path.Combine(Path.GetTempPath(), "Missing-Vpp-" + Guid.NewGuid().ToString("N")));
        using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2." + (camera ? "Camera" : "Algorithm")), config))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var host = (Control)form.GetType().GetField(camera ? "_cognexHost" : "_editorHost", Private).GetValue(form);
            if (!form.Visible || !host.Controls.OfType<TextBox>().Any(t => t.Text.Contains("VPP file not found"))) throw new Exception("Missing VPP did not remain visible as an inline error.");
            Console.WriteLine("PASS " + (camera ? "Camera" : "SDK algorithm") + " missing VPP leaves form open, shows file path and never starts communication");
            form.Close();
        }
    }
}

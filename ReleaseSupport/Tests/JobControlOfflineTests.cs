using Aron_V2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class JobControlOfflineTests
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    sealed class Designer : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
    static object Call(object instance, string name) => instance.GetType().GetMethod(name, All).Invoke(instance, null);
    static void Set(object instance, string field, object value) => instance.GetType().GetField(field, All).SetValue(instance, value);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static IEnumerable<Button> Buttons(Control control)
    { foreach (Control child in control.Controls) { if (child is Button) yield return (Button)child; foreach (var b in Buttons(child)) yield return b; } }
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string path = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; };
        try { Run(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new Designer();
        using (var main = new FormMain())
        {
            Func<string> reason = () => (string)Call(main, "JobControlBlockReason");
            Set(main, "_runOnline", true); Set(main, "_licenseOk", true); Set(main, "_cameraValidationComplete", true);
            Check(reason() == null, "Startup Online request without a communication service is effectively Offline and allows editing");
            var fake = (IVisionCommunicationService)Activator.CreateInstance(typeof(ManualSimulationTests).GetNestedType("FakeService", All), true);
            Set(main, "_communicationService", fake);
            Check(reason().Contains("system is Online"), "Real Online state still blocks catalog edits");
            Set(main, "_runOnline", false); Check(reason() == null, "Operator Offline allows editing with an existing service");
            Set(main, "_runOnline", true); Set(main, "_licenseOk", false);
            Check(reason() == null, "License-disabled Offline state does not get mistaken for Online");
            Set(main, "_licenseOk", true); Set(main, "_cameraValidationComplete", false);
            Check(reason() == null, "Camera-not-ready Offline state permits configuration without starting camera recovery");
            var gates = (SemaphoreSlim[])main.GetType().GetField("_chGates", All).GetValue(main);
            gates[2].Wait(); try { Check(reason().Contains("CH2"), "Busy-channel message identifies the actual channel"); } finally { gates[2].Release(); }
            Set(main, "_cameraRecovering", 1); Check(reason().Contains("Camera recovery"), "Camera recovery has a distinct blocking reason"); Set(main, "_cameraRecovering", 0);
            Set(main, "_switchingOnline", true); Check(reason().Contains("switching"), "Online transition remains protected"); Set(main, "_switchingOnline", false);
            Set(main, "_cameraRecoveryStopping", true); Check(reason().Contains("stopping"), "Shutdown remains protected"); Set(main, "_cameraRecoveryStopping", false);
            var assembly = typeof(FormMain).Assembly;
            var language = assembly.GetType("Aron_V2.UiLanguage");
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "zh-CN" });
            Set(main, "_cameraRecovering", 1); Check(reason().Contains("恢复相机"), "Blocking reasons follow application language"); Set(main, "_cameraRecovering", 0);
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "en" });
            // Reproduce the reported condition through the real Create button.
            Set(main, "_communicationService", null); Set(main, "_cameraValidationComplete", true);
            string directory = Path.Combine(Path.GetTempPath(), "JobControlOffline-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Parameter.xml");
            XmlConfigHelper.Save(new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", Cameras = new List<CameraConfig>() } } }, path);
            var catalog = Activator.CreateInstance(assembly.GetType("Aron_V2.JobCatalog"), All, null, new object[] { path, Path.Combine(directory, "VPP"), null, null }, null);
            var type = assembly.GetType("Aron_V2.JobControlForm");
            using (var form = (Form)Activator.CreateInstance(type, All, null, new object[] { catalog, new Func<bool>(() => reason() == null), new Func<string, bool>(_ => false) }, null))
            {
                type.GetProperty("BlockReason", All).SetValue(form, reason);
                form.Show(); Application.DoEvents();
                form.Controls.Find("NewJobName", true).Single().Text = "Job2";
                Buttons(form).Single(b => b.Text == "Create empty Job").PerformClick();
                for (int i = 0; i < 200 && (bool)type.GetProperty("IsBusy", All).GetValue(form); i++) { Application.DoEvents(); Thread.Sleep(10); }
                Check(XmlConfigHelper.Load(path).Models.Any(m => m.Name == "Job2"), "Actual Create empty Job succeeds with startup _runOnline=true and no service");
                form.Close();
            }
            var calls = fake.GetType().GetField("Calls", All).GetValue(fake);
            Check(!((System.Collections.IEnumerable)calls).Cast<object>().Any(), "Checks and Job creation perform zero PLC calls");
            Check((bool)main.GetType().GetField("_runOnline", All).GetValue(main), "Fix does not mutate the existing Online request or communication behavior");
        }
    }
}

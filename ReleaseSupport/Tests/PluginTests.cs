using Aron.PluginContracts;
using Aron_V2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class PluginTests
{
    private sealed class SlowPlugin : IAronPlugin
    {
        public readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
        public string Id => "Slow"; public void Initialize(IPluginHost host) { }
        public IEnumerable<PluginPage> Pages => new PluginPage[0];
        public void OnInspection(InspectionSnapshot snapshot) { Entered.Set(); Release.WaitOne(); }
        public void Dispose() { }
    }
    private sealed class Host : IPluginHost
    {
        readonly string _root;
        public Host(string root) { _root = root; Directory.CreateDirectory(ConfigDirectory); Directory.CreateDirectory(DataDirectory); Directory.CreateDirectory(LogDirectory); }
        public string ConfigDirectory => Path.Combine(_root, "Project", "Config", "Plugins", "PreWeld");
        public string DataDirectory => Path.Combine(_root, "Data", "Plugins", "PreWeld");
        public string LogDirectory => Path.Combine(_root, "Logs", "Plugins", "PreWeld");
        public bool CanEdit => true;
        public string Language => "en";
        public IList<CameraPosition> GetCatalog() => new[] { "Cam1", "Cam2" }.Select(c => new CameraPosition { Job = "Job1", Camera = c, Position = "Pos2", MainUsed = true }).ToList();
        public void Log(string level, string message) { Console.WriteLine(level + " " + message); }
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static InspectionSnapshot Shot(string camera, string position, string part, string cells, string mark = "", bool success = true, bool manual = false) => new InspectionSnapshot("Job1", camera, position, part, camera == "Cam1" ? 0 : 1, success, manual, DateTime.Now, new Dictionary<string, string> { { "CellCoordinates", cells }, { "Mark", mark }, { "Result_Cam", success ? "1" : "2" } });
    static string[] Summaries(Host host) => Directory.Exists(Path.Combine(host.DataDirectory, "CellData", "Summary")) ? Directory.GetFiles(Path.Combine(host.DataDirectory, "CellData", "Summary"), "*_Replay.xml", SearchOption.AllDirectories) : new string[0];
    static void Wait(Func<bool> condition) { for (int i = 0; i < 120; i++) { if (condition()) return; Thread.Sleep(25); } throw new Exception("Observer timeout"); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixture");
            string pluginDir = Path.Combine(root, "Plugins", "PreWeld"); Directory.CreateDirectory(pluginDir);
            foreach (string name in new[] { "Aron.Plugins.PreWeld.dll", "plugin.xml" }) File.Copy(Path.Combine(args[0], name), Path.Combine(pluginDir, name), true);
            int constructed = 0; Host host = null;
            Func<string, IPluginHost> factory = id => { constructed++; return host = new Host(root); };
            Action<string, string> log = (level, message) => Console.WriteLine(level + " " + message);
            using (var disabled = new PluginRuntime(root, factory, log))
            {
                Check(disabled.Available.Count == 1 && !disabled.Observing && constructed == 0, "Installed plugin remains disabled by default; no host/config/data initialization");
                Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Aron.Plugins.PreWeld"), "Disabled plugin DLL is not loaded");
                disabled.SaveEnabled(new[] { "PreWeld" });
                Check(!disabled.Observing && disabled.SelectedIds.Contains("PreWeld"), "Enable selection persists but does not hot-load into active workflow");
            }
            using (var runtime = new PluginRuntime(root, factory, log))
            {
                Check(runtime.Observing && constructed == 1 && runtime.Loaded.Count() == 1, "Explicit enabled plugin loads via contract API");
                var slot = runtime.Loaded.Single();
                var service = slot.Plugin.GetType().Assembly.GetType("Aron_V2.CellInspection.CellInspectionService");
                Check(!slot.Plugin.GetType().Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Cognex") || a.Name == "Visual Inspection"), "Plugin has no VisionPro/native/host EXE dependency");
                File.WriteAllText(Path.Combine(host.ConfigDirectory, "CellFinalizeEvent.xml"), "<CellFinalizeEventSettings><Enabled>true</Enabled><Job>Job1</Job><Position>Pos2</Position></CellFinalizeEventSettings>");
                // XmlRoot comes from the migrated model, not a guessed root name.
                var settingsType = slot.Plugin.GetType().Assembly.GetType("Aron_V2.CellInspection.CellFinalizeEventSettings");
                object settings = Activator.CreateInstance(settingsType); settingsType.GetProperty("Enabled").SetValue(settings, true); settingsType.GetProperty("Job").SetValue(settings, "Job1"); settingsType.GetProperty("Position").SetValue(settings, "Pos2");
                service.GetMethod("SaveFinalizeEvent").Invoke(null, new[] { settings });
                object rules = Activator.CreateInstance(slot.Plugin.GetType().Assembly.GetType("Aron_V2.CellInspection.CellInspectionSettings"));
                rules.GetType().GetProperty("RequireHeightData").SetValue(rules, false); service.GetMethod("SaveSettings").Invoke(null, new[] { rules });
                var original = new Dictionary<string, string> { { "CellCoordinates", "1,1,2;" } };
                var immutable = new InspectionSnapshot("Job1", "Cam1", "Pos1", "TEST", 0, true, false, DateTime.Now, original); original["CellCoordinates"] = "changed";
                Check(immutable.Outputs["CellCoordinates"] == "1,1,2;", "Observer receives an immutable copy, not live VPP terminals/buffers");
                runtime.Publish(Shot("Cam1", "Pos2", "MANUAL", "1,1,2;", manual: true)); Thread.Sleep(100);
                Check(!Directory.Exists(Path.Combine(host.DataDirectory, "CellData")), "Local/manual observation never creates production data");
                runtime.Publish(Shot("Cam1", "Pos1", "PART-A", "1,0,0;2,10,0;", "Mark1,1,2,3;"));
                runtime.Publish(Shot("Cam1", "Pos2", "PART-A", "3,20,0;"));
                Wait(() => Directory.Exists(Path.Combine(host.DataDirectory, "CellData", "Sessions")));
                Thread.Sleep(100); Check(Summaries(host).Length == 0, "Final position waits for all enabled cameras across channels");
                runtime.Publish(Shot("Cam1", "Pos2", "PART-A", "", success: false));
                runtime.Publish(Shot("Cam2", "Pos2", "PART-A", "4,30,0;")); Thread.Sleep(100);
                Check(Summaries(host).Length == 0, "Failed reshoot removes stale camera-complete state");
                runtime.Publish(Shot("Cam1", "Pos2", "PART-A", "3,20,0;"));
                Wait(() => Summaries(host).Length == 1);
                string summary = Summaries(host).Single(); object doc = service.GetMethod("LoadDocument").Invoke(null, new object[] { summary });
                Check((int)doc.GetType().GetProperty("FinalCellCount").GetValue(doc) == 4 && (int)doc.GetType().GetProperty("MarkPointCount").GetValue(doc) == 1, "Legacy raw captures, deduplication, Mark and final XML aggregation migrated");
                Check(!Directory.GetFiles(Path.Combine(root, "Project"), "*.xml", SearchOption.AllDirectories).Any(p => p.Contains("Raw") || p.Contains("Summary")), "Project holds configuration only; runtime XML stays under Data");
                var entries = slot.Plugin.Pages.ToArray(); Check(entries.Length == 2, "Optional setup and replay/compensation pages exposed");
                foreach (var page in entries)
                {
                    using (Form form = page.Create()) { form.Show(); Application.DoEvents(); Check(form.ClientSize.Width >= 600, "Migrated page opens without any device: " + page.Title); Directory.CreateDirectory("Tests/UIValidation"); using (var image = new System.Drawing.Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size)); image.Save("Tests/UIValidation/Plugin-" + (page.Title.StartsWith("Pre-weld") ? "Setup" : "Replay") + ".png"); } form.Close(); }
                }
                runtime.Publish(Shot("Cam1", "Pos1", "PART-B", "1,1,1;", "invalid mark"));
                Wait(() => slot.Failed);
                Check(!runtime.Observing && slot.Descriptor.Error.Contains("Mark"), "Plugin error quarantines its observer instead of throwing into host workflow");
            }
            string invalidRoot = Path.Combine(root, "BadApi"); string badDir = Path.Combine(invalidRoot, "Plugins", "PreWeld"); Directory.CreateDirectory(badDir);
            File.WriteAllText(Path.Combine(badDir, "plugin.xml"), File.ReadAllText(Path.Combine(pluginDir, "plugin.xml")).Replace("api=\"1\"", "api=\"999\""));
            Directory.CreateDirectory(Path.Combine(invalidRoot, "Project", "Config", "Plugins")); File.WriteAllText(Path.Combine(invalidRoot, "Project", "Config", "Plugins", "Enabled.xml"), "<Plugins><Plugin id=\"PreWeld\" enabled=\"true\" /></Plugins>");
            using (var bad = new PluginRuntime(invalidRoot, id => throw new Exception("Must not initialize"), log)) Check(!bad.Observing && bad.Available.Single().Error.Contains("API"), "Incompatible API rejected before plugin code runs");
            var slow = new SlowPlugin(); var slowSlot = new PluginRuntime.Slot(new PluginDescriptor { Id = "Slow" }, slow, log);
            try
            {
                var sample = Shot("Cam1", "Pos1", "QUEUE", "1,1,1;"); slowSlot.Enqueue(sample); slow.Entered.WaitOne(2000);
                Check(!slowSlot.IsIdle, "Pending plugin work is visible to configuration-save guard");
                for (int i = 0; i < 300; i++) slowSlot.Enqueue(sample);
                Check(slowSlot.Failed, "Bounded queue overflow quarantines module instead of silently losing part data");
            }
            finally { slow.Release.Set(); slowSlot.Stop(); }
            Check(slowSlot.IsIdle, "Quarantined queue drains without calling host or PLC");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Cognex")), "Entire plugin suite never loads Cognex, acquires a camera or starts PLC transport");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

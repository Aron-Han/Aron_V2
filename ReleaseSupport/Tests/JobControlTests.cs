using Aron_V2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class JobControlTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, All).Invoke(obj, args);
    static object Field(object obj, string name)
    {
        if (obj == null) throw new Exception("Null target for " + name);
        var field = obj.GetType().GetField(name, All);
        if (field == null) throw new Exception("Missing field " + obj.GetType().FullName + "." + name);
        return field.GetValue(obj);
    }
    static void Pump() { for (int i = 0; i < 20; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Reject(Action action, string text) { try { action(); } catch (TargetInvocationException) { Check(true, text); return; } throw new Exception(text); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string deps = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(deps, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
            Application.EnableVisualStyles();
            Run(args[1]); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Run(string screenshots)
    {
        string folder = Path.Combine(Path.GetTempPath(), "AronJobCatalog-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Parameter.xml"), root = Path.Combine(folder, "VPP"), outputs = Path.Combine(folder, "VPPOutput.xml");
        var config = new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", General = new GeneralConfig { CamN = "2", maxLines_Richbox = "500" }, Cameras = new List<CameraConfig> {
            new CameraConfig { Name = "Cam1", SerialNumber = "ordinary", Positions = Positions() },
            new CameraConfig { Name = "Cam2", ConnectionMode = "LmiGocator2640", SdkCategory = "SDK-3D", IpAddress = "192.168.1.10", SerialNumber = "lmi", Positions = Positions() }
        } } } };
        // Use the actual registered LMI provider identifier, without loading its SDK.
        config.Models[0].Cameras[1].ConnectionMode = "LMI.GoSDK";
        var acquisition = typeof(FormMain).Assembly.GetType("Aron_V2.CameraIntegration.CameraAcquisition");
        var providers = (System.Collections.IEnumerable)acquisition.GetField("Providers", All)?.GetValue(null);
        if (providers == null) providers = (System.Collections.IEnumerable)acquisition.GetProperty("Providers", All).GetValue(null);
        foreach (var provider in providers) { config.Models[0].Cameras[1].ConnectionMode = (string)provider.GetType().GetField("Id", All)?.GetValue(provider) ?? (string)provider.GetType().GetProperty("Id", All).GetValue(provider); break; }
        XmlConfigHelper.Save(config, path);
        XDocument.Parse("<VppOutputConfig><Jobs><Job Name='Job1'><Cameras><Camera Name='Cam1'><VPPOutput><Item Name='Result' Start='8'/></VPPOutput></Camera></Cameras></Job></Jobs></VppOutputConfig>").Save(outputs);
        var catalogType = typeof(FormMain).Assembly.GetType("Aron_V2.JobCatalog");
        Action<string, bool> blank = (p, camera) => File.WriteAllText(p, camera ? "blank-camera" : "blank-inspection");
        object catalog = Activator.CreateInstance(catalogType, All, null, new object[] { path, root, blank, outputs }, null);
        foreach (var camera in config.Models[0].Cameras)
        foreach (var pos in camera.Positions)
        {
            string directory = Path.Combine(root, "Job1", camera.Name, pos.Name); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Inspection.vpp"), camera.Name + pos.Name + " inspection bytes");
            if (camera.Name == "Cam1") File.WriteAllText(Path.Combine(directory, "Camera.vpp"), pos.Name + " camera bytes");
        }
        Call(catalog, "CreateJob", "Job2", "Job1");
        var clone = XmlConfigHelper.Load(path).Models.Single(m => m.Name == "Job2");
        Check(clone.Cameras[1].IpAddress == "192.168.1.10" && clone.Cameras[0].Positions[1].Parameters[0].Value == "15", "Clone includes SDK bindings, all positions and Limit values");
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Job1"), "*.vpp", SearchOption.AllDirectories))
            Check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(file.Replace("Job1", "Job2"))), "Clone preserves exact VPP bytes: " + Path.GetFileName(file));
        Check(!File.Exists(Path.Combine(root, "Job2", "Cam2", "Pos1", "Camera.vpp")), "SDK clone requires no Cognex camera tool");
        var jobsXml = XDocument.Load(outputs).Root.Element("Jobs");
        Check(XNode.DeepEquals(jobsXml.Elements().First().Element("Cameras"), jobsXml.Elements().Last().Element("Cameras")), "New Job inherits identical output offsets/types without rewriting source mapping");
        string sentinel = Path.Combine(root, "Job2", "Cam1", "Pos1", "Inspection.vpp"); byte[] original = File.ReadAllBytes(sentinel);
        Call(catalog, "DeleteJob", "Job2"); Check(File.ReadAllBytes(sentinel).SequenceEqual(original), "Deleting Job retains VPP files");
        Reject(() => Call(catalog, "CreateJob", "Job2", "Job1"), "Clone refuses retained destination files");
        Call(catalog, "CreateJob", "Job2", null); Check(XmlConfigHelper.Load(path).Models.Last().Cameras.Count == 0, "Empty Job is independent, with no inherited camera or Limit values");
        var fresh = new CameraConfig { Name = "Cam1" }; Call(catalog, "AddCamera", "Job2", fresh);
        Check(File.ReadAllBytes(sentinel).SequenceEqual(original), "Re-adding a camera preserves retained VPP");
        var added = XmlConfigHelper.Load(path).Models.Last().Cameras.Single();
        Check(added.Positions[0].General.MainUsed == "0", "New camera has Pos1 but does not silently enable production");
        Call(catalog, "DeleteCamera", "Job2", "Cam1"); Check(File.Exists(sentinel), "Deleting camera retains VPP files");
        Call(catalog, "AddCamera", "Job2", fresh);
        Reject(() => Call(catalog, "CreateJob", "../bad", null), "Unsafe/non-numeric Job name rejected");
        string missing = Path.Combine(root, "Job1", "Cam1", "Pos2", "Camera.vpp"); File.Move(missing, missing + ".held");
        Reject(() => Call(catalog, "CreateJob", "Job3", "Job1"), "Incomplete source clone fails rather than creating a partial Job");
        Check(!Directory.Exists(Path.Combine(root, "Job3")) && !XmlConfigHelper.Load(path).Models.Any(m => m.Name == "Job3") && Directory.GetDirectories(root, ".job-clone-*").Length == 0, "Failed clone leaves no committed Job or staging directory");
        File.Move(missing + ".held", missing);
        Call(catalog, "CreateJob", "Job3", "Job1");
        byte[] oldParameter = File.ReadAllBytes(path), oldOutput = File.ReadAllBytes(outputs);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject(() => Call(catalog, "CreateJob", "Job6", "Job1"), "Blocked configuration commit aborts the clone");
        Check(File.ReadAllBytes(path).SequenceEqual(oldParameter) && File.ReadAllBytes(outputs).SequenceEqual(oldOutput) && !Directory.Exists(Path.Combine(root, "Job6")), "Commit failure restores output mapping and removes only the new clone directory");
        Global.ParameterCogfig = path; Global.Vpp_Root = root; Global.VppOutputCfgPath = outputs;
        XmlConfigHelper.Load_Job(XmlConfigHelper.Load(path), "Job3");
        Check(Global.CamGeneral.ContainsKey("Job1.Cam1.Pos1") && Global.CamGeneral.ContainsKey("Job3.Cam1.Pos2"), "Runtime lookup retains recipes used by other channels");
        UiChecks(catalog, screenshots);
        using (var output = new Out_put_Parameters())
        {
            output.Show(); Pump();
            var rows = (System.Collections.IList)Field(output, "_rows");
            object row = rows[0]; row.GetType().GetProperty("Source").SetValue(row, "unsaved-draft");
            Call(catalog, "CreateJob", "Job6", "Job1"); Call(output, "RefreshCatalogEntries");
            Check(((ComboBox)Field(output, "CboJob")).Items.Contains("Job6") && ReferenceEquals(row, rows[0]) && (string)row.GetType().GetProperty("Source").GetValue(row) == "unsaved-draft", "Open output page updates Job selector without discarding the current draft");
            Call(catalog, "DeleteJob", "Job6"); Call(output, "RefreshCatalogEntries");
            Check(!((ComboBox)Field(output, "CboJob")).Items.Contains("Job6"), "Deleted Job disappears from output settings too");
        }
        Console.WriteLine("Isolated fixture: " + folder);
    }
    static List<PositionConfig> Positions() => new[] { "Pos1", "Pos2" }.Select(p => new PositionConfig { Name = p,
        General = new PositionGeneralConfig { Exposure = "10", MainUsed = "1", MainChannel = "0", SecondUsed = "1", SecondChannel = "1" },
        Parameters = new List<ParameterConfig> { new ParameterConfig { Name = "Limit_Max_X", Description = "upper", Value = "15" } } }).ToList();
    static void UiChecks(object catalog, string screenshots)
    {
        var app = typeof(FormMain).Assembly; Type jobType = app.GetType("Aron_V2.JobControlForm");
        var centerType = app.GetType("Aron_V2.SettingsCenterForm"); var pageType = centerType.GetNestedType("Page", All);
        Array pages = Array.CreateInstance(pageType, 3); Form center = null;
        for (int i = 0; i < 3; i++)
        {
            int index = i; object p = Activator.CreateInstance(pageType, true);
            pageType.GetField("Title", All).SetValue(p, new[] { "Job Control", "Limit Setting", "General Camera" }[i]);
            pageType.GetField("Icon", All).SetValue(p, Enum.ToObject(pageType.GetField("Icon", All).FieldType, 0));
            pageType.GetField("SaveButtonName", All).SetValue(p, i == 0 ? null : "btnSave");
            pageType.GetField("Revision", All).SetValue(p, new Func<string>(() => new FileInfo(Global.ParameterCogfig).LastWriteTimeUtc.Ticks.ToString()));
            pageType.GetField("Create", All).SetValue(p, new Func<Form>(() => {
                if (index == 1) return new Parameters_Config();
                if (index == 2) return new FormCamGeneralConfig(XmlConfigHelper.Load(Global.ParameterCogfig), Global.ParameterCogfig);
                var form = (Form)Activator.CreateInstance(jobType, All, null, new object[] { catalog, new Func<bool>(() => true), new Func<string, bool>(_ => false) }, null);
                jobType.GetEvent("ConfigSaved", All).GetAddMethod(true).Invoke(form, new object[] { new EventHandler((s, e) => Call(center, "MarkSaved")) }); return form;
            })); pages.SetValue(p, i);
        }
        using (center = (Form)Activator.CreateInstance(centerType, All, null, new object[] { pages, "Job Control" }, null))
        {
            center.Show(); Pump();
            var job = (Form)Field(center, "_page");
            Check(((ComboBox)Field(job, "_jobs")).Items.Count == 3, "Job Control loads all jobs immediately");
            Directory.CreateDirectory(screenshots);
            using (var image = new Bitmap(center.Width, center.Height)) { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(screenshots, "JobControl.png")); }
            Call(center, "SelectPage", 1); Pump(); var oldLimit = (Form)Field(center, "_page");
            Check(!((Button)Field(oldLimit, "btnAddJob")).Visible && !((Button)Field(oldLimit, "btnDeleteJob")).Visible, "Limit has no Job add/delete entry");
            Call(center, "SelectPage", 2); Pump(); var oldGeneral = (Form)Field(center, "_page");
            Check(!((Button)Field(oldGeneral, "btnAddCam")).Visible && !((Button)Field(oldGeneral, "btnDeleteCam")).Visible, "General Camera has no camera add/delete entry");
            Call(center, "SelectPage", 0); Pump(); job = (Form)Field(center, "_page");
            Call(catalog, "CreateJob", "Job4", "Job1"); Call(job, "Committed", "Job4"); Pump();
            Call(center, "SelectPage", 1); Pump(); var limit = (Form)Field(center, "_page");
            Check(oldLimit.IsDisposed && ((ComboBox)Field(limit, "comboJob")).Items.Contains("Job4"), "Catalog commit invalidates old Limit snapshot; next selection has new Job without restart");
            Call(center, "SelectPage", 2); Pump(); var general = (Form)Field(center, "_page");
            Check(oldGeneral.IsDisposed && ((ComboBox)Field(general, "CboJob")).Items.Contains("Job4"), "General Camera refreshes from same committed catalog");
            Call(center, "SelectPage", 0); Pump();
            job = (Form)Field(center, "_page");
            ((TextBox)Field(job, "_name")).Text = "Job5";
            var cloneButton = FindButtons(job).Single(b => b.Text == "Clone Job");
            cloneButton.PerformClick();
            for (int i = 0; i < 100 && (bool)jobType.GetProperty("IsBusy", All).GetValue(job); i++) Pump();
            Check(XmlConfigHelper.Load(Global.ParameterCogfig).Models.Any(m => m.Name == "Job5") && !(bool)Field(center, "_dirty"), "Actual asynchronous Clone button saves and notifies the settings center");
            var lang = app.GetType("Aron_V2.UiLanguage"); lang.GetMethod("SetLanguage", All).Invoke(null, new object[] { "zh-CN" }); Pump();
            using (var image = new Bitmap(center.Width, center.Height)) { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(screenshots, "JobControl-Chinese.png")); }
            Check(((Label)Field(center, "_heading")).Text == "作业管理", "New page participates in application language switching");
            lang.GetMethod("SetLanguage", All).Invoke(null, new object[] { "en" }); center.Close();
        }
        LicenseManager.CurrentContext = new DesignerContext();
        using (var main = new FormMain())
        {
            Call(main, "ApplyCatalogConfiguration");
            Check(main.camViews.Count == 2 && Global.CamGeneral.ContainsKey("Job4.Cam2.Pos2"), "Safe main refresh adds display slots and all-recipe lookups without application startup");
            object first = main.camViews[0]; Call(main, "ApplyCatalogConfiguration");
            Check(ReferenceEquals(first, main.camViews[0]), "Repeated refresh preserves existing camera controls and counters");
            Call(main, "UpdateUI", "JobID0", "Job2");
            Check(true, "Different camera counts and removed slots do not crash Job display refresh");
            var config = XmlConfigHelper.Load(Global.ParameterCogfig);
            foreach (var model in config.Models) model.Cameras.RemoveAll(c => c.Name == "Cam2");
            XmlConfigHelper.Save(config, Global.ParameterCogfig); Call(main, "ApplyCatalogConfiguration");
            var table = (TableLayoutPanel)Field(main, "tableLayoutPanel5");
            Check(table.Controls.Count == 1 && main.camViews.Count == 2 && ReferenceEquals(first, main.camViews[0]), "Deleted slots disappear from layout without renumbering or replacing existing controls");
        }
    }
    static IEnumerable<Button> FindButtons(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button) yield return (Button)child;
            foreach (var button in FindButtons(child)) yield return button;
        }
    }
}

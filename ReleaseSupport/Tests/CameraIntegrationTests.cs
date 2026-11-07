using Aron_V2;
using Aron_V2.CameraIntegration;
using Cognex.VisionPro;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class CameraIntegrationTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static CameraConfig Sensor(string name, string ip) { return new CameraConfig { Name = name, ConnectionMode = CameraAcquisition.Lmi, IpAddress = ip, SerialNumber = "12345", CaptureTimeoutMs = 1000 }; }
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    sealed class FakeDevice : ISdkCameraDevice
    {
        readonly string _ip;
        internal static int Inside, Peak, Created, Disposed;
        internal static bool Fail;
        internal FakeDevice(CameraConfig config) { _ip = config.IpAddress; Interlocked.Increment(ref Created); }
        public object Capture(int timeout) { throw new InvalidOperationException("Fake capture failure " + _ip); }
        public string Diagnose()
        {
            int inside = Interlocked.Increment(ref Inside), peak;
            do { peak = Peak; if (peak >= inside) break; } while (Interlocked.CompareExchange(ref Peak, inside, peak) != peak);
            try { Thread.Sleep(80); if (Fail) throw new InvalidOperationException("fake connection failure"); return _ip; }
            finally { Interlocked.Decrement(ref Inside); }
        }
        public void Dispose() { Interlocked.Increment(ref Disposed); }
    }
    static AppConfig Project()
    {
        var position = new PositionConfig { Name = "Pos1", General = new PositionGeneralConfig { MainUsed = "1", MainChannel = "0", SecondUsed = "1", SecondChannel = "1", Exposure = "27" }, Parameters = new List<ParameterConfig> { new ParameterConfig { Name = "Limit1", Value = "12" } } };
        return new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", General = new GeneralConfig { CamN = "2" }, Cameras = new List<CameraConfig> {
            new CameraConfig { Name = "Cam1", Positions = new List<PositionConfig> { position } }, new CameraConfig { Name = "Cam2", Positions = new List<PositionConfig>() } } } } };
    }
    static void Configuration(string folder)
    {
        string path = Path.Combine(folder, "config.xml");
        using (var writer = new StreamWriter(path)) writer.Write("<AppConfig><Models><Model Name=\"Job1\"><Cameras><Camera Name=\"Cam1\"><Positions /></Camera></Cameras></Model></Models></AppConfig>");
        Check(!CameraAcquisition.IsSdk(XmlConfigHelper.Load(path).Models[0].Cameras[0]), "Old XML defaults to ordinary Cognex acquisition");
        Check(CameraAcquisition.CategoryOf(Sensor("Cam1", "192.168.1.10")) == CameraAcquisition.Sdk3D, "Legacy LMI binding without a category is classified as SDK-3D");
        XmlConfigHelper.Save(Project(), path);
        CameraAcquisition.SaveBinding(path, "Job1", "Cam1", Sensor("Cam1", "192.168.1.10"));
        var cam = CameraAcquisition.Find(XmlConfigHelper.Load(path), "Job1", "Cam1");
        Check(CameraAcquisition.IsSdk(cam) && cam.IpAddress == "192.168.1.10" && cam.SerialNumber == "12345", "LMI binding persists per camera");
        Check(cam.Positions[0].General.SecondChannel == "1" && cam.Positions[0].General.Exposure == "27" && cam.Positions[0].Parameters[0].Value == "12", "Binding save preserves channels, exposure and limits");
        Check(File.Exists(path + ".camera.bak") && !Directory.GetFiles(folder, "*.tmp").Any(), "Atomic binding save retains backup and no temporary files");
        CameraAcquisition.SaveBinding(path, "Job1", "Cam2", Sensor("Cam2", "192.168.1.11"));
        Check(CameraAcquisition.Find(XmlConfigHelper.Load(path), "Job1", "Cam1").IpAddress == "192.168.1.10", "Second camera save does not overwrite the first");
        string before = File.ReadAllText(path);
        try { CameraAcquisition.SaveBinding(path, "Job1", "Cam2", Sensor("Cam2", "192.168.1.10")); throw new Exception("Duplicate IP accepted"); }
        catch (InvalidOperationException) { Check(File.ReadAllText(path) == before, "Duplicate device IP rejected without changing XML"); }
        Check(CameraAcquisition.Validate(Sensor("Cam1", "not-an-ip")) != null, "Invalid SDK IP rejected");
        var invalid = Sensor("Cam1", "192.168.1.10"); invalid.ConnectionMode = "FutureVendor";
        Check(CameraAcquisition.Validate(invalid) != null, "Unknown vendor cannot silently fall back to Cognex");
        invalid = Sensor("Cam1", "192.168.1.10"); invalid.CaptureTimeoutMs = 0;
        Check(CameraAcquisition.Validate(invalid) != null, "Invalid timeout rejected");
    }
    static void Isolation()
    {
        var original = CameraAcquisition.Providers[0];
        CameraAcquisition.Providers[0] = new SdkCameraProvider(CameraAcquisition.Lmi, "Fake", c => new FakeDevice(c));
        try
        {
            var a = Sensor("Cam1", "192.168.1.20"); var b = Sensor("Cam2", "192.168.1.21");
            var results = new[] { Task.Run(() => CameraAcquisition.Diagnose(a)), Task.Run(() => CameraAcquisition.Diagnose(b)) }; Task.WaitAll(results);
            Check(FakeDevice.Peak == 2 && results[0].Result != results[1].Result && FakeDevice.Created == 2, "Different physical cameras execute concurrently with isolated connections/results");
            FakeDevice.Peak = 0; Task.WaitAll(Task.Run(() => CameraAcquisition.Diagnose(a)), Task.Run(() => CameraAcquisition.Diagnose(a)));
            Check(FakeDevice.Peak == 1 && FakeDevice.Created == 2, "Same physical device is serialized without extra connections");
            int disposed = FakeDevice.Disposed; FakeDevice.Fail = true;
            try { CameraAcquisition.Diagnose(a); } catch (InvalidOperationException) { }
            FakeDevice.Fail = false; CameraAcquisition.Diagnose(a);
            Check(FakeDevice.Disposed == disposed + 1 && FakeDevice.Created == 3, "SDK failure disposes connection and next recovery recreates it");
            a.SerialNumber = "67890"; CameraAcquisition.Diagnose(a);
            Check(FakeDevice.Created == 4, "Changed serial binding cannot reuse an obsolete connection");
        }
        finally { CameraAcquisition.DisposeAll(); CameraAcquisition.Providers[0] = original; }
    }
    static void Layout()
    {
        foreach (float fontSize in new[] { 9F, 12F, 15F })
        using (var form = new Form { ClientSize = new Size(1350, 760), Opacity = 0, ShowInTaskbar = false })
        using (var font = new Font("Segoe UI", fontSize))
        {
            var panel = new CameraBindingPanel(); panel.Bind("Job1", Sensor("Cam1", "192.168.1.20"));
            form.Controls.Add(panel); DesktopTheme.ApplySettings(form); form.Font = font; form.Show(); Application.DoEvents();
            Check(panel.UseSdk && panel.Draft().IpAddress == "192.168.1.20", "SDK panel restores per-camera binding at font " + fontSize);
            var body = (Control)typeof(CameraBindingPanel).GetField("_sdkBody", Private).GetValue(panel);
            Check(body.Visible && body.Right <= body.Parent.ClientSize.Width && body.Bottom <= body.Parent.ClientSize.Height, "SDK settings/preview fit at font " + fontSize);
            panel.Bind("Job1", new CameraConfig { Name = "Cam2" }); Application.DoEvents();
            Check(!panel.UseSdk && !body.Visible && panel.Draft().ConnectionMode == CameraAcquisition.Cognex, "Ordinary camera restores Cognex mode without SDK access");
            panel.Bind("Job1", Sensor("Cam1", "192.168.1.20")); Application.DoEvents();
            Directory.CreateDirectory("Tests/UIValidation");
            if (fontSize == 9) using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Camera-SDK-Binding.png"); }
            form.Close();
        }
        var config = Project(); CameraAcquisition.CopyBinding(Sensor("Cam1", "192.168.1.20"), config.Models[0].Cameras[0]);
        using (var form = new Camera(config))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            Check(!(bool)typeof(Camera).GetField("_cognexInitialized", Private).GetValue(form), "No native editor initialization when no Visionpro position is selected");
            var cameras = (ComboBox)typeof(Camera).GetField("Cbo_Camera", Private).GetValue(form);
            Check(cameras.Items.Count == 1 && cameras.Items[0].ToString() == "Cam2", "Visionpro page excludes all SDK cameras");
            typeof(Camera).GetMethod("ShowCognexEditorFailure", Private).Invoke(form, new object[] { new FileLoadException("Simulated Core initialization failure 0x8007045A") });
            Check(form.Visible, "Native editor failure display leaves the Camera settings shell open");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Camera-SDK-Actual.png"); }
            form.Close();
        }
        config.Models[0].Cameras[0].SdkDirectory = @"D:\LegacyGoSdk\bin\win64";
        foreach (string category in new[] { CameraAcquisition.Sdk3D, CameraAcquisition.Sdk2DLine, CameraAcquisition.Sdk2D })
        using (var form = new SdkCameraForm(config, category))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var cameras = (ComboBox)typeof(SdkCameraForm).GetField("_cameras", Private).GetValue(form);
            Check(cameras.Items.Count == (category == CameraAcquisition.Sdk3D ? 1 : 0), category + " page filters cameras and supports an empty category");
            if (category == CameraAcquisition.Sdk3D)
            {
                Check(cameras.Items[0].ToString() == "Cam1", "SDK-3D page displays only the LMI camera");
                Check(typeof(SdkCameraForm).GetField("_channels", Private) == null, "SDK page removes redundant channel selector");
                var settings = (CameraBindingPanel)typeof(SdkCameraForm).GetField("_settings", Private).GetValue(form);
                var provider = (ComboBox)typeof(CameraBindingPanel).GetField("_provider", Private).GetValue(settings);
                var directory = (TextBox)typeof(CameraBindingPanel).GetField("_directory", Private).GetValue(settings);
                Check(provider.Parent == null && directory.Parent == null, "SDK page does not display provider/directory editors");
                Check(settings.Draft().ConnectionMode == CameraAcquisition.Lmi && settings.Draft().SdkDirectory == @"D:\LegacyGoSdk\bin\win64", "Hidden provider and legacy SDK path survive editing/saving");
                Check(config.Models[0].Cameras[0].Positions[0].General.SecondChannel == "1", "Removing SDK toolbar selector does not change main/second channel configuration");
                settings.Bind("Job1", Sensor("Cam1", "192.168.1.20"));
                Check(string.IsNullOrEmpty(settings.Draft().SdkDirectory), "New camera keeps automatic SDK discovery without a per-camera directory");
                var unknown = Sensor("Cam1", "192.168.1.20"); unknown.ConnectionMode = "FutureVendor";
                settings.Bind("Job1", unknown);
                Check(settings.Draft().ConnectionMode == "FutureVendor" && CameraAcquisition.Validate(settings.Draft()) != null, "Hidden provider cannot silently replace an unsupported vendor with LMI");
                settings.Bind("Job1", config.Models[0].Cameras[0]);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Camera-SDK3D-Page.png"); }
            }
            else Check(!((Button)typeof(SdkCameraForm).GetField("_save", Private).GetValue(form)).Enabled, "Empty SDK category cannot save an invalid binding");
            form.Close();
        }
        using (var dialog = new AddCameraDialog())
        {
            Check(!CameraAcquisition.IsSdk(dialog.CreateCamera("Cam3")), "Add Camera defaults to Visionpro without changing project configuration");
            ((ComboBox)typeof(AddCameraDialog).GetField("_mode", Private).GetValue(dialog)).SelectedIndex = 1;
            var sdk = dialog.CreateCamera("Cam3");
            Check(sdk.ConnectionMode == CameraAcquisition.Lmi && sdk.SdkCategory == CameraAcquisition.Sdk3D, "Add SDK Camera selects LMI and stores its 3D category");
            Check(((ComboBox)typeof(AddCameraDialog).GetField("_provider", Private).GetValue(dialog)).Items.Count == 1, "Only currently supported LMI provider is offered");
            dialog.Opacity = 0; dialog.ShowInTaskbar = false; dialog.Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Add-Camera-Type.png"); }
            dialog.Close();
        }
        using (var form = new FormCamGeneralConfig(config, Path.Combine(Path.GetTempPath(), "unused-camera-layout.xml")))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var mode = (Label)typeof(FormCamGeneralConfig).GetField("_modeLabel", Private).GetValue(form);
            Check(mode.Text == "Mode: SDK", "GeneralConfig Mode follows the selected SDK camera");
            ((ComboBox)typeof(FormCamGeneralConfig).GetField("CboCam", Private).GetValue(form)).SelectedIndex = 1; Application.DoEvents();
            Check(mode.Text == "Mode: Visionpro", "GeneralConfig Mode changes when selecting a Visionpro camera");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/GeneralConfig-Camera-Mode.png"); }
            form.Close();
        }
    }
    [STAThread] static void Main()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Aron-camera-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try { Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException); Application.EnableVisualStyles(); Configuration(folder); Isolation(); Layout(); Console.WriteLine("Camera integration tests passed; no physical camera/PLC contacted."); Environment.ExitCode = 0; }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); Environment.ExitCode = 1; }
        finally { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
}

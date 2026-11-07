using Aron_V2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class BlankVppTests
{
    static MethodInfo ensure;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static IReadOnlyList<string> Ensure(string root, CameraConfig camera, Action<string, bool> writer = null)
    {
        try { return (IReadOnlyList<string>)ensure.Invoke(null, new object[] { root, "Job1", camera, writer }); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }
    static CameraConfig Camera(string mode, params string[] positions)
    {
        return new CameraConfig { Name = "Cam4", ConnectionMode = mode,
            Positions = positions.Select(p => new PositionConfig { Name = p, General = new PositionGeneralConfig { MainUsed = "1", MainChannel = "0", SecondUsed = "1", SecondChannel = "1" } }).ToList() };
    }
    static void Fake(string path, bool camera) { using (var file = new FileStream(path, FileMode.CreateNew)) { byte[] bytes = System.Text.Encoding.UTF8.GetBytes(camera ? "camera fixture" : "inspection fixture"); file.Write(bytes, 0, bytes.Length); } }
    static string Hash(string path) { using (var file = File.OpenRead(path)) using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(file)); }
    static object Load(string path)
    {
        var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
        return core.GetType("Cognex.VisionPro.CogSerializer").GetMethod("LoadObjectFromFile", new[] { typeof(string) }).Invoke(null, new object[] { path });
    }
    static int Count(object value, string property) { object collection = value.GetType().GetProperty(property).GetValue(value); return (int)collection.GetType().GetProperty("Count").GetValue(collection); }

    static void FileRules(string temporary)
    {
        string sdk = Path.Combine(temporary, "sdk");
        var config = Camera("LmiSdk", "Pos1", "Pos2");
        Check(Ensure(sdk, config, Fake).Count == 2 && !Directory.GetFiles(sdk, "Camera.vpp", SearchOption.AllDirectories).Any(), "SDK creates only Inspection.vpp per position");
        string inspection = Path.Combine(sdk, "Job1", "Cam4", "Pos1", "Inspection.vpp");
        string hash = Hash(inspection); DateTime timestamp = File.GetLastWriteTimeUtc(inspection);
        Check(Ensure(sdk, config, (p, c) => { throw new Exception("Must not serialize an existing file"); }).Count == 0 && Hash(inspection) == hash && File.GetLastWriteTimeUtc(inspection) == timestamp, "Existing VPP bytes and timestamp are untouched; no serializer is called");
        var cameras = new List<CameraConfig> { config }; cameras.Remove(config); cameras.Add(Camera("LmiSdk", "Pos1"));
        Ensure(sdk, cameras[0], Fake);
        Check(Hash(inspection) == hash && File.Exists(Path.Combine(sdk, "Job1", "Cam4", "Pos2", "Inspection.vpp")), "Delete/re-add same camera preserves retained files, including old positions");
        var ordinary = Camera("Cognex", "Pos1");
        Check(Ensure(sdk, ordinary, Fake).Count == 1 && Hash(inspection) == hash, "SDK-to-Visionpro reuse adds only missing Camera.vpp; keeps existing Inspection.vpp");
        string cameraFile = Path.Combine(sdk, "Job1", "Cam4", "Pos1", "Camera.vpp");
        Ensure(sdk, Camera("LmiSdk", "Pos1"), Fake);
        Check(File.Exists(cameraFile), "Switching back to SDK never removes a retained Camera.vpp");

        string normal = Path.Combine(temporary, "normal"); var noPositions = Camera("Cognex");
        Check(Ensure(normal, noPositions, Fake).Count == 2 && noPositions.Positions.Count == 0, "New Visionpro camera gets Pos1 files without changing position/channel configuration");
        noPositions.Positions.Add(new PositionConfig { Name = "Pos2" });
        Check(Ensure(normal, noPositions, Fake).Count == 2, "Saving a new position creates its missing VPP pair");
        string empty = Path.Combine(normal, "Job1", "Cam4", "Pos2", "Inspection.vpp");
        using (var stream = new FileStream(empty, FileMode.Truncate)) { }
        Check(Ensure(normal, noPositions, Fake).Count == 0 && new FileInfo(empty).Length == 0, "Even empty/damaged existing VPPs are preserved, not silently replaced");

        string failure = Path.Combine(temporary, "failure"); bool failed = false;
        try { Ensure(failure, Camera("LmiSdk", "Pos1"), (p, c) => { Fake(p, c); throw new IOException("Simulated save failure"); }); }
        catch (IOException) { failed = true; }
        Check(failed && Directory.GetFiles(failure, "*", SearchOption.AllDirectories).Length == 0, "Failed serialization publishes no partial VPP and cleans only its staging file");
        string race = Path.Combine(temporary, "race");
        var results = Enumerable.Range(0, 8).Select(_ => Task.Run(() => Ensure(race, Camera("LmiSdk", "Pos1"), Fake).Count)).ToArray(); Task.WaitAll(results);
        Check(results.Sum(t => t.Result) == 1 && Directory.GetFiles(race, "*", SearchOption.AllDirectories).Length == 1, "Concurrent creators publish one complete VPP without overwriting or staging residue");
        bool rejected = false; var invalid = Camera("LmiSdk", "..");
        try { Ensure(temporary, invalid, Fake); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Path traversal in Job/Camera/Position is rejected");
    }

    static void NativeFiles(Assembly app, string temporary)
    {
        object[] report = { null };
        Check((bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report), "VisionPro 9.6 SR2 dependency preflight");
        string native = Path.Combine(temporary, "native");
        Ensure(native, Camera("Cognex", "Pos1"));
        foreach (string path in Directory.GetFiles(native, "*.vpp", SearchOption.AllDirectories))
        {
            object tool = Load(path);
            try
            {
                if (Path.GetFileName(path) == "Camera.vpp")
                    Check(tool.GetType().Name == "CogAcqFifoTool" && tool.GetType().GetProperty("Operator").GetValue(tool) == null, "Blank Camera.vpp loads with NO bound acquisition operator");
                else
                    Check(tool.GetType().Name == "CogToolBlock" && Count(tool, "Tools") == 0 && Count(tool, "Inputs") == 0 && Count(tool, "Outputs") == 0, "Blank Inspection.vpp loads with zero tools/inputs/outputs");
            }
            finally { (tool as IDisposable)?.Dispose(); }
        }
    }

    static void ProvisionSite(string configPath, string root)
    {
        var before = Directory.GetFiles(root, "*.vpp", SearchOption.AllDirectories).ToDictionary(p => p, Hash, StringComparer.OrdinalIgnoreCase);
        var config = XmlConfigHelper.Load(configPath);
        var job = config.Models.Single(m => m.Name == "Job1"); var camera = job.Cameras.Single(c => c.Name == "Cam4");
        Check(camera.ConnectionMode == "LmiSdk" && camera.Positions.Any(p => p.Name == "Pos1"), "Site target is configured SDK Job1/Cam4/Pos1");
        foreach (string path in Ensure(root, camera)) Console.WriteLine("CREATED " + path);
        Check(before.All(p => File.Exists(p.Key) && Hash(p.Key) == p.Value), "All pre-existing site VPP hashes are unchanged");
        string target = Path.Combine(root, "Job1", "Cam4", "Pos1", "Inspection.vpp");
        object tool = Load(target); try { Check(tool.GetType().Name == "CogToolBlock", "Site Cam4/Pos1 Inspection.vpp deserializes normally"); } finally { (tool as IDisposable)?.Dispose(); }
        string hash = Hash(target);
        Check(Ensure(root, camera).Count == 0 && Hash(target) == hash, "Repeated site provisioning preserves existing VPP");
    }

    static void UiHooks(string temporary)
    {
        string originalRoot = Global.Vpp_Root;
        try
        {
            Global.Vpp_Root = Path.Combine(temporary, "ui-hooks");
            var camera = Camera("LmiSdk", "Pos1");
            var model = new ModelConfig { Name = "Job1", Cameras = new List<CameraConfig> { camera } };
            var config = new AppConfig { Models = new List<ModelConfig> { model } };
            using (var form = new FormCamGeneralConfig(config, Path.Combine(temporary, "ui-config.xml")))
            {
                var hook = typeof(FormCamGeneralConfig).GetMethod("EnsureCameraVppFiles", BindingFlags.Instance | BindingFlags.NonPublic);
                Check((bool)hook.Invoke(form, new object[] { "Job1", camera }) && File.Exists(Path.Combine(Global.Vpp_Root, "Job1", "Cam4", "Pos1", "Inspection.vpp")), "GeneralConfig add/save hook provisions the SDK position");
                Check(camera.Positions[0].General.MainChannel == "0" && camera.Positions[0].General.SecondChannel == "1", "Provisioning leaves main/second channel configuration unchanged");
            }
            using (var form = new Parameters_Config())
            {
                var ordinary = Camera("Cognex", "Pos1"); ordinary.Name = "Cam5";
                var hook = typeof(Parameters_Config).GetMethod("EnsureNewCameraVppFiles", BindingFlags.Instance | BindingFlags.NonPublic);
                var rows = new[] { new KeyValuePair<string, CameraConfig>("Job1", ordinary) };
                Check((bool)hook.Invoke(form, new object[] { rows }) && File.Exists(Path.Combine(Global.Vpp_Root, "Job1", "Cam5", "Pos1", "Camera.vpp")) && File.Exists(Path.Combine(Global.Vpp_Root, "Job1", "Cam5", "Pos1", "Inspection.vpp")), "Limit Setting new-Job/new-position hook provisions Visionpro files");
            }
        }
        finally { Global.Vpp_Root = originalRoot; }
    }

    [STAThread] static void Main(string[] args)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "Aron-blank-vpp-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        try
        {
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            ensure = app.GetType("Aron_V2.BlankCameraVpp").GetMethod("EnsureForCamera", BindingFlags.Static | BindingFlags.NonPublic);
            FileRules(temporary); NativeFiles(app, temporary);
            UiHooks(temporary);
            if (args.Length == 3) ProvisionSite(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            Console.WriteLine("PASS no tool Run/Trigger/Live, camera SDK connection or PLC startup was invoked.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        finally
        {
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\') + "\\";
            if (Path.GetFullPath(temporary).StartsWith(root, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(temporary).StartsWith("Aron-blank-vpp-")) Directory.Delete(temporary, true);
        }
    }
}

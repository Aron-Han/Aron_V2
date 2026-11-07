using Aron_V2;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

internal static class InspectionContractTests
{
    const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type Contract;
    sealed class Designer : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, All).Invoke(obj, args);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static List<string> Validate(CogToolBlock tool, string[] original = null, string[] required = null) => (List<string>)Contract.GetMethod("Validate", All).Invoke(null, new object[] { tool, original, required });
    static void Save(CogToolBlock tool, string path, string[] original = null, string[] required = null) => Contract.GetMethod("SaveChecked", All).Invoke(null, new object[] { tool, path, original, required });
    static void Reject(Action action, string message) { try { action(); } catch (TargetInvocationException e) { Check(e.InnerException is InvalidOperationException, message); return; } throw new Exception(message); }
    static IEnumerable<ToolBarButton> ToolbarButtons(Control parent)
    {
        if (parent is ToolBar) foreach (ToolBarButton button in ((ToolBar)parent).Buttons) yield return button;
        foreach (Control child in parent.Controls) foreach (var button in ToolbarButtons(child)) yield return button;
    }
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string name = new AssemblyName(e.Name).Name;
            string path = name == "Visual Inspection" ? args[0] : Path.Combine(Path.GetDirectoryName(args[0]), name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try
        {
            var app = Assembly.LoadFrom(args[0]); object[] report = { null };
            if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", All).Invoke(null, report)) throw new Exception((string)report[0]);
            Run(app); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static CogToolBlock Fixture(string result = "Result_Cam", string send = "resultSend", Type image = null, Type data = null, Type sendType = null)
    {
        var tool = new CogToolBlock();
        tool.Inputs.Add(new CogToolBlockTerminal("Image", image ?? typeof(ICogImage)));
        tool.Inputs.Add(new CogToolBlockTerminal("Data", data ?? typeof(string)));
        if (result != null) tool.Outputs.Add(new CogToolBlockTerminal(result, typeof(string)));
        if (send != null) tool.Outputs.Add(new CogToolBlockTerminal(send, sendType ?? typeof(string)));
        return tool;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run(Assembly app)
    {
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new Designer();
        Contract = app.GetType("Aron_V2.InspectionVppContract");
        string root = Path.Combine(Path.GetTempPath(), "InspectionContract-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string file = Path.Combine(root, "Inspection.vpp"); File.WriteAllText(file, "original disk bytes");
        using (var good = Fixture())
        {
            Check(Validate(good).Count == 0, "Valid declared ports pass without any image, result values or algorithm execution");
            using (var missing = Fixture(result: null))
            {
                Check(Validate(missing).Any(e => e.Contains("Result_Cam")), "Missing Result_Cam is identified");
                Reject(() => Save(missing, file), "Missing result port blocks Save");
                Check(File.ReadAllText(file) == "original disk bytes" && !File.Exists(file + ".bak"), "Rejected Save leaves disk and backup unchanged");
                string unused = Path.Combine(root, "new-position", "Inspection.vpp"); Reject(() => Save(missing, unused), "Invalid new algorithm is blocked");
                Check(!Directory.Exists(Path.GetDirectoryName(unused)), "Rejected Save creates no directory or temporary VPP");
            }
            using (var renamed = Fixture(send: "Result_Send")) Check(Validate(renamed).Any(e => e.Contains("resultSend")), "Similar spelling Result_Send cannot replace fixed resultSend port");
            using (var renamed = Fixture(send: "ResultSend")) Check(Validate(renamed).Any(e => e.Contains("resultSend")), "Fixed output names preserve their exact casing");
            using (var wrong = Fixture(image: typeof(string))) Check(Validate(wrong).Any(e => e.Contains("Input type mismatch")), "Wrong image input type is blocked");
            using (var wrong = Fixture(data: typeof(int))) Check(Validate(wrong).Any(e => e.Contains("Input type mismatch")), "Wrong data input type is blocked");
            using (var wrong = Fixture(sendType: typeof(bool))) Check(Validate(wrong).Any(e => e.Contains("Output type mismatch")), "Wrong resultSend type is blocked");
            Check(Validate(good, new[] { "InputImage", "Data" }).Any(e => e.Contains("renamed or reordered")), "Existing fixed input names/order are protected");
            Check(Validate(good, required: new[] { "Result_X" }).Any(e => e.Contains("Result_X")), "Configured required PLC output ports are also checked");
            using (var empty = new CogToolBlock()) Check(Validate(empty).Count == 4, "All missing fixed inputs and outputs are reported together");
            int runs = 0; good.Ran += (s, e) => runs++;
            Save(good, file); Check(File.ReadAllText(file + ".bak") == "original disk bytes", "Successful Save atomically replaces the VPP and preserves its prior version");
            using (var disk = (CogToolBlock)CogSerializer.LoadObjectFromFile(file)) Check(Validate(disk).Count == 0, "Saved fixture loads with its complete interface");
            Check(runs == 0, "Validation and Save never run the ToolBlock");

            Load_Job.VppCache.Set("Job1", 0, "Cam1", "Pos1", null, (CogToolBlock)CogSerializer.DeepCopyObject(good));
            Load_Job.VppCache.Set("Job1", 1, "Cam1", "Pos1", null, (CogToolBlock)CogSerializer.DeepCopyObject(good));
            Load_Job.VppCache.Set("Job1", 2, "Cam2", "Pos1", null, (CogToolBlock)CogSerializer.DeepCopyObject(good));
            var other = Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 2, "Cam2", "Pos1")];
            using (var editor = new Algorithm(new AppConfig { Models = new List<ModelConfig>() }))
            {
                Check((bool)Call(editor, "EnsureAlgorithmEditor"), "Blank algorithm editor opens without camera acquisition");
                var original = Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 0, "Cam1", "Pos1")];
                Call(editor, "BindAlgorithmSubject", original);
                var nativeButtons = ToolbarButtons(editor).ToList();
                Check(nativeButtons.Where(b => (b.Tag as string) == "Save" || (b.Tag as string) == "SaveAs").Any() && nativeButtons.Where(b => (b.Tag as string) == "Save" || (b.Tag as string) == "SaveAs").All(b => !b.Visible), "Unchecked VisionPro Save/SaveAs buttons are hidden after binding");
                Check(nativeButtons.Single(b => (b.Tag as string) == "Run").Visible, "Native algorithm Run control remains available");
                var draft = (CogToolBlock)typeof(Algorithm).GetField("_draft", All).GetValue(editor);
                draft.Outputs.Clear();
                Check(!ReferenceEquals(original, draft) && Validate(original).Count == 0 && Validate(draft).Count > 0, "Deleting draft ports does not mutate the runtime cache");
                Reject(() => Save(draft, file), "Invalid editor draft cannot overwrite saved VPP");
            }
            var config = new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", Cameras = new List<CameraConfig> { new CameraConfig { Name = "Cam1", Positions = new List<PositionConfig> { new PositionConfig { Name = "Pos1", General = new PositionGeneralConfig { MainUsed = "0", SecondUsed = "0" } } } } } } } };
            Global.ParameterCogfig = Path.Combine(root, "Parameter.xml"); XmlConfigHelper.Save(config, Global.ParameterCogfig);
            Global.Vpp_Root = root;
            using (var editor = new Algorithm(config))
            {
                typeof(Algorithm).GetField("_loading", All).SetValue(editor, true);
                foreach (var pair in new[] { new[] { "Cbo_JobID", "Job1" }, new[] { "Cbo_Camera", "Cam1" }, new[] { "Cbo_Position", "Pos1" }, new[] { "Cbo_Channel", "0" } })
                {
                    var selector = (ComboBox)typeof(Algorithm).GetField(pair[0], All).GetValue(editor); selector.Items.Add(pair[1]); selector.SelectedIndex = 0;
                }
                typeof(Algorithm).GetField("_loading", All).SetValue(editor, false);
                Call(editor, "EnsureAlgorithmEditor"); Call(editor, "BindAlgorithmSubject", good);
                var draft = (CogToolBlock)typeof(Algorithm).GetField("_draft", All).GetValue(editor);
                draft.Outputs["resultSend"].Value = "saved draft";
                Call(editor, "SaveInspectionVpp");
                Check(File.Exists(Path.Combine(root, "Job1", "Cam1", "Pos1", "Inspection.vpp")) && (string)Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 1, "Cam1", "Pos1")].Outputs["resultSend"].Value == "saved draft", "Actual application Save path publishes a validated draft to disk and all matching cached channels");
                Check(ReferenceEquals(other, Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 2, "Cam2", "Pos1")]), "Application Save leaves unrelated cameras untouched");
            }
            var updateType = typeof(Load_Job).GetNestedType("InspectionEdit", All);
            using (var abort = (IDisposable)Activator.CreateInstance(updateType, All, null, new object[] { good, "Job1", "Cam1", "Pos1", new[] { 0, 1 } }, null)) { }
            Check(Validate(Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 0, "Cam1", "Pos1")]).Count == 0, "Aborted save leaves existing runtime tools intact");
            using (var update = (IDisposable)Activator.CreateInstance(updateType, All, null, new object[] { good, "Job1", "Cam1", "Pos1", new[] { 0, 1 } }, null)) Call(update, "Commit");
            var main = Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 0, "Cam1", "Pos1")];
            var second = Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 1, "Cam1", "Pos1")];
            Check(!ReferenceEquals(main, second) && !ReferenceEquals(main, good) && ReferenceEquals(other, Load_Job.VppCache.Tb[Load_Job.VppCache.Key("Job1", 2, "Cam2", "Pos1")]), "Main/second channel caches receive independent copies; other cameras are unchanged");
            using (var owner = new FormMain())
            {
                var gates = (SemaphoreSlim[])typeof(FormMain).GetField("_chGates", All).GetValue(owner);
                gates[1].Wait();
                try { Reject(() => Call(owner, "AcquireAlgorithmSaveGates", new[] { 0, 1 }), "Busy inspection channel blocks publication"); }
                finally { gates[1].Release(); }
                Check(gates[0].CurrentCount == 1, "Partial gate acquisition is rolled back on failure");
                using (var lease = (IDisposable)Call(owner, "AcquireAlgorithmSaveGates", new[] { 0, 1 })) Check(gates[0].CurrentCount == 0 && gates[1].CurrentCount == 0, "Only affected channel gates are held during save");
                Check(gates[0].CurrentCount == 1 && gates[1].CurrentCount == 1, "Successful Save releases channel gates");
            }
            foreach (var tool in Load_Job.VppCache.Tb.Values.Distinct()) tool.Dispose(); Load_Job.VppCache.Tb.Clear();
            var language = app.GetType("Aron_V2.UiLanguage");
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "zh-CN" });
            string message = (string)Contract.GetMethod("FailureMessage", All).Invoke(null, new object[] { new[] { "Missing output: Result_Cam", "Missing output: resultSend" } });
            Check(message.Contains("缺少输出引脚") && message.Contains("Result_Cam") && message.Contains("resultSend"), "Chinese failure reminder preserves exact terminal names");
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "en" });
        }
        Console.WriteLine("Fixture: " + root);
    }
}

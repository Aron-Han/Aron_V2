using Aron_V2;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Fake acquisition delegate + fake communication. Never call Cam_Inspection,
// a device SDK, socket Start, real service methods or a VPP loader.
internal static class ManualSimulationTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    sealed class FakeService : IVisionCommunicationService
    {
        internal readonly ConcurrentQueue<string> Calls = new ConcurrentQueue<string>();
        public bool IsStarted { get; set; } = true;
        public event Action<CommunicationChannelInput> InputChanged { add { } remove { } }
        public event Action<int> TriggerRequested { add { } remove { } }
        public event Action<int> ResultAckReceived { add { } remove { } }
        public event Action<string> LogMessage { add { } remove { } }
        public void Start() { throw new Exception("Real transport startup is forbidden in tests"); }
        public void Stop() { Calls.Enqueue("Stop"); }
        public void SetCameraReady(bool ready) { Calls.Enqueue("CameraReady:" + ready); }
        public void SetOnline(bool online) { Calls.Enqueue("Online:" + online); }
        public void NotifyInspectionStart(int c) { Calls.Enqueue("Start:" + c); }
        public void NotifyInspectionEnd(int c) { Calls.Enqueue("End:" + c); }
        public void WriteJobPosEcho(int c, byte job, byte pos) { Calls.Enqueue("Echo:" + c + ":" + job + ":" + pos); }
        public void WriteInspectionResult(int c, float x, float y, float a, short result) { Calls.Enqueue("Result:" + c); }
        public void ClearInspectionResult(int c) { Calls.Enqueue("Ack:" + c); }
        public void ClearAllInspectionResult(int c) { Calls.Enqueue("Clear:" + c); }
        public void ClearExposureComplete(int c) { Calls.Enqueue("Exposure:" + c); }
        public void Dispose() { }
        internal void Reset() { string value; while (Calls.TryDequeue(out value)) { } }
    }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, All).Invoke(target, args); }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, All).SetValue(target, value); }
    static void Pump(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted) { if (watch.ElapsedMilliseconds > 8000) throw new Exception("Simulation test timeout"); Application.DoEvents(); Thread.Sleep(5); }
        task.GetAwaiter().GetResult();
    }
    static AppConfig Fixture()
    {
        var config = new AppConfig { Models = new List<ModelConfig>() };
        foreach (string job in new[] { "Job1", "Job2" })
        {
            var model = new ModelConfig { Name = job, Cameras = new List<CameraConfig>() }; config.Models.Add(model);
            for (int channel = 0; channel < 2; channel++)
            {
                var camera = new CameraConfig { Name = "Cam" + (channel + 1), Positions = new List<PositionConfig>() }; model.Cameras.Add(camera);
                for (int pos = 1; pos <= 2; pos++)
                {
                    camera.Positions.Add(new PositionConfig { Name = "Pos" + pos, General = new PositionGeneralConfig { MainUsed = "1", MainChannel = channel.ToString(), SecondUsed = "0", SecondChannel = channel.ToString() } });
                    Global.CamGeneral[job + "." + camera.Name + ".Pos" + pos] = new CameraGeneralRuntime { MainUsed = "1", MainChannel = channel.ToString() };
                }
            }
        }
        return config;
    }
    static void SendFakeResult(FormMain main, int channel)
    {
        Type itemType = typeof(FormMain).GetNestedType("CommunicationMainResultSnapshot", All);
        var item = Activator.CreateInstance(itemType, true);
        itemType.GetProperty("ResultCode").SetValue(item, (short)1);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType)); list.Add(item);
        Call(main, "SendCommunicationResultAfterMainComplete", channel, list);
    }
    [STAThread] static void Main(string[] args)
    {
        // Resolve normal managed dependencies only; all config paths follow
        // this test EXE's isolated directory, not the production project.
        string bin = Path.GetFullPath(args[0]);
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        { string path = Path.Combine(bin, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(path) ? Assembly.LoadFrom(path) : null; };
        try { Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    static void Run()
    {
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new DesignerContext();
        Check((string)typeof(FormMain).GetMethod("NormalizeInspectionPosition", All).Invoke(null, new object[] { "10" }) == "Pos10" &&
            (string)typeof(FormMain).GetMethod("InspectionPositionNumber", All).Invoke(null, new object[] { "Pos10" }) == "10" &&
            (string)typeof(FormMain).GetMethod("NormalizeInspectionPosition", All).Invoke(null, new object[] { "Pos2" }) == "Pos2", "Multi-digit position IDs stay complete; existing names remain unchanged");
        Global.CurrentConfig = Fixture();
        foreach (int ch in new[] { 0, 1 }) { Global.Model_JobID[ch] = Global.Model_JobID_Send[ch] = "Job1"; Global.Position_ID[ch] = Global.Position_ID_Send[ch] = "1"; }
        using (var main = new FormMain { Opacity = 0, ShowInTaskbar = false })
        {
            var nativeOnline = (Button)typeof(FormMain).GetField("button3", All).GetValue(main);
            Check(nativeOnline.GetType() == typeof(Button), "Designer uses a native Button without loading a custom component");
            Call(main, "InitializeOnlineStateButton");
            var runtimeOnline = (Button)typeof(FormMain).GetField("button3", All).GetValue(main);
            Check(nativeOnline.IsDisposed && runtimeOnline.GetType().Name == "OnlineStateButton", "Runtime installs styled button and disposes only the placeholder");
            main.Show(); Application.DoEvents();
            Check(runtimeOnline.Visible, "Runtime button remains visible when the main window is first shown");
            Check(!((System.Windows.Forms.Timer)typeof(FormMain).GetField("_communicationStatusTimer", All).GetValue(main)).Enabled, "Designer context starts no communication timers or hardware");
            var service = new FakeService(); Set(main, "_communicationService", service); Set(main, "_cameraValidationComplete", true);
            Set(main, "_licenseOk", true); Set(main, "_runOnline", true);
            var onlineButton = (Button)typeof(FormMain).GetField("button3", All).GetValue(main);
            foreach (bool requested in new[] { false, true })
            {
                Call(main, "button3_Click", onlineButton, EventArgs.Empty);
                var toggleWatch = Stopwatch.StartNew();
                while ((bool)typeof(FormMain).GetField("_switchingOnline", All).GetValue(main) ||
                    onlineButton.BackColor != (requested ? Color.Green : Color.Yellow))
                { if (toggleWatch.ElapsedMilliseconds > 8000) throw new Exception("Online switch timeout"); Application.DoEvents(); Thread.Sleep(5); }
                Check(onlineButton.BackColor == (requested ? Color.Green : Color.Yellow), "Operator state color: " + requested);
                Check(service.IsStarted && service.Calls.SequenceEqual(new[] { "Online:" + requested }), "Button only changes Online signal, without Stop/Start: " + requested);
                service.Reset();
            }
            var scope = (AsyncLocal<bool>)typeof(FormMain).GetField("_manualLocalOnly", All).GetValue(null);
            Global.Result_Send = Enumerable.Repeat((byte)77, 240).ToArray();
            byte[] before = (byte[])Global.Result_Send.Clone();
            int inspections = 0;
            Func<string, string, string, int, Task> inspect = async (camera, job, pos, ch) =>
            {
                inspections++;
                Check(camera == (ch + 1).ToString() && job == "Job1" && pos == "1", "Trigger uses current channel camera/job/position snapshot");
                await Task.Run(() => { Check(scope.Value, "Local-only suppression flows into background inspection work"); SendFakeResult(main, ch); });
                main.Trigger_Failed("Cam1", "fake local failure");
            };
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, true, inspect));
            Check(inspections == 1 && !service.Calls.Any() && Global.Result_Send.SequenceEqual(before) && !scope.Value,
                "Local Trigger has zero PLC calls, no shared output mutation and restores scope");
            Check((bool)typeof(FormMain).GetField("_cameraValidationComplete", All).GetValue(main), "Local failure does not schedule PLC recovery or change global camera readiness");
            Func<string, string, string, int, Task> full = (camera, job, pos, ch) => { SendFakeResult(main, ch); return Task.CompletedTask; };
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, false, full));
            Check(service.Calls.SequenceEqual(new[] { "Start:0", "Result:0", "End:0" }), "PLC mode retains Start -> Result -> End order");
            service.Reset(); Set(main, "_runOnline", false);
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, false, full));
            Check(!service.Calls.Any(), "Normal trigger remains blocked Offline");
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, true, inspect));
            Check(!service.Calls.Any(), "Local-only trigger works Offline without PLC writes");
            Set(main, "_runOnline", true);
            var started = new TaskCompletionSource<bool>(); var release = new TaskCompletionSource<bool>();
            Func<string, string, string, int, Task> waiting = async (camera, job, pos, ch) => { started.SetResult(true); await release.Task; SendFakeResult(main, ch); };
            Task local = (Task)Call(main, "ProcessCommunicationTriggerCore", 0, true, waiting); Pump(started.Task);
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 1, false, full)); release.SetResult(true); Pump(local);
            Check(service.Calls.SequenceEqual(new[] { "Start:1", "Result:1", "End:1" }), "Local channel 0 never suppresses simultaneous normal channel 1"); service.Reset();
            Func<string, string, string, int, Task> fail = (camera, job, pos, ch) => { throw new Exception("fake inspection failure"); };
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, true, fail));
            Pump((Task)Call(main, "ProcessCommunicationTriggerCore", 0, false, full));
            Check(!scope.Value && service.Calls.SequenceEqual(new[] { "Start:0", "Result:0", "End:0" }), "Failure restores scope and releases the channel gate"); service.Reset();

            var input = new CommunicationChannelInput { Channel = 0, SetUserData = true, JobId = 1, PosId = 2, PartNo = "SN,\"中文" };
            Call(main, "ProcessCommunicationInputCore", input, true, null);
            Check(Global.Position_ID[0] == "2" && Global.PartCode[0] == input.PartNo && !service.Calls.Any(), "Local Receive Data applies Pos/PartN without PLC echo");
            input.ClearResult = 1; input.JobId = 99; input.PosId = 99; input.PartNo = "must not apply";
            Call(main, "ProcessCommunicationInputCore", input, true, null);
            Check(Global.Position_ID[0] == "2" && Global.PartCode[0] != "must not apply" && !service.Calls.Any() && Global.Result_Send.SequenceEqual(before), "Local clear preserves automatic priority and never changes PLC outputs/buffer");
            Call(main, "ProcessCommunicationInputCore", input, false, null);
            Check(service.Calls.SequenceEqual(new[] { "Clear:0" }), "PLC Clear Result executes original clear action only"); service.Reset();
            input.ClearResult = 0; input.JobId = 2; input.PosId = 1; input.PartNo = "SN2";
            int loads = 0; Action<int, string> load = (ch, job) => { Check(ch == 0 && job == "Job2", "Job change requests original channel/job VPP loading"); loads++; };
            Call(main, "ProcessCommunicationInputCore", input, true, load);
            Check(loads == 1 && Global.Model_JobID[0] == "Job2" && Global.PartCode[0] == "SN2" && !service.Calls.Any(), "Local Job change reuses shared business logic without a real VPP/device load in tests");
            Call(main, "ProcessCommunicationInput", input);
            Check(service.Calls.SequenceEqual(new[] { "Echo:0:2:1" }), "Automatic input wrapper still performs normal Job/Pos PLC echo"); service.Reset();
            input.PosId = 99; Call(main, "ProcessCommunicationInputCore", input, true, null);
            Check(Global.Position_ID[0] == "1" && !service.Calls.Any(), "Invalid position cannot change runtime state");
            input.SetUserData = false; input.InspectionResultAcknowledge = input.ClearExposureComplete = true;
            Call(main, "ProcessCommunicationInputCore", input, true, null);
            Check(!service.Calls.Any(), "Local input cannot acknowledge/clear real PLC outputs");
            Call(main, "ProcessCommunicationInput", input);
            Check(service.Calls.SequenceEqual(new[] { "Exposure:0", "Ack:0" }), "Original automatic acknowledge/exposure-clear calls retained"); service.Reset();
            var requestType = typeof(FormMain).Assembly.GetType("Aron_V2.ManualSimulationRequest");
            var validationRequest = Activator.CreateInstance(requestType, true);
            requestType.GetField("Channel", All).SetValue(validationRequest, 0);
            var selected = new CommunicationConfig { Protocol = CommunicationProtocol.Profinet, ChannelCount = 4 };
            CommunicationConfigHelper.Save(selected); Set(main, "_displayProtocol", (CommunicationProtocol?)CommunicationProtocol.Profinet);
            Check((bool)Call(main, "ValidateManualRequest", selected, validationRequest, false), "Local trigger validation permits a ready channel");
            requestType.GetField("PlcInteraction", All).SetValue(validationRequest, true);
            Set(main, "_runOnline", false);
            Check(!(bool)Call(main, "ValidateManualRequest", selected, validationRequest, false), "PLC mode is blocked Offline before any hardware operation");
            Set(main, "_runOnline", true); service.IsStarted = false;
            Check(!(bool)Call(main, "ValidateManualRequest", selected, validationRequest, true), "PLC mode is blocked without an active service"); service.IsStarted = true;
            requestType.GetField("PlcInteraction", All).SetValue(validationRequest, false);
            Set(main, "_cameraValidationComplete", false);
            Check(!(bool)Call(main, "ValidateManualRequest", selected, validationRequest, false) &&
                (bool)Call(main, "ValidateManualRequest", selected, validationRequest, true), "Camera safety blocks capture, but local data simulation needs no camera");
            Set(main, "_cameraValidationComplete", true);
            requestType.GetField("Channel", All).SetValue(validationRequest, 4);
            Check(!(bool)Call(main, "ValidateManualRequest", selected, validationRequest, true), "Out-of-range channel is rejected");
            var dialogType = typeof(FormMain).Assembly.GetType("Aron_V2.ManualSimulationDialog");
            foreach (var pair in new[] { Tuple.Create(CommunicationProtocol.Profinet, 4), Tuple.Create(CommunicationProtocol.S7, 64), Tuple.Create(CommunicationProtocol.TcpIp, 6) })
            foreach (bool receive in new[] { false, true })
            using (var dialog = (Form)Activator.CreateInstance(dialogType, All, null, new object[] { new CommunicationConfig { Protocol = pair.Item1, ChannelCount = pair.Item2 }, receive, 0 }, null))
            {
                dialog.Opacity = 0; dialog.Show(); Application.DoEvents();
                var channels = (ComboBox)dialog.Controls.Find("ManualChannel", true).Single();
                var mode = (ComboBox)dialog.Controls.Find("ManualPlcInteraction", true).Single();
                Check(channels.Items.Count == pair.Item2 && channels.Items.Cast<int>().SequenceEqual(Enumerable.Range(0, pair.Item2)), "Channels match selected protocol: " + pair.Item1);
                Check(mode.SelectedIndex == 0, "PLC interaction defaults to No");
                channels.SelectedIndex = pair.Item2 - 1;
                var request = dialogType.GetProperty("Request", All).GetValue(dialog);
                Check((int)request.GetType().GetField("Channel", All).GetValue(request) == pair.Item2 - 1, "Full channel number retained (including channel 63)");
                if (receive)
                {
                    var part = (TextBox)dialog.Controls.Find("ManualPartNo", true).Single(); part.Text = "SN|A,\"中文\r\nX";
                    request = dialogType.GetProperty("Request", All).GetValue(dialog);
                    string log = (string)Call(request, "Describe", pair.Item1, true);
                    Check(log.Contains("SetUserData=1") && log.Contains("ClearResult=") && log.Contains("PosID=") && log.Contains("JobID=") && log.Contains("\\r\\n"), "Assembled logical input log is complete and escapes product-code delimiters/newlines");
                }
                foreach (Control child in dialog.Controls[0].Controls)
                    Check(dialog.Controls[0].ClientRectangle.Contains(child.Bounds), "Manual dialog element fits: " + child.Name);
                foreach (Button button in dialog.Controls.Find("ManualExecute", true).Single().Parent.Controls.OfType<Button>())
                    Check(button.Parent.ClientRectangle.Contains(button.Bounds), "Execute/Cancel buttons fully visible");
                Directory.CreateDirectory("Tests/UIValidation");
                using (var image = new Bitmap(dialog.Width, dialog.Height))
                { dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/ManualSimulation-" + (receive ? "Receive" : "Trigger") + ".png"); }
            }
            var language = typeof(FormMain).Assembly.GetType("Aron_V2.UiLanguage");
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "zh-CN" });
            using (var chinese = (Form)Activator.CreateInstance(dialogType, All, null,
                new object[] { selected, true, 0 }, null))
            {
                chinese.Opacity = 0; chinese.Show(); Application.DoEvents();
                Check(chinese.Text == "手动接收数据" && ((ComboBox)chinese.Controls.Find("ManualPlcInteraction", true).Single()).Items[0].ToString().Contains("仅本地"), "Chinese title and PLC mode choices follow application language");
                Check(chinese.Controls.Find("ManualExecute", true).Single().Text == "应用", "Chinese execute caption translated");
            }
            language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "en" });
            Check(Global.Result_Send.SequenceEqual(before), "All local simulation tests leave shared PLC bytes unchanged");
            Set(main, "_communicationService", null);
        }
    }
}

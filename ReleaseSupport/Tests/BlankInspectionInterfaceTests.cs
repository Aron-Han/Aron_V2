using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
internal static class BlankInspectionInterfaceTests
{
    const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string path = Path.Combine(Path.GetDirectoryName(args[0]), new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Test(args[0]); return 0; } catch (Exception ex) { Console.WriteLine(ex.Message); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Test(string path)
    {
        var app = Assembly.LoadFrom(path); object[] report = { null };
        if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", Flags).Invoke(null, report)) throw new Exception((string)report[0]);
        var initialize = app.GetType("Aron_V2.BlankCameraVpp").GetMethod("InitializeInspectionInterface", Flags);
        using (var tool = new CogToolBlock())
        {
            if (!(bool)initialize.Invoke(null, new object[] { tool })) throw new Exception("Empty block not initialized");
            if (tool.Inputs.Count != 5 || tool.Outputs.Count != 2 || tool.Tools.Count != 0) throw new Exception("Incorrect default interface");
            foreach (string name in new[] { "JobID", "PosID", "SN" })
                if (tool.Inputs[name].ValueType != typeof(string)) throw new Exception("Metadata must accept null strings");
            if (tool.Inputs[0].Name != "Input1" || tool.Inputs[0].ValueType != typeof(ICogImage) || tool.Inputs[1].Name != "Send_Data") throw new Exception("Image/data ordering incorrect");
            if ((string)tool.Outputs["Result_Cam"].Value != "2") throw new Exception("Blank algorithm must default to NG");
            if ((bool)initialize.Invoke(null, new object[] { tool })) throw new Exception("Existing terminals changed");
            using (var stream = new MemoryStream())
            {
                CogSerializer.SaveObjectToStream(tool, stream); stream.Position = 0;
                using (var restored = (CogToolBlock)CogSerializer.LoadObjectFromStream(stream))
                {
                    if (restored.Inputs.Count != 5 || restored.Outputs.Count != 2) throw new Exception("Serialization lost terminals");
                    var errors = (System.Collections.Generic.List<string>)app.GetType("Aron_V2.InspectionVppContract").GetMethod("Validate", Flags).Invoke(null,
                        new object[] { restored, new[] { "Input1", "Send_Data" }, new string[0] });
                    if (errors.Count != 0) throw new Exception(string.Join(";", errors));
                    var contract = app.GetType("Aron_V2.InspectionVppContract");
                    contract.GetMethod("ApplyContext", Flags).Invoke(null, new object[] { restored, "Job1", "Pos3", "ABC123" });
                    if ((string)restored.Inputs["SN"].Value != "ABC123") throw new Exception("SN not applied");
                    contract.GetMethod("ApplyContext", Flags).Invoke(null, new object[] { restored, null, "", " " });
                    if (restored.Inputs["JobID"].Value != null || restored.Inputs["PosID"].Value != null || restored.Inputs["SN"].Value != null) throw new Exception("Missing context retained stale values");
                    foreach (string name in new[] { "Input1", "Send_Data", "JobID", "PosID", "SN", "Result_Cam", "resultSend" })
                    {
                        using (var altered = (CogToolBlock)CogSerializer.DeepCopyObject(restored))
                        {
                            if (altered.Inputs.Contains(name)) altered.Inputs.Remove(altered.Inputs[name]); else altered.Outputs.Remove(altered.Outputs[name]);
                            var rejected = (System.Collections.Generic.List<string>)contract.GetMethod("Validate", Flags).Invoke(null, new object[] { altered, new[] { "Input1", "Send_Data" }, new string[0] });
                            if (rejected.Count == 0) throw new Exception("Missing required terminal accepted: " + name);
                        }
                    }
                }
            }
        }
        using (var existing = new CogToolBlock())
        {
            existing.Inputs.Add(new CogToolBlockTerminal("Custom", typeof(string)));
            if ((bool)initialize.Invoke(null, new object[] { existing }) || existing.Inputs.Count != 1) throw new Exception("Existing algorithm modified");
        }
        using (var candidates = new CogToolBlock())
        {
            initialize.Invoke(null, new object[] { candidates });
            var width = new CogToolBlockTerminal("WidthLimit", typeof(double)); width.Value = 1.25d; candidates.Inputs.Add(width);
            candidates.Inputs.Add(new CogToolBlockTerminal("Threshold", typeof(int)));
            var choices = (System.Collections.IList)app.GetType("Aron_V2.Parameters_Config").GetMethod("GetLimitInputChoices", Flags).Invoke(null, new object[] { candidates, new[] { "Threshold" } });
            if (choices.Count != 1 || (string)choices[0].GetType().GetField("Name", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(choices[0]) != "WidthLimit") throw new Exception("Default or duplicate VPP inputs were offered");
            if ((string)choices[0].GetType().GetField("DefaultValue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(choices[0]) != "1.25") throw new Exception("Input default value lost");
            var dialogType = app.GetType("Aron_V2.LimitInputChoiceForm");
            using (var dialog = (System.Windows.Forms.Form)Activator.CreateInstance(dialogType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { choices, "Job1 / Cam1 / Pos1" }, null))
            using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
            {
                var selector = (System.Windows.Forms.ComboBox)dialogType.GetField("Selector", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dialog);
                if (selector.DropDownStyle != System.Windows.Forms.ComboBoxStyle.DropDownList || selector.SelectedIndex != -1) throw new Exception("Parameter selection accepts manual input/default selection");
                timer.Tick += (s, e) => { timer.Stop(); dialog.DialogResult = System.Windows.Forms.DialogResult.Cancel; dialog.Close(); };
                timer.Start();
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.Cancel || dialogType.GetProperty("SelectedInput", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dialog, null) != null) throw new Exception("Cancel confirmed a parameter");
            }
            Console.WriteLine("PASS VPP default/duplicate exclusions, native value preservation, dropdown-only selection and cancelled dialog");
        }
        Console.WriteLine("PASS interface order, types, NG defaults, VPP roundtrip, save validation and existing algorithm preservation; no tool Run or device connection.");
    }
}

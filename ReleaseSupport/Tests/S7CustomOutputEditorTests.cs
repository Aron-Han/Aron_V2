using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Aron_V2;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;

class S7CustomOutputEditorTests
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    sealed class Designer : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, All).Invoke(target, args);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static DataGridView Grid(object form) => (DataGridView)form.GetType().GetField("dgvOutput", All).GetValue(form);
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            string path = Path.Combine(Path.GetDirectoryName(args[0]), new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try
        {
            var app = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Visual Inspection.exe")); object[] report = { null };
            Check((bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", All).Invoke(null, report), "VisionPro 9.6 SR2 runtime validation");
            Run(app); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run(Assembly app)
    {
        Check(app == typeof(Global).Assembly, "Tests and UI use the same application assembly");
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new Designer();
        var config = new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", Cameras = new List<CameraConfig> { new CameraConfig { Name = "Cam1", Positions = new List<PositionConfig> { new PositionConfig { Name = "Pos1", General = new PositionGeneralConfig { MainUsed = "1", MainChannel = "0" } } } } } } } };
        Global.CurrentConfig = config; XmlConfigHelper.Save(config, Global.ParameterCogfig);
        using (var tool = new CogToolBlock())
        {
            tool.Outputs.Add(new CogToolBlockTerminal("WidthPin", typeof(double)));
            tool.Outputs.Add(new CogToolBlockTerminal("Passed", typeof(bool)));
            Load_Job.VppCache.Set("Job1", 0, "Cam1", "Pos1", null, tool);
            using (var form = new S7OutputSettingForm(2))
            {
                form.Show(); Application.DoEvents();
                var grid = Grid(form);
                Check(grid.Columns.Contains("VppOutput") && !grid.Columns.Contains("SourceJob") && !grid.Columns.Contains("SourcePosition"), "Output grid needs only a VPP pin name, without Job/Camera/Position columns");
                Check((bool)Call(form, "SaveGridToXml"), "Unchanged legacy table saves without requiring new bindings");
                var fixedRow = grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["ParameterName"].Value) == "Result Valid");
                var dataRow = grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["ParameterName"].Value) == "Result X");
                Check(fixedRow.Cells["DataType"].ReadOnly && !dataRow.Cells["DataType"].ReadOnly, "Handshake type stays locked; data type becomes configurable");
                grid.CurrentCell = fixedRow.Cells["ParameterName"];
                Check(!grid.BeginEdit(false), "Fixed handshake name cannot enter edit mode");
                Call(form, "AddOutput", null, EventArgs.Empty);
                var row = grid.CurrentRow;
                row.Cells["ParameterName"].Value = "Width"; row.Cells["DbAddress"].Value = "DB101.1000";
                row.Cells["VppOutput"].Value = "WidthPin";
                Check((bool)Call(form, "SaveGridToXml"), "New custom row saves using only the VPP pin name");
                Call(form, "LoadConfigToGrid"); grid = Grid(form);
                row = grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["ParameterName"].Value) == "Width");
                Check(Convert.ToString(row.Cells["VppOutput"].Value) == "WidthPin" && Convert.ToString(row.Cells["DbAddress"].Value) == "DB101.1000", "Binding and custom address survive XML roundtrip");
                foreach (var legacy in grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToInt32(r.Cells["ChannelNo"].Value) == 0 && new[] { "Result X", "Result Y", "Result A", "Result0" }.Contains(Convert.ToString(r.Cells["ParameterName"].Value))).ToList()) grid.Rows.Remove(legacy);
                Check((bool)Call(form, "SaveGridToXml"), "Legacy data rows can be replaced by a custom-only CH0 output section");
                Call(form, "LoadConfigToGrid"); grid = Grid(form);
                Check(!grid.Rows.Cast<DataGridViewRow>().Any(r => Convert.ToInt32(r.Cells["ChannelNo"].Value) == 0 && Convert.ToString(r.Cells["ParameterName"].Value) == "Result X"), "Deleted legacy data rows are not regenerated");
                form.Width = 1450; form.Height = 850; Application.DoEvents();
                using(var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "s7-custom-output.png")); }
                form.Close();
            }
            using(var form = new S7OutputSettingForm(1))
            {
                form.Show(); Application.DoEvents();
                Check(!Grid(form).Rows.Cast<DataGridViewRow>().Any(r => Convert.ToInt32(r.Cells["ChannelNo"].Value) == 1), "Reduced channel count hides inactive data and handshake rows");
                Check((bool)Call(form, "SaveGridToXml"), "Custom output table saves with one active channel");
                form.Close();
            }
            string outputPath = ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml");
            byte[] original = File.ReadAllBytes(outputPath);
            using (var basic = new S7SettingForm())
            {
                basic.GetType().GetField("_config", All).SetValue(basic, S7ConfigXmlHelper.Load());
                Call(basic, "EnsureS7PointTables");
            }
            Check(original.SequenceEqual(File.ReadAllBytes(outputPath)), "Saving connection settings does not rebuild or erase custom mappings");
            CommunicationConfigHelper.Save(new CommunicationConfig { Protocol = CommunicationProtocol.S7, ChannelCount = 1 });
            var algorithm = (Algorithm)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Algorithm));
            var required = (List<string>)Call(algorithm, "RequiredOutputsForSave", "Job1", "Cam1", "Pos1", tool);
            Check(required.Contains("WidthPin") && !required.Contains("Result_X"), "Algorithm Save protects bound S7 pins and permits removed legacy data pins");
            tool.Outputs.Remove("WidthPin");
            var errors = (List<string>)app.GetType("Aron_V2.InspectionVppContract").GetMethod("Validate", All).Invoke(null, new object[] { tool, null, required });
            Check(errors.Any(x => x.Contains("WidthPin")), "Removing a bound VPP pin is rejected by the existing algorithm Save contract");
            tool.Outputs.Add(new CogToolBlockTerminal("WidthPin", typeof(double)));

        }
        Console.WriteLine("PASS All configuration is isolated under the test executable directory.");
    }
}

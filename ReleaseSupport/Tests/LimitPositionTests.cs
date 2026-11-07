using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Aron_V2;
internal static class LimitPositionTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static object Field(object value, string name) { return value.GetType().GetField(name, Flags).GetValue(value); }
    static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static PositionConfig Position(string name, string value) { return new PositionConfig { Name = name, Parameters = new List<ParameterConfig> { new ParameterConfig { Name = "LimitWidth", Description = "Width limit", Value = value } } }; }
    [STAThread] static int Main(string[] args)
    {
        try { Run(); return 0; } catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    static void Run()
    {
        var config = new AppConfig { Models = new List<ModelConfig> {
            new ModelConfig { Name = "Job1", Cameras = new List<CameraConfig> { new CameraConfig { Name = "Cam1", Positions = new List<PositionConfig> { Position("Pos1", "10"), Position("Pos2", "20") } } } },
            new ModelConfig { Name = "Job2", Cameras = new List<CameraConfig> { new CameraConfig { Name = "Cam9", Positions = new List<PositionConfig> { Position("Pos7", "70") } } } } } };
        using (var form = new Parameters_Config())
        {
            form.GetType().GetField("_config", Flags).SetValue(form, config);
            Call(form, "FillJobCombo", "Job1");
            var pos = (ComboBox)Field(form, "_comboPos"); var grid = (DataGridView)Field(form, "dataGridView1");
            var rows = (IList)Field(form, "_rows");
            Check(pos.Items.Count == 2 && rows.Count == 1, "Pos selector lists configured positions; grid contains one selected position only");
            rows[0].GetType().GetProperty("Value").SetValue(rows[0], "123", null);
            pos.SelectedItem = "Pos2";
            Check(rows.Count == 1 && (string)rows[0].GetType().GetProperty("Value").GetValue(rows[0], null) == "20", "Switching Position filters its own parameters");
            pos.SelectedItem = "Pos1";
            Check((string)rows[0].GetType().GetProperty("Value").GetValue(rows[0], null) == "123", "Unsaved edits survive position switches");
            Check(grid.Columns["Position"].ReadOnly && !grid.AllowUserToAddRows, "Parameter rows cannot accidentally create or move positions");
        }
        var dialogType = typeof(Parameters_Config).Assembly.GetType("Aron_V2.LimitCloneTargetForm");
        using (var dialog = (Form)Activator.CreateInstance(dialogType, Flags, null, new object[] { new Func<AppConfig>(() => config), "Job1", "Cam1", "Pos1" }, null))
        {
            var jobs = (ComboBox)Field(dialog, "_jobs"); var cameras = (ComboBox)Field(dialog, "_cameras"); var positions = (ComboBox)Field(dialog, "_positions");
            Check(!positions.Items.Contains("Pos1") && positions.Items.Contains("Pos2"), "Clone excludes the source position");
            jobs.SelectedItem = "Job2";
            Check((string)cameras.SelectedItem == "Cam9" && (string)positions.SelectedItem == "Pos7", "Clone Job/Cam/Pos selectors cascade correctly");
            config.Models[1].Cameras[0].Positions.Add(Position("Pos8", "80")); Call(dialog, "Reload");
            Check(positions.Items.Contains("Pos8"), "Clone selectors pick up new global positions without restarting");
            config.Models[1].Cameras[0].Positions.Clear(); Call(dialog, "Reload");
            Check(positions.Items.Count == 0 && !((Button)Field(dialog, "_confirm")).Enabled, "Removed positions disappear and cannot be cloned to");
            config.Models[1].Cameras[0].Positions.Add(Position("Pos7", "70")); Call(dialog, "Reload");
            dialog.Show(); Application.DoEvents();
            Directory.CreateDirectory("Tests/UIValidation");
            using (var image = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/LimitCloneTarget.png"); }
            dialog.Close();
        }
        var copy = (List<ParameterConfig>)typeof(Parameters_Config).GetMethod("CopyLimitParameters", Flags).Invoke(null, new object[] { config.Models[0].Cameras[0].Positions[0].Parameters });
        copy[0].Value = "999";
        Check(config.Models[0].Cameras[0].Positions[0].Parameters[0].Value == "10", "Clone creates independent parameter objects");
        Console.WriteLine("PASS no main application, VPP, camera or PLC startup");
    }
}

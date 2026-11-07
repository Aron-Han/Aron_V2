using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class LogPanelTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Flags).GetValue(obj); }
    static void Call(object obj, string name, params object[] args) { obj.GetType().GetMethod(name, Flags).Invoke(obj, args); }
    static int Count(object panel, string name) { return (int)panel.GetType().GetProperty(name, Flags).GetValue(panel, null); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void Layout(Control parent) { foreach (Control child in parent.Controls) Layout(child); parent.PerformLayout(); }
    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0]), args.Length > 1 && args[1] == "--panel-only"); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run(Assembly app, bool panelOnly)
    {
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new DesignerContext();
        var type = app.GetType("Aron_V2.LogPanel", true);
        var filter = app.GetType("Aron_V2.LogViewFilter", true);
        using (var host = new Form { Size = new Size(700, 900), ShowInTaskbar = false, Opacity = 0 })
        using (var panel = (Control)Activator.CreateInstance(type, true))
        {
            host.Controls.Add(panel); host.Show(); Application.DoEvents();
            string full = "Camera failed\r\nSystem.Exception: very long diagnostic\r\n at Test.Frame()";
            Call(panel, "Append", "Application ready", Color.Green);
            Call(panel, "Append", "S7 connected: 192.168.0.1", Color.Green);
            Call(panel, "Append", "PLC heartbeat timeout", Color.Red);
            Call(panel, "Append", full, Color.Red);
            Call(panel, "Append", "普通信息", Color.Black);
            Call(panel, "FlushPending");
            Check(Count(panel, "RetainedCount") == 5 && Count(panel, "VisibleCount") == 5, "ALL retains all events, including communication and warnings");
            Check((string)type.GetMethod("VisibleMessage", Flags).Invoke(panel, new object[] { 0 }) == "普通信息", "ALL places newest event first");
            Call(panel, "SetFilter", Enum.Parse(filter, "Warning"));
            Check(Count(panel, "VisibleCount") == 2, "Warning includes errors and communication warnings");
            Check((string)type.GetMethod("VisibleMessage", Flags).Invoke(panel, new object[] { 0 }) == full, "Warning newest first and full multiline exception remains intact");
            Call(panel, "SetFilter", Enum.Parse(filter, "Communicate"));
            Check(Count(panel, "VisibleCount") == 2, "Communicate contains healthy S7 and failing PLC messages");
            Call(panel, "Append", "Profinet handshake ready", Color.Black); Call(panel, "FlushPending");
            Check(Count(panel, "VisibleCount") == 3 && Count(panel, "RetainedCount") == 6, "Current filter applies to new events without deleting other categories");
            Check((string)type.GetMethod("VisibleMessage", Flags).Invoke(panel, new object[] { 0 }) == "Profinet handshake ready", "Communication places newest event first");
            Call(panel, "SetFilter", Enum.Parse(filter, "All"));
            Check(Count(panel, "VisibleCount") == 6, "Switching back to ALL restores retained history");
            var grid = (DataGridView)Field(panel, "_grid"); var column = grid.Columns[2];
            Check(grid.VirtualMode && !grid.AutoGenerateColumns && grid.Columns.Count == 3 && grid.ReadOnly, "Read-only virtual grid has three fixed columns");
            Check((bool)typeof(Control).GetProperty("DoubleBuffered", Flags).GetValue(grid, null), "Log grid is double buffered");
            var watch = Stopwatch.StartNew();
            Parallel.For(0, 3000, n => Call(panel, "Append", "S7 event " + n, Color.Black));
            Call(panel, "FlushPending");
            Check(Count(panel, "RetainedCount") == 2000 && Count(panel, "VisibleCount") == 2000, "Large background burst is bounded to 2000 UI records");
            Check(ReferenceEquals(column, grid.Columns[2]), "Burst updates preserve grid column instances");
            Console.WriteLine("INFO 3000-event enqueue/flush ms=" + watch.ElapsedMilliseconds);
            var scroll = (CheckBox)Field(panel, "_autoScroll"); scroll.Checked = false;
            grid.FirstDisplayedScrollingRowIndex = 10;
            string reading = (string)type.GetMethod("VisibleMessage", Flags).Invoke(panel, new object[] { 10 });
            Call(panel, "Append", "S7 another event", Color.Black); Call(panel, "FlushPending");
            Check((string)type.GetMethod("VisibleMessage", Flags).Invoke(panel, new object[] { grid.FirstDisplayedScrollingRowIndex }) == reading, "Auto scroll off preserves the record being read even when retention removes an old row");
            // Clear test-only buffer before a representative visual capture.
            ((System.Collections.IList)Field(panel, "_entries")).Clear();
            Call(panel, "SetFilter", Enum.Parse(filter, "All"));
            Call(panel, "Append", "Application ready", Color.Black);
            Call(panel, "Append", "Cam1 parameters loaded", Color.Green);
            Call(panel, "Append", "S7 connected: 192.168.0.1", Color.Green);
            Call(panel, "Append", "CH0 Trigger Ready = True", Color.Black);
            Call(panel, "Append", "Camera not connected", Color.Orange);
            Call(panel, "Append", "PLC heartbeat timeout", Color.Red);
            Call(panel, "Append", full, Color.Red); Call(panel, "FlushPending");
            foreach (int width in new[] { 360, 520, 700 })
            {
                host.ClientSize = new Size(width, 750); Layout(host); Application.DoEvents();
                Check(grid.Right <= grid.Parent.ClientSize.Width && grid.Bottom <= grid.Parent.ClientSize.Height, "Log content fits panel width " + width);
                foreach (var button in (Button[])Field(panel, "_filters"))
                    Check(button.Width >= TextRenderer.MeasureText(button.Text, button.Font).Width, "Filter label fits: " + button.Text + " at " + width);
                Directory.CreateDirectory("Tests/UIValidation");
                using (var bitmap = new Bitmap(panel.Width, panel.Height))
                { panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Log-B-" + width + ".png"); }
            }
        }
        if (panelOnly) return;
        using (var main = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain", true)))
        {
            var panel = (Control)Field(main, "_logPanel");
            var root = (TableLayoutPanel)Field(main, "tableLayoutPanel1");
            var split = (SplitContainer)Field(main, "_inspectionLogSplit");
            Check(panel.Parent == split.Panel2 && root.GetPositionFromControl(split) == new TableLayoutPanelCellPosition(0, 1), "Main log uses the full-width bottom splitter pane");
            Check(!((Timer)Field(panel, "_refresh")).Enabled && !((Timer)Field(main, "_communicationStatusTimer")).Enabled, "Designer construction starts neither log refresh nor communication");
            Type manager = app.GetType("Aron_V2.LogManager", true);
            FieldInfo rootField = manager.GetField("_logRootDirectory", BindingFlags.NonPublic | BindingFlags.Static);
            object originalRoot = rootField.GetValue(null);
            string isolated = Path.Combine(Path.GetTempPath(), "Aron-LogPanelTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                manager.GetMethod("SetRootDirectory").Invoke(null, new object[] { isolated });
                Call(panel, "SetFilter", Enum.Parse(filter, "Warning"));
                const string message = "Ordinary record hidden by warning filter\r\nOriginal multiline payload";
                Call(main, "UpdateLog", "Log", Tuple.Create((object)message, (object)Color.Black));
                Call(panel, "FlushPending");
                Check(Count(panel, "VisibleCount") == 0 && Count(panel, "RetainedCount") == 1, "Main log filter affects display only");
                string written = File.ReadAllText(Directory.GetFiles(isolated, "*.txt", SearchOption.AllDirectories).Single());
                Check(written.Contains(message), "Original file logger still receives the full unfiltered multiline message");
            }
            finally { rootField.SetValue(null, originalRoot); }
        }
        LicenseManager.CurrentContext = new LicenseContext();
        using (var host = new Form { ShowInTaskbar = false, Opacity = 0 })
        using (var panel = (Control)Activator.CreateInstance(type, true))
        {
            host.Controls.Add(panel); host.Show(); Application.DoEvents();
            Check(((Timer)Field(panel, "_refresh")).Enabled, "Normal log panel starts its UI-only batch timer");
            Call(panel, "Append", "Live log update", Color.Black);
            System.Threading.Thread.Sleep(160); Application.DoEvents();
            Check(Count(panel, "VisibleCount") == 1, "Live log becomes visible through the normal timer without manual flush");
        }
    }
}

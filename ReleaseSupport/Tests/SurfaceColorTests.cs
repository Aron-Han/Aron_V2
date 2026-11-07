using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Cognex.ColorTest { public sealed class Display : Panel { } }

internal static class SurfaceColorTests
{
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run(Assembly app)
    {
        Application.EnableVisualStyles();
        var apply = app.GetType("Aron_V2.DesktopTheme", true).GetMethod("ApplySurfaceColors", BindingFlags.Static | BindingFlags.NonPublic);
        using (var form = new Form())
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill };
            var channel = new UserControl(); var gutter = new Panel();
            channel.Controls.Add(gutter); root.Controls.Add(channel); form.Controls.Add(root);
            var signal = new Button { BackColor = Color.Red, ForeColor = Color.White, Text = "Offline" };
            var waiting = new Button { BackColor = Color.Yellow, Text = "Waiting" };
            var warning = new Panel { BackColor = Color.Orange };
            var separator = new Panel { Height = 1, BackColor = Color.Gainsboro };
            var display = new Cognex.ColorTest.Display { BackColor = Color.Navy };
            var vendorPanel = new Panel { BackColor = SystemColors.Control }; display.Controls.Add(vendorPanel);
            var grid = new DataGridView(); grid.Columns.Add("Address", "Address"); grid.Rows.Add("0");
            grid.Rows[0].Cells[0].Style.BackColor = Color.MistyRose;
            var log = new RichTextBox { Text = "Error" }; log.SelectAll(); log.SelectionColor = Color.Red;
            var status = new StatusStrip(); var communication = new ToolStripStatusLabel { Text = "Communicate:Null", ForeColor = Color.Red }; status.Items.Add(communication);
            form.Controls.AddRange(new Control[] { signal, waiting, warning, separator, display, grid, log, status });
            var bounds = gutter.Bounds; int clicks = 0; signal.Click += (s, e) => clicks++;
            apply.Invoke(null, new object[] { form }); apply.Invoke(null, new object[] { form });
            Check(form.BackColor == Color.White && root.BackColor == Color.White && channel.BackColor == Color.White && gutter.BackColor == Color.White, "Main surfaces and nested channel gaps share white");
            var later = new UserControl(); var nested = new Panel(); later.Controls.Add(nested); root.Controls.Add(later);
            Check(later.BackColor == Color.White && nested.BackColor == Color.White, "Dynamically added channel backgrounds are unified");
            Check(signal.BackColor == Color.Red && signal.ForeColor == Color.White && waiting.BackColor == Color.Yellow && warning.BackColor == Color.Orange, "Red/yellow/orange runtime status colors retained");
            Check(display.BackColor == Color.Navy && vendorPanel.BackColor == SystemColors.Control, "Cognex display and private scroll/container surfaces untouched");
            Check(separator.BackColor == Color.Gainsboro, "Deliberate hairline separator retained");
            Check(grid.Rows[0].Cells[0].Style.BackColor == Color.MistyRose && (string)grid.Rows[0].Cells[0].Value == "0", "Table warning styles and values untouched");
            Check(log.SelectionColor == Color.Red && communication.ForeColor == Color.Red && communication.Text == "Communicate:Null", "Log and connection status colors/text untouched");
            Check(status.BackColor == Color.White && gutter.Bounds == bounds && clicks == 0, "Status surface unified without changing geometry or firing actions");
        }
        LicenseManager.CurrentContext = new DesignerContext();
        using (var main = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain", true)))
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
            var cameraHost = (Control)main.GetType().GetField("tableLayoutPanel5", f).GetValue(main);
            var menu = (Control)main.GetType().GetField("menuStrip1", f).GetValue(main);
            var status = (Control)main.GetType().GetField("statusStrip1", f).GetValue(main);
            Check(main.BackColor == menu.BackColor && cameraHost.BackColor == menu.BackColor && status.BackColor == menu.BackColor, "Actual FormMain menu, camera host and status have identical backgrounds");
            Check(!((Timer)main.GetType().GetField("_communicationStatusTimer", f).GetValue(main)).Enabled, "Color test does not start devices or communication");
        }
    }
}

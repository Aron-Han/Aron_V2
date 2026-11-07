using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class EditorChannelLayoutTests
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var remove = app.GetType("Aron_V2.DesktopTheme", true).GetMethod("RemoveEditorChannelFromToolbar", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (bool camera in new[] { false, true })
            using (var form = new Form { ClientSize = new Size(1623, 200), Opacity = 0, ShowInTaskbar = false })
            {
                var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 60, ColumnCount = 11, RowCount = 1 };
                for (int i = 0; i < 10; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var caption = new Label { Text = "Channel:", Dock = DockStyle.Fill };
                var selector = new ComboBox(); selector.Items.AddRange(new[] { "0", "2" }); selector.SelectedIndex = 1;
                int changes = 0; selector.SelectedIndexChanged += (s, e) => changes++;
                var load = new Button { Text = "Load", Dock = DockStyle.Fill };
                var save = new Button { Text = "Save", Dock = DockStyle.Fill };
                toolbar.Controls.Add(caption, 6, 0); toolbar.Controls.Add(selector, 7, 0);
                toolbar.Controls.Add(load, 8, 0); toolbar.Controls.Add(save, 9, 0);
                TextBox sn = null;
                if (camera) { sn = new TextBox { Dock = DockStyle.Fill }; toolbar.Controls.Add(sn, 10, 0); }
                form.Controls.Add(toolbar); form.Show(); Application.DoEvents();
                int oldLeft = load.Left;
                remove.Invoke(null, new object[] { toolbar, caption, selector }); Application.DoEvents();
                Check(!caption.Visible && !selector.Visible && !selector.TabStop, "Channel caption/selector removed from visible UI and tab order");
                Check(toolbar.GetColumnWidths()[6] == 0 && toolbar.GetColumnWidths()[7] == 0, "Both reserved columns collapse completely");
                Check(load.Left == oldLeft - 240 && save.Left == load.Right + save.Margin.Left + load.Margin.Right, "Load and Save shift left without a blank gap");
                Check(selector.SelectedItem.ToString() == "2" && selector.Items.Count == 2 && changes == 0, "Internal channel selection unchanged; no extra selection event");
                selector.SelectedIndex = 0;
                Check(changes == 1, "Existing internal channel selection handler retained");
                if (sn != null) Check(sn.Width > 500 && sn.Right <= toolbar.ClientSize.Width, "Camera SN uses freed space and stays inside toolbar");
                form.Close();
            }
            Environment.Exit(0);
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); Environment.Exit(1); }
    }
}

using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;

internal static class S7ChannelVisibilityTests
{
    static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Hidden).Invoke(target, args); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static Form Open(Type type, int count)
    {
        Form form = (Form)Activator.CreateInstance(type, new object[] { count });
        Call(form, "InitGridStyle");
        Call(form, "LoadConfigToGrid");
        return form;
    }
    static DataGridView Grid(Form form, string direction) { return (DataGridView)form.GetType().GetField("dgv" + direction, Hidden).GetValue(form); }
    static DataGridViewRow Find(DataGridView grid, int channel)
    {
        foreach (DataGridViewRow row in grid.Rows) if (Convert.ToInt32(row.Cells["ChannelNo"].Value) == channel) return row;
        return null;
    }
    [STAThread]
    static int Main(string[] args)
    {
        Assembly app = Assembly.LoadFrom(args[0]);
        foreach (string direction in new[] { "Input", "Output" })
        {
            Type type = app.GetType("Aron_V2.S7" + direction + "SettingForm", true);
            using (Form form = Open(type, 2))
            {
                DataGridView grid = Grid(form, direction);
                DataGridViewRow row = Find(grid, 1);
                string suffix = Convert.ToString(row.Cells["DataType"].Value) == "Bool" ? ".0" : "";
                row.Cells["DbAddress"].Value = "DB9000.100" + suffix;
                Check((bool)Call(form, "SaveGridToXml"), direction + " saves initial two-channel custom mapping");
            }
            using (Form form = Open(type, 1))
            {
                DataGridView grid = Grid(form, direction);
                Check(Find(grid, 1) == null && Find(grid, 0) != null && Find(grid, -1) != null,
                    direction + " one channel shows CH0 and global signals only");
                DataGridViewRow row = Find(grid, 0);
                row.Cells["DbAddress"].Value = "DB8000.100.0";
                Check((bool)Call(form, "SaveGridToXml"), direction + " reduced table saves without inactive-channel validation errors");
                IList saved = (IList)Call(form, "LoadRowsFromXml");
                bool retained = false;
                foreach (object item in saved)
                    if ((int)item.GetType().GetProperty("Channel").GetValue(item, null) == 1 &&
                        Convert.ToString(item.GetType().GetProperty("DbAddress").GetValue(item, null)) == "DB9000.100.0") retained = true;
                Check(retained, direction + " saving CH0 preserves disabled CH1 custom address on disk");
            }
            using (Form form = Open(type, 2))
            {
                DataGridView grid = Grid(form, direction);
                Check(Convert.ToString(Find(grid, 1).Cells["DbAddress"].Value) == "DB9000.100.0", direction + " increasing count restores CH1 custom address");
                Check(Convert.ToString(Find(grid, 0).Cells["DbAddress"].Value) == "DB8000.100.0", direction + " active-channel edit survives reload");
            }
            using (Form form = Open(type, 3))
                Check(Convert.ToString(Find(Grid(form, direction), 2).Cells["DbAddress"].Value) == "", direction + " new channel still requires explicit address configuration");
        }
        return 0;
    }
}

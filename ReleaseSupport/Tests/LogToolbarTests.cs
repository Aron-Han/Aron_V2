using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class LogToolbarTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Flags).GetValue(obj); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, Flags).Invoke(obj, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static void Pump() { Application.DoEvents(); }
    [STAThread] static void Main(string[] args)
    {
        string path = Path.Combine(Path.GetTempPath(), "Aron-log-view-" + Guid.NewGuid().ToString("N") + ".xml");
        FieldInfo preferencePath = null; object originalPath = null;
        try
        {
            Application.EnableVisualStyles(); LicenseManager.CurrentContext = new DesignerContext();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            preferencePath = app.GetType("Aron_V2.LogViewPreferences").GetField("FilePath", BindingFlags.Static | BindingFlags.NonPublic);
            originalPath = preferencePath.GetValue(null); preferencePath.SetValue(null, path);
            Type type = app.GetType("Aron_V2.LogPanel"), filterType = app.GetType("Aron_V2.LogViewFilter"), categoryType = app.GetType("Aron_V2.LogCategory");
            using (var host = new Form { ClientSize = new Size(1280, 250) })
            using (var panel = (Control)Activator.CreateInstance(type, true))
            {
                Call(panel, "UseBottomLayout"); host.Controls.Add(panel); host.Show(); Pump();
                Check(!File.Exists(path), "Opening log settings does not write/overwrite preferences");
                Check(!panel.Controls.Find("Log", true).Any() && !((CheckBox)Field(panel, "_autoScroll")).Visible, "No Log title/footer checkbox in bottom layout");
                var grid = (DataGridView)Field(panel, "_grid");
                Check(grid.BorderStyle == BorderStyle.None && panel.Controls.Find("LogCard", true).Length == 1, "One soft outer card, no dark table-only frame");
                var gear = (Button)Field(panel, "_settingsButton");
                var toolbar = panel.Controls.Find("LogToolbar", true).Single();
                var tabs = (Button[])Field(panel, "_filters");
                Check(gear.PointToScreen(Point.Empty).X > tabs.Last().PointToScreen(Point.Empty).X + tabs.Last().Width, "Gear is at the far right of the same filter toolbar");
                Check(Math.Abs(gear.PointToScreen(Point.Empty).Y + gear.Height / 2.0 - tabs[0].PointToScreen(Point.Empty).Y - tabs[0].Height / 2.0) <= 1.5, "Gear and filter tabs share vertical center");
                foreach (var sample in new[] { Tuple.Create("Application ready", Color.Black), Tuple.Create("Cam1 parameters loaded", Color.Black), Tuple.Create("Inspection complete", Color.Black), Tuple.Create("S7 connected", Color.Black), Tuple.Create("PLC heartbeat timeout", Color.Red), Tuple.Create("Camera offline", Color.Orange) })
                    Call(panel, "Append", sample.Item1, sample.Item2);
                Call(panel, "FlushPending");
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 6, "Default ALL shows every category");
                Call(panel, "SetAllCategories", Enum.ToObject(categoryType, 0));
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 0 && (int)type.GetProperty("RetainedCount", Flags).GetValue(panel) == 6, "Unchecking every ALL category hides display without deleting history");
                var options = (IDictionary)Field(panel, "_categoryOptions");
                ((ToolStripMenuItem)options[Enum.Parse(categoryType, "Algorithm")]).Checked = true;
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 1 && (string)Call(panel, "VisibleMessage", 0) == "Inspection complete", "Algorithm checkbox displays only selected category");
                Call(panel, "Append", "Cam2 capture ready", Color.Black); Call(panel, "FlushPending");
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 1 && (int)type.GetProperty("RetainedCount", Flags).GetValue(panel) == 7, "New excluded records remain buffered");
                Call(panel, "SetFilter", Enum.Parse(filterType, "Warning"));
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 2, "Warning ignores ALL category choices");
                Call(panel, "SetFilter", Enum.Parse(filterType, "Communicate"));
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 2, "Communicate retains original matching logic");
                var scroll = (ToolStripMenuItem)Field(panel, "_scrollOption"); scroll.Checked = false;
                Check(!((CheckBox)Field(panel, "_autoScroll")).Checked && File.Exists(path), "Auto scroll menu controls the original state and persists it");
                gear.PerformClick(); Pump();
                var popup = (ContextMenuStrip)Field(panel, "_settingsMenu");
                Check(popup.Visible && popup.Items.OfType<ToolStripMenuItem>().Count(i => i.CheckOnClick) == 5, "Gear opens Auto scroll plus four real checkable category items");
                popup.Close();
                var language = app.GetType("Aron_V2.UiLanguage");
                language.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "zh-CN" });
                gear.PerformClick(); Pump();
                Check(scroll.Text == "自动滚动" && ((ToolStripMenuItem)options[Enum.Parse(categoryType, "Camera")]).Text == "相机 / 取像", "Gear menu follows global Chinese language without changing checks");
                popup.Close();
                language.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { "en" });
                Call(panel, "SetAllCategories", Enum.Parse(categoryType, "All")); Call(panel, "SetFilter", Enum.Parse(filterType, "All"));
                Check((int)type.GetProperty("VisibleCount", Flags).GetValue(panel) == 7, "Restoring categories recovers full retained history");
                // Persist an intentionally restricted category through the user menu.
                ((ToolStripMenuItem)options[Enum.Parse(categoryType, "Camera")]).Checked = false;
                Directory.CreateDirectory("Tests/UIValidation");
                using (var bitmap = new Bitmap(host.Width, host.Height)) { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/LogToolbar-Actual.png"); }
            }
            string saved = File.ReadAllText(path);
            using (var panel = (Control)Activator.CreateInstance(type, true))
            {
                Call(panel, "UseBottomLayout");
                Check(!((CheckBox)Field(panel, "_autoScroll")).Checked && (int)Field(panel, "_allCategories") == 13, "Auto scroll and category preferences restore on reopen");
                Check(File.ReadAllText(path) == saved, "Restoring preferences does not overwrite them with defaults");
            }
            using (var main = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain")))
            {
                var menu = (MenuStrip)Field(main, "menuStrip1");
                var online = (Button)Field(main, "button3");
                float scale; using (var graphics = Graphics.FromHwnd(IntPtr.Zero)) scale = graphics.DpiX / 96F;
                foreach (Size size in new[] { new Size(1280,720), new Size(1920,1080), new Size(2560,1440) })
                {
                    main.ClientSize = size; main.PerformLayout(); menu.Parent.PerformLayout(); online.Parent.PerformLayout();
                    Check(Math.Abs(online.Top + online.Height / 2.0 - menu.Items[0].Bounds.Top - menu.Items[0].Height / 2.0) <= 1.5, "Online and actual navigation item centerlines align at " + size);
                    Check(menu.Items.Cast<ToolStripItem>().All(i => i.Height <= 66 * scale), "Navigation is compact at " + size);
                    Check(online.Width >= 220 * scale && online.Height >= 44 * scale, "Online button is enlarged at " + size);
                }
                Check(Field(main, "_communicationService") == null, "Presentation initializes no communication service");
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        finally { if (preferencePath != null) preferencePath.SetValue(null, originalPath); if (File.Exists(path)) File.Delete(path); }
    }
}

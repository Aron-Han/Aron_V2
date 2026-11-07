using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class DropDownMenuTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void AssertHandler(ToolStripItem item, string method)
    {
        object key = typeof(ToolStripItem).GetField("EventClick", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var events = (EventHandlerList)typeof(Component).GetProperty("Events", Private).GetValue(item, null);
        Check(events[key] != null && events[key].GetInvocationList().Any(d => d.Method.Name == method), "Click handler retained: " + method);
    }
    static void Capture(MenuStrip menu, ToolStripMenuItem item, string name)
    {
        using (var bmp = new Bitmap(menu.Width, menu.Height + item.DropDown.Height + 15))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
            item.DropDown.DrawToBitmap(bmp, new Rectangle(item.Bounds.Left, menu.Height + 6, item.DropDown.Width, item.DropDown.Height));
            Directory.CreateDirectory("Tests/UIValidation"); bmp.Save("Tests/UIValidation/Dropdown-B-" + name + ".png");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); Environment.Exit(1); }
    }
    static void Run(Assembly app)
    {
        Application.EnableVisualStyles(); LicenseManager.CurrentContext = new DesignerContext();
        using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain", true)))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.ClientSize = new Size(1280, 720); form.Show(); Application.DoEvents();
            var menu = (MenuStrip)form.GetType().GetField("menuStrip1", Private).GetValue(form);
            Check(!menu.ShowItemToolTips && menu.Items.Count == 6, "No obstructing top-level tooltip; five original menus plus Help retained");
            Check(!((ToolStripMenuItem)menu.Items[0]).HasDropDownItems && !((ToolStripMenuItem)menu.Items[2]).HasDropDownItems, "Login and Database remain direct actions");
            foreach (int index in new[] { 1, 3, 4 })
            {
                var top = (ToolStripMenuItem)menu.Items[index];
                var original = top.DropDownItems.Cast<ToolStripItem>().ToArray();
                Check(original.Length == (index == 1 ? 6 : index == 3 ? 2 : 3), "Original dropdowns retained with optional plugin entry: " + top.Text);
                foreach (var child in original)
                {
                    if (child.Name == "cameraToolStripMenuItem")
                    {
                        var camera = (ToolStripMenuItem)child;
                        Check(camera.DropDownItems.Cast<ToolStripItem>().Select(i => i.Text).SequenceEqual(new[] { "Visionpro", "SDK-3D", "SDK-2Dline", "SDK-2D" }), "Camera has four category-specific submenus");
                        AssertHandler(camera.DropDownItems[0], "cameraToolStripMenuItem_Click");
                    }
                    else if (child.Name != "externalPluginsToolStripMenuItem") AssertHandler(child, child.Name + "_Click");
                    Check(child.Image != null, "Outline icon assigned: " + child.Text);
                }
                foreach (float scale in new[] { 1F, 1.5F, 2F })
                {
                    top.Enabled = true; top.DropDown.Font = new Font("Segoe UI", 10 * scale);
                    foreach (ToolStripItem item in top.DropDownItems) item.Font = top.DropDown.Font;
                    top.ShowDropDown(); Application.DoEvents();
                    Point expected = menu.PointToScreen(new Point(top.Bounds.Left, top.Bounds.Bottom));
                    Check(top.DropDown.Left == expected.X && top.DropDown.Top >= expected.Y - 1, "Popup left edge aligns with owner (native 1px border join): " + top.Text + " font " + scale);
                    Check(original.SequenceEqual(top.DropDownItems.Cast<ToolStripItem>()), "Dropdown preserves item identity/order");
                    foreach (ToolStripItem item in top.DropDownItems.Cast<ToolStripItem>().Where(i => i.Available))
                    {
                        Check(item.Bounds.Right <= top.DropDown.ClientSize.Width && item.Bounds.Bottom <= top.DropDown.ClientSize.Height, "Item fits popup: " + item.Text);
                        Check(top.DropDown.ClientSize.Width - item.Bounds.Right <= top.DropDown.Padding.Right + 1,
                            "No unused right strip beside menu row: " + item.Text);
                        var size = TextRenderer.MeasureText(item.Text, item.Font);
                        Check(item.Width >= size.Width + 50 && item.Height >= size.Height, "Text fits: " + item.Text);
                    }
                    int bottom = top.DropDownItems.Cast<ToolStripItem>().Where(i => i.Available).Max(i => i.Bounds.Bottom + i.Margin.Bottom);
                    Check(top.DropDown.Height - bottom == top.DropDown.Padding.Bottom, "No bottom whitespace after visible rows: " + top.Text);
                    if (scale == 1)
                    {
                        original[0].Select(); Application.DoEvents(); Capture(menu, top, index.ToString());
                        if (index == 1)
                        {
                            var camera = (ToolStripMenuItem)original[0]; camera.ShowDropDown(); Application.DoEvents();
                            Check(camera.DropDownDirection == ToolStripDropDownDirection.Right && camera.DropDown.Left >= top.DropDown.Right - 3, "Camera second-level menu opens on the right");
                            foreach (ToolStripItem page in camera.DropDownItems)
                                Check(page.Bounds.Right >= camera.DropDown.ClientSize.Width - camera.DropDown.Padding.Right - 1, "Second-level selection fills popup width: " + page.Text);
                            using (var bmp = new Bitmap(1280, 650))
                            using (var graphics = Graphics.FromImage(bmp))
                            {
                                graphics.Clear(Color.White); menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
                                var origin = menu.PointToScreen(Point.Empty);
                                top.DropDown.DrawToBitmap(bmp, new Rectangle(top.DropDown.Left - origin.X, top.DropDown.Top - origin.Y, top.DropDown.Width, top.DropDown.Height));
                                camera.DropDown.DrawToBitmap(bmp, new Rectangle(camera.DropDown.Left - origin.X, camera.DropDown.Top - origin.Y, camera.DropDown.Width, camera.DropDown.Height));
                                bmp.Save("Tests/UIValidation/Camera-Categories-Menu.png");
                            }
                            camera.HideDropDown();
                        }
                    }
                    top.HideDropDown();
                }
                original[0].Enabled = false;
                top.ShowDropDown(); Application.DoEvents();
                Check(!original[0].Enabled, "Opening menu does not enable permission-disabled items");
                top.HideDropDown();
                if (index == 1)
                {
                    var visible = original.Where(i => i.Available).ToArray();
                    Check(visible.Length == 4, "Setting retains four visible items and two hidden legacy targets");
                    int fullHeight = top.DropDown.Height;
                    var last = visible.Last(); last.Available = false;
                    top.ShowDropDown(); Application.DoEvents();
                    Check(top.DropDown.Height < fullHeight, "Menu shrinks after a row is hidden");
                    top.HideDropDown(); last.Available = true;
                    top.ShowDropDown(); Application.DoEvents();
                    Check(top.DropDown.Height == fullHeight, "Menu expands back after row is restored");
                    Check(original.SequenceEqual(top.DropDownItems.Cast<ToolStripItem>()) && !original[0].Enabled, "Reflow keeps original items and permissions");
                    top.HideDropDown();
                }
            }
            Check(!((Timer)form.GetType().GetField("_communicationStatusTimer", Private).GetValue(form)).Enabled, "Menu tests do not start communication or devices");
        }
    }
}

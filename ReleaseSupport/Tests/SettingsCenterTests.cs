using Aron_V2;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class SettingsCenterTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static Type Center = typeof(FormMain).Assembly.GetType("Aron_V2.SettingsCenterForm");
    static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    static void Expect(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static Form Create(Func<Form>[] factories, bool[] allowed, Func<string> revision = null)
    {
        Type entry = Center.GetNestedType("Page", Flags);
        Array pages = Array.CreateInstance(entry, factories.Length);
        for (int i = 0; i < factories.Length; i++)
        {
            int n = i;
            object page = Activator.CreateInstance(entry, true);
            entry.GetField("Title", Flags).SetValue(page, new[] { "General", "Limit Setting", "General Camera" }[i]);
            var icon = entry.GetField("Icon", Flags);
            icon.SetValue(page, Enum.Parse(icon.FieldType, new[] { "Settings", "Ruler", "Camera" }[i]));
            entry.GetField("Create", Flags).SetValue(page, factories[i]);
            entry.GetField("Allowed", Flags).SetValue(page, new Func<bool>(() => allowed[n]));
            entry.GetField("Revision", Flags).SetValue(page, revision);
            entry.GetField("SaveButtonName", Flags).SetValue(page, i == 0 ? "btnApply" : "btnSave");
            pages.SetValue(page, i);
        }
        return (Form)Activator.CreateInstance(Center, Flags, null, new object[] { pages, "Limit Setting" }, null);
    }
    static void Pump() { for (int i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Capture(Form form, string name)
    {
        Directory.CreateDirectory("Tests/UIValidation");
        using (var image = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save("Tests/UIValidation/SettingsCenter-" + name + ".png");
        }
    }
    static AppConfig Fixture()
    {
        return new AppConfig { Models = new List<ModelConfig> { new ModelConfig { Name = "Job1", General = new GeneralConfig { CamN = "1" },
            Cameras = new List<CameraConfig> { new CameraConfig { Name = "Cam1", Positions = new List<PositionConfig> {
                new PositionConfig { Name = "Pos1", General = new PositionGeneralConfig { MainUsed = "1", MainChannel = "0", SecondUsed = "1", SecondChannel = "1" },
                    Parameters = new List<ParameterConfig> { new ParameterConfig { Name = "Limit1_Min_X", Description = "X lower limit", Value = "-15" }, new ParameterConfig { Name = "Limit1_Max_X", Description = "X upper limit", Value = "15" } } } } } } } } };
    }
    [STAThread] static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            // Never construct/show the main application or call any real Save,
            // VPP factory, device SDK, communication or startup callback.
            using (var form = Create(new Func<Form>[] { () => new Form(), () => new Form { Controls = { new TextBox { Text = "value" } } }, () => new Form() }, new[] { true, true, false }))
            {
                form.Show(); Pump();
                var child = (Form)Field(form, "_page");
                Expect((int)Field(form, "_selected") == 1 && !child.TopLevel && child.FormBorderStyle == FormBorderStyle.None, "Initial Limit page is embedded, not another window");
                Expect(!((List<Button>)Field(form, "_navigation"))[2].Enabled, "Original permission gate disables General Camera");
                Call(form, "SelectPage", 2);
                Expect((int)Field(form, "_selected") == 1, "Disabled category cannot be opened programmatically");
                Call(form, "MarkEdited");
                Expect((bool)Field(form, "_dirty"), "User editing records unsaved state");
                Call(form, "MarkSaved");
                Expect(!(bool)Field(form, "_dirty"), "Only successful Save notification clears unsaved state");
                Call(form, "SelectPage", 0); Pump();
                Expect(!child.IsDisposed && !child.Visible && (int)Field(form, "_selected") == 0, "Clean page is retained without rebuilding or showing a second window");
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Call(form, "SelectPage", 1);
                Expect(ReferenceEquals(child, Field(form, "_page")), "Returning to a clean page reuses its exact original controls");
                Console.WriteLine("Cached switch: " + watch.ElapsedMilliseconds + "ms");
                Call(form, "SelectPage", 0);
                Call(form, "MarkSaved");
                Call(form, "SelectPage", 1);
                Expect(child.IsDisposed && !ReferenceEquals(child, Field(form, "_page")), "Successful Save invalidates other cached pages to prevent stale configuration");
                form.Close();
            }
            string revision = "1";
            using (var form = Create(new Func<Form>[] { () => new Form(), () => new Form(), () => new Form() }, new[] { true, true, true }, () => revision))
            {
                form.Show(); Pump();
                var child = (Form)Field(form, "_page");
                Call(form, "SelectPage", 0); revision = "2"; Call(form, "SelectPage", 1);
                Expect(child.IsDisposed, "External configuration revision change invalidates a clean cached page");
                form.Close();
            }
            AppConfig config = Fixture();
            int clicks = 0;
            using (var form = Create(new Func<Form>[] {
                () => new Form(), () => { var page = new Form(); var save = new Button { Name = "btnSave" }; save.Click += (s,e) => clicks++; page.Controls.Add(save); return page; }, () => new Form() }, new[] { true, true, true }))
            {
                form.Show(); Pump();
                ((Panel)Field(form, "_footer")).Controls.OfType<Button>().Single().PerformClick();
                Expect(clicks == 1, "Footer invokes the exact original Save click handler once, without new save logic");
                form.Close();
            }
            var factories = new Func<Form>[] {
                () => { var page = new ImageRecord(); Call(page, "PrepareForSettingsCenter"); return page; },
                () => {
                    var page = new Parameters_Config();
                    var load = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), page, typeof(Parameters_Config).GetMethod("Parameters_Config_Load", Flags));
                    page.Load -= load;
                    typeof(Parameters_Config).GetField("_config", Flags).SetValue(page, config);
                    Call(page, "FillJobCombo", new object[] { null });
                    return page;
                },
                () => new FormCamGeneralConfig(config, Path.Combine(Path.GetTempPath(), "unused-settings-center-fixture.xml")) };
            using (var form = Create(factories, new[] { true, true, true }))
            {
                form.Show(); Pump();
                Size? saveSize = null;
                Point? saveOffset = null;
                for (int index = 0; index < 3; index++)
                {
                    Call(form, "SelectPage", index); Pump();
                    Expect(!(bool)Field(form, "_dirty"), "Page loading is not marked dirty: " + index);
                    foreach (Size size in new[] { new Size(1000, 720), new Size(1400, 900) })
                    {
                        form.ClientSize = size; Pump();
                        var content = (Panel)Field(form, "_content");
                        var child = (Form)Field(form, "_page");
                        Expect(child.Width <= content.ClientSize.Width && child.Height <= content.ClientSize.Height, "Embedded page fits at " + size + " page " + index);
                        if (size.Width == 1400)
                        {
                            var footer = (Panel)Field(form, "_footer");
                            var save = footer.Controls.OfType<Button>().Single(b => b.Visible);
                            var offset = new Point(footer.ClientSize.Width - save.Right, footer.ClientSize.Height - save.Bottom);
                            Expect(save.Left >= 0 && save.Top >= 0 && save.Right <= footer.ClientSize.Width && save.Bottom <= footer.ClientSize.Height, "Save is inside footer: " + save.Bounds + " / " + footer.ClientSize);
                            if (saveSize == null) { saveSize = save.Size; saveOffset = offset; }
                            Expect(save.Size == saveSize.Value && offset == saveOffset.Value, "Save has identical fixed bottom-right size and margins on page " + index);
                            Expect(!child.Controls.Find(index == 0 ? "btnApply" : "btnSave", true).Any(), "Original Save button is moved out of scrollable page " + index);
                            Capture(form, index.ToString());
                            if (index == 0) CheckGeneralAlignment(child);
                            else CheckToolbarAlignment(child);
                        }
                    }
                }
                var camera = (Form)Field(form, "_page");
                var grid = (DataGridView)Field(camera, "dataGridView1");
                Expect(grid.Columns.Count == 7 && grid.Rows.Count == 1, "General Camera retains original position/channel grid");
                Expect(config.Models[0].Cameras[0].Positions[0].General.SecondChannel == "1", "Hosting does not change main/sub channels");
                form.Close();
            }
            Expect(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "GoSdkNet"), "No GoSDK was initialized");
            Console.WriteLine("PASS No real Save, camera acquisition or PLC startup called.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }

    static void CheckGeneralAlignment(Form page)
    {
        var table = (TableLayoutPanel)page.Controls.Find("GeneralSettingsLayout", true).Single();
        for (int row = 0; row < table.RowCount; row++)
        {
            Control label = table.GetControlFromPosition(0, row), value = table.GetControlFromPosition(1, row);
            if (label == null || value == null || ReferenceEquals(label, value)) continue;
            double captionCenter = label.PointToScreen(Point.Empty).Y + label.Height / 2.0;
            var inline = value as TableLayoutPanel;
            Control actualInput = inline != null && inline.Controls.Count > 0 ? inline.Controls[0] : value;
            double inputCenter = actualInput.PointToScreen(Point.Empty).Y + actualInput.Height / 2.0;
            Expect(Math.Abs(captionCenter - inputCenter) <= 1.5, "Label/actual input centers align: " + label.Text + " " + captionCenter + " / " + inputCenter);
        }
        var root = page.Controls.Find("txtRoot", true).Single();
        var browse = page.Controls.Find("btnBrowse", true).Single();
        Expect(root.Height == browse.Height && Math.Abs(root.PointToScreen(Point.Empty).Y - browse.PointToScreen(Point.Empty).Y) <= 1, "Directory Browse matches textbox height and center: " + root.Bounds + " / " + browse.Bounds);
        var format = page.Controls.Find("cboFormat", true).Single();
        var retention = page.Controls.Find("numRetention", true).Single();
        Expect(retention.PointToScreen(Point.Empty).Y - format.PointToScreen(Point.Empty).Y - format.Height >= 8, "Save type and delete cycle have a visible vertical gap");
    }

    static void CheckToolbarAlignment(Form page)
    {
        var toolbar = page.Controls.Find("AlignedSelectorToolbar", true).Single();
        var controls = toolbar.Controls.Cast<Control>().ToList();
        for (int index = 0; index + 1 < controls.Count; index++)
        {
            var label = controls[index] as Label;
            Control next = controls[index + 1];
            if (label == null || !(next is ComboBox || next is Button || next is Label) || Math.Abs(label.Top - next.Top) > 20) continue;
            Expect(Math.Abs(label.Top + label.Height / 2.0 - next.Top - next.Height / 2.0) <= 1.5, "Toolbar centers align: " + page.Name + " / " + label.Text);
        }
    }
}

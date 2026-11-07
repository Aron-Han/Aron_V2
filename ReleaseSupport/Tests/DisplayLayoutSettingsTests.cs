using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class DisplayLayoutSettingsTests
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static object Field(object target, string name) { return target.GetType().GetField(name, All).GetValue(target); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, All).Invoke(target, args); }
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void Pump() { for (int i = 0; i < 8; i++) Application.DoEvents(); }
    static void MainCheck(Assembly app, Type config, string path)
    {
        var language = app.GetType("Aron_V2.UiLanguage");
        config.GetProperty("FilePath", All).SetValue(null, path);
        Check(!File.Exists(path), "Isolated settings path starts absent");
        object defaults = config.GetMethod("Load", All).Invoke(null, null);
        Check((Size)Call(defaults, "Grid", 4) == new Size(4, 1) && !File.Exists(path), "Missing file preserves original one-row layout without writing");
        using (var general = (Form)Activator.CreateInstance(app.GetType("Aron_V2.ImageRecord")))
        using (var host = new Form { ClientSize = new Size(1120, 760), ShowInTaskbar = false, Opacity = 0 })
        {
            Call(general, "PrepareForSettingsCenter");
            general.TopLevel = false; general.FormBorderStyle = FormBorderStyle.None; general.Dock = DockStyle.Fill;
            host.Controls.Add(general); host.Show(); general.Show(); Pump();
            var combo = (ComboBox)Field(general, "cboFormat");
            var check = (CheckBox)Field(general, "chkEnable");
            foreach (int width in new[] { 850, 1120, 1400 })
            {
                host.ClientSize = new Size(width, 760); Pump();
                Check(combo.Parent.ClientRectangle.Contains(combo.Bounds), "Native format dropdown fully inside its row at " + width);
                Check(combo.Parent.Height >= combo.Height + 2 && !combo.Bounds.IntersectsWith(check.Bounds), "Dropdown bottom border has clearance; checkbox does not overlap");
            }
            combo.DroppedDown = true; Pump();
            Check(combo.Items.Cast<object>().Select(x => x.ToString()).SequenceEqual(new[] { "BMP", "PNG" }), "Original save-format options remain available");
            combo.DroppedDown = false;
        }

        LicenseManager.CurrentContext = new DesignerContext();
        using (var main = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain")))
        {
            main.ShowInTaskbar = false; main.Opacity = 0; main.ClientSize = new Size(1500, 950);
            var views = (IList)Field(main, "camViews");
            var viewType = app.GetType("Aron_V2.UserControl_ImageShow");
            var table = (TableLayoutPanel)Field(main, "tableLayoutPanel5");
            for (int i = 0; i < 4; i++)
            {
                var view = (Control)Activator.CreateInstance(viewType);
                view.Name = "Cam" + (i + 1); view.Dock = DockStyle.Fill;
                views.Add(view); table.Controls.Add(view);
            }
            main.Show(); Pump();
            foreach (Control view in views)
            {
                Call(view, "SetButton_JobID", Color.Green, "Job1");
                Call(view, "SetButton_PosID", Color.Gainsboro, "Pos1");
                Call(view, "SetButton_EngineID", Color.Gainsboro, "Engine:0");
                Call(view, "SetButton_CamID", Color.Gainsboro, view.Name);
            }
            var originals = views.Cast<Control>().ToArray();
            var displays = originals.Select(v => v.GetType().GetProperty("CogDisplay").GetValue(v)).ToArray();
            var bitmapSource = new Bitmap(32, 32);
            var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
            object syntheticImage = Activator.CreateInstance(core.GetType("Cognex.VisionPro.CogImage8Grey"), bitmapSource);
            main.Disposed += (s, e) => { ((IDisposable)syntheticImage).Dispose(); bitmapSource.Dispose(); };
            foreach (object display in displays) display.GetType().GetProperty("Image").SetValue(display, syntheticImage);
            Call(main, "ApplyCameraViewLayout"); Pump();
            Check(table.RowCount == 1 && table.ColumnCount == 4, "Legacy startup layout remains 1 x 4");
            using (var page = (Form)Activator.CreateInstance(app.GetType("Aron_V2.DisplayLayoutSettingsForm"), All, null, new object[] { 4 }, null))
            {
                page.ShowInTaskbar = false; page.Opacity = 0; page.Show(); Pump();
                var rows = (NumericUpDown)page.Controls.Find("DisplayRows", true).Single();
                var columns = (NumericUpDown)page.Controls.Find("DisplayColumns", true).Single();
                var save = (Button)page.Controls.Find("btnSave", true).Single();
                int saves = 0;
                page.GetType().GetEvent("SettingsSaved", All).GetAddMethod(true).Invoke(page, new object[] { new EventHandler((s, e) => { saves++; Call(main, "ApplyCameraViewLayout"); }) });
                foreach (Size requested in new[] { new Size(2, 1), new Size(2, 2), new Size(3, 2), new Size(1, 1) })
                {
                    int beforeRows = table.RowCount, beforeColumns = table.ColumnCount;
                    rows.Value = requested.Height; columns.Value = requested.Width; Pump();
                    Check(table.RowCount == beforeRows && table.ColumnCount == beforeColumns, "Draft row/column edits do not affect live layout");
                    save.PerformClick(); Pump();
                    int expectedColumns = requested == new Size(3, 2) ? 2 : requested.Width;
                    int expectedRows = (4 + expectedColumns - 1) / expectedColumns;
                    Check(table.RowCount == expectedRows && table.ColumnCount == expectedColumns && table.Controls.Count == 4, "Save applies occupied grid and keeps every camera: " + expectedRows + " x " + expectedColumns);
                    for (int i = 0; i < 4; i++)
                    {
                        var view = originals[i];
                        Check(ReferenceEquals(views[i], view) && ReferenceEquals(table.GetControlFromPosition(i % expectedColumns, i / expectedColumns), view), "Original camera order and instance preserved: " + i);
                        Check(ReferenceEquals(displays[i], viewType.GetProperty("CogDisplay").GetValue(view)) && !view.IsDisposed && view.Visible, "Original image display stays alive: " + i);
                        Check(ReferenceEquals(displays[i].GetType().GetProperty("Image").GetValue(displays[i]), syntheticImage) &&
                            (bool)displays[i].GetType().GetProperty("AutoFit").GetValue(displays[i]), "Synthetic image and AutoFit retained through reflow: " + i);
                        Check((string)viewType.GetProperty("JobID").GetValue(view) == "Job1" && (string)viewType.GetProperty("PosID").GetValue(view) == "Pos1" && (int)viewType.GetProperty("Channel").GetValue(view) == 0, "Routing identity unchanged: " + i);
                    }
                    var reloaded = config.GetMethod("Load", All).Invoke(null, null);
                    Check((Size)Call(reloaded, "Grid", 4) == new Size(expectedColumns, expectedRows), "Saved grid reloads identically for next startup");
                }
                Check(saves == 4, "Exactly one successful-save callback per click");
                foreach (string code in new[] { "zh-CN", "en" })
                {
                    language.GetMethod("SetLanguage", All).Invoke(null, new object[] { code });
                    language.GetMethod("ApplyTo", All).Invoke(null, new object[] { page }); Pump();
                    Check(page.Controls.Find("DisplayLayoutPreview", true).Single().Text.StartsWith(code == "en" ? "Cameras:" : "相机数量："), "Layout summary follows language: " + code);
                }
                Check(!((Timer)Field(main, "_communicationStatusTimer")).Enabled && Field(main, "_communicationService") == null, "No production communication startup in designer-context tests");
            }
            // Verify the real settings-center host uses the standard footer and
            // that its new page's successful Save can apply the live UI grid.
            Type centerType = app.GetType("Aron_V2.SettingsCenterForm");
            Type entryType = centerType.GetNestedType("Page", All);
            var entries = Array.CreateInstance(entryType, 2);
            Form center = null;
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var entry = Activator.CreateInstance(entryType, true);
                entryType.GetField("Title", All).SetValue(entry, i == 0 ? "General" : "Display Layout");
                entryType.GetField("SaveButtonName", All).SetValue(entry, i == 0 ? "btnApply" : "btnSave");
                entryType.GetField("Allowed", All).SetValue(entry, new Func<bool>(() => true));
                entryType.GetField("Create", All).SetValue(entry, new Func<Form>(() =>
                {
                    if (index == 0)
                    {
                        var page = (Form)Activator.CreateInstance(app.GetType("Aron_V2.ImageRecord"));
                        Call(page, "PrepareForSettingsCenter"); return page;
                    }
                    var display = (Form)Activator.CreateInstance(app.GetType("Aron_V2.DisplayLayoutSettingsForm"), All, null, new object[] { 4 }, null);
                    display.GetType().GetEvent("SettingsSaved", All).GetAddMethod(true).Invoke(display,
                        new object[] { new EventHandler((s, e) => { Call(main, "ApplyCameraViewLayout"); Call(center, "MarkSaved"); }) });
                    return display;
                }));
                entries.SetValue(entry, i);
            }
            using (center = (Form)Activator.CreateInstance(centerType, All, null, new object[] { entries, "Display Layout" }, null))
            {
                center.Show(main); Pump();
                var footer = (Panel)Field(center, "_footer");
                var displayPage = (Form)Field(center, "_page");
                Check(displayPage.GetType().Name == "DisplayLayoutSettingsForm", "Display Layout opens as a settings-center page");
                var columnsInput = (NumericUpDown)displayPage.Controls.Find("DisplayColumns", true).Single();
                center.Activate(); columnsInput.Focus(); Pump(); columnsInput.Value = 2;
                Check((bool)Field(center, "_dirty"), "Display edits participate in existing unsaved-change protection");
                var save = footer.Controls.OfType<Button>().Single(b => b.Visible);
                Rectangle saveBounds = save.Bounds;
                save.PerformClick(); Pump();
                Check(!(bool)Field(center, "_dirty") && table.ColumnCount == 2 && table.RowCount == 2, "Footer Save applies layout immediately and clears dirty state");
                Directory.CreateDirectory("Tests/UIValidation");
                using (var image = new Bitmap(center.Width, center.Height))
                { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/DisplayLayoutSettings.png"); }
                Call(center, "SelectPage", 0); Pump();
                Check(footer.Controls.OfType<Button>().Single(b => b.Visible).Bounds == saveBounds, "Display Layout shares the same bottom-right Save geometry as General");
                using (var image = new Bitmap(center.Width, center.Height))
                { center.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/GeneralFormatFixed.png"); }
                center.Close();
            }
            // Reproduce a saved 1x4 layout with only one remaining camera,
            // including a non-contiguous catalog ID and viewport resizing.
            File.WriteAllText(path, "<DisplayLayout Rows=\"1\" Columns=\"4\" />");
            main.GetType().GetField("_catalogCameraIds", All).SetValue(main, new System.Collections.Generic.HashSet<int> { 2 });
            Call(main, "ApplyCameraViewLayout"); Pump();
            Check(table.ColumnCount == 1 && table.RowCount == 1 && table.Controls.Count == 1 &&
                ReferenceEquals(table.GetControlFromPosition(0, 0), originals[1]), "Single remaining Cam2 fills a 1x1 grid without changing camera identity");
            foreach (int width in new[] { 1000, 1500, 1800 })
            {
                main.ClientSize = new Size(width, 950); Pump();
                Check(originals[1].Width >= table.ClientSize.Width - originals[1].Margin.Horizontal - 4 &&
                    originals[1].Height >= table.ClientSize.Height - originals[1].Margin.Vertical - 4,
                    "Single camera expands with the viewport at " + width);
            }
            Check(File.ReadAllText(path).Contains("Columns=\"4\""), "Auto-fit leaves saved four-column preference intact");
            main.GetType().GetField("_catalogCameraIds", All).SetValue(main, null);
            Call(main, "ApplyCameraViewLayout"); Pump();
            Check(table.ColumnCount == 4 && table.RowCount == 1 && table.Controls.Count == 4 &&
                originals.All(v => !v.IsDisposed && v.Visible), "Restoring cameras restores preferred layout and reuses live controls");
        }
    }
    [STAThread] static void Main(string[] args)
    {
        string folder = Path.Combine(Path.GetTempPath(), "AronDisplayLayoutTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            object[] report = { null };
            Check((bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", All).Invoke(null, report), "Dependency audit only; no capture or PLC");
            MainCheck(app, app.GetType("Aron_V2.DisplayLayoutSettings"), Path.Combine(folder, "DisplayLayout.xml"));
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        finally
        {
            string file = Path.Combine(folder, "DisplayLayout.xml");
            if (File.Exists(file)) File.Delete(file);
            if (Directory.GetFileSystemEntries(folder).Length == 0) Directory.Delete(folder);
        }
    }
}

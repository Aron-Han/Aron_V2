using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Isolated real display controls only. Never create FormMain, run an inspection,
// connect an SDK device, start a capture or initialize PLC communication.
internal static class CameraCardLayoutTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static Control Field(Control view, string name) { return (Control)view.GetType().GetField(name, Private).GetValue(view); }
    static Rectangle InView(Control view, Control child)
    { return new Rectangle(view.PointToClient(child.PointToScreen(Point.Empty)), child.Size); }
    static void Save(Form form, string name)
    {
        Directory.CreateDirectory("Tests/UIValidation");
        using (var bitmap = new Bitmap(form.Controls[0].Width, form.Controls[0].Height))
        { form.Controls[0].DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/" + name + ".png"); }
    }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var type = app.GetType("Aron_V2.UserControl_ImageShow", true);
            var language = app.GetType("Aron_V2.UiLanguage", true);
            object[] report = { null };
            Check((bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report), "VisionPro dependency audit only (no device startup)");
            using (var form = new Form { ClientSize = new Size(1920, 850), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) })
            using (var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.FromArgb(241, 245, 250) })
            {
                form.Controls.Add(row);
                var views = new UserControl[4];
                for (int i = 0; i < 4; i++)
                {
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                    views[i] = (UserControl)Activator.CreateInstance(type);
                    views[i].Dock = DockStyle.Fill; row.Controls.Add(views[i], i, 0);
                }
                form.Show(); Application.DoEvents();
                foreach (var view in views)
                {
                    foreach (var entry in new[] { new[] { "SetButton_JobID", "Job1" }, new[] { "SetButton_CamID", "Cam1" }, new[] { "SetButton_PosID", "Pos1" }, new[] { "SetButton_EngineID", "Engine:0" } })
                        type.GetMethod(entry[0]).Invoke(view, new object[] { entry[0] == "SetButton_JobID" ? Color.Green : Color.Gainsboro, entry[1] });
                    type.GetMethod("SetButton_Parameter").Invoke(view, new object[] { Color.Yellow, "Waiting" });
                    type.GetMethod("BindStats").Invoke(view, new[] { Activator.CreateInstance(app.GetType("CameraStats"), "UiLayoutReadOnly") });
                    foreach (string name in new[] { "lbl_Total", "lbl_Pass", "lbl_Passrate" })
                        Check(Field(view, name).DataBindings.Count == 1 && Field(view, name).DataBindings[0].DataSourceUpdateMode == DataSourceUpdateMode.Never, "Original one-way statistics binding retained: " + name);
                    Field(view, "lbl_Total").Text = "Total：Total：440";
                    Field(view, "lbl_Pass").Text = "Pass：Pass：440";
                    Field(view, "lbl_Passrate").Text = "PassRate：100.00%";
                    Check((string)Field(view, "lbl_Total").GetType().GetProperty("DisplayValue", Private).GetValue(Field(view, "lbl_Total")) == "440", "Statistics renderer extracts value without rewriting bound Text");
                }
                foreach (Size size in new[] { new Size(1920, 850), new Size(1280, 650), new Size(2560, 1000) })
                {
                    form.ClientSize = size; Application.DoEvents();
                    foreach (var view in views)
                    {
                        var root = (TableLayoutPanel)Field(view, "tableLayoutPanel1");
                        Check(root.ColumnCount == 1 && root.RowCount == 5 && root.GetColumnWidths().Length == 1, "One-column card, no stale designer spans at " + view.Width);
                        var image = InView(view, Field(view, "cogRecordDisplay1"));
                        string[] top = { "button1", "btnResult" };
                        string[] bottom = { "lbl_Total", "lbl_Pass", "lbl_Passrate", "button2", "button3", "button4", "Reset", "btnTrigger", "btnReplay" };
                        Check(top.All(n => InView(view, Field(view, n)).Bottom <= image.Top) && bottom.All(n => InView(view, Field(view, n)).Top >= image.Bottom), "Only Cam/status above image; statistics/selectors/actions below it");
                        Check(InView(view, Field(view, "lbl_Total")).Bottom <= InView(view, Field(view, "button2")).Top &&
                            InView(view, Field(view, "button2")).Bottom <= InView(view, Field(view, "Reset")).Top &&
                            root.GetRow(Field(view, "cogRecordDisplay1")) == 1 && root.GetRow(Field(view, "tableLayoutPanel3")) == 2,
                            "Exact reference order: header, image, metrics, Job/Pos/Engine, actions");
                        Check(Field(view, "Reset").Size == Field(view, "button2").Size &&
                            Field(view, "btnTrigger").Size == Field(view, "button3").Size &&
                            Field(view, "btnReplay").Size == Field(view, "button4").Size,
                            "Action buttons match Job/Pos/Engine width and height exactly");
                        foreach (string name in top.Concat(bottom))
                        { var rect = InView(view, Field(view, name)); Check(view.ClientRectangle.Contains(rect) && rect.Width > 0 && rect.Height > 0, "Visible bounds: " + name); }
                        Check(Field(view, "button2").BackColor == Color.Green && Field(view, "btnResult").BackColor == Color.Yellow, "Job and Waiting status colors retained");
                        var display = type.GetProperty("CogDisplay").GetValue(view);
                        Check((bool)display.GetType().GetProperty("AutoFit").GetValue(display), "Original display AutoFit retained");
                    }
                    if (size.Width == 1920) Save(form, "CameraCards-B-Layout-A-Buttons");
                    if (size.Width == 1280) Save(form, "CameraCards-Narrow");
                }
                var first = views[0];
                int reset = 0, trigger = 0, replay = 0;
                type.GetEvent("Reset_Count").AddEventHandler(first, new EventHandler((s, e) => reset++));
                type.GetEvent("Trigger_Manual").AddEventHandler(first, new EventHandler((s, e) => trigger++));
                type.GetEvent("Replay").AddEventHandler(first, new EventHandler((s, e) => replay++));
                ((Button)Field(first, "Reset")).PerformClick();
                ((Button)Field(first, "btnTrigger")).PerformClick();
                ((Button)Field(first, "btnReplay")).PerformClick();
                Check(reset == 1 && replay == 1 && trigger == 0, "Original action events fire once; disabled trigger stays blocked");
                type.GetMethod("SetButton_btntrigger").Invoke(first, new object[] { true });
                ((Button)Field(first, "btnTrigger")).PerformClick();
                Check(trigger == 1, "Original trigger enable method and Click work unchanged");
                var global = app.GetType("Aron_V2.Global");
                var config = (IDictionary)global.GetField("CamGeneral").GetValue(null);
                config.Add("Job1.Cam1.Pos1", null); config.Add("Job1.Cam1.Pos2", null);
                try
                {
                    ((Button)Field(first, "button3")).PerformClick(); Application.DoEvents();
                    var popup = Application.OpenForms.Cast<Form>().Single(f => f.GetType().Name == "PopupListForm");
                    var list = (ListBox)popup.Controls[0]; list.SelectedIndex = 1;
                    popup.GetType().GetMethod("Pick", Private).Invoke(popup, null);
                    Check((string)type.GetProperty("PosID").GetValue(first) == "Pos2", "Actual original Pos popup updates the original identity control");
                }
                finally { config.Remove("Job1.Cam1.Pos1"); config.Remove("Job1.Cam1.Pos2"); }
                foreach (string code in new[] { "zh-CN", "en" })
                {
                    language.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { code });
                    Application.DoEvents();
                    Check((string)type.GetProperty("JobID").GetValue(first) == "Job1" && (string)type.GetProperty("CamID").GetValue(first) == "Cam1" && (int)type.GetProperty("Channel").GetValue(first) == 0, "Language preserves routing identities: " + code);
                }
            }
            using (var form = new Form { ClientSize = new Size(1920, 960) })
            using (var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 })
            {
                form.Controls.Add(grid);
                for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
                for (int i = 0; i < 2; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                for (int i = 0; i < 6; i++)
                {
                    var view = (UserControl)Activator.CreateInstance(type); view.Dock = DockStyle.Fill;
                    type.GetMethod("SetButton_CamID").Invoke(view, new object[] { Color.Gainsboro, "Cam" + (i + 1) });
                    type.GetMethod("SetButton_JobID").Invoke(view, new object[] { Color.Green, "Job1" });
                    type.GetMethod("SetButton_Parameter").Invoke(view, new object[] { Color.Yellow, "Waiting" });
                    Field(view, "lbl_Total").Text = "Total:440"; Field(view, "lbl_Pass").Text = "Pass:440"; Field(view, "lbl_Passrate").Text = "PassRate:100.00%";
                    grid.Controls.Add(view, i % 3, i / 3);
                }
                form.Show(); Application.DoEvents();
                foreach (UserControl view in grid.Controls)
                {
                    var root = (TableLayoutPanel)Field(view, "tableLayoutPanel1");
                    int[] heights = root.GetRowHeights();
                    Check(heights[2] + heights[3] + heights[4] <= heights[0] * 2.84F, "Six-camera footer is about 25% shorter relative to the unchanged header");
                    var image = InView(view, Field(view, "cogRecordDisplay1"));
                    Check(image.Height > 150 && image.Bottom <= InView(view, Field(view, "lbl_Total")).Top, "Six-camera grid retains a usable image and non-overlapping footer");
                    Check(Field(view, "Reset").Height == Field(view, "button2").Height && Field(view, "Reset").Height >= Field(view, "Reset").Font.Height + 4, "Compact action/readout heights match and text fits");
                }
                Save(form, "CameraCards-Six-Compact");
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

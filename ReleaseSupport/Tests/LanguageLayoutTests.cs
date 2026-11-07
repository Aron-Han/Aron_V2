using Aron_V2;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class LanguageLayoutTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static Type Language = typeof(FormMain).Assembly.GetType("Aron_V2.UiLanguage");
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Flags).GetValue(obj); }
    static void LanguageCall(string name, params object[] args) { Language.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Pump() { for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(10); } }
    static void Screenshot(Form form, string name)
    {
        Directory.CreateDirectory("Tests/UIValidation");
        using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/" + name + ".png"); }
    }
    private sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    [STAThread] static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            var culture = CultureInfo.CurrentCulture;
            using (var form = new Form())
            {
                var button = new Button { Text = "Save" };
                var input = new TextBox { Text = "Save" };
                var id = new Button { Text = "Cam4" };
                var label = new Label { Text = "Offline", ForeColor = Color.Red };
                var grid = new DataGridView(); grid.Columns.Add("Parameter", "Parameter"); grid.Rows.Add("Limit1_Min_X");
                var menu = new MenuStrip(); var setting = new ToolStripMenuItem("&Setting"); menu.Items.Add(setting);
                form.Controls.AddRange(new Control[] { button, input, id, label, grid, menu });
                LanguageCall("ApplyTo", form);
                LanguageCall("SetLanguage", "zh-CN"); LanguageCall("ApplyTo", form);
                Check(button.Text == "保存" && label.Text == "离线" && setting.Text == "设置" && grid.Columns[0].HeaderText == "参数", "Captions, status, menus and column headers switch to Chinese");
                Check(input.Text == "Save" && id.Text == "Cam4" && grid.Rows[0].Cells[0].Value.ToString() == "Limit1_Min_X" && label.ForeColor == Color.Red, "Input values, camera IDs, parameter keys and status colors are untouched");
                label.Text = "S7 Online";
                Check(label.Text == "S7 在线", "Runtime status updates are translated without changing the underlying state");
                LanguageCall("SetLanguage", "en"); LanguageCall("ApplyTo", form);
                Check(button.Text == "Save" && setting.Text == "&Setting" && label.Text == "S7 Online", "English round-trip restores exact original captions and mnemonic");
            }
            Check(ReferenceEquals(culture, CultureInfo.CurrentCulture), "Language changes do not alter numeric/date parsing culture");
            string testFile = Path.Combine(Path.GetTempPath(), "aron-language-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                ImageRecord.Current.Language = "zh-CN"; ImageRecord.SaveSettings(testFile);
                ImageRecord.Current.Language = "en"; ImageRecord.LoadSettings(testFile);
                Check(ImageRecord.Current.Language == "zh-CN", "Language persists through the original XML settings serializer");
                ImageRecord.Current.Language = "en";
            }
            finally { if (File.Exists(testFile)) File.Delete(testFile); }
            LanguageCall("SetLanguage", "en");
            using (var page = new ImageRecord())
            {
                typeof(ImageRecord).GetMethod("PrepareForSettingsCenter", Flags).Invoke(page, null);
                page.Show(); Pump();
                var selector = (ComboBox)page.Controls.Find("LanguageSelector", true).Single();
                selector.SelectedIndex = 1;
                Check((string)Language.GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) == "en", "Selecting Chinese alone does not apply it before Save");
                LanguageCall("Start", "zh-CN"); Pump();
                Check(page.Controls.Find("label1", true).Single().Text == "保存格式：", "Global language observer translates newly opened application pages automatically");
                var format = page.Controls.Find("cboFormat", true).Single();
                var root = page.Controls.Find("txtRoot", true).Single();
                var typeLabel = page.Controls.Find("label1", true).Single();
                Check(format.PointToScreen(Point.Empty).X == root.PointToScreen(Point.Empty).X, "Save type and other input fields share the exact left edge");
                Check(Math.Abs(typeLabel.PointToScreen(Point.Empty).Y + typeLabel.Height / 2.0 - format.PointToScreen(Point.Empty).Y - format.Height / 2.0) <= 1.5, "Save type label and actual combo box centers align: " + typeLabel.PointToScreen(Point.Empty) + "/" + typeLabel.Height + " vs " + format.PointToScreen(Point.Empty) + "/" + format.Height + "; wrapper " + format.Parent.Bounds);
                page.Dispose();
            }
            LanguageCall("SetLanguage", "en");

            LicenseManager.CurrentContext = new DesignerContext();
            using (var main = new FormMain())
            {
                // Disable original Load. Never call Program.Main/Bootstrap or
                // FormMain.Shown, native camera controls, Save or communication.
                main.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), main, typeof(FormMain).GetMethod("FormMain_Load", Flags));
                var split = (SplitContainer)Field(main, "_inspectionLogSplit");
                var cameras = (TableLayoutPanel)Field(main, "tableLayoutPanel5");
                var log = (Control)Field(main, "_logPanel");
                var online = (Button)Field(main, "button3");
                var status = (StatusStrip)Field(main, "statusStrip1");
                Check(log.Parent == split.Panel2 && cameras.Parent == split.Panel1 && online.Parent.Name == "OnlineHeaderHost", "Bottom log splitter and top-right original Online button are correctly hosted");
                Check(Field(main, "_communicationService") == null && !((System.Windows.Forms.Timer)Field(main, "_communicationStatusTimer")).Enabled, "Layout initialization creates no PLC service or startup timer in designer context");
                cameras.ColumnCount = 4; cameras.ColumnStyles.Clear();
                for (int i = 0; i < 4; i++)
                {
                    cameras.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                    cameras.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.MidnightBlue, Margin = new Padding(6) }, i, 0);
                }
                online.Text = "Offline"; online.BackColor = Color.Red;
                main.Show(); Pump();
                foreach (Size size in new[] { new Size(1280, 720), new Size(1920, 1080), new Size(2560, 1440) })
                {
                    main.ClientSize = size; Pump();
                    int first = split.SplitterDistance;
                    split.SplitterDistance = Math.Max(split.Panel1MinSize, first - 60); Pump();
                    Check(cameras.Height == split.Panel1.ClientSize.Height && log.Height == split.Panel2.ClientSize.Height, "Dragging splitter resizes image/log panes at " + size);
                    Check(cameras.Width > main.ClientSize.Width * .98 && log.Width > main.ClientSize.Width * .98, "Camera and log areas both use full width at " + size);
                    Screenshot(main, "BottomLog-Diagnostic");
                    Check(online.PointToScreen(Point.Empty).X > main.PointToScreen(Point.Empty).X + main.ClientSize.Width * .7 && online.PointToScreen(Point.Empty).Y < cameras.PointToScreen(Point.Empty).Y, "Online stays above cameras at upper right at " + size + ": button " + online.PointToScreen(Point.Empty) + "; cameras " + cameras.PointToScreen(Point.Empty) + "; host " + online.Parent.Bounds);
                    Check(status.PointToScreen(Point.Empty).Y >= log.PointToScreen(Point.Empty).Y + log.Height, "Status strip remains below log at " + size);
                }
                main.ClientSize = new Size(1600, 1000); Pump();
                LanguageCall("ApplyTo", main); Screenshot(main, "BottomLog-English");
                LanguageCall("SetLanguage", "zh-CN"); LanguageCall("ApplyTo", main); Pump(); Screenshot(main, "BottomLog-Chinese");
                Check(online.Text == "离线" && online.BackColor == Color.Red, "Global language switching preserves Offline signal color");
                main.Dispose();
            }
            LanguageCall("SetLanguage", "en");
            Console.WriteLine("PASS No real acquisition/inspection run, startup, GoSystem or PLC connection invoked.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

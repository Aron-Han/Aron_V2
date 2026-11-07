using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Compile this executable into a NEW isolated directory, not the production
// output. Global.ProjectDatabaseDir follows this executable's BaseDirectory.
internal static class DatabaseLayoutBTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, All).Invoke(target, args); }
    static Control Field(object target, string name) { return (Control)target.GetType().GetField(name, All).GetValue(target); }
    static void Click(Control control)
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        ((Button)control).PerformClick();
    }
    static void Ready(DataGridView grid, Form form)
    {
        var watch = Stopwatch.StartNew();
        while (grid.DataSource == null || Field(form, "label3").Text.StartsWith("Loading page"))
        { if (watch.ElapsedMilliseconds > 15000) throw new Exception("Query timeout: " + Field(form, "label3").Text + "; rows=" + grid.Rows.Count); Application.DoEvents(); Thread.Sleep(10); }
        Application.DoEvents();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            // This harness pumps messages instead of Application.Run. Install
            // the normal UI context explicitly so async queries marshal back
            // to the UI thread exactly as they do in the production app.
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Application.ThreadException += (s, e) => { Console.Error.WriteLine(e.Exception); Environment.Exit(1); };
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            string dbPath = (string)app.GetType("Aron_V2.Global").GetProperty("dBPath").GetValue(null);
            Check(dbPath.StartsWith(AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase) &&
                !dbPath.StartsWith(Path.GetDirectoryName(Path.GetFullPath(args[0])) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Database fixture is isolated from production output");
            var sqlite = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])), "System.Data.SQLite.dll"));
            using (var form = (Form)Activator.CreateInstance(app.GetType("WindowsFormsApp2.DataBase")))
            {
                form.Opacity = 0; form.ShowInTaskbar = false; form.Show();
                var grid = (DataGridView)Field(form, "dataGridView1"); Ready(grid, form);
                Check(Field(form, "button3").IsDisposed && form.Controls.Find("button3", true).Length == 0, "Add Row removed from the interface");
                Check(grid.Columns.Count == 9 && grid.ReadOnly && !grid.AllowUserToAddRows && !grid.AutoGenerateColumns, "Original nine readonly fields retained");
                string payload = "Result_X=-0.239;\r\nResult_Y=-1.699; Result_A=0.182; Result=1; comma, quote\" 中文";
                DateTime time = new DateTime(2026, 10, 26, 12, 0, 0);
                using (var connection = (IDbConnection)Activator.CreateInstance(sqlite.GetType("System.Data.SQLite.SQLiteConnection"), "Data Source=" + dbPath + ";Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "INSERT INTO Orders (Serial,JobN,CamN,PosN,Channel,ResultCode,RawMessage,CreatedAt) VALUES (@Serial,'Job1','Cam1','Pos1',0,1,@Raw,@Date)";
                        for (int i = 0; i < 55; i++)
                        {
                            command.Parameters.Clear();
                            foreach (var pair in new[] { new[] { "@Serial", "SN-" + i.ToString("000") }, new[] { "@Raw", payload }, new[] { "@Date", time.AddSeconds(i).ToString("yyyy-MM-dd HH:mm:ss") } })
                            { var p = command.CreateParameter(); p.ParameterName = pair[0]; p.Value = pair[1]; command.Parameters.Add(p); }
                            command.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
                Call(form, "LoadTotalCount"); Call(form, "LoadPage", 1); Ready(grid, form);
                Check(grid.Rows.Count == 50 && Field(form, "label3").Text == "Page 1 / 2", "Original page size/count and descending page query preserved");
                grid.CurrentCell = grid.Rows[5].Cells[0]; Application.DoEvents();
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var detail = (TextBox)form.Controls.Find("DbDetail" + column.Name, true).Single();
                    Check(detail.ReadOnly && detail.Text == grid.CurrentRow.Cells[column.Name].Value.ToString(), "Selected record value unchanged: " + column.Name);
                }
                var raw = (TextBox)form.Controls.Find("DbDetailPayload", true).Single();
                Check(raw.Multiline && raw.WordWrap && raw.ScrollBars == ScrollBars.Vertical && raw.Text == payload, "Full multiline Payload shown without rewriting raw content");
                string csv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DatabaseLayout-export.csv");
                Call(form, "ExportDataGridViewToCsv", grid, csv);
                Check(File.ReadAllText(csv).Contains(payload.Replace("\"", "\"\"")), "Original CSV exporter retains commas, quotes and multiline values");
                var serialColumn = grid.Columns["Serial"]; serialColumn.Width = 211;
                Click(Field(form, "button1")); Ready(grid, form);
                Check(grid.Rows.Count == 5 && Field(form, "label3").Text == "Page 2 / 2" && ReferenceEquals(serialColumn, grid.Columns["Serial"]) && serialColumn.Width == 211, "Next page retains original columns and user widths");
                Click(Field(form, "button4")); Ready(grid, form);
                Check(grid.Rows.Count == 50, "Previous page action retained");
                ((DateTimePicker)Field(form, "dateTimePicker1")).Value = time.AddSeconds(10);
                ((DateTimePicker)Field(form, "dateTimePicker2")).Value = time.AddSeconds(14);
                Click(Field(form, "Filter")); Ready(grid, form);
                Check(grid.Rows.Count == 5 && grid.Rows.Cast<DataGridViewRow>().All(r => string.CompareOrdinal(r.Cells["CreatedAt"].Value.ToString(), "2026-10-26 12:00:10") >= 0 && string.CompareOrdinal(r.Cells["CreatedAt"].Value.ToString(), "2026-10-26 12:00:14") <= 0), "Original inclusive time filter preserved");
                var language = app.GetType("Aron_V2.UiLanguage");
                foreach (string code in new[] { "zh-CN", "en" })
                {
                    language.GetMethod("SetLanguage", All).Invoke(null, new object[] { code });
                    language.GetMethod("ApplyTo", All).Invoke(null, new object[] { form }); Application.DoEvents();
                    Check(raw.Text == payload && grid.CurrentRow.Cells["Payload"].Value.ToString() == payload, "Language changes captions only, not record data: " + code);
                }
                foreach (Size size in new[] { new Size(1840, 1050), new Size(1380, 800), new Size(2200, 1100) })
                {
                    form.ClientSize = size; Application.DoEvents();
                    var split = (SplitContainer)form.Controls.Find("DatabaseRecordSplit", true).Single();
                    var toolbar = (Control)form.Controls.Find("DatabaseToolbar", true).Single();
                    var footer = (Control)form.Controls.Find("DatabaseFooter", true).Single();
                    Check(toolbar.Bottom <= split.Top && split.Bottom <= footer.Top && footer.Bottom <= form.ClientSize.Height, "Toolbar/content/footer remain separate at " + form.ClientSize);
                    Check(split.Panel1.Width >= split.Panel1MinSize && split.Panel2.Width >= split.Panel2MinSize, "Master/detail panes retain usable sizes");
                }
                Directory.CreateDirectory("Tests/UIValidation");
                using (var image = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/Database-Layout-B.png"); }
                ((DateTimePicker)Field(form, "dateTimePicker1")).Value = time.AddYears(-1);
                ((DateTimePicker)Field(form, "dateTimePicker2")).Value = time.AddYears(-1).AddSeconds(1);
                Click(Field(form, "Filter")); Ready(grid, form);
                Check(grid.Rows.Count == 0 && form.Controls.Find("DbDetailPayload", true).Single().Text == "", "Empty query clears selected record details");
                using (var connection = (IDbConnection)Activator.CreateInstance(sqlite.GetType("System.Data.SQLite.SQLiteConnection"), "Data Source=" + dbPath + ";Version=3;"))
                {
                    connection.Open(); using (var command = connection.CreateCommand())
                    { command.CommandText = "SELECT COUNT(*) FROM Orders"; Check(Convert.ToInt32(command.ExecuteScalar()) == 55, "Browsing/filter/export did not mutate fixture records"); }
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

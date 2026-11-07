using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Aron_V2;
using WindowsFormsApp2;
internal static class DatabaseRenderingTests
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] static void Main()
    {
        try { Run(); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run()
    {
        Application.EnableVisualStyles();
        // This executable is built in CommunicationValidation, so its Project/Database
        // is separate from the production bin/x64/Debug project and its records.
        var watch = Stopwatch.StartNew();
        using (var form = new Parameters_Config())
        {
            Console.WriteLine("Limit constructor: " + watch.ElapsedMilliseconds + " ms");
            Global.ParameterCogfig = Path.GetFullPath("Aron_V2/bin/x64/Debug/Project/Config/Parameter.xml");
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show();
            Console.WriteLine("Limit first Show: " + watch.ElapsedMilliseconds + " ms");
            while (form.UseWaitCursor && watch.ElapsedMilliseconds < 15000) { Application.DoEvents(); Thread.Sleep(10); }
            Check(!form.UseWaitCursor, "Limit configuration completed without blocking form opening");
            Console.WriteLine("Limit ready: " + watch.ElapsedMilliseconds + " ms");
        }
        using (var form = new DataBase())
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show();
            var grid = (DataGridView)typeof(DataBase).GetField("dataGridView1", Flags).GetValue(form);
            Check((bool)typeof(Control).GetProperty("DoubleBuffered", Flags).GetValue(grid), "Database grid is double buffered");
            Check(!grid.AutoGenerateColumns && grid.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.None, "No repeated automatic column generation or measuring");
            var column = grid.Columns["Serial"]; column.Width = 211;
            var bind = typeof(DataBase).GetMethod("BindDataTableToGrid", Flags);
            for (int n = 0; n < 5; n++)
            {
                var table = new DataTable();
                foreach (DataGridViewColumn col in grid.Columns) table.Columns.Add(col.DataPropertyName);
                table.Rows.Add("1", "SN", "1", "Job1", "Cam1", "Pos1", "0", "2026-10-21", "payload");
                bind.Invoke(form, new object[] { table });
            }
            Check(ReferenceEquals(column, grid.Columns["Serial"]) && column.Width == 211, "Five page replacements preserve columns and user widths");
            // Complete the initial asynchronous query before closing; use the isolated database only.
            for (int n = 0; n < 20; n++) { Application.DoEvents(); Thread.Sleep(10); }
        }
    }
}

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Cognex.ThemeTest
{
    // An SDK-like subtree must remain owned by the vendor, not by our theme.
    public sealed class Editor : Panel { }
}

internal static class SettingsThemeTests
{
    sealed class Row { public string Value { get; set; } }
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); Environment.Exit(1); }
    }

    static void Run(Assembly app)
    {
        Application.EnableVisualStyles();
        MethodInfo apply = app.GetType("Aron_V2.DesktopTheme", true).GetMethod("ApplySettings", BindingFlags.Static | BindingFlags.NonPublic);
        using (var form = new Form { Size = new Size(800, 550), ShowInTaskbar = false, Opacity = 0 })
        {
            var source = new BindingList<Row> { new Row { Value = "Float" }, new Row { Value = "Byte" } };
            var grid = new DataGridView { AutoGenerateColumns = false, AllowUserToAddRows = false, Location = new Point(10, 10), Size = new Size(650, 230) };
            var column = new DataGridViewComboBoxColumn { Name = "Format", DataPropertyName = "Value", ReadOnly = true };
            column.Items.AddRange("Float", "Byte");
            grid.Columns.Add(column);
            form.Controls.Add(grid);
            grid.DataSource = source;
            form.Show(); Application.DoEvents();
            object tag = new object();
            grid.Rows[0].Tag = tag;
            grid.Rows[0].Cells[0].Style.BackColor = Color.MistyRose;
            grid.Rows[1].Cells[0].Style.BackColor = Color.LightGray;
            int painted = 0, clicks = 0, changed = 0;
            grid.CellPainting += (s, e) => painted++;
            grid.CellValueChanged += (s, e) => changed++;
            var save = new Button { Text = "Save", Location = new Point(10, 260), Size = new Size(100, 35) };
            save.Click += (s, e) => clicks++;
            form.Controls.Add(save);
            var status = new Button { Text = "Status", BackColor = Color.Red };
            form.Controls.Add(status);
            var password = new TextBox { UseSystemPasswordChar = true, Text = "not-a-real-password", ReadOnly = true };
            form.Controls.Add(password);
            var number = new NumericUpDown { Minimum = 1, Maximum = 64, Value = 4 };
            form.Controls.Add(number);
            var radio = new RadioButton { Checked = true };
            form.Controls.Add(radio);
            var vendor = new Cognex.ThemeTest.Editor { BackColor = Color.Beige };
            var vendorButton = new Button { Text = "Vendor", BackColor = Color.Bisque };
            vendor.Controls.Add(vendorButton); form.Controls.Add(vendor);
            Rectangle saveBounds = save.Bounds;
            apply.Invoke(null, new object[] { form });
            apply.Invoke(null, new object[] { form });
            Check(ReferenceEquals(grid.DataSource, source) && ReferenceEquals(grid.Columns[0], column), "Grid bindings and column instances retained");
            Check(grid.Rows.Count == 2 && (string)grid.Rows[0].Cells[0].Value == "Float" && ReferenceEquals(grid.Rows[0].Tag, tag), "Rows, values and tags retained");
            Check(column.ReadOnly && column.Items.Count == 2 && column.DataPropertyName == "Value", "Read-only and format dropdown semantics retained");
            Check(grid.Rows[0].Cells[0].Style.BackColor == Color.MistyRose && grid.Rows[1].Cells[0].Style.BackColor == Color.LightGray, "Warning and read-only cell colors retained");
            Check(changed == 0, "Applying theme does not edit grid values");
            grid.Rows[0].Cells[0].Value = "Byte";
            Check(changed == 1, "Original value-change handler retained without duplication");
            using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, grid.ClientRectangle);
            Check(painted > 0, "Original cell-painting handler remains active");
            save.PerformClick();
            Check(clicks == 1 && save.Bounds == saveBounds, "Original button action and fixed-layout bounds retained");
            Check(save.BackColor == Color.FromArgb(26, 119, 207) && status.BackColor == Color.Red, "Primary button styled and status color preserved");
            Check(password.UseSystemPasswordChar && password.ReadOnly && password.Text == "not-a-real-password", "Password behavior unchanged");
            Check(number.Minimum == 1 && number.Maximum == 64 && number.Value == 4 && radio.Checked, "Protocol and channel selection state unchanged");
            Check(vendor.BackColor == Color.Beige && vendorButton.BackColor == Color.Bisque && vendorButton.FlatStyle == FlatStyle.Standard, "Vendor editor subtree untouched");
            var added = new Button { Text = "Save", Size = new Size(100, 35) }; form.Controls.Add(added);
            Check(added.BackColor == save.BackColor, "Late-added settings buttons share the theme");
            Check((bool)typeof(Control).GetProperty("DoubleBuffered", Private).GetValue(grid, null), "Settings grid uses double buffering");
        }
        foreach (string name in new[] { "ImageRecord", "Rotate_Center", "Out_put_Parameters", "FormPlcInputParameters", "S7SettingForm" })
        using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2." + name, true)))
        {
            form.Opacity = 0; form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            Check(form.BackColor == Color.White, name + " opens with the settings theme without camera or PLC startup");
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(System.IO.Path.GetFullPath("Tests/UIValidation/" + name + "-settings.png"));
            }
            form.Close();
        }
    }
}

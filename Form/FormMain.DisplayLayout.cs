using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Aron_V2
{
    internal sealed class DisplayLayoutSettings
    {
        internal static string FilePath { get; set; } = ProjectPaths.Config("General/DisplayLayout.xml");
        internal int Rows = 1;
        internal int Columns; // Missing legacy file: preserve one row of all cameras.

        internal Size Grid(int cameraCount)
        {
            int count = Math.Max(1, cameraCount);
            int columns = Columns == 0 ? count : Math.Max(1, Math.Min(Columns, count));
            int preferredRows = Math.Max(1, Math.Min(Rows, count));
            // Retain the saved arrangement preference, but never reserve whole
            // empty rows/columns after cameras are removed from the catalog.
            // A single remaining camera always gets the entire viewport.
            if (count <= preferredRows * columns)
                columns = Math.Min(columns, (count + preferredRows - 1) / preferredRows);
            return new Size(columns, (count + columns - 1) / columns);
        }

        internal static DisplayLayoutSettings Load()
        {
            var result = new DisplayLayoutSettings();
            try
            {
                if (!File.Exists(FilePath)) return result;
                var root = XDocument.Load(FilePath).Root;
                if (root == null || root.Name != "DisplayLayout") return result;
                int rows, columns;
                if (int.TryParse((string)root.Attribute("Rows"), out rows) && rows >= 1 && rows <= 16 &&
                    int.TryParse((string)root.Attribute("Columns"), out columns) && columns >= 1 && columns <= 16)
                { result.Rows = rows; result.Columns = columns; }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
            { Trace.WriteLine("Display layout settings: " + ex.Message); }
            return result;
        }

        internal void Save()
        {
            if (Rows < 1 || Rows > 16 || Columns < 1 || Columns > 16) throw new ArgumentOutOfRangeException("Rows/Columns");
            string path = Path.GetFullPath(FilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                new XDocument(new XElement("DisplayLayout", new XAttribute("Rows", Rows), new XAttribute("Columns", Columns))).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    internal sealed class DisplayLayoutSettingsForm : Form
    {
        internal event EventHandler SettingsSaved;
        private readonly NumericUpDown _rows, _columns;
        private readonly Label _preview;
        private readonly int _cameraCount;

        internal DisplayLayoutSettingsForm(int cameraCount)
        {
            _cameraCount = Math.Max(0, cameraCount);
            var settings = DisplayLayoutSettings.Load();
            float scale = DesktopTheme.DisplayScale;
            Text = "Display Layout";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96 * scale, 96 * scale);
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2,
                Padding = new Padding((int)(10 * scale)), Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145 * scale));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _rows = Number("DisplayRows", settings.Rows, scale);
            _columns = Number("DisplayColumns", settings.Columns == 0 ? Math.Max(1, cameraCount) : settings.Columns, scale);
            AddField(layout, 0, "Rows:", _rows, scale);
            AddField(layout, 1, "Columns:", _columns, scale);
            _preview = new Label { Name = "DisplayLayoutPreview", AutoSize = true, ForeColor = DesktopTheme.Muted,
                Margin = new Padding(0, (int)(18 * scale), 0, 0) };
            layout.Controls.Add(_preview, 0, 2); layout.SetColumnSpan(_preview, 2);
            var note = new Label { Text = "Save applies immediately. Cameras keep their order. Unused rows and columns collapse automatically; no camera is hidden.",
                AutoSize = true, MaximumSize = new Size((int)(600 * scale), 0), Margin = new Padding(0, (int)(14 * scale), 0, 0) };
            layout.Controls.Add(note, 0, 3); layout.SetColumnSpan(note, 2);
            var save = new Button { Name = "btnSave", Text = "Save", Size = new Size((int)(100 * scale), (int)(32 * scale)) };
            layout.Controls.Add(save, 1, 4);
            Controls.Add(layout);
            DesktopTheme.ApplySettings(this);
            _rows.ValueChanged += (s, e) => UpdatePreview();
            _columns.ValueChanged += (s, e) => UpdatePreview();
            save.Click += (s, e) =>
            {
                try
                {
                    new DisplayLayoutSettings { Rows = (int)_rows.Value, Columns = (int)_columns.Value }.Save();
                    SettingsSaved?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { MessageBox.Show(this, ex.Message, UiLanguage.Text("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            UpdatePreview();
        }

        private static NumericUpDown Number(string name, int value, float scale)
        { return new NumericUpDown { Name = name, Minimum = 1, Maximum = 16, Value = Math.Max(1, Math.Min(16, value)), Width = (int)(120 * scale), Anchor = AnchorStyles.Left }; }

        private static void AddField(TableLayoutPanel layout, int row, string text, Control field, float scale)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var margin = new Padding(0, (int)(8 * scale), 0, (int)(8 * scale));
            var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = margin };
            field.Margin = margin;
            layout.Controls.Add(label, 0, row); layout.Controls.Add(field, 1, row);
        }

        private void UpdatePreview()
        {
            Size grid = new DisplayLayoutSettings { Rows = (int)_rows.Value, Columns = (int)_columns.Value }.Grid(_cameraCount);
            _preview.Text = "Cameras: " + _cameraCount + "    Layout: " + grid.Height + " × " + grid.Width;
        }
    }

    public partial class FormMain
    {
        private void ApplyCameraViewLayout()
        {
            if (camViews.Count == 0) return;
            var displayed = camViews.Where((view, index) => _catalogCameraIds == null || _catalogCameraIds.Contains(index + 1)).ToList();
            Size grid = DisplayLayoutSettings.Load().Grid(displayed.Count);
            tableLayoutPanel5.SuspendLayout();
            try
            {
                // Reparent the same instances: preserve image, events, stats,
                // CamIndex, names, identities and all runtime acquisition state.
                tableLayoutPanel5.Controls.Clear();
                tableLayoutPanel5.ColumnStyles.Clear(); tableLayoutPanel5.RowStyles.Clear();
                tableLayoutPanel5.ColumnCount = grid.Width; tableLayoutPanel5.RowCount = grid.Height;
                for (int i = 0; i < grid.Width; i++) tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / grid.Width));
                for (int i = 0; i < grid.Height; i++) tableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / grid.Height));
                float scale = DesktopTheme.DisplayScale;
                tableLayoutPanel5.MinimumSize = new Size((int)(grid.Width * 180 * scale), (int)(grid.Height * 260 * scale));
                var parent = tableLayoutPanel5.Parent as ScrollableControl;
                if (parent != null) parent.AutoScroll = true;
                foreach (var view in camViews) view.Visible = displayed.Contains(view);
                for (int i = 0; i < displayed.Count; i++)
                    tableLayoutPanel5.Controls.Add(displayed[i], i % grid.Width, i / grid.Width);
            }
            finally { tableLayoutPanel5.ResumeLayout(true); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Aron_V2
{
    internal static class LogViewPreferences
    {
        internal static string FilePath = ProjectPaths.Config("General/LogView.xml");
        internal static void Load(out bool autoScroll, out LogCategory categories)
        {
            autoScroll = true; categories = LogCategory.All;
            try
            {
                if (!File.Exists(FilePath)) return;
                var root = XElement.Load(FilePath);
                if (bool.TryParse((string)root.Attribute("AutoScroll"), out bool scroll)) autoScroll = scroll;
                if (int.TryParse((string)root.Attribute("Categories"), out int flags) && flags >= 0 && flags <= (int)LogCategory.All) categories = (LogCategory)flags;
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Log view preferences: " + ex.Message); }
        }
        internal static void Save(bool autoScroll, LogCategory categories)
        {
            string temp = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                new XElement("LogView", new XAttribute("AutoScroll", autoScroll), new XAttribute("Categories", (int)categories)).Save(temp);
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Log view preferences could not be saved: " + ex.Message); }
            finally { try { if (temp != null && File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    internal sealed partial class LogPanel
    {
        private LogCategory _allCategories = LogCategory.All;
        private ContextMenuStrip _settingsMenu;
        private Button _settingsButton;
        private ToolStripMenuItem _scrollOption;
        private readonly Dictionary<LogCategory, ToolStripMenuItem> _categoryOptions = new Dictionary<LogCategory, ToolStripMenuItem>();
        private bool _syncingOptions;

        private void BuildBottomLayout()
        {
            if (_settingsMenu != null) return;
            float scale = DesktopTheme.DisplayScale;
            SuspendLayout();
            var old = Controls[0];
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.White };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32 * scale));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new TableLayoutPanel { Name = "LogToolbar", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.FromArgb(244, 247, 251) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34 * scale));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, (int)(3 * scale), 0, 0), WrapContents = false, BackColor = header.BackColor };
            for (int i = 0; i < _filters.Length; i++)
            {
                Button filter = _filters[i];
                filter.Dock = DockStyle.None;
                filter.Size = new Size((int)(new[] { 78, 100, 124 }[i] * scale), (int)(26 * scale));
                filter.Margin = Padding.Empty;
                filters.Controls.Add(filter);
            }
            _settingsButton = new Button { Name = "LogSettingsButton", Anchor = AnchorStyles.None, Size = new Size((int)(28 * scale), (int)(26 * scale)),
                AccessibleName = "Log Settings", Margin = Padding.Empty, TabIndex = 3 };
            DesktopTheme.StyleButton(_settingsButton);
            _settingsButton.FlatAppearance.BorderColor = Color.FromArgb(211, 223, 235);
            _settingsButton.Image = DesktopTheme.CreateIcon(DesktopIcon.Settings, (int)(18 * scale), DesktopTheme.Ink);
            _settingsButton.Disposed += (s, e) => _settingsButton.Image?.Dispose();
            header.Controls.Add(filters, 0, 0); header.Controls.Add(_settingsButton, 1, 0);
            layout.Controls.Add(header, 0, 0);
            _grid.BorderStyle = BorderStyle.None;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            layout.Controls.Add(_grid, 0, 1);
            // Retain the original state object used by the scrolling algorithm,
            // but expose its option only in the gear menu, not a second footer.
            Controls.Add(_autoScroll); _autoScroll.Dock = DockStyle.None; _autoScroll.Hide();
            Controls.Remove(old); old.Dispose();
            BackColor = Color.FromArgb(244, 247, 251);
            Padding = new Padding((int)(6 * scale), (int)(4 * scale), (int)(6 * scale), (int)(4 * scale));
            var card = new LogCard { Name = "LogCard", Dock = DockStyle.Fill, Padding = new Padding((int)(6 * scale)), BackColor = Color.White };
            card.Controls.Add(layout); Controls.Add(card); card.BringToFront();
            CreateSettingsMenu();
            LogViewPreferences.Load(out bool scroll, out LogCategory categories);
            _syncingOptions = true;
            _autoScroll.Checked = scroll;
            _scrollOption.Checked = scroll;
            SetAllCategories(categories);
            _settingsButton.Click += (s, e) =>
            {
                UiLanguage.ApplyTo(_settingsMenu);
                _settingsMenu.Show(_settingsButton, new Point(_settingsButton.Width, 0), ToolStripDropDownDirection.AboveLeft);
            };
            ResumeLayout(true);
        }

        private void CreateSettingsMenu()
        {
            _settingsMenu = new ContextMenuStrip { Font = Font, ShowImageMargin = false, ShowCheckMargin = true, Padding = new Padding(5), BackColor = Color.White };
            _settingsMenu.Items.Add(new ToolStripMenuItem("Log Settings") { Enabled = false });
            _scrollOption = new ToolStripMenuItem("Auto scroll") { CheckOnClick = true };
            _scrollOption.CheckedChanged += (s, e) =>
            {
                _autoScroll.Checked = _scrollOption.Checked;
                if (!_syncingOptions) LogViewPreferences.Save(_autoScroll.Checked, _allCategories);
            };
            _settingsMenu.Items.Add(_scrollOption);
            _settingsMenu.Items.Add(new ToolStripSeparator());
            _settingsMenu.Items.Add(new ToolStripMenuItem("ALL: visible categories") { Enabled = false });
            string[] names = { "System / Startup", "Camera / Acquisition", "Algorithm / Inspection", "Communication" };
            var categories = new[] { LogCategory.System, LogCategory.Camera, LogCategory.Algorithm, LogCategory.Communication };
            for (int i = 0; i < names.Length; i++)
            {
                LogCategory category = categories[i];
                var option = new ToolStripMenuItem(names[i]) { CheckOnClick = true };
                option.CheckedChanged += (s, e) =>
                {
                    if (_syncingOptions) return;
                    SetAllCategories(option.Checked ? _allCategories | category : _allCategories & ~category);
                    LogViewPreferences.Save(_autoScroll.Checked, _allCategories);
                };
                _categoryOptions.Add(category, option); _settingsMenu.Items.Add(option);
            }
            _settingsMenu.Closing += (s, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true; };
        }

        internal void SetAllCategories(LogCategory categories)
        {
            _allCategories = categories & LogCategory.All;
            _syncingOptions = true;
            try
            {
                foreach (var pair in _categoryOptions) pair.Value.Checked = (_allCategories & pair.Key) != 0;
                if (_scrollOption != null) _scrollOption.Checked = _autoScroll.Checked;
            }
            finally { _syncingOptions = false; }
            SetFilter(_filter); // Re-filter retained history; never delete it.
        }

        private sealed class LogCard : Panel
        {
            internal LogCard() { DoubleBuffered = true; ResizeRedraw = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width < 10 || Height < 10) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float diameter = 8 * DesktopTheme.DisplayScale;
                var rect = new RectangleF(.5F, .5F, Width - 1.5F, Height - 1.5F);
                using (var path = new GraphicsPath())
                using (var pen = new Pen(Color.FromArgb(211, 223, 235)))
                {
                    path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
                    path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
                    path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
                    path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure();
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}

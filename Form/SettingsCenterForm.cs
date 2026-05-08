using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    // Navigation/hosting only. Each page retains its original controls, loading,
    // validation, Save handler and post-save callback; no production startup here.
    internal sealed class SettingsCenterForm : Form
    {
        internal sealed class Page
        {
            internal string Title;
            internal DesktopIcon Icon;
            internal Func<Form> Create;
            internal Func<bool> Allowed;
            internal Func<string> Revision;
            internal string SaveButtonName;
        }

        private sealed class CachedPage
        {
            internal Form Form;
            internal Button Save;
            internal string Revision;
            internal int Generation;
        }

        private readonly Dictionary<int, CachedPage> _cache = new Dictionary<int, CachedPage>();
        private int _generation;

        private readonly List<Page> _pages;
        private readonly List<Button> _navigation = new List<Button>();
        private readonly Panel _content;
        private readonly Label _heading;
        private readonly Label _status;
        private readonly Panel _footer;
        private Form _page;
        private int _selected = -1;
        private bool _dirty;
        private bool _ready;
        private readonly Font _body = new Font("Segoe UI", 9F);
        private readonly Font _title = new Font("Segoe UI", 14F, FontStyle.Bold);

        internal SettingsCenterForm(IEnumerable<Page> pages, string initialPage)
        {
            _pages = pages.ToList();
            float scale = DesktopTheme.DisplayScale;
            Text = "Settings";
            Name = "SettingsCenter";
            AutoScaleMode = AutoScaleMode.Dpi;
            // Construct at the current system DPI. Unlike designer-generated
            // forms these controls are created after Font has already changed.
            AutoScaleDimensions = new SizeF(96 * scale, 96 * scale);
            Font = _body;
            BackColor = DesktopTheme.Surface;
            ForeColor = DesktopTheme.Ink;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size((int)(1120 * scale), (int)(720 * scale));
            MinimumSize = new Size((int)(840 * scale), (int)(560 * scale));
            DoubleBuffered = true;

            var layout = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190 * scale));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var sidebar = new FlowLayoutPanel { Name = "SettingsNavigation", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(0, (int)(12 * scale), 0, 0), Margin = Padding.Empty, BackColor = Color.White };
            sidebar.Paint += (s, e) => { using (var pen = new Pen(DesktopTheme.Border)) e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height); };
            foreach (Page page in _pages)
            {
                int index = _navigation.Count;
                var button = new Button { Text = "   " + page.Title, Name = "SettingsNav" + index, Height = (int)(48 * scale), Width = (int)(188 * scale),
                    Margin = new Padding(0, 1, 1, 1), TextAlign = ContentAlignment.MiddleLeft,
                    ImageAlign = ContentAlignment.MiddleLeft, TextImageRelation = TextImageRelation.ImageBeforeText,
                    Padding = new Padding((int)(14 * scale), 0, (int)(4 * scale), 0), TabIndex = index, AccessibleName = page.Title };
                DesktopTheme.StyleButton(button);
                button.FlatAppearance.BorderSize = 0;
                button.Image = DesktopTheme.CreateIcon(page.Icon, (int)(22 * scale), DesktopTheme.Ink);
                button.Disposed += (s, e) => button.Image?.Dispose();
                button.Click += (s, e) => SelectPage(index);
                button.Paint += (s, e) => { if (_selected == index) using (var brush = new SolidBrush(DesktopTheme.Accent)) e.Graphics.FillRectangle(brush, 0, 0, Math.Max(3, (int)(3 * DeviceDpi / 96F)), button.Height); };
                _navigation.Add(button);
                sidebar.Controls.Add(button);
            }
            sidebar.SizeChanged += (s, e) => { foreach (Button button in _navigation) button.Width = Math.Max(100, sidebar.ClientSize.Width - (sidebar.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0) - 2); };

            var right = new BufferedTable { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, Padding = new Padding((int)(12 * scale), 0, (int)(12 * scale), 0) };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54 * scale));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 52 * scale));
            _heading = new Label { Name = "SettingsHeading", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = _title, Margin = Padding.Empty };
            _content = new BufferedPanel { Name = "SettingsContent", Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            _status = new Label { Name = "SettingsStatus", TextAlign = ContentAlignment.MiddleLeft, ForeColor = DesktopTheme.Muted, Margin = Padding.Empty };
            _footer = new BufferedPanel { Name = "SettingsFooter", Dock = DockStyle.Fill, Margin = Padding.Empty };
            _footer.Controls.Add(_status);
            _footer.SizeChanged += (s, e) => PositionSaveButton();
            right.Controls.Add(_heading, 0, 0); right.Controls.Add(_content, 0, 1); right.Controls.Add(_footer, 0, 2);
            layout.Controls.Add(sidebar, 0, 0); layout.Controls.Add(right, 1, 0);
            Controls.Add(layout);
            Shown += (s, e) =>
            {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)), Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
                RefreshPermissions();
                int first = _pages.FindIndex(p => p.Title == initialPage && (p.Allowed == null || p.Allowed()));
                if (first < 0) first = _pages.FindIndex(p => p.Allowed == null || p.Allowed());
                if (first >= 0) SelectPage(first);
            };
            FormClosing += (s, e) => { if (!ConfirmLeave()) e.Cancel = true; };
            Disposed += (s, e) => { foreach (var page in _cache.Values) { page.Save?.Dispose(); page.Form.Dispose(); } _cache.Clear(); _body.Dispose(); _title.Dispose(); };
        }

        internal void RefreshPermissions()
        {
            for (int i = 0; i < _pages.Count; i++) _navigation[i].Enabled = _pages[i].Allowed == null || _pages[i].Allowed();
        }

        internal void MarkSaved()
        {
            _dirty = false;
            _generation++;
            if (_cache.TryGetValue(_selected, out var saved))
            {
                saved.Generation = _generation;
                saved.Revision = _pages[_selected].Revision?.Invoke();
            }
            if (_page != null) ResetTextModified(_page);
            _status.Text = "Saved. Changes use the existing application save behavior.";
        }

        private static void ResetTextModified(Control control)
        {
            var input = control as TextBoxBase;
            if (input != null) input.Modified = false;
            foreach (Control child in control.Controls) ResetTextModified(child);
        }

        private void MarkEdited()
        {
            if (!_ready || _page == null || _page.UseWaitCursor) return;
            _dirty = true;
            _status.Text = "Unsaved changes — use this page's Save button before switching.";
        }

        private bool ConfirmLeave()
        {
            if (_page is JobControlForm catalog && catalog.IsBusy) return false;
            if (!_dirty) return true;
            return MessageBox.Show(this, UiLanguage.Text("This page may contain unsaved changes. Discard them and continue?\r\n\r\nChoose No to return and use the page's Save button."),
                UiLanguage.Text("Unsaved settings"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        internal void SelectPage(int index)
        {
            if (index < 0 || index >= _pages.Count || index == _selected) return;
            if (_pages[index].Allowed != null && !_pages[index].Allowed()) return;
            if (!ConfirmLeave()) return;
            Form next = null;
            CachedPage target = null;
            bool created = false;
            _content.SuspendLayout();
            _footer.SuspendLayout();
            try
            {
                string revision = _pages[index].Revision?.Invoke();
                if (_cache.TryGetValue(index, out target) && (target.Form.IsDisposed || target.Generation != _generation || target.Revision != revision))
                {
                    target.Save?.Dispose(); target.Form.Dispose(); _cache.Remove(index); target = null;
                }
                if (target == null)
                {
                    // Lazy first visit; clean pages are reused until a save or
                    // external configuration change invalidates their revision.
                    next = _pages[index].Create();
                    target = new CachedPage { Form = next, Revision = revision, Generation = _generation };
                    created = true;
                next.TopLevel = false;
                next.FormBorderStyle = FormBorderStyle.None;
                next.ShowInTaskbar = false;
                next.MinimumSize = Size.Empty;
                next.MaximumSize = Size.Empty;
                next.Dock = DockStyle.Fill;
                next.Margin = Padding.Empty;
                next.AutoScroll = true;
                _content.Controls.Add(next);
                }
                next = target.Form;
                _ready = false;
                next.Show();
                if (created)
                {
                    target.Save = FindSaveButton(next, _pages[index].SaveButtonName);
                    if (target.Save != null)
                    {
                        Control oldParent = target.Save.Parent;
                        _footer.Controls.Add(target.Save);
                        if (oldParent is FlowLayoutPanel && oldParent.Controls.Count == 0) oldParent.Hide();
                        DesktopTheme.StyleButton(target.Save, true);
                        target.Save.AutoSize = false;
                        target.Save.MinimumSize = Size.Empty;
                        target.Save.MaximumSize = Size.Empty;
                        target.Save.Dock = DockStyle.None;
                        target.Save.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                        target.Save.Margin = Padding.Empty;
                        target.Save.Padding = Padding.Empty;
                    }
                    _cache[index] = target;
                }
                next.BringToFront();
                var previous = _page;
                int previousIndex = _selected;
                bool discardPrevious = _dirty;
                _page = next;
                if (previous != null)
                {
                    previous.Hide();
                    if (_cache.TryGetValue(previousIndex, out var old)) old.Save?.Hide();
                    if (discardPrevious)
                    {
                        if (_cache.TryGetValue(previousIndex, out old)) old.Save?.Dispose();
                        previous.Dispose(); _cache.Remove(previousIndex);
                    }
                }
                _selected = index;
                if (target.Save != null) { target.Save.Show(); target.Save.BringToFront(); }
                PositionSaveButton();
                _heading.Text = _pages[index].Title;
                _dirty = false;
                _status.Text = next is JobControlForm ? "Operations save immediately and refresh the settings pages." : "Changes apply after Save. Existing files and settings are retained.";
                if (created) ObserveEdits(next);
                _ready = true;
                for (int i = 0; i < _navigation.Count; i++)
                {
                    _navigation[i].BackColor = i == index ? DesktopTheme.Hover : Color.White;
                    _navigation[i].ForeColor = i == index ? DesktopTheme.Accent : DesktopTheme.Ink;
                    _navigation[i].Invalidate();
                }
            }
            catch (Exception ex)
            {
                if (created) { target?.Save?.Dispose(); next?.Dispose(); _cache.Remove(index); }
                _ready = _page != null;
                MessageBox.Show(this, ex.GetBaseException().Message, "Open settings failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _content.ResumeLayout(true); _footer.ResumeLayout(true); }
        }

        private static Button FindSaveButton(Form form, string name)
        {
            return string.IsNullOrEmpty(name) ? null : form.Controls.Find(name, true).OfType<Button>().FirstOrDefault();
        }

        private void PositionSaveButton()
        {
            float scale = DesktopTheme.DisplayScale;
            _status.Bounds = new Rectangle(0, 0, Math.Max(0, _footer.ClientSize.Width - (int)(118 * scale)), _footer.ClientSize.Height);
            if (!_cache.TryGetValue(_selected, out var page) || page.Save == null) return;
            page.Save.Size = new Size((int)(100 * scale), (int)(32 * scale));
            page.Save.Location = new Point(Math.Max(0, _footer.ClientSize.Width - page.Save.Width - (int)(8 * scale)), Math.Max(0, (_footer.ClientSize.Height - page.Save.Height) / 2));
        }

        private sealed class BufferedPanel : Panel { internal BufferedPanel() { DoubleBuffered = true; } }
        private sealed class BufferedTable : TableLayoutPanel { internal BufferedTable() { DoubleBuffered = true; } }

        private void ObserveEdits(Control control)
        {
            if (control is JobControlForm) return; // Catalog operations commit individually.
            var grid = control as DataGridView;
            if (grid != null)
            {
                grid.CellBeginEdit += (s, e) => MarkEdited();
                grid.UserDeletedRow += (s, e) => MarkEdited();
                return; // Don't confuse data binding/selection with user edits.
            }
            var input = control as TextBoxBase;
            if (input != null) input.TextChanged += (s, e) => { if (input.Focused || input.Modified) MarkEdited(); };
            var check = control as CheckBox;
            if (check != null) check.CheckedChanged += (s, e) => { if (check.Focused) MarkEdited(); };
            var combo = control as ComboBox;
            // Job/Camera selectors select context, not parameter edits.
            if (combo != null && _page is ImageRecord) combo.SelectionChangeCommitted += (s, e) => MarkEdited();
            var number = control as NumericUpDown;
            if (number != null) number.ValueChanged += (s, e) => { if (number.ContainsFocus) MarkEdited(); };
            var button = control as Button;
            if (button != null && (button.Name.IndexOf("Add", StringComparison.OrdinalIgnoreCase) >= 0 ||
                button.Name.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0 || button.Name == "btnBrowse"))
                button.Click += (s, e) => MarkEdited();
            foreach (Control child in control.Controls) ObserveEdits(child);
        }
    }
}

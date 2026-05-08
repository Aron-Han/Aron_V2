using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Aron_V2
{
    internal static partial class DesktopTheme
    {
        private static readonly ConditionalWeakTable<Form, SettingsThemeScope> SettingsScopes = new ConditionalWeakTable<Form, SettingsThemeScope>();

        internal static void ApplySettings(Form form)
        {
            // Explicit opt-in: do not change the main inspection/status screen.
            SettingsScopes.GetValue(form, owner => new SettingsThemeScope(owner));
        }

        internal static void RemoveEditorChannelFromToolbar(TableLayoutPanel toolbar, Control caption, ComboBox selector)
        {
            // Keep the existing internal selection/cache key and its handlers.
            // Only remove the redundant editing surface and its reserved space.
            toolbar.SuspendLayout();
            caption.Visible = selector.Visible = false;
            selector.TabStop = false;
            foreach (Control control in new Control[] { caption, selector })
            {
                int column = toolbar.GetColumn(control);
                toolbar.ColumnStyles[column].SizeType = SizeType.Absolute;
                toolbar.ColumnStyles[column].Width = 0;
            }
            toolbar.ResumeLayout(true);
        }

        private sealed class SettingsThemeScope
        {
            private readonly Form _owner;
            private readonly Font _body = new Font("Segoe UI", 9F);
            private Font _header;
            private readonly HashSet<Control> _observed = new HashSet<Control>();
            private bool _shown;
            private bool _applying;

            internal SettingsThemeScope(Form owner)
            {
                _owner = owner;
                _header = new Font(_body, FontStyle.Bold);
                owner.SuspendLayout();
                owner.Font = _body;
                owner.BackColor = Surface;
                owner.ForeColor = Ink;
                ApplySurfaceColors(owner);
                Visit(owner);
                owner.ResumeLayout(true);
                // Existing Load handlers still run normally, before this final
                // visual pass. Nothing is polled or rebound during later paints.
                owner.Shown += (s, e) => { _shown = true; Refresh(); };
                owner.FontChanged += (s, e) =>
                {
                    if (owner.IsDisposed || owner.Disposing) return;
                    Font previous = _header;
                    _header = new Font(owner.Font, FontStyle.Bold);
                    Refresh();
                    previous.Dispose();
                };
                owner.Disposed += (s, e) => { _header.Dispose(); _body.Dispose(); _observed.Clear(); };
            }

            private void Refresh()
            {
                if (_applying || _owner.IsDisposed || _owner.Disposing) return;
                _applying = true;
                _owner.SuspendLayout();
                try { Visit(_owner); }
                finally { _owner.ResumeLayout(true); _applying = false; }
            }

            private void Visit(Control control)
            {
                // Vendor editors own their children and acquisition/display UI.
                string ns = control.GetType().Namespace ?? string.Empty;
                if (!ns.StartsWith("System.Windows.Forms", StringComparison.Ordinal) && !ns.StartsWith("Aron_V2", StringComparison.Ordinal) && !ReferenceEquals(control, _owner)) return;
                var grid = control as DataGridView;
                if (grid != null) { StyleGrid(grid); return; }
                var button = control as Button;
                if (button != null)
                {
                    string text = button.Text.Replace("&", "").Trim();
                    bool primary = text.StartsWith("Save", StringComparison.OrdinalIgnoreCase) || text == "Apply" || text == "OK" || text == "Calculate" || text == "保存" || text == "确定";
                    if (IsStatusColor(button.BackColor) && !(primary && button.BackColor == Accent)) return;
                    StyleButton(button, primary);
                    if (!primary && (text.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0 || text == "Clear All")) button.ForeColor = Color.FromArgb(185, 45, 53);
                    button.Font = _owner.Font;
                    if (_shown && IsFlexibleButton(button))
                    {
                        float scale = DisplayScale;
                        button.AutoSize = true;
                        button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, (int)(80 * scale)), Math.Max(button.MinimumSize.Height, Math.Max((int)(30 * scale), button.Font.Height + (int)(12 * scale))));
                        button.Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale));
                        button.Margin = new Padding((int)(4 * scale));
                    }
                    return;
                }
                var rich = control as RichTextBox;
                if (rich != null) { rich.BackColor = Color.White; rich.BorderStyle = BorderStyle.FixedSingle; return; }
                var textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.Font = _owner.Font;
                    textBox.BackColor = textBox.ReadOnly && !textBox.Multiline ? Color.FromArgb(244, 247, 250) : Color.White;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    return;
                }
                var number = control as NumericUpDown;
                if (number != null) { number.Font = _owner.Font; number.BackColor = Color.White; number.BorderStyle = BorderStyle.FixedSingle; return; }
                var combo = control as ComboBox;
                if (combo != null) { combo.Font = _owner.Font; combo.BackColor = Color.White; return; }
                var list = control as ListBox;
                if (list != null) { list.Font = _owner.Font; list.BackColor = Color.White; return; }
                if (control is Label || control is CheckBox || control is RadioButton || control is GroupBox || control is TabControl) control.Font = _owner.Font;
                if ((control is Panel || control is GroupBox || control is TabPage) && !IsStatusColor(control.BackColor)) control.BackColor = Color.White;
                if (_observed.Add(control))
                {
                    control.ControlAdded += (s, e) => Visit(e.Control);
                    control.Disposed += (s, e) => _observed.Remove(control);
                    if (control is TableLayoutPanel || control is FlowLayoutPanel)
                        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(control, true, null);
                }
                foreach (Control child in control.Controls) Visit(child);
                if (_shown && control is FlowLayoutPanel && control.Name == "AlignedSelectorToolbar")
                    AlignSelectorToolbar((FlowLayoutPanel)control);
            }

            private static void AlignSelectorToolbar(FlowLayoutPanel toolbar)
            {
                // Native combo boxes determine their own height. Match the
                // adjacent buttons to that height, without owner-drawing or
                // replacing selectors (and their selection events).
                float scale = DisplayScale;
                int height = 0;
                foreach (Control child in toolbar.Controls)
                    if (child is ComboBox) height = Math.Max(height, child.PreferredSize.Height);
                if (height == 0) return;
                int gap = (int)(4 * scale);
                toolbar.SuspendLayout();
                foreach (Control child in toolbar.Controls)
                {
                    var button = child as Button;
                    if (button != null)
                    {
                        int width = Math.Max(button.MinimumSize.Width, button.PreferredSize.Width);
                        button.AutoSize = false;
                        button.MinimumSize = new Size(button.MinimumSize.Width, 0);
                        button.Padding = new Padding((int)(10 * scale), 0, (int)(10 * scale), 0);
                        button.Size = new Size(width, height);
                    }
                    if (child is ComboBox) child.Width = Math.Max(child.Width, (int)(90 * scale));
                    int top = gap + (child is Label ? Math.Max(0, (height - child.PreferredSize.Height) / 2) : 0);
                    child.Margin = new Padding(gap, top, gap, gap);
                }
                toolbar.ResumeLayout(true);
            }

            private void StyleGrid(DataGridView grid)
            {
                // Never rebuild columns/bindings or alter values, permissions,
                // validation handlers, row tags or per-row/per-cell warning colors.
                grid.Font = _owner.Font;
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = Color.White;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.GridColor = Color.FromArgb(222, 229, 237);
                grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 242, 248);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(236, 242, 248);
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
                grid.ColumnHeadersDefaultCellStyle.Font = _header;
                grid.DefaultCellStyle.Font = _owner.Font;
                grid.DefaultCellStyle.BackColor = Color.White;
                grid.DefaultCellStyle.ForeColor = Ink;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 236, 253);
                grid.DefaultCellStyle.SelectionForeColor = Ink;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
                grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 247, 250);
                grid.RowHeadersDefaultCellStyle.ForeColor = Muted;
                if (_shown)
                {
                    int padding = Math.Max(2, (int)(2 * DisplayScale));
                    grid.DefaultCellStyle.Padding = new Padding(padding + 2, padding, padding + 2, padding);
                    grid.ColumnHeadersHeight = Math.Max(grid.ColumnHeadersHeight, _header.Height + padding * 4);
                    int minimumRowHeight = _owner.Font.Height + padding * 2 + 4;
                    grid.RowTemplate.MinimumHeight = Math.Max(grid.RowTemplate.MinimumHeight, minimumRowHeight);
                    grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, minimumRowHeight);
                    foreach (DataGridViewRow row in grid.Rows)
                        row.MinimumHeight = Math.Max(row.MinimumHeight, minimumRowHeight);
                }
                typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(grid, true, null);
            }

            private static bool IsStatusColor(Color color)
            {
                return color.A != 0 && color.GetSaturation() > 0.35F && color.GetBrightness() < 0.85F;
            }

            private static bool IsFlexibleButton(Button button)
            {
                if (button.Parent is FlowLayoutPanel) return true;
                var table = button.Parent as TableLayoutPanel;
                if (table == null || !table.AutoSize) return false;
                int row = table.GetRow(button);
                return row >= 0 && row < table.RowStyles.Count && table.RowStyles[row].SizeType == SizeType.AutoSize;
            }
        }
    }

    // The original S7 connection form uses a legacy source-file encoding. Keep
    // that file byte-identical and apply the UI-only hook in this partial class.
    public partial class S7SettingForm
    {
        protected override void OnLoad(EventArgs e)
        {
            DesktopTheme.ApplySettings(this);
            base.OnLoad(e);
        }
    }
}

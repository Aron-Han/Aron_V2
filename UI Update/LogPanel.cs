using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal enum LogViewFilter { All, Warning, Communicate }
    [Flags] internal enum LogCategory { System = 1, Camera = 2, Algorithm = 4, Communication = 8, All = 15 }

    internal sealed class LogEntry
    {
        internal DateTime Time;
        internal string Message;
        internal bool Warning;
        internal bool Error;
        internal bool Communication;
        internal LogCategory Category;
        internal string Type { get { return Warning ? "WARN" : Communication ? "COMM" : "INFO"; } }
        internal string Summary
        {
            get
            {
                string first = Message.Split(new[] { '\r', '\n' }, 2)[0];
                return first.Length > 180 ? first.Substring(0, 180) + "…" : first + (first.Length < Message.Length ? " …" : "");
            }
        }
        internal static LogEntry Create(string message, Color color, DateTime time)
        {
            message = message ?? string.Empty;
            string text = message.ToLowerInvariant();
            string[] communication = { "communication", "communicate", "profinet", "tcp/ip", "tcpip", "plc", "s7", "cc24", "heartbeat", "handshake", "trigger ready", "trigger enable", "setuserdata", "clearresult", "receive trigger", "重连", "通讯" };
            string[] warning = { "failed", "error", "exception", "timeout", "offline", "not connected", "not found", "异常", "警告", "失败", "超时", "断开", "丢失" };
            bool isCommunication = communication.Any(text.Contains) || System.Text.RegularExpressions.Regex.IsMatch(message, "(?<!普)通信");
            LogCategory category = isCommunication ? LogCategory.Communication :
                new[] { "camera", "capture", "acquisition", "gocator", "gosdk", "lmi", "相机", "取像" }.Any(text.Contains) || System.Text.RegularExpressions.Regex.IsMatch(text, @"\bcam\d+\b") ? LogCategory.Camera :
                new[] { "algorithm", "inspection", "vpp", "算法", "检测" }.Any(text.Contains) ? LogCategory.Algorithm : LogCategory.System;
            return new LogEntry { Time = time, Message = message, Category = category,
                Error = color.ToArgb() == Color.Red.ToArgb() || new[] { "failed", "error", "exception", "失败", "异常" }.Any(text.Contains),
                Communication = isCommunication,
                Warning = color.ToArgb() == Color.Red.ToArgb() || color.ToArgb() == Color.OrangeRed.ToArgb() || color.ToArgb() == Color.Orange.ToArgb() || color.ToArgb() == Color.DarkOrange.ToArgb() || warning.Any(text.Contains) };
        }
    }

    // Presentation-only log buffer. Publishers and persistent file logging are
    // unchanged; category inference deliberately lives here, not in the transport.
    internal sealed partial class LogPanel : UserControl
    {
        private const int Capacity = 2000;
        private readonly Queue<LogEntry> _pending = new Queue<LogEntry>();
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly List<LogEntry> _visible = new List<LogEntry>();
        private readonly DataGridView _grid;
        private readonly CheckBox _autoScroll;
        private readonly Button[] _filters;
        private readonly Timer _refresh;
        private readonly Font _body = new Font("Segoe UI", 9F);
        private readonly Font _title = new Font("Segoe UI", 12F, FontStyle.Bold);
        private LogViewFilter _filter;
        private ContextMenuStrip _clearMenu;
        internal int RetainedCount { get { return _entries.Count; } }
        internal int VisibleCount { get { return _visible.Count; } }

        internal void UseBottomLayout()
        {
            BuildBottomLayout();
        }
        internal string VisibleMessage(int index) { return _visible[index].Message; }

        internal LogPanel()
        {
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = DesktopTheme.Surface; Font = _body; Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8), Margin = Padding.Empty, BackColor = BackColor };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(new Label { Text = "Log", Font = _title, ForeColor = DesktopTheme.Ink, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
            var filters = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 6) };
            _filters = new Button[3];
            string[] names = { "ALL", "Warning", "Communicate" };
            for (int i = 0; i < names.Length; i++)
            {
                // Reserve more room for the longest label in the narrow main pane.
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, new[] { 24F, 32F, 44F }[i]));
                int index = i;
                var button = new Button { Text = names[i], Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true, TabIndex = i, AccessibleName = "Log filter " + names[i] };
                button.Click += (s, e) => SetFilter((LogViewFilter)index);
                _filters[i] = button; filters.Controls.Add(button, i, 0);
            }
            layout.Controls.Add(filters, 0, 1);
            _grid = new BufferedLogGrid { Dock = DockStyle.Fill, Margin = Padding.Empty, VirtualMode = true, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, GridColor = Color.FromArgb(232, 237, 243), EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal };
            foreach (string name in new[] { "Time", "Type", "Message" }) _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = name, SortMode = DataGridViewColumnSortMode.NotSortable });
            _grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.DefaultCellStyle.ForeColor = DesktopTheme.Ink;
            _grid.DefaultCellStyle.SelectionBackColor = DesktopTheme.Hover;
            _grid.DefaultCellStyle.SelectionForeColor = DesktopTheme.Ink;
            _grid.DefaultCellStyle.Padding = new Padding(4);
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 242, 248);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = DesktopTheme.Ink;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(236, 242, 248);
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.CellValueNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _visible.Count) return;
                LogEntry row = _visible[e.RowIndex];
                e.Value = e.ColumnIndex == 0 ? row.Time.ToString("HH:mm:ss.fff") : e.ColumnIndex == 1 ? row.Type : row.Summary;
            };
            _grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _visible.Count) return;
                LogEntry row = _visible[e.RowIndex];
                e.CellStyle.ForeColor = row.Error ? Color.FromArgb(185, 45, 53) : row.Warning ? Color.FromArgb(180, 100, 20) : row.Communication ? DesktopTheme.Accent : DesktopTheme.Ink;
                e.CellStyle.BackColor = row.Error ? Color.FromArgb(255, 239, 240) : row.Warning ? Color.FromArgb(255, 247, 235) : e.RowIndex % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 253);
            };
            _grid.CellToolTipTextNeeded += (s, e) => { if (e.RowIndex >= 0) e.ToolTipText = "Double-click to view/copy the full log message."; };
            _grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _visible.Count || e.ColumnIndex != 1) return;
                LogEntry row = _visible[e.RowIndex];
                e.PaintBackground(e.ClipBounds, true);
                Color fill = row.Error ? Color.FromArgb(255, 212, 216) : row.Warning ? Color.FromArgb(255, 225, 167) : row.Communication ? Color.FromArgb(218, 237, 255) : Color.FromArgb(232, 237, 243);
                var badge = new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y + 4, e.CellBounds.Width - 10, e.CellBounds.Height - 8);
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, badge);
                TextRenderer.DrawText(e.Graphics, row.Type, _grid.Font, badge, e.CellStyle.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Border); e.Handled = true;
            };
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && e.RowIndex < _visible.Count) ShowDetails(_visible[e.RowIndex]); };
            _clearMenu=new ContextMenuStrip { ShowImageMargin=false };
            var clear=new ToolStripMenuItem("Clear Log");clear.Click+=(s,e)=>ClearDisplayLog();
            _clearMenu.Items.Add(clear);_clearMenu.Opening+=(s,e)=>clear.Text=UiLanguage.Text("Clear Log");
            _grid.ContextMenuStrip=_clearMenu;
            layout.Controls.Add(_grid, 0, 2);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 5, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _autoScroll = new CheckBox { Text = "Auto scroll", Checked = true, Dock = DockStyle.Fill, Margin = Padding.Empty, AutoSize = true };
            footer.Controls.Add(_autoScroll, 0, 0); layout.Controls.Add(footer, 0, 3);
            Controls.Add(layout);
            _refresh = new Timer { Interval = 100 }; _refresh.Tick += (s, e) => FlushPending();
            SetFilter(LogViewFilter.All);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (LicenseManager.UsageMode != LicenseUsageMode.Designtime) _refresh.Start();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_grid == null) return;
            _grid.Columns[0].Width = TextRenderer.MeasureText("00:00:00.000", Font).Width + 12;
            _grid.Columns[1].Width = TextRenderer.MeasureText("COMM", Font).Width + 16;
            _grid.RowTemplate.Height = Font.Height + 16;
            _grid.ColumnHeadersHeight = Font.Height + 18;
        }

        internal void Append(string message, Color color)
        {
            if (IsDisposed || Disposing) return;
            var entry = LogEntry.Create(message, color, DateTime.Now);
            if(!OperatorRelevant(entry)) return;
            lock (_pending) { if (_pending.Count == Capacity) _pending.Dequeue(); _pending.Enqueue(entry); }
        }

        private static bool OperatorRelevant(LogEntry entry)
        {
            // Keep routine editor/cache/loading steps out of the operator panel.
            // Disk diagnostics and publishers are unaffected by this presentation filter.
            bool recovery=entry.Message.StartsWith("Camera recovery: ",StringComparison.Ordinal)||entry.Message.StartsWith("相机恢复：",StringComparison.Ordinal);
            bool result=entry.Message.StartsWith("Result: ",StringComparison.Ordinal)||entry.Message.IndexOf("Inspection result:",StringComparison.OrdinalIgnoreCase)>=0||entry.Message.Contains("检测结果：");
            return entry.Warning||entry.Communication||recovery||result;
        }
        private bool Matches(LogEntry entry)
        {
            if(!OperatorRelevant(entry)) return false;
            return _filter == LogViewFilter.All ? (_allCategories & entry.Category) != 0 : (_filter == LogViewFilter.Warning ? entry.Warning : entry.Communication);
        }

        internal void FlushPending()
        {
            if (IsDisposed || Disposing) return;
            LogEntry[] batch;
            lock (_pending) { if (_pending.Count == 0) return; batch = _pending.ToArray(); _pending.Clear(); }
            int first = _grid.FirstDisplayedScrollingRowIndex;
            LogEntry firstEntry = first >= 0 && first < _visible.Count ? _visible[first] : null;
            foreach(var entry in batch)
            {
                if(entry.Message.StartsWith("Camera recovery: ",StringComparison.Ordinal)||entry.Message.StartsWith("相机恢复：",StringComparison.Ordinal))
                    _entries.RemoveAll(e=>e.Message.StartsWith("Camera recovery: ",StringComparison.Ordinal)||e.Message.StartsWith("相机恢复：",StringComparison.Ordinal));
                _entries.Add(entry);
            }
            if (_entries.Count > Capacity) _entries.RemoveRange(0, _entries.Count - Capacity);
            // Bounded rebuild of references, not of DataGridView rows/columns.
            _visible.Clear(); _visible.AddRange(_entries.AsEnumerable().Reverse().Where(Matches).OrderByDescending(entry => entry.Time));
            _grid.RowCount = _visible.Count;
            if (_visible.Count != 0)
            {
                int preserved = firstEntry == null ? -1 : _visible.IndexOf(firstEntry);
                int target = _autoScroll.Checked ? 0 : preserved >= 0 ? preserved : Math.Max(0, Math.Min(first, _visible.Count - 1));
                _grid.FirstDisplayedScrollingRowIndex = target;
            }
            _grid.Invalidate();
        }

        internal void SetFilter(LogViewFilter filter)
        {
            _filter = filter;
            for (int i = 0; i < _filters.Length; i++) DesktopTheme.StyleButton(_filters[i], i == (int)filter);
            _visible.Clear(); _visible.AddRange(_entries.AsEnumerable().Reverse().Where(Matches).OrderByDescending(entry => entry.Time));
            _grid.RowCount = _visible.Count;
            if (_autoScroll.Checked && _visible.Count > 0) _grid.FirstDisplayedScrollingRowIndex = 0;
            _grid.Invalidate();
        }

        private void ShowDetails(LogEntry entry)
        {
            using (var dialog = new LogDetailsForm(entry)) dialog.ShowDialog(this);
        }
        internal void ClearDisplayLog()
        {
            lock(_pending) _pending.Clear();
            _entries.Clear();_visible.Clear();_grid.RowCount=0;_grid.Invalidate();
            OperationAudit.Record("Log","Clear display","Main log display cleared; persistent logs retained.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refresh.Stop(); _refresh.Dispose(); _settingsMenu?.Dispose(); _clearMenu?.Dispose(); }
            base.Dispose(disposing);
            if (disposing) { _body.Dispose(); _title.Dispose(); }
        }
        private sealed class BufferedLogGrid : DataGridView { internal BufferedLogGrid() { DoubleBuffered = true; } }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using System.Xml.Linq;

namespace Aron_V2
{
    public partial class FormMain
    {
        private LogPanel _logPanel;
        private SplitContainer _inspectionLogSplit;
        private bool _splitInitialized;
        private bool _splitUserMoving;
        private bool _splitReady;
        private double _savedLogHeight = LogSplitterPreferences.Load();
        private void InitializeOnlineStateButton()
        {
            // Keep a native Button in InitializeComponent so the VS designer
            // never needs to instantiate a private runtime-only control.
            Button original = button3;
            Control parent = original.Parent;
            if (parent == null) return;
            var table = parent as TableLayoutPanel;
            var position = table == null ? new TableLayoutPanelCellPosition() : table.GetPositionFromControl(original);
            int columnSpan = table == null ? 1 : table.GetColumnSpan(original), rowSpan = table == null ? 1 : table.GetRowSpan(original);
            int childIndex = parent.Controls.GetChildIndex(original);
            var styled = new OnlineStateButton
            {
                Name = original.Name, Text = original.Text, Font = original.Font,
                Bounds = original.Bounds, Dock = original.Dock, Anchor = original.Anchor,
                Margin = original.Margin, Padding = original.Padding,
                TabIndex = original.TabIndex, TabStop = original.TabStop,
                Enabled = original.Enabled,
                BackColor = original.BackColor, ForeColor = original.ForeColor,
                AccessibleName = original.AccessibleName, AccessibleDescription = original.AccessibleDescription
            };
            parent.SuspendLayout();
            try
            {
                original.Click -= button3_Click;
                styled.Click += button3_Click;
                parent.Controls.Remove(original);
                button3 = styled;
                if (table == null) parent.Controls.Add(styled);
                else
                {
                    table.Controls.Add(styled, position.Column, position.Row);
                    table.SetColumnSpan(styled, columnSpan); table.SetRowSpan(styled, rowSpan);
                }
                parent.Controls.SetChildIndex(styled, childIndex);
            }
            finally { parent.ResumeLayout(true); original.Dispose(); }
        }

        private void InitializeLogPanel()
        {
            _logPanel = new LogPanel { Name = "logPanel", Margin = richTextBox1.Margin, TabIndex = richTextBox1.TabIndex };
            _logPanel.UseBottomLayout();
            tableLayoutPanel1.Controls.Remove(richTextBox1);
            richTextBox1.Visible = false; // Keep the designer field for compatibility.
            float scale = DesktopTheme.DisplayScale;
            tableLayoutPanel1.SuspendLayout();
            tableLayoutPanel1.Controls.Remove(menuStrip1);
            tableLayoutPanel1.Controls.Remove(tableLayoutPanel5);
            tableLayoutPanel1.Controls.Remove(button3);
            tableLayoutPanel1.Controls.Remove(statusStrip1);
            tableLayoutPanel1.ColumnStyles.Clear();
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tableLayoutPanel1.RowStyles.Clear();
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header = new TableLayoutPanel { Name = "MainHeader", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240 * scale));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.Controls.Add(menuStrip1, 0, 0);
            // The designer menu previously spanned four root columns. Clear
            // that layout metadata when moving it into the two-column header.
            header.SetColumnSpan(menuStrip1, 1);
            header.SetRowSpan(menuStrip1, 1);
            var onlineHost = new Panel { Name = "OnlineHeaderHost", Dock = DockStyle.Fill, Margin = Padding.Empty };
            header.Controls.Add(onlineHost, 1, 0);
            button3.Dock = DockStyle.None;
            button3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            var onlineFont = new Font("Segoe UI", 12F);
            button3.Font = onlineFont;
            Disposed += (s, e) => onlineFont.Dispose();
            button3.Size = new Size((int)(224 * scale), (int)(46 * scale));
            onlineHost.Controls.Add(button3);
            Action positionOnline = () =>
            {
                button3.Size = new Size((int)(224 * scale), (int)(46 * scale));
                int center = menuStrip1.Items.Count == 0 ? onlineHost.Height / 2 : menuStrip1.Items[0].Bounds.Top + menuStrip1.Items[0].Height / 2;
                Point location = new Point(Math.Max(0, onlineHost.Width - button3.Width - (int)(8 * scale)), Math.Max(0, center - button3.Height / 2));
                if (button3.Location != location) button3.Location = location;
            };
            onlineHost.SizeChanged += (s, e) => positionOnline();
            onlineHost.Layout += (s, e) => positionOnline();
            menuStrip1.Layout += (s, e) => positionOnline();
            positionOnline();
            // Reparent the exact Online/Offline button. Its Click handler,
            // enable state and red/green/orange colors remain runtime-owned.
            _inspectionLogSplit = new InspectionSplitContainer { Name = "InspectionLogSplit", Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2, SplitterWidth = Math.Max(8, (int)(10 * scale)), Margin = Padding.Empty,
                BackColor = Color.FromArgb(195, 207, 219), Panel1MinSize = 0, Panel2MinSize = 0 };
            _inspectionLogSplit.Panel1.BackColor = _inspectionLogSplit.Panel2.BackColor = Color.White;
            _inspectionLogSplit.Panel1.Controls.Add(tableLayoutPanel5);
            _inspectionLogSplit.Panel2.Controls.Add(_logPanel);
            tableLayoutPanel5.Dock = DockStyle.Fill;
            tableLayoutPanel1.SetColumnSpan(header, 1);
            tableLayoutPanel1.SetColumnSpan(_inspectionLogSplit, 1);
            tableLayoutPanel1.SetColumnSpan(statusStrip1, 1);
            _inspectionLogSplit.Panel1.Controls.SetChildIndex(tableLayoutPanel5, 0);
            tableLayoutPanel5.Margin = Padding.Empty;
            tableLayoutPanel1.Controls.Add(header, 0, 0);
            tableLayoutPanel1.Controls.Add(_inspectionLogSplit, 0, 1);
            tableLayoutPanel1.Controls.Add(statusStrip1, 0, 2);
            _inspectionLogSplit.SizeChanged += (s, e) => InitializeSplitDistance();
            _inspectionLogSplit.SplitterMoving += (s, e) => _splitUserMoving = true;
            _inspectionLogSplit.SplitterMoved += (s, e) =>
            {
                if (!_splitUserMoving) return;
                _splitUserMoving = false;
                SaveSplitDistance();
            };
            Shown += (s, e) =>
            {
                // Restore after startup/maximization has assigned the final client area.
                _splitInitialized = false;
                InitializeSplitDistance();
                _splitReady = true;
            };
            FormClosing += (s, e) => { if (!e.Cancel) SaveSplitDistance(); };
            tableLayoutPanel1.ResumeLayout(true);
            InitializeSplitDistance();
            Disposed += (s, e) => richTextBox1.Dispose();
        }

        private void InitializeSplitDistance()
        {
            int available = _inspectionLogSplit.Height - _inspectionLogSplit.SplitterWidth;
            float scale = DesktopTheme.DisplayScale;
            int minimumTop = (int)(160 * scale), minimumLog = (int)(90 * scale);
            if (available < minimumTop + minimumLog) return;
            _inspectionLogSplit.Panel1MinSize = minimumTop;
            _inspectionLogSplit.Panel2MinSize = minimumLog;
            if (_splitInitialized) return;
            int logHeight = _savedLogHeight > 0 ? (int)Math.Round(_savedLogHeight * scale) : Math.Min((int)(180 * scale), available / 3);
            logHeight = Math.Max(minimumLog, Math.Min(logHeight, available - minimumTop));
            _inspectionLogSplit.SplitterDistance = available - logHeight;
            _splitInitialized = true;
        }

        private void SaveSplitDistance()
        {
            if (!_splitReady || !_splitInitialized || WindowState == FormWindowState.Minimized || _inspectionLogSplit.Panel2.Height <= 0) return;
            _savedLogHeight = _inspectionLogSplit.Panel2.Height / (double)DesktopTheme.DisplayScale;
            LogSplitterPreferences.Save(_savedLogHeight);
        }

        private sealed class InspectionSplitContainer : SplitContainer
        {
            internal InspectionSplitContainer() { DoubleBuffered = true; ResizeRedraw = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Rectangle rect = SplitterRectangle;
                using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, rect);
                float scale = DesktopTheme.DisplayScale, x = rect.Left + rect.Width / 2F, y = rect.Top + rect.Height / 2F;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(133, 153, 175), Math.Max(1, 1.4F * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    e.Graphics.DrawLine(pen, x - 20 * scale, y - 1.5F * scale, x + 20 * scale, y - 1.5F * scale);
                    e.Graphics.DrawLine(pen, x - 20 * scale, y + 1.5F * scale, x + 20 * scale, y + 1.5F * scale);
                }
            }
        }
    }

    internal static class LogSplitterPreferences
    {
        internal static double Load()
        {
            try
            {
                string path = ProjectPaths.Config("General/MainLayout.xml");
                if (!File.Exists(path)) return 0;
                double height = (double?)XElement.Load(path).Attribute("LogHeight96Dpi") ?? 0;
                return height > 0 && height < 10000 && !double.IsNaN(height) ? height : 0;
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Load log splitter position: " + ex.Message); return 0; }
        }
        internal static void Save(double height)
        {
            string temp = null;
            try
            {
                string path = ProjectPaths.Config("General/MainLayout.xml");
                temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                new XElement("MainLayout", new XAttribute("LogHeight96Dpi", height)).Save(temp);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Save log splitter position: " + ex.Message); }
            finally { if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }
    }
}

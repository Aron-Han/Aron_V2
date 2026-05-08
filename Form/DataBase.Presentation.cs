using Aron_V2;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp2
{
    public partial class DataBase
    {
        private readonly Dictionary<string, TextBox> _recordDetails = new Dictionary<string, TextBox>();
        private SplitContainer _databaseSplit;
        private bool _bindingRecordDetails;

        private void InitializeDatabasePresentation()
        {
            float scale = DesktopTheme.DisplayScale;
            Func<int, int> px = n => (int)Math.Round(n * scale);
            SuspendLayout(); tableLayoutPanel1.SuspendLayout();
            try
            {
                // Reuse original controls/events. The demonstration insert
                // button is removed; production InsertInspectionRecord stays.
                tableLayoutPanel1.Controls.Clear();
                button3.Click -= button3_Click; button3.Dispose();
                tableLayoutPanel1.ColumnStyles.Clear(); tableLayoutPanel1.RowStyles.Clear();
                tableLayoutPanel1.ColumnCount = 1; tableLayoutPanel1.RowCount = 3;
                tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(54)));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(48)));
                tableLayoutPanel1.Padding = new Padding(px(10)); tableLayoutPanel1.Margin = Padding.Empty;
                tableLayoutPanel1.SetColumnSpan(dataGridView1, 1);

                var toolbar = new TableLayoutPanel { Name = "DatabaseToolbar", Dock = DockStyle.Fill,
                    ColumnCount = 7, RowCount = 1, Margin = Padding.Empty };
                foreach (var column in new[] {
                    new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Absolute, px(205)),
                    new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Absolute, px(205)),
                    new ColumnStyle(SizeType.Absolute, px(96)), new ColumnStyle(SizeType.Percent, 100),
                    new ColumnStyle(SizeType.Absolute, px(105)) }) toolbar.ColumnStyles.Add(column);
                toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                Control[] fields = { label1, dateTimePicker1, label2, dateTimePicker2, Filter };
                for (int i = 0; i < fields.Length; i++)
                {
                    Control item = fields[i]; item.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                    item.Margin = new Padding(px(4)); toolbar.Controls.Add(item, i, 0);
                }
                label1.AutoSize = label2.AutoSize = true;
                toolbar.Controls.Add(button2, 6, 0);
                foreach (Button button in new[] { Filter, button2, button1, button4, Clear_Database })
                {
                    button.AutoSize = false; button.MinimumSize = button.MaximumSize = Size.Empty;
                    button.Size = new Size(px(96), px(32)); button.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                    button.Margin = new Padding(px(4));
                    DesktopTheme.StyleButton(button, ReferenceEquals(button, Filter));
                }
                Clear_Database.ForeColor = Color.FromArgb(205, 53, 65);
                SetDatabaseButtonIcon(Filter, DesktopIcon.Sliders, Color.White, scale);
                SetDatabaseButtonIcon(button2, DesktopIcon.Send, DesktopTheme.Accent, scale);
                button4.Text = "Previous"; button1.Text = "Next";

                _databaseSplit = new SplitContainer { Name = "DatabaseRecordSplit", Dock = DockStyle.Fill,
                    Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel2, SplitterWidth = px(6),
                    BackColor = DesktopTheme.Border, Margin = new Padding(0, px(4), 0, px(4)),
                    Size = new Size(px(1180), px(560)) };
                _databaseSplit.Panel1MinSize = px(300); _databaseSplit.Panel2MinSize = px(225);
                _databaseSplit.SplitterDistance = px(870);
                _databaseSplit.Panel1.BackColor = _databaseSplit.Panel2.BackColor = Color.White;
                _databaseSplit.Panel1.Controls.Add(dataGridView1);
                _databaseSplit.Panel1.Padding = new Padding(1);
                dataGridView1.Margin = Padding.Empty;
                _databaseSplit.Panel2.Controls.Add(CreateRecordDetails(scale));

                var footer = new TableLayoutPanel { Name = "DatabaseFooter", Dock = DockStyle.Fill,
                    ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(115)));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                footer.Controls.Add(Clear_Database, 0, 0);
                var paging = new TableLayoutPanel { Name = "DatabasePaging", AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
                paging.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(100)));
                paging.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                paging.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(100)));
                paging.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                label3.AutoSize = true; label3.Anchor = AnchorStyles.None; label3.Margin = new Padding(px(8), 0, px(8), 0);
                paging.Controls.Add(button4, 0, 0); paging.Controls.Add(label3, 1, 0); paging.Controls.Add(button1, 2, 0);
                footer.Controls.Add(paging, 2, 0);
                tableLayoutPanel1.Controls.Add(toolbar, 0, 0);
                tableLayoutPanel1.Controls.Add(_databaseSplit, 0, 1);
                tableLayoutPanel1.Controls.Add(footer, 0, 2);

                int[] widths = { 56, 145, 60, 65, 65, 65, 75, 165, 230 };
                for (int i = 0; i < dataGridView1.Columns.Count; i++)
                {
                    var column = dataGridView1.Columns[i];
                    column.MinimumWidth = px(i == 8 ? 210 : 45);
                    if (i == 8) column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    else column.Width = px(widths[i]);
                }
                dataGridView1.RowHeadersVisible = false;
                dataGridView1.RowTemplate.Height = px(32);
                dataGridView1.ColumnHeadersHeight = px(32);
                dataGridView1.SelectionChanged += (s, e) => UpdateRecordDetails();
                dataGridView1.CurrentCellChanged += (s, e) => UpdateRecordDetails();
                dataGridView1.DataBindingComplete += (s, e) => UpdateRecordDetails();
                MinimumSize = new Size(px(920), px(560));
                ClientSize = new Size(px(1220), px(740));
                Shown += (s, e) =>
                {
                    Rectangle work = Screen.FromControl(this).WorkingArea;
                    MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width), Math.Min(MinimumSize.Height, work.Height));
                    Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                    _databaseSplit.SplitterDistance = Math.Max(_databaseSplit.Panel1MinSize,
                        Math.Min(_databaseSplit.Width - _databaseSplit.Panel2MinSize - _databaseSplit.SplitterWidth, (int)(_databaseSplit.Width * .74F)));
                    dataGridView1.BorderStyle = BorderStyle.None;
                    _databaseSplit.Panel1.BackColor = DesktopTheme.Border;
                    foreach (var value in _recordDetails.Values)
                    {
                        value.BorderStyle = BorderStyle.None;
                        value.BackColor = Color.FromArgb(247, 249, 252);
                        value.Parent.BackColor = value.BackColor;
                    }
                    UpdateRecordDetails();
                };
            }
            finally { tableLayoutPanel1.ResumeLayout(true); ResumeLayout(true); }
        }

        private Control CreateRecordDetails(float scale)
        {
            Func<int, int> px = n => (int)Math.Round(n * scale);
            var panel = new TableLayoutPanel { Name = "RecordDetailsPanel", Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 10, Padding = new Padding(px(10)), Margin = Padding.Empty };
            EnableBufferedPainting(panel);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(76)));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, px(36)));
            var titleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            var title = new Label { Text = "Record Details", AutoSize = true, Font = titleFont,
                Anchor = AnchorStyles.Left, Margin = Padding.Empty };
            panel.Controls.Add(title, 0, 0); panel.SetColumnSpan(title, 2);
            Shown += (s, e) => title.Font = titleFont;
            Disposed += (s, e) => titleFont.Dispose();
            string[] fields = { "Id", "Serial", "Result", "Job", "Cam", "PosN", "Channel", "CreatedAt", "Payload" };
            for (int i = 0; i < fields.Length; i++)
            {
                string field = fields[i]; bool payload = field == "Payload";
                panel.RowStyles.Add(new RowStyle(payload ? SizeType.Percent : SizeType.Absolute, payload ? 100 : px(32)));
                var caption = new Label { Text = field, AutoSize = true,
                    Anchor = payload ? AnchorStyles.Top | AnchorStyles.Left : AnchorStyles.Left, Margin = new Padding(0, px(6), px(5), px(6)) };
                var value = new TextBox { Name = "DbDetail" + field, ReadOnly = true,
                    Multiline = true, WordWrap = true, ScrollBars = payload ? ScrollBars.Vertical : ScrollBars.None,
                    Dock = DockStyle.Fill, Margin = new Padding(0, px(3), 0, px(3)),
                    BackColor = Color.FromArgb(247, 249, 252), BorderStyle = BorderStyle.FixedSingle };
                _recordDetails.Add(field, value);
                var surface = new Panel { Dock = DockStyle.Fill, Padding = new Padding(px(5), px(3), px(5), px(3)),
                    Margin = value.Margin, BackColor = value.BackColor };
                value.Margin = Padding.Empty;
                surface.Controls.Add(value);
                surface.Paint += (s, e) => DesktopTheme.DrawInspectionSurface(e.Graphics, surface.ClientSize,
                    Color.FromArgb(247, 249, 252), DesktopTheme.Border);
                panel.Controls.Add(caption, 0, i + 1); panel.Controls.Add(surface, 1, i + 1);
            }
            return panel;
        }

        private void SetDatabaseButtonIcon(Button button, DesktopIcon kind, Color color, float scale)
        {
            var image = DesktopTheme.CreateIcon(kind, (int)(16 * scale), color);
            button.Image = image; button.ImageAlign = ContentAlignment.MiddleCenter;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            Disposed += (s, e) => image.Dispose();
        }

        private void UpdateRecordDetails()
        {
            if (_bindingRecordDetails || IsDisposed || Disposing || dataGridView1.IsDisposed) return;
            // No extra database query. Show the exact values already loaded for
            // the selected row; wrapping is visual and never rewrites Payload.
            var row = dataGridView1.CurrentRow;
            foreach (var field in _recordDetails)
                field.Value.Text = row == null || row.IsNewRow || !dataGridView1.Columns.Contains(field.Key)
                    ? "" : row.Cells[field.Key].Value?.ToString() ?? "";
        }
    }
}

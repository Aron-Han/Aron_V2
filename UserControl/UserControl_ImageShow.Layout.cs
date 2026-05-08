using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class UserControl_ImageShow
    {
        // Presentation only. Reuse every original control, binding and event;
        // never copy identity/status text into a second source of truth.
        private void BuildCameraCardLayout()
        {
            float scale = DesktopTheme.DisplayScale;
            Func<int, int> px = n => (int)Math.Round(n * scale);
            SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            try
            {
                tableLayoutPanel1.Controls.Clear();
                tableLayoutPanel1.ColumnStyles.Clear();
                tableLayoutPanel1.RowStyles.Clear();
                tableLayoutPanel1.ColumnCount = 1;
                tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                tableLayoutPanel1.RowCount = 5;
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(36)));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(46)));
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(27)));
                // Match Job/Pos/Engine row height and cell margins exactly.
                tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, px(27)));
                tableLayoutPanel1.Padding = new Padding(px(8));
                tableLayoutPanel1.Margin = Padding.Empty;
                tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;

                var header = CameraCardRow("CameraCardHeader", 64, 36);
                var selectors = CameraCardRow("CameraCardSelectors", 33.3333F, 33.3333F, 33.3334F);
                var actions = CameraCardRow("CameraCardActions", 33.3333F, 33.3333F, 33.3334F);
                // Clear old designer spans before inserting into a one-column root.
                tableLayoutPanel1.SetColumnSpan(cogRecordDisplay1, 1);
                tableLayoutPanel1.SetColumnSpan(tableLayoutPanel3, 1);
                tableLayoutPanel1.Controls.Add(header, 0, 0);
                tableLayoutPanel1.Controls.Add(cogRecordDisplay1, 0, 1);
                tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 2);
                tableLayoutPanel1.Controls.Add(selectors, 0, 3);
                tableLayoutPanel1.Controls.Add(actions, 0, 4);
                tableLayoutPanel1.SetColumnSpan(cogRecordDisplay1, 1);
                tableLayoutPanel1.SetColumnSpan(tableLayoutPanel3, 1);
                header.Controls.Add(button1, 0, 0);
                header.Controls.Add(btnResult, 1, 0);
                selectors.Controls.Add(button2, 0, 0);
                selectors.Controls.Add(button3, 1, 0);
                selectors.Controls.Add(button4, 2, 0);
                actions.Controls.Add(Reset, 0, 0);
                actions.Controls.Add(btnTrigger, 1, 0);
                actions.Controls.Add(btnReplay, 2, 0);

                foreach (Control control in new Control[] { button1, btnResult, button2, button3, button4, Reset, btnTrigger, btnReplay })
                {
                    control.Dock = DockStyle.Fill;
                    control.Margin = new Padding(px(3), px(3), px(3), px(3));
                }
                foreach (Control control in new Control[] { button2, button3, button4, Reset, btnTrigger, btnReplay })
                    control.Margin = new Padding(px(3), px(2), px(3), px(2));
                DesktopTheme.StyleInspectionReadouts(this, btnResult);
                button1.TextAlign = ContentAlignment.MiddleCenter;
                btnResult.TextAlign = ContentAlignment.MiddleCenter;
                var headerFont = new Font("Segoe UI", 11F, FontStyle.Bold);
                button1.Font = headerFont;
                Disposed += (s, e) => headerFont.Dispose();
                cogRecordDisplay1.Margin = new Padding(px(3), px(5), px(3), px(3));
                tableLayoutPanel3.Margin = new Padding(px(3), px(3), px(3), px(3));
                tableLayoutPanel3.Padding = Padding.Empty;
                tableLayoutPanel3.Paint += PaintCameraMetricDividers;
                foreach (Label label in new[] { lbl_Total, lbl_Pass, lbl_Passrate })
                {
                    label.AutoSize = false;
                    label.Dock = DockStyle.Fill;
                    label.Margin = Padding.Empty;
                    label.TextAlign = ContentAlignment.MiddleCenter;
                }
                // Cam has moved from its old one-cell wrapper into the header.
                tableLayoutPanel2.Dispose();
            }
            finally
            {
                tableLayoutPanel1.ResumeLayout(true);
                ResumeLayout(true);
            }
        }

        private static TableLayoutPanel CameraCardRow(string name, params float[] widths)
        {
            var row = new TableLayoutPanel
            {
                Name = name, Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = Padding.Empty, ColumnCount = widths.Length, RowCount = 1,
                BackColor = DesktopTheme.Surface
            };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            foreach (float width in widths) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            return row;
        }

        private void PaintCameraMetricDividers(object sender, PaintEventArgs e)
        {
            var row = (TableLayoutPanel)sender;
            using (var pen = new Pen(DesktopTheme.Border))
            {
                e.Graphics.DrawLine(pen, 0, 0, row.Width, 0);
                e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
            }
        }
    }

    // Draw the existing bound Text as caption + value. DataBindings, Text,
    // DoubleClick and the CameraStats persistence model remain untouched.
    internal sealed class InspectionMetricLabel : Label
    {
        private string CaptionKey { get { return Name == "lbl_Passrate" ? "PassRate" : Name == "lbl_Pass" ? "Pass" : "Total"; } }
        private readonly Font _captionFont = new Font("Segoe UI", 7.5F);
        private readonly Font _valueFont = new Font("Segoe UI", 11.5F, FontStyle.Bold);

        public InspectionMetricLabel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal string DisplayValue
        {
            get
            {
                string text = Text ?? "";
                int colon = text.LastIndexOfAny(new[] { ':', '：' });
                return colon >= 0 ? text.Substring(colon + 1).Trim() : text == "label1" ? "0" : text.Trim();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            string caption = UiLanguage.Text(CaptionKey + ":").TrimEnd(':', '：');
            if (caption == "PassRate") caption = "Pass Rate";
            int top = Math.Max(0, (Height - _captionFont.Height - _valueFont.Height) / 2);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(e.Graphics, caption, _captionFont,
                new Rectangle(2, top, Math.Max(0, Width - 4), _captionFont.Height), DesktopTheme.Muted, flags);
            int available = Math.Max(1, Width - 4);
            using (var format = new StringFormat(StringFormat.GenericTypographic)
                { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                float measured = e.Graphics.MeasureString(DisplayValue, _valueFont, int.MaxValue, format).Width;
                using (var valueFont = new Font(_valueFont.FontFamily,
                    Math.Max(7F, _valueFont.Size * Math.Min(1F, (available - 3) / Math.Max(1F, measured))), FontStyle.Bold))
                using (var brush = new SolidBrush(DesktopTheme.Ink))
                    e.Graphics.DrawString(DisplayValue, valueFont, brush,
                        new Rectangle(2, top + _captionFont.Height, available, _valueFont.Height), format);
            }
            using (var pen = new Pen(DesktopTheme.Border))
            {
                e.Graphics.DrawLine(pen, 0, 0, Width, 0);
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                if (CaptionKey != "PassRate") e.Graphics.DrawLine(pen, Width - 1, 8, Width - 1, Height - 8);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _captionFont.Dispose(); _valueFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

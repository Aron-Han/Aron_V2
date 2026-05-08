using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class LogDetailsForm : Form
    {
        private static readonly Color MessageSurface = Color.FromArgb(246, 248, 251);
        private readonly Font _body = new Font("Segoe UI", 9F);
        private readonly Font _heading = new Font("Segoe UI", 15F, FontStyle.Bold);
        private readonly RichTextBox _message;
        private readonly Label _hint;
        private readonly Button _copy;
        private readonly Button _close;
        private bool _fittingHeight;
        private bool _fitQueued;
        internal string FullMessageForCopy { get; private set; }

        internal LogDetailsForm(LogEntry entry)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Log Details"; StartPosition = FormStartPosition.CenterParent;
            Font = _body; BackColor = DesktopTheme.Surface; ForeColor = DesktopTheme.Ink;
            ClientSize = new Size(760, 410); MinimumSize = new Size(640, 340);
            MinimizeBox = false; ShowInTaskbar = false;
            FullMessageForCopy = entry.Message ?? string.Empty;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(20, 12, 20, 16), Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 10) };
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));
            header.Controls.Add(new Label { Text = "Log Details", Font = _heading, AutoSize = true, Margin = Padding.Empty, Anchor = AnchorStyles.Left }, 0, 0);
            Color signal = entry.Error ? Color.FromArgb(185, 45, 53) : entry.Warning ? Color.FromArgb(201, 137, 24) : DesktopTheme.Accent;
            header.Controls.Add(new SignalGlyph { Warning = entry.Warning, ForeColor = signal, Size = new Size(30, 30), MinimumSize = new Size(30, 30), MaximumSize = new Size(30, 30), Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = Padding.Empty }, 1, 0);
            root.Controls.Add(header, 0, 0);

            var metadata = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 10) };
            metadata.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            metadata.Controls.Add(Metadata("TIME", new Label { Text = entry.Time.ToString("HH:mm:ss.fff"), Name = "timeValue", AutoSize = true, Margin = Padding.Empty }), 0, 0);
            var level = new Label { Text = entry.Type, Name = "levelValue", AutoSize = true, ForeColor = signal, BackColor = entry.Error ? Color.FromArgb(255, 229, 232) : entry.Warning ? Color.FromArgb(255, 237, 192) : DesktopTheme.Hover, Padding = new Padding(8, 3, 8, 3), Margin = Padding.Empty };
            metadata.Controls.Add(Metadata("LEVEL", level), 1, 0);
            metadata.Controls.Add(Metadata("SOURCE", new Label { Text = entry.Communication ? "Communicate" : "Application", Name = "sourceValue", AutoSize = true, Margin = Padding.Empty }), 2, 0);
            root.Controls.Add(metadata, 0, 1);
            root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = DesktopTheme.Border, Margin = Padding.Empty }, 0, 2);

            var card = new MessageCard { Dock = DockStyle.Fill, BackColor = MessageSurface, Padding = new Padding(14), Margin = new Padding(0, 10, 0, 0), MinimumSize = new Size(0, 140) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty, BackColor = MessageSurface };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = "Message", AutoSize = true, ForeColor = DesktopTheme.Muted, Margin = new Padding(0, 0, 0, 10) }, 0, 0);
            _message = new RichTextBox { Name = "messageText", Dock = DockStyle.Fill, Margin = Padding.Empty, Text = FullMessageForCopy, ReadOnly = true, WordWrap = true, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, BorderStyle = BorderStyle.None, BackColor = MessageSurface, ForeColor = DesktopTheme.Ink, HideSelection = false, Font = _body };
            content.Controls.Add(_message, 0, 1); card.Controls.Add(content); root.Controls.Add(card, 0, 3);
            _hint = new Label { Text = "Read-only · Automatic word wrap", AutoSize = true, ForeColor = DesktopTheme.Muted, Margin = new Padding(0, 8, 0, 14) };
            root.Controls.Add(_hint, 0, 4);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _copy = new Button { Name = "copyAll", Text = "Copy All", AutoSize = true, MinimumSize = new Size(100, 34), Padding = new Padding(12, 5, 12, 5), Margin = Padding.Empty };
            _close = new Button { Name = "closeDetails", Text = "Close", AutoSize = true, MinimumSize = new Size(100, 34), Padding = new Padding(12, 5, 12, 5), Margin = Padding.Empty, DialogResult = DialogResult.Cancel };
            DesktopTheme.StyleButton(_copy); DesktopTheme.StyleButton(_close, true);
            _copy.Click += CopyAll;
            _close.Click += (s, e) => Close();
            actions.Controls.Add(_copy, 0, 0); actions.Controls.Add(_close, 2, 0);
            root.Controls.Add(actions, 0, 5); Controls.Add(root);
            root.Layout += (s, e) => QueueMinimumHeight(root, card);
            FontChanged += (s, e) => QueueMinimumHeight(root, card);
            AcceptButton = _close; CancelButton = _close;
            Shown += (s, e) => { FitMinimumHeight(root, card); _message.SelectionStart = 0; _message.SelectionLength = 0; _close.Focus(); };
            ResumeLayout(true);
        }

        private void QueueMinimumHeight(TableLayoutPanel root, Control card)
        {
            if (_fitQueued || !IsHandleCreated || IsDisposed || Disposing) return;
            _fitQueued = true;
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (IsDisposed || Disposing) return;
                    root.PerformLayout(); FitMinimumHeight(root, card);
                }
                finally { _fitQueued = false; }
            }));
        }

        private void FitMinimumHeight(TableLayoutPanel root, Control card)
        {
            if (_fittingHeight || !IsHandleCreated || Disposing || IsDisposed) return;
            int[] rows = root.GetRowHeights();
            if (rows.Length != 6) return;
            int required = root.Padding.Vertical + card.Margin.Vertical + card.MinimumSize.Height;
            for (int i = 0; i < rows.Length; i++) if (i != 3) required += rows[i];
            required += Height - ClientSize.Height;
            if (required <= MinimumSize.Height) return;
            _fittingHeight = true;
            try { MinimumSize = new Size(MinimumSize.Width, required); }
            finally { _fittingHeight = false; }
        }

        private static Control Metadata(string title, Control value)
        {
            var field = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = Padding.Empty };
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize)); field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = DesktopTheme.Muted, Margin = new Padding(0, 0, 0, 4) }, 0, 0);
            field.Controls.Add(value, 0, 1); return field;
        }

        private void CopyAll(object sender, EventArgs e)
        {
            try
            {
                if (FullMessageForCopy.Length != 0) Clipboard.SetText(FullMessageForCopy);
                _hint.Text = "Copied · Complete original message";
            }
            catch (ExternalException) { _hint.Text = "Clipboard busy. Please try again."; }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) { _body.Dispose(); _heading.Dispose(); }
        }

        private sealed class MessageCard : Panel
        {
            internal MessageCard() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(DesktopTheme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private sealed class SignalGlyph : Control
        {
            internal bool Warning;
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float size = Math.Min(Width, Height) - 6, x = (Width - size) / 2, y = (Height - size) / 2;
                using (var pen = new Pen(ForeColor, 1.8F))
                using (var brush = new SolidBrush(ForeColor))
                {
                    if (Warning) e.Graphics.DrawPolygon(pen, new[] { new PointF(x + size / 2, y), new PointF(x + size, y + size), new PointF(x, y + size) });
                    else e.Graphics.DrawEllipse(pen, x, y, size, size);
                    e.Graphics.DrawLine(pen, x + size / 2, y + size * .35F, x + size / 2, y + size * .65F);
                    e.Graphics.FillEllipse(brush, x + size / 2 - 1, y + size * .78F, 2, 2);
                }
            }
        }
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class ImageRecord
    {
        private bool _settingsCenterLayout;
        private ComboBox _languageSelector;
        private bool _aligningCaptions;

        internal void PrepareForSettingsCenter()
        {
            if (_settingsCenterLayout) return;
            _settingsCenterLayout = true;
            SuspendLayout();
            float scale = DesktopTheme.DisplayScale;
            var layout = new TableLayoutPanel { Name = "GeneralSettingsLayout", Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Padding = new Padding(10), Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145 * scale));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            System.Action<string> section = text =>
            {
                var label = new Label { Text = text, AutoSize = true, ForeColor = DesktopTheme.Accent, Margin = new Padding(0, row == 0 ? 0 : 16, 0, 8) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(label, 0, row++); layout.SetColumnSpan(label, 2);
            };
            System.Action<Control, Control> field = (label, value) =>
            {
                label.AutoSize = true; label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, (int)(6 * scale), (int)(8 * scale), (int)(6 * scale));
                value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                if (value is TextBoxBase) value.MaximumSize = new Size((int)(560 * scale), 0);
                value.Margin = new Padding(0, (int)(6 * scale), 0, (int)(6 * scale));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize, 38 * scale));
                layout.Controls.Add(label, 0, row); layout.Controls.Add(value, 1, row++);
            };
            System.Func<Control[], Control> line = controls =>
            {
                if (controls.Length > 2)
                {
                    var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
                    foreach (Control item in controls) { item.Margin = new Padding(0, 0, (int)(10 * scale), 0); flow.Controls.Add(item); }
                    return flow;
                }
                var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = controls.Length, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                panel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
                int column = 0;
                foreach (Control item in controls)
                {
                    panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    item.Anchor = AnchorStyles.Left;
                    item.Margin = new Padding(0, 0, (int)(10 * scale), 0);
                    panel.Controls.Add(item, column++, 0);
                }
                return panel;
            };
            section("Image storage");
            field(label1, line(new Control[] { cboFormat, chkEnable }));
            field(label2, numRetention);
            numRetention.Anchor = AnchorStyles.Left;
            numRetention.Width = (int)(120 * scale);
            txtRoot.Width = (int)(320 * scale);
            field(new Label { Text = "Image directory:" }, line(new Control[] { txtRoot, btnBrowse }));
            section("FTP upload");
            field(new Label { Text = "Enable FTP:" }, checkBox1);
            field(label3, textBox1); field(label4, textBox2); field(label5, textBox3); field(label6, textBox4);
            section("Display and application");
            _languageSelector = new ComboBox { Name = "LanguageSelector", DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(180 * scale) };
            _languageSelector.Items.AddRange(new object[] { "English", "中文" });
            _languageSelector.SelectedIndex = UiLanguage.Normalize(Current.Language) == "zh-CN" ? 1 : 0;
            field(new Label { Text = "Language:" }, _languageSelector);
            _languageSelector.Anchor = AnchorStyles.Left;
            Load += (s, e) => _languageSelector.SelectedIndex = UiLanguage.Normalize(Current.Language) == "zh-CN" ? 1 : 0;
            field(new Label { Text = "Log display:" }, line(new Control[] { Show_upload_info, checkBox2, chkCleanupLog }));
            field(label7, Lincese); field(label8, Program_Name);
            layout.RowCount = row;
            layout.Layout += (s, e) => AlignCaptionCenters(layout);
            // Save is hosted by the settings center's fixed footer, not in
            // this scrollable parameter layout. Its original handler is retained.
            // All original input/button instances were reparented, not copied.
            // Their Load, validation, browse and Save handlers are untouched.
            Controls.Add(layout);
            layout.BringToFront();
            AutoScroll = true;
            Shown += (s, e) => AlignSettingsCenterFields();
            // Embedded pages do not reliably raise Shown after their native
            // combo height settles. Align again after Load/handle creation.
            Load += (s, e) => QueueInlineAlignment();
            VisibleChanged += (s, e) => { if (Visible) QueueInlineAlignment(); };
            cboFormat.SizeChanged += (s, e) => QueueInlineAlignment();
            ResumeLayout(true);
        }

        private bool _inlineAlignmentPending;
        private void QueueInlineAlignment()
        {
            if (_inlineAlignmentPending || !IsHandleCreated || IsDisposed) return;
            _inlineAlignmentPending = true;
            BeginInvoke(new System.Action(() =>
            {
                _inlineAlignmentPending = false;
                if (!IsDisposed) AlignSettingsCenterFields();
            }));
        }

        private void AlignSettingsCenterFields()
        {
            float scale = DesktopTheme.DisplayScale;
            btnBrowse.AutoSize = false;
            btnBrowse.MinimumSize = Size.Empty;
            btnBrowse.MaximumSize = Size.Empty;
            btnBrowse.Padding = Padding.Empty;
            btnBrowse.Text = "…";
            btnBrowse.Size = new Size((int)(42 * scale), txtRoot.Height);
            btnBrowse.AccessibleName = "Browse image directory";
            btnBrowse.Parent.PerformLayout();
            // Native ComboBox height can become larger after handle creation
            // than TableLayoutPanel's cached preferred height. Reserve its real
            // height so the caption is centered on the combo, not a shorter row.
            foreach (Control value in new Control[] { cboFormat.Parent, txtRoot.Parent })
            {
                var line = value as TableLayoutPanel;
                if (line == null) continue;
                int height = 0;
                foreach (Control child in line.Controls) height = System.Math.Max(height,
                    System.Math.Max(child.Height, child is ComboBox ? ((ComboBox)child).PreferredHeight : child.Height) + child.Margin.Vertical);
                height += (int)(4 * scale);
                line.AutoSize = false;
                line.MinimumSize = new Size(0, height);
                line.MaximumSize = new Size(0, height);
                line.Height = height;
                line.RowStyles[0].SizeType = SizeType.Absolute;
                line.RowStyles[0].Height = height;
                line.PerformLayout();
            }
            cboFormat.Parent.Parent.PerformLayout();
        }

        private void AlignCaptionCenters(TableLayoutPanel layout)
        {
            if (_aligningCaptions || !layout.IsHandleCreated) return;
            _aligningCaptions = true;
            layout.SuspendLayout();
            try
            {
                for (int row = 0; row < layout.RowCount; row++)
                {
                    var caption = layout.GetControlFromPosition(0, row) as Label;
                    Control field = layout.GetControlFromPosition(1, row);
                    if (caption == null || field == null || ReferenceEquals(caption, field)) continue;
                    var inline = field as TableLayoutPanel;
                    Control reference = inline != null && inline.Controls.Count != 0 ? inline.Controls[0] : field;
                    int center = layout.PointToClient(reference.PointToScreen(new Point(0, reference.Height / 2))).Y;
                    int delta = center - caption.Top - caption.Height / 2;
                    if (delta == 0) continue;
                    Padding margin = caption.Margin;
                    // Keep total row spacing unchanged while compensating for
                    // native controls whose actual height differs from the
                    // preferred height used by the table's centering engine.
                    delta = System.Math.Max(-margin.Top, System.Math.Min(margin.Bottom, delta));
                    if (delta != 0) caption.Margin = new Padding(margin.Left, margin.Top + delta, margin.Right, margin.Bottom - delta);
                }
            }
            finally { layout.ResumeLayout(true); _aligningCaptions = false; }
        }
    }
}

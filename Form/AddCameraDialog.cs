using Aron_V2.CameraIntegration;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class AddCameraDialog : Form
    {
        private readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly ComboBox _provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly Label _category = new Label { AutoSize = true, Dock = DockStyle.Fill };

        internal AddCameraDialog()
        {
            Text = "Add Camera"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18), ColumnCount = 2, RowCount = 4, MinimumSize = new Size(420, 0) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _mode.Items.AddRange(new object[] { "Visionpro", "SDK" }); _mode.SelectedIndex = 0;
            _provider.Items.AddRange(CameraAcquisition.Providers); if (_provider.Items.Count > 0) _provider.SelectedIndex = 0;
            Control[] editors = { _mode, _provider, _category }; string[] labels = { "Mode", "SDK brand", "Category" };
            for (int i = 0; i < editors.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(new Label { Text = labels[i], AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 8, 16, 8) }, 0, i);
                editors[i].Margin = new Padding(4, 8, 4, 8); layout.Controls.Add(editors[i], 1, i);
            }
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var add = new Button { Text = "Add", AutoSize = true, MinimumSize = new Size(85, 30) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(85, 30), DialogResult = DialogResult.Cancel };
            add.Click += (s, e) => { if (_mode.SelectedIndex == 0 || _provider.SelectedItem != null) DialogResult = DialogResult.OK; };
            actions.Controls.AddRange(new Control[] { add, cancel }); layout.Controls.Add(actions, 0, 3); layout.SetColumnSpan(actions, 2);
            Controls.Add(layout); AcceptButton = add; CancelButton = cancel;
            _mode.SelectedIndexChanged += (s, e) => UpdateMode(); _provider.SelectedIndexChanged += (s, e) => UpdateMode();
            UpdateMode(); DesktopTheme.ApplySettings(this);
        }

        private void UpdateMode()
        {
            _provider.Enabled = _mode.SelectedIndex == 1;
            _category.Text = _mode.SelectedIndex == 0 ? CameraAcquisition.Visionpro : (_provider.SelectedItem as SdkCameraProvider)?.Category ?? "No SDK providers installed";
        }

        internal CameraConfig CreateCamera(string name)
        {
            var provider = _mode.SelectedIndex == 1 ? (SdkCameraProvider)_provider.SelectedItem : null;
            return new CameraConfig { Name = name, ConnectionMode = provider?.Id ?? CameraAcquisition.Cognex,
                SdkCategory = provider?.Category, Positions = new System.Collections.Generic.List<PositionConfig>() };
        }
    }
}

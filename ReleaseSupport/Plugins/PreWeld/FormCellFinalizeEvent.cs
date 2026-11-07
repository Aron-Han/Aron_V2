using Aron.PluginContracts;
using Aron_V2.CellInspection;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    public sealed class FormCellFinalizeEvent : Form
    {
        private readonly IPluginHost _host;
        private readonly ComboBox _job = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        private readonly ComboBox _position = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
        private readonly CheckBox _enabled = new CheckBox { Text = "Enable automatic Cell XML collection", AutoSize = true };
        public FormCellFinalizeEvent(IPluginHost host)
        {
            _host = host; Text = "Pre-weld setup"; Font = new Font("Segoe UI", 10); BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(650, 310); MinimumSize = Size;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(20) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 3 ? 90 : 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(_enabled, 0, 0); layout.SetColumnSpan(_enabled, 2);
            layout.Controls.Add(new Label { Text = "Job", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); layout.Controls.Add(_job, 1, 1);
            layout.Controls.Add(new Label { Text = "Summary Pos", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); layout.Controls.Add(_position, 1, 2);
            var hint = new Label { Text = "All enabled cameras at this position must finish successfully for the same Job and product code. The plugin writes XML only; PLC signals stay unchanged. Replay/Manual do not collect production data.", Dock = DockStyle.Fill };
            layout.Controls.Add(hint, 0, 3); layout.SetColumnSpan(hint, 2);
            var save = new Button { Text = "Save", Width = 110, Height = 36, Anchor = AnchorStyles.Bottom | AnchorStyles.Right, BackColor = Color.FromArgb(29, 120, 208), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            layout.Controls.Add(save, 1, 4); save.Click += Save; Controls.Add(layout);
            var settings = CellInspectionService.LoadFinalizeEvent(); _enabled.Checked = settings.Enabled;
            _job.Items.AddRange(host.GetCatalog().Select(p => p.Job).Distinct().Cast<object>().ToArray());
            _job.SelectedIndexChanged += (s, e) => FillPositions();
            if (_job.Items.Count > 0) _job.SelectedIndex = Math.Max(0, _job.Items.IndexOf(settings.Job ?? ""));
            if (_position.Items.Contains(settings.Position ?? "")) _position.SelectedItem = settings.Position;
        }
        private void FillPositions()
        {
            _position.Items.Clear();
            _position.Items.AddRange(_host.GetCatalog().Where(p => p.Job == Convert.ToString(_job.SelectedItem)).Select(p => p.Position).Distinct().OrderBy(p => p).Cast<object>().ToArray());
            if (_position.Items.Count > 0) _position.SelectedIndex = 0;
        }
        private void Save(object sender, EventArgs e)
        {
            try
            {
                if (!_host.CanEdit) throw new InvalidOperationException("Switch Offline with configuration permission before saving.");
                if (_enabled.Checked && (_job.SelectedItem == null || _position.SelectedItem == null)) throw new InvalidOperationException("Choose a valid Job and position.");
                if (_enabled.Checked && !_host.GetCatalog().Any(p => p.Job == Convert.ToString(_job.SelectedItem) && p.Position == Convert.ToString(_position.SelectedItem) && (p.MainUsed || p.SecondUsed))) throw new InvalidOperationException("The summary position has no enabled main/second camera. Configure General Camera first.");
                CellInspectionService.SaveFinalizeEvent(new CellFinalizeEventSettings { Enabled = _enabled.Checked, Job = Convert.ToString(_job.SelectedItem), Position = Convert.ToString(_position.SelectedItem), Camera = "" });
                _host.Log("INFO", "Pre-weld collection configuration saved"); DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}

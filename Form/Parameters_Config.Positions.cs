using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class Parameters_Config
    {
        private readonly ComboBox _comboPos = new ComboBox { Name = "comboPos" };
        private readonly Button _clonePosition = new Button { Name = "btnClonePosition", Text = "Clone", AutoSize = true };
        private string _displayJob, _displayCamera, _displayPosition;
        private sealed class LimitEdit
        {
            internal string Job, Camera, Position;
            internal List<ParameterConfig> Parameters;
        }
        private readonly Dictionary<string, LimitEdit> _positionEdits = new Dictionary<string, LimitEdit>(StringComparer.OrdinalIgnoreCase);
        private static string TLimit(string en, string zh) => UiLanguage.Current == "zh-CN" ? zh : en;
        private static string LimitKey(string job, string camera, string position) => job + "|" + camera + "|" + position;
        internal static PositionConfig FindLimitPosition(AppConfig config, string job, string camera, string position)
        {
            return config?.Models?.FirstOrDefault(m => m.Name == job)?.Cameras?.FirstOrDefault(c => c.Name == camera)?.Positions?.FirstOrDefault(p => p.Name == position);
        }
        internal static List<ParameterConfig> CopyLimitParameters(IEnumerable<ParameterConfig> source) => (source ?? Enumerable.Empty<ParameterConfig>())
            .Where(p => p != null).Select(p => new ParameterConfig { Name = p.Name, Description = p.Description, Value = p.Value }).ToList();
        private static void ValidateLimitRows(List<ParameterConfig> parameters)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in parameters)
                if (string.IsNullOrWhiteSpace(parameter.Name) || !names.Add(parameter.Name))
                    throw new InvalidOperationException(TLimit("Parameter names must be nonempty and unique within the selected Position.", "当前 Pos 内的参数名称不能为空或重复。"));
        }
        private void StoreVisibleParameters()
        {
            if (string.IsNullOrEmpty(_displayJob) || string.IsNullOrEmpty(_displayCamera) || string.IsNullOrEmpty(_displayPosition)) return;
            dataGridView1.EndEdit(); BindingContext[_rows].EndCurrentEdit();
            _positionEdits[LimitKey(_displayJob, _displayCamera, _displayPosition)] = new LimitEdit {
                Job = _displayJob, Camera = _displayCamera, Position = _displayPosition,
                Parameters = _rows.Select(r => new ParameterConfig { Name = r.Parameter, Description = r.Description, Value = r.Value }).ToList() };
        }
        private void FillPosCombo()
        {
            string previous = _comboPos.SelectedItem as string;
            _updatingSelectors = true;
            try
            {
                _comboPos.Items.Clear();
                var camera = _config?.Models?.FirstOrDefault(m => m.Name == comboJob.SelectedItem as string)?.Cameras?.FirstOrDefault(c => c.Name == comboCam.SelectedItem as string);
                foreach (var pos in camera?.Positions ?? Enumerable.Empty<PositionConfig>()) _comboPos.Items.Add(pos.Name);
                if (previous != null && _comboPos.Items.Contains(previous)) _comboPos.SelectedItem = previous;
                else if (_comboPos.Items.Count > 0) _comboPos.SelectedIndex = 0;
            }
            finally { _updatingSelectors = false; }
        }
        private void RefreshLimitCatalog()
        {
            if (UseWaitCursor) return;
            try
            {
                StoreVisibleParameters();
                _config = XmlConfigHelper.Load(Global.ParameterCogfig);
                FillJobCombo();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        private void CloneLimitPosition(object sender, EventArgs e)
        {
            if (UseWaitCursor) return;
            try
            {
                StoreVisibleParameters();
                LimitEdit source;
                if (!_positionEdits.TryGetValue(LimitKey(_displayJob, _displayCamera, _displayPosition), out source) || source.Parameters.Count == 0)
                    throw new InvalidOperationException(TLimit("The selected Position has no parameters to clone.", "当前 Pos 没有可克隆的参数。"));
                ValidateLimitRows(source.Parameters);
                using (var dialog = new LimitCloneTargetForm(() => XmlConfigHelper.Load(Global.ParameterCogfig), source.Job, source.Camera, source.Position))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var latest = XmlConfigHelper.Load(Global.ParameterCogfig);
                    var target = FindLimitPosition(latest, dialog.TargetJob, dialog.TargetCamera, dialog.TargetPosition);
                    if (target == null) throw new InvalidOperationException(TLimit("Target no longer exists. Select it again.", "目标已被删除，请重新选择。"));
                    if (LimitKey(source.Job, source.Camera, source.Position).Equals(LimitKey(dialog.TargetJob, dialog.TargetCamera, dialog.TargetPosition), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(TLimit("Cannot clone to the same Position.", "不能克隆到自身。"));
                    string destination = dialog.TargetJob + " / " + dialog.TargetCamera + " / " + dialog.TargetPosition;
                    if (MessageBox.Show(this, TLimit("Replace the target Limit parameters with the current Position's parameters?\r\n", "将当前 Pos 的 Limit 参数复制并替换目标参数？\r\n") + destination +
                        TLimit("\r\nVPP and camera/channel settings are retained. Click Save afterwards to apply.", "\r\nVPP、相机及通道配置保持不变。完成后需点击 Save 生效。"), TLimit("Clone Limit parameters", "克隆 Limit 参数"), MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                    _positionEdits[LimitKey(dialog.TargetJob, dialog.TargetCamera, dialog.TargetPosition)] = new LimitEdit {
                        Job = dialog.TargetJob, Camera = dialog.TargetCamera, Position = dialog.TargetPosition, Parameters = CopyLimitParameters(source.Parameters) };
                    _config = latest;
                    FillJobCombo(dialog.TargetJob);
                    comboCam.SelectedItem = dialog.TargetCamera;
                    _comboPos.SelectedItem = dialog.TargetPosition;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, TLimit("Clone Limit parameters", "克隆 Limit 参数"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    internal sealed class LimitCloneTargetForm : Form
    {
        private readonly Func<AppConfig> _read;
        private readonly string _job, _camera, _position;
        private readonly ComboBox _jobs = new ComboBox(), _cameras = new ComboBox(), _positions = new ComboBox();
        private readonly Button _confirm = new Button { Text = "Clone", AutoSize = true };
        private bool _loading;
        internal string TargetJob => _jobs.SelectedItem as string;
        internal string TargetCamera => _cameras.SelectedItem as string;
        internal string TargetPosition => _positions.SelectedItem as string;
        internal LimitCloneTargetForm(Func<AppConfig> read, string job, string camera, string position)
        {
            _read = read; _job = job; _camera = camera; _position = position;
            Text = UiLanguage.Current == "zh-CN" ? "克隆 Limit 参数到" : "Clone Limit parameters to";
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(22) };
            var source = new Label { Text = (UiLanguage.Current == "zh-CN" ? "来源：" : "From: ") + job + " / " + camera + " / " + position, AutoSize = true, Margin = new Padding(4, 4, 4, 18) };
            layout.Controls.Add(source, 0, 0); layout.SetColumnSpan(source, 2);
            var selectors = new[] { _jobs, _cameras, _positions };
            for (int i = 0; i < selectors.Length; i++)
            {
                var combo = selectors[i]; combo.DropDownStyle = ComboBoxStyle.DropDownList; combo.Width = 200; combo.Margin = new Padding(8);
                layout.Controls.Add(new Label { Text = new[] { "Job:", "Cam:", "Pos:" }[i], AutoSize = true, Anchor = AnchorStyles.Left }, 0, i + 1);
                layout.Controls.Add(combo, 1, i + 1);
                combo.DropDown += (s, e) => Reload();
            }
            _jobs.SelectedIndexChanged += (s, e) => { if (!_loading) FillCameras(); };
            _cameras.SelectedIndexChanged += (s, e) => { if (!_loading) FillPositions(); };
            _positions.SelectedIndexChanged += (s, e) => UpdateConfirm();
            _confirm.Click += (s, e) => { if (_confirm.Enabled) { DialogResult = DialogResult.OK; Close(); } };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            layout.Controls.Add(cancel, 0, 4); layout.Controls.Add(_confirm, 1, 4);
            Controls.Add(layout); AcceptButton = _confirm; CancelButton = cancel;
            DesktopTheme.ApplySettings(this); Reload();
        }
        private void Fill(ComboBox combo, IEnumerable<string> names)
        {
            string previous = combo.SelectedItem as string;
            combo.Items.Clear(); foreach (string name in names) combo.Items.Add(name);
            if (previous != null && combo.Items.Contains(previous)) combo.SelectedItem = previous;
            else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        private void Reload()
        {
            _loading = true;
            try { Fill(_jobs, (_read().Models ?? new List<ModelConfig>()).Select(m => m.Name)); }
            finally { _loading = false; }
            FillCameras();
        }
        private void FillCameras()
        {
            _loading = true;
            try { Fill(_cameras, (_read().Models?.FirstOrDefault(m => m.Name == TargetJob)?.Cameras ?? new List<CameraConfig>()).Select(c => c.Name)); }
            finally { _loading = false; }
            FillPositions();
        }
        private void FillPositions()
        {
            _loading = true;
            try { Fill(_positions, (_read().Models?.FirstOrDefault(m => m.Name == TargetJob)?.Cameras?.FirstOrDefault(c => c.Name == TargetCamera)?.Positions ?? new List<PositionConfig>())
                .Where(p => !(TargetJob == _job && TargetCamera == _camera && p.Name == _position)).Select(p => p.Name)); }
            finally { _loading = false; }
            UpdateConfirm();
        }
        private void UpdateConfirm() { _confirm.Enabled = TargetJob != null && TargetCamera != null && TargetPosition != null && !(TargetJob == _job && TargetCamera == _camera && TargetPosition == _position); }
    }
}

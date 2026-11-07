using Aron_V2.CameraIntegration;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class SdkCameraForm : Form
    {
        private readonly AppConfig _config;
        private readonly string _category;
        private readonly ComboBox _jobs = Selector(), _cameras = Selector(), _positions = Selector();
        private readonly TextBox _serial = new TextBox { Width = 160, MaxLength = 128 };
        private readonly CameraBindingPanel _settings;
        private readonly Label _empty = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        private readonly Button _load = new Button { Text = "Load", AutoSize = true }, _save = new Button { Text = "Save", AutoSize = true };
        private bool _updating;

        internal SdkCameraForm(AppConfig config, string category)
        {
            _config = config; _category = category;
            Text = "Camera — " + category; StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            float scale = DesktopTheme.DisplayScale;
            ClientSize = new Size((int)(1000 * scale), (int)(560 * scale));
            MinimumSize = new Size((int)(760 * scale), (int)(430 * scale));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var toolbar = new FlowLayoutPanel { Name = "AlignedSelectorToolbar", Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(10, 8, 10, 8) };
            AddSelector(toolbar, "JobID:", _jobs); AddSelector(toolbar, "Camera:", _cameras);
            AddSelector(toolbar, "Position:", _positions);
            toolbar.Controls.Add(_load); toolbar.Controls.Add(_save); AddSelector(toolbar, "SN:", _serial);
            foreach (Control control in toolbar.Controls) control.Margin = new Padding(4);
            root.Controls.Add(toolbar, 0, 0);
            var body = new Panel { Dock = DockStyle.Fill };
            _settings = new CameraBindingPanel(true, category) { ReadSerial = () => _serial.Text,
                ReadPosition = () => _positions.SelectedItem as string,
                CanUseDevice = () => !(Owner is FormMain main) || main.CanTestSdkCamera,
                DeviceBlockReason = () => (Owner as FormMain)?.SdkCameraTestBlockReason(),
                DeviceState = () => (Owner as FormMain)?.SdkCameraTestState,
                AcquireDeviceLease = () => (Owner as FormMain)?.AcquireSdkTestGates() };
            body.Controls.Add(_settings); body.Controls.Add(_empty); root.Controls.Add(body, 0, 1); Controls.Add(root);
            _empty.Text = "No " + category + " cameras configured in this job.\r\nAdd a camera in GeneralConfig and select its Mode / SDK brand.\r\nThis category page is reserved for future providers.";
            _jobs.SelectedIndexChanged += (s, e) => { if (!_updating) RefreshCameras(); };
            _cameras.SelectedIndexChanged += (s, e) => { if (!_updating) RefreshPositions(); };
            _load.Click += (s, e) => ReloadBinding(); _save.Click += (s, e) => SaveBinding();
            _settings.SaveRequested += (s, e) => SaveBinding();
            // Keep the selected camera stable until the asynchronous operation
            // finishes, so a previous camera's image cannot appear on a new one.
            _settings.BusyChanged += (s, e) => toolbar.Enabled = !_settings.IsBusy;
            Load += (s, e) =>
            {
                _updating = true;
                try { foreach (var job in _config?.Models ?? Enumerable.Empty<ModelConfig>()) if (job != null) _jobs.Items.Add(job.Name); if (_jobs.Items.Count > 0) _jobs.SelectedIndex = 0; }
                finally { _updating = false; }
                RefreshCameras();
            };
            DesktopTheme.ApplySettings(this);
        }

        private static ComboBox Selector() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 }; }
        private static void AddSelector(FlowLayoutPanel panel, string text, Control selector)
        { panel.Controls.Add(new Label { Text = text, AutoSize = true }); panel.Controls.Add(selector); }
        private CameraConfig SelectedCamera { get { return CameraAcquisition.Find(_config, _jobs.SelectedItem as string, _cameras.SelectedItem as string); } }

        private void RefreshCameras()
        {
            _updating = true;
            try
            {
                _cameras.Items.Clear();
                var job = _config?.Models?.FirstOrDefault(m => m.Name == _jobs.SelectedItem as string);
                foreach (var camera in job?.Cameras ?? Enumerable.Empty<CameraConfig>())
                    if (CameraAcquisition.MatchesCategory(camera, _category)) _cameras.Items.Add(camera.Name);
                if (_cameras.Items.Count > 0) _cameras.SelectedIndex = 0;
            }
            finally { _updating = false; }
            RefreshPositions();
        }

        private void RefreshPositions()
        {
            _updating = true;
            try
            {
                _positions.Items.Clear();
                foreach (var position in SelectedCamera?.Positions ?? Enumerable.Empty<PositionConfig>()) if (position != null) _positions.Items.Add(position.Name);
                if (_positions.Items.Count > 0) _positions.SelectedIndex = 0;
            }
            finally { _updating = false; }
            var camera = SelectedCamera;
            _empty.Visible = camera == null; _settings.Visible = camera != null;
            _load.Enabled = _save.Enabled = _serial.Enabled = camera != null;
            _serial.Text = camera?.SerialNumber ?? "";
            if (camera != null) _settings.Bind(_jobs.SelectedItem as string, camera);
        }

        private void SaveBinding()
        {
            if (SelectedCamera == null) return;
            try
            {
                if (Owner is FormMain main && !main.CanTestSdkCamera) throw new InvalidOperationException(main.SdkCameraTestBlockReason());
                var draft = _settings.Draft();
                CameraAcquisition.SaveBinding(Global.ParameterCogfig, _jobs.SelectedItem as string, _cameras.SelectedItem as string, draft);
                CameraAcquisition.CopyBinding(draft, SelectedCamera);
                _settings.ShowStatus("Binding saved. Restart before production.");
                MessageBox.Show(this, "Camera binding saved. Restart before production to apply it. Communication settings are unchanged.", "SDK Camera", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "Save SDK Camera Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ReloadBinding()
        {
            try
            {
                var fresh = CameraAcquisition.Find(XmlConfigHelper.Load(Global.ParameterCogfig), _jobs.SelectedItem as string, _cameras.SelectedItem as string);
                if (fresh == null || !CameraAcquisition.MatchesCategory(fresh, _category)) throw new InvalidOperationException("The selected camera was removed or belongs to a different category. Reopen this page.");
                CameraAcquisition.CopyBinding(fresh, SelectedCamera); _serial.Text = fresh.SerialNumber ?? "";
                _settings.Bind(_jobs.SelectedItem as string, fresh); _settings.ShowStatus("Binding loaded from disk. No device connection was started.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, "Load SDK Camera Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}

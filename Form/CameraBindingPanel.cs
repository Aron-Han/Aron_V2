using Aron_V2.CameraIntegration;
using Cognex.VisionPro;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    // Pure WinForms shell: no SDK/editor construction and no connection on open.
    internal sealed class CameraBindingPanel : UserControl
    {
        private readonly RadioButton _cognex = new RadioButton { Text = "Cognex acquisition tool", AutoSize = true };
        private readonly RadioButton _sdk = new RadioButton { Text = "SDK acquisition tool", AutoSize = true };
        private readonly ComboBox _provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _ip = new TextBox(), _directory = new TextBox();
        private readonly NumericUpDown _timeout = new NumericUpDown { Minimum = 100, Maximum = 120000, Increment = 500, Value = 10000 };
        private readonly Label _context = new Label { AutoSize = true, Dock = DockStyle.Fill };
        private readonly TextBox _log = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White };
        private readonly PictureBox _preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(244, 247, 250), BorderStyle = BorderStyle.FixedSingle };
        private readonly TableLayoutPanel _sdkBody;
        private readonly Button _test = new Button { Text = "Test Capture", AutoSize = true }, _diagnose = new Button { Text = "Diagnostics", AutoSize = true };
        private bool _binding, _busy;
        private readonly bool _sdkOnly;
        private readonly string _category;
        internal event EventHandler ModeChanged;
        internal event EventHandler SaveRequested;
        internal event EventHandler BusyChanged;
        internal bool IsBusy { get { return _busy; } }
        internal Func<string> ReadSerial;
        // SDK binding is camera-level, but a test capture is made while a
        // concrete position is selected. It can therefore be offered to that
        // position's algorithm editor as its latest 3D image.
        internal Func<string> ReadPosition;
        internal Func<bool> CanUseDevice;
        internal bool UseSdk { get { return _sdk.Checked; } }
        internal string JobName { get; private set; }
        internal string CameraName { get; private set; }

        internal CameraBindingPanel(bool sdkOnly = false, string category = null)
        {
            _sdkOnly = sdkOnly; _category = category;
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var save = new Button { Text = "Save Binding", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            DesktopTheme.StyleButton(save, true);
            modes.Controls.AddRange(new Control[] { _cognex, _sdk, save });
            modes.Visible = !sdkOnly;
            foreach (Control control in modes.Controls) control.Margin = new Padding(6, 6, 12, 6);
            save.Click += (s, e) => { if (!_busy) SaveRequested?.Invoke(this, EventArgs.Empty); };
            root.Controls.Add(modes, 0, 0); root.Controls.Add(_context, 0, 1);
            _context.Margin = new Padding(6, 8, 6, 12);
            _sdkBody = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            _sdkBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            _sdkBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            var settingsHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _provider.Items.AddRange(CameraAcquisition.Providers.Where(p => category == null || p.Category == category).ToArray());
            if (_provider.Items.Count > 0) _provider.SelectedIndex = 0;
            // SDK pages use the provider chosen by Add Camera. Retain its ID
            // and legacy directory in the draft, but do not expose a second selector.
            if (!sdkOnly) AddField(fields, "SDK provider", _provider);
            AddField(fields, "Sensor IP", _ip);
            if (!sdkOnly)
            {
                var directory = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = Padding.Empty };
                directory.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); directory.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var browse = new Button { Text = "Browse…", AutoSize = true };
                _directory.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                directory.Controls.Add(_directory, 0, 0); directory.Controls.Add(browse, 1, 0);
                browse.Click += (s, e) => { using (var dialog = new FolderBrowserDialog { Description = "Select GoSDK x64 bin/win64 directory" }) if (dialog.ShowDialog(this) == DialogResult.OK) _directory.Text = dialog.SelectedPath; };
                AddField(fields, "SDK directory", directory);
            }
            AddField(fields, "Timeout (ms)", _timeout);
            var hint = new Label { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(500, 0),
                Text = "SN in the top toolbar is optional and is checked against the sensor ID.\r\nEach Camera has its own device binding, shared by all its Positions. Channels are configured in GeneralConfig.\r\nUse a different IP for each physical sensor. With Accelerator, enter the virtual sensor IP.\r\nLMI Gocator requires compatible GoSDK/firmware and Uniform Surface output; Profile output is not supported by this adapter.\r\nGoSDK is located automatically. Deploy one complete x64 runtime in the application's LmiSdk folder.\r\nSaved bindings take effect after restarting the application. Test/Diagnostics use the current draft without saving." };
            int row = fields.RowCount++; fields.RowStyles.Add(new RowStyle(SizeType.AutoSize)); fields.Controls.Add(hint, 0, row); fields.SetColumnSpan(hint, 2); hint.Margin = new Padding(6, 16, 6, 12);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            actions.Controls.AddRange(new Control[] { _test, _diagnose });
            row = fields.RowCount++; fields.RowStyles.Add(new RowStyle(SizeType.AutoSize)); fields.Controls.Add(actions, 0, row); fields.SetColumnSpan(actions, 2);
            _test.Click += async (s, e) => await RunDeviceOperation(true);
            _diagnose.Click += async (s, e) => await RunDeviceOperation(false);
            settingsHost.Controls.Add(fields); _sdkBody.Controls.Add(settingsHost, 0, 0);
            var results = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); results.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); results.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            results.Controls.Add(_preview, 0, 0); results.Controls.Add(_log, 0, 1); _sdkBody.Controls.Add(results, 1, 0);
            root.Controls.Add(_sdkBody, 0, 2); Controls.Add(root);
            _sdk.CheckedChanged += (s, e) => { UpdateMode(); if (!_binding) ModeChanged?.Invoke(this, EventArgs.Empty); };
            _cognex.Checked = !sdkOnly; _sdk.Checked = sdkOnly; UpdateMode();
            Disposed += (s, e) =>
            {
                _preview.Image?.Dispose();
                if (_sdkOnly) { _provider.Dispose(); _directory.Dispose(); }
            };
        }

        private static void AddField(TableLayoutPanel fields, string label, Control control)
        {
            int row = fields.RowCount++; fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fields.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 10, 14, 10) }, 0, row);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right; control.Margin = new Padding(4, 8, 4, 8);
            fields.Controls.Add(control, 1, row);
        }

        internal void Bind(string job, CameraConfig camera)
        {
            _binding = true;
            try
            {
                JobName = job; CameraName = camera?.Name;
                _sdk.Checked = _sdkOnly || CameraAcquisition.IsSdk(camera); _cognex.Checked = !_sdk.Checked;
                string mode = camera?.ConnectionMode ?? CameraAcquisition.Lmi;
                var provider = _provider.Items.Cast<SdkCameraProvider>().FirstOrDefault(p => string.Equals(p.Id, mode, StringComparison.OrdinalIgnoreCase));
                if (_sdk.Checked && provider == null)
                { provider = new SdkCameraProvider(mode, "Unsupported: " + mode, null); _provider.Items.Add(provider); }
                _provider.SelectedItem = provider ?? _provider.Items.Cast<SdkCameraProvider>().FirstOrDefault();
                _ip.Text = camera?.IpAddress ?? ""; _directory.Text = camera?.SdkDirectory ?? "";
                _timeout.Value = Math.Max(_timeout.Minimum, Math.Min(_timeout.Maximum, camera?.CaptureTimeoutMs > 0 ? camera.CaptureTimeoutMs : 10000));
                _log.Clear(); var old = _preview.Image; _preview.Image = null; old?.Dispose();
                UpdateMode();
            }
            finally { _binding = false; }
        }

        internal CameraConfig Draft()
        {
            return new CameraConfig { Name = CameraName, ConnectionMode = UseSdk ? (_provider.SelectedItem as SdkCameraProvider)?.Id : CameraAcquisition.Cognex,
                SdkCategory = _category,
                IpAddress = _ip.Text.Trim(), SerialNumber = ReadSerial?.Invoke() ?? "", SdkDirectory = _directory.Text.Trim(), CaptureTimeoutMs = (int)_timeout.Value };
        }

        private void UpdateMode()
        {
            _sdkBody.Visible = UseSdk;
            _context.Text = (JobName ?? "") + " / " + (CameraName ?? "") + " — " +
                (UseSdk ? ((_provider.SelectedItem as SdkCameraProvider)?.Name ?? "SDK") + " — device binding shared by all Positions. Restart after saving." : "Cognex Camera.vpp editor below. Existing Load / Save behavior is retained.");
        }

        internal void ShowStatus(string text) { _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + text + "\r\n"); }

        private async Task RunDeviceOperation(bool capture)
        {
            if (_busy) return;
            if (CanUseDevice != null && !CanUseDevice())
            { MessageBox.Show(this, "Switch Offline and wait for current inspections to finish before SDK testing.", "SDK camera", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var config = Draft(); string error = CameraAcquisition.Validate(config);
            if (error != null) { ShowStatus(error); return; }
            _busy = true; Enabled = false;
            BusyChanged?.Invoke(this, EventArgs.Empty);
            var owner = FindForm(); if (owner != null) owner.FormClosing += PreventClosing;
            try
            {
                ShowStatus(capture ? "Capturing " + config.IpAddress + "…" : "Checking " + config.IpAddress + "…");
                if (capture)
                {
                    var watch = Stopwatch.StartNew();
                    ICogImage image = await Task.Run(() => CameraAcquisition.Capture(config));
                    bool retained = false;
                    try
                    {
                        Bitmap bitmap = VpImageUtil.ToManagedBitmap(image);
                        if (bitmap == null) throw new InvalidOperationException("Capture returned an image, but VisionPro could not convert it to a preview bitmap.");
                        var previous = _preview.Image; _preview.Image = bitmap; previous?.Dispose();
                        string position = ReadPosition == null ? null : ReadPosition();
                        if (!string.IsNullOrWhiteSpace(JobName) && !string.IsNullOrWhiteSpace(CameraName) && !string.IsNullOrWhiteSpace(position))
                        {
                            Load_Job.VppCache.RememberLatestInspectionImage(JobName, CameraName, position, image);
                            retained = true;
                            ShowStatus($"Capture complete: {bitmap.Width} × {bitmap.Height}; {watch.Elapsed.TotalMilliseconds:F0} ms. Latest original 3D image is available in Algorithm for {position}.");
                        }
                        else ShowStatus($"Capture complete: {bitmap.Width} × {bitmap.Height}; {watch.Elapsed.TotalMilliseconds:F0} ms.");
                    }
                    finally
                    {
                        // The cache owns the lifetime of a retained original
                        // image for this application's session. The preview is
                        // an independent Bitmap and the non-retained path can
                        // release its SDK image immediately.
                        if (!retained) (image as IDisposable)?.Dispose();
                    }
                }
                else ShowStatus(await Task.Run(() => CameraAcquisition.Diagnose(config)));
            }
            catch (Exception ex) { ShowStatus("FAIL: " + ex.GetBaseException().Message); }
            finally { if (owner != null) owner.FormClosing -= PreventClosing; _busy = false; Enabled = true; BusyChanged?.Invoke(this, EventArgs.Empty); }
        }

        private void PreventClosing(object sender, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) e.Cancel = true; }
    }
}

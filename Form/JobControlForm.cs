using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Aron_V2
{
    internal sealed class JobControlForm : Form
    {
        private readonly JobCatalog _catalog;
        private readonly Func<bool> _canChange;
        private readonly Func<string, bool> _activeJob;
        private readonly ComboBox _jobs = Selector(), _source = Selector();
        private readonly TextBox _name = new TextBox { Name = "NewJobName", Width = 180 };
        private readonly DataGridView _cameras = new DataGridView { Name = "JobCameraList", Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false, RowHeadersVisible = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoSize = true, ForeColor = DesktopTheme.Muted };
        internal bool IsBusy { get; private set; }
        internal Func<string> BlockReason { get; set; }
        internal event EventHandler ConfigSaved;
        internal JobControlForm(JobCatalog catalog, Func<bool> canChange, Func<string, bool> activeJob)
        {
            _catalog = catalog; _canChange = canChange; _activeJob = activeJob;
            Text = "Job Control"; BackColor = Color.White;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 1, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(i == 4 ? SizeType.Percent : SizeType.AutoSize, i == 4 ? 100 : 0));
            layout.Controls.Add(Row(new Label { Text = "JobN:", AutoSize = true }, _jobs,
                Command("Delete Job", () => { DeleteJob(); return Task.CompletedTask; })), 0, 0);
            layout.Controls.Add(Row(new Label { Text = "New Job:", AutoSize = true }, _name,
                Command("Create empty Job", () => Create(false))), 0, 1);
            layout.Controls.Add(Row(new Label { Text = "Clone from:", AutoSize = true }, _source,
                Command("Clone Job", () => Create(true))), 0, 2);
            layout.Controls.Add(Row(Command("Add Camera", () => { AddCamera(); return Task.CompletedTask; }), Command("Delete Camera", () => { DeleteCamera(); return Task.CompletedTask; })), 0, 3);
            foreach (string caption in new[] { "Camera", "Mode", "Position" })
                _cameras.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = caption, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            layout.Controls.Add(_cameras, 0, 4); layout.Controls.Add(_status, 0, 5); Controls.Add(layout);
            _jobs.SelectedIndexChanged += (s, e) => RefreshCameras();
            Load += (s, e) => RefreshCatalog();
            DesktopTheme.ApplySettings(this);
        }
        private static ComboBox Selector() => new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private static TableLayoutPanel Row(params Control[] controls)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = controls.Length + 1, RowCount = 1, Padding = new Padding(0, 4, 0, 4) };
            float scale = DesktopTheme.DisplayScale;
            for (int i = 0; i < controls.Length; i++)
            {
                Control control = controls[i];
                row.ColumnStyles.Add(new ColumnStyle(control is Label ? SizeType.Absolute : SizeType.AutoSize, control is Label ? 88 * scale : 0));
                control.Anchor = AnchorStyles.Left; control.Margin = new Padding(4, 6, 8, 6);
                if (control is ComboBox || control is TextBox) control.Width = (int)(180 * scale);
                row.Controls.Add(control, i, 0);
            }
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return row;
        }
        private Button Command(string text, Func<Task> action)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(110, 32) };
            button.Click += async (s, e) =>
            {
                try
                {
                    string reason = BlockReason?.Invoke();
                    if (reason != null) throw new InvalidOperationException(reason);
                    if (BlockReason == null && _canChange != null && !_canChange()) throw new InvalidOperationException(L("Switch Offline and wait for inspections before editing Job Control.", "请切换离线并等待检测结束后修改作业。"));
                    IsBusy = true; Enabled = false; UseWaitCursor = true;
                    await action();
                }
                catch (Exception ex) { MessageBox.Show(this, UiLanguage.Text(ex.GetBaseException().Message), UiLanguage.Text("Job Control"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { IsBusy = false; if (!IsDisposed) { Enabled = true; UseWaitCursor = false; } }
            };
            return button;
        }
        private static string L(string english, string chinese) => UiLanguage.Current == "zh-CN" ? chinese : english;
        private void RefreshCatalog(string preferred = null)
        {
            string selected = preferred ?? _jobs.SelectedItem as string, source = _source.SelectedItem as string;
            var config = _catalog.Read();
            foreach (var combo in new[] { _jobs, _source })
            {
                combo.Items.Clear(); foreach (var job in config.Models) combo.Items.Add(job.Name);
                string wanted = combo == _jobs ? selected : source;
                if (wanted != null && combo.Items.Contains(wanted)) combo.SelectedItem = wanted;
                else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            }
            try { _name.Text = JobCatalog.NextName(config.Models.Select(m => m.Name), "Job"); }
            catch (InvalidOperationException) { _name.Text = ""; }
            RefreshCameras();
            _status.Text = "Clone includes Limits, camera bindings and all configured VPPs. Deleted VPP files are retained.";
            _status.MaximumSize = new Size(Math.Max(200, ClientSize.Width - 24), 0);
        }
        private void RefreshCameras()
        {
            _cameras.Rows.Clear();
            var model = _catalog.Read().Models.FirstOrDefault(m => m.Name == _jobs.SelectedItem as string);
            foreach (var camera in model?.Cameras ?? Enumerable.Empty<CameraConfig>())
                _cameras.Rows.Add(camera.Name, CameraIntegration.CameraAcquisition.IsSdk(camera) ? "SDK / " + camera.SdkCategory : "Visionpro",
                    string.Join(", ", (camera.Positions ?? Enumerable.Empty<PositionConfig>()).Select(p => p.Name)));
        }
        private void Committed(string preferred = null)
        {
            RefreshCatalog(preferred); ConfigSaved?.Invoke(this, EventArgs.Empty);
        }
        private async Task Create(bool clone)
        {
            string name = _name.Text.Trim();
            string source = clone ? _source.SelectedItem as string : null;
            if (clone && source == null) return;
            await Task.Run(() => _catalog.CreateJob(name, source)); Committed(name);
        }
        private void DeleteJob()
        {
            string job = _jobs.SelectedItem as string; if (job == null) return;
            if (_activeJob != null && _activeJob(job)) throw new InvalidOperationException(L("This Job is currently selected by a runtime channel. Select another Job before deleting it.", "此作业正被运行通道选用，请先切换到其它作业再删除。"));
            if (MessageBox.Show(this, L("Delete " + job + " configuration? VPP files will be retained.", "删除 " + job + " 的配置？磁盘 VPP 文件保留。"), UiLanguage.Text("Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _catalog.DeleteJob(job); Committed();
        }
        private void AddCamera()
        {
            string job = _jobs.SelectedItem as string; if (job == null) return;
            using (var dialog = new AddCameraDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var model = _catalog.Read().Models.First(m => m.Name == job);
                string name = JobCatalog.NextName((model.Cameras ?? Enumerable.Empty<CameraConfig>()).Select(c => c.Name), "Cam");
                _catalog.AddCamera(job, dialog.CreateCamera(name)); Committed(job);
            }
        }
        private void DeleteCamera()
        {
            string job = _jobs.SelectedItem as string, camera = _cameras.CurrentRow?.Cells[0].Value as string;
            if (job == null || camera == null || UiLanguage.ConfirmCameraDeletion(this, camera) != DialogResult.Yes) return;
            _catalog.DeleteCamera(job, camera); Committed(job);
        }
    }
}

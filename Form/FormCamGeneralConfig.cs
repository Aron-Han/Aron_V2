using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	public partial class FormCamGeneralConfig : Form
	{

        private readonly AppConfig _config;
        private readonly string _configPath;

        private readonly BindingList<PosGeneralRow> _rows = new BindingList<PosGeneralRow>();
        private bool _gridBuilt;
        private bool _updatingSelectors;
        private readonly Label _modeLabel = new Label { Name = "CameraMode", Text = "Mode: —", AutoSize = true };

        public event EventHandler ConfigSaved;

        public FormCamGeneralConfig(AppConfig config, string configPath)
        {
            _config = config;
            _configPath = configPath;

            InitializeComponent();

            // 事件（只绑一次，避免重复弹“保存成功”）
            this.Load += Form_Load;
            CboJob.SelectedIndexChanged += CboJob_SelectedIndexChanged;
            CboCam.SelectedIndexChanged += CboCam_SelectedIndexChanged;

            btnAddRow.Click += BtnAddRow_Click;
            btnDeleteRow.Click += BtnDeleteRow_Click;
            btnSave.Click += BtnSave_Click;
            btnAddCam.Visible = btnDeleteCam.Visible = false;

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.MultiSelect = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.DataSource = _rows;
            InitializeCompactLayout();
            DesktopTheme.ApplySettings(this);
        }

        private void InitializeCompactLayout()
        {
            SuspendLayout();
            DoubleBuffered = true;
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(dataGridView1, true, null);
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.BackgroundColor = SystemColors.Window;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            dataGridView1.AllowUserToResizeRows = false;
            CboJob.DropDownStyle = CboCam.DropDownStyle = ComboBoxStyle.DropDownList;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            float scale = DeviceDpi / 96F;
            foreach (Button button in new[] { btnAddCam, btnDeleteCam, btnAddRow, btnDeleteRow, btnSave })
            {
                button.AutoSize = true;
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                button.MinimumSize = new Size((int)(90 * scale), (int)(30 * scale));
                button.Margin = new Padding(4);
            }
            var filters = new FlowLayoutPanel { Name = "AlignedSelectorToolbar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            label3.Text = "Cam count:";
            label3.AutoSize = lblCamCount.AutoSize = true;
            label3.TextAlign = lblCamCount.TextAlign = ContentAlignment.MiddleLeft;
            foreach (Control control in new Control[] { label1, CboJob, label2, CboCam, _modeLabel, label3, lblCamCount })
            {
                control.Margin = new Padding(4, 8, 4, 4);
                filters.Controls.Add(control);
            }
            var commands = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            commands.Controls.AddRange(new Control[] { btnAddRow, btnDeleteRow });
            var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            actions.Controls.Add(btnSave);
            layout.Controls.Add(filters, 0, 0); layout.Controls.Add(commands, 0, 1); layout.Controls.Add(dataGridView1, 0, 2); layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);
            ResumeLayout(true);
        }

        #region 加载/联动

        private void Form_Load(object sender, EventArgs e)
        {
            BuildGridColumns();   // 只建一次列
            FillJobs();
            if (CboJob.Items.Count > 0) CboJob.SelectedIndex = 0;
        }

        private void CboJob_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingSelectors) return;
            FillCams();
            RefreshGrid();
            UpdateCamCountLabel();
        }

        private void CboCam_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_updatingSelectors) return;
            RefreshGrid();
        }

        private void FillJobs()
        {
            CboJob.Items.Clear();
            if (_config?.Models != null)
            {
                foreach (var m in _config.Models)
                    CboJob.Items.Add(m.Name ?? "");
            }
        }

        private void FillCams(string preferred = null)
        {
            var job = GetCurrentJob();
            string old = preferred ?? CboCam.SelectedItem as string;
            _updatingSelectors = true;
            CboCam.BeginUpdate();
            try
            {

            CboCam.Items.Clear();
            if (job?.Cameras == null) return;

            foreach (var cam in job.Cameras)
                CboCam.Items.Add(cam.Name ?? "");

            // 尽量还原旧选择
            if (!string.IsNullOrEmpty(old) && CboCam.Items.Contains(old))
                CboCam.SelectedItem = old;
            else if (CboCam.Items.Count > 0)
                CboCam.SelectedIndex = 0;
            }
            finally { CboCam.EndUpdate(); _updatingSelectors = false; }
        }

        private void UpdateCamCountLabel()
        {
            var job = GetCurrentJob();
            int cnt = job?.Cameras?.Count ?? 0;
            lblCamCount.Text = cnt.ToString();            // CamN 自动显示
            if (job == null) return;
            if (job?.General == null) job.General = new GeneralConfig();
            job.General.CamN = cnt.ToString();            // 顺带把 CamN 回写到 General
        }

        private ModelConfig GetCurrentJob()
        {
            var name = CboJob.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name) || _config?.Models == null) return null;
            return _config.Models.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private CameraConfig GetCurrentCam()
        {
            var job = GetCurrentJob();
            var camName = CboCam.SelectedItem as string;
            if (job?.Cameras == null || string.IsNullOrWhiteSpace(camName)) return null;
            return job.Cameras.FirstOrDefault(c => string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Grid

        private void BuildGridColumns()
        {
            if (_gridBuilt) return;
            _gridBuilt = true;

            dataGridView1.Columns.Clear();

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Job",
                DataPropertyName = nameof(PosGeneralRow.Model),
                ReadOnly = true,
                Width = 120
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Cam",
                DataPropertyName = nameof(PosGeneralRow.Camera),
                ReadOnly = true,
                Width = 100
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Pos",
                DataPropertyName = nameof(PosGeneralRow.Position),
                Width = 120
            });

            // MainUsed
            var colMainUsed = new DataGridViewComboBoxColumn
            {
                HeaderText = "Main Used",
                DataPropertyName = nameof(PosGeneralRow.MainUsed),
                Width = 90,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colMainUsed.Items.Add("0");
            colMainUsed.Items.Add("1");
            dataGridView1.Columns.Add(colMainUsed);

            // MainChannel
            var colMainCh = new DataGridViewComboBoxColumn
            {
                HeaderText = "Main Channel",
                DataPropertyName = nameof(PosGeneralRow.MainChannel),
                Width = 110,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colMainCh.Items.AddRange(new object[] { "0", "1", "2", "3" });
            dataGridView1.Columns.Add(colMainCh);

            // SecondUsed
            var colSecUsed = new DataGridViewComboBoxColumn
            {
                HeaderText = "Second Used",
                DataPropertyName = nameof(PosGeneralRow.SecondUsed),
                Width = 100,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colSecUsed.Items.Add("0");
            colSecUsed.Items.Add("1");
            dataGridView1.Columns.Add(colSecUsed);

            // SecondChannel
            var colSecCh = new DataGridViewComboBoxColumn
            {
                HeaderText = "Second Channel",
                DataPropertyName = nameof(PosGeneralRow.SecondChannel),
                Width = 120,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };
            colSecCh.Items.AddRange(new object[] { "0", "1", "2", "3" });
            dataGridView1.Columns.Add(colSecCh);
            dataGridView1.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.FillWeight = column.Width;
                column.MinimumWidth = 55;
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
        }

		private void RefreshGrid()
		{
            _modeLabel.Text = "Mode: " + (GetCurrentCam() == null ? "—" : CameraIntegration.CameraAcquisition.IsSdk(GetCurrentCam()) ? "SDK" : "Visionpro");
			_rows.RaiseListChangedEvents = false;
			dataGridView1.SuspendLayout();
			try
			{
			_rows.Clear();

			var job = GetCurrentJob();
			var cam = GetCurrentCam();
			if (job == null || cam == null) return;

			if (cam.Positions == null || cam.Positions.Count == 0)
			{
				// 没有任何 Pos，给一个占位行，便于用户新增
				_rows.Add(new PosGeneralRow
				{
					Model = job.Name,
					Camera = cam.Name,

					// 当前没有旧 Pos，所以原始 Pos 为空
					OriginalPosition = "",

					Position = "Pos1",
					MainUsed = "1",
					MainChannel = "0",
					SecondUsed = "0",
					SecondChannel = "0"
				});
				return;
			}

			// 把该 Cam 下所有 Pos 一次性列出来
			foreach (var pos in cam.Positions)
			{
				if (pos == null) continue;

				var g = pos.General ?? CreateDefaultPositionGeneral();

				_rows.Add(new PosGeneralRow
				{
					Model = job.Name,
					Camera = cam.Name,

					// 记录原始 Pos 名称，用于保存时迁移 Limit 参数
					OriginalPosition = pos.Name ?? "",

					Position = pos.Name ?? "",
					MainUsed = g.MainUsed ?? "0",
					MainChannel = g.MainChannel ?? "0",
					SecondUsed = g.SecondUsed ?? "0",
					SecondChannel = g.SecondChannel ?? "0"
				});
			}
			}
			finally
			{
				_rows.RaiseListChangedEvents = true;
				_rows.ResetBindings();
				dataGridView1.ResumeLayout();
			}
		}

		#endregion

		#region 

		private void BtnAddRow_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            // 自动生成一个不重复的 Pos 名
            var existing = new HashSet<string>(_rows.Select(r => r.Position ?? ""), StringComparer.OrdinalIgnoreCase);
            int n = 1;
            string next;
            do { next = "Pos" + n; n++; } while (existing.Contains(next));

            _rows.Add(new PosGeneralRow
            {
                Model = job.Name,
                Camera = cam.Name,
                Position = next,
                MainUsed = "0",
                MainChannel = "0",
                SecondUsed = "0",
                SecondChannel = "0"
            });

            if (_rows.Count > 0)
            {
                dataGridView1.ClearSelection();
                dataGridView1.Rows[_rows.Count - 1].Selected = true;
                dataGridView1.FirstDisplayedScrollingRowIndex = _rows.Count - 1;
            }
        }

        private void BtnDeleteRow_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0) return;
            var toRemove = new List<PosGeneralRow>();
            foreach (DataGridViewRow r in dataGridView1.SelectedRows)
            {
                var item = r.DataBoundItem as PosGeneralRow;
                if (item != null) toRemove.Add(item);
            }
            foreach (var it in toRemove)
                _rows.Remove(it);
        }

        private void BtnAddCam_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            if (job == null) return;

            if (job.Cameras == null) job.Cameras = new List<CameraConfig>();

            // 生成一个唯一相机名：Cam1/Cam2/...（找最大编号+1）
            string newName = GenerateNextCamName(job);

            using (var dialog = new AddCameraDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var camera = dialog.CreateCamera(newName);
                if (!EnsureCameraVppFiles(job.Name, camera)) return;
                job.Cameras.Add(camera);
            }

            // UI 刷新并选中新相机
            FillCams(newName);
            UpdateCamCountLabel();
            RefreshGrid();

            // 立刻落盘（可留到点“保存”时统一保存，二选一）
            XmlConfigHelper.Save(_config, _configPath);
            // MessageBox.Show($"已添加：{newName}");
        }

        private string GenerateNextCamName(ModelConfig job)
        {
            int maxNo = 0;
            foreach (var c in job.Cameras)
            {
                int n = TryParseCamIndex(c?.Name);
                if (n > maxNo) maxNo = n;
            }
            return "Cam" + (maxNo + 1);
        }

        private int TryParseCamIndex(string camName)
        {
            if (string.IsNullOrWhiteSpace(camName)) return 0;
            // Cam10, CAM2, cam003 都兼容
            if (camName.Length >= 4 &&
                camName.StartsWith("Cam", StringComparison.OrdinalIgnoreCase))
            {
                int n;
                if (int.TryParse(camName.Substring(3), out n)) return n;
            }
            return 0;
        }

        private void BtnDeleteCam_Click(object sender, EventArgs e)
        {
            var job = GetCurrentJob();
            var cam = GetCurrentCam();
            if (job == null || cam == null) return;

            if (UiLanguage.ConfirmCameraDeletion(this, cam.Name) != DialogResult.Yes)
                return;

            job.Cameras.Remove(cam);

            FillCams();                 // 重新填充下拉

            UpdateCamCountLabel();      // CamN 同步
            RefreshGrid();

            XmlConfigHelper.Save(_config, _configPath);
        }

		#endregion

		#region 保存

		private void BtnSave_Click(object sender, EventArgs e)
		{
			dataGridView1.EndEdit();
			BindingContext[_rows].EndCurrentEdit();
			var job = GetCurrentJob();
			var cam = GetCurrentCam();
			if (job == null || cam == null) return;

			if (job.Cameras == null)
				job.Cameras = new List<CameraConfig>();

			if (cam.Positions == null)
				cam.Positions = new List<PositionConfig>();

			// 1) 先缓存旧 Pos，避免清空后丢失 Parameters
			var oldPositions = cam.Positions.ToList();

			var oldByName = oldPositions
				.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
				.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

			// 2) 清空并重建 Pos，支持删除行
			cam.Positions.Clear();

			for (int i = 0; i < _rows.Count; i++)
			{
				var r = _rows[i];
				if (r == null) continue;

				string newPosName = string.IsNullOrWhiteSpace(r.Position)
					? "Pos1"
					: r.Position.Trim();

				// 3) 找旧 Pos
				// 优先用 OriginalPosition，因为用户可能把 Pos1 改成 Pos2
				PositionConfig oldPos = null;

				if (!string.IsNullOrWhiteSpace(r.OriginalPosition))
				{
					oldByName.TryGetValue(r.OriginalPosition.Trim(), out oldPos);
				}

				// 如果 OriginalPosition 没找到，再用当前 Pos 名找
				if (oldPos == null)
				{
					oldByName.TryGetValue(newPosName, out oldPos);
				}

				// 最后兜底：按行号匹配，避免老版本没有 OriginalPosition 时直接丢参数
				if (oldPos == null && i >= 0 && i < oldPositions.Count)
				{
					oldPos = oldPositions[i];
				}

				var pos = new PositionConfig
				{
					Name = newPosName,

					General = new PositionGeneralConfig
					{
						Exposure = oldPos?.General?.Exposure ?? "10",
						MainUsed = r.MainUsed ?? "0",
						MainChannel = r.MainChannel ?? "0",
						SecondUsed = r.SecondUsed ?? "0",
						SecondChannel = r.SecondChannel ?? "0"
					},

					// 关键：把旧的 Limit 参数带回来
					Parameters = CloneParameters(oldPos != null ? oldPos.Parameters : null)
				};

				cam.Positions.Add(pos);

				// 保存后更新原始名，避免连续保存时再次匹配错
				r.OriginalPosition = newPosName;
			}

			UpdateCamCountLabel();

            if (!EnsureCameraVppFiles(job.Name, cam)) return;
			XmlConfigHelper.Save(_config, _configPath);

			MessageBox.Show(UiLanguage.Text("Saved successfully！"), "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);

			var h = ConfigSaved;
			if (h != null) h(this, EventArgs.Empty);
		}

		#endregion

		#region 辅助类型/默认值

        private bool EnsureCameraVppFiles(string job, CameraConfig camera)
        {
            try
            {
                var created = BlankCameraVpp.EnsureForCamera(Global.Vpp_Root, job, camera);
                foreach (string file in created)
                    UI_Update.LogChangeEventArgs.Set("Log", "Created blank VPP (not configured for production): " + file, Color.Green);
                return true;
            }
            catch (Exception ex)
            {
                UI_Update.LogChangeEventArgs.Set("Log", "Create missing camera VPP failed: " + ex, Color.Red);
                MessageBox.Show(this, "无法创建缺失的空白 VPP，配置尚未保存。已有 VPP 不会覆盖。\r\n" + ex.GetBaseException().Message,
                    "Create VPP failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

		private static PositionGeneralConfig CreateDefaultPositionGeneral()
        {
            return new PositionGeneralConfig
            {
                Exposure = "10",
                MainUsed = "1",
                MainChannel = "0",
                SecondUsed = "0",
                SecondChannel = "0"
            };
        }

        private class PosGeneralRow
        {
			public string Model { get; set; }
			public string Camera { get; set; }

			// 原始 Pos 名称，用来在用户改名后仍然找到旧 Limit 参数
			public string OriginalPosition { get; set; }

			public string Position { get; set; }
			public string MainUsed { get; set; }
			public string MainChannel { get; set; }
			public string SecondUsed { get; set; }
			public string SecondChannel { get; set; }
		}

		#endregion

		#region 克隆方法
		private static List<ParameterConfig> CloneParameters(IEnumerable<ParameterConfig> source)
		{
			var list = new List<ParameterConfig>();

			if (source == null)
				return list;

			foreach (var p in source)
			{
				if (p == null) continue;

				list.Add(new ParameterConfig
				{
					Name = p.Name,
					Description = p.Description,
					Value = p.Value
				});
			}

			return list;
		}
		#endregion
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Aron_V2
{
	public partial class Parameters_Config : Form
	{
		public event EventHandler ConfigSaved;
		private AppConfig _config;
		private bool _updatingSelectors;
		private readonly BindingList<ParameterRow> _rows = new BindingList<ParameterRow>();

		public Parameters_Config()
		{
			InitializeComponent();
			InitializeCompactLayout();
			btnAddJob.Visible = btnDeleteJob.Visible = false;
			DesktopTheme.ApplySettings(this);
			comboJob.DropDown += (s, e) => RefreshLimitCatalog();
			comboCam.DropDown += (s, e) => RefreshLimitCatalog();
			_comboPos.DropDown += (s, e) => RefreshLimitCatalog();
		}
		private async void Parameters_Config_Load(object sender, EventArgs e)
		{
			await ReloadConfigurationAsync();
		}

		private async Task ReloadConfigurationAsync()
		{
			if (!btnLoad.Enabled) return;
			var watch = System.Diagnostics.Stopwatch.StartNew();
			btnLoad.Enabled = btnSave.Enabled = comboJob.Enabled = comboCam.Enabled = false;
			btnAddJob.Enabled = btnDeleteJob.Enabled = btnAddrow.Enabled = btnDeleterow.Enabled = false;
			UseWaitCursor = true;
			try
			{
				var path = Global.ParameterCogfig;
				var config = await Task.Run(() => XmlConfigHelper.Load(path));
				if (IsDisposed || Disposing) return;
				_config = config;
				_positionEdits.Clear(); _displayPosition = null;
				FillJobCombo();
				System.Diagnostics.Trace.WriteLine("Limit Setting loaded in " + watch.ElapsedMilliseconds + " ms");
			}
			catch (Exception ex)
			{
				if (!IsDisposed && !Disposing) MessageBox.Show(this, ex.Message, "Load Limit Setting failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			finally
			{
				if (!IsDisposed && !Disposing)
				{
					btnLoad.Enabled = btnSave.Enabled = comboJob.Enabled = comboCam.Enabled = true;
					btnAddJob.Enabled = btnDeleteJob.Enabled = btnAddrow.Enabled = btnDeleterow.Enabled = true;
					UseWaitCursor = false;
				}
			}
		}

		private void InitializeCompactLayout()
		{
			SuspendLayout();
			DoubleBuffered = true;
			typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(dataGridView1, true, null);
			dataGridView1.AutoGenerateColumns = false;
			dataGridView1.Columns.Clear();
			string[] names = { "Model", "Camera", "Position", "Parameter", "Description", "Value" };
			float[] weights = { 65, 65, 65, 160, 160, 95 };
			for (int i = 0; i < names.Length; i++)
				dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { Name = names[i], HeaderText = names[i], DataPropertyName = names[i], AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = weights[i], MinimumWidth = 55, SortMode = DataGridViewColumnSortMode.NotSortable });
			dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
			dataGridView1.RowTemplate.Height = Math.Max(dataGridView1.RowTemplate.Height, Font.Height + 8);
			dataGridView1.AllowUserToResizeRows = false;
			dataGridView1.Columns["Model"].ReadOnly = dataGridView1.Columns["Camera"].ReadOnly = dataGridView1.Columns["Position"].ReadOnly = true;
			dataGridView1.AllowUserToAddRows = false;
			dataGridView1.BackgroundColor = SystemColors.Window;
			dataGridView1.Dock = DockStyle.Fill;
			dataGridView1.DataSource = _rows;
			comboJob.DropDownStyle = comboCam.DropDownStyle = ComboBoxStyle.DropDownList;
			float scale = DeviceDpi / 96F;
			foreach (Button button in new[] { btnLoad, btnSave, btnAddJob, btnDeleteJob, btnAddrow, btnDeleterow })
			{
				button.AutoSize = true;
				button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
				button.MinimumSize = new Size((int)(90 * scale), (int)(30 * scale));
				button.Margin = new Padding(4);
			}
			var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 3 };
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
			var filters = new FlowLayoutPanel { Name = "AlignedSelectorToolbar", Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 0, 0, 6) };
			_comboPos.DropDownStyle = ComboBoxStyle.DropDownList;
			_comboPos.Width = 110;
			_comboPos.SelectedIndexChanged += (s, e) => { if (!_updatingSelectors) RefreshGrid(); };
			_clonePosition.Click += CloneLimitPosition;
			foreach (Control control in new Control[] { label1, comboJob, label2, comboCam, new Label { Text = "Pos:", AutoSize = true }, _comboPos, btnLoad, _clonePosition, btnSave })
			{
				control.Margin = new Padding(4, 8, 4, 4);
				filters.Controls.Add(control);
			}
			var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
			rows.Controls.Add(btnAddrow); rows.Controls.Add(btnDeleterow);
			layout.Controls.Add(filters, 0, 0); layout.Controls.Add(rows, 0, 1); layout.Controls.Add(dataGridView1, 0, 2);
			Controls.Add(layout);
			ResumeLayout(true);
		}

		private class ParameterRow
		{
			public string Model { get; set; }
			public string Camera { get; set; }
			public string Position { get; set; }
			public string Parameter { get; set; }
			public string Description { get; set; }
			public string Value { get; set; }
		}

		private void FormMain_Load(object sender, EventArgs e)
		{
			_config = XmlConfigHelper.Load(Global.ParameterCogfig);
			FillJobCombo();
		}

		private void FillJobCombo(string preferredJob = null)
		{
			preferredJob = preferredJob ?? comboJob.SelectedItem as string;
			_updatingSelectors = true;
			comboJob.BeginUpdate();
			try
			{
				comboJob.Items.Clear();
				if (_config != null && _config.Models != null)
					foreach (var model in _config.Models) comboJob.Items.Add(model.Name);
				if (preferredJob != null && comboJob.Items.Contains(preferredJob)) comboJob.SelectedItem = preferredJob;
				else if (comboJob.Items.Count > 0) comboJob.SelectedIndex = 0;
			}
			finally { comboJob.EndUpdate(); _updatingSelectors = false; }
			FillCamCombo();
			RefreshGrid();
		}

		private void comboJob_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_updatingSelectors) return;
			FillCamCombo();
			RefreshGrid();
		}

		private void FillCamCombo()
		{
			string preferred = comboCam.SelectedItem as string;
			_updatingSelectors = true;
			comboCam.BeginUpdate();
			try
			{
			comboCam.Items.Clear();
			string job = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			if (string.IsNullOrEmpty(job) || _config == null || _config.Models == null) return;

			var model = _config.Models.FirstOrDefault(m => m.Name == job);
			if (model != null && model.Cameras != null)
				for (int i = 0; i < model.Cameras.Count; i++)
					comboCam.Items.Add(model.Cameras[i].Name);

			if (preferred != null && comboCam.Items.Contains(preferred)) comboCam.SelectedItem = preferred;
			else if (comboCam.Items.Count > 0) comboCam.SelectedIndex = 0;
			}
			finally { comboCam.EndUpdate(); _updatingSelectors = false; }
			FillPosCombo();
		}

		private void comboCam_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_updatingSelectors) return;
			FillPosCombo();
			RefreshGrid();
		}

		private List<ParameterRow> BuildParameterList(AppConfig config, string modelName, string cameraName)
		{
			var list = new List<ParameterRow>();
			if (config == null || config.Models == null || string.IsNullOrEmpty(modelName)) return list;

			var model = config.Models.FirstOrDefault(m => m.Name == modelName);
			if (model == null || model.Cameras == null) return list;

			foreach (var cam in model.Cameras)
			{
				if (!string.IsNullOrEmpty(cameraName) &&
				   !string.Equals(cam.Name, cameraName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (cam.Positions == null) continue;
				foreach (var pos in cam.Positions)
				{
					if (pos.Parameters == null) continue;
					foreach (var p in pos.Parameters)
					{
						list.Add(new ParameterRow
						{
							Model = model.Name,
							Camera = cam.Name,
							Position = pos.Name,
							Parameter = p != null ? (p.Name ?? "") : "",
							Description = p != null ? (p.Description ?? "") : "",
							Value = p != null ? (p.Value ?? "") : ""
						});
					}
				}
			}
			return list;
		}

		private void RefreshGrid()
		{
			StoreVisibleParameters();
			string job = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			string cam = comboCam.SelectedItem == null ? null : comboCam.SelectedItem.ToString();
			string pos = _comboPos.SelectedItem as string;

			var rows = BuildParameterList(_config, job, cam).Where(row => row.Position == pos).ToList();
			LimitEdit draft;
			if (_positionEdits.TryGetValue(LimitKey(job, cam, pos), out draft))
				rows = draft.Parameters.Select(p => new ParameterRow { Model = job, Camera = cam, Position = pos, Parameter = p.Name, Description = p.Description, Value = p.Value }).ToList();
			_displayJob = job; _displayCamera = cam; _displayPosition = pos;
			_rows.RaiseListChangedEvents = false;
			dataGridView1.SuspendLayout();
			try
			{
				_rows.Clear();
				foreach (ParameterRow row in rows) _rows.Add(row);
			}
			finally
			{
				_rows.RaiseListChangedEvents = true;
				_rows.ResetBindings();
				dataGridView1.ResumeLayout();
			}
		}

		private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                StoreVisibleParameters();
                var latest = XmlConfigHelper.Load(Global.ParameterCogfig);
                foreach (var edit in _positionEdits.Values)
                {
                    var position = FindLimitPosition(latest, edit.Job, edit.Camera, edit.Position);
                    if (position == null) throw new InvalidOperationException(TLimit("The selected Job/Camera/Position no longer exists. Reload the configuration.", "选择的 Job/Camera/Pos 已被删除，请重新加载配置。"));
                    ValidateLimitRows(edit.Parameters);
                }
                foreach (var edit in _positionEdits.Values)
                    FindLimitPosition(latest, edit.Job, edit.Camera, edit.Position).Parameters = CopyLimitParameters(edit.Parameters);
                XmlConfigHelper.Save(latest, Global.ParameterCogfig);
                _config = latest;
                _positionEdits.Clear();
                ConfigSaved?.Invoke(this, EventArgs.Empty);
                MessageBox.Show(this, TLimit("Save OK.", "保存成功。"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, TLimit("Save failed", "保存失败"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

		private PositionGeneralConfig CreateDefaultPositionGeneral()
		{
			return new PositionGeneralConfig
			{
				Exposure = "10",
				MainUsed = "1",
				MainChannel = "1",
				SecondUsed = "0",
				SecondChannel = "0"
			};
		}

		private void btnDeleteRow_Click(object sender, EventArgs e)
		{
			var row = dataGridView1.CurrentRow?.DataBoundItem as ParameterRow;
			if (row != null) _rows.Remove(row);
		}

		private PositionConfig BuildDefaultPosition(string posName)
		{
			var pos = new PositionConfig();
			pos.Name = posName;
			pos.Parameters = new List<ParameterConfig>();
			pos.Parameters.Add(new ParameterConfig { Name = "X_Max", Description = "X最大值", Value = "200" });
			pos.Parameters.Add(new ParameterConfig { Name = "X_Min", Description = "X最小值", Value = "100" });
			return pos;
		}
		private CameraConfig BuildDefaultCamera(string camName)
		{
			var cam = new CameraConfig();
			cam.Name = camName;
			cam.Positions = new List<PositionConfig>();
			cam.Positions.Add(BuildDefaultPosition("Pos1"));
			return cam;
		}
		private GeneralConfig CreateDefaultGeneral()
		{
			return new GeneralConfig { maxLines_Richbox = "500", CamN = "2" };
		}

		private void btnAddJob_Click(object sender, EventArgs e)
		{
			if (_config == null) _config = new AppConfig { Models = new List<ModelConfig>() };

			string defaultName = "Job" + (_config.Models != null ? _config.Models.Count + 1 : 1);
			string name = Prompt.Show("输入新 Job 名称：", "新增 Job", defaultName).Trim();
			if (string.IsNullOrEmpty(name)) return;
			if (_config.Models.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
			{ MessageBox.Show("已存在同名 Job"); return; }

			var job = new ModelConfig();
			job.Name = name;
			job.General = CreateDefaultGeneral();
			job.Cameras = new List<CameraConfig>();

			int camN = 2;
			int tmp;
			if (job.General != null && int.TryParse(job.General.CamN, out tmp) && tmp > 0) camN = tmp;
			for (int i = 1; i <= camN; i++) job.Cameras.Add(BuildDefaultCamera("Cam" + i));

            if (!EnsureNewCameraVppFiles(job.Cameras.Select(c => new KeyValuePair<string, CameraConfig>(job.Name, c)))) return;
			_config.Models.Add(job);
			XmlConfigHelper.Save(_config, Global.ParameterCogfig);

			// UI
			FillJobCombo(name);
		}

        private bool EnsureNewCameraVppFiles(IEnumerable<KeyValuePair<string, CameraConfig>> cameras)
        {
            try
            {
                foreach (var item in cameras)
                    foreach (string file in BlankCameraVpp.EnsureForCamera(Global.Vpp_Root, item.Key, item.Value))
                        UI_Update.LogChangeEventArgs.Set("Log", "Created blank VPP (not configured for production): " + file, Color.Green);
                return true;
            }
            catch (Exception ex)
            {
                UI_Update.LogChangeEventArgs.Set("Log", "Create missing VPP failed: " + ex, Color.Red);
                MessageBox.Show(this, "无法创建新相机/位置的空白 VPP，配置尚未保存；已有文件不覆盖。\r\n" + ex.GetBaseException().Message,
                    "Create VPP failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void btnDeleteJob_Click(object sender, EventArgs e)
		{
			string job = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			if (string.IsNullOrEmpty(job))
			{
				MessageBox.Show("Please select the Job you want to Delete");
				return;
			}

			var result = MessageBox.Show(
				"Make sure delete Job \"" + job + "\" 吗？\r\n cannot be cancel",
				"Make sure Delete",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (result != DialogResult.Yes) return;

			if (DeleteJob(_config, job))
			{
				XmlConfigHelper.Save(_config, Global.ParameterCogfig);
				// 重新填充下拉并刷新界面
				FillJobCombo();
				MessageBox.Show("Delete Complete Job: " + job);
			}
			else
			{
				MessageBox.Show("删除失败：未找到 Job 或配置为空。");
			}
		}

		private bool DeleteJob(AppConfig cfg, string jobName)
		{
			if (cfg == null || cfg.Models == null || cfg.Models.Count == 0) return false;

			for (int i = 0; i < cfg.Models.Count; i++)
			{
				if (string.Equals(cfg.Models[i].Name, jobName, StringComparison.OrdinalIgnoreCase))
				{
					cfg.Models.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		private void btnAddRow_Click(object sender, EventArgs e)
		{
			if (_config == null)
			{
				MessageBox.Show("请先加载配置。");
				return;
			}

			var jobName = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			var camName = comboCam.SelectedItem == null ? null : comboCam.SelectedItem.ToString();

			if (string.IsNullOrEmpty(jobName))
			{
				MessageBox.Show("请先选择 Job。");
				return;
			}
			if (string.IsNullOrEmpty(camName))
			{
				MessageBox.Show("请先选择 Cam。");
				return;
			}

			// 找到该 Job / Cam 的第一个 Position（若没有，就默认用 "Pos1"；保存时会自动创建）
			string posName = _comboPos.SelectedItem as string;
			if (string.IsNullOrEmpty(posName)) { MessageBox.Show(this, TLimit("Select a Position first.", "请先选择 Pos。")); return; }

			// 生成一个不重名的默认参数名
			string defaultParamName = CreateDefaultParameterNameForCurrentScope(jobName, camName, posName);

			// 也可以弹窗让用户输入（可选）：
			string input = Prompt.Show("参数名：", "新增参数", defaultParamName);
			if (!string.IsNullOrWhiteSpace(input)) defaultParamName = input.Trim();

			// 拿到当前绑定源，没有就创建一个
			var list = dataGridView1.DataSource as BindingList<ParameterRow>;
			if (list == null)
			{
				var rows = BuildParameterList(_config, jobName, camName);
				list = new BindingList<ParameterRow>(rows);
				dataGridView1.AutoGenerateColumns = false;
				dataGridView1.DataSource = list;
			}

			// 新增一行（默认值可按需调整）
			var newRow = new ParameterRow
			{
				Model = jobName,
				Camera = camName,
				Position = posName,
				Parameter = defaultParamName,    // 如 "Param3"
				Description = "新参数",           // 默认描述
				Value = "0"                      // 默认值
			};

			list.Add(newRow);

			// 选中新行，便于用户立即编辑
			int idx = list.Count - 1;
			if (idx >= 0)
			{
				dataGridView1.ClearSelection();
				dataGridView1.Rows[idx].Selected = true;
				dataGridView1.CurrentCell = dataGridView1.Rows[idx].Cells["Value"]; // 光标放到 Value 列
				dataGridView1.BeginEdit(true);
			}
		}

		/// <summary>
		/// 找到指定 Job/Cam 的第一个 Position 名称；若没有返回 null
		/// </summary>
		private string FindFirstPositionName(AppConfig cfg, string jobName, string camName)
		{
			if (cfg == null || cfg.Models == null) return null;
			var model = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (model == null || model.Cameras == null) return null;
			var cam = model.Cameras.FirstOrDefault(c => string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
			if (cam == null || cam.Positions == null || cam.Positions.Count == 0) return null;
			return cam.Positions[0].Name;
		}

		/// <summary>
		/// 在“当前筛选范围（Job/Cam/Pos）”内，生成一个不和现有参数名冲突的默认名字（如 Param1/Param2/…）
		/// </summary>
		private string CreateDefaultParameterNameForCurrentScope(string jobName, string camName, string posName)
		{
			// 先从当前 DataGridView 现有行中找，避免重名
			var exist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var src = dataGridView1.DataSource as BindingList<ParameterRow>;
			if (src != null)
			{
				foreach (var r in src)
				{
					if (r == null) continue;
					if (string.Equals(r.Model, jobName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(r.Camera, camName, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(r.Position, posName, StringComparison.OrdinalIgnoreCase) &&
						!string.IsNullOrEmpty(r.Parameter))
					{
						exist.Add(r.Parameter);
					}
				}
			}

			// 也从 _config 里查一遍（防止表格暂未加载的参数）
			if (_config != null && _config.Models != null)
			{
				var model = _config.Models.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
				if (model != null && model.Cameras != null)
				{
					var cam = model.Cameras.FirstOrDefault(c => string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
					if (cam != null && cam.Positions != null)
					{
						var pos = cam.Positions.FirstOrDefault(p => string.Equals(p.Name, posName, StringComparison.OrdinalIgnoreCase));
						if (pos != null && pos.Parameters != null)
						{
							foreach (var p in pos.Parameters)
							{
								if (p != null && !string.IsNullOrEmpty(p.Name))
									exist.Add(p.Name);
							}
						}
					}
				}
			}

			// 生成 Param1/Param2/… 不重名的一个
			int i = 1;
			while (true)
			{
				string name = "Param" + i;
				if (!exist.Contains(name)) return name;
				i++;
			}
		}

		private async void btnLoad_Click_1(object sender, EventArgs e)
		{
			await ReloadConfigurationAsync();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			if (_config == null)
			{
				MessageBox.Show("配置还没有加载。");
				return;
			}

			var jobName = comboJob.SelectedItem as string;
			var camName = comboCam.SelectedItem as string;

			if (string.IsNullOrEmpty(jobName) || string.IsNullOrEmpty(camName))
			{
				MessageBox.Show("请先选择要删除的 Job 和 Cam。");
				return;
			}

			var confirm = MessageBox.Show(
				$"确定要删除 Job \"{jobName}\" 下的相机 \"{camName}\" 吗？\r\n此操作会删除相机下的所有参数。",
				"确认删除",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (confirm != DialogResult.Yes)
				return;

			bool removed = DeleteCamera(_config, jobName, camName);
			if (!removed)
			{
				MessageBox.Show("未找到要删除的相机。");
				return;
			}

			// 保存回XML
			XmlConfigHelper.Save(_config, Global.ParameterCogfig);

			// 刷新下拉+表格
			FillJobCombo(jobName);

			// 通知主窗体（如果你有这个事件）
			ConfigSaved?.Invoke(this, EventArgs.Empty);

			MessageBox.Show("Camera deleted。");
		}

		private bool DeleteCamera(AppConfig cfg, string jobName, string camName)
		{
			if (cfg == null || cfg.Models == null) return false;

			// 找到这个 Job
			var model = cfg.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (model == null || model.Cameras == null) return false;

			// 找到这个 Cam
			var cam = model.Cameras.FirstOrDefault(c =>
				string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
			if (cam == null) return false;

			// 真正删除这个节点
			model.Cameras.Remove(cam);
			return true;
		}
	}




}

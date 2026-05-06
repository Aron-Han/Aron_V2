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

		public Parameters_Config()
		{
			InitializeComponent();
			comboJob.SelectedIndexChanged += comboJob_SelectedIndexChanged;
			comboCam.SelectedIndexChanged += comboCam_SelectedIndexChanged;
		}
		private void Parameters_Config_Load(object sender, EventArgs e)
		{
			_config = XmlConfigHelper.Load(Global.ParameterCogfig);
			FillJobCombo();
			RefreshGrid();
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

		private void FillJobCombo()
		{
			comboJob.Items.Clear();
			if (_config != null && _config.Models != null)
				for (int i = 0; i < _config.Models.Count; i++)
					comboJob.Items.Add(_config.Models[i].Name);

			if (comboJob.Items.Count > 0) comboJob.SelectedIndex = 0;
		}

		private void comboJob_SelectedIndexChanged(object sender, EventArgs e)
		{
			FillCamCombo();
			RefreshGrid();
		}

		private void FillCamCombo()
		{
			comboCam.Items.Clear();
			string job = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			if (string.IsNullOrEmpty(job) || _config == null || _config.Models == null) return;

			var model = _config.Models.FirstOrDefault(m => m.Name == job);
			if (model != null && model.Cameras != null)
				for (int i = 0; i < model.Cameras.Count; i++)
					comboCam.Items.Add(model.Cameras[i].Name);

			if (comboCam.Items.Count > 0) comboCam.SelectedIndex = 0;
		}

		private void comboCam_SelectedIndexChanged(object sender, EventArgs e)
		{
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
			string job = comboJob.SelectedItem == null ? null : comboJob.SelectedItem.ToString();
			string cam = comboCam.SelectedItem == null ? null : comboCam.SelectedItem.ToString();

			if (string.IsNullOrEmpty(job)) { dataGridView1.DataSource = null; return; }

			var rows = BuildParameterList(_config, job, cam);
			dataGridView1.AutoGenerateColumns = true;
			dataGridView1.DataSource = new BindingList<ParameterRow>(rows);
		}

		private void btnSave_Click(object sender, EventArgs e)
		{
			var src = dataGridView1.DataSource as BindingList<ParameterRow>;
			if (_config == null || src == null) return;

			foreach (var row in src)
			{
				if (row == null) continue;

				// 1) model
				var model = _config.Models != null
					? _config.Models.FirstOrDefault(m =>
						string.Equals(m.Name, row.Model, StringComparison.OrdinalIgnoreCase))
					: null;

				if (model == null)
				{
					if (_config.Models == null)
						_config.Models = new List<ModelConfig>();

					model = new ModelConfig
					{
						Name = row.Model,
						Cameras = new List<CameraConfig>()
					};
					_config.Models.Add(model);
				}

				// 2) camera  ← 这里就不再管 General 了
				var cam = model.Cameras != null
					? model.Cameras.FirstOrDefault(c =>
						string.Equals(c.Name, row.Camera, StringComparison.OrdinalIgnoreCase))
					: null;

				if (cam == null)
				{
					if (model.Cameras == null)
						model.Cameras = new List<CameraConfig>();

					cam = new CameraConfig
					{
						Name = row.Camera,
						// 当前结构下 Camera 不需要 General 了
						Positions = new List<PositionConfig>()
					};
					model.Cameras.Add(cam);
				}

				// 3) position  ← 在这里给 Position 补 General
				var pos = cam.Positions != null
					? cam.Positions.FirstOrDefault(p =>
						string.Equals(p.Name, row.Position, StringComparison.OrdinalIgnoreCase))
					: null;

				if (pos == null)
				{
					if (cam.Positions == null)
						cam.Positions = new List<PositionConfig>();

					pos = new PositionConfig
					{
						Name = row.Position,
						General = CreateDefaultPositionGeneral(),  // ★ 新增：pos 自己的 General
						Parameters = new List<ParameterConfig>()
					};
					cam.Positions.Add(pos);
				}
				else
				{
					// 老的 pos 如果没有 General 也补一个
					if (pos.General == null)
						pos.General = CreateDefaultPositionGeneral();
				}

				// 4) parameter
				var par = pos.Parameters != null
					? pos.Parameters.FirstOrDefault(p =>
						string.Equals(p.Name, row.Parameter, StringComparison.OrdinalIgnoreCase))
					: null;

				if (par == null)
				{
					if (pos.Parameters == null)
						pos.Parameters = new List<ParameterConfig>();

					par = new ParameterConfig
					{
						Name = row.Parameter
					};
					pos.Parameters.Add(par);
				}

				// 5) 写值
				par.Description = row.Description ?? string.Empty;
				par.Value = row.Value ?? string.Empty;
			}

			// 保存到文件
			XmlConfigHelper.Save(_config, Global.ParameterCogfig);

			// 通知主窗体刷新
			var handler = ConfigSaved;
			if (handler != null)
				handler(this, EventArgs.Empty);

			MessageBox.Show("保存成功！");
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
			var row = dataGridView1.CurrentRow != null ?
					  dataGridView1.CurrentRow.DataBoundItem as ParameterRow : null;
			if (_config == null || row == null) return;

			var model = _config.Models != null ? _config.Models.FirstOrDefault(m => m.Name == row.Model) : null;
			if (model == null || model.Cameras == null) return;

			var cam = model.Cameras.FirstOrDefault(c => c.Name == row.Camera);
			if (cam == null || cam.Positions == null) return;

			var pos = cam.Positions.FirstOrDefault(p => p.Name == row.Position);
			if (pos == null || pos.Parameters == null) return;

			var par = pos.Parameters.FirstOrDefault(p => p.Name == row.Parameter);
			if (par != null) pos.Parameters.Remove(par);


			// 从表格也删掉
			var list = dataGridView1.DataSource as BindingList<ParameterRow>;
			if (list != null) list.Remove(row);

			XmlConfigHelper.Save(_config, Global.ParameterCogfig);
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

			_config.Models.Add(job);
			XmlConfigHelper.Save(_config, Global.ParameterCogfig);

			// UI
			FillJobCombo();
			comboJob.SelectedItem = name; // 触发刷新
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
				RefreshGrid();
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
			string posName = FindFirstPositionName(_config, jobName, camName);
			if (string.IsNullOrEmpty(posName)) posName = "Pos1";

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
				dataGridView1.AutoGenerateColumns = true;
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

		private void btnLoad_Click_1(object sender, EventArgs e)
		{
			_config = XmlConfigHelper.Load(Global.ParameterCogfig);
			FillJobCombo();
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
			FillJobCombo();     // 会重新填 job
			comboJob.SelectedItem = jobName;  // 仍然选回原 job
			FillCamCombo();     // 重新填 cam（这个 cam 已经没有了）
			RefreshGrid();

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

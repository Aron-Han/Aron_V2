using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Core.Common.CommandTrees.ExpressionBuilder;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aron_V2.Out_put_Parameters;
using static Aron_V2.XmlConfigHelper;

namespace Aron_V2
{
	public partial class Out_put_Parameters : Form
	{
		private const int PLC_BUFFER_SIZE = 240;
		private bool _gridHasErrors = false;
		private VppOutputConfig _cfg;
		private string _cfgPath = Global.VppOutputCfgPath; // 你的路径
		private enum ValidateMode { Global, PerChannel }
		private readonly ValidateMode _validateMode = ValidateMode.Global; // 默认“全通道共享240”
		private BindingList<OutputRow> _rows = new BindingList<OutputRow>();
        public event EventHandler<VppOutputSavedEventArgs> VppOutputSaved;

        public class OutputRow
		{
			public string Job { get; set; }          // 只读展示
			public int Channel { get; set; }         // 过滤条件（0..3）
			public string Cam { get; set; }          // Cam1/Cam2/Cam3...
			public string Name { get; set; }         // 逻辑字段名（日志/调试）
			public string Type { get; set; }         // string/float/int/short/bool/double
			public string Source { get; set; }       // ToolBlock 输出端口名
			public bool Required { get; set; }       // true/false
			public int Start { get; set; }           // 在240B中的起始字节
			public int Length { get; set; }          // 占用字节
		}


		public Out_put_Parameters()
		{
			InitializeComponent();

			_cfgPath = string.IsNullOrEmpty(Global.VppOutputCfgPath)
				? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VppOutput.xml")
				: Global.VppOutputCfgPath;

			// 事件绑定
			CboJob.SelectedIndexChanged += (s, e) => RefreshGrid();

			btnAdd.Click += btnAdd_Click;
			btnDelete.Click += btnDelete_Click;
			btnSave.Click += btnSave_Click;

			// DataGridView 基本设置
			dataGridView1.AutoGenerateColumns = false;
			dataGridView1.AllowUserToAddRows = false;
			dataGridView1.MultiSelect = true;
			dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

			// 实时校验相关事件（用于地址重叠/越界染色）
			dataGridView1.CurrentCellDirtyStateChanged += (s, e) =>
			{
				if (dataGridView1.IsCurrentCellDirty)
					dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
			};
			dataGridView1.CellValueChanged += (s, e) =>
			{
				if (e.RowIndex >= 0) RevalidateGrid();   // 你前面提供的校验函数
			};
			dataGridView1.DataBindingComplete += (s, e) => RevalidateGrid();
			dataGridView1.RowsAdded += (s, e) => RevalidateGrid();
			dataGridView1.RowsRemoved += (s, e) => RevalidateGrid();

			BuildGridColumns();        // 记得在这里的列定义里保留 “Channel” 列（0/1/2/3 下拉）
			dataGridView1.DataSource = _rows;  // _rows: BindingList<OutputRow>
		}

		

		// === 构造 DataGridView 列 ===
		private void BuildGridColumns()
		{
			dataGridView1.Columns.Clear();

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Job",
				HeaderText = "Job",
				ReadOnly = true,
				Width = 100
			});

			var colChannel = new DataGridViewComboBoxColumn
			{
				DataPropertyName = "Channel",
				HeaderText = "Channel",
				Width = 70
			};
			colChannel.Items.AddRange(0, 1, 2, 3);
			dataGridView1.Columns.Add(colChannel);

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Cam",
				HeaderText = "Cam",
				Width = 80
			});

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Name",
				HeaderText = "Field Name",
				Width = 150
			});

			var colType = new DataGridViewComboBoxColumn
			{
				DataPropertyName = "Type",
				HeaderText = "Type",
				Width = 90
			};
			colType.Items.AddRange("string", "float", "double", "int", "short", "bool");
			dataGridView1.Columns.Add(colType);

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Source",
				HeaderText = "VPP Output Port",
				AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
			});

			dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn
			{
				DataPropertyName = "Required",
				HeaderText = "Required",
				Width = 70
			});

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Start",
				HeaderText = "Start",
				Width = 70
			});

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Length",
				HeaderText = "Length",
				Width = 70
			});
		}

		// === 将 cfg -> rows（按 Job+Channel 过滤） ===
		private void RefreshGrid()
		{
			var jobName = CboJob.SelectedItem as string;
			_rows.Clear();
			if (string.IsNullOrEmpty(jobName) || _cfg == null || _cfg.Jobs == null) return;

			var job = _cfg.Jobs.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null) return;

			foreach (var cam in job.Cameras ?? new List<CamOutput>())
			{
				foreach (var it in cam.VPPOutput ?? new List<OutputItem>())
				{
					_rows.Add(new OutputRow
					{
						Job = job.Name,
						Channel = it.Channel,   // 显示所有通道
						Cam = cam.Name,
						Name = it.Name,
						Type = it.Type,
						Source = it.Source,
						Required = string.Equals(it.Required, "true", StringComparison.OrdinalIgnoreCase),
						Start = it.Start,
						Length = it.Length
					});
				}
			}
			RevalidateGrid();
		}

		// === Add Row ===
		private void btnAdd_Click(object sender, EventArgs e)
		{
			var job = CboJob.SelectedItem as string;
			if (string.IsNullOrEmpty(job)) return;

			_rows.Add(new OutputRow
			{
				Job = job,
				Channel = 0,         // 默认 0
				Cam = "Cam1",
				Name = "Field",
				Type = "short",
				Source = "Result",
				Required = true,
				Start = 0,
				Length = 2
			});
			RevalidateGrid();
		}

		// === Delete Row(s) ===
		private void btnDelete_Click(object sender, EventArgs e)
		{
			foreach (DataGridViewRow sel in dataGridView1.SelectedRows)
			{
				if (sel.Index >= 0 && sel.Index < _rows.Count)
					_rows.RemoveAt(sel.Index);
			}
			RevalidateGrid();
		}

		// === Save ===
		private void btnSave_Click(object sender, EventArgs e)
		{
			if (_cfg == null) _cfg = new VppOutputConfig { Jobs = new List<JobOutput>() };

			var jobName = CboJob.SelectedItem as string;
			if (string.IsNullOrWhiteSpace(jobName)) return;

			// 1) 行清洗：去掉无效行
			var rows = _rows
				.Where(r => r != null
						 && !string.IsNullOrWhiteSpace(r.Cam)
						 && !string.IsNullOrWhiteSpace(r.Name)
						 && !string.IsNullOrWhiteSpace(r.Type)
						 && !string.IsNullOrWhiteSpace(r.Source)
						 && r.Length > 0)
				// 方便落盘：按 Cam -> Channel -> Start 排序
				.OrderBy(r => r.Cam, StringComparer.OrdinalIgnoreCase)
				.ThenBy(r => r.Channel)
				.ThenBy(r => r.Start)
				.ToList();

			// 2) 逐行规则校验（类型、范围、长度等）
			var errs = ValidateRows(rows);

			// 3) 栅格重算：重叠/越界（会标红单元格），并把结果写入 _gridHasErrors
			RevalidateGrid();

			// 4) 业务校验：同一 Job 的同一 Channel 不允许多台主相机
			if (!ValidateOneMainCamPerChannelForJob(rows, jobName, out var mainErr))
				errs.Add(mainErr);

			if (_gridHasErrors || errs.Count > 0)
			{
				MessageBox.Show("保存失败：\r\n" + string.Join("\r\n", errs),
					"校验未通过", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			// 5) 找/建 Job
			var job = _cfg.Jobs.FirstOrDefault(j =>
				string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null)
			{
				job = new JobOutput { Name = jobName, Cameras = new List<CamOutput>() };
				_cfg.Jobs.Add(job);
			}
			if (job.Cameras == null) job.Cameras = new List<CamOutput>();

			// 6) 重建该 Job 的 Cameras/VPPOutput（把当前表里此 Job 的所有 Channel 一次性落盘）
			job.Cameras.Clear();
			foreach (var g in rows.GroupBy(r => r.Cam, StringComparer.OrdinalIgnoreCase))
			{
				var cam = new CamOutput { Name = g.Key, VPPOutput = new List<OutputItem>() };

				foreach (var r in g)
				{
					cam.VPPOutput.Add(new OutputItem
					{
						Name = r.Name ?? "",
						Type = r.Type ?? "string",
						Source = r.Source ?? "",
						Required = r.Required ? "true" : "false",
						Channel = r.Channel,
						Start = r.Start,
						Length = r.Length
					});
				}

				job.Cameras.Add(cam);
			}

			// 7) 保存
			try
			{
				XmlConfigHelper.SaveVppOutput(_cfg, _cfgPath);

                MessageBox.Show("Save Successed！", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OnVppOutputSaved();
            }
			catch (Exception ex)
			{
				MessageBox.Show("Save Failed：" + ex.Message, "Error:", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// === 校验：起始/长度、区间重叠、字段/端口名 ===
		private List<string> ValidateRows(IList<OutputRow> rows)
		{
			var errs = new List<string>();
			const int MaxBytes = 240;

			// 基础项校验
			for (int i = 0; i < rows.Count; i++)
			{
				var r = rows[i];
				if (string.IsNullOrWhiteSpace(r.Cam)) errs.Add($"第 {i + 1} 行：Cam 不能为空");
				if (string.IsNullOrWhiteSpace(r.Name)) errs.Add($"第 {i + 1} 行：Name 不能为空");
				if (string.IsNullOrWhiteSpace(r.Source)) errs.Add($"第 {i + 1} 行：Source 不能为空");
				if (r.Start < 0 || r.Start >= MaxBytes) errs.Add($"第 {i + 1} 行：Start 超界(0~{MaxBytes - 1})");
				if (r.Length <= 0 || r.Start + r.Length > MaxBytes) errs.Add($"第 {i + 1} 行：Length 非法或越界");
			}

			// 区间重叠校验（同 Channel 下）
			var byChan = rows.GroupBy(r => r.Channel);
			foreach (var g in byChan)
			{
				var list = g.OrderBy(r => r.Start).ToList();
				for (int i = 1; i < list.Count; i++)
				{
					int prevEnd = list[i - 1].Start + list[i - 1].Length - 1;
					if (list[i].Start <= prevEnd)
					{
						errs.Add($"Channel {g.Key} 上区间重叠：[{list[i - 1].Start},{prevEnd}] 与 [{list[i].Start},{list[i].Start + list[i].Length - 1}]");
					}
				}
			}

			return errs;
		}

		private bool ValidateOneMainCamPerChannelForJob(IEnumerable<OutputRow> rows, string jobName, out string error)
		{
			error = null;
			if (rows == null) return true;

			// 只看当前 Job 的行，并且忽略缺字段的空行
			var q = rows.Where(r => r != null
								 && string.Equals(r.Job, jobName, StringComparison.OrdinalIgnoreCase)
								 && !string.IsNullOrWhiteSpace(r.Cam));

			var conflict = q
				.GroupBy(r => r.Channel) // 同一 Job 下按 Channel 分组
				.Select(g => new {
					Channel = g.Key,
					Cams = g.Select(x => x.Cam).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
				})
				.FirstOrDefault(x => x.Cams.Count > 1);

			if (conflict != null)
			{
				error = $"Job={jobName} 的 Channel={conflict.Channel} 被配置成由多台相机输出：{string.Join(", ", conflict.Cams)}。\r\n" +
						$"同一通道应只由一台主相机负责发送，请修正。";
				return false;
			}
			return true;
		}

		private void Out_put_Parameters_Load(object sender, EventArgs e)
		{
			_cfg = XmlConfigHelper.LoadVppOutput(_cfgPath);

			// 绑定事件（用于实时校验上色）
			dataGridView1.AutoGenerateColumns = false;
			dataGridView1.DataSource = _rows;
			dataGridView1.CellValueChanged += dgv_CellValueChanged;
			dataGridView1.CurrentCellDirtyStateChanged += dgv_CurrentCellDirtyStateChanged;
			dataGridView1.DataBindingComplete += (s, ev) => RevalidateGrid();
			dataGridView1.RowsAdded += (s, ev) => RevalidateGrid();
			dataGridView1.RowsRemoved += (s, ev) => RevalidateGrid();

			// 初始化列（示例：Channel列用下拉0~3）
			InitGridColumns();

			// 填充 Job 下拉
			CboJob.Items.Clear();
			var jobNames = (_cfg.Jobs ?? new List<JobOutput>()).Select(j => j.Name).Distinct().ToList();
			foreach (var name in jobNames) CboJob.Items.Add(name);
			if (CboJob.Items.Count > 0) CboJob.SelectedIndex = 0;

			// 初次刷新
			RefreshGrid();
		}

		private void InitGridColumns()
		{
			dataGridView1.Columns.Clear();

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Job", HeaderText = "Job", ReadOnly = true });

			var colCh = new DataGridViewComboBoxColumn { DataPropertyName = "Channel", HeaderText = "Channel" };
			colCh.Items.AddRange(0, 1, 2, 3);
			dataGridView1.Columns.Add(colCh);

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Cam", HeaderText = "Cam" });
			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Field Name" });

			var colType = new DataGridViewComboBoxColumn { DataPropertyName = "Type", HeaderText = "Type" };
			colType.Items.AddRange("string", "float", "double", "int", "short", "bool");
			dataGridView1.Columns.Add(colType);

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Source", HeaderText = "VPP Output Port" });
			dataGridView1.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = "Required", HeaderText = "Required" });
			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Start", HeaderText = "Start" });
			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Length", HeaderText = "Length" });
		}

		#region 实时校验
		private void dgv_CurrentCellDirtyStateChanged(object sender, EventArgs e)
		{
			if (dataGridView1.IsCurrentCellDirty)
				dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
		}
		private void dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex >= 0) RevalidateGrid();
		}

		private void RevalidateGrid()
		{
			_gridHasErrors = false;

			// 复位颜色
			foreach (DataGridViewRow r in dataGridView1.Rows)
			{
				ResetCellStyle(r, "Start");
				ResetCellStyle(r, "Length");
			}
			var list = _rows.ToList();

			IEnumerable<IEnumerable<_Seg>> buckets;
			if (_validateMode == ValidateMode.PerChannel)
			{
				buckets = list.GroupBy(r => r.Channel).Select(g => BuildSegs(g.ToList()));
			}
			else
			{
				// 全局共享 240：所有行同桶
				buckets = new[] { BuildSegs(list) };
			}

			foreach (var segs in buckets)
			{
				var arr = segs.OrderBy(s => s.Start).ToList();
				for (int i = 0; i < arr.Count; i++)
				{
					var s = arr[i];
					// 越界
					if (s.End >= PLC_BUFFER_SIZE || s.Length <= 0)
					{
						MarkRowError(s.RowIndex, "Start", "越界/非法");
						MarkRowError(s.RowIndex, "Length", "越界/非法");
					}
					// 与前一个重叠
					if (i > 0 && arr[i].Start <= arr[i - 1].End)
					{
						MarkRowError(arr[i - 1].RowIndex, "Start", "地址重叠");
						MarkRowError(arr[i - 1].RowIndex, "Length", "地址重叠");
						MarkRowError(arr[i].RowIndex, "Start", "地址重叠");
						MarkRowError(arr[i].RowIndex, "Length", "地址重叠");
					}
				}
			}
		}

		private class _Seg
		{
			public int RowIndex; public int Start; public int End; public int Length;
		}
		private IEnumerable<_Seg> BuildSegs(List<OutputRow> rows)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				var r = rows[i];
				int start = r.Start;
				int len = r.Length;
				yield return new _Seg { RowIndex = i, Start = start, Length = len, End = start + len - 1 };
			}
		}
		private void ResetCellStyle(DataGridViewRow row, string propName)
		{
			var col = FindColumnByProp(propName); if (col == null) return;
			var cell = row.Cells[col.Index];
			cell.Style.BackColor = SystemColors.Window;
			cell.ToolTipText = ""; cell.ErrorText = "";
		}
		private void MarkRowError(int rowIndex, string propName, string tip)
		{
			_gridHasErrors = true;
			var col = FindColumnByProp(propName); if (col == null) return;
			var cell = dataGridView1.Rows[rowIndex].Cells[col.Index];
			cell.Style.BackColor = Color.MistyRose;
			cell.ToolTipText = tip; cell.ErrorText = tip;
		}
		private DataGridViewColumn FindColumnByProp(string propName)
		{
			foreach (DataGridViewColumn c in dataGridView1.Columns)
				if (string.Equals(c.DataPropertyName, propName, StringComparison.OrdinalIgnoreCase)) return c;
			return null;
		}

        public class VppOutputSavedEventArgs : EventArgs
        {
            public string Path { get; }
            public VppOutputConfig Config { get; }

            public VppOutputSavedEventArgs(string path, VppOutputConfig cfg)
            {
                Path = path;
                Config = cfg;
            }
        }

        private void OnVppOutputSaved()
        {
            var h = VppOutputSaved;
            if (h != null)
                h(this, new VppOutputSavedEventArgs(_cfgPath, _cfg));
        }

		#endregion

		private void btnSave_Click_1(object sender, EventArgs e)
		{

		}
	}


}

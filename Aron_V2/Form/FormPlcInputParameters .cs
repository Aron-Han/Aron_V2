using Aron_V2.Profinet;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
	public partial class FormPlcInputParameters : Form
	{
		private const int PLC_BUFFER_SIZE = 240;

		private BindingList<PlcInputChannel> _rows = new BindingList<PlcInputChannel>();
		private PlcInputConfig _cfg;

		public FormPlcInputParameters()
		{
			InitializeComponent();

			this.Load += FormPlcInputParameters_Load;

			dataGridView1.AutoGenerateColumns = false;
			dataGridView1.AllowUserToAddRows = false;
			dataGridView1.AllowUserToDeleteRows = false;
			dataGridView1.MultiSelect = false;
			dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

			BuildGridColumns();

			dataGridView1.CellValueChanged += delegate { RevalidateGrid(); };
			dataGridView1.DataBindingComplete += delegate { RevalidateGrid(); };

			btnSave.Click += btnSave_Click;
		}

		private void FormPlcInputParameters_Load(object sender, EventArgs e)
		{
			_cfg = PlcInputConfigHelper.Load(PlcInputConfigHelper.DefaultPath);

			_rows.Clear();

			foreach (var ch in _cfg.Channels.OrderBy(x => x.Channel))
			{
				_rows.Add(ch);
			}

			dataGridView1.DataSource = null;
			dataGridView1.DataSource = _rows;

			RevalidateGrid();
		}

		private void BuildGridColumns()
		{
			dataGridView1.Columns.Clear();

			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = "Channel",
				HeaderText = "Channel",
				ReadOnly = true,
				Width = 70
			});

			AddIntColumn("ClearStart", "Clear Start");
			AddIntColumn("ClearLength", "Clear Len");

			AddIntColumn("JobStart", "Job Start");
			AddIntColumn("JobLength", "Job Len");

			AddIntColumn("PosStart", "Pos Start");
			AddIntColumn("PosLength", "Pos Len");

			AddIntColumn("PartStart", "Part Start");
			AddIntColumn("PartLength", "Part Len");
		}

		private void AddIntColumn(string prop, string header)
		{
			dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
			{
				DataPropertyName = prop,
				HeaderText = header,
				Width = 90
			});
		}

		private void btnSave_Click(object sender, EventArgs e)
		{
			var errs = ValidateRows();

			if (errs.Length > 0)
			{
				MessageBox.Show(
					"Save Failed：\r\n" + string.Join("\r\n", errs),
					"Input Config Error",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
				return;
			}

			var cfg = new PlcInputConfig();
			foreach (var r in _rows.OrderBy(x => x.Channel))
			{
				cfg.Channels.Add(r);
			}

			try
			{
				PlcInputConfigHelper.Save(cfg, PlcInputConfigHelper.DefaultPath);

				// 重新加载到运行缓存
				input_Parameters.ReloadInputConfig();

				MessageBox.Show("Save Successed！", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Save Failed：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private string[] ValidateRows()
		{
			var errs = new System.Collections.Generic.List<string>();

			if (_rows.Count != 4)
			{
				errs.Add("The input configuration must be fixed at 4 channels");
			}

			foreach (var r in _rows)
			{
				if (r.Channel < 0 || r.Channel > 3)
					errs.Add("Channel only can be 0~3");

				CheckRange(errs, r.Channel, "Clear", r.ClearStart, r.ClearLength);
				CheckRange(errs, r.Channel, "Job", r.JobStart, r.JobLength);
				CheckRange(errs, r.Channel, "Pos", r.PosStart, r.PosLength);
				CheckRange(errs, r.Channel, "PartCode", r.PartStart, r.PartLength);
			}

			return errs.ToArray();
		}

		private void CheckRange(System.Collections.Generic.List<string> errs, int channel, string name, int start, int len)
		{
			if (start < 0 || start >= PLC_BUFFER_SIZE)
			{
				errs.Add($"Channel{channel} {name} Start out of range：{start}");
				return;
			}

			if (len <= 0 || start + len > PLC_BUFFER_SIZE)
			{
				errs.Add($"Channel{channel} {name} Length illegal：Start={start}, Length={len}");
			}
		}

		private void RevalidateGrid()
		{
			foreach (DataGridViewRow row in dataGridView1.Rows)
			{
				row.DefaultCellStyle.BackColor = Color.White;
			}

			foreach (DataGridViewRow row in dataGridView1.Rows)
			{
				if (row.DataBoundItem == null) continue;

				var r = row.DataBoundItem as PlcInputChannel;
				if (r == null) continue;

				bool hasError =
					!IsRangeOk(r.ClearStart, r.ClearLength) ||
					!IsRangeOk(r.JobStart, r.JobLength) ||
					!IsRangeOk(r.PosStart, r.PosLength) ||
					!IsRangeOk(r.PartStart, r.PartLength);

				if (hasError)
					row.DefaultCellStyle.BackColor = Color.MistyRose;
			}
		}

		private bool IsRangeOk(int start, int len)
		{
			return start >= 0 && start < PLC_BUFFER_SIZE && len > 0 && start + len <= PLC_BUFFER_SIZE;
		}
	}
}
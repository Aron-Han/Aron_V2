using Aron_V2.Profinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
	internal sealed class BufferedDataGridView : DataGridView
	{
		public BufferedDataGridView()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			UpdateStyles();
		}
	}

	public abstract class ProfinetTableSettingFormBase : Form
	{
		protected readonly DataGridView Grid = new BufferedDataGridView();
		private readonly TextBox _log = new TextBox();
		protected readonly FlowLayoutPanel Buttons = new FlowLayoutPanel();
		private readonly HashSet<int> _overlapRows = new HashSet<int>();
		private TextBox _editingTextBox;
		private bool _updatingGrid;

		protected ProfinetTableSettingFormBase(string title)
		{
			SuspendLayout();
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			Text = title; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(1050, 650); ClientSize = new Size(1250, 720);
			Grid.Dock = DockStyle.Fill; Grid.AllowUserToAddRows = false; Grid.AllowUserToDeleteRows = false; Grid.RowHeadersVisible = false;
			Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			Grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
			Grid.CellValueChanged += Grid_CellValueChanged;
			Grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
			Grid.EditingControlShowing += Grid_EditingControlShowing;
			Grid.CellPainting += Grid_CellPainting;
			Grid.RowsRemoved += delegate { RecalculateOverlapBorders(); };
			Buttons.Dock = DockStyle.Fill; Buttons.AutoSize = true; Buttons.Padding = new Padding(8, 6, 8, 4);
			AddButton("Load", delegate { LoadRows(); }); AddButton("Save", delegate { SaveRows(); });
			_log.Dock = DockStyle.Fill; _log.Multiline = true; _log.ReadOnly = true; _log.ScrollBars = ScrollBars.Vertical;
			TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 74)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
			layout.Controls.Add(Grid, 0, 0); layout.Controls.Add(Buttons, 0, 1); layout.Controls.Add(_log, 0, 2); Controls.Add(layout);
			Load += delegate { LoadRows(); };
			DesktopTheme.ApplySettings(this);
			ResumeLayout(true);
		}

		protected Button AddButton(string text, EventHandler click) { Button b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(120, 32), Padding = new Padding(8, 3, 8, 3) }; b.Click += click; Buttons.Controls.Add(b); return b; }
		protected void AddCommonColumns(bool output)
		{
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Channel", HeaderText = "Channel", FillWeight = 45 });
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Parameter", HeaderText = "Parameter", FillWeight = 120 });
			if (output) Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "VppOutput", HeaderText = "VPP Output", FillWeight = 130 });
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", HeaderText = output ? "Result Address" : "UserData Address", FillWeight = 90 });
			Grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "DataType", HeaderText = "Data Type", DataSource = ProfinetCommConfigHelper.SupportedDataTypes, FillWeight = 70 });
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Length", HeaderText = "Length", FillWeight = 60 });
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "Note", FillWeight = 115 });
			Grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentValue", HeaderText = "Current Value", ReadOnly = true, FillWeight = 100 });
		}

		protected bool ReadAddressAndLength(DataGridViewRow row, out int address, out int length)
		{
			address = 0; length = 0;
			string label = "Channel " + row.Cells["Channel"].Value + " " + row.Cells["Parameter"].Value;
			if (!int.TryParse(Convert.ToString(row.Cells["Address"].Value), out address) || address < 0 || !int.TryParse(Convert.ToString(row.Cells["Length"].Value), out length) || length <= 0 || address + length > ProfinetCommConfigHelper.ProcessDataLength)
			{
				MessageBox.Show(label + " address/length must be within 0..239.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false;
			}
			return true;
		}

		protected void BeginGridUpdate()
		{
			_updatingGrid = true;
			Grid.SuspendLayout();
		}

		protected void EndGridUpdate()
		{
			ConfigureAllLengthCells();
			_updatingGrid = false;
			Grid.ResumeLayout();
			RecalculateOverlapBorders();
		}

		protected void RefreshMappingValidation()
		{
			ConfigureAllLengthCells();
			RecalculateOverlapBorders();
		}

		private void Grid_CurrentCellDirtyStateChanged(object sender, EventArgs e)
		{
			if (Grid.IsCurrentCellDirty && Grid.CurrentCell != null && Grid.CurrentCell.OwningColumn.Name == "DataType")
				Grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
		}

		private void Grid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
		{
			if (_updatingGrid || e.RowIndex < 0 || e.ColumnIndex < 0) return;
			string column = Grid.Columns[e.ColumnIndex].Name;
			if (column == "DataType") ConfigureLengthCell(Grid.Rows[e.RowIndex]);
			if (column == "Address" || column == "Length" || column == "DataType") RecalculateOverlapBorders();
		}

		private void Grid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
		{
			if (_editingTextBox != null) _editingTextBox.TextChanged -= EditingTextBox_TextChanged;
			_editingTextBox = e.Control as TextBox;
			if (_editingTextBox != null && Grid.CurrentCell != null && (Grid.CurrentCell.OwningColumn.Name == "Address" || Grid.CurrentCell.OwningColumn.Name == "Length"))
				_editingTextBox.TextChanged += EditingTextBox_TextChanged;
		}

		private void EditingTextBox_TextChanged(object sender, EventArgs e)
		{
			RecalculateOverlapBorders();
		}

		private void ConfigureAllLengthCells()
		{
			foreach (DataGridViewRow row in Grid.Rows) ConfigureLengthCell(row);
		}

		private void ConfigureLengthCell(DataGridViewRow row)
		{
			if (row == null || row.IsNewRow || !Grid.Columns.Contains("DataType") || !Grid.Columns.Contains("Length")) return;
			string type = Convert.ToString(row.Cells["DataType"].Value);
			int fixedLength = ProfinetCommConfigHelper.GetFixedLength(type);
			DataGridViewCell lengthCell = row.Cells["Length"];
			if (fixedLength > 0)
			{
				if (Convert.ToString(lengthCell.Value) != fixedLength.ToString()) lengthCell.Value = fixedLength;
				lengthCell.ReadOnly = true;
				lengthCell.Style.BackColor = Color.Gainsboro;
				lengthCell.ToolTipText = type + " length is fixed at " + fixedLength + " byte(s).";
			}
			else
			{
				lengthCell.ReadOnly = false;
				lengthCell.Style.BackColor = Color.White;
				lengthCell.ToolTipText = "String length is configurable.";
			}
		}

		private string GetDisplayedCellText(DataGridViewRow row, string columnName)
		{
			if (Grid.CurrentCell != null && Grid.CurrentCell.RowIndex == row.Index && Grid.CurrentCell.OwningColumn.Name == columnName && _editingTextBox != null && _editingTextBox.Focused)
				return _editingTextBox.Text;
			return Convert.ToString(row.Cells[columnName].Value);
		}

		private void RecalculateOverlapBorders()
		{
			if (_updatingGrid || !Grid.Columns.Contains("Address") || !Grid.Columns.Contains("Length")) return;
			HashSet<int> previous = new HashSet<int>(_overlapRows);
			_overlapRows.Clear();
			var ranges = new List<Tuple<int, int, int>>();
			foreach (DataGridViewRow row in Grid.Rows)
			{
				int address, length;
				if (!int.TryParse(GetDisplayedCellText(row, "Address"), out address) || !int.TryParse(GetDisplayedCellText(row, "Length"), out length) || address < 0 || length <= 0 || address + length > ProfinetCommConfigHelper.ProcessDataLength)
				{
					_overlapRows.Add(row.Index);
					continue;
				}
				ranges.Add(Tuple.Create(row.Index, address, length));
			}
			for (int i = 0; i < ranges.Count; i++)
				for (int j = i + 1; j < ranges.Count; j++)
					if (ranges[i].Item2 < ranges[j].Item2 + ranges[j].Item3 && ranges[j].Item2 < ranges[i].Item2 + ranges[i].Item3)
					{ _overlapRows.Add(ranges[i].Item1); _overlapRows.Add(ranges[j].Item1); }

			previous.UnionWith(_overlapRows);
			foreach (int rowIndex in previous)
				if (rowIndex >= 0 && rowIndex < Grid.Rows.Count)
				{
					DataGridViewCell cell = Grid.Rows[rowIndex].Cells["Address"];
					cell.Style.BackColor = _overlapRows.Contains(rowIndex) ? Color.MistyRose : Color.Empty;
					Grid.InvalidateCell(cell);
				}
			if (_editingTextBox != null && Grid.CurrentCell != null && Grid.CurrentCell.OwningColumn.Name == "Address")
				_editingTextBox.BackColor = _overlapRows.Contains(Grid.CurrentCell.RowIndex) ? Color.MistyRose : Color.White;
		}

		private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex < 0 || e.ColumnIndex != Grid.Columns["Address"].Index || !_overlapRows.Contains(e.RowIndex)) return;
			e.Paint(e.ClipBounds, e.PaintParts);
			Rectangle border = e.CellBounds; border.Width -= 2; border.Height -= 2;
			using (Pen pen = new Pen(Color.Red, 2)) e.Graphics.DrawRectangle(pen, border);
			e.Handled = true;
		}

		protected bool ValidateNoOverlap(IEnumerable<Tuple<int, int, int, string>> ranges)
		{
			// UserData and Result are shared NDM byte areas. Channels may be
			// interleaved, but no two configured fields may occupy the same byte.
			List<Tuple<int, int, int, string>> ordered = ranges.OrderBy(x => x.Item2).ToList();
			for (int i = 1; i < ordered.Count; i++) if (ordered[i].Item2 < ordered[i - 1].Item2 + ordered[i - 1].Item3)
			{ MessageBox.Show("Address overlap: CH" + ordered[i - 1].Item1 + " " + ordered[i - 1].Item4 + " / CH" + ordered[i].Item1 + " " + ordered[i].Item4, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
			return true;
		}

		protected void Log(string text) { _log.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine); }
		protected abstract void LoadRows();
		protected abstract void SaveRows();
	}

	public sealed class ProfinetInputSettingForm : ProfinetTableSettingFormBase
	{
		public ProfinetInputSettingForm() : base("Profinet Input Setting") { AddCommonColumns(false); Grid.Columns["Channel"].ReadOnly = true; Grid.Columns["Parameter"].ReadOnly = true; }
		protected override void LoadRows()
		{
			BeginGridUpdate(); Grid.Rows.Clear(); foreach (ProfinetInputPoint x in ProfinetCommConfigHelper.Load().InputPoints.OrderBy(x => x.Address).ThenBy(x => x.Channel))
				Grid.Rows.Add(x.Channel, x.Parameter, x.Address, x.DataType, x.Length, x.Note, string.Empty);
			EndGridUpdate();
			Log("Profinet input mapping loaded. Addresses are offsets inside UserData.");
		}
		protected override void SaveRows()
		{
			Grid.EndEdit(); var points = new List<ProfinetInputPoint>(); var ranges = new List<Tuple<int, int, int, string>>();
			foreach (DataGridViewRow row in Grid.Rows)
			{
				int address, length; if (!ReadAddressAndLength(row, out address, out length)) return;
				int ch = Convert.ToInt32(row.Cells["Channel"].Value); string name = Convert.ToString(row.Cells["Parameter"].Value); string type = Convert.ToString(row.Cells["DataType"].Value);
				points.Add(new ProfinetInputPoint { Channel = ch, Parameter = name, Address = address, DataType = type, Length = length, Note = Convert.ToString(row.Cells["Note"].Value).Trim() }); ranges.Add(Tuple.Create(ch, address, length, name));
			}
			if (!ValidateNoOverlap(ranges)) return; ProfinetCommConfig config = ProfinetCommConfigHelper.Load(); config.InputPoints = points; ProfinetCommConfigHelper.Save(config); Log("Input mapping saved: " + ProfinetCommConfigHelper.DefaultPath); MessageBox.Show("Save succeeded.", Text);
		}
	}

	public sealed class ProfinetOutputSettingForm : ProfinetTableSettingFormBase
	{
		public ProfinetOutputSettingForm() : base("Profinet Output Setting")
		{
			AddCommonColumns(true); AddButton("Add Output", AddOutput); AddButton("Delete Selected", DeleteSelected);
			Grid.CellBeginEdit += delegate(object s, DataGridViewCellCancelEventArgs e) { ProfinetOutputPoint x = Grid.Rows[e.RowIndex].Tag as ProfinetOutputPoint; if (x != null && x.IsFixed && (Grid.Columns[e.ColumnIndex].Name == "Channel" || Grid.Columns[e.ColumnIndex].Name == "Parameter" || Grid.Columns[e.ColumnIndex].Name == "VppOutput" || Grid.Columns[e.ColumnIndex].Name == "DataType")) e.Cancel = true; };
		}
		protected override void LoadRows()
		{
			BeginGridUpdate(); Grid.Rows.Clear(); foreach (ProfinetOutputPoint x in ProfinetCommConfigHelper.Load().OutputPoints.OrderBy(x => x.Address).ThenBy(x => x.Channel))
			{ int i = Grid.Rows.Add(x.Channel, x.Parameter, x.VppOutput, x.Address, x.DataType, x.Length, x.Note, string.Empty); Grid.Rows[i].Tag = x; Grid.Rows[i].DefaultCellStyle.BackColor = x.IsFixed ? Color.WhiteSmoke : Color.LightYellow; }
			EndGridUpdate();
			Log("Profinet output mapping loaded. JobID/PosID are required; custom rows map a VPP output into Result.");
		}
		private void AddOutput(object sender, EventArgs e)
		{
			int ch = 0; if (Grid.CurrentRow != null) int.TryParse(Convert.ToString(Grid.CurrentRow.Cells["Channel"].Value), out ch);
			var x = new ProfinetOutputPoint { Channel = ch, Parameter = "New Output", VppOutput = "", Address = 8, DataType = "Float", Length = 4 };
			int i = Grid.Rows.Add(ch, x.Parameter, x.VppOutput, x.Address, x.DataType, x.Length, x.Note, ""); Grid.Rows[i].Tag = x; Grid.Rows[i].DefaultCellStyle.BackColor = Color.LightYellow;
			RefreshMappingValidation();
		}
		private void DeleteSelected(object sender, EventArgs e)
		{
			if (Grid.CurrentRow == null) return; ProfinetOutputPoint x = Grid.CurrentRow.Tag as ProfinetOutputPoint;
			if (x != null && x.IsFixed) { MessageBox.Show("JobID and PosID are required and cannot be deleted.", Text); return; } Grid.Rows.Remove(Grid.CurrentRow); RefreshMappingValidation();
		}
		protected override void SaveRows()
		{
			Grid.EndEdit(); var points = new List<ProfinetOutputPoint>(); var ranges = new List<Tuple<int, int, int, string>>();
			foreach (DataGridViewRow row in Grid.Rows)
			{
				int address, length; if (!ReadAddressAndLength(row, out address, out length)) return; int ch; if (!int.TryParse(Convert.ToString(row.Cells["Channel"].Value), out ch) || ch < 0 || ch >= 4) { MessageBox.Show("Channel must be 0..3.", Text); return; }
				ProfinetOutputPoint old = row.Tag as ProfinetOutputPoint; bool fixedRow = old != null && old.IsFixed; string name = Convert.ToString(row.Cells["Parameter"].Value).Trim(); string source = Convert.ToString(row.Cells["VppOutput"].Value).Trim();
				if (string.IsNullOrEmpty(name) || (!fixedRow && string.IsNullOrEmpty(source))) { MessageBox.Show("Custom output requires both Parameter and VPP Output.", Text); return; }
				points.Add(new ProfinetOutputPoint { Channel = ch, Parameter = name, VppOutput = source, Address = address, DataType = Convert.ToString(row.Cells["DataType"].Value), Length = length, IsFixed = fixedRow, Note = Convert.ToString(row.Cells["Note"].Value).Trim() }); ranges.Add(Tuple.Create(ch, address, length, name));
			}
			if (!ValidateNoOverlap(ranges)) return; ProfinetCommConfig config = ProfinetCommConfigHelper.Load(); config.OutputPoints = points; ProfinetCommConfigHelper.Save(config); LoadRows(); Log("Output mapping saved: " + ProfinetCommConfigHelper.DefaultPath); MessageBox.Show("Save succeeded.", Text);
		}
	}
}

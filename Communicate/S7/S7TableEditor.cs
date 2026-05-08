using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Aron_V2
{
	/// <summary>Shared, event-driven validation for the S7 input and output editors.</summary>
	internal sealed class S7TableEditor
	{
		private readonly DataGridView _grid;
		private readonly Func<int> _channelCount;
		private readonly Dictionary<DataGridViewCell, string> _errors = new Dictionary<DataGridViewCell, string>();
		private TextBox _editor;
		private bool _busy;
		private bool _loading;
		private static readonly string[] Formats = Enum.GetNames(typeof(S7PointDataType));

		/// <summary>Sorts valid S7 addresses by DB number, byte and bit. Invalid or blank
		/// addresses deliberately go to the end so they remain visible for correction.</summary>
		internal static long AddressSortKey(string address)
		{
			Match match = Regex.Match(address ?? string.Empty, @"^DB(\d+)\.(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase);
			int db, offset, bit = 0;
			if (!match.Success || !int.TryParse(match.Groups[1].Value, out db) || !int.TryParse(match.Groups[2].Value, out offset) ||
				(match.Groups[3].Success && !int.TryParse(match.Groups[3].Value, out bit))) return long.MaxValue;
			return (long)db * 1000000000L + (long)offset * 8L + bit;
		}

		internal static void ArrangeWindow(Form form, DataGridView grid, TextBox log, params Button[] buttons)
		{
			form.SuspendLayout();
			var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 74));
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
			var commands = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 8) };
			foreach (Button button in buttons)
			{
				button.AutoSize = true;
				button.Margin = new Padding(6);
				commands.Controls.Add(button);
			}
			grid.Dock = DockStyle.Fill;
			log.Dock = DockStyle.Fill;
			layout.Controls.Add(grid, 0, 0);
			layout.Controls.Add(commands, 0, 1);
			layout.Controls.Add(log, 0, 2);
			form.Controls.Add(layout);
			form.ResumeLayout(true);
		}

		public S7TableEditor(DataGridView grid, Func<int> channelCount)
		{
			_grid = grid; _channelCount = channelCount;
			grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
			typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, true, null);
			DataGridViewColumn old = grid.Columns["DataType"];
			int index = old.Index;
			var combo = new DataGridViewComboBoxColumn { Name = old.Name, HeaderText = old.HeaderText, FillWeight = old.FillWeight, SortMode = DataGridViewColumnSortMode.NotSortable, DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
			combo.Items.AddRange(Formats.Cast<object>().ToArray());
			grid.Columns.Remove(old); grid.Columns.Insert(index, combo);
			foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
			grid.CellValueChanged += delegate(object s, DataGridViewCellEventArgs e)
			{
				if (_loading || _busy || e.RowIndex < 0) return;
				string name = grid.Columns[e.ColumnIndex].Name;
				if (name == "DataType") UpdateLength(grid.Rows[e.RowIndex]);
				if (name != "CurrentValue") ValidateCells();
			};
			grid.CurrentCellDirtyStateChanged += delegate
			{
				if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
			};
			grid.EditingControlShowing += delegate(object s, DataGridViewEditingControlShowingEventArgs e)
			{
				if (_editor != null) _editor.TextChanged -= EditorChanged;
				_editor = e.Control as TextBox;
				if (_editor != null) _editor.TextChanged += EditorChanged;
			};
			grid.CellEndEdit += delegate { ValidateCells(false); };
			grid.CellPainting += PaintError;
		}

		public static string NormalizeFormat(string value)
		{
			string type = (value ?? "").Trim();
			if (type.Equals("Bit", StringComparison.OrdinalIgnoreCase)) type = "Bool";
			if (type.Equals("Short", StringComparison.OrdinalIgnoreCase)) type = "Int16";
			if (type.Equals("Int", StringComparison.OrdinalIgnoreCase)) type = "Int32";
			if (type.Equals("Real", StringComparison.OrdinalIgnoreCase)) type = "Float";
			return Formats.FirstOrDefault(x => x.Equals(type, StringComparison.OrdinalIgnoreCase)) ?? "Bool";
		}

		public void BeginLoad() { _loading = true; _grid.SuspendLayout(); }
		public void EndLoad()
		{
			foreach (DataGridViewRow row in _grid.Rows)
			{
				UpdateLength(row);
				string parameter = Convert.ToString(row.Cells["ParameterName"].Value);
				bool fixedSignal = _grid.Columns.Contains("VppOutput") ? S7OutputData.IsFixed(parameter) : IsFixedSignal(parameter);
				row.Cells["DataType"].ReadOnly = fixedSignal;
				if (fixedSignal)
				{
					row.Cells["DataType"].Style.BackColor = Color.Gainsboro;
					row.Cells["DataType"].ToolTipText = "Fixed S7 signal: the format cannot be changed.";
				}
			}
			_loading = false; _grid.ResumeLayout(); ValidateCells(false);
		}

		public bool CanSave()
		{
			_grid.EndEdit(); ValidateCells(false);
			if (_errors.Count == 0) return true;
			MessageBox.Show(_errors.First().Value + "\r\nCorrect the red cells before saving or testing PLC addresses.", "S7 mapping", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return false;
		}

		private void EditorChanged(object sender, EventArgs e)
		{
			if (_grid.CurrentCell != null && _grid.CurrentCell.OwningColumn.Name != "CurrentValue") ValidateCells();
		}

		private void UpdateLength(DataGridViewRow row)
		{
			_busy = true;
			try
			{
				string type = NormalizeFormat(Convert.ToString(row.Cells["DataType"].Value));
				DataGridViewCell cell = row.Cells["Length"];
				int length = Profinet.ProfinetCommConfigHelper.GetFixedLength(type);
				cell.ReadOnly = type != "String";
				if (type == "Bool") cell.Value = ""; // Bool addresses occupy one bit, not one byte.
				else if (length > 0) cell.Value = length.ToString();
				else if (string.IsNullOrWhiteSpace(Convert.ToString(cell.Value))) cell.Value = "1";
				cell.Style.BackColor = cell.ReadOnly ? Color.Gainsboro : Color.White;
				cell.ToolTipText = type == "Bool" ? "Bool occupies one bit." : (type == "String" ? "String length in bytes." : type + " occupies " + length + " bytes.");
			}
			finally { _busy = false; }
		}

		private string Value(DataGridViewRow row, string name, bool live)
		{
			if (live && _grid.IsCurrentCellInEditMode && _grid.CurrentCell == row.Cells[name] && _editor != null && _editor.Focused) return _editor.Text.Trim();
			return Convert.ToString(row.Cells[name].Value).Trim();
		}

		private sealed class Range
		{
			public DataGridViewRow Row;
			public int Db;
			public long Start;
			public long End;
		}

		private void ValidateCells(bool live = true)
		{
			if (_busy || _loading) return;
			var next = new Dictionary<DataGridViewCell, string>();
			var ranges = new List<Range>();
			int count = _channelCount();
			foreach (DataGridViewRow row in _grid.Rows)
			{
				int channel;
				if (!int.TryParse(Value(row, "ChannelNo", live), out channel) || channel < -1 || channel >= count || (channel == -1 && !IsGlobal(Value(row, "ParameterName", false))))
					next[row.Cells["ChannelNo"]] = "Channel must be 0.." + (count - 1) + "; -1 is reserved for system signals.";
				string type = NormalizeFormat(Value(row, "DataType", live));
				Match match = Regex.Match(Value(row, "DbAddress", live), @"^DB(\d+)\.(\d+)(?:\.(\d+))?$", RegexOptions.IgnoreCase);
				int db, offset, bit = 0;
				if (!match.Success || !int.TryParse(match.Groups[1].Value, out db) || !int.TryParse(match.Groups[2].Value, out offset) || (match.Groups[3].Success && (!int.TryParse(match.Groups[3].Value, out bit) || bit > 7)) || (type != "Bool" && match.Groups[3].Success))
				{ next[row.Cells["DbAddress"]] = "Use DB<number>.<byte> for data or DB<number>.<byte>.<bit 0..7> for Bool."; continue; }
				int bytes = Profinet.ProfinetCommConfigHelper.GetFixedLength(type);
				if (type == "String" && (!int.TryParse(Value(row, "Length", live), out bytes) || bytes <= 0 || bytes > ushort.MaxValue))
				{ next[row.Cells["Length"]] = "String length must be 1..65535 bytes."; continue; }
				long start = (long)offset * 8 + (type == "Bool" ? bit : 0);
				ranges.Add(new Range { Row = row, Db = db, Start = start, End = start + (type == "Bool" ? 1 : (long)bytes * 8) });
			}
			for (int i = 0; i < ranges.Count; i++)
				for (int j = i + 1; j < ranges.Count; j++)
					if (ranges[i].Db == ranges[j].Db && ranges[i].Start < ranges[j].End && ranges[j].Start < ranges[i].End)
					{
						next[ranges[i].Row.Cells["DbAddress"]] = "Address overlaps CH" + Value(ranges[j].Row, "ChannelNo", live) + " " + Value(ranges[j].Row, "ParameterName", false);
						next[ranges[j].Row.Cells["DbAddress"]] = "Address overlaps CH" + Value(ranges[i].Row, "ChannelNo", live) + " " + Value(ranges[i].Row, "ParameterName", false);
					}
			var changed = new HashSet<DataGridViewCell>(_errors.Keys.Concat(next.Keys));
			foreach (DataGridViewCell cell in changed)
			{
				string before, after; _errors.TryGetValue(cell, out before); next.TryGetValue(cell, out after);
				if (before == after) continue;
				cell.ToolTipText = after ?? "";
				cell.Style.BackColor = after != null ? Color.MistyRose : Color.Empty;
				if (cell.DataGridView == _grid) _grid.InvalidateCell(cell);
			}
			_errors.Clear(); foreach (var pair in next) _errors.Add(pair.Key, pair.Value);
			if (_editor != null && _grid.CurrentCell != null && _grid.IsCurrentCellInEditMode)
				_editor.BackColor = _errors.ContainsKey(_grid.CurrentCell) ? Color.MistyRose : Color.White;
		}

		private static bool IsGlobal(string parameter)
		{
			return new[] { "Trigger Enable", "Buffer Results Enable", "System Ready", "System Busy", "Online" }.Contains(parameter, StringComparer.OrdinalIgnoreCase);
		}

		private static bool IsFixedSignal(string parameter)
		{
			if (S7OutputData.IsNdmExtension(parameter)) return true;
			return IsGlobal(parameter) || new[] { "Trigger Camera", "Inspection Result Acknowledge", "Clear Exposure Complete", "Set User Data", "Clear Result", "JobID", "PosID", "PartN", "Trigger Ready Camera", "Trigger Ack Camera", "Exposure Complete", "Result Valid", "SetUserData Ack", "Result X", "Result Y", "Result A", "Result" }.Contains(parameter, StringComparer.OrdinalIgnoreCase)
				|| Regex.IsMatch(parameter ?? "", @"^Result\d+$", RegexOptions.IgnoreCase);
		}

		private void PaintError(object sender, DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex < 0 || e.ColumnIndex < 0 || !_errors.ContainsKey(_grid.Rows[e.RowIndex].Cells[e.ColumnIndex])) return;
			e.Paint(e.ClipBounds, e.PaintParts);
			Rectangle rect = e.CellBounds; rect.Inflate(-1, -1);
			using (Pen pen = new Pen(Color.Red, 2)) e.Graphics.DrawRectangle(pen, rect);
			e.Handled = true;
		}
	}
}

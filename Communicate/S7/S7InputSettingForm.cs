using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using HslCommunication;

namespace Aron_V2
{
	[Serializable]
	public class S7InputSignalTableRow
	{
		public int Channel { get; set; }
		public string Parameter { get; set; }
		public string DbAddress { get; set; }
		public string Length { get; set; }
		public string Format { get; set; }
		public string CurrentValue { get; set; }
		public string Note { get; set; }

		public S7InputSignalTableRow()
		{
			Parameter = string.Empty;
			DbAddress = string.Empty;
			Length = string.Empty;
			Format = string.Empty;
			CurrentValue = string.Empty;
			Note = string.Empty;
		}

		public S7InputSignalTableRow(int channel, string parameter, string dbAddress, string length, string format, string currentValue)
		{
			Channel = channel;
			Parameter = parameter;
			DbAddress = dbAddress;
			Length = length;
			Format = format;
			CurrentValue = currentValue;
			Note = string.Empty;
		}
	}

	[Serializable]
	public class S7InputSignalTableConfig
	{
		public List<S7InputSignalTableRow> Rows { get; set; }

		public S7InputSignalTableConfig()
		{
			Rows = new List<S7InputSignalTableRow>();
		}
	}

	public partial class S7InputSettingForm : Form
	{
		private S7CommConfig _config;
		private S7TableEditor _tableEditor;
		private int? _selectedChannelCount;

		public S7InputSettingForm(int channelCount) : this()
		{
			_selectedChannelCount = Math.Max(1, Math.Min(CommunicationConfigHelper.MaxScalableChannelCount, channelCount));
		}

		private static readonly string InputTableConfigPath =
			ProjectPaths.Config("Communication/S7/S7InputTableConfig.xml");

		private const string COL_CHANNEL = "ChannelNo";
		private const string COL_PARAMETER = "ParameterName";
		private const string COL_ADDRESS = "DbAddress";
		private const string COL_LENGTH = "Length";
		private const string COL_FORMAT = "DataType";
		private const string COL_VALUE = "CurrentValue";
		private const string COL_NOTE = "Note";
		private const int GLOBAL_CHANNEL = -1;

		public S7InputSettingForm()
		{
			InitializeComponent();
			S7TableEditor.ArrangeWindow(this, dgvInput, txtLog, btnLoad, btnSave, btnReadSelected, btnReadChannel, btnResetDefault);
			DesktopTheme.ApplySettings(this);
		}

		private void S7InputSettingForm_Load(object sender, EventArgs e)
		{
			InitGridStyle();
			LoadConfigToGrid();
		}

		private void InitGridStyle()
		{
			dgvInput.AllowUserToAddRows = false;
			dgvInput.AllowUserToDeleteRows = false;
			dgvInput.RowHeadersVisible = false;
			dgvInput.MultiSelect = false;
			dgvInput.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			dgvInput.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			_tableEditor = new S7TableEditor(dgvInput, () => GetActiveChannelCount(S7ConfigXmlHelper.Load()));
			if (!dgvInput.Columns.Contains(COL_NOTE))
				dgvInput.Columns.Add(new DataGridViewTextBoxColumn { Name = COL_NOTE, HeaderText = "Note", FillWeight = 125 });

			if (dgvInput.Columns.Contains(COL_CHANNEL))
				dgvInput.Columns[COL_CHANNEL].ReadOnly = true;

			if (dgvInput.Columns.Contains(COL_PARAMETER))
				dgvInput.Columns[COL_PARAMETER].ReadOnly = true;

			try
			{
				btnReadSelected.Visible = false;
				btnReadSelected.Enabled = false;
				btnResetDefault.Visible = false;
				btnResetDefault.Enabled = false;
			}
			catch
			{
			}

		}

		private void btnLoad_Click(object sender, EventArgs e)
		{
			LoadConfigToGrid();
		}

		private void btnSave_Click(object sender, EventArgs e)
		{
			if (!SaveGridToXml()) return;
			AppendLog("S7 input table saved: " + InputTableConfigPath);
		}

		private void btnResetDefault_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Reset S7 input table by current S7 setting base address and active channel count?",
				"S7 Input", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				return;

			LoadRowsToGrid(CreateDefaultRows());
			if (!SaveGridToXml()) return;

			AppendLog("S7 input table reset by current S7 setting.");
		}

		private void btnReadSelected_Click(object sender, EventArgs e)
		{
			try
			{
				if (!SaveGridToXml()) return;

				if (dgvInput.CurrentRow == null)
				{
					MessageBox.Show("Please select one row first.");
					return;
				}

				using (S7AddressTester tester = CreateConnectedTester())
				{
					if (tester == null)
						return;

					ReadRow(tester, dgvInput.CurrentRow);
				}
			}
			catch (Exception ex)
			{
				AppendLog("Read selected exception: " + ex.Message);
				MessageBox.Show(ex.Message, "S7 Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnReadChannel_Click(object sender, EventArgs e)
		{
			try
			{
				if (!SaveGridToXml()) return;

				if (dgvInput.CurrentRow == null)
				{
					MessageBox.Show("Please select one channel row first.");
					return;
				}

				int channel = ToInt(GetCell(dgvInput.CurrentRow, COL_CHANNEL), -1);
				if (channel != GLOBAL_CHANNEL && (channel < 0 || channel >= GetActiveChannelCount(_config)))
					return;

				using (S7AddressTester tester = CreateConnectedTester())
				{
					if (tester == null)
						return;

					foreach (DataGridViewRow row in dgvInput.Rows)
					{
						if (ToInt(GetCell(row, COL_CHANNEL), -1) == channel)
							ReadRow(tester, row);
					}
				}
			}
			catch (Exception ex)
			{
				AppendLog("Read channel exception: " + ex.Message);
				MessageBox.Show(ex.Message, "S7 Input", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private S7AddressTester CreateConnectedTester()
		{
			_config = S7ConfigXmlHelper.Load();

			S7AddressTester tester = new S7AddressTester(_config.Basic);
			OperateResult connect = tester.Connect();

			if (!connect.IsSuccess)
			{
				AppendLog("Connect failed: " + connect.Message);
				MessageBox.Show(connect.Message, "S7", MessageBoxButtons.OK, MessageBoxIcon.Error);
				tester.Dispose();
				return null;
			}

			return tester;
		}

		private void ReadRow(S7AddressTester tester, DataGridViewRow row)
		{
			string parameter = GetCell(row, COL_PARAMETER);

			string address = GetCell(row, COL_ADDRESS);
			if (string.IsNullOrWhiteSpace(address))
			{
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " address is empty, skipped.");
				return;
			}

			S7PointDataType type = ParseDataType(GetCell(row, COL_FORMAT));
			ushort length = (ushort)ToInt(GetCell(row, COL_LENGTH), 1);

			OperateResult<string> result = tester.ReadAsString(address, type, length);
			if (result.IsSuccess)
			{
				SetCell(row, COL_VALUE, result.Content);
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " [" + address + "] = " + result.Content);
			}
			else
			{
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " [" + address + "] read failed: " + result.Message);
			}
		}

		private void LoadConfigToGrid()
		{
			_config = S7ConfigXmlHelper.Load();

			List<S7InputSignalTableRow> rows = LoadRowsFromXml();
			if (rows == null || rows.Count == 0)
			{
				rows = CreateDefaultRows();
				SaveRowsToXml(rows);
			}

			LoadRowsToGrid(rows);
			AppendLog("S7 input table loaded.");
		}

		private void LoadRowsToGrid(List<S7InputSignalTableRow> rows)
		{
			// Add newly enabled channels without replacing existing project addresses.
			foreach (S7InputSignalTableRow required in CreateDefaultRows())
			{
				S7InputSignalTableRow existing = rows.Find(x => x.Channel == required.Channel && string.Equals(x.Parameter, required.Parameter, StringComparison.OrdinalIgnoreCase));
				if (existing == null)
				{
					required.DbAddress = required.Parameter == "Buffer Results Enable" ? S7OutputData.NdmAddress(rows.Find(x => x.Channel == GLOBAL_CHANNEL && x.Parameter == "Trigger Enable")?.DbAddress, 1) : string.Empty;
					rows.Add(required);
				}
				else existing.Format = required.Format;
			}
			_tableEditor.BeginLoad();
			dgvInput.Rows.Clear();

			int activeCount = GetActiveChannelCount(_config);
			foreach (S7InputSignalTableRow row in rows.OrderBy(r => S7TableEditor.AddressSortKey(r.DbAddress)).ThenBy(r => r.Channel).ThenBy(r => r.Parameter))
			{
				// Keep disabled mappings on disk, but exclude them from editing and validation.
				if (row.Channel >= activeCount) continue;
				dgvInput.Rows.Add(
					row.Channel,
					row.Parameter,
					row.DbAddress,
					row.Length,
					S7TableEditor.NormalizeFormat(row.Format),
					row.CurrentValue,
					row.Note
				);
			}
			_tableEditor.EndLoad();
		}

		private List<S7InputSignalTableRow> CreateDefaultRows()
		{
			List<S7InputSignalTableRow> rows = new List<S7InputSignalTableRow>();

			S7CommConfig cfg = S7ConfigXmlHelper.Load();

			string inputDb = "DB100";
			int inputBase = 0;

			if (cfg != null && cfg.Basic != null)
			{
				inputDb = GetDbName(cfg.Basic.InputStartAddress, "DB100");
				inputBase = GetBaseByte(cfg.Basic.InputStartAddress);
			}

			// Global input. Channel = -1 means this row is not tied to CH0/CH1.
			// PLC Trigger Enable = true 时，各通道才允许输出 Trigger Ready Camera。
			rows.Add(new S7InputSignalTableRow(
				GLOBAL_CHANNEL,
				"Trigger Enable",
				MakeBoolAddress(inputDb, inputBase + 0, 0),
				"",
				"Bool",
				""));

			rows.Add(new S7InputSignalTableRow(GLOBAL_CHANNEL, "Buffer Results Enable", MakeBoolAddress(inputDb, inputBase, 1), "", "Bool", ""));
			for (int ch = 0; ch < GetActiveChannelCount(cfg); ch++)
			{
				int count = Math.Max(4, GetActiveChannelCount(cfg));
				int bitByte = inputBase + 16 + ch;
				int clearResultByte = inputBase + 16 + count + ch;
				int jobIdByte = inputBase + 16 + count * 2 + ch;
				int posIdByte = inputBase + 16 + count * 3 + ch;
				int partNoByte = inputBase + 16 + count * 4 + ch * 40;

				rows.Add(new S7InputSignalTableRow(ch, "Trigger Camera", MakeBoolAddress(inputDb, bitByte, 0), "", "Bool", ""));
				rows.Add(new S7InputSignalTableRow(ch, "Inspection Result Acknowledge", MakeBoolAddress(inputDb, bitByte, 1), "", "Bool", ""));
				rows.Add(new S7InputSignalTableRow(ch, "Clear Exposure Complete", MakeBoolAddress(inputDb, bitByte, 2), "", "Bool", ""));
				rows.Add(new S7InputSignalTableRow(ch, "Set User Data", MakeBoolAddress(inputDb, bitByte, 3), "", "Bool", ""));

				rows.Add(new S7InputSignalTableRow(ch, "Clear Result", MakeByteAddress(inputDb, clearResultByte), "1", "Byte", ""));
				rows.Add(new S7InputSignalTableRow(ch, "JobID", MakeByteAddress(inputDb, jobIdByte), "1", "Byte", ""));
				rows.Add(new S7InputSignalTableRow(ch, "PosID", MakeByteAddress(inputDb, posIdByte), "1", "Byte", ""));
				rows.Add(new S7InputSignalTableRow(ch, "PartN", MakeByteAddress(inputDb, partNoByte), "40", "String", ""));
			}

			return rows;
		}

		private bool SaveGridToXml()
		{
			dgvInput.EndEdit();
			if (!_tableEditor.CanSave()) return false;

			List<S7InputSignalTableRow> rows = new List<S7InputSignalTableRow>();

			foreach (DataGridViewRow gridRow in dgvInput.Rows)
			{
				if (gridRow.IsNewRow)
					continue;

				S7InputSignalTableRow row = new S7InputSignalTableRow();
				row.Channel = ToInt(GetCell(gridRow, COL_CHANNEL), 0);
				row.Parameter = GetCell(gridRow, COL_PARAMETER);
				row.DbAddress = GetCell(gridRow, COL_ADDRESS);
				row.Length = GetCell(gridRow, COL_LENGTH);
				row.Format = NormalizeFormat(GetCell(gridRow, COL_FORMAT));
				row.CurrentValue = GetCell(gridRow, COL_VALUE);
				row.Note = GetCell(gridRow, COL_NOTE);

				rows.Add(row);
			}

			// Saving the visible channels must not erase mappings for temporarily disabled channels.
			List<S7InputSignalTableRow> savedRows = LoadRowsFromXml();
			int activeCount = GetActiveChannelCount(_config);
			if (savedRows != null) rows.AddRange(savedRows.FindAll(row => row.Channel >= activeCount));
			SaveRowsToXml(rows.OrderBy(row => S7TableEditor.AddressSortKey(row.DbAddress)).ThenBy(row => row.Channel).ThenBy(row => row.Parameter).ToList());
			return true;
		}

		private List<S7InputSignalTableRow> LoadRowsFromXml()
		{
			try
			{
				if (!File.Exists(InputTableConfigPath))
					return null;

				XmlSerializer serializer = new XmlSerializer(typeof(S7InputSignalTableConfig));

				using (FileStream fs = new FileStream(InputTableConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					S7InputSignalTableConfig cfg = serializer.Deserialize(fs) as S7InputSignalTableConfig;
					if (cfg == null)
						return null;

					return cfg.Rows;
				}
			}
			catch
			{
				return null;
			}
		}

		private void SaveRowsToXml(List<S7InputSignalTableRow> rows)
		{
			string dir = Path.GetDirectoryName(InputTableConfigPath);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			S7InputSignalTableConfig cfg = new S7InputSignalTableConfig();
			cfg.Rows = rows ?? new List<S7InputSignalTableRow>();

			XmlSerializer serializer = new XmlSerializer(typeof(S7InputSignalTableConfig));

			using (FileStream fs = new FileStream(InputTableConfigPath, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				serializer.Serialize(fs, cfg);
			}
		}

		private S7PointDataType ParseDataType(string typeText)
		{
			string text = NormalizeFormat(typeText);

			try
			{
				return (S7PointDataType)Enum.Parse(typeof(S7PointDataType), text, true);
			}
			catch
			{
				return S7PointDataType.Bool;
			}
		}

		private string NormalizeFormat(string format)
		{
			if (string.IsNullOrWhiteSpace(format))
				return "Bool";

			string f = format.Trim();

			if (string.Equals(f, "Bit", StringComparison.OrdinalIgnoreCase))
				return "Bool";

			if (string.Equals(f, "Short", StringComparison.OrdinalIgnoreCase))
				return "Int16";

			if (string.Equals(f, "Real", StringComparison.OrdinalIgnoreCase))
				return "Float";

			return f;
		}

		private string GetCell(DataGridViewRow row, string columnName)
		{
			if (row == null || !row.DataGridView.Columns.Contains(columnName))
				return string.Empty;

			object value = row.Cells[columnName].Value;
			return value == null ? string.Empty : value.ToString().Trim();
		}

		private void SetCell(DataGridViewRow row, string columnName, string value)
		{
			if (row == null || !row.DataGridView.Columns.Contains(columnName))
				return;

			row.Cells[columnName].Value = value;
		}

		private int ToInt(string value, int defaultValue)
		{
			try
			{
				int result;
				if (int.TryParse(value, out result))
					return result;

				return defaultValue;
			}
			catch
			{
				return defaultValue;
			}
		}

		private void AppendLog(string msg)
		{
			txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
		}



		private int GetActiveChannelCount(S7CommConfig cfg)
		{
			if (_selectedChannelCount.HasValue) return _selectedChannelCount.Value;
			int count = 2;

			if (cfg != null && cfg.Basic != null)
				count = cfg.Basic.ActiveChannelCount;

			if (count <= 0)
				count = 2;

			if (count > CommunicationConfigHelper.MaxScalableChannelCount)
				count = CommunicationConfigHelper.MaxScalableChannelCount;

			return count;
		}

		private static string GetDbName(string address, string defaultDbName)
		{
			if (string.IsNullOrWhiteSpace(address))
				return defaultDbName;

			string text = address.Trim();

			int dot = text.IndexOf('.');
			if (dot > 0)
				return text.Substring(0, dot);

			return text;
		}

		private static int GetBaseByte(string address)
		{
			if (string.IsNullOrWhiteSpace(address))
				return 0;

			string text = address.Trim();

			int dot = text.IndexOf('.');
			if (dot < 0 || dot >= text.Length - 1)
				return 0;

			string rest = text.Substring(dot + 1);

			int dot2 = rest.IndexOf('.');
			if (dot2 >= 0)
				rest = rest.Substring(0, dot2);

			int value;
			if (int.TryParse(rest, out value))
				return value;

			return 0;
		}

		private static string MakeByteAddress(string dbName, int byteOffset)
		{
			return dbName + "." + byteOffset.ToString();
		}

		private static string MakeBoolAddress(string dbName, int byteOffset, int bitOffset)
		{
			return dbName + "." + byteOffset.ToString() + "." + bitOffset.ToString();
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using HslCommunication;
using System.Linq;

namespace Aron_V2
{
	[Serializable]
	public class S7OutputSignalTableRow
	{
		public int Channel { get; set; }
		public string Parameter { get; set; }
		public string DbAddress { get; set; }
		public string Length { get; set; }
		public string Format { get; set; }
		public string CurrentValue { get; set; }
        public string VppOutput { get; set; }
		public string Note { get; set; }

		public S7OutputSignalTableRow()
		{
			Parameter = string.Empty;
			DbAddress = string.Empty;
			Length = string.Empty;
			Format = string.Empty;
			CurrentValue = string.Empty;
			Note = string.Empty;
		}

		public S7OutputSignalTableRow(int channel, string parameter, string dbAddress, string length, string format, string currentValue)
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
	public class S7OutputSignalTableConfig
	{
		public List<S7OutputSignalTableRow> Rows { get; set; }

		public S7OutputSignalTableConfig()
		{
			Rows = new List<S7OutputSignalTableRow>();
		}
	}

	public partial class S7OutputSettingForm : Form
	{
		private S7CommConfig _config;
		private S7TableEditor _tableEditor;
		private int? _selectedChannelCount;

		public S7OutputSettingForm(int channelCount) : this()
		{
			_selectedChannelCount = Math.Max(1, Math.Min(CommunicationConfigHelper.MaxScalableChannelCount, channelCount));
		}

		private static readonly string OutputTableConfigPath =
			ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml");

		private const string COL_CHANNEL = "ChannelNo";
		private const string COL_PARAMETER = "ParameterName";
		private const string COL_ADDRESS = "DbAddress";
		private const string COL_LENGTH = "Length";
		private const string COL_FORMAT = "DataType";
		private const string COL_VALUE = "CurrentValue";
		private const string COL_NOTE = "Note";
		private const int GLOBAL_CHANNEL = -1;

		public S7OutputSettingForm()
		{
			InitializeComponent();
			DesktopTheme.ApplySettings(this);
			S7TableEditor.ArrangeWindow(this, dgvOutput, txtLog, btnLoad, btnSave, btnReadSelected, btnWriteSelected, btnWriteChannel, btnResetDefault, NewCommand("Add Output", AddOutput), NewCommand("Delete Output", DeleteOutput));
		}

		private void S7OutputSettingForm_Load(object sender, EventArgs e)
		{
			InitGridStyle();
			LoadConfigToGrid();
		}


        private static string T(string en, string zh) => UiLanguage.Current == "zh-CN" ? zh : en;
        private Button NewCommand(string text, EventHandler action)
        {
            var button = new Button { Text = text, AutoSize = true };
            button.Click += action; return button;
        }
        private void AddOutput(object sender, EventArgs e)
        {
            int channel = dgvOutput.CurrentRow == null ? 0 : Math.Max(0, ToInt(GetCell(dgvOutput.CurrentRow, COL_CHANNEL), 0));
            string name; int n = 1;
            do { name = "Output" + n++; } while (dgvOutput.Rows.Cast<DataGridViewRow>().Any(r => GetCell(r, COL_PARAMETER).Equals(name, StringComparison.OrdinalIgnoreCase) && ToInt(GetCell(r, COL_CHANNEL), 0) == channel));
            _tableEditor.BeginLoad();
            int index = dgvOutput.Rows.Cast<DataGridViewRow>().Where(r => ToInt(GetCell(r, COL_CHANNEL), -1) == channel).Select(r => r.Index + 1).DefaultIfEmpty(dgvOutput.Rows.Count).Max();
            dgvOutput.Rows.Insert(index, channel, name, "", "4", "Float", "", "", "");
            _tableEditor.EndLoad(); dgvOutput.CurrentCell = dgvOutput.Rows[index].Cells[COL_PARAMETER];
        }
        private void DeleteOutput(object sender, EventArgs e)
        {
            var row = dgvOutput.CurrentRow; if (row == null) return;
            if (row.Tag is bool && (bool)row.Tag)
            { MessageBox.Show(T("Fixed handshake rows cannot be deleted.", "固定握手行不能删除。")); return; }
            if (MessageBox.Show(T("Delete this output mapping? The VPP pin is retained.", "删除此输出映射？VPP 引脚仍会保留。"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            dgvOutput.Rows.Remove(row); _tableEditor.EndLoad();
        }
        private bool ValidateBindings()
        {
            try
            {
                var names = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataGridViewRow row in dgvOutput.Rows)
                {
                    string name = GetCell(row, COL_PARAMETER); int channel = ToInt(GetCell(row, COL_CHANNEL), 0);
                    if (string.IsNullOrWhiteSpace(name) || !names.Add(channel + "|" + name)) throw new InvalidOperationException("Parameter names must be nonempty and unique within a channel.");
                    if (S7OutputData.IsFixed(name))
                    {
                        if (!(row.Tag is bool) || !(bool)row.Tag) throw new InvalidOperationException("This name is reserved for a fixed handshake signal: " + name);
                        continue;
                    }
                    string pin = GetCell(row, "VppOutput");
                    if (string.IsNullOrWhiteSpace(pin) && !S7OutputData.IsLegacy(name, channel))
                        throw new InvalidOperationException(name + ": " + T("Enter the VPP output pin name.", "请填写 VPP 输出引脚名称。"));
                }
                return true;
            }
            catch (Exception ex) { MessageBox.Show(ex.GetBaseException().Message, "S7 Output", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
        }
		private void InitGridStyle()
		{
			dgvOutput.AllowUserToAddRows = false;
			dgvOutput.AllowUserToDeleteRows = false;
			dgvOutput.RowHeadersVisible = false;
			dgvOutput.MultiSelect = false;
			dgvOutput.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			dgvOutput.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            if (!dgvOutput.Columns.Contains("VppOutput"))
                dgvOutput.Columns.Add(new DataGridViewTextBoxColumn { Name = "VppOutput", HeaderText = "VPP Output", FillWeight = 150 });
            dgvOutput.Columns["VppOutput"].DisplayIndex = 2;
			if (!dgvOutput.Columns.Contains(COL_NOTE))
				dgvOutput.Columns.Add(new DataGridViewTextBoxColumn { Name = COL_NOTE, HeaderText = "Note", FillWeight = 125 });
			_tableEditor = new S7TableEditor(dgvOutput, () => GetActiveChannelCount(S7ConfigXmlHelper.Load()));
            dgvOutput.CellBeginEdit += (sender, e) => {
                var row = dgvOutput.Rows[e.RowIndex];
                if (row.Tag is bool && (bool)row.Tag &&
                    (dgvOutput.Columns[e.ColumnIndex].Name == COL_CHANNEL || dgvOutput.Columns[e.ColumnIndex].Name == COL_PARAMETER || dgvOutput.Columns[e.ColumnIndex].Name == "VppOutput")) e.Cancel = true;
            };
            foreach (DataGridViewColumn column in dgvOutput.Columns)
            {
                column.MinimumWidth = column.Name == COL_CHANNEL || column.Name == COL_LENGTH ? 65 : column.Name == COL_PARAMETER || column.Name == "VppOutput" ? 150 : column.Name == COL_ADDRESS ? 130 : 95;
                column.FillWeight = column.MinimumWidth;
            }


			if (dgvOutput.Columns.Contains(COL_CHANNEL))
				dgvOutput.Columns[COL_CHANNEL].ReadOnly = false;

			if (dgvOutput.Columns.Contains(COL_PARAMETER))
				dgvOutput.Columns[COL_PARAMETER].ReadOnly = false;

			try
			{
				btnReadSelected.Text = "Read All";
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
			AppendLog("S7 output table saved: " + OutputTableConfigPath);
            AppendLog(T("Use Communication Save & Apply to activate the table. Apply while Offline.", "请在 Offline 状态下返回通讯窗口，点击 Save & Apply 使点表生效。"));
		}

		private void btnResetDefault_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Reset S7 output table by current S7 setting base address and channel count?",
				"S7 Output", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				return;

			LoadRowsToGrid(CreateDefaultRows());
			if (!SaveGridToXml()) return;

			AppendLog("S7 output table reset by current S7 setting.");
		}

		private void btnReadSelected_Click(object sender, EventArgs e)
		{
			try
			{
				if (!SaveGridToXml()) return;

				using (S7AddressTester tester = CreateConnectedTester())
				{
					if (tester == null)
						return;

					foreach (DataGridViewRow row in dgvOutput.Rows)
					{
						if (row == null || row.IsNewRow)
							continue;

						ReadRow(tester, row);
					}
				}

				AppendLog("S7 output read all complete.");
			}
			catch (Exception ex)
			{
				AppendLog("Read all exception: " + ex.Message);
				MessageBox.Show(ex.Message, "S7 Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnWriteSelected_Click(object sender, EventArgs e)
		{
			try
			{
				if (!SaveGridToXml()) return;

				if (dgvOutput.CurrentRow == null)
				{
					MessageBox.Show("Please select one row first.");
					return;
				}

				using (S7AddressTester tester = CreateConnectedTester())
				{
					if (tester == null)
						return;

					WriteRow(tester, dgvOutput.CurrentRow);
				}
			}
			catch (Exception ex)
			{
				AppendLog("Write selected exception: " + ex.Message);
				MessageBox.Show(ex.Message, "S7 Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void btnWriteChannel_Click(object sender, EventArgs e)
		{
			try
			{
				if (!SaveGridToXml()) return;

				if (dgvOutput.CurrentRow == null)
				{
					MessageBox.Show("Please select one channel row first.");
					return;
				}

				int channel = ToInt(GetCell(dgvOutput.CurrentRow, COL_CHANNEL), -1);
				if (channel != GLOBAL_CHANNEL && (channel < 0 || channel >= GetActiveChannelCount(_config)))
					return;

				using (S7AddressTester tester = CreateConnectedTester())
				{
					if (tester == null)
						return;

					foreach (DataGridViewRow row in dgvOutput.Rows)
					{
						if (ToInt(GetCell(row, COL_CHANNEL), -1) == channel)
							WriteRow(tester, row);
					}
				}
			}
			catch (Exception ex)
			{
				AppendLog("Write channel exception: " + ex.Message);
				MessageBox.Show(ex.Message, "S7 Output", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

		private void WriteRow(S7AddressTester tester, DataGridViewRow row)
		{
			string parameter = GetCell(row, COL_PARAMETER);
			string address = GetCell(row, COL_ADDRESS);
			if (string.IsNullOrWhiteSpace(address))
			{
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " address is empty, skipped.");
				return;
			}

			string value = GetCell(row, COL_VALUE);
			S7PointDataType type = ParseDataType(GetCell(row, COL_FORMAT));

			OperateResult result = tester.WriteByString(address, type, value);
			if (result.IsSuccess)
			{
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " [" + address + "] <= " + value);
			}
			else
			{
				AppendLog("CH" + GetCell(row, COL_CHANNEL) + " " + parameter + " [" + address + "] write failed: " + result.Message);
			}
		}

		private void LoadConfigToGrid()
		{
			_config = S7ConfigXmlHelper.Load();

			List<S7OutputSignalTableRow> rows = LoadRowsFromXml();
			if (rows == null || rows.Count == 0)
			{
				rows = CreateDefaultRows();
				SaveRowsToXml(rows);
			}
			else
			{
				bool changed;
				rows = CleanupUnusedOutputRows(rows, out changed);
				if (changed)
					SaveRowsToXml(rows);
			}

			LoadRowsToGrid(rows);
			AppendLog("S7 output table loaded.");
		}


		private List<S7OutputSignalTableRow> CleanupUnusedOutputRows(List<S7OutputSignalTableRow> rows, out bool changed)
		{
			changed = false;

			List<S7OutputSignalTableRow> cleaned = new List<S7OutputSignalTableRow>();

			if (rows == null)
				return cleaned;

			foreach (S7OutputSignalTableRow row in rows)
			{
				if (row == null)
					continue;

				if (IsUnusedOutputParameter(row.Parameter) && string.IsNullOrWhiteSpace(row.VppOutput))
				{
					changed = true;
					continue;
				}

				cleaned.Add(row);
			}

			return cleaned;
		}

		private bool IsUnusedOutputParameter(string parameter)
		{
			if (string.IsNullOrWhiteSpace(parameter))
				return false;

			string p = parameter.Trim();

			return string.Equals(p, "Acq Error", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(p, "Inspection Passed", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(p, "Results Buffer Overrun", StringComparison.OrdinalIgnoreCase);
		}

		private void LoadRowsToGrid(List<S7OutputSignalTableRow> rows)
		{
			// Add newly enabled channels without replacing existing project addresses.
			foreach (S7OutputSignalTableRow required in CreateDefaultRows().Where(x => S7OutputData.IsFixed(x.Parameter)))
			{
				S7OutputSignalTableRow existing = rows.Find(x => x.Channel == required.Channel && string.Equals(x.Parameter, required.Parameter, StringComparison.OrdinalIgnoreCase));
				if (existing == null)
				{
					required.DbAddress = S7OutputData.IsNdmExtension(required.Parameter) ? S7OutputData.NdmAddress(rows.Find(x => x.Channel == required.Channel && x.Parameter == "Result Valid")?.DbAddress, required.Parameter == "Inspection Complete" ? 4 : 6) : string.Empty;
					rows.Add(required);
				}
				else existing.Format = required.Format;
			}
			_tableEditor.BeginLoad();
			dgvOutput.Rows.Clear();

			int activeCount = GetActiveChannelCount(_config);
			foreach (S7OutputSignalTableRow row in rows.OrderBy(r => S7TableEditor.AddressSortKey(r.DbAddress)).ThenBy(r => r.Channel).ThenBy(r => r.Parameter))
			{
				// Keep disabled mappings on disk, but exclude them from editing and validation.
				if (row.Channel >= activeCount) continue;
				int index = dgvOutput.Rows.Add(
					row.Channel,
					row.Parameter,
					row.DbAddress,
					row.Length,
					S7TableEditor.NormalizeFormat(row.Format),
					row.CurrentValue, row.VppOutput, row.Note
				);
                dgvOutput.Rows[index].Tag = S7OutputData.IsFixed(row.Parameter);
                dgvOutput.Rows[index].Cells["VppOutput"].ReadOnly = S7OutputData.IsFixed(row.Parameter);
			}
			_tableEditor.EndLoad();
		}

		private List<S7OutputSignalTableRow> CreateDefaultRows()
		{
			List<S7OutputSignalTableRow> rows = new List<S7OutputSignalTableRow>();

			S7CommConfig cfg = S7ConfigXmlHelper.Load();

			string outputDb = "DB101";
			int outputBase = 0;

			if (cfg != null && cfg.Basic != null)
			{
				outputDb = GetDbName(cfg.Basic.OutputStartAddress, "DB101");
				outputBase = GetBaseByte(cfg.Basic.OutputStartAddress);
			}

			// Global outputs. Channel = -1 means this row is not tied to CH0/CH1.
			rows.Add(new S7OutputSignalTableRow(GLOBAL_CHANNEL, "System Ready", MakeBoolAddress(outputDb, outputBase + 0, 0), "", "Bool", ""));
			rows.Add(new S7OutputSignalTableRow(GLOBAL_CHANNEL, "System Busy", MakeBoolAddress(outputDb, outputBase + 0, 1), "", "Bool", ""));
			rows.Add(new S7OutputSignalTableRow(GLOBAL_CHANNEL, "Online", MakeBoolAddress(outputDb, outputBase + 0, 7), "", "Bool", ""));

			for (int ch = 0; ch < GetActiveChannelCount(cfg); ch++)
			{
				int count = Math.Max(4, GetActiveChannelCount(cfg));
				int engineByte = outputBase + 16 + ch * 8;
				int engineAckByte = engineByte + 1;

				int jobIdByte = outputBase + 16 + count * 8 + ch;
				int posIdByte = outputBase + 16 + count * 9 + ch;

				// Each result block:
				// X Float 4 + Y Float 4 + A Float 4 + Result Int16 2 = 14 bytes
				int resultBase = outputBase + 16 + count * 10 + ch * 14;

				rows.Add(new S7OutputSignalTableRow(ch, "Trigger Ready Camera", MakeBoolAddress(outputDb, engineByte, 0), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Trigger Ack Camera", MakeBoolAddress(outputDb, engineByte, 1), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Exposure Complete", MakeBoolAddress(outputDb, engineByte, 3), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Result Valid", MakeBoolAddress(outputDb, engineByte, 5), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Inspection Complete", MakeBoolAddress(outputDb, engineByte, 4), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Results Buffer Overrun", MakeBoolAddress(outputDb, engineByte, 6), "", "Bool", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "SetUserData Ack", MakeBoolAddress(outputDb, engineAckByte, 0), "", "Bool", ""));

				rows.Add(new S7OutputSignalTableRow(ch, "JobID", MakeByteAddress(outputDb, jobIdByte), "1", "Byte", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "PosID", MakeByteAddress(outputDb, posIdByte), "1", "Byte", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Result X", MakeByteAddress(outputDb, resultBase), "4", "Float", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Result Y", MakeByteAddress(outputDb, resultBase + 4), "4", "Float", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Result A", MakeByteAddress(outputDb, resultBase + 8), "4", "Float", ""));
				rows.Add(new S7OutputSignalTableRow(ch, "Result" + ch.ToString(), MakeByteAddress(outputDb, resultBase + 12), "2", "Int16", ""));
			}

			return rows;
		}

		private bool SaveGridToXml()
		{
			dgvOutput.EndEdit();
			if (!_tableEditor.CanSave() || !ValidateBindings()) return false;

			List<S7OutputSignalTableRow> rows = new List<S7OutputSignalTableRow>();

			foreach (DataGridViewRow gridRow in dgvOutput.Rows)
			{
				if (gridRow.IsNewRow)
					continue;

				S7OutputSignalTableRow row = new S7OutputSignalTableRow();
				row.Channel = ToInt(GetCell(gridRow, COL_CHANNEL), 0);
				row.Parameter = GetCell(gridRow, COL_PARAMETER);
				row.DbAddress = GetCell(gridRow, COL_ADDRESS);
				row.Length = GetCell(gridRow, COL_LENGTH);
				row.Format = NormalizeFormat(GetCell(gridRow, COL_FORMAT));
				row.CurrentValue = GetCell(gridRow, COL_VALUE);
                row.VppOutput = GetCell(gridRow, "VppOutput");
				row.Note = GetCell(gridRow, COL_NOTE);

				rows.Add(row);
			}

			// Saving the visible channels must not erase mappings for temporarily disabled channels.
			List<S7OutputSignalTableRow> savedRows = LoadRowsFromXml();
			int activeCount = GetActiveChannelCount(_config);
			if (savedRows != null) rows.AddRange(savedRows.FindAll(row => row.Channel >= activeCount));
			SaveRowsToXml(rows.OrderBy(row => S7TableEditor.AddressSortKey(row.DbAddress)).ThenBy(row => row.Channel).ThenBy(row => row.Parameter).ToList());
			return true;
		}

		private List<S7OutputSignalTableRow> LoadRowsFromXml()
		{
			try
			{
				if (!File.Exists(OutputTableConfigPath))
					return null;

				XmlSerializer serializer = new XmlSerializer(typeof(S7OutputSignalTableConfig));

				using (FileStream fs = new FileStream(OutputTableConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					S7OutputSignalTableConfig cfg = serializer.Deserialize(fs) as S7OutputSignalTableConfig;
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

		private void SaveRowsToXml(List<S7OutputSignalTableRow> rows)
		{
			string dir = Path.GetDirectoryName(OutputTableConfigPath);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			S7OutputSignalTableConfig cfg = new S7OutputSignalTableConfig();
			cfg.Rows = rows ?? new List<S7OutputSignalTableRow>();

			XmlSerializer serializer = new XmlSerializer(typeof(S7OutputSignalTableConfig));

			string temporary = OutputTableConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream fs = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) serializer.Serialize(fs, cfg);
                if (File.Exists(OutputTableConfigPath)) File.Replace(temporary, OutputTableConfigPath, null); else File.Move(temporary, OutputTableConfigPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
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

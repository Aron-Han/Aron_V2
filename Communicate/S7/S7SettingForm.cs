using System;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;

using System.Windows.Forms;

using HslCommunication;



namespace Aron_V2

{

	public partial class S7SettingForm : Form

	{

		private S7CommConfig _config;

		private S7AddressTester _tester;

		private S7HeartbeatService _heartbeat;



		private const string KEY_ENABLE = "Enable S7";

		private const string KEY_PLC_TYPE = "PLC Type";

		private const string KEY_IP = "IP";

		private const string KEY_PORT = "Port";

		private const string KEY_RACK = "Rack";

		private const string KEY_SLOT = "Slot";

		private const string KEY_TIMEOUT = "Connect Timeout(ms)";

		private const string KEY_POLL = "Poll Interval(ms)";
		private const string KEY_ACTIVE_CHANNEL_COUNT = "Active Channel Count";

		private const string KEY_INPUT_START = "Input Start";

		private const string KEY_INPUT_LENGTH = "Input Length";

		private const string KEY_OUTPUT_START = "Output Start";

		private const string KEY_OUTPUT_LENGTH = "Output Length";

		private const int GLOBAL_CHANNEL = -1;



		private const string KEY_HB_ENABLE = "Enable Heartbeat";

		private const string KEY_HB_ADDRESS = "Byte Address";

		private const string KEY_HB_VALUE = "Character";

		private const string KEY_HB_INTERVAL = "Interval(ms)";



		private const string KEY_MANUAL_ADDRESS = "Address";

		private const string KEY_MANUAL_TYPE = "Type";

		private const string KEY_MANUAL_STRING_LENGTH = "String Length";

		private const string KEY_MANUAL_WRITE_VALUE = "Write Value";

		private const string KEY_MANUAL_READ_RESULT = "Read Result";

		public event EventHandler S7SettingSaved;

		public S7SettingForm()

		{

			InitializeComponent();

		}



		private void S7SettingForm_Load(object sender, EventArgs e)

		{

			InitGridStyle();

			LoadConfigToUi();

		}



		private void InitGridStyle()

		{

			SetGridStyle(dgvBasic);

			SetGridStyle(dgvHeartbeat);

			SetGridStyle(dgvManual);



			dgvBasic.Columns["colBasicName"].ReadOnly = true;

			dgvBasic.Columns["colBasicDescription"].ReadOnly = true;



			dgvHeartbeat.Columns["colHeartbeatName"].ReadOnly = true;

			dgvHeartbeat.Columns["colHeartbeatDescription"].ReadOnly = true;



			dgvManual.Columns["colManualName"].ReadOnly = true;

			dgvManual.Columns["colManualDescription"].ReadOnly = true;

		}



		private void SetGridStyle(DataGridView dgv)

		{

			dgv.RowHeadersVisible = false;

			dgv.AllowUserToAddRows = false;

			dgv.AllowUserToDeleteRows = false;

			dgv.AllowUserToResizeRows = false;

			dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;

			dgv.MultiSelect = false;

			dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

			dgv.RowTemplate.Height = 26;

		}



		private void btnLoad_Click(object sender, EventArgs e)

		{

			LoadConfigToUi();

		}



		private void btnSave_Click(object sender, EventArgs e)
		{
			SaveUiToConfig();

			S7ConfigXmlHelper.Save(_config);

			// Existing project address tables are authoritative: saving connection settings must not erase custom bindings.
			EnsureS7PointTables();

			AppendLog("S7 setting saved. Existing input/output tables and VPP bindings retained; edit point addresses in Input/Output Setting.");

			// 通知 FormMain 立即重启 S7 通讯服务，使新配置无需重启软件即可生效。
			EventHandler handler = S7SettingSaved;
			if (handler != null)
				handler(this, EventArgs.Empty);
		}

		private void EnsureS7PointTables()
		{
			string inputPath = ProjectPaths.Config("Communication/S7/S7InputTableConfig.xml");
			string outputPath = ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml");

            if (!File.Exists(inputPath)) SaveInputRowsToXml(CreateDefaultInputRows(_config), inputPath);
            if (!File.Exists(outputPath)) SaveOutputRowsToXml(CreateDefaultOutputRows(_config), outputPath);
		}

		private void BackupIfExists(string path)
		{
			try
			{
				if (!File.Exists(path))
					return;

				string bak = path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
				File.Copy(path, bak, true);
			}
			catch (Exception ex)
			{
				AppendLog("Backup S7 point table failed: " + ex.Message);
			}
		}

		private List<S7InputSignalTableRow> CreateDefaultInputRows(S7CommConfig cfg)
		{
			List<S7InputSignalTableRow> rows = new List<S7InputSignalTableRow>();

			string inputDb = "DB100";
			int inputBase = 0;

			if (cfg != null && cfg.Basic != null)
			{
				inputDb = GetDbName(cfg.Basic.InputStartAddress, "DB100");
				inputBase = GetBaseByte(cfg.Basic.InputStartAddress);
			}

			// Global input. Channel = -1 means this row is not tied to CH0/CH1.
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
				int bitByte = inputBase + 16 + ch;
				int clearResultByte = inputBase + 20 + ch;
				int jobIdByte = inputBase + 24 + ch;
				int posIdByte = inputBase + 28 + ch;
				int partNoByte = inputBase + 32 + ch * 40;

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

		private List<S7OutputSignalTableRow> CreateDefaultOutputRows(S7CommConfig cfg)
		{
			List<S7OutputSignalTableRow> rows = new List<S7OutputSignalTableRow>();

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
				int engineByte = outputBase + 16 + ch * 8;
				int engineAckByte = engineByte + 1;
				int jobIdByte = outputBase + 48 + ch;
				int posIdByte = outputBase + 52 + ch;
				int resultBase = outputBase + 56 + ch * 14;

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

		private void SaveInputRowsToXml(List<S7InputSignalTableRow> rows, string path)
		{
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			S7InputSignalTableConfig cfg = new S7InputSignalTableConfig();
			cfg.Rows = rows ?? new List<S7InputSignalTableRow>();

			XmlSerializer serializer = new XmlSerializer(typeof(S7InputSignalTableConfig));
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				serializer.Serialize(fs, cfg);
			}
		}

		private void SaveOutputRowsToXml(List<S7OutputSignalTableRow> rows, string path)
		{
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			S7OutputSignalTableConfig cfg = new S7OutputSignalTableConfig();
			cfg.Rows = rows ?? new List<S7OutputSignalTableRow>();

			XmlSerializer serializer = new XmlSerializer(typeof(S7OutputSignalTableConfig));
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				serializer.Serialize(fs, cfg);
			}
		}

		private static int GetActiveChannelCount(S7CommConfig cfg)
		{
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

		private void btnConnectTest_Click(object sender, EventArgs e)

		{

			SaveUiToConfig();



			StopHeartbeat();



			if (_tester != null)

			{

				_tester.Dispose();

				_tester = null;

			}



			_tester = new S7AddressTester(_config.Basic);

			OperateResult result = _tester.Connect();



			if (result.IsSuccess)

			{

				AppendLog("Connect success. PLC=" + _config.Basic.PlcType + ", IP=" + _config.Basic.IpAddress);



				if (_config.Basic.HeartbeatEnable)

					StartHeartbeat();



				MessageBox.Show("S7 connect success.\r\n\r\nThis verifies only the temporary connection test. Click Save in this window, then Save & Apply in Communication to restart the runtime S7 service and verify the configured DB points.", "S7", MessageBoxButtons.OK, MessageBoxIcon.Information);

			}

			else

			{

				AppendLog("Connect failed: " + result.Message);

				MessageBox.Show("S7 connect failed:\r\n" + result.Message, "S7", MessageBoxButtons.OK, MessageBoxIcon.Error);

			}

		}



		private void btnHeartbeatOnce_Click(object sender, EventArgs e)

		{

			try

			{

				EnsureTesterConnected();



				OperateResult result = _tester.WriteHeartbeatOnce();

				if (result.IsSuccess)

				{

					AppendLog("Heartbeat write once success. "

						+ _config.Basic.HeartbeatAddress + " <= " + _config.Basic.HeartbeatValue);

				}

				else

				{

					AppendLog("Heartbeat write once failed: " + result.Message);

					MessageBox.Show(result.Message, "S7 Heartbeat", MessageBoxButtons.OK, MessageBoxIcon.Error);

				}

			}

			catch (Exception ex)

			{

				AppendLog("Heartbeat write once exception: " + ex.Message);

				MessageBox.Show(ex.Message, "S7 Heartbeat", MessageBoxButtons.OK, MessageBoxIcon.Error);

			}

		}



		private void btnHeartbeatStartStop_Click(object sender, EventArgs e)

		{

			try

			{

				SaveUiToConfig();



				if (_heartbeat != null && _heartbeat.IsRunning)

				{

					StopHeartbeat();

					btnHeartbeatStartStop.Text = "Start Heartbeat";

					return;

				}



				EnsureTesterConnected();

				StartHeartbeat();

				btnHeartbeatStartStop.Text = "Stop Heartbeat";

			}

			catch (Exception ex)

			{

				AppendLog("Start/stop heartbeat exception: " + ex.Message);

				MessageBox.Show(ex.Message, "S7 Heartbeat", MessageBoxButtons.OK, MessageBoxIcon.Error);

			}

		}



		private void StartHeartbeat()

		{

			StopHeartbeat();



			if (_tester == null || !_tester.IsConnected)

			{

				AppendLog("Heartbeat cannot start because S7 is not connected.");

				return;

			}



			if (!_config.Basic.HeartbeatEnable)

			{

				AppendLog("Heartbeat is disabled.");

				return;

			}



			_heartbeat = new S7HeartbeatService(_tester, _config.Basic);

			_heartbeat.LogMessage += OnHeartbeatLog;

			_heartbeat.Start();

			btnHeartbeatStartStop.Text = "Stop Heartbeat";

		}



		private void StopHeartbeat()

		{

			if (_heartbeat != null)

			{

				_heartbeat.LogMessage -= OnHeartbeatLog;

				_heartbeat.Dispose();

				_heartbeat = null;

			}



			if (btnHeartbeatStartStop != null)

				btnHeartbeatStartStop.Text = "Start Heartbeat";

		}



		private void OnHeartbeatLog(string msg)

		{

			try

			{

				if (this.IsDisposed)

					return;



				if (this.InvokeRequired)

				{

					this.BeginInvoke(new Action<string>(OnHeartbeatLog), msg);

					return;

				}



				AppendLog(msg);

			}

			catch

			{

			}

		}



		private void btnRead_Click(object sender, EventArgs e)

		{

			try

			{

				EnsureTesterConnected();



				string address = GetTableValue(dgvManual, KEY_MANUAL_ADDRESS);

				S7PointDataType type = GetManualDataType();

				ushort len = (ushort)ToInt(GetTableValue(dgvManual, KEY_MANUAL_STRING_LENGTH), 8);



				OperateResult<string> result = _tester.ReadAsString(address, type, len);



				if (result.IsSuccess)

				{

					SetTableValue(dgvManual, KEY_MANUAL_READ_RESULT, result.Content);

					AppendLog("Read success. " + address + " = " + result.Content);

				}

				else

				{

					AppendLog("Read failed. " + address + ", " + result.Message);

					MessageBox.Show(result.Message, "S7 Read", MessageBoxButtons.OK, MessageBoxIcon.Error);

				}

			}

			catch (Exception ex)

			{

				AppendLog("Read exception: " + ex.Message);

				MessageBox.Show(ex.Message, "S7 Read", MessageBoxButtons.OK, MessageBoxIcon.Error);

			}

		}



		private void btnWrite_Click(object sender, EventArgs e)

		{

			try

			{

				EnsureTesterConnected();



				string address = GetTableValue(dgvManual, KEY_MANUAL_ADDRESS);

				S7PointDataType type = GetManualDataType();

				string value = GetTableValue(dgvManual, KEY_MANUAL_WRITE_VALUE);



				OperateResult result = _tester.WriteByString(address, type, value);



				if (result.IsSuccess)

				{

					AppendLog("Write success. " + address + " <= " + value);

				}

				else

				{

					AppendLog("Write failed. " + address + ", " + result.Message);

					MessageBox.Show(result.Message, "S7 Write", MessageBoxButtons.OK, MessageBoxIcon.Error);

				}

			}

			catch (Exception ex)

			{

				AppendLog("Write exception: " + ex.Message);

				MessageBox.Show(ex.Message, "S7 Write", MessageBoxButtons.OK, MessageBoxIcon.Error);

			}

		}



		private void LoadConfigToUi()

		{

			_config = S7ConfigXmlHelper.Load();

			_config.EnsureFourChannels();



			S7BasicSetting b = _config.Basic;



			dgvBasic.Rows.Clear();

			AddBasicRow(KEY_ENABLE, b.Enable.ToString(), "true / false");

			AddBasicRow(KEY_PLC_TYPE, b.PlcType, "S1200 / S1500 / S300 / S400 / S200Smart");

			AddBasicRow(KEY_IP, b.IpAddress, "PLC IP address");

			AddBasicRow(KEY_PORT, b.Port.ToString(), "S7 TCP default is 102");

			AddBasicRow(KEY_RACK, b.Rack.ToString(), "Usually 0");

			AddBasicRow(KEY_SLOT, b.Slot.ToString(), "S1200/S1500 usually 0 or 1; S300 often 2");

			AddBasicRow(KEY_TIMEOUT, b.ConnectTimeoutMs.ToString(), "Connection timeout");

			AddBasicRow(KEY_POLL, b.PollIntervalMs.ToString(), "Runtime trigger polling interval");
			AddBasicRow(KEY_ACTIVE_CHANNEL_COUNT, b.ActiveChannelCount.ToString(), "1~64; Profinet alone is fixed at 4 channels");

			AddBasicRow(KEY_INPUT_START, b.InputStartAddress, "PLC -> Vision base address, e.g. DB100.0");

			AddBasicRow(KEY_INPUT_LENGTH, b.InputLength.ToString(), "PLC -> Vision total length");

			AddBasicRow(KEY_OUTPUT_START, b.OutputStartAddress, "Vision -> PLC base address, e.g. DB101.0");

			AddBasicRow(KEY_OUTPUT_LENGTH, b.OutputLength.ToString(), "Vision -> PLC total length");



			dgvHeartbeat.Rows.Clear();

			AddHeartbeatRow(KEY_HB_ENABLE, b.HeartbeatEnable.ToString(), "true / false");

			AddHeartbeatRow(KEY_HB_ADDRESS, b.HeartbeatAddress, "Byte address, e.g. DB101.100");

			AddHeartbeatRow(KEY_HB_VALUE, b.HeartbeatValue, "One character, e.g. A / 1 / H");

			AddHeartbeatRow(KEY_HB_INTERVAL, b.HeartbeatIntervalMs.ToString(), "Heartbeat period in ms");



			dgvManual.Rows.Clear();

			AddManualRow(KEY_MANUAL_ADDRESS, "DB100.0.0", "HSL address, e.g. DB100.0.0 / DB100.2 / DB101.100");

			AddManualRow(KEY_MANUAL_TYPE, S7PointDataType.Bool.ToString(), "Bool / Int16 / Int32 / Float / Double / String");

			AddManualRow(KEY_MANUAL_STRING_LENGTH, "8", "Only used when Type = String");

			AddManualRow(KEY_MANUAL_WRITE_VALUE, "", "Value to write");

			AddManualRow(KEY_MANUAL_READ_RESULT, "", "Read result, read only");



			SetManualReadResultReadOnly();



			AppendLog("S7 setting loaded.");

		}



		private void AddBasicRow(string name, string value, string description)

		{

			dgvBasic.Rows.Add(name, value, description);

		}



		private void AddHeartbeatRow(string name, string value, string description)

		{

			dgvHeartbeat.Rows.Add(name, value, description);

		}



		private void AddManualRow(string name, string value, string description)

		{

			dgvManual.Rows.Add(name, value, description);

		}



		private void SetManualReadResultReadOnly()

		{

			foreach (DataGridViewRow row in dgvManual.Rows)

			{

				if (Convert.ToString(row.Cells[0].Value) == KEY_MANUAL_READ_RESULT)

				{

					row.Cells[1].ReadOnly = true;

					row.Cells[1].Style.BackColor = System.Drawing.SystemColors.Control;

					break;

				}

			}

		}



		private void SaveUiToConfig()

		{

			if (_config == null)

				_config = new S7CommConfig();



			if (_config.Basic == null)

				_config.Basic = new S7BasicSetting();



			dgvBasic.EndEdit();

			dgvHeartbeat.EndEdit();

			dgvManual.EndEdit();



			S7BasicSetting b = _config.Basic;



			b.Enable = ToBool(GetTableValue(dgvBasic, KEY_ENABLE));

			b.PlcType = GetTableValue(dgvBasic, KEY_PLC_TYPE);

			b.IpAddress = GetTableValue(dgvBasic, KEY_IP);

			b.Port = ToInt(GetTableValue(dgvBasic, KEY_PORT), 102);

			b.Rack = (byte)ToInt(GetTableValue(dgvBasic, KEY_RACK), 0);

			b.Slot = (byte)ToInt(GetTableValue(dgvBasic, KEY_SLOT), 0);

			b.ConnectTimeoutMs = ToInt(GetTableValue(dgvBasic, KEY_TIMEOUT), 3000);

			b.PollIntervalMs = ToInt(GetTableValue(dgvBasic, KEY_POLL), 50);

			b.ActiveChannelCount = ClampActiveChannelCount(ToInt(GetTableValue(dgvBasic, KEY_ACTIVE_CHANNEL_COUNT), 2));

			b.InputStartAddress = GetTableValue(dgvBasic, KEY_INPUT_START);

			b.InputLength = ToInt(GetTableValue(dgvBasic, KEY_INPUT_LENGTH), 64);

			b.OutputStartAddress = GetTableValue(dgvBasic, KEY_OUTPUT_START);

			b.OutputLength = ToInt(GetTableValue(dgvBasic, KEY_OUTPUT_LENGTH), 128);



			b.HeartbeatEnable = ToBool(GetTableValue(dgvHeartbeat, KEY_HB_ENABLE));

			b.HeartbeatAddress = GetTableValue(dgvHeartbeat, KEY_HB_ADDRESS);



			string hbValue = GetTableValue(dgvHeartbeat, KEY_HB_VALUE);

			b.HeartbeatValue = string.IsNullOrEmpty(hbValue) ? "A" : hbValue.Substring(0, 1);



			b.HeartbeatIntervalMs = ToInt(GetTableValue(dgvHeartbeat, KEY_HB_INTERVAL), 1000);



			_config.EnsureFourChannels();

		}



		private string GetTableValue(DataGridView dgv, string name)

		{

			if (dgv == null)

				return string.Empty;



			foreach (DataGridViewRow row in dgv.Rows)

			{

				string rowName = Convert.ToString(row.Cells[0].Value);

				if (string.Equals(rowName, name, StringComparison.OrdinalIgnoreCase))

				{

					object value = row.Cells[1].Value;

					return value == null ? string.Empty : value.ToString().Trim();

				}

			}



			return string.Empty;

		}



		private void SetTableValue(DataGridView dgv, string name, string value)

		{

			if (dgv == null)

				return;



			foreach (DataGridViewRow row in dgv.Rows)

			{

				string rowName = Convert.ToString(row.Cells[0].Value);

				if (string.Equals(rowName, name, StringComparison.OrdinalIgnoreCase))

				{

					row.Cells[1].Value = value;

					return;

				}

			}

		}



		private void EnsureTesterConnected()

		{

			SaveUiToConfig();



			if (_tester == null || !_tester.IsConnected)

			{

				_tester = new S7AddressTester(_config.Basic);

				OperateResult connect = _tester.Connect();



				if (!connect.IsSuccess)

					throw new Exception("S7 connect failed: " + connect.Message);

			}

		}



		private S7PointDataType GetManualDataType()

		{

			string typeName = GetTableValue(dgvManual, KEY_MANUAL_TYPE);

			if (string.IsNullOrWhiteSpace(typeName))

				return S7PointDataType.Bool;



			try

			{

				return (S7PointDataType)Enum.Parse(typeof(S7PointDataType), typeName, true);

			}

			catch

			{

				return S7PointDataType.Bool;

			}

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




		private int ClampActiveChannelCount(int value)
		{
			if (value <= 0)
				return 2;

			if (value > CommunicationConfigHelper.MaxScalableChannelCount)
				return CommunicationConfigHelper.MaxScalableChannelCount;

			return value;
		}

		private bool ToBool(string value)

		{

			if (string.IsNullOrWhiteSpace(value))

				return false;



			string s = value.Trim().ToLower();

			return s == "true" || s == "1" || s == "yes" || s == "on";

		}



		private void AppendLog(string msg)

		{

			txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);

		}

	}

}

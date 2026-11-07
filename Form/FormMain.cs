using Aron_V2;
using Aron_V2.Profinet;
using Aron_V2.Properties;
using Aron_V2.Security;
using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ToolBlock;
using QWhale.Editor.TextSource;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;//耗时统计
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Xml;
using System.Xml.Linq;
using WindowsFormsApp2;
using static Aron_V2.FormCamGeneralConfig;
using static Aron_V2.Load_Job;
using static Aron_V2.XmlConfigHelper;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using Sunny.UI;


namespace Aron_V2
{
	public partial class FormMain : Form
	{
		//private AppConfig config;
		private VppOutputConfig _vppOutCfg;

		public List<UserControl_ImageShow> camViews = new List<UserControl_ImageShow>();

		private Dictionary<string, Action<bool>> _permissionTargets;
		private CameraStats[] _stats;
		private InitResult _init;
		private bool _eventsHooked;

		//2024-06-17 15:00:00: 定时清理过期图片
		private System.Threading.Timer _imageCleanupTimer;
		private int _imageCleanupRunning = 0;
		private LicenseMonitor _licenseMonitor;
		private volatile bool _licenseOk = true;
		private int _licenseCloseHandled = 0;
		private int _licensePopupShown = 0;

		private int _cameraRecovering = 0;
		private System.Windows.Forms.Timer _cameraRecoveryTimer;
		private volatile bool _cameraRecoveryStopping;
		private int _cameraRestartRequested = 0;
		private volatile bool _cameraValidationComplete;
		private readonly ConcurrentDictionary<string, string> _lastCameraSerialLog =
			new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private IVisionCommunicationService _communicationService;
		private volatile bool _runOnline = true;
		private bool _switchingOnline;
		private CommunicationProtocol? _displayProtocol;
		private readonly ToolStripStatusLabel _communicationStatus = new ToolStripStatusLabel();
		private readonly System.Windows.Forms.Timer _communicationStatusTimer = new System.Windows.Forms.Timer();



		#region [lock]
		private readonly ConcurrentDictionary<string, object> _camLocks = new ConcurrentDictionary<string, object>();

		private static readonly object _sendLock = new object();

		private static readonly SemaphoreSlim[] _chGates = Enumerable.Range(0, CommunicationConfigHelper.MaxScalableChannelCount)
			.Select(_ => new SemaphoreSlim(1, 1))
			.ToArray();

		private static bool IsValidChannel(int ch) => ch >= 0 && ch < _chGates.Length;

		private static SemaphoreSlim GetGateOrThrow(int ch)
		{
			if (!IsValidChannel(ch)) throw new ArgumentOutOfRangeException(nameof(ch));
			return _chGates[ch];
		}
		#endregion

		public FormMain()
		{
			InitializeComponent();
			if (LicenseManager.UsageMode != LicenseUsageMode.Designtime) InitializeOnlineStateButton();
			InitializeCameraMenus();
			InitializeSettingsCenterMenu();
			InitializeManualSimulationMenu();
			InitializeHelpMenu();
			InitializeAboutMenu();
            InitializePluginMenu();
			DesktopTheme.ApplySurfaceColors(this);
			DesktopTheme.ApplyMainMenu(menuStrip1, tableLayoutPanel1);
			InitializeLogPanel();
			DesktopTheme.StyleMainStatus(statusStrip1, toolStripStatusLabel1, _communicationStatus);
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            InitializePlugins();
			_communicationStatus.Text = "Communicate:Null";
			_communicationStatus.ForeColor = Color.Red;
			statusStrip1.Items.Add(_communicationStatus);
			InitializeResourceStatus();
            LoadCommunicationSelectionForDisplay();
			_communicationStatusTimer.Interval = 500;
			_communicationStatusTimer.Tick += delegate { RefreshCommunicationState(); };
			_communicationStatusTimer.Start();
			FormClosed += delegate { _communicationStatusTimer.Dispose(); };
			HookEventsEarly();
			Shown += FormMain_Shown;
		}

		#region[Start load]
		public void ApplyInit(InitResult init)
		{
			_init = init ?? throw new ArgumentNullException(nameof(init));
		}

		//初始化时需要加载的事件
		private void HookEventsEarly()
		{
			if (_eventsHooked) return;
			DataChangedEventArgs.StateChanged += GlobalState_StateChanged;
			LogChangeEventArgs.StateChanged += LogState_StateChanged;
			_eventsHooked = true;
		}
		#endregion

		#region Button
		private void cameraToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenVisionEditor(() => new Camera(XmlConfigHelper.Load(Global.ParameterCogfig)), "Camera");
		}
		private void algorithmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenVisionEditor(() => new Algorithm(XmlConfigHelper.Load(Global.ParameterCogfig)), "Algorithm");
		}

		private void OpenVisionEditor(Func<Form> create, string name)
		{
			Form editor = null;
			try { editor = create(); editor.Show(this); }
			catch (Exception ex)
			{
				editor?.Dispose();
				LogChangeEventArgs.Set("Log", name + " editor failed: " + ex, Color.Red);
				var root = ex.GetBaseException();
				string detail = root.Message;
				if (root is System.IO.FileLoadException load) detail += "\r\nAssembly: " + load.FileName;
				MessageBox.Show(this, name + " could not be opened. The application will remain running.\r\n\r\n" + detail,
					name, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		private void databaseToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var Form2 = new DataBase())
			{
				Form2.ShowDialog();
			}
		}
		private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenSettingsCenter("General");
		}
		private void rotateCenterToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var Form2 = new Rotate_Center())
			{
				Form2.ShowDialog();
			}
		}
		private void parametersConfigToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenSettingsCenter("Limit Setting");
		}
		private void outputToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var Form2 = new Out_put_Parameters())
			{
				Form2.ShowDialog();
			}
		}
		private void outputSettingToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var dlg = new Out_put_Parameters();
			dlg.VppOutputSaved += (s, ev) =>
			{
				try
				{
					_vppOutCfg = ev.Config;

					LogChangeEventArgs.Set("Log", "Reflash vpp output OK", Color.Green);
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Refresh VPP output failed: " + ex.Message, Color.Red);
				}
			};

			dlg.Show(this);
		}
		private void generalToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenSettingsCenter("General Camera");
		}
		private void loginToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var dlg = new LoginSimple())
			{
				dlg.ShowInTaskbar = false;
				dlg.StartPosition = FormStartPosition.CenterParent;
				var r = dlg.ShowDialog(this);
				if (r == DialogResult.OK && dlg.LoggedInUser != null)
				{
					var user = dlg.LoggedInUser;

					// 1) 状态栏显示
					toolStripStatusLabel1.Text = $"{user.Username} ({user.Role})";

					// 2) 应用权限
					ApplyPermissions(user);
				}
			}
		}

		public void CamView_Trigger_Manual(object sender, EventArgs e)
		{
			UserControl_ImageShow clickedControl = sender as UserControl_ImageShow;

			if (clickedControl != null)
			{
				Trigger_Manual(clickedControl);
			}
		}

		public void CamView_Reset_Count(object sender, EventArgs e)
		{
			UserControl_ImageShow clickedControl = sender as UserControl_ImageShow;

			if (clickedControl != null)
			{
				_stats[int.Parse(clickedControl.CamID.Substring(3, 1)) - 1].Reset();
			}
		}

		public void CamView_Replay(object sender, EventArgs e)
		{
			UserControl_ImageShow clickedControl = sender as UserControl_ImageShow;

			if (clickedControl != null)
			{
				Replay(clickedControl);
			}
		}

		private async void Trigger_Manual(UserControl_ImageShow uc)
		{
			if (!_cameraValidationComplete || _cameraRestartRequested != 0)
			{
				LogChangeEventArgs.Set("Log", "Camera startup check is not complete; manual trigger is unavailable.", Color.Orange);
				return;
			}
			Global.Manual_Trigger_Lock[int.Parse(uc.Channel.ToString().Substring(uc.Channel.ToString().Length - 1, 1))] = true;
			try
			{
				await Cam_Inspection(uc.Name, uc.JobID, uc.PosID, uc.Channel);
			}
			finally
			{
				Global.Manual_Trigger_Lock[int.Parse(uc.Channel.ToString().Substring(uc.Channel.ToString().Length - 1, 1))] = false;
			}
		}
		public void Replay(UserControl_ImageShow uc)
		{

			ReplayImage(uc.Name, uc.PosID, uc.JobID, uc.Channel);
		}
		private void inputSettingToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var f = new FormPlcInputParameters())
			{
				f.ShowDialog(this);
			}

		}

		#endregion


		#region Event
		private void FormMain_Load(object sender, EventArgs e)
		{
			if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
			ImageRecord.LoadSettings();
			ApplySoftwareTitle();
			this.WindowState = FormWindowState.Maximized;

			Global.CurrentConfig = _init.Config;
			_vppOutCfg = _init.VppOutCfg;

			_camLocks.Clear();
			foreach (KeyValuePair<string, object> kv in _init.CamLocks)
				_camLocks[kv.Key] = kv.Value;

			UI_Load();
			UI.Init(this);
			int configuredChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());
			for (int i = 0; i < configuredChannelCount; i++)
			{
				DataChangedEventArgs.Set("JobID" + i, "Job1");
			}

			button3.Text = "Checking cameras";
			button3.BackColor = Color.Orange;
			button3.Enabled = false;

			InitPermissionTargets();
			toolStripStatusLabel1.Text = _init.DefaultUser.Username;
			ApplyPermissions(_init.DefaultUser);


			// 建议同步给全局保存路径，避免保存和清理不是同一个目录
			Global.Save_Image_Root = ImageRecord.Current.Root;

			// 启动图片定期清理
			StartImageCleanupScheduler();
			StartLicenseMonitor();
		}

		private async void FormMain_Shown(object sender, EventArgs e)
		{
			try
			{
				LogChangeEventArgs.Set("Log", "Verify active cameras with one background capture each before communication starts...", Color.Black);
				string failure = await Task.Run(() => VerifyStartupCameras());
				if (IsDisposed || _cameraRestartRequested != 0) return;
				if (!string.IsNullOrEmpty(failure))
				{
					RequestCameraRestart("Startup camera check failed: " + failure);
					return;
				}
				_cameraValidationComplete = true;
				Program.ResetCameraRestartCount();
				button3.Enabled = true;
				LogChangeEventArgs.Set("Log", "All active cameras captured successfully; communication is now allowed.", Color.Green);
				InitCommunication();
			}
			catch (Exception ex)
			{
				RequestCameraRestart("Startup camera check error: " + ex.Message);
			}
		}

		private string VerifyStartupCameras()
		{
			var activeCameras = Global.CamGeneral
				.Where(kv => kv.Value != null && (kv.Value.MainUsed == "1" || kv.Value.SecondUsed == "1"))
				.Select(kv => kv.Key.Split('.'))
				.Where(parts => parts.Length >= 3)
				.Select(parts => parts[1])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (activeCameras.Count == 0)
				return "No active cameras were found in the current job configuration.";

			// Validate every loaded active Job/Cam/Pos binding before allowing S7 to connect.
			foreach (var entry in Load_Job.VppCache.Acq)
			{
				string[] parts = entry.Key.Split('|');
				if (parts.Length != 4 || entry.Value == null) continue;
				if (CameraIntegration.CameraAcquisition.IsSdk(CameraIntegration.CameraAcquisition.Find(Global.CurrentConfig, parts[0], parts[2]))) continue;
				CameraGeneralRuntime general;
				if (!Global.CamGeneral.TryGetValue(parts[0] + "." + parts[2] + "." + parts[3], out general) ||
					general == null || (general.MainUsed != "1" && general.SecondUsed != "1")) continue;
				try
				{
					string mismatch = CameraSerialBinding.Mismatch(parts[0], parts[2], parts[3], entry.Value);
					if (mismatch != null) return mismatch;
				}
				catch (Exception ex)
				{
					return "Camera SN check failed for " + parts[0] + "/" + parts[2] + "/" + parts[3] + ": " + ex.Message;
				}
			}

			foreach (string camName in activeCameras)
			{
				var sdkBinding = Global.CamGeneral.Where(kv => kv.Value != null && (kv.Value.MainUsed == "1" || kv.Value.SecondUsed == "1"))
					.Select(kv => kv.Key.Split('.')).Where(p => p.Length >= 3 && string.Equals(p[1], camName, StringComparison.OrdinalIgnoreCase))
					.Select(p => CameraIntegration.CameraAcquisition.Find(Global.CurrentConfig, p[0], camName))
					.FirstOrDefault(c => CameraIntegration.CameraAcquisition.IsSdk(c));
				if (sdkBinding != null)
				{
					try
					{
						var image = CameraIntegration.CameraAcquisition.Capture(sdkBinding);
						(image as IDisposable)?.Dispose();
						LogChangeEventArgs.Set("Log", camName + " SDK startup capture passed (" + sdkBinding.IpAddress + ").", Color.Green);
					}
					catch (Exception ex) { return camName + " SDK startup capture failed: " + ex.GetBaseException().Message; }
					continue;
				}
				var entry = Load_Job.VppCache.Acq.FirstOrDefault(kv =>
				{
					string[] parts = kv.Key.Split('|');
					return parts.Length == 4 &&
						string.Equals(parts[2], camName, StringComparison.OrdinalIgnoreCase) && kv.Value != null;
				});
				if (entry.Value == null)
					return camName + " has no loaded acquisition tool.";

				try
				{
					LogCameraSerial(camName, entry.Value, "startup");
					entry.Value.Run();
					string[] selected = entry.Key.Split('|');
					if (selected.Length == 4)
					{
						string mismatch = CameraSerialBinding.Mismatch(selected[0], selected[2], selected[3], entry.Value);
						if (mismatch != null) return mismatch;
					}
					if (entry.Value.RunStatus == null ||
						entry.Value.RunStatus.Result.ToString() != "Accept" ||
						entry.Value.OutputImage == null)
					{
						return camName + " capture failed: " +
							(entry.Value.RunStatus == null ? "RunStatus is null" : entry.Value.RunStatus.Message);
					}
					LogChangeEventArgs.Set("Log", camName + " startup capture passed.", Color.Green);
				}
				catch (Exception ex)
				{
					return camName + " capture exception: " + ex.Message;
				}
			}
			return null;
		}

		private void LogCameraSerial(string camName, CogAcqFifoTool camera, string phase)
		{
			string serial;
			try
			{
				var grabber = camera == null || camera.Operator == null
					? null : camera.Operator.FrameGrabber;
				serial = grabber == null ? "<no frame grabber>" : grabber.SerialNumber;
				if (string.IsNullOrWhiteSpace(serial)) serial = "<empty>";
			}
			catch (Exception ex)
			{
				serial = "<read failed: " + ex.Message + ">";
			}

			string logKey = phase + ":" + camName;
			string previous;
			if (_lastCameraSerialLog.TryGetValue(logKey, out previous) &&
				string.Equals(previous, serial, StringComparison.Ordinal)) return;
			_lastCameraSerialLog[logKey] = serial;
			string message = "Camera SN diagnostic: " + camName + " (" + phase + ") SerialNumber=" + serial;
			LogManager.WriteLog(message);
			LogChangeEventArgs.Set("Log", message, Color.Black);
		}


		private void RequestCameraRestart(string reason)
		{
			if (_cameraRecoveryStopping || IsDisposed) return;
			_cameraValidationComplete = false;
			if (_communicationService != null) _communicationService.SetCameraReady(false);
			LogChangeEventArgs.Set("Log", "Camera offline; communication stopped until camera recovery: " + reason, Color.Red);
			Action schedule = () =>
			{
				if (IsDisposed || Disposing) return;
				button3.Text = "Camera Offline";
				button3.BackColor = Color.Red;
				button3.Enabled = true;
				if (_cameraRecoveryTimer == null)
				{
					_cameraRecoveryTimer = new System.Windows.Forms.Timer { Interval = 5000 };
					_cameraRecoveryTimer.Tick += (s, e) =>
					{
						if (!_cameraValidationComplete && _licenseOk)
							button3_Click(button3, EventArgs.Empty);
					};
				}
				_cameraRecoveryTimer.Start();
			};
			if (IsHandleCreated && InvokeRequired) BeginInvoke(schedule);
			else schedule();
		}

		private bool ConfirmCameraRestartInEnglish(int restartCount)
		{
			using (var dialog = new Form())
			{
				dialog.Text = "Camera Offline - Confirm Restart";
				dialog.StartPosition = FormStartPosition.CenterParent;
				dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
				dialog.ControlBox = false;
				dialog.ShowInTaskbar = false;
				dialog.ClientSize = new Size(560, 190);
				dialog.Font = new Font("Segoe UI", 10F);

				var message = new Label
				{
					Location = new Point(20, 20),
					Size = new Size(520, 105),
					Text = "Camera capture is still failing after " + restartCount + " restarts.\r\n" +
						"Restart the software again?\r\n\r\n" +
						"Stay Offline keeps communication stopped. After repair, click Camera Offline to retry."
				};
				var restartButton = new Button
				{
					Text = "Restart",
					DialogResult = DialogResult.Yes,
					Location = new Point(298, 140),
					Size = new Size(105, 32)
				};
				var stayOfflineButton = new Button
				{
					Text = "Stay Offline",
					DialogResult = DialogResult.No,
					Location = new Point(413, 140),
					Size = new Size(127, 32)
				};
				dialog.Controls.Add(message);
				dialog.Controls.Add(restartButton);
				dialog.Controls.Add(stayOfflineButton);
				dialog.CancelButton = stayOfflineButton;
				dialog.ActiveControl = stayOfflineButton;
				return dialog.ShowDialog(this) == DialogResult.Yes;
			}
		}

		private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			// 只拦截用户点击右上角 X / Alt+F4 / 菜单关闭
			if (_cameraRestartRequested == 0 && e.CloseReason == CloseReason.UserClosing)
			{
				LogChangeEventArgs.Set("Log", "User clicked close button, waiting for confirmation...", Color.Black);

				DialogResult dr = MessageBox.Show(
					"Are you sure you want to close the software?",
					"Confirm Close",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question,
					MessageBoxDefaultButton.Button2);

				if (dr != DialogResult.Yes)
				{
					LogChangeEventArgs.Set("Log", "Close canceled by user.", Color.Orange);
					e.Cancel = true;
					return;
				}

				LogChangeEventArgs.Set("Log", "User confirmed close. Software is shutting down...", Color.Red);
			}
			else
			{
				LogChangeEventArgs.Set("Log", "Software closing. Reason: " + e.CloseReason, Color.Red);
			}

			try
			{
				LogChangeEventArgs.Set("Log", "Start releasing timers and monitors...", Color.Black);
				_cameraRecoveryStopping = true;
				if (_cameraRecoveryTimer != null)
				{
					_cameraRecoveryTimer.Stop();
					_cameraRecoveryTimer.Dispose();
					_cameraRecoveryTimer = null;
				}

				if (_imageCleanupTimer != null)
				{
					_imageCleanupTimer.Dispose();
					_imageCleanupTimer = null;
					LogChangeEventArgs.Set("Log", "Image cleanup timer disposed.", Color.Black);
				}

				if (_licenseMonitor != null)
				{
					_licenseMonitor.StatusChanged -= LicenseMonitor_StatusChanged;
					_licenseMonitor.Dispose();
					_licenseMonitor = null;
					LogChangeEventArgs.Set("Log", "License monitor disposed.", Color.Black);
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Dispose timer/monitor failed: " + ex.Message, Color.Red);
			}

			if (_eventsHooked)
			{
				try
				{
					LogChangeEventArgs.Set("Log", "Start unhook events...", Color.Black);

					DataChangedEventArgs.StateChanged -= GlobalState_StateChanged;
					LogChangeEventArgs.StateChanged -= LogState_StateChanged;

					_eventsHooked = false;
				}
				catch (Exception ex)
				{
					try { LogManager.WriteLog("Unhook events failed: " + ex.Message); } catch { }
				}

				try
				{
					try { LogManager.WriteLog("Stop IO workers..."); } catch { }
					IoWorkers.Stop();

					try { LogManager.WriteLog("Release all VPP..."); } catch { }
					Load_Job.ReleaseAllVPP();
					CameraIntegration.CameraAcquisition.DisposeAll();

					try { LogManager.WriteLog("Software closed successfully."); } catch { }
				}
				catch (Exception ex)
				{
					try { LogManager.WriteLog("Software close failed: " + ex.Message); } catch { }
				}
				try
				{
					StopS7Communication();
					StopProfinetCommunication();
					LogChangeEventArgs.Set("Log", "Communication stopped.", Color.Black);
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Stop communication failed: " + ex.Message, Color.Red);
				}
			}
		}

		#endregion

		#region S7

		private void InitCommunication()
		{
			try
			{
				CommunicationConfig selected = CommunicationConfigHelper.Load();
				_displayProtocol = Enum.IsDefined(typeof(CommunicationProtocol), selected.Protocol) ? (CommunicationProtocol?)selected.Protocol : null;
				if (!_displayProtocol.HasValue) { StopS7Communication(); return; }
				if (selected.Protocol == CommunicationProtocol.Profinet)
				{
					StartProfinetCommunication();
					return;
				}

				if (selected.Protocol == CommunicationProtocol.TcpIp)
				{
					StopS7Communication();
					StopProfinetCommunication();
					LogChangeEventArgs.Set("Log", "TCP/IP selected. Waiting for the project frame specification before starting the socket transport.", Color.Orange);
					return;
				}

				StopProfinetCommunication();
				LogChangeEventArgs.Set("Log", "Start S7 communication...", Color.Black);

				bool ok = StartS7Communication();

				if (ok)
				{
					LogChangeEventArgs.Set("Log", "S7 communication started.", Color.Green);
				}
				else
				{
					// Start 返回 false 时，不代表服务已经停止。
					// 新版 S7VisionCommService 会在 PLC 离线时继续自动重连。
					LogChangeEventArgs.Set("Log", "S7重连中...", Color.Orange);
				}
			}
			catch (Exception ex)
			{
				button3.Text = "Communication Error";
				button3.BackColor = Color.Red;

				LogChangeEventArgs.Set("Log", "Init communication failed: " + ex.Message, Color.Red);
			}
			finally { RefreshCommunicationState(); }
		}

		private void StopS7Communication()
		{
			if (_communicationService == null) return;
			_communicationService.InputChanged -= CommunicationService_InputChanged;
			_communicationService.TriggerRequested -= CommunicationService_TriggerRequested;
			_communicationService.LogMessage -= CommunicationService_LogMessage;
			_communicationService.Stop();
			_communicationService = null;
		}

		private void StartProfinetCommunication()
		{
			StopS7Communication();
			_communicationService = new ProfinetVisionCommService();
			_communicationService.SetOnline(_runOnline);
			_communicationService.InputChanged += CommunicationService_InputChanged;
			_communicationService.TriggerRequested += CommunicationService_TriggerRequested;
			_communicationService.LogMessage += CommunicationService_LogMessage;
			_communicationService.Start();

			RefreshCommunicationState();
		}

		private void StopProfinetCommunication()
		{
			if (_communicationService is ProfinetVisionCommService)
				StopS7Communication();
		}

		private static float ReadFloatToolOutput(CogToolBlock tb, params string[] names)
		{
			try
			{
				if (tb == null || tb.Outputs == null || names == null)
					return 0;

				foreach (string name in names)
				{
					if (string.IsNullOrWhiteSpace(name))
						continue;

					if (!tb.Outputs.Contains(name))
						continue;

					object raw = tb.Outputs[name].Value;
					if (raw == null)
						return 0;

					if (raw is float)
						return (float)raw;

					if (raw is double)
						return Convert.ToSingle((double)raw);

					if (raw is int)
						return Convert.ToSingle((int)raw);

					if (raw is short)
						return Convert.ToSingle((short)raw);

					string text = raw.ToString();
					if (string.IsNullOrWhiteSpace(text))
						return 0;

					float parsed;
					if (float.TryParse(
						text.Trim(),
						System.Globalization.NumberStyles.Any,
						System.Globalization.CultureInfo.InvariantCulture,
						out parsed))
					{
						return parsed;
					}

					if (float.TryParse(text.Trim(), out parsed))
						return parsed;
				}
			}
			catch
			{
			}

			return 0;
		}


		//Get data
		private void ProcessCommunicationInput(CommunicationChannelInput input)
		{
			ProcessCommunicationInputCore(input, false);
		}

        private void ProcessCommunicationInputCore(CommunicationChannelInput input, bool localOnly, Action<int, string> loadJob = null)
		{
            if (input != null) DiagnosticLogStore.Stage("Input CH=" + input.Channel,
                "LocalOnly=" + localOnly + "; Online=" + _runOnline + "; CameraValidated=" + _cameraValidationComplete
                + "; SetUserData=" + input.SetUserData + "; ClearResult=" + input.ClearResult + "; JobID=" + input.JobId
                + "; PosID=" + input.PosId + "; ResultAck=" + input.InspectionResultAcknowledge + "; PartNoLength=" + (input.PartNo ?? "").Length);
			if (input == null || (!localOnly && !_cameraValidationComplete) || _cameraRecoveryStopping) return;
			int channel = input.Channel;

			if (!IsValidChannel(channel))
			{
				LogChangeEventArgs.Set("Log", "Invalid communication channel: " + channel, Color.Red);
				return;
			}

			var gate = GetGateOrThrow(channel);
			gate.Wait();

			try
			{
				if ((!localOnly && (!_runOnline || !_cameraValidationComplete)) || _cameraRecoveryStopping) return;
				// 1. PLC 请求清 Exposure Complete
				if (input.ClearExposureComplete)
				{
					if (!localOnly && _communicationService != null)
						_communicationService.ClearExposureComplete(channel);

					LogChangeEventArgs.Set("Log", $"Communication Clear Exposure Complete channel{channel}", Color.Black);
				}

				// 2. PLC 已采集结果，请视觉只清 Result Valid
				if (input.InspectionResultAcknowledge)
				{
					if (!localOnly && _communicationService != null)
						_communicationService.ClearInspectionResult(channel);

					LogChangeEventArgs.Set("Log", $"Communication Inspection Result Acknowledge channel{channel}", Color.Black);
				}

				// 3. 没有 SetUserData 上升沿，不处理 ClearResult / JobID / PosID
				if (!input.SetUserData)
					return;

				// 4. ClearResult 优先级高于 JobID / PosID
				if (input.ClearResult > 0)
				{
					if (!localOnly && _communicationService != null)
						_communicationService.ClearAllInspectionResult(channel);
					else if (localOnly)
						LogChangeEventArgs.Set("Log", "Manual local-only ClearResult: PLC output/buffer unchanged; Job/Pos/PartN skipped by normal ClearResult priority.", Color.Blue);

					LogChangeEventArgs.Set("Log", $"Communication ClearResult by SetUserData channel{channel}", Color.Black);
					return;
				}

				// 5. SetUserData 上升沿，且 ClearResult 未置位，才处理 Job / Pos
				int wantJobDigit = input.JobId;
				int wantPosDigit = input.PosId;
				string wantJobName = "Job" + wantJobDigit.ToString();

				if (wantJobDigit <= 0 || wantPosDigit <= 0)
				{
					LogChangeEventArgs.Set("Log",
						$"SetUserData received, but JobID/PosID invalid. CH={channel}, JobID={wantJobDigit}, PosID={wantPosDigit}",
						Color.Red);
					return;
				}

				var cfg = Global.CurrentConfig;

				bool needJobChange = true;
				var currentShown = Global.Model_JobID_Send[channel];

				if (!string.IsNullOrEmpty(currentShown)
					&& currentShown.StartsWith("Job", StringComparison.OrdinalIgnoreCase)
					&& int.TryParse(currentShown.Substring(3), out var shownDigit)
					&& shownDigit == wantJobDigit)
				{
					needJobChange = false;
				}

				if (needJobChange)
				{
					var vr = ValidateJobCamPos(cfg, channel, wantJobDigit, wantPosDigit);

					if (vr.Ok)
					{
						LogChangeEventArgs.Set("Log",
							$"Start Change Job: {vr.JobName}.{vr.CamName}.{vr.PosName} (Ch={channel})",
							Color.Black);

						if (Global.Model_JobID[channel] != wantJobName)
						{
							if (loadJob == null) Load_Job.LoadVPP_ForChannel_AllPos(channel, wantJobName);
							else loadJob(channel, wantJobName);
						}

						Global.Model_JobID[channel] = wantJobName;
						Global.Model_JobID_Send[channel] = wantJobName;
						Global.Position_ID[channel] = wantPosDigit.ToString();
						Global.Position_ID_Send[channel] = Global.Position_ID[channel];

						Global.PartCode[channel] = input.PartNo ?? "";

						// UI 自定义控件显示需要 Job1/Job2，而不是 1/2。
						DataChangedEventArgs.Set("JobID" + channel, wantJobName);
						DataChangedEventArgs.Set("PosID" + channel, wantPosDigit.ToString());

						// 反馈给 PLC：Output 表里当前 Channel 的 JobID / PosID
						if (!localOnly && _communicationService != null)
							_communicationService.WriteJobPosEcho(channel, (byte)wantJobDigit, (byte)wantPosDigit);

						LogChangeEventArgs.Set("Log",
							$"Change Job Complete: {vr.JobName}.{vr.CamName}.{vr.PosName} (Ch={channel})",
							Color.Green);
					}
					else
					{
						LogChangeEventArgs.Set("Log", $"Communication change failed: {vr.Error} (Ch={channel})", Color.Red);
					}
				}
				else
				{
					var vr2 = ValidatePosInCurrentJob(cfg, channel, wantPosDigit);

					if (vr2.Ok)
					{
						Global.Model_JobID[channel] = wantJobName;
						Global.Model_JobID_Send[channel] = wantJobName;
						Global.Position_ID[channel] = wantPosDigit.ToString();
						Global.Position_ID_Send[channel] = Global.Position_ID[channel];

						Global.PartCode[channel] = input.PartNo ?? "";

						// 即使只是切 Pos，也同步刷新 Job 显示，避免 UI 仍显示 1。
						DataChangedEventArgs.Set("JobID" + channel, wantJobName);
						DataChangedEventArgs.Set("PosID" + channel, wantPosDigit.ToString());

						// 反馈给 PLC：Output 表里当前 Channel 的 JobID / PosID
						if (!localOnly && _communicationService != null)
							_communicationService.WriteJobPosEcho(channel, (byte)wantJobDigit, (byte)wantPosDigit);

						LogChangeEventArgs.Set("Log",
							$"Change Pos Complete: {vr2.CamName}.{vr2.PosName} (Ch={channel})",
							Color.Green);
					}
					else
					{
						LogChangeEventArgs.Set("Log", $"Communication change Pos failed: {vr2.Error} (Ch={channel})", Color.Red);
					}
				}
			}
			catch (Exception ex)
			{
                DiagnosticLogStore.Exception("ProcessCommunicationInput CH=" + channel, ex);
				LogChangeEventArgs.Set("Log", "ProcessCommunicationInput error: " + ex.Message, Color.Red);
			}
			finally
			{
				gate.Release();
			}
		}

		//Trigger
		private Task ProcessCommunicationTrigger(int channel)
		{
			return ProcessCommunicationTriggerCore(channel, false, null);
		}

		private async Task ProcessCommunicationTriggerCore(int channel, bool localOnly, Func<string, string, string, int, Task> inspect)
		{
            DiagnosticLogStore.Stage("Trigger CH=" + channel, "Received; LocalOnly=" + localOnly + "; Online=" + _runOnline
                + "; CameraValidated=" + _cameraValidationComplete + "; RestartRequested=" + _cameraRestartRequested);
			if ((!localOnly && !_runOnline) || !_cameraValidationComplete || _cameraRestartRequested != 0) return;
			if (!IsValidChannel(channel))
			{
				LogChangeEventArgs.Set("Log", "Receive trigger but channel invalid: " + channel, Color.Red);
				return;
			}

			var gate = GetGateOrThrow(channel);
			DiagnosticLogStore.Stage("Trigger CH=" + channel, "Waiting for channel gate");
			await gate.WaitAsync().ConfigureAwait(false);

			try
			{
				if ((!localOnly && !_runOnline) || !_cameraValidationComplete || _cameraRestartRequested != 0) return;
				LogChangeEventArgs.Set("Log", "Receive communication trigger: " + channel, Color.Green);

				string jobSnap = Global.Model_JobID[channel];
				string posSnap = Global.Position_ID[channel];
                DiagnosticLogStore.Stage("Trigger CH=" + channel, "Gate acquired; Job=" + jobSnap + "; Pos=" + posSnap);

				string camName = XmlConfigHelper.FindCamByChannel(jobSnap, channel);

				int camIndex;
				if (!TryParseCamIndex(camName, out camIndex))
				{
					LogChangeEventArgs.Set("Log", $"No valid camera for channel {channel}: {camName}", Color.Red);
					return;
				}

				if (!localOnly && _communicationService != null)
					_communicationService.NotifyInspectionStart(channel);

				if (this.IsHandleCreated)
				{
					var tcs = new TaskCompletionSource<bool>();

					this.BeginInvoke(new Action(async () =>
					{
						bool previousMode = _manualLocalOnly.Value;
						_manualLocalOnly.Value = previousMode || localOnly;
						try
						{
							if (inspect == null) await Cam_Inspection(camIndex.ToString(), jobSnap, posSnap, channel);
							else await inspect(camIndex.ToString(), jobSnap, posSnap, channel);
							tcs.SetResult(true);
						}
						catch (Exception exUi)
						{
							tcs.SetException(exUi);
						}
						finally { _manualLocalOnly.Value = previousMode; }
					}));

					await tcs.Task.ConfigureAwait(false);
				}

				if (!localOnly && _communicationService != null)
					_communicationService.NotifyInspectionEnd(channel);
			}
			catch (Exception ex)
			{
                DiagnosticLogStore.Exception("ProcessCommunicationTrigger CH=" + channel, ex);
				LogChangeEventArgs.Set("Log", "Communication trigger error: " + ex.Message, Color.Red);
			}
			finally
			{
				gate.Release();
			}
		}

		private bool StartS7Communication()
		{
			if (!_cameraValidationComplete || _cameraRestartRequested != 0)
				throw new InvalidOperationException("S7 connection is blocked until all active cameras pass the startup capture check.");
			StopS7Communication();

			_communicationService = new S7VisionCommService();
			_communicationService.SetOnline(_runOnline);

			_communicationService.InputChanged += CommunicationService_InputChanged;
			_communicationService.TriggerRequested += CommunicationService_TriggerRequested;
			_communicationService.LogMessage += CommunicationService_LogMessage;

			LogChangeEventArgs.Set("Log", "Create S7 service and start...", Color.Black);

			_communicationService.Start();

			return _communicationService.IsStarted;
		}

		private void CommunicationService_LogMessage(string msg)
		{
			if (string.IsNullOrWhiteSpace(msg))
				return;

			Color color = Color.Black;

			if (msg.IndexOf("重连中", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				color = Color.Orange;
			}
			else if (msg.IndexOf("重连成功", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("started", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				color = Color.Green;
			}
			else if (msg.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
				|| msg.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				color = Color.Red;
			}

			LogChangeEventArgs.Set("Log", msg, color);

			try
			{
				if (this.IsDisposed || !this.IsHandleCreated)
					return;

				Action updateUi = () =>
				{
					RefreshCommunicationState();
				};

				if (this.InvokeRequired)
					this.BeginInvoke(updateUi);
				else
					updateUi();
			}
			catch
			{
			}
		}

		private void CommunicationService_InputChanged(CommunicationChannelInput input)
		{
			ProcessCommunicationInput(input);
		}

		private void CommunicationService_TriggerRequested(int channel)
		{
			ThreadPool.QueueUserWorkItem(async _ =>
			{
				await ProcessCommunicationTrigger(channel).ConfigureAwait(false);
			});
		}

		private static void LoadJobOnly(int channel, int jobId)
		{
			if (Global.Model_JobID[channel] != "Job" + jobId.ToString())
			{
				Load_Job.LoadVPP_ForChannel_AllPos(channel, "Job" + jobId.ToString());
			}
		}

		private void S7SettingForm_S7SettingSaved(object sender, EventArgs e)
		{
			ApplyS7SettingImmediately();
		}

		private void ApplyS7SettingImmediately()
		{
			try
			{
				LogChangeEventArgs.Set("Log", "Apply S7 setting immediately...", Color.Black);

				bool ok = StartS7Communication();

				if (ok)
				{
					RefreshCommunicationState();
					LogChangeEventArgs.Set("Log", "S7 setting applied. Communication restarted.", Color.Green);
				}
				else
				{
					// PLC 当前不在线时，服务仍会保留并持续自动重连。
					button3.Text = "S7 Offline";
					button3.BackColor = Color.Red;
					LogChangeEventArgs.Set("Log", "S7 setting applied. S7重连中...", Color.Orange);
				}
			}
			catch (Exception ex)
			{
				button3.Text = "S7 Error";
				button3.BackColor = Color.Red;
				LogChangeEventArgs.Set("Log", "Apply S7 setting failed: " + ex.Message, Color.Red);
			}
		}
		#endregion

		#region Profinet

		public static void NewTrigger(object sender, string channel)
		{
			ThreadPool.QueueUserWorkItem(async _ =>
			{
				int chan;
				if (!int.TryParse(channel, out chan) || !IsValidChannel(chan))
				{
					LogChangeEventArgs.Set("Log", "Receive Trigger but channel invalid: " + channel, Color.Red);
					return;
				}

				var gate = GetGateOrThrow(chan);
				await gate.WaitAsync().ConfigureAwait(false);
				try
				{
					var main = Application.OpenForms["FormMain"] as FormMain;
					LogChangeEventArgs.Set("Log", "Receive Trigger: " + channel, Color.Green);

					// 用 PLC 已经比对过的那套状态做快照
					string jobSnap = Global.Model_JobID[chan];
					string posSnap = Global.Position_ID[chan];

					string camName = XmlConfigHelper.FindCamByChannel(jobSnap, chan);
					int camIndex;
					if (!TryParseCamIndex(camName, out camIndex))
					{
						LogChangeEventArgs.Set("Log", $"No valid camera for channel {chan}: {camName}", Color.Red);
						return;
					}

					if (main != null && main.IsHandleCreated)
					{
						var tcs = new TaskCompletionSource<bool>();
						main.BeginInvoke(new Action(async () =>
						{
							try
							{
								await main.Cam_Inspection(camIndex.ToString(), jobSnap, posSnap, chan);
								tcs.SetResult(true);
							}
							catch (Exception exUi)
							{
								tcs.SetException(exUi);
							}
						}));
						await tcs.Task.ConfigureAwait(false);
					}

					if (!Global.Manual_Trigger_Lock[chan])
					{
						var frame = SnapshotPlcBuffer();
						lock (_sendLock)
						{
							CC24_Comm.Instance().SendData(frame, 0, chan);
						}

						byte[] trimmedFrame = TrimTrailingZeros(frame);
						string ShowByte = string.Join(",", trimmedFrame);
						LogChangeEventArgs.Set("Log", $"Engine{chan} Send PLC Buffer: {ShowByte}", Color.Green);
					}
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Trigger error: " + ex.Message, Color.Red);
				}
				finally
				{
					gate.Release();
				}
			});
		}

		private static byte[] SnapshotPlcBuffer()
		{
			lock (Global.PlcBufferLock)
			{
				var buf = new byte[Global.Result_Send.Length];
				Buffer.BlockCopy(Global.Result_Send, 0, buf, 0, buf.Length);
				return buf;
			}
		}

		private static bool TryParseCamIndex(string camName, out int index)
		{
			index = -1;
			if (string.IsNullOrEmpty(camName)) return false;
			if (!camName.StartsWith("Cam", StringComparison.OrdinalIgnoreCase)) return false;
			return int.TryParse(camName.Substring(3), out index); // 取 "Cam" 后面的全部数字
		}

		private static byte[] TrimTrailingZeros(byte[] data)
		{
			int lastIndex = data.Length - 1;

			// 从尾部往前找第一个非零字节
			while (lastIndex >= 0 && data[lastIndex] == 0)
				lastIndex--;

			// 全是0？
			if (lastIndex < 0)
				return Array.Empty<byte>(); // 返回空数组

			// 截取有效部分
			return data.Take(lastIndex + 1).ToArray();
		}
		#endregion

		#region Update UI
		private void LogState_StateChanged(string key, Tuple<object, object> value)
		{
			if (this.IsDisposed || !this.IsHandleCreated)
				return;
			if (InvokeRequired)
			{
				try
				{
					BeginInvoke(new Action(() => UpdateLog(key, value)));
				}
				catch (ObjectDisposedException) { }
			}
			else
			{
				UpdateLog(key, value);
			}
		}
		private void UpdateLog(string key, Tuple<object, object> value)
		{
			switch (key)
			{
				case "Log":
					string logText = value.Item1?.ToString();
					Color color = (Color)value.Item2;
					_logPanel.Append(logText, color);
					LogManager.WriteLog(logText, "INFO", false); // Diagnostic copy already captured on publisher thread.
					break;
			}
		}

		private void GlobalState_StateChanged(string key, object value)
		{
			if (this.IsDisposed) return;
			UI_Control.Post(() => UpdateUI(key, value));
		}
		private void UpdateUI(string key, object value)
		{
			switch (key)
			{
				case "Login":
					//toolStripStatusLabel2.Text = value.ToString();
					break;
				case "Guest_Login":
					//toolStripStatusLabel2.Text = "Guest";
					Global.currentUser = "Guest";
					break;
				case "JobID0":
					for (int i = 0; i < Global.CamN_Use; i++)
					{
						if (i >= camViews.Count || !Global.CamGeneral.ContainsKey(Global.Model_JobID[0] + ".Cam" + (i + 1) + ".Pos1")) continue;
						if (Global.CamGeneral[Global.Model_JobID[0] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[0] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainChannel == "0")
						{
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
							camViews[i].SetButton_btntrigger(true);
						}

						else if (Global.CamGeneral[Global.Model_JobID[0] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[0] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondChannel == "0")
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
					}
					break;
				case "JobID1":
					for (int i = 0; i < Global.CamN_Use; i++)
					{
						if (i >= camViews.Count || !Global.CamGeneral.ContainsKey(Global.Model_JobID[1] + ".Cam" + (i + 1) + ".Pos1")) continue;
						if (Global.CamGeneral[Global.Model_JobID[1] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[1] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainChannel == "1")
						{
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
							camViews[i].SetButton_btntrigger(true);
						}
						else if (Global.CamGeneral[Global.Model_JobID[1] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[1] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondChannel == "1")
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
					}
					break;
				case "JobID2":
					for (int i = 0; i < Global.CamN_Use; i++)
					{
						if (i >= camViews.Count || !Global.CamGeneral.ContainsKey(Global.Model_JobID[2] + ".Cam" + (i + 1) + ".Pos1")) continue;
						if (Global.CamGeneral[Global.Model_JobID[2] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[2] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainChannel == "2")
						{
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
							camViews[i].SetButton_btntrigger(true);
						}
						else if (Global.CamGeneral[Global.Model_JobID[2] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[2] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondChannel == "2")
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
					}
					break;
				case "JobID3":
					for (int i = 0; i < Global.CamN_Use; i++)
					{
						if (i >= camViews.Count || !Global.CamGeneral.ContainsKey(Global.Model_JobID[3] + ".Cam" + (i + 1) + ".Pos1")) continue;
						if (Global.CamGeneral[Global.Model_JobID[3] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[3] + ".Cam" + (i + 1).ToString() + ".Pos1"].MainChannel == "3")
						{
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
							camViews[i].SetButton_btntrigger(true);
						}
						else if (Global.CamGeneral[Global.Model_JobID[3] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondUsed == "1" &&
							Global.CamGeneral[Global.Model_JobID[3] + ".Cam" + (i + 1).ToString() + ".Pos1"].SecondChannel == "3")
							camViews[i].SetButton_JobID(Color.Green, value.ToString());
					}
					break;
				case "PosID0":
					var job = Global.Model_JobID[0];
					var posName = "Pos" + Convert.ToString(value ?? "", System.Globalization.CultureInfo.InvariantCulture);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						key = $"{job}.Cam{i + 1}.{posName}";

						// 先安全取配置
						if (!Global.CamGeneral.TryGetValue(key, out CameraGeneralRuntime gen) || gen == null)
						{
							continue;
						}

						bool onMain = gen.MainUsed == "1" && gen.MainChannel == "0";
						bool onSecond = gen.SecondUsed == "1" && gen.SecondChannel == "0";

						if (onMain || onSecond)
							camViews[i].SetButton_PosID(Color.Green, posName);
					}
					break;
				case "PosID1":
					var job1 = Global.Model_JobID[1];
					var posName1 = "Pos" + Convert.ToString(value ?? "", System.Globalization.CultureInfo.InvariantCulture);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						key = $"{job1}.Cam{i + 1}.{posName1}";

						// 先安全取配置
						if (!Global.CamGeneral.TryGetValue(key, out CameraGeneralRuntime gen) || gen == null)
						{
							continue;
						}

						bool onMain = gen.MainUsed == "1" && gen.MainChannel == "1";
						bool onSecond = gen.SecondUsed == "1" && gen.SecondChannel == "1";

						if (onMain || onSecond)
							camViews[i].SetButton_PosID(Color.Green, posName1);
					}
					break;
				case "PosID2":
					var job2 = Global.Model_JobID[2];
					var posName2 = "Pos" + Convert.ToString(value ?? "", System.Globalization.CultureInfo.InvariantCulture);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						key = $"{job2}.Cam{i + 1}.{posName2}";

						// 先安全取配置
						if (!Global.CamGeneral.TryGetValue(key, out CameraGeneralRuntime gen) || gen == null)
						{
							continue;
						}

						bool onMain = gen.MainUsed == "1" && gen.MainChannel == "2";
						bool onSecond = gen.SecondUsed == "1" && gen.SecondChannel == "2";

						if (onMain || onSecond)
							camViews[i].SetButton_PosID(Color.Green, posName2);
					}
					break;
				case "PosID3":
					var job3 = Global.Model_JobID[3];
					var posName3 = "Pos" + Convert.ToString(value ?? "", System.Globalization.CultureInfo.InvariantCulture);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						key = $"{job3}.Cam{i + 1}.{posName3}";

						// 先安全取配置
						if (!Global.CamGeneral.TryGetValue(key, out CameraGeneralRuntime gen) || gen == null)
						{
							continue;
						}

						bool onMain = gen.MainUsed == "1" && gen.MainChannel == "3";
						bool onSecond = gen.SecondUsed == "1" && gen.SecondChannel == "3";

						if (onMain || onSecond)
							camViews[i].SetButton_PosID(Color.Green, posName3);
					}
					break;
				case "Profinet":
					BeginInvoke(new Action(() =>
					{
						RefreshCommunicationState();
						if (value.ToString() == "Online")
							LogChangeEventArgs.Set("Log", "Init CC24 OK!", Color.Green);
					}));


					break;
				case "Cam1Stutas":
					camViews[0].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam2Stutas":
					camViews[1].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam3Stutas":
					camViews[2].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam4Stutas":
					camViews[3].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam5Stutas":
					camViews[4].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam6Stutas":
					camViews[5].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam7Stutas":
					camViews[6].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
				case "Cam8Stutas":
					camViews[7].SetButton_Parameter((value.ToString() == "1") ? Color.Green : Color.Red, (value.ToString() == "1") ? "OK" : "NG");
					break;
			}
		}

		public void UI_Load()
		{
			_stats = new CameraStats[Global.CamN_Use];
			tableLayoutPanel5.ColumnCount = Global.CamN_Use;
			tableLayoutPanel5.RowCount = 1;
			tableLayoutPanel5.RowStyles.Clear();
			tableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
			tableLayoutPanel5.ColumnStyles.Clear();
			for (int i = 0; i < Global.CamN_Use; i++)
			{
				tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Global.CamN_Use));
			}
			for (int i = 0; i < Global.CamN_Use; i++)
			{
				UserControl_ImageShow camView = new UserControl_ImageShow();
				_stats[i] = new CameraStats("Cam" + (i + 1).ToString());

				camView.Name = (i + 1).ToString();
				camView.Dock = DockStyle.Fill;
				camView.SetButton_CamID(Color.Gainsboro, "Cam" + (i + 1).ToString());
				camView.SetButton_JobID(Color.Gainsboro, "Job1");
				camView.Trigger_Manual += CamView_Trigger_Manual;
				camView.Reset_Count += CamView_Reset_Count;
				camView.Replay += CamView_Replay;
				tableLayoutPanel5.Controls.Add(camView, i, 0);
				camViews.Add(camView);
				camViews[i].BindStats(_stats[i]);
			}
			ApplyCameraViewLayout();
			for (int i = 0; i < Global.CamN_Use; i++)
			{
				if (!Global.CamGeneral.ContainsKey("Job1.Cam" + (i + 1) + ".Pos1")) continue;
				if (Global.CamGeneral["Job1." + "Cam" + (i + 1).ToString() + ".Pos1"].MainUsed == "1")
				{
					camViews[i].SetButton_EngineID(Color.Gainsboro, "Engine:" + Global.CamGeneral["Job1." + "Cam" + (i + 1).ToString() + ".Pos1"].MainChannel);
					camViews[i].SetButton_PosID(Color.Gainsboro, "Pos1");
					LogChangeEventArgs.Set("Log", "Cam" + (i + 1).ToString() + " Load Parameter OK", Color.Black);
				}
				else if (Global.CamGeneral["Job1." + "Cam" + (i + 1).ToString() + ".Pos1"].SecondUsed == "1")
				{
					camViews[i].SetButton_EngineID(Color.Gainsboro, "Engine:" + Global.CamGeneral["Job1." + "Cam" + (i + 1).ToString() + ".Pos1"].SecondChannel);
					camViews[i].SetButton_PosID(Color.Gainsboro, "Pos1");
					LogChangeEventArgs.Set("Log", "Cam" + (i + 1).ToString() + " Load Parameter OK", Color.Black);
				}
				else
				{
					camViews[i].SetButton_EngineID(Color.Gray, "Engine:" + Global.CamGeneral["Job1." + "Cam" + (i + 1).ToString() + ".Pos1"].MainChannel);
					camViews[i].SetButton_PosID(Color.Gray, "Pos1");
					LogChangeEventArgs.Set("Log", "Cam" + (i + 1).ToString() + " Not Used", Color.Black);
				}
			}
		}

		void UpdateStatsByResult(string camName, string recentResult)
		{
			if (string.IsNullOrEmpty(camName) || !camName.StartsWith("Cam")) return;
			if (!int.TryParse(camName.Substring(3), out var camNo)) return;
			int idx = camNo - 1;
			if (idx < 0 || idx >= _stats.Length) return;

			bool isOk = (recentResult == "1");
			_stats[idx].Increment(isOk);
		}

		/// <summary>
		/// change the name of software by config
		private void ApplySoftwareTitle()
		{
			string title = (ImageRecord.Current.Program_Name ?? "").Trim();

			if (string.IsNullOrWhiteSpace(title))
				title = "Aron Vision System";

			this.Text = title + " - " + BuildInfo.SoftwareVersion;
		}

		#endregion

		#region Vision
		private bool Trigger(CogAcqFifoTool camera, string CamN, string jobId, string posName)
		{
			try
			{
				if (camera == null)
				{
					Trigger_Failed(CamN, "CogAcqFifoTool is null");
					return false;
				}

				camViews[int.Parse(CamN.Substring(CamN.Length - 1)) - 1]
					.SetButton_CamStatus(Color.Green, CamN);

				LogCameraSerial(CamN, camera, "trigger");
				string mismatch = CameraSerialBinding.Mismatch(jobId, CamN, posName, camera);
				if (mismatch != null)
				{
					Trigger_Failed(CamN, mismatch);
					return false;
				}
				camera.Run();
				mismatch = CameraSerialBinding.Mismatch(jobId, CamN, posName, camera);
				if (mismatch != null)
				{
					Trigger_Failed(CamN, mismatch);
					return false;
				}

				if (camera.RunStatus == null ||
					camera.RunStatus.Result.ToString() != "Accept")
				{
					string msg = camera.RunStatus == null ? "RunStatus is null" : camera.RunStatus.Message;
					Trigger_Failed(CamN, msg);
					return false;
				}

				if (camera.OutputImage == null)
				{
					Trigger_Failed(CamN, "OutputImage is null");
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				DiagnosticLogStore.Exception("VisionPro acquisition " + CamN + " " + jobId + " " + posName, ex);
				Trigger_Failed(CamN, ex.Message);
				return false;
			}
		}

		//for Auto and Trigger Manual
		public async Task Cam_Inspection(string CamN, string JobID, string PosID, int Channel)
		{
            using (DiagnosticLogStore.BeginOperation("Inspection CH=" + Channel + " Job=" + JobID + " Pos=" + PosID))
            {
			PosID = NormalizeInspectionPosition(PosID);

			string ResultTotal = "2";
			Global.ResultTotal_Cam[int.Parse(CamN) - 1] = ResultTotal;

			try
			{

				LogChangeEventArgs.Set("Log", "Start Second", Color.Black);
				await RunSecondGroup(JobID, PosID, Channel);
				LogChangeEventArgs.Set("Log", "Start main", Color.Black);
				await RunMain(JobID, PosID, Channel);

			}
			catch (Exception e)
			{
				LogChangeEventArgs.Set("Log", "Trigger Failed:" + e.ToString(), Color.Red);
			}
            }
		}

		private void ImageShow(string CamN, CogAcqFifoTool Acq, CogToolBlock VPP)
		{
			this.BeginInvoke(new Action(() =>
			{
				try
				{
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.Record = VPP.CreateLastRunRecord().SubRecords["CogIPOneImageTool1.OutputImage"];
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.AutoFit = true;
				}
				catch (Exception f)
				{
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.Image = Acq.OutputImage;
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.AutoFit = true;
					LogChangeEventArgs.Set("Log", "Cam" + CamN + " Image Show Error:" + f.ToString(), Color.Red);
				}
			}));
		}

		public void ReplayImage(string CamN, string PosID, string JobID, int Channel)
		{
			Load_Job.VppCache.TryGetTb(JobID, Channel, "Cam" + CamN, PosID, out var VPP);

			string Image_record;

			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Filter = "VisionPro Images|*.cdb;*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|Cognex Image Database (*.cdb)|*.cdb|All Files (*.*)|*.*";
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					string selectedPath = dialog.FileName;
					Image_record = selectedPath;
					// CDB is a native Cognex image container, not a System.Drawing bitmap.
					// Preserve the native ICogImage, including 3D/range image information.
					try
					{
						using (var imageFileTool = new CogImageFileTool())
						{
							imageFileTool.Operator.Open(Image_record, CogImageFileModeConstants.Read);
							imageFileTool.Run();
							if (imageFileTool.OutputImage == null || imageFileTool.RunStatus.Result != CogToolResultConstants.Accept)
								throw new InvalidOperationException("Image read failed: " + imageFileTool.RunStatus.Message);
							Global.image_Replay = (ICogImage)CogSerializer.DeepCopyObject(imageFileTool.OutputImage);
						}
					}
					catch (Exception ex)
					{
						LogChangeEventArgs.Set("Log", "Replay image load failed: " + Image_record + " | " + ex.Message, Color.Red);
						MessageBox.Show(ex.Message, "Replay", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
					try
					{
						string Recent_Reult = "2";
						CamN = "Cam" + CamN;
						// Replay must publish the same original image as live acquisition.
						// The algorithm editor owns a separate draft and reads this cache,
						// rather than the runtime tool's previous LastRun record.
						string replaySerial = CurrentInspectionSerial(Channel);
						Load_Job.VppCache.RememberLatestInspectionImage(JobID, CamN, PosID, Global.image_Replay, replaySerial, Global.Replay_Send_Data);

						string genKey = JobID + "." + CamN + "." + PosID;
						CameraGeneralRuntime gen;
						Global.CamGeneral.TryGetValue(genKey, out gen);

						var dict = XmlConfigHelper.GetParametersFor(Global.CurrentConfig, JobID, CamN, PosID);
						lock (_camLocks.GetOrAdd(CamN, _ => new object()))
						{
							VppHelper.ApplyParametersToToolBlock(VPP, JobID, CamN, PosID, dict);

							VPP.Inputs[0].Value = Global.image_Replay;
							VPP.Inputs[1].Value = string.IsNullOrEmpty(Global.Replay_Send_Data) ? null : Global.Replay_Send_Data;
							InspectionVppContract.ApplyContext(VPP, JobID, PosID, replaySerial);
							VPP.Run();
							Recent_Reult = VPP.Outputs["Result_Cam"].Value.ToString();
							string show = "";
							try
							{
								if (VPP.Outputs.Contains("resultSend"))
								{
									show = (VPP.Outputs["resultSend"].Value).ToString();
									Global.Replay_Send_Data = "[" + CamN + "]" + show;
									LogChangeEventArgs.Set("Log", "Replay Complete:" + $"[{CamN}]" + "Result:" + show, Color.Black);
								}
								else
									LogChangeEventArgs.Set("Log", "Replay Error: " + $"[{CamN}]" + "Lack of Vpp output:resultSend", Color.Red);
							}
							catch (Exception ex)
							{
								LogChangeEventArgs.Set("Log", "Replay Error:" + ex, Color.Black);
							}
						}
						try
						{
							this.BeginInvoke(new Action(() =>
							{
								DataChangedEventArgs.Set(CamN + "Stutas", Recent_Reult);
								ImageShowReplay(CamN.Substring(3, 1), VPP);
							}));
						}
						catch { }
					}
					catch (Exception f)
					{
						LogChangeEventArgs.Set("Log", "Replay eror:" + f.ToString(), Color.Black);
					}
				}
			}
		}

		public void Trigger_Failed(string CamN, string Message)
		{
			if (_manualLocalOnly.Value)
			{
				LogChangeEventArgs.Set("Log", "Manual local-only capture failed: " + CamN + ": " + Message + ". PLC state/recovery unchanged.", Color.Red);
				return;
			}
			try
			{
				camViews[int.Parse(CamN.Substring(CamN.Length - 1)) - 1]
					.SetButton_CamStatus(Color.Red, CamN + ":Offline");
			}
			catch { }

		LogChangeEventArgs.Set(
				"Log",
				"Camera " + CamN + " trigger failed: " + Message + ". Automatic recovery scheduled.",
				Color.Red);
		RequestCameraRestart(CamN + ": " + Message);
		}

        private static S7InspectionOutputs SnapshotS7Outputs(CogToolBlock tool, string job, string cam, string pos)
        {
            var snapshot = new S7InspectionOutputs { Job = job, Camera = cam, Position = pos,
                Succeeded = tool.RunStatus.Result == CogToolResultConstants.Accept };
            for (int i = 0; i < tool.Outputs.Count; i++)
            {
                object value = tool.Outputs[i].Value;
                if (value == null || value is string || value is bool || value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal)
                    snapshot.Values[tool.Outputs[i].Name] = value;
            }
            return snapshot;
        }

		private class CommunicationMainResultSnapshot
		{
			public int Channel { get; set; }
			public string CamName { get; set; }
			public short ResultCode { get; set; }
			public float X { get; set; }
			public float Y { get; set; }
			public float A { get; set; }
            public S7InspectionOutputs S7Outputs { get; set; }

			public CommunicationMainResultSnapshot()
			{
				CamName = string.Empty;
				ResultCode = 2;
			}
		}

		//运行附属相机
		private async Task RunSecondGroup(string jobId, string posName, int currentChannel)
		{
			if (Global.CurrentConfig == null || Global.CurrentConfig.Models == null) return;

			var job = Global.CurrentConfig.Models
		.FirstOrDefault(m => string.Equals(m.Name, jobId, StringComparison.OrdinalIgnoreCase));
			if (job == null || job.Cameras == null) return;

			var results = new List<CamRunResult>();
			object lockObj = new object();
			var tasks = new List<System.Threading.Tasks.Task>();

			for (int i = 0; i < job.Cameras.Count; i++)
			{
				var cam = job.Cameras[i];
				if (cam == null || cam.Positions == null) continue;

				var pos = cam.Positions.FirstOrDefault(p => p.Name == posName);
				if (pos == null || pos.General == null) continue;

				int secondUsed, secondChannel;
				int.TryParse(pos.General.SecondUsed, out secondUsed);
				int.TryParse(pos.General.SecondChannel, out secondChannel);

				if (secondUsed == 1 && secondChannel == currentChannel)
				{
					string camName = "Cam" + (i + 1);
					Load_Job.VppCache.TryGetAcq(jobId, currentChannel, camName, posName, out var acq);
					Load_Job.VppCache.TryGetTb(jobId, currentChannel, camName, posName, out var vpp);

					var t = System.Threading.Tasks.Task.Run(() =>
					{
						CaptureAndInspect(jobId, camName, posName, currentChannel, acq, vpp, "",
							r =>
							{
								lock (lockObj)
								{
									results.Add(r);
								}
							},
							null);
					});

					tasks.Add(t);
					DataChangedEventArgs.Set("PosID" + currentChannel, InspectionPositionNumber(posName));
				}
			}

			// 同步等待所有任务完成
			await Task.WhenAll(tasks);
			// 全部完成后汇总
			var sb = new System.Text.StringBuilder();
			foreach (var r in results)
			{
				sb.AppendLine(r.ToString());
			}
			string allResultString = sb.ToString();
			Global.Result_data_send[currentChannel] = allResultString;
			LogChangeEventArgs.Set("Log", "second complete", Color.Black);

			UseAllCamResults(allResultString);
		}

		//运行主相机
		private async Task RunMain(string jobId, string posName, int currentChannel)
		{
			if (Global.CurrentConfig == null || Global.CurrentConfig.Models == null) return;

			var job = Global.CurrentConfig.Models
		.FirstOrDefault(m => string.Equals(m.Name, jobId, StringComparison.OrdinalIgnoreCase));
			if (job == null || job.Cameras == null) return;

			var results = new List<CamRunResult>();
			var communicationResults = new List<CommunicationMainResultSnapshot>();
			object lockObj = new object();
			var tasks = new List<System.Threading.Tasks.Task>();

			for (int i = 0; i < job.Cameras.Count; i++)
			{
				var cam = job.Cameras[i];
				if (cam == null || cam.Positions == null) continue;

				var pos = cam.Positions.FirstOrDefault(p => p.Name == posName);
				if (pos == null || pos.General == null) continue;

				int mainUsed, mainChannel;
				int.TryParse(pos.General.MainUsed, out mainUsed);
				int.TryParse(pos.General.MainChannel, out mainChannel);

				if (mainUsed == 1 && mainChannel == currentChannel)
				{
					string camName = "Cam" + (i + 1);
					Load_Job.VppCache.TryGetAcq(jobId, currentChannel, camName, posName, out var acq);
					Load_Job.VppCache.TryGetTb(jobId, currentChannel, camName, posName, out var vpp);

					// 开后台任务
					var t = System.Threading.Tasks.Task.Run(() =>
					{
						CaptureAndInspect(jobId, camName, posName, currentChannel, acq, vpp, Global.Result_data_send[currentChannel],
							r =>
							{
								lock (lockObj)
								{
									results.Add(r);
								}
							},
							s7 =>
							{
								if (s7 == null)
									return;

								lock (lockObj)
								{
									communicationResults.Add(s7);
								}
							});
					});

					tasks.Add(t);
					DataChangedEventArgs.Set("PosID" + currentChannel, InspectionPositionNumber(posName));
				}
			}

			// 同步等待所有主相机任务完成
			await Task.WhenAll(tasks);

			// 全部完成后汇总
			var sb = new System.Text.StringBuilder();
			foreach (var r in results)
			{
				sb.AppendLine(r.ToString());
			}
			string allResultString = sb.ToString();
			LogChangeEventArgs.Set("Log", "main complete", Color.Black);

			UseAllCamResults(allResultString);

			// 注意：S7 结果必须在所有主相机完成后再发送，避免 PLC 提前看到 Result Valid。
			SendCommunicationResultAfterMainComplete(currentChannel, communicationResults);
		}


		private void SendCommunicationResultAfterMainComplete(int channel, List<CommunicationMainResultSnapshot> communicationResults)
		{
			if (_manualLocalOnly.Value || _communicationService == null || _cameraRestartRequested != 0)
				return;

			if (communicationResults == null || communicationResults.Count == 0)
			{
				LogChangeEventArgs.Set("Log", "Communication main result skipped. CH=" + channel.ToString() + ", no main result.", Color.Red);
				return;
			}

			CommunicationMainResultSnapshot selected = communicationResults[0];

			// 如果多主相机共用同一通道，任一 NG 则通道结果为 NG。
			short summaryCode = 1;
			for (int i = 0; i < communicationResults.Count; i++)
			{
				if (communicationResults[i] == null)
					continue;

				if (communicationResults[i].ResultCode != 1)
				{
					summaryCode = 2;
					selected = communicationResults[i];
					break;
				}
			}

			if (summaryCode == 1)
			{
				for (int i = 0; i < communicationResults.Count; i++)
				{
					if (communicationResults[i] != null)
					{
						selected = communicationResults[i];
						break;
					}
				}
			}

            DiagnosticLogStore.Stage("Result CH=" + channel, "WriteInspectionResult begin; source=" + selected.CamName + "; result=" + summaryCode);
			var s7Service = _communicationService as S7VisionCommService;
            if (s7Service != null)
            {
                var sources = communicationResults.Where(r => r != null && r.S7Outputs != null).OrderBy(r => ReferenceEquals(r, selected) ? 0 : 1).Select(r => r.S7Outputs).ToList();
                if (!s7Service.WriteInspectionResult(channel, selected.X, selected.Y, selected.A, summaryCode,
                    sources.FirstOrDefault()?.Job, sources.FirstOrDefault()?.Position, sources))
                {
                    LogChangeEventArgs.Set("Log", "S7 result send failed. CH=" + channel + "; Result Valid was not asserted. Check S7 output bindings and communication log.", Color.Red);
                    return;
                }
            }
            else _communicationService.WriteInspectionResult(channel, selected.X, selected.Y, selected.A, summaryCode);
            DiagnosticLogStore.Stage("Result CH=" + channel, "WriteInspectionResult returned (not PLC consumption confirmation)");

			LogChangeEventArgs.Set("Log",
				"Communication result sent after all main cameras complete. CH=" + channel.ToString()
				+ ", Result=" + summaryCode.ToString()
				+ ", Source=" + (selected.CamName ?? string.Empty),
				Color.Green);
		}

		//相机拍照+检测
		private void CaptureAndInspect(string jobId, string camName, string posName, int channel,
            CogAcqFifoTool camera, CogToolBlock tool, string inputData, Action<CamRunResult> onFinished,
            Action<CommunicationMainResultSnapshot> onCommunicationMainResult)
        {
            using (DiagnosticLogStore.BeginOperation("Camera CH=" + channel + " Job=" + jobId + " Cam=" + camName + " Pos=" + posName))
            {
                try { CaptureAndInspectCore(jobId, camName, posName, channel, camera, tool, inputData, onFinished, onCommunicationMainResult); }
                catch (Exception ex) { DiagnosticLogStore.Exception("CaptureAndInspect", ex); PublishPluginFailure(jobId, camName, posName, channel); throw; }
            }
        }

		private static string CurrentInspectionSerial(int channel)
		{
			return Global.PartCode != null && channel >= 0 && channel < Global.PartCode.Length && !string.IsNullOrWhiteSpace(Global.PartCode[channel]) ? Global.PartCode[channel] : null;
		}

		private void CaptureAndInspectCore(string jobId,
	string camName,
	string posName,
	int channel,
	CogAcqFifoTool camera,
	CogToolBlock tool,
	string inputData,
	Action<CamRunResult> onFinished,
	Action<CommunicationMainResultSnapshot> onCommunicationMainResult)
		{
			ICogImage image;
			string inspectionSerial = CurrentInspectionSerial(channel);
            var pluginCapturedAt = DateTime.Now;
            Aron.PluginContracts.InspectionSnapshot pluginSnapshot = null;
            string diagnosticContext = "CH=" + channel + " Job=" + jobId + " Cam=" + camName + " Pos=" + posName;
            DiagnosticLogStore.Stage(diagnosticContext, "Acquire begin");
			if (!TryAcquireInspectionImage(camera, camName, jobId, posName, out image))
			{
                DiagnosticLogStore.Stage(diagnosticContext, "Acquire returned false");
                PublishPluginFailure(jobId, camName, posName, channel);
				if (onFinished != null)
				{
					onFinished(new CamRunResult
					{
						CamName = camName,
						ResultTotal = camName + ":CameraOffline"
					});
				}

				if (onCommunicationMainResult != null)
				{
					onCommunicationMainResult(new CommunicationMainResultSnapshot
					{
						Channel = channel,
						CamName = camName,
						ResultCode = 2,
						X = 0,
						Y = 0,
						A = 0
					});
				}

				return;
			}

			// Keep the original Cognex/LMI image, not the display bitmap.  The
			// algorithm editor uses this cache only for local parameter tuning.
			// It is never persisted to Inspection.vpp or sent to PLC logic.
			Load_Job.VppCache.RememberLatestInspectionImage(jobId, camName, posName, image, inspectionSerial, inputData);

			string result_send = "";
            DiagnosticLogStore.Stage(diagnosticContext, "Acquire completed; preparing parameters");
			string Recent_Reult = "2";

			// PLC 采图通知（主相机才报）
			string genKey = jobId + "." + camName + "." + posName;
			CameraGeneralRuntime gen;
			Global.CamGeneral.TryGetValue(genKey, out gen);
			int mainCh = -1;
			if (gen != null && gen.MainUsed == "1") int.TryParse(gen.MainChannel, out mainCh);


			// 2) 设置参数 + 同步 Run（确保 Outputs 已更新）
			var dict = XmlConfigHelper.GetParametersFor(Global.CurrentConfig, jobId, camName, posName);
			lock (_camLocks[camName])
			{
				VppHelper.ApplyParametersToToolBlock(tool, jobId, camName, posName, dict);

				tool.Inputs[0].Value = image;
				tool.Inputs[1].Value = inputData;
				InspectionVppContract.ApplyContext(tool, jobId, posName, inspectionSerial);
                DiagnosticLogStore.Stage(diagnosticContext, "ToolBlock.Run begin");
				tool.Run();
                DiagnosticLogStore.Stage(diagnosticContext, "ToolBlock.Run returned; reading outputs");
				Recent_Reult = tool.Outputs.Contains("Result_Cam")
					? (tool.Outputs["Result_Cam"].Value ?? "").ToString()
					: "3";

				if (onCommunicationMainResult != null && mainCh >= 0)
				{
					short resultCode = 2;

					int tmpCode;
					if (int.TryParse(Recent_Reult, out tmpCode))
						resultCode = (short)tmpCode;

					// S7 发送值只读取 VPP 独立输出：
					// Result_X / Result_Y / Result_A
					// 这三个输出虽然是 System.String，但 ReadFloatToolOutput 会自动解析成 float。
					float resultX = ReadFloatToolOutput(tool, "Result_X");
					float resultY = ReadFloatToolOutput(tool, "Result_Y");
					float resultA = ReadFloatToolOutput(tool, "Result_A");

					onCommunicationMainResult(new CommunicationMainResultSnapshot
					{
						Channel = mainCh,
						CamName = camName,
						ResultCode = resultCode,
						X = resultX,
						Y = resultY,
						A = resultA,
                        S7Outputs = _communicationService is S7VisionCommService ? SnapshotS7Outputs(tool, jobId, camName, posName) : null
					});

					LogChangeEventArgs.Set("Log",
						"Communication main result cached. CH=" + mainCh.ToString()
						+ ", " + camName
						+ ", Result=" + resultCode.ToString()
						+ ", X=" + resultX.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
						+ ", Y=" + resultY.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
						+ ", A=" + resultA.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture),
						Color.Black);
				}
				UpdateStatsByResult(camName, Recent_Reult);
                DiagnosticLogStore.Stage(diagnosticContext, "Outputs read; Result_Cam=" + Recent_Reult);
                pluginSnapshot = CreatePluginSnapshot(tool, jobId, camName, posName, channel, Recent_Reult, pluginCapturedAt);
				LogChangeEventArgs.Set("Log", camName + ":Inspection vpp Complete", Color.Black);
			}

			try
			{
				this.BeginInvoke(new Action(() =>
				{
					DataChangedEventArgs.Set(camName + "Stutas", Recent_Reult);
					ImageShow(camName.Substring(3, 1), camera, tool);
				}));

				// UI 外部做非 UI 的异步工作（更清晰，也更安全）
				// Save Image to Local
				if (!_manualLocalOnly.Value && !Global.Manual_Trigger_Lock[channel] && ImageRecord.Current.EnableSave)
				{
					try
					{
						bool okFlag = (Recent_Reult != "2");
						string sn = Global.PartCode[channel];

						string root = string.IsNullOrWhiteSpace(ImageRecord.Current.Root)
							? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images")
							: ImageRecord.Current.Root.TrimEnd('\\', '/');

						string date = DateTime.Now.ToString("yyyyMMdd");
						string okNg = okFlag ? "OK" : "NG";
						string fileNameNoExt = $"{camName + "_" + posName}_{DateTime.Now:HHmmss_fff}_{(sn ?? "").Trim()}_{okNg}";

						string formatMode = (ImageRecord.Current.FormatMode ?? "BMP").ToUpperInvariant();
						string ext = (formatMode == "PNG") ? ".png" : ".bmp";

						string dir = System.IO.Path.Combine(root, jobId, camName, posName, date, okNg);
						string fullPath = System.IO.Path.Combine(dir, fileNameNoExt + ext);
                        if (pluginSnapshot != null) pluginSnapshot = pluginSnapshot.WithImagePath(fullPath);

						var bmp = VpImageUtil.ToManagedBitmap(image);

						IoWorkers.EnqueueImage(new IoWorkers.SaveImageTask
						{
							JobId = jobId,
							CamName = camName,
							PosName = posName,
							Serial = sn,
							OkFlag = okFlag,
							FullPath = fullPath,
							Ext = ext,
							Bmp = bmp
						});
					}
					catch (Exception ex)
					{
						LogChangeEventArgs.Set("Log", "Queue image failed: " + ex.Message, System.Drawing.Color.Red);
					}
				}
			}
			catch { }
			// 4) 读取配置并把“本通道”的输出写入 240B 缓冲（仅主相机）
			if (!_manualLocalOnly.Value && !Global.Manual_Trigger_Lock[channel])
			{
				CommunicationProtocol activeProtocol = CommunicationConfigHelper.Load().Protocol;
				if (activeProtocol == CommunicationProtocol.Profinet && mainCh >= 0)
				{
					Profinet.ProfinetDataMapper.ApplyVppOutputs(mainCh, tool);
				}
				else if (_vppOutCfg != null && mainCh >= 0)
				{
					var allItems = XmlConfigHelper.GetVppItemsForCam(_vppOutCfg, jobId, camName);
					var itemsForThisChannel = allItems.FindAll(it => it != null && it.Channel == mainCh);

					var errs = XmlConfigHelper.ValidateVppOutputs(tool, itemsForThisChannel);
					if (errs.Count > 0)
					{
						LogChangeEventArgs.Set("Log",
							$"[{camName}-{posName}] VPP lack of output: {string.Join(", ", errs)}",
							Color.Red);
					}
					else
					{
						// bigEndian 按你的 PLC 要求设置
						XmlConfigHelper.ApplyChannelOutputsToPlcBuffer(mainCh, tool, allItems, /*bigEndian*/ true);

						if (mainCh >= 0)
						{
							try
							{
								var allItemsDb = XmlConfigHelper.GetVppItemsForCam(_vppOutCfg, jobId, camName);
								var itemsForCh = allItemsDb.FindAll(x => x != null && x.Channel == mainCh);

								var tuples = BuildOutputsForDb(tool, itemsForCh);
								var outputs = tuples.Select(t =>
									new ValueTuple<string, string, string, int, int>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5));

								int resultCode = ReadResultCode(tool);
								string rawMsg = BuildRawMessage(tool, itemsForCh);

								string serial = "";
								try { serial = (Global.PartCode != null && mainCh < Global.PartCode.Length) ? (Global.PartCode[mainCh] ?? "") : ""; }
								catch { }

								System.Threading.ThreadPool.QueueUserWorkItem(_ =>
								{
									try
									{
										long id = DataBase.InsertInspectionRecord(
											serial, jobId, mainCh, camName, posName, resultCode, rawMsg, outputs
										);
										LogChangeEventArgs.Set("Log", $"Write Database OK", Color.Black);
									}
									catch (Exception exDb)
									{
										LogChangeEventArgs.Set("Log", "Write Database failed: " + exDb.Message, Color.Red);
									}
								});
							}
							catch (Exception ex)
							{
								LogChangeEventArgs.Set("Log", "Prepare Database values failed: " + ex.Message, Color.Red);
							}
						}
					}
				}
			}


			// 5) 日志
			try
			{
				var show = tool.Outputs.Contains("resultSend")
					? (tool.Outputs["resultSend"].Value ?? "").ToString()
					: "";

				if (!string.IsNullOrWhiteSpace(show))
					result_send = show;
			}
			catch { }

			// 6) 回调
			if (onFinished != null)
			{
				onFinished(new CamRunResult
				{
					CamName = camName,
					ResultTotal = result_send // Summary Display and tarinsfor from second to main
				});
			}
            if (pluginSnapshot != null) _plugins?.Publish(pluginSnapshot);
		}


		private void UseAllCamResults(string allResultString)
		{
			allResultString = (allResultString ?? "").TrimEnd('\r', '\n');

			LogChangeEventArgs.Set("Log",
				"Slave camera Inspection Complete," + allResultString,
				Color.Black);
		}


		#endregion

		#region XML
		private void ReloadConfigAndUI()
		{
			Global.CurrentConfig = XmlConfigHelper.Load(Global.ParameterCogfig);
		}
		#endregion

		#region Image Cleanup
		private void StartImageCleanupScheduler()
		{
			try
			{
				// 先释放旧定时器，避免重复创建
				if (_imageCleanupTimer != null)
				{
					_imageCleanupTimer.Dispose();
					_imageCleanupTimer = null;
				}

				// 如果设置为 <= 0，则不启用自动清理
				if (ImageRecord.Current.GetRetentionMinutes() <= 0)
				{
					if (ImageRecord.Current.ShowImageCleanupLog)
						LogChangeEventArgs.Set("Log", "Image auto cleanup disabled", Color.Black);
					return;
				}

				// 启动及设置变更时先清理一次
				ThreadPool.QueueUserWorkItem(_ => RunImageCleanupSafe());

				// 每分钟检查一次过期图片
				TimeSpan interval = TimeSpan.FromMinutes(1);
				_imageCleanupTimer = new System.Threading.Timer(
					_ => RunImageCleanupSafe(),
					null,
					interval,
					interval);

				if (ImageRecord.Current.ShowImageCleanupLog)
					LogChangeEventArgs.Set("Log",
						$"Image cleanup scheduler started, interval=1 minute, root={ImageRecord.Current.Root}, retention={ImageRecord.Current.GetRetentionMinutes()} minutes",
						Color.Green);
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Start image cleanup scheduler failed: " + ex.Message, Color.Red);
			}
		}

		private void RunImageCleanupSafe()
		{
			// 防止上一次还没执行完，又进来一次
			if (System.Threading.Interlocked.Exchange(ref _imageCleanupRunning, 1) == 1)
				return;

			try
			{
				// 重新读取一次最新设置，防止用户在界面改了参数后程序还用旧值
				ImageRecord.LoadSettings();
				Global.Save_Image_Root = ImageRecord.Current.Root;

				if (ImageRecord.Current.GetRetentionMinutes() > 0)
				{
					int deleted = ImageRecord.CleanupOldMinutes();
					if (ImageRecord.Current.ShowImageCleanupLog)
						LogChangeEventArgs.Set("Log",
						$"Image cleanup checked, root={ImageRecord.Current.Root}, retention={ImageRecord.Current.GetRetentionMinutes()} minutes, deleted={deleted}",
						Color.Green);
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Image cleanup failed: " + ex.Message, Color.Red);
			}
			finally
			{
				System.Threading.Interlocked.Exchange(ref _imageCleanupRunning, 0);
			}
		}

		#endregion

		#region Linense Monitor
		private void StartLicenseMonitor()
		{
			try
			{
				if (_licenseMonitor != null)
				{
					_licenseMonitor.StatusChanged -= LicenseMonitor_StatusChanged;
					_licenseMonitor.Dispose();
					_licenseMonitor = null;
				}

				// 重新读取 Setting 里保存的 License ID
				ImageRecord.LoadSettings();

				string licenseId = (ImageRecord.Current.Lincese_ID ?? "").Trim();

				if (string.IsNullOrWhiteSpace(licenseId))
				{
					_licenseOk = false;
					LogChangeEventArgs.Set("Log", "Cognex License monitor not started: License ID is empty", Color.Red);
					return;
				}

				// 建议 10 秒检查一次
				_licenseMonitor = new LicenseMonitor(licenseId, 10000);
				_licenseMonitor.StatusChanged += LicenseMonitor_StatusChanged;
				_licenseMonitor.Start();

				LogChangeEventArgs.Set("Log", "Cognex License monitor started, License ID: " + licenseId, Color.Green);
			}
			catch (Exception ex)
			{
				_licenseOk = false;
				LogChangeEventArgs.Set("Log", "Start Cognex License monitor failed: " + ex.Message, Color.Red);
			}
		}

		private void LicenseMonitor_StatusChanged(object sender, LicenseStatusChangedEventArgs e)
		{
			if (e == null || e.Result == null) return;

			LicenseCheckResult r = e.Result;

			if (r.IsOK)
			{
				_licenseOk = true;

				if (this.IsDisposed || !this.IsHandleCreated)
					return;

				try
				{
					this.BeginInvoke(new Action(() =>
					{
						LogChangeEventArgs.Set("Log", "Cognex License OK: " + r.Message, Color.Green);
					}));
				}
				catch
				{
				}

				return;
			}

			// ===============================
			// License NG
			// ===============================
			_licenseOk = false;

			// 掉线后立即关闭 CC24
			CloseCc24ByLicenseLost(r.Message);

			if (this.IsDisposed || !this.IsHandleCreated)
				return;

			try
			{
				this.BeginInvoke(new Action(() =>
				{
					LogChangeEventArgs.Set("Log", "Cognex License NG: " + r.Message, Color.Red);

					// 如果你有状态栏，可以打开
					// toolStripStatusLabel_License.Text = "License: NG";
					// toolStripStatusLabel_License.ForeColor = Color.Red;
				}));
			}
			catch
			{
			}
		}

		private void CloseCc24ByLicenseLost(string reason)
		{
			// 防止重复执行 Close
			if (System.Threading.Interlocked.Exchange(ref _licenseCloseHandled, 1) == 1)
				return;

			try
			{
				LogChangeEventArgs.Set("Log", "Cognex License lost, close CC24. Reason: " + reason, Color.Red);

				lock (_sendLock)
				{
					try
					{
						CC24_Comm.Instance().Close();
					}
					catch (Exception ex)
					{
						LogChangeEventArgs.Set("Log", "Close CC24 failed after License lost: " + ex.Message, Color.Red);
					}
				}

				try
				{
					if (!this.IsDisposed && this.IsHandleCreated)
					{
						this.BeginInvoke(new Action(() =>
						{
							button3.Text = "Offline";
							button3.BackColor = Color.Red;
						}));
					}
				}
				catch
				{
				}

				LogChangeEventArgs.Set("Log", "CC24 closed because Cognex License is NG. Software restart required.", Color.Red);

				// 加密狗掉线后弹窗提示
				ShowLicenseLostMessageBox(reason);
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "License lost handler error: " + ex.Message, Color.Red);
			}
		}

		//LinceseMessageBox
		private void ShowLicenseLostMessageBox(string reason)
		{
			// 防止重复弹窗
			if (System.Threading.Interlocked.Exchange(ref _licensePopupShown, 1) == 1)
				return;

			if (this.IsDisposed || !this.IsHandleCreated)
				return;

			try
			{
				this.BeginInvoke(new Action(() =>
				{
					try
					{
						MessageBox.Show(
							this,
							"Cognex License / CodeMeter dongle has been disconnected!\r\n\r\n" +
							"The software has stopped CC24 communication with the PLC.\r\n" +
							"Please check the dongle, CodeMeter service, or USB connection.\r\n\r\n" +
							"Please restart the software after the issue is resolved.",
							"License Lost - Restart Required",
							MessageBoxButtons.OK,
							MessageBoxIcon.Error);
					}
					catch
					{
					}
				}));
			}
			catch
			{
			}
		}
		#endregion

		private static bool TryGetToolOutput(CogToolBlock tb, string port, out object value)
		{
			value = null;
			try
			{
				if (tb != null && !string.IsNullOrEmpty(port) && tb.Outputs.Contains(port))
				{
					value = tb.Outputs[port].Value;
					return true;
				}
			}
			catch { }
			return false;
		}

		// 把某通道要写 PLC 的 OutputItem 采集成插库需要的 tuple 列表
		private static List<Tuple<string, string, string, int, int>> BuildOutputsForDb(
			CogToolBlock tb, List<OutputItem> itemsForChannel)
		{
			var list = new List<Tuple<string, string, string, int, int>>();
			if (tb == null || itemsForChannel == null) return list;

			foreach (var it in itemsForChannel)
			{
				object raw;
				string text = "";
				if (TryGetToolOutput(tb, it.Source, out raw) && raw != null)
				{
					// 用 InvariantCulture 转文本，避免小数点逗号因地区而变
					text = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture);
				}
				list.Add(Tuple.Create(it.Name ?? "", it.Type ?? "string", text, it.Start, it.Length));
			}
			return list;
		}

		// 从 ToolBlock 里读一个“结果码”(没有就给 2-FAIL 或你习惯的默认)
		private static int ReadResultCode(CogToolBlock tb)
		{
			object v;
			if (TryGetToolOutput(tb, "Result", out v) || TryGetToolOutput(tb, "ResultCode", out v))
			{
				int code;
				if (v != null && int.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture), out code))
					return code;
			}
			return 2; // 默认 NG/失败
		}

		// 可选：把关键输出拼成一条原始信息，便于审计
		private static string BuildRawMessage(CogToolBlock tb, IEnumerable<OutputItem> itemsForChannel)
		{
			var parts = new List<string>();
			foreach (var it in itemsForChannel)
			{
				object v;
				if (TryGetToolOutput(tb, it.Source, out v) && v != null)
					parts.Add(it.Name + "=" + Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
				else
					parts.Add(it.Name + "=<NA>");
			}
			return string.Join("; ", parts);
		}

		private void ImageShowReplay(string CamN, CogToolBlock VPP)
		{
			this.BeginInvoke(new Action(() =>
			{
				try
				{
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.Record = VPP.CreateLastRunRecord().SubRecords["CogIPOneImageTool1.OutputImage"];
					camViews[Convert.ToInt16(CamN) - 1].CogDisplay.AutoFit = true;
				}
				catch (Exception f)
				{
					LogChangeEventArgs.Set("Log", "Cam" + CamN + " Image Show Error:" + f.ToString(), Color.Red);
				}
			}));
		}

		public sealed class ValidateResult
		{
			public bool Ok { get; set; }
			public string JobName { get; set; }
			public string CamName { get; set; }
			public string PosName { get; set; }
			public string Error { get; set; }

			public static ValidateResult Fail(string err) => new ValidateResult { Ok = false, Error = err };
			public static ValidateResult Success(string job, string cam, string pos)
				=> new ValidateResult { Ok = true, JobName = job, CamName = cam, PosName = pos };
		}

		// 校验：根据 Job{digit}+Channel 找主相机，并校验 Pos 是否存在
		private static ValidateResult ValidateJobCamPos(AppConfig cfg, int channel, int jobDigit, int posDigit)
		{
			if (cfg == null || cfg.Models == null) return ValidateResult.Fail("Configuration is not loaded");
			if (jobDigit <= 0) return ValidateResult.Fail("Invalid Job number");
			if (posDigit <= 0) return ValidateResult.Fail("Invalid Pos number");

			string jobName = "Job" + jobDigit;
			var job = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null) return ValidateResult.Fail($"Configuration does not contain {jobName}");
			if (job.Cameras == null || job.Cameras.Count == 0) return ValidateResult.Fail($"{jobName} has no cameras configured");

			CameraConfig camCfgForCh = null;
			foreach (var cam in job.Cameras)
			{
				if (cam?.Positions == null) continue;
				foreach (var pos in cam.Positions)
				{
					var g = pos?.General;
					if (g == null) continue;
					if (g.MainUsed == "1" && int.TryParse(g.MainChannel, out var ch) && ch == channel)
					{
						camCfgForCh = cam; break;
					}
				}
				if (camCfgForCh != null) break;
			}
			if (camCfgForCh == null) return ValidateResult.Fail($"No main camera for Channel={channel} was found under {jobName}");

			string posName = "Pos" + posDigit;
			var posCfg = camCfgForCh.Positions?.FirstOrDefault(p => string.Equals(p?.Name, posName, StringComparison.OrdinalIgnoreCase));
			if (posCfg == null) return ValidateResult.Fail($"{jobName}.{camCfgForCh.Name}.{posName} does not exist in the configuration");

			return ValidateResult.Success(jobName, camCfgForCh.Name, posName);
		}

		// 仅验证：当前 Job（按 Global 的记录）下，对应 Channel 的主相机里是否存在该 Pos
		private static ValidateResult ValidatePosInCurrentJob(AppConfig cfg, int channel, int posDigit)
		{
			if (cfg == null || cfg.Models == null) return ValidateResult.Fail("Configuration is not loaded");
			if (posDigit <= 0) return ValidateResult.Fail("Invalid Pos number");

			var currentJob = !string.IsNullOrEmpty(Global.Model_JobID_Send[channel])
				? Global.Model_JobID_Send[channel]
				: Global.Model_JobID[channel];

			if (string.IsNullOrEmpty(currentJob)) return ValidateResult.Fail("No Job is selected for the current channel");

			var job = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, currentJob, StringComparison.OrdinalIgnoreCase));
			if (job == null) return ValidateResult.Fail($"Configuration does not contain {currentJob}");

			CameraConfig camForCh = null;
			foreach (var cam in job.Cameras ?? Enumerable.Empty<CameraConfig>())
			{
				foreach (var pos in cam.Positions ?? Enumerable.Empty<PositionConfig>())
				{
					var g = pos?.General;
					if (g == null) continue;
					if (g.MainUsed == "1" && int.TryParse(g.MainChannel, out var ch) && ch == channel)
					{
						camForCh = cam; break;
					}
				}
				if (camForCh != null) break;
			}
			if (camForCh == null) return ValidateResult.Fail($"No main camera for Channel={channel} was found under {currentJob}");

			string posName = "Pos" + posDigit;
			var posCfg = camForCh.Positions?.FirstOrDefault(p => string.Equals(p?.Name, posName, StringComparison.OrdinalIgnoreCase));
			if (posCfg == null) return ValidateResult.Fail($"{currentJob}.{camForCh.Name}.{posName} does not exist in the configuration");

			return ValidateResult.Success(currentJob, camForCh.Name, posName);
		}


		public void ApplyPermissions(UserRecord user)
		{
			// 解析XML得到权限集合
			var ps = PermissionManager.Resolve(user);

			// 默认先全部禁用，再按Allow去启用（行为更安全）
			foreach (var kv in _permissionTargets)
			{
				kv.Value(false);
			}

			// 按权限键逐个启用
			foreach (var kv in _permissionTargets)
			{
				if (ps.IsAllowed(kv.Key))
					kv.Value(true);
			}
		}

		private void InitPermissionTargets()
		{
			_permissionTargets = new Dictionary<string, Action<bool>>(StringComparer.OrdinalIgnoreCase)
			{
				// 菜单项（举例，名称换成你自己的控件名）
				["CameraMenu"] = on => cameraToolStripMenuItem.Enabled = on,
				["AlgorithmMenu"] = on => algorithmToolStripMenuItem.Enabled = on,
				["GeneralMenu"] = on => generalToolStripMenuItem.Enabled = on,
				["DatabaseMenu"] = on => databaseToolStripMenuItem.Enabled = on,
				["SaveImageMenu"] = on => saveImageToolStripMenuItem.Enabled = on,
				["RotateCenterMenu"] = on => rotateCenterToolStripMenuItem.Enabled = on,
				["TestDualTrigger"] = on => manualTestToolStripMenuItem.Enabled = on,
				["Setting"] = on => settingToolStripMenuItem.Enabled = on,

				// 自定义一组“触发按钮”权限（比如每个相机的 Trigger 手动按钮）
				// 这里给一个总开关：遍历 camViews 里你做的 UserControl，去Enable内部按钮
				["TriggerButtons"] = on =>
				{
					try
					{
						//foreach (var cv in camViews)
						//{
						//	// 你自己的 UserControl 暴露一个API来控制手动触发按钮可用性
						//	cv.SetButton_btntrigger(on);
						//}
					}
					catch { }
				},
			};
		}

		#region [Manual Test]

		private void trigger0ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ShowManualSimulation(false, 0);
		}

		private void trigger1ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ShowManualSimulation(false, 1);
		}

		private void sendDataToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ShowManualSimulation(true, 0);
		}



		#endregion

        // Selection display is independent of camera validation/service startup.
        // Reading the saved protocol must never start a camera, socket or PLC.
        private void LoadCommunicationSelectionForDisplay()
        {
            var selected = CommunicationConfigHelper.Load();
            _displayProtocol = Enum.IsDefined(typeof(CommunicationProtocol), selected.Protocol)
                ? (CommunicationProtocol?)selected.Protocol : null;
            RefreshCommunicationState();
        }

        private void RefreshCommunicationState()
		{
			if (IsDisposed || Disposing) return;
			string protocol = !_displayProtocol.HasValue ? "Null" : (_displayProtocol.Value == CommunicationProtocol.TcpIp ? "TCP/IP" : _displayProtocol.Value.ToString());
			bool connected = _communicationService != null && _communicationService.IsStarted;
			string text = "Communicate:" + protocol;
			if (_communicationStatus.Text != text) _communicationStatus.Text = text;
			Color color = connected ? Color.Green : Color.Red;
			if (_communicationStatus.ForeColor != color) _communicationStatus.ForeColor = color;
			if (!_cameraValidationComplete || _switchingOnline || _cameraRestartRequested != 0) return;
			bool online = _runOnline && _licenseOk && connected;
			text = online ? "Online" : "Offline";
			color = online ? Color.Green : (!_runOnline && _licenseOk && connected ? Color.Yellow : Color.Red);
			if (button3.Text != UiLanguage.Text(text)) button3.Text = text;
			if (button3.BackColor != color) button3.BackColor = color;
		}

		private async void button3_Click(object sender, EventArgs e)
		{
			if (_switchingOnline || _cameraRecoveryStopping || _cameraRestartRequested != 0 || !_licenseOk) return;
			if (!_cameraValidationComplete) { RecoverCamerasManually(sender, e); return; }
			_switchingOnline = true;
			button3.Enabled = false;
			bool target = !_runOnline;
			_runOnline = false;
			var held = new List<SemaphoreSlim>();
			try
			{
				foreach (SemaphoreSlim gate in _chGates) { await gate.WaitAsync(); held.Add(gate); }
				if (!_cameraValidationComplete || _cameraRecoveryStopping) return;
				if (_communicationService == null) InitCommunication();
				IVisionCommunicationService service = _communicationService;
				if (service != null) await Task.Run(() => service.SetOnline(target));
				_runOnline = target && service != null;
				LogChangeEventArgs.Set("Log", _runOnline ? "Vision Online: communication signals initialized; Trigger Ready requires PLC Trigger Enable." : "Vision Offline: Online=False; automatic triggers disabled; PLC transport retained.", _runOnline ? Color.Green : Color.Orange);
			}
			catch (Exception ex)
			{
				_runOnline = false;
				try { if (_communicationService != null) _communicationService.SetOnline(false); } catch { }
				LogChangeEventArgs.Set("Log", "Switch Online/Offline failed: " + ex.Message, Color.Red);
			}
			finally
			{
				foreach (SemaphoreSlim gate in held) gate.Release();
				_switchingOnline = false;
				button3.Enabled = true;
				RefreshCommunicationState();
			}
		}

		private async void RecoverCamerasManually(object sender, EventArgs e)
		{
			if (_cameraRecoveryStopping || IsDisposed || Disposing || !_licenseOk) return;
			if (_cameraRestartRequested != 0)
			{
				LogChangeEventArgs.Set("Log", "Camera restart decision is in progress.", Color.Orange);
				return;
			}
			if (Interlocked.Exchange(ref _cameraRecovering, 1) == 1)
			{
				LogChangeEventArgs.Set("Log", "Camera recovery is already running.", Color.Orange);
				return;
			}

			Control btn = sender as Control;
			if (btn != null) btn.Enabled = false;

			try
			{
				LogChangeEventArgs.Set("Log", "Start VisionPro camera recovery...", Color.Black);
				_cameraValidationComplete = false;
				if (_communicationService != null) _communicationService.SetCameraReady(false);

				await System.Threading.Tasks.Task.Run(() =>
				{
					RecoverVisionProCameras();
				});
				if (_cameraRecoveryStopping || IsDisposed || !_licenseOk) return;

				if (!_cameraValidationComplete)
				{
					string failure = await Task.Run(() => VerifyStartupCameras());
					if (_cameraRecoveryStopping || IsDisposed || !_licenseOk) return;
					if (!string.IsNullOrEmpty(failure))
					{
						RequestCameraRestart("Manual camera recovery failed: " + failure);
						return;
					}
					_cameraValidationComplete = true;
					Program.ResetCameraRestartCount();
					LogChangeEventArgs.Set("Log", "Manual camera recovery passed; communication is now allowed.", Color.Green);
					if (_communicationService == null)
						InitCommunication();
					else
						_communicationService.SetCameraReady(true);
					if (_cameraRecoveryTimer != null) _cameraRecoveryTimer.Stop();
					RefreshCommunicationState();
				}
				else
				{
					LogChangeEventArgs.Set("Log", "VisionPro camera recovery complete.", Color.Green);
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "VisionPro camera recovery failed: " + ex.ToString(), Color.Red);
				RequestCameraRestart(ex.Message);
			}
			finally
			{
				if (btn != null) btn.Enabled = true;
				Interlocked.Exchange(ref _cameraRecovering, 0);
			}
		}

		private void RecoverVisionProCameras()
		{
			List<SemaphoreSlim> lockedGates = new List<SemaphoreSlim>();

			try
			{
				// 1. 暂停所有通道触发，避免恢复过程中 PLC 又触发拍照
				for (int i = 0; i < _chGates.Length; i++)
				{
					_chGates[i].Wait();
					lockedGates.Add(_chGates[i]);
				}

				// 2. 重新读取配置
				Global.CurrentConfig = XmlConfigHelper.Load(Global.ParameterCogfig);

				// 3. 重新加载当前 4 个通道对应的 Job
				int configuredChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());
				for (int ch = 0; ch < configuredChannelCount; ch++)
				{
					string jobId = Global.Model_JobID[ch];

					if (string.IsNullOrWhiteSpace(jobId))
						jobId = "Job1";

					try
					{
						LogChangeEventArgs.Set("Log", $"Reload VPP/AcqFifo for Channel {ch}, {jobId}...", Color.Black);

						// 关键：重新加载该通道当前 Job 下所有 Pos 的 VPP 和 AcqFifo
						Load_Job.LoadVPP_ForChannel_AllPos(ch, jobId);

						LogChangeEventArgs.Set("Log", $"Reload Channel {ch} {jobId} OK.", Color.Green);
					}
					catch (Exception exCh)
					{
						LogChangeEventArgs.Set("Log", $"Reload Channel {ch} failed: {exCh.Message}", Color.Red);
					}
				}

				// 4. UI 相机状态先恢复成 Ready，下一次触发再判断真实 OK/NG
				if (!this.IsDisposed && this.IsHandleCreated)
				{
					this.BeginInvoke(new Action(() =>
					{
						for (int i = 0; i < camViews.Count; i++)
						{
							camViews[i].SetButton_CamStatus(Color.Gainsboro, "Cam" + (i + 1) + ":Ready");
						}
					}));
				}
			}
			finally
			{
				// 5. 释放通道锁，恢复 PLC 触发
				for (int i = lockedGates.Count - 1; i >= 0; i--)
				{
					lockedGates[i].Release();
				}
			}
		}

		private void settingToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			using (S7SettingForm f = new S7SettingForm())
			{
				f.S7SettingSaved += S7SettingForm_S7SettingSaved;
				f.ShowDialog(this);
				f.S7SettingSaved -= S7SettingForm_S7SettingSaved;
			}
		}

		private void inputToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (S7InputSettingForm f = new S7InputSettingForm())
			{
				f.ShowDialog(this);
			}
		}

		private void outputSettingToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			using (S7OutputSettingForm f = new S7OutputSettingForm())
			{
				f.ShowDialog(this);
			}
		}

		private void communicateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (CommunicationSettingForm form = new CommunicationSettingForm())
			{
				form.CommunicationSettingSaved += CommunicationSettingForm_CommunicationSettingSaved;
				form.ShowDialog(this);
				form.CommunicationSettingSaved -= CommunicationSettingForm_CommunicationSettingSaved;
			}
		}

		private void CommunicationSettingForm_CommunicationSettingSaved(object sender, EventArgs e)
		{
			LogChangeEventArgs.Set("Log", "Apply communication selection...", Color.Black);
			InitCommunication();
		}
	}

}

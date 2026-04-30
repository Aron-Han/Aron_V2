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
		//test
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


		#region [lock]
		private readonly ConcurrentDictionary<string, object> _camLocks = new ConcurrentDictionary<string, object>();

		private static readonly object _sendLock = new object();

		private static readonly SemaphoreSlim[] _chGates = new[] { new SemaphoreSlim(1, 1), new SemaphoreSlim(1, 1), new SemaphoreSlim(1, 1), new SemaphoreSlim(1, 1), };

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
			HookEventsEarly();
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
			var cfg = XmlConfigHelper.Load(Global.ParameterCogfig);
			var f = new Camera(cfg);
			f.Show(this);
		}
		private void algorithmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var cfg = XmlConfigHelper.Load(Global.ParameterCogfig);
			var f = new Algorithm(cfg);
			f.Show(this);
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
			using (var Form2 = new ImageRecord())
			{
				Form2.SettingsSaved += (s, ev) =>
				{
					ImageRecord.LoadSettings();
					Global.Save_Image_Root = ImageRecord.Current.Root;

					ApplySoftwareTitle();
					StartLicenseMonitor();

					LogChangeEventArgs.Set("Log", "Settings saved, software title updated.", Color.Green);
				};

				Form2.ShowDialog();
			}
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
			using (var Form2 = new Parameters_Config())
			{
				Form2.ConfigSaved += (s, f) => ReloadConfigAndUI();
				Form2.ShowDialog();
			}
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
					LogChangeEventArgs.Set("Log", "Reflash vpp output Failed：" + ex.Message, Color.Red);
				}
			};

			dlg.Show(this);
		}
		private void generalToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Global.CurrentConfig = XmlConfigHelper.Load(Global.ParameterCogfig);
			XmlConfigHelper.Load_Job(Global.CurrentConfig, Global.Model_JobID[0]);
			var f = new FormCamGeneralConfig(Global.CurrentConfig, Global.ParameterCogfig);
			f.ConfigSaved += (_, __) =>
			{
				Global.CurrentConfig = XmlConfigHelper.Load(Global.ParameterCogfig);
				XmlConfigHelper.Load_Job(Global.CurrentConfig, Global.Model_JobID[0]);
			};
			f.Show(this);
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
			Global.Manual_Trigger_Lock[int.Parse(uc.Channel.ToString().Substring(uc.Channel.ToString().Length - 1, 1))] = true;
			await Cam_Inspection(uc.Name, uc.JobID, uc.PosID, uc.Channel);
			Global.Manual_Trigger_Lock[int.Parse(uc.Channel.ToString().Substring(uc.Channel.ToString().Length - 1, 1))] = false;
		}
		public void Replay(UserControl_ImageShow uc)
		{

			ReplayImage(uc.Name, uc.PosID, uc.JobID, uc.Channel);
		}


		#endregion

		#region Event
		private void FormMain_Load(object sender, EventArgs e)
		{
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
			for (int i = 0; i < 4; i++)
			{
				DataChangedEventArgs.Set("JobID" + i, "Job1");
			}

			ThreadPool.QueueUserWorkItem(new WaitCallback(InI_Porfinet), this);
			if (CC24_Comm.Instance().IsConnected)
			{
				button3.Text = "Connected";
				button3.BackColor = Color.Green;
			}
			else
			{
				button3.Text = "Disconnected";
				button3.BackColor = Color.Red;
			}

			InitPermissionTargets();
			toolStripStatusLabel1.Text = _init.DefaultUser.Username;
			ApplyPermissions(_init.DefaultUser);


			// 建议同步给全局保存路径，避免保存和清理不是同一个目录
			Global.Save_Image_Root = ImageRecord.Current.Root;

			// ===== 启动“每天清理一次” =====
			StartImageCleanupScheduler();
			StartLicenseMonitor();
		}

		public static void InI_Porfinet(Object Param)
		{
			try
			{
				bool flagD = false;
				#region[启动CC24卡]
				flagD = CC24_Comm.Instance().InitCommCard();  //查找通信卡

				CC24_Comm.Instance().InitFFP();               //启动工业通信协议
				if (flagD)
				{
					LogChangeEventArgs.Set("Log", "Start Ini Porfinet...", Color.Green);
					CC24_Comm.Instance().NewUserData += GetData;
					CC24_Comm.Instance().NewTrigger += NewTrigger;
				}
				else
				{
					LogChangeEventArgs.Set("Log", "InI CC24 Failed！", Color.Red);
					Thread.Sleep(2000);
				}
				#endregion
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "InI CC24 Failed！ Error Message:" + ex.ToString(), Color.Red);
			}
		}

		private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			// 只拦截用户点击右上角 X / Alt+F4 / 菜单关闭
			if (e.CloseReason == CloseReason.UserClosing)
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
					CC24_Comm.Instance().NewUserData -= GetData;
					CC24_Comm.Instance().NewTrigger -= NewTrigger;

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

					try { LogManager.WriteLog("Close CC24 communication..."); } catch { }

					lock (_sendLock)
					{
						CC24_Comm.Instance().Close();
					}

					try { LogManager.WriteLog("Software closed successfully."); } catch { }
				}
				catch (Exception ex)
				{
					try { LogManager.WriteLog("Software close failed: " + ex.Message); } catch { }
				}
				finally
				{
					base.Dispose();
				}
			}
		}

		#endregion

		#region Profinet
		public static void GetData(object sender, byte[] str, int channel)
		{
			try
			{
				string s = string.Join(",", str.Select(b => b.ToString()));
				if (ImageRecord.Current.Show_Received_data)
					LogChangeEventArgs.Set("Log", "Receive Data:" + s, Color.Black);

				if (!IsValidChannel(channel))
				{
					LogChangeEventArgs.Set("Log", "Invalid channel", Color.Red);
					return;
				}

				// ===== 串行化：同一通道 GetData/Trigger/Send 不交叉 =====
				var gate = GetGateOrThrow(channel);
				gate.Wait();
				try
				{
					// 2) Clear Result（四个 bit 任一个为 '1'，就清当前通道）
					bool clrAny =
						(input_Parameters.CharDigitAt(str, input_Parameters.IdxClrCh0) == 1) ||
						(input_Parameters.CharDigitAt(str, input_Parameters.IdxClrCh1) == 1) ||
						(input_Parameters.CharDigitAt(str, input_Parameters.IdxClrCh2) == 1) ||
						(input_Parameters.CharDigitAt(str, input_Parameters.IdxClrCh3) == 1);

					if (clrAny)
					{
						input_Parameters.ClearResultBufferByConfig(channel);
						var clearresult = SnapshotPlcBuffer();

						lock (_sendLock)
						{
							CC24_Comm.Instance().SendData(clearresult, 0, channel);
							CC24_Comm.Instance().mNdm.NotifySystemStatus(true, false);
						}
						LogChangeEventArgs.Set("Log", $"ClearResult channel{channel}", Color.Black);
						return;
					}

					// 3) Pos and Job
					int pos0 = input_Parameters.CharDigitAt(str, input_Parameters.IdxPosCh0);
					int pos1 = input_Parameters.CharDigitAt(str, input_Parameters.IdxPosCh1);
					int pos2 = input_Parameters.CharDigitAt(str, input_Parameters.IdxPosCh2);
					int pos3 = input_Parameters.CharDigitAt(str, input_Parameters.IdxPosCh3);

					int jobDigit0 = input_Parameters.CharDigitAt(str, input_Parameters.IdxJobCh0);
					int jobDigit1 = input_Parameters.CharDigitAt(str, input_Parameters.IdxJobCh1);
					int jobDigit2 = input_Parameters.CharDigitAt(str, input_Parameters.IdxJobCh2);
					int jobDigit3 = input_Parameters.CharDigitAt(str, input_Parameters.IdxJobCh3);

					var cfg = Global.CurrentConfig;

					#region[ChangeJob and pos]
					int wantJobDigit = (channel == 0) ? jobDigit0 :
					   (channel == 1) ? jobDigit1 :
					   (channel == 2) ? jobDigit2 : jobDigit3;

					int wantPosDigit = (channel == 0) ? pos0 :
							   (channel == 1) ? pos1 :
							   (channel == 2) ? pos2 : pos3;

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
							LogChangeEventArgs.Set("Log", $"Start Change Job: {vr.JobName}.{vr.CamName}.{vr.PosName} (Ch={channel})", Color.Black);
							JobChange(channel, wantJobDigit, wantPosDigit.ToString());
							Global.Model_JobID_Send[channel] = Global.Model_JobID[channel];
							Global.Position_ID[channel] = wantPosDigit.ToString();
							Global.Position_ID_Send[channel] = Global.Position_ID[channel];
							LogChangeEventArgs.Set("Log", $"Change Job Complete: {vr.JobName}.{vr.CamName}.{vr.PosName} (Ch={channel})", Color.Green);
							DataChangedEventArgs.Set("PosID" + channel, vr.PosName.Substring(vr.PosName.Length - 1, 1));
						}
						else
						{
							LogChangeEventArgs.Set("Log", $"Fail change to：{vr.Error} (Ch={channel})", Color.Red);
						}
					}
					else
					{
						var vr2 = ValidatePosInCurrentJob(cfg, channel, wantPosDigit);
						if (vr2.Ok)
						{
							Global.Position_ID[channel] = wantPosDigit.ToString();
							Global.Position_ID_Send[channel] = Global.Position_ID[channel];
							LogChangeEventArgs.Set("Log", $"Change Pos Complete: {vr2.CamName}.{vr2.PosName} (Ch={channel})", Color.Green);
							DataChangedEventArgs.Set("PosID" + channel, vr2.PosName.Substring(vr2.PosName.Length - 1, 1));
						}
						else
						{
							if (vr2.JobName != null && vr2.PosName != null)
							{
								DataChangedEventArgs.Set("JobID" + channel, vr2.JobName.Substring(vr2.JobName.Length - 1, 1));
								DataChangedEventArgs.Set("PosID" + channel, vr2.PosName.Substring(vr2.PosName.Length - 1, 1));
							}
							else
							{
								LogChangeEventArgs.Set("Log", $"Fail change to：{vr2.Error} (Ch={channel})", Color.Red);
							}
						}
					}
					#endregion

					// 5) PartCode
					switch (channel)
					{
						case 0:
							{
								var part = input_Parameters.SafeSlice(str, input_Parameters.IdxPartCh0, input_Parameters.PartLen);
								Global.PartCode[0] = part;
								LogChangeEventArgs.Set("Log",
									$"Channel0  JobID:{Global.Model_JobID_Send[0]}  PosID:{Global.Position_ID_Send[0]}  PartCode:{Global.PartCode[0]}",
									Color.Green);
								break;
							}
						case 1:
							{
								var part = input_Parameters.SafeSlice(str, input_Parameters.IdxPartCh1, input_Parameters.PartLen);
								Global.PartCode[1] = part;
								LogChangeEventArgs.Set("Log",
									$"Channel1  JobID:{Global.Model_JobID_Send[1]}  PosID:{Global.Position_ID_Send[1]}  PartCode:{part}",
									Color.Green);
								break;
							}
						case 2:
							{
								var part = input_Parameters.SafeSlice(str, input_Parameters.IdxPartCh2, input_Parameters.PartLen);
								Global.PartCode[2] = part;
								LogChangeEventArgs.Set("Log",
									$"Channel2  JobID:{Global.Model_JobID_Send[2]}  PosID:{Global.Position_ID_Send[2]}",
									Color.Green);
								break;
							}
						case 3:
							{
								var part = input_Parameters.SafeSlice(str, input_Parameters.IdxPartCh3, input_Parameters.PartLen);
								Global.PartCode[3] = part;
								LogChangeEventArgs.Set("Log",
									$"Channel3  JobID:{Global.Model_JobID_Send[3]}  PosID:{Global.Position_ID_Send[3]}",
									Color.Green);
								break;
							}
					}

					// ===== 回显写入 + 发送：保证 PLC 比对用的是“本次 GetData 处理后的值” =====
					lock (Global.PlcBufferLock)
					{
						Global.Result_Send[PlcEchoRegion.IdxJobCh0] = input_Parameters.ToDigit(Global.Model_JobID_Send[0].Length >= 4 ? Global.Model_JobID_Send[0].Substring(3, 1) : "0");
						Global.Result_Send[PlcEchoRegion.IdxJobCh1] = input_Parameters.ToDigit(Global.Model_JobID_Send[1].Length >= 4 ? Global.Model_JobID_Send[1].Substring(3, 1) : "0");
						Global.Result_Send[PlcEchoRegion.IdxJobCh2] = input_Parameters.ToDigit(Global.Model_JobID_Send[2].Length >= 4 ? Global.Model_JobID_Send[2].Substring(3, 1) : "0");
						Global.Result_Send[PlcEchoRegion.IdxJobCh3] = input_Parameters.ToDigit(Global.Model_JobID_Send[3].Length >= 4 ? Global.Model_JobID_Send[3].Substring(3, 1) : "0");

						Global.Result_Send[PlcEchoRegion.IdxPosCh0] = input_Parameters.ToDigit(Global.Position_ID_Send[0] ?? "0");
						Global.Result_Send[PlcEchoRegion.IdxPosCh1] = input_Parameters.ToDigit(Global.Position_ID_Send[1] ?? "0");
						Global.Result_Send[PlcEchoRegion.IdxPosCh2] = input_Parameters.ToDigit(Global.Position_ID_Send[2] ?? "0");
						Global.Result_Send[PlcEchoRegion.IdxPosCh3] = input_Parameters.ToDigit(Global.Position_ID_Send[3] ?? "0");
					}

					var frame = SnapshotPlcBuffer();
					lock (_sendLock)
					{
						CC24_Comm.Instance().SendData(frame, 0, channel);
						CC24_Comm.Instance().mNdm.NotifySystemStatus(true, false);
					}
				}
				finally
				{
					gate.Release();
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "GetData error: " + ex.Message, Color.Red);
			}


		}

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
					LogChangeEventArgs.Set("Log", "Receive Trigger: " + channel, Color.Black);

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

		public static void JobChange(int Channel, int JobID, string PositionID)
		{
			lock (_sendLock)
			{
				CC24_Comm.Instance().mNdm.NotifySystemStatus(false, true);
			}
			if (Global.Model_JobID[Channel] != "Job" + JobID.ToString())
				Load_Job.LoadVPP_ForChannel_AllPos(Channel, "Job" + JobID);
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
					Log.AppendColoredText(this.richTextBox1, logText, color);
					LogManager.WriteLog(logText);
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
						this.button3.Text = value.ToString();
						this.button3.BackColor = (value.ToString() == "Connect") ? Color.Green : Color.Red;
						if (value.ToString() == "Connect")
							LogChangeEventArgs.Set("Log", "InI CC24 OK！", Color.Green);
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
			tableLayoutPanel5.ColumnStyles.Clear();
			for (int i = 0; i < Global.CamN_Use; i++)
			{
				tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / Global.CamN_Use));
			}
			for (int i = 0; i < Global.CamN_Use; i++)
			{
				UserControl_ImageShow camView = new UserControl_ImageShow();
				_stats[i] = new CameraStats();

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
			for (int i = 0; i < Global.CamN_Use; i++)
			{
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

			this.Text = title;
		}

		#endregion

		#region Vision
		private void Trigger(CogAcqFifoTool camera, string CamN)
		{
			camViews[int.Parse(CamN.Substring(CamN.Length - 1)) - 1].SetButton_CamStatus(Color.Green, CamN);
			if (camera == null) return;
			camera.Run();
			if (camera.RunStatus.Result.ToString() != "Accept")
			{
				Trigger_Failed(CamN, camera.RunStatus.Message);
			}
		}

		//for Auto and Trigger Manual
		public async Task Cam_Inspection(string CamN, string JobID, string PosID, int Channel)
		{
			if (PosID.Length == 1)
				PosID = "Pos" + PosID;

			string ResultTotal = "2";
			Global.ResultTotal_Cam[int.Parse(CamN) - 1] = ResultTotal;

			try
			{
				if (Global.Manual_Trigger_Lock[Channel] == false)
				{
					lock (_sendLock)
					{
						CC24_Comm.Instance().mNdm.NotifyAcquisitionStarted(Channel, Channel);
					}
				}


				LogChangeEventArgs.Set("Log", "Start Second", Color.Black);
				await RunSecondGroup(JobID, PosID, Channel);
				LogChangeEventArgs.Set("Log", "Start main", Color.Black);
				await RunMain(JobID, PosID, Channel);


				if (!Global.Manual_Trigger_Lock[Channel])
				{
					lock (_sendLock)
					{
						CC24_Comm.Instance().mNdm.NotifyAcquisitionComplete(Channel, Channel);
						CC24_Comm.Instance().mNdm.NotifyAcquisitionReady(Channel);
					}
				}
			}
			catch (Exception e)
			{
				LogChangeEventArgs.Set("Log", "Trigger Failed:" + e.ToString(), Color.Red);
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
				dialog.Filter = "Image Files|*.png;*.bmp;"; ;
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					string selectedPath = dialog.FileName;
					Image_record = selectedPath;
					Global.imageRecord = new Bitmap(Image_record);
					CogImageFileTool imageFileTool = new CogImageFileTool();
					imageFileTool.Operator.Open(Image_record, CogImageFileModeConstants.Read);
					imageFileTool.Run();
					Global.image_Replay = imageFileTool.OutputImage;
					try
					{
						string Recent_Reult = "2";
						CamN = "Cam" + CamN;

						string genKey = JobID + "." + CamN + "." + PosID;
						CameraGeneralRuntime gen;
						Global.CamGeneral.TryGetValue(genKey, out gen);

						var dict = XmlConfigHelper.GetParametersFor(Global.CurrentConfig, JobID, CamN, PosID);
						lock (_camLocks.GetOrAdd(CamN, _ => new object()))
						{
							VppHelper.ApplyParametersToToolBlock(VPP, JobID, CamN, PosID, dict);

							VPP.Inputs[0].Value = Global.image_Replay;
							VPP.Inputs[1].Value = string.IsNullOrEmpty(Global.Replay_Send_Data) ? null: Global.Replay_Send_Data;
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
			camViews[int.Parse(CamN.Substring(CamN.Length - 1)) - 1].SetButton_CamStatus(Color.Red, CamN + ":Offline");
			LogChangeEventArgs.Set("Log", "Camera" + CamN + " Trigger Failed:" + Message + "After fixed，should restart software", Color.Red);
			lock (_sendLock)
			{
				CC24_Comm.Instance().Close();
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
							});
					});

					tasks.Add(t);
					DataChangedEventArgs.Set("PosID" + currentChannel, posName.Substring(posName.Length - 1, 1));
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
							});
					});

					tasks.Add(t);
					DataChangedEventArgs.Set("PosID" + currentChannel, posName.Substring(posName.Length - 1, 1));
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
			LogChangeEventArgs.Set("Log", "main complete", Color.Black);

			UseAllCamResults(allResultString);
		}


		//相机拍照+检测
		private void CaptureAndInspect(string jobId, string camName, string posName, int channel, CogAcqFifoTool camera, CogToolBlock tool, string inputData, Action<CamRunResult> onFinished)
		{
			Trigger(camera, camName);
			var image = camera.OutputImage;
			string result_send = "";
			string Recent_Reult = "2";

			// PLC 采图通知（主相机才报）
			string genKey = jobId + "." + camName + "." + posName;
			CameraGeneralRuntime gen;
			Global.CamGeneral.TryGetValue(genKey, out gen);
			int mainCh = -1;
			if (gen != null && gen.MainUsed == "1") int.TryParse(gen.MainChannel, out mainCh);
			if (gen != null && gen.MainUsed == "1" &&
				!Global.Manual_Trigger_Lock[Math.Max(0, mainCh)])
			{
				lock (_sendLock)
				{
					CC24_Comm.Instance().mNdm.NotifyAcquisitionComplete(mainCh, mainCh);
					CC24_Comm.Instance().mNdm.NotifyAcquisitionReady(mainCh);
				}
			}

			// 2) 设置参数 + 同步 Run（确保 Outputs 已更新）
			var dict = XmlConfigHelper.GetParametersFor(Global.CurrentConfig, jobId, camName, posName);
			lock (_camLocks[camName])
			{
				VppHelper.ApplyParametersToToolBlock(tool, jobId, camName, posName, dict);

				tool.Inputs[0].Value = image;
				tool.Inputs[1].Value = inputData;
				tool.Run();
				Recent_Reult = tool.Outputs.Contains("Result_Cam")
					? (tool.Outputs["Result_Cam"].Value ?? "").ToString()
					: "3";
				UpdateStatsByResult(camName, Recent_Reult);
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
				if (!Global.Manual_Trigger_Lock[channel] && ImageRecord.Current.EnableSave)
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
			if (!Global.Manual_Trigger_Lock[channel])
			{
				if (_vppOutCfg != null && mainCh >= 0)
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
		}


		private void UseAllCamResults(string allResultString)
		{
			LogChangeEventArgs.Set("Log", "Slave camera Inspection Complete," + allResultString, Color.Black);
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
				if (ImageRecord.Current.RetentionDays <= 0)
				{
					LogChangeEventArgs.Set("Log", "Image auto cleanup disabled", Color.Black);
					return;
				}

				// 启动时先清理一次
				ThreadPool.QueueUserWorkItem(_ => RunImageCleanupSafe());

				// 计算距离下一次“凌晨00:05”的时间
				DateTime now = DateTime.Now;
				DateTime nextRun = DateTime.Today.AddDays(1).AddMinutes(5); // 明天 00:05
				TimeSpan dueTime = nextRun - now;
				if (dueTime < TimeSpan.Zero)
					dueTime = TimeSpan.FromMinutes(1);

				// 每24小时执行一次
				_imageCleanupTimer = new System.Threading.Timer(
					_ => RunImageCleanupSafe(),
					null,
					dueTime,
					TimeSpan.FromDays(1));

				LogChangeEventArgs.Set("Log",
					$"Image cleanup scheduler started, next run at {nextRun:yyyy-MM-dd HH:mm:ss}",
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

				if (ImageRecord.Current.RetentionDays > 0)
				{
					ImageRecord.CleanupOldDays();
					LogChangeEventArgs.Set("Log",
						$"Image cleanup complete, retention={ImageRecord.Current.RetentionDays} days",
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
							button3.Text = "Disconnected";
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
			if (cfg == null || cfg.Models == null) return ValidateResult.Fail("配置未加载");
			if (jobDigit <= 0) return ValidateResult.Fail("无效的Job号");
			if (posDigit <= 0) return ValidateResult.Fail("无效的Pos号");

			string jobName = "Job" + jobDigit;
			var job = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null) return ValidateResult.Fail($"配置中不存在 {jobName}");
			if (job.Cameras == null || job.Cameras.Count == 0) return ValidateResult.Fail($"{jobName} 下无相机");

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
			if (camCfgForCh == null) return ValidateResult.Fail($"{jobName} 下未找到负责 Channel={channel} 的主相机");

			string posName = "Pos" + posDigit;
			var posCfg = camCfgForCh.Positions?.FirstOrDefault(p => string.Equals(p?.Name, posName, StringComparison.OrdinalIgnoreCase));
			if (posCfg == null) return ValidateResult.Fail($"{jobName}.{camCfgForCh.Name}.{posName} 在配置中不存在");

			return ValidateResult.Success(jobName, camCfgForCh.Name, posName);
		}

		// 仅验证：当前 Job（按 Global 的记录）下，对应 Channel 的主相机里是否存在该 Pos
		private static ValidateResult ValidatePosInCurrentJob(AppConfig cfg, int channel, int posDigit)
		{
			if (cfg == null || cfg.Models == null) return ValidateResult.Fail("配置未加载");
			if (posDigit <= 0) return ValidateResult.Fail("无效的Pos号");

			var currentJob = !string.IsNullOrEmpty(Global.Model_JobID_Send[channel])
				? Global.Model_JobID_Send[channel]
				: Global.Model_JobID[channel];

			if (string.IsNullOrEmpty(currentJob)) return ValidateResult.Fail("当前通道未选择Job");

			var job = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, currentJob, StringComparison.OrdinalIgnoreCase));
			if (job == null) return ValidateResult.Fail($"配置中不存在 {currentJob}");

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
			if (camForCh == null) return ValidateResult.Fail($"{currentJob} 下未找到负责 Channel={channel} 的主相机");

			string posName = "Pos" + posDigit;
			var posCfg = camForCh.Positions?.FirstOrDefault(p => string.Equals(p?.Name, posName, StringComparison.OrdinalIgnoreCase));
			if (posCfg == null) return ValidateResult.Fail($"{currentJob}.{camForCh.Name}.{posName} 在配置中不存在");

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
			NewTrigger(sender, "0");
		}

		private void trigger1ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			NewTrigger(sender, "1");
		}

		private void sendDataToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var input = SimplePrompt.Show("Manaul Receive Data", "Please Test in the Data：");
			if (string.IsNullOrWhiteSpace(input)) return;

			try
			{
				//GetData(this, input);
			}
			catch (Exception ex)
			{
				System.Windows.Forms.MessageBox.Show("Send Failed：" + ex.Message, "Error",
					System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
			}

		}


		#endregion

	}

	public static class SimplePrompt
	{
		public static string Show(string title, string label, string defaultText = "")
		{
			using (var f = new Form())
			using (var lbl = new Label())
			using (var txt = new TextBox())
			using (var ok = new Button())
			using (var cancel = new Button())
			{
				f.Text = title;
				f.FormBorderStyle = FormBorderStyle.FixedDialog;
				f.StartPosition = FormStartPosition.CenterParent;
				f.MinimizeBox = f.MaximizeBox = false;
				f.ClientSize = new Size(420, 140);
				f.AcceptButton = ok;
				f.CancelButton = cancel;

				lbl.Text = label;
				lbl.AutoSize = true;
				lbl.Location = new Point(12, 15);

				txt.Text = defaultText;
				txt.Location = new Point(12, 45);
				txt.Width = 394;

				ok.Text = "OK";
				ok.DialogResult = DialogResult.OK;
				ok.Location = new Point(230, 90);
				cancel.Text = "Cancel";
				cancel.DialogResult = DialogResult.Cancel;
				cancel.Location = new Point(320, 90);

				f.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });

				return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
			}
		}
	}


}

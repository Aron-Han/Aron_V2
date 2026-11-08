using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml.Linq;
using HslCommunication;
using ThreadingTimer = System.Threading.Timer;

namespace Aron_V2
{
	/// <summary>
	/// S7 运行通讯服务。
	/// 逻辑对应原来的 Profinet:
	/// 1. NewUserData  -> InputChanged
	/// 2. NewTrigger   -> TriggerRequested
	/// </summary>
	public class S7VisionCommService : IVisionCommunicationService, IVisionAcquisitionNotifications
	{
		private readonly Dictionary<string, S7PointItem> _inputMap =
			new Dictionary<string, S7PointItem>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, S7PointItem> _outputMap =
			new Dictionary<string, S7PointItem>(StringComparer.OrdinalIgnoreCase);

		private readonly bool[] _lastTrigger = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly bool[] _lastAck = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly bool[] _lastSetUserData = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly bool[] _lastClearExposureComplete = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly string[] _lastInputSnapshot = new string[CommunicationConfigHelper.MaxScalableChannelCount];
		private bool _lastTriggerEnable = false;
		private bool _bufferResults;
		private readonly bool[] _resultValid = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly bool[] _inspectionComplete = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
        private readonly bool[] _acquiring = new bool[CommunicationConfigHelper.MaxScalableChannelCount];
		private readonly Dictionary<int, Queue<List<KeyValuePair<S7PointItem, string>>>> _resultQueues = new Dictionary<int, Queue<List<KeyValuePair<S7PointItem, string>>>>();
		private readonly byte[] _echoJob = new byte[CommunicationConfigHelper.MaxScalableChannelCount], _echoPos = new byte[CommunicationConfigHelper.MaxScalableChannelCount];
		private Queue<List<KeyValuePair<S7PointItem, string>>> Results(int channel)
		{
			if (!_resultQueues.ContainsKey(channel)) _resultQueues[channel] = new Queue<List<KeyValuePair<S7PointItem, string>>>();
			return _resultQueues[channel];
		}
		private void PublishResult(int channel, List<KeyValuePair<S7PointItem, string>> packet)
		{
			try
			{
				foreach (var item in packet)
					if (!WriteOutputPoint(channel, item.Key.Parameter, item.Value)) throw new IOException("Data write failed: " + item.Key.Parameter + " [" + item.Key.DbAddress + "]");
				if (!WriteOutputPoint(channel, "Result Valid", "true")) throw new IOException("Result Valid write failed.");
				_resultValid[channel] = true;
			}
			catch
			{
				WriteOutputPoint(channel, "Result Valid", "false"); _resultValid[channel] = false;
				throw;
			}
		}

		private S7CommConfig _config;
		private S7AddressTester _tester;
		private ThreadingTimer _pollTimer;
		private ThreadingTimer _heartbeatTimer;
		private ThreadingTimer _reconnectTimer;
		private int _polling;
		private int _reconnecting;
		private bool _started;
		private volatile bool _cameraReady = true;
		private bool _initializingOutputs;
		private DateTime _lastOnlinePublish = DateTime.MinValue;
		private volatile bool _onlineRequested = true;

		public void SetOnline(bool online)
		{
			lock (_serviceWriteLock)
			{
				_onlineRequested = online;
				if (!_started || _tester == null) return;
				try { InitializeOutputsAfterConnected(); }
				catch { EnterReconnectMode(); throw; }
			}
		}

		public void SetCameraReady(bool ready)
		{
			lock (_serviceWriteLock)
			{
				_cameraReady = ready;
				if (!ready)
				{
					// Camera loss stops polling, heartbeat and reconnect as well as the socket.
					StopCore();
					return;
				}
				if (!_started || _tester == null)
				{
					Start();
					return;
				}
				try
				{
					InitializeOutputsAfterConnected();
				}
				catch
				{
					EnterReconnectMode();
					throw;
				}
			}
		}
		private volatile bool _stopRequested;
		private volatile bool _reconnectMode;
		private readonly object _connectionStateLock = new object();
		private DateTime _lastReconnectLogTime = DateTime.MinValue;
		private DateTime _lastRuntimeFailureLogTime = DateTime.MinValue;
		private string _lastRuntimeFailure = string.Empty;
		private const int GlobalChannel = -1;
		private int _activeChannelCount = 2;

		private int ActiveChannelCount
		{
			get { return _activeChannelCount; }
		}

		// 保证一组业务输出连续写入。
		private readonly object _serviceWriteLock = new object();

		public event Action<CommunicationChannelInput> InputChanged;
		public event Action<int> TriggerRequested;
		public event Action<int> ResultAckReceived;
		public event Action<string> LogMessage;

		private readonly Dictionary<string, DateTime> _lastReadErrorLogTime =
	new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

		public bool IsStarted
		{
			get { return _started; }
		}

		public void Start()
		{
			lock (_serviceWriteLock) StartCore();
		}

		private void StartCore()
		{
			try
			{
				Stop();
				if (!_cameraReady) return;

				_stopRequested = false;
				_reconnectMode = false;

				_config = S7ConfigXmlHelper.Load();
				if (_config == null || _config.Basic == null)
					throw new Exception("S7 config is null.");

				if (!_config.Basic.Enable)
					WriteLog("Warning: Enable S7 is false, but S7 service will still start because FormMain is now S7 mode.");

				_activeChannelCount = GetActiveChannelCount(_config);

				LoadAddressMaps();

				_tester = new S7AddressTester(_config.Basic);
				OperateResult connect = _tester.Connect();
				if (!connect.IsSuccess)
					throw new Exception(connect.Message);

				_started = true;

				InitializeOutputsAfterConnected();

				EnsureRuntimeTimers();

				WriteLog("S7 service started.");
			}
			catch (Exception ex)
			{
				WriteRuntimeFailureLimited("S7 service startup failed: " + RootMessage(ex));
				_started = false;
				SafeDisposeTester();

				// 启动时如果 PLC 不在线，不再刷详细错误，只进入自动重连。
				EnterReconnectMode();
			}
		}

		public void Stop()
		{
			lock (_serviceWriteLock) StopCore();
		}

		private void StopCore()
		{
			_stopRequested = true;

			try
			{
				if (_pollTimer != null)
				{
					_pollTimer.Dispose();
					_pollTimer = null;
				}

				if (_heartbeatTimer != null)
				{
					_heartbeatTimer.Dispose();
					_heartbeatTimer = null;
				}

				if (_reconnectTimer != null)
				{
					_reconnectTimer.Dispose();
					_reconnectTimer = null;
				}

				if (_tester != null && _started)
				{
					try
					{
						SafeWriteBoolOutput(GlobalChannel, "Online", false);
						SafeWriteBoolOutput(GlobalChannel, "System Ready", false);
						SafeWriteBoolOutput(GlobalChannel, "System Busy", false);
						for (int ch = 0; ch < ActiveChannelCount; ch++)
							SafeWriteBoolOutput(ch, "Trigger Ready Camera", false);
					}
					catch
					{
					}
				}

				SafeDisposeTester();
				_reconnectMode = false;
				_started = false;
			}
			catch
			{
				// Stop must remain best-effort. A shutdown write failure is reported
				// by the normal runtime path, not treated as a reconnect failure.
				_started = false;
			}
		}

		private void SafeDisposeTester()
		{
			try
			{
				if (_tester != null)
				{
					_tester.Dispose();
					_tester = null;
				}
			}
			catch
			{
				_tester = null;
			}
		}

		private void EnterReconnectMode()
		{
			if (_stopRequested || !_cameraReady)
				return;

			lock (_connectionStateLock)
			{
				if (!_reconnectMode)
				{
					_reconnectMode = true;
					_started = false;
					StartReconnectTimer();
				}
			}

			WriteReconnectLogLimited();
		}

		private void StartReconnectTimer()
		{
			if (_reconnectTimer != null)
				return;

			_reconnectTimer = new ThreadingTimer(ReconnectTimerCallback, null, 0, 2000);
		}

		private void StopReconnectTimer()
		{
			try
			{
				if (_reconnectTimer != null)
				{
					_reconnectTimer.Dispose();
					_reconnectTimer = null;
				}
			}
			catch
			{
				_reconnectTimer = null;
			}
		}

		private void ReconnectTimerCallback(object state)
		{
			lock (_serviceWriteLock) ReconnectCore();
		}

		private void ReconnectCore()
		{
			if (_stopRequested || !_cameraReady)
				return;

			if (System.Threading.Interlocked.Exchange(ref _reconnecting, 1) == 1)
				return;

			try
			{
				WriteReconnectLogLimited();

				SafeDisposeTester();

				_config = S7ConfigXmlHelper.Load();
				if (_config == null || _config.Basic == null)
					return;

				_activeChannelCount = GetActiveChannelCount(_config);
				LoadAddressMaps();

				_tester = new S7AddressTester(_config.Basic);
				OperateResult connect = _tester.Connect();
				if (!connect.IsSuccess)
				{
					_started = false;
					SafeDisposeTester();
					return;
				}

				_started = true;
				_reconnectMode = false;

				InitializeOutputsAfterConnected();
				EnsureRuntimeTimers();
				StopReconnectTimer();

				WriteLog("S7重连成功.");
			}
			catch (Exception ex)
			{
				WriteRuntimeFailureLimited("S7 reconnect failed: " + RootMessage(ex));
				_started = false;
				SafeDisposeTester();
			}
			finally
			{
				System.Threading.Interlocked.Exchange(ref _reconnecting, 0);
			}
		}

		private void WriteReconnectLogLimited()
		{
			DateTime now = DateTime.Now;

			lock (_connectionStateLock)
			{
				if ((now - _lastReconnectLogTime).TotalSeconds < 5)
					return;

				_lastReconnectLogTime = now;
			}

			WriteLog("S7重连中...");
		}

		// Connection Test proves only that a TCP/S7 session can be opened. The
		// runtime additionally reads and writes the configured DB points. Keep the
		// first concrete failure visible without changing reconnect behaviour or
		// flooding the operator log every poll cycle.
		private void WriteRuntimeFailureLimited(string message)
		{
			DateTime now = DateTime.Now;
			lock (_connectionStateLock)
			{
				if (string.Equals(_lastRuntimeFailure, message, StringComparison.Ordinal) &&
					(now - _lastRuntimeFailureLogTime).TotalSeconds < 5)
					return;
				_lastRuntimeFailure = message;
				_lastRuntimeFailureLogTime = now;
			}
			WriteLog(message + " Check the saved S7 table address, PLC DB access/PUT-GET permission, and DB optimization setting.");
		}

		private static string RootMessage(Exception ex)
		{
			return (ex == null ? null : ex.GetBaseException().Message) ?? "Unknown error";
		}

		private void EnsureRuntimeTimers()
		{
			int poll = 50;

			if (_config != null && _config.Basic != null && _config.Basic.PollIntervalMs > 0)
				poll = _config.Basic.PollIntervalMs;

			if (_pollTimer == null)
				_pollTimer = new ThreadingTimer(PollTimerCallback, null, poll, poll);

			if (_config != null && _config.Basic != null && _config.Basic.HeartbeatEnable && _heartbeatTimer == null)
			{
				int hb = _config.Basic.HeartbeatIntervalMs;
				if (hb <= 0)
					hb = 1000;

				_heartbeatTimer = new ThreadingTimer(HeartbeatTimerCallback, null, hb, hb);
			}
		}

		private void InitializeOutputsAfterConnected()
		{
			lock (_serviceWriteLock)
			{
				_initializingOutputs = true;
				try
				{
					SafeWriteBoolOutput(GlobalChannel, "Online", false);
					SafeWriteBoolOutput(GlobalChannel, "System Ready", false);
					SafeWriteBoolOutput(GlobalChannel, "System Busy", false);
					_lastTriggerEnable = ReadTriggerEnable();
					_bufferResults = ReadBoolInput(GlobalChannel, "Buffer Results Enable");
					_resultQueues.Clear();
					for (int ch = 0; ch < ActiveChannelCount; ch++)
					{
						_acquiring[ch] = false;
						SafeWriteBoolOutput(ch, "Trigger Ready Camera", false);
						SafeWriteBoolOutput(ch, "Trigger Ack Camera", false);
						SafeWriteBoolOutput(ch, "SetUserData Ack", false);
						SafeWriteBoolOutput(ch, "Result Valid", false);
						_resultValid[ch] = false;
						_inspectionComplete[ch] = false;
						SafeWriteBoolOutput(ch, "Inspection Complete", false);
						SafeWriteBoolOutput(ch, "Results Buffer Overrun", false);
						SafeWriteBoolOutput(ch, "Exposure Complete", false);
						SafeWriteFloatOutput(ch, "Result X", 0);
						SafeWriteFloatOutput(ch, "Result Y", 0);
						SafeWriteFloatOutput(ch, "Result A", 0);
						WriteChannelSummaryResult(ch, 0);
						SafeWriteByteOutput(ch, "JobID", 0);
						SafeWriteByteOutput(ch, "PosID", 0);
						// Capture current levels: do not replay a trigger left high before recovery.
						CommunicationChannelInput input = ReadChannelInput(ch);
						_lastTrigger[ch] = input.TriggerCamera;
						_lastAck[ch] = input.InspectionResultAcknowledge;
						_lastSetUserData[ch] = false; // PLC may resend user data after initialization.
						_lastClearExposureComplete[ch] = input.ClearExposureComplete;
						_lastInputSnapshot[ch] = string.Empty;
					}
					for (int ch = 0; ch < ActiveChannelCount; ch++)
						SafeWriteBoolOutput(ch, "Trigger Ready Camera", _cameraReady && _onlineRequested && _lastTriggerEnable);
					SafeWriteBoolOutput(GlobalChannel, "Online", _onlineRequested && _cameraReady);
					SafeWriteBoolOutput(GlobalChannel, "System Ready", _onlineRequested && _cameraReady);
					_lastOnlinePublish = DateTime.UtcNow;
				}
				finally { _initializingOutputs = false; }
			}
		}

		private static bool IsCommunicationBroken(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return false;

			string s = message.ToLowerInvariant();

			return s.Contains("connect")
				|| s.Contains("connection")
				|| s.Contains("socket")
				|| s.Contains("timeout")
				|| s.Contains("timed out")
				|| s.Contains("remote host")
				|| s.Contains("refused")
				|| s.Contains("closed")
				|| s.Contains("abort")
				|| s.Contains("network")
				|| s.Contains("tcp")
				|| s.Contains("offline")
				|| s.Contains("断开")
				|| s.Contains("连接")
				|| s.Contains("超时")
				|| s.Contains("网络");
		}


		private void PollTimerCallback(object state)
		{
			PollConnected();
		}

		private void PollConnected()
		{
			if (System.Threading.Interlocked.Exchange(ref _polling, 1) == 1) return;
			try
			{
				var notifications = new List<Action>();
				lock (_serviceWriteLock) PollCore(notifications);
				// Handlers may wait for a camera gate and write outputs. Never hold
				// the service lock while invoking application code (lock inversion).
				foreach (Action notification in notifications)
				{
					if (!_cameraReady || !_onlineRequested || !_started || _stopRequested) break;
					notification();
				}
			}
			catch (Exception ex) { WriteRuntimeFailureLimited("S7 polling failed: " + RootMessage(ex)); EnterReconnectMode(); }
			finally { System.Threading.Interlocked.Exchange(ref _polling, 0); }
		}

		private void PollCore(List<Action> notifications)
		{
			if (!_started || _tester == null)
				return;

			try
			{
				if ((DateTime.UtcNow - _lastOnlinePublish).TotalSeconds >= 1)
				{
					SafeWriteBoolOutput(GlobalChannel, "Online", _onlineRequested && _cameraReady);
					_lastOnlinePublish = DateTime.UtcNow;
				}
				if (!_cameraReady || !_onlineRequested || !_started) return;
				bool triggerEnable = ReadTriggerEnable();
				bool buffering = ReadBoolInput(GlobalChannel, "Buffer Results Enable");
				if (buffering != _bufferResults)
				{
					// Explicit mode changes discard waiting snapshots; published data is retained.
					_resultQueues.Clear();
					_bufferResults = buffering;
					WriteLog("S7 Buffer Results Enable=" + buffering + "; waiting result queues cleared.");
				}

				if (triggerEnable != _lastTriggerEnable)
				{
					_lastTriggerEnable = triggerEnable;
					ApplyTriggerEnableState(triggerEnable);
				}

				for (int ch = 0; ch < ActiveChannelCount; ch++)
				{
					CommunicationChannelInput input = ReadChannelInput(ch);
					if (input.InspectionResultAcknowledge)
					{
						if (_resultValid[ch])
						{
							if (!WriteOutputPoint(ch, "Result Valid", "false")) throw new IOException("Result acknowledgement write failed. CH=" + ch);
							_resultValid[ch] = false;
						}
					}
					else if (_bufferResults && !_resultValid[ch] && Results(ch).Count > 0)
					{
						PublishResult(ch, Results(ch).Peek());
						Results(ch).Dequeue();
					}

					bool notifyInputChanged = false;

					// Inspection Result Acknowledge 上升沿：PLC 已取走结果，视觉只清 Result Valid。
					if (input.InspectionResultAcknowledge && !_lastAck[ch])
					{
						notifyInputChanged = true;

						Action<int> ackHandler = ResultAckReceived;
						if (ackHandler != null)
							{
								int channel = ch;
								notifications.Add(() => ackHandler(channel));
							}
					}

					// NDM level handshake: Clear held high keeps Exposure Complete reset.
					if (input.ClearExposureComplete)
						SafeWriteBoolOutput(ch, "Exposure Complete", false);
					// Preserve the existing rising-edge application notification.
					if (input.ClearExposureComplete && !_lastClearExposureComplete[ch])
					{
						notifyInputChanged = true;
					}

					// SetUserDataAck 跟随 PLC SetUserData 电平。
					// SetUserData 上升沿时，只读取一次 ClearResult / JobID / PosID / PartN。
					if (input.SetUserData != _lastSetUserData[ch])
					{
						SafeWriteBoolOutput(ch, "SetUserData Ack", input.SetUserData);

						if (input.SetUserData)
						{
							ReadUserDataFields(input);
							notifyInputChanged = true;
						}

						_lastSetUserData[ch] = input.SetUserData;
					}

					if (notifyInputChanged)
					{
						Action<CommunicationChannelInput> inputHandler = InputChanged;
						if (inputHandler != null)
							notifications.Add(() => inputHandler(input));
					}

					_lastAck[ch] = input.InspectionResultAcknowledge;
					_lastClearExposureComplete[ch] = input.ClearExposureComplete;

					// TriggerAck 跟随 PLC Trigger 电平，但前提是全局 Trigger Enable = true。
					if (!triggerEnable)
					{
						if (_lastTrigger[ch] || input.TriggerCamera)
							SafeWriteBoolOutput(ch, "Trigger Ack Camera", false);

						_lastTrigger[ch] = input.TriggerCamera;
						continue;
					}

					if (input.TriggerCamera != _lastTrigger[ch])
					{
						SafeWriteBoolOutput(ch, "Trigger Ack Camera", input.TriggerCamera);

						if (input.TriggerCamera)
						{
							Action<int> triggerHandler = TriggerRequested;
							if (triggerHandler != null)
							{
								int channel = ch;
								notifications.Add(() => triggerHandler(channel));
							}
						}

						_lastTrigger[ch] = input.TriggerCamera;
					}
				}
			}
			catch (Exception ex)
			{
				WriteRuntimeFailureLimited("S7 polling failed: " + RootMessage(ex));
				EnterReconnectMode();
			}
		}

		private void HeartbeatTimerCallback(object state)
		{
			lock (_serviceWriteLock) HeartbeatCore();
		}

		private void HeartbeatCore()
		{
			try
			{
				if (_tester == null || !_started)
					return;

				OperateResult result = _tester.WriteHeartbeatOnce();
				if (!result.IsSuccess && IsCommunicationBroken(result.Message))
				{
					EnterReconnectMode();
				}
			}
			catch (Exception ex)
			{
				WriteRuntimeFailureLimited("S7 heartbeat failed: " + RootMessage(ex));
				EnterReconnectMode();
			}
		}

		private bool ReadTriggerEnable()
		{
			string value;

			// 新版推荐：Channel = -1 表示全局 Trigger Enable。
			if (ReadInputPoint(GlobalChannel, "Trigger Enable", out value))
				return IsTrue(value);

			// 兼容旧 XML：旧表可能把 Trigger Enable 放在 Channel 0。
			if (ReadInputPoint(0, "Trigger Enable", out value))
				return IsTrue(value);

			// 没有配置 Trigger Enable 时，安全起见默认不允许 Ready / Trigger。
			return false;
		}

		private void ApplyTriggerEnableState(bool enabled)
		{
			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				SafeWriteBoolOutput(ch, "Trigger Ready Camera", enabled && _cameraReady && _onlineRequested && !_acquiring[ch]);

				if (!enabled)
					SafeWriteBoolOutput(ch, "Trigger Ack Camera", false);
			}

			WriteLog("S7 Trigger Enable = " + enabled.ToString());
		}

		public CommunicationChannelInput ReadChannelInput(int channel)
		{
			CommunicationChannelInput input = new CommunicationChannelInput();
			input.Channel = channel;

			// 常规轮询只读取握手/控制信号。
			// ClearResult / JobID / PosID / PartN 只在 SetUserData 上升沿时读取一次。
			input.TriggerCamera = ReadBoolInput(channel, "Trigger Camera");
			input.InspectionResultAcknowledge = ReadBoolInput(channel, "Inspection Result Acknowledge");
			input.ClearExposureComplete = ReadBoolInput(channel, "Clear Exposure Complete");
			input.SetUserData = ReadBoolInput(channel, "Set User Data");

			return input;
		}

		private void ReadUserDataFields(CommunicationChannelInput input)
		{
			if (input == null)
				return;

			int channel = input.Channel;

			// ClearResult 与 JobID / PosID 同理，只在 SetUserData 上升沿读取一次。
			// FormMain 中 ClearResult 优先级高于 Job/Pos 切换。
			input.ClearResult = ReadByteInput(channel, "Clear Result");
			input.JobId = ReadByteInput(channel, "JobID");
			input.PosId = ReadByteInput(channel, "PosID");
			input.PartNo = ReadStringInput(channel, "PartN");
		}

		private string MakeInputSnapshot(CommunicationChannelInput input)
		{
			if (input == null)
				return string.Empty;

			return input.Channel + "|" +
				   input.InspectionResultAcknowledge + "|" +
				   input.ClearExposureComplete + "|" +
				   input.SetUserData + "|" +
				   input.ClearResult + "|" +
				   input.JobId + "|" +
				   input.PosId + "|" +
				   (input.PartNo ?? string.Empty);
		}

		public void NotifyInspectionStart(int channel)
		{
			lock (_serviceWriteLock)
			{
                if(channel<0 || channel>=ActiveChannelCount)return;
                _acquiring[channel]=true;
				// TriggerAck 不在这里控制。
				// TriggerAck 由 PollTimerCallback 根据 PLC Trigger 电平直接控制。
				SafeWriteBoolOutput(channel, "Trigger Ready Camera", false);

				// Exposure Complete is latched until PLC Clear Exposure Complete.
			}
		}

		public void NotifyInspectionEnd(int channel)
		{
			lock (_serviceWriteLock)
			{
				if (!_cameraReady || !_onlineRequested || !_started) return;
				// Exposure Complete is asserted at acquisition, not at inspection end.
				// In particular, do not reassert it if the PLC cleared it during analysis.
				// Acquisition completion, not analysis completion, owns Trigger Ready.
			}
		}
        public void NotifyAcquisitionComplete(int channel)
        {
            lock(_serviceWriteLock)
            {
                if(!_cameraReady||!_onlineRequested||!_started||channel<0||channel>=ActiveChannelCount)return;
                string clear;
                // Read the current PLC level, not the previous polling snapshot.
                // A failed read must never be treated as permission to assert.
                if(!ReadInputPoint(channel,"Clear Exposure Complete",out clear))
                {
                    WriteLog("S7 acquisition complete: cannot read Clear Exposure Complete. CH="+channel+"; Exposure Complete was not asserted.");
                    return;
                }
                bool complete=!IsTrue(clear);
                SafeWriteBoolOutput(channel,"Exposure Complete",complete);
                _acquiring[channel]=false;
                SafeWriteBoolOutput(channel,"Trigger Ready Camera",ReadTriggerEnable() && _cameraReady && _onlineRequested && _started);
                WriteLog("S7 acquisition complete. CH="+channel+"; Exposure Complete="+complete+"; inspection is not yet complete.");
            }
        }

		public void WriteJobPosEcho(int channel, byte jobId, byte posId)
		{
			lock (_serviceWriteLock)
			{
                if(!_cameraReady||!_onlineRequested||!_started||channel<0||channel>=ActiveChannelCount)
                    throw new InvalidOperationException("S7 Job/Pos echo was not sent: transport is not Online/ready.");
                _echoJob[channel]=jobId;_echoPos[channel]=posId;
                try
                {
                    var packet=new List<KeyValuePair<S7PointItem,string>>();
                    packet.Add(new KeyValuePair<S7PointItem,string>(_outputMap[MakeKey(channel,"JobID")],jobId.ToString(CultureInfo.InvariantCulture)));
                    packet.Add(new KeyValuePair<S7PointItem,string>(_outputMap[MakeKey(channel,"PosID")],posId.ToString(CultureInfo.InvariantCulture)));
                    // Match the Profinet metadata frame: clear this channel's old
                    // measurements without requiring a VPP run or touching other channels.
                    foreach(var point in _outputMap.Values.Where(p=>p.Channel==channel&&!S7OutputData.IsFixed(p.Parameter)))
                    {
                        string value=point.Format.Equals("String",StringComparison.OrdinalIgnoreCase)?new string('\0',point.Length):point.Format.Equals("Bool",StringComparison.OrdinalIgnoreCase)?"false":"0";
                        packet.Add(new KeyValuePair<S7PointItem,string>(point,value));
                    }
                    SubmitResultFrame(channel,packet);
                    WriteLog("S7 Job/Pos echo accepted. CH="+channel+", JobID="+jobId+", PosID="+posId+"; Result Valid / Inspection Complete use result-frame handshake.");
                }
                catch(Exception ex)
                {
                    WriteLog("S7 Job/Pos echo failed. CH="+channel+"; "+ex.Message);
                    throw;
                }
			}
		}

        public void WriteInspectionResult(int channel, float x, float y, float a, short resultCode)
        {
            WriteInspectionResult(channel, x, y, a, resultCode, null, null, null);
        }

        // Only S7 consumes these snapshots. The shared handshake contract and Profinet remain unchanged.
        public bool WriteInspectionResult(int channel, float x, float y, float a, short resultCode,
            string job, string position, IList<S7InspectionOutputs> sources)
        {
            lock (_serviceWriteLock)
            {
                if (!_cameraReady || !_onlineRequested || !_started || channel < 0 || channel >= ActiveChannelCount) return false;
                try
                {
                    var pending = new List<KeyValuePair<S7PointItem, string>>();
                    foreach (S7PointItem point in _outputMap.Values.Where(p => p.Channel == channel && !S7OutputData.IsFixed(p.Parameter)))
                    {
                        // Result is an alias of Result{channel}, never send it twice.
                        S7PointItem canonical;
                        if (point.Parameter.Equals("Result", StringComparison.OrdinalIgnoreCase) && _outputMap.TryGetValue(MakeKey(channel, "Result" + channel), out canonical) && string.Equals(canonical.DbAddress, point.DbAddress, StringComparison.OrdinalIgnoreCase)) continue;
                        object value;
                        if (!string.IsNullOrWhiteSpace(point.VppOutput))
                        {
                            var matches = sources == null ? new List<S7InspectionOutputs>() : sources.Where(v => v != null &&
                                string.Equals(v.Job, job, StringComparison.OrdinalIgnoreCase) && string.Equals(v.Position, position, StringComparison.OrdinalIgnoreCase) &&
                                v.Values != null && v.Values.ContainsKey(point.VppOutput)).ToList();
                            if (matches.Count == 0 || !matches[0].Succeeded || !matches[0].Values.TryGetValue(point.VppOutput, out value))
                                throw new InvalidOperationException("Missing/failed current-cycle VPP output: " + point.VppOutput + " -> " + point.Parameter);
                            // FormMain orders the original summary-result camera first. Never use a previous-cycle value.

                        }
                        else if (point.Parameter.Equals("Result X", StringComparison.OrdinalIgnoreCase)) value = x;
                        else if (point.Parameter.Equals("Result Y", StringComparison.OrdinalIgnoreCase)) value = y;
                        else if (point.Parameter.Equals("Result A", StringComparison.OrdinalIgnoreCase)) value = a;
                        else if (S7OutputData.IsLegacy(point.Parameter, channel)) value = resultCode;
                        else throw new InvalidOperationException("VPP output is not bound: " + point.Parameter);
                        pending.Add(new KeyValuePair<S7PointItem, string>(point, S7OutputData.Serialize(value, point.Format, point.Length)));
                    }
                    pending.Insert(0, new KeyValuePair<S7PointItem, string>(_outputMap[MakeKey(channel, "JobID")], _echoJob[channel].ToString(CultureInfo.InvariantCulture)));
                    pending.Insert(1, new KeyValuePair<S7PointItem, string>(_outputMap[MakeKey(channel, "PosID")], _echoPos[channel].ToString(CultureInfo.InvariantCulture)));
                    SubmitResultFrame(channel,pending);
                    return true;
                }
                catch (Exception ex)
                {
                    if (!_bufferResults) { WriteOutputPoint(channel, "Result Valid", "false"); _resultValid[channel] = false; }
                    WriteLog("S7 result not published. CH=" + channel + "; Result Valid not asserted; " + ex.Message);
                    return false;
                }
            }
        }

        // Called under _serviceWriteLock for both metadata echo and inspection results.
        private void SubmitResultFrame(int channel,List<KeyValuePair<S7PointItem,string>> pending)
        {
                    string ackValue;
                    if(!ReadInputPoint(channel,"Inspection Result Acknowledge",out ackValue))
                        throw new IOException("Cannot read Inspection Result Acknowledge. CH="+channel);
                    bool ack = IsTrue(ackValue);
                    if (_bufferResults && (_resultValid[channel] || ack || Results(channel).Count > 0))
                    {
                        // Sixteen outstanding result sets including the currently published set.
                        if (Results(channel).Count + (_resultValid[channel] ? 1 : 0) >= 16)
                        {
                            SafeWriteBoolOutput(channel, "Results Buffer Overrun", true);
                            bool droppedToggle = !_inspectionComplete[channel];
                            if (!WriteOutputPoint(channel, "Inspection Complete", droppedToggle.ToString())) throw new IOException("Inspection Complete write failed.");
                            _inspectionComplete[channel] = droppedToggle;
                            WriteLog("S7 results buffer overrun. CH=" + channel + "; newest result dropped, capacity=16.");
                            return;
                        }
                        Results(channel).Enqueue(pending);
                    }
                    else
                    {
                        PublishResult(channel, pending);
                        if (ack)
                        {
                            if (!WriteOutputPoint(channel, "Result Valid", "false")) throw new IOException("Result Valid acknowledgement failed.");
                            _resultValid[channel] = false;
                        }
                    }
                    SafeWriteBoolOutput(channel, "Results Buffer Overrun", false);
                    bool completed = !_inspectionComplete[channel];
                    if (!WriteOutputPoint(channel, "Inspection Complete", completed.ToString())) throw new IOException("Inspection Complete write failed.");
                    _inspectionComplete[channel] = completed;
                    WriteLog("S7 inspection completed. CH=" + channel + ", buffered=" + _bufferResults + ", queued=" + Results(channel).Count + ", ResultValid=" + _resultValid[channel] + ", Complete=" + completed);
        }

		private void WriteChannelSummaryResult(int channel, short resultCode)
		{
			string value = resultCode.ToString();

			// 新命名：Result0 / Result1 / Result2 / Result3
			if (WriteOutputPoint(channel, "Result" + channel.ToString(), value))
				return;

			// 兼容旧命名：Result
			WriteOutputPoint(channel, "Result", value);
		}

		public void ClearInspectionResult(int channel)
		{
			lock (_serviceWriteLock)
			{
				// Ack notifications are dispatched asynchronously. Ignore a stale callback
				// after a newer result has already been published on Ack falling edge.
				if (_bufferResults || !ReadBoolInput(channel, "Inspection Result Acknowledge")) return;
				// PLC 采集完 ResultX/Y/A/Result0/Result1 后，发送 Inspection Result Acknowledge。
				// 视觉侧只把 Result Valid 复位为 false，表示本次结果已被 PLC 取走。
				// TriggerAck 只跟随 PLC Trigger 电平。
				// Exposure Complete 由 Clear Exposure Complete 单独复位。
				// ResultX/Y/A/Result0/Result1 保留最后一次结果，下一次检测覆盖。
				SafeWriteBoolOutput(channel, "Result Valid", false);
				_resultValid[channel] = false;

				SafeWriteBoolOutput(channel, "Trigger Ready Camera", _cameraReady && _onlineRequested && _lastTriggerEnable && !_acquiring[channel]);
			}
		}

		public void ClearAllInspectionResult(int channel)
		{
			lock (_serviceWriteLock)
			{
				Results(channel).Clear(); _resultValid[channel] = false;
				SafeWriteBoolOutput(channel, "Results Buffer Overrun", false);
				// ClearResult 指令使用：清除上一轮检测所有结果输出。
				// TriggerAck 只跟随 PLC Trigger 电平，不在这里强制清。
				SafeWriteBoolOutput(channel, "Result Valid", false);
				// Clear Result does not acknowledge acquisition completion.

				SafeWriteFloatOutput(channel, "Result X", 0);
				SafeWriteFloatOutput(channel, "Result Y", 0);
				SafeWriteFloatOutput(channel, "Result A", 0);
				WriteChannelSummaryResult(channel, 0);

				SafeWriteBoolOutput(channel, "Trigger Ready Camera", _cameraReady && _onlineRequested && _lastTriggerEnable && !_acquiring[channel]);
			}
		}

		public void ClearExposureComplete(int channel)
		{
			lock (_serviceWriteLock)
			{
				// PLC 发送 Clear Exposure Complete 后，视觉侧复位 Exposure Complete。
				SafeWriteBoolOutput(channel, "Exposure Complete", false);
			}
		}

		private bool ReadBoolInput(int channel, string parameter)
		{
			string value;
			if (!ReadInputPoint(channel, parameter, out value))
				return false;

			return IsTrue(value);
		}

		private byte ReadByteInput(int channel, string parameter)
		{
			string value;
			if (!ReadInputPoint(channel, parameter, out value))
				return 0;

			byte result;
			if (byte.TryParse(value, out result))
				return result;

			return 0;
		}

		private string ReadStringInput(int channel, string parameter)
		{
			string value;
			if (!ReadInputPoint(channel, parameter, out value))
				return string.Empty;

			return value ?? string.Empty;
		}

		private bool ReadInputPoint(int channel, string parameter, out string value)
		{
			value = string.Empty;

			S7PointItem point;
			if (!_inputMap.TryGetValue(MakeKey(channel, parameter), out point))
				return false;

			if (point == null || string.IsNullOrWhiteSpace(point.DbAddress))
				return false;

			OperateResult<string> result = _tester.ReadAsString(point.DbAddress, point.DataType, point.Length);

			if (!result.IsSuccess)
			{
				if (_initializingOutputs) throw new IOException("S7 initialization read failed: " + parameter + ": " + result.Message);
				if (IsCommunicationBroken(result.Message))
				{
					EnterReconnectMode();
					return false;
				}

				if (!_reconnectMode)
					WriteReadErrorLogLimited(channel, parameter, result.Message);

				return false;
			}

			value = result.Content;
			return true;
		}

		private void WriteReadErrorLogLimited(int channel, string parameter, string message)
		{
			string key = channel.ToString() + "|" + parameter;
			DateTime now = DateTime.Now;

			lock (_lastReadErrorLogTime)
			{
				DateTime last;
				if (_lastReadErrorLogTime.TryGetValue(key, out last))
				{
					if ((now - last).TotalSeconds < 5)
						return;
				}

				_lastReadErrorLogTime[key] = now;
			}

			WriteLog("S7 read input failed. CH=" + channel + ", " + parameter + ", " + message);
		}

		private void SafeWriteBoolOutput(int channel, string parameter, bool value)
		{
			WriteOutputPoint(channel, parameter, value ? "true" : "false");
		}

		private void SafeWriteByteOutput(int channel, string parameter, byte value)
		{
			WriteOutputPoint(channel, parameter, value.ToString());
		}

		private void SafeWriteInt16Output(int channel, string parameter, short value)
		{
			WriteOutputPoint(channel, parameter, value.ToString());
		}

		private void SafeWriteFloatOutput(int channel, string parameter, float value)
		{
			WriteOutputPoint(channel, parameter, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
		}

		private bool WriteOutputPoint(int channel, string parameter, string value)
		{
			if (_tester == null)
				return false;

			S7PointItem point;

			// 1. 先按指定通道找，例如 CH0 Result0、CH1 Result1。
			if (!_outputMap.TryGetValue(MakeKey(channel, parameter), out point))
			{
				// 2. 再按全局点位找，例如 System Ready / System Busy / Online。
				if (!_outputMap.TryGetValue(MakeKey(GlobalChannel, parameter), out point))
				{
					// 3. 兼容旧 XML：旧表可能把全局点位放在 Channel 0。
					if (!_outputMap.TryGetValue(MakeKey(0, parameter), out point))
						return false;
				}
			}

			if (point == null || string.IsNullOrWhiteSpace(point.DbAddress))
				return false;

			OperateResult result = _tester.WriteByString(point.DbAddress, point.DataType, value);
			if (!result.IsSuccess)
			{
				string detail = parameter + " [" + point.DbAddress + ", " + point.Format + "]: " + result.Message;
				if (_initializingOutputs) throw new IOException("S7 initialization write failed: " + detail);
				if (IsCommunicationBroken(result.Message))
				{
					EnterReconnectMode();
					return false;
				}

				if (!_reconnectMode)
					WriteLog("S7 write output failed. CH=" + channel + ", " + detail);

				return false;
			}

			return true;
		}

		private void LoadAddressMaps()
		{
			_inputMap.Clear();
			_outputMap.Clear();

			LoadMapFromXml(ProjectPaths.Config("Communication/S7/S7InputTableConfig.xml"), _inputMap);
			LoadMapFromXml(ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml"), _outputMap);

			if (_inputMap.Count == 0)
				LoadDefaultInputMap();

			if (_outputMap.Count == 0)
				LoadDefaultOutputMap();

			EnsureResultSummaryAliases();
			EnsureNdmExtensions();
			ValidateRequiredSignals();

			WriteLog("S7 address map loaded. Input=" + _inputMap.Count + ", Output=" + _outputMap.Count);
		}

		private void EnsureNdmExtensions()
		{
			EnsureNdmPoint(_inputMap, GlobalChannel, "Buffer Results Enable", "Trigger Enable", 1);
			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				EnsureNdmPoint(_outputMap, ch, "Inspection Complete", "Result Valid", 4);
				EnsureNdmPoint(_outputMap, ch, "Results Buffer Overrun", "Result Valid", 6);
			}
		}
		private void EnsureNdmPoint(Dictionary<string, S7PointItem> map, int channel, string name, string reference, int bit)
		{
			if (map.ContainsKey(MakeKey(channel, name))) return;
			S7PointItem anchor;
			if (!map.TryGetValue(MakeKey(channel, reference), out anchor)) return;
			string address = S7OutputData.NdmAddress(anchor.DbAddress, bit);
			long key = S7TableEditor.AddressSortKey(address);
			foreach (var point in map.Values)
			{
				long start = S7TableEditor.AddressSortKey(point.DbAddress);
				long length = point.Format == "Bool" ? 1 : (long)point.Length * 8;
				if (key >= start && key < start + length)
					throw new InvalidOperationException("S7 NDM address conflict: " + name + " " + address + " overlaps " + point.Parameter + ". Configure the new signal in the address table.");
			}
			map[MakeKey(channel, name)] = new S7PointItem { Channel = channel, Parameter = name, DbAddress = address, Format = "Bool", Length = 1 };
			WriteLog("S7 legacy map extended: CH=" + channel + " " + name + "=" + address);
		}
		private void ValidateRequiredSignals()
		{
			RequireSignal(_inputMap, GlobalChannel, "Trigger Enable");
			RequireSignal(_inputMap, GlobalChannel, "Buffer Results Enable");
			foreach (string name in new[] { "System Ready", "System Busy", "Online" }) RequireSignal(_outputMap, GlobalChannel, name);
			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				RequireSignal(_outputMap, ch, "Inspection Complete");
				RequireSignal(_outputMap, ch, "Results Buffer Overrun");
				foreach (string name in new[] { "Trigger Camera", "Inspection Result Acknowledge", "Clear Exposure Complete", "Set User Data", "Clear Result", "JobID", "PosID", "PartN" }) RequireSignal(_inputMap, ch, name);
				foreach (string name in new[] { "Trigger Ready Camera", "Trigger Ack Camera", "Exposure Complete", "Result Valid", "SetUserData Ack", "JobID", "PosID" }) RequireSignal(_outputMap, ch, name);
			}
		}

		private void RequireSignal(Dictionary<string, S7PointItem> map, int channel, string name)
		{
			S7PointItem point;
			if (!map.TryGetValue(MakeKey(channel, name), out point) || string.IsNullOrWhiteSpace(point.DbAddress))
			{
				string message = "S7 required address missing: CH" + channel + " " + name + ". Complete and save the input/output tables before going Online.";
				WriteLog(message);
				throw new InvalidOperationException(message);
			}
		}

		private void LoadMapFromXml(string path, Dictionary<string, S7PointItem> map)
		{
			try
			{
				if (!File.Exists(path))
					return;

				XDocument doc = XDocument.Load(path);

				foreach (XElement e in doc.Descendants())
				{
					string name = e.Name.LocalName;
					if (!name.EndsWith("TableRow", StringComparison.OrdinalIgnoreCase))
						continue;

					int channel = ToInt(GetChildValue(e, "Channel"), 0);
					string parameter = GetChildValue(e, "Parameter");
					string address = GetChildValue(e, "DbAddress");
					string lengthText = GetChildValue(e, "Length");
					string format = GetChildValue(e, "Format");

					if (string.IsNullOrWhiteSpace(parameter))
						continue;

					S7PointItem item = new S7PointItem();
					item.Channel = channel;
					item.Parameter = parameter.Trim();
					item.DbAddress = address.Trim();
					item.Format = NormalizeFormat(format);
					item.Length = ToUShort(lengthText, 1);
                    item.VppOutput = GetChildValue(e, "VppOutput").Trim();

					map[MakeKey(item.Channel, item.Parameter)] = item;
				}
			}
			catch (Exception ex)
			{
				WriteLog("Load S7 map failed: " + path + ", " + ex.Message);
			}
		}

		private void EnsureResultSummaryAliases()
		{
			// 兼容旧 XML：
			// 如果旧表里只有 Channel|Result，就补一个 Channel|Result0/1/2/3 别名指向同一个地址。
			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				string newKey = MakeKey(ch, "Result" + ch.ToString());
				if (_outputMap.ContainsKey(newKey))
					continue;

				S7PointItem oldItem;
				if (_outputMap.TryGetValue(MakeKey(ch, "Result"), out oldItem) && oldItem != null)
				{
					S7PointItem alias = new S7PointItem();
					alias.Channel = ch;
					alias.Parameter = "Result" + ch.ToString();
					alias.DbAddress = oldItem.DbAddress;
					alias.Format = oldItem.Format;
					alias.Length = oldItem.Length;
                    alias.VppOutput = oldItem.VppOutput;
					_outputMap[newKey] = alias;
					continue;
				}


			}
		}

		private void LoadDefaultInputMap()
		{
			string inputDb = "DB100";
			int inputBase = 0;

			if (_config != null && _config.Basic != null)
			{
				inputDb = GetDbName(_config.Basic.InputStartAddress, "DB100");
				inputBase = GetBaseByte(_config.Basic.InputStartAddress);
			}

			AddInput(GlobalChannel, "Trigger Enable", MakeBoolAddress(inputDb, inputBase + 0, 0), "Bool", 1);

			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				int bitByte = inputBase + 16 + ch;
				int clearResultByte = inputBase + 20 + ch;
				int jobIdByte = inputBase + 24 + ch;
				int posIdByte = inputBase + 28 + ch;
				int partNoByte = inputBase + 32 + ch * 40;

				AddInput(ch, "Trigger Camera", MakeBoolAddress(inputDb, bitByte, 0), "Bool", 1);
				AddInput(ch, "Inspection Result Acknowledge", MakeBoolAddress(inputDb, bitByte, 1), "Bool", 1);
				AddInput(ch, "Clear Exposure Complete", MakeBoolAddress(inputDb, bitByte, 2), "Bool", 1);
				AddInput(ch, "Set User Data", MakeBoolAddress(inputDb, bitByte, 3), "Bool", 1);
				AddInput(ch, "Clear Result", MakeByteAddress(inputDb, clearResultByte), "Byte", 1);
				AddInput(ch, "JobID", MakeByteAddress(inputDb, jobIdByte), "Byte", 1);
				AddInput(ch, "PosID", MakeByteAddress(inputDb, posIdByte), "Byte", 1);
				AddInput(ch, "PartN", MakeByteAddress(inputDb, partNoByte), "String", 40);
			}
		}

		private void LoadDefaultOutputMap()
		{
			string outputDb = "DB101";
			int outputBase = 0;

			if (_config != null && _config.Basic != null)
			{
				outputDb = GetDbName(_config.Basic.OutputStartAddress, "DB101");
				outputBase = GetBaseByte(_config.Basic.OutputStartAddress);
			}

			// 全局输出，不属于 CH0。
			// Channel 使用 -1，仅用于配置表区分全局点位。
			AddOutput(GlobalChannel, "System Ready", MakeBoolAddress(outputDb, outputBase + 0, 0), "Bool", 1);
			AddOutput(GlobalChannel, "System Busy", MakeBoolAddress(outputDb, outputBase + 0, 1), "Bool", 1);
			AddOutput(GlobalChannel, "Online", MakeBoolAddress(outputDb, outputBase + 0, 7), "Bool", 1);

			for (int ch = 0; ch < ActiveChannelCount; ch++)
			{
				int engineByte = outputBase + 16 + ch * 8;
				int engineAckByte = engineByte + 1;
				int jobIdByte = outputBase + 48 + ch;
				int posIdByte = outputBase + 52 + ch;
				int resultBase = outputBase + 56 + ch * 14;

				AddOutput(ch, "Trigger Ready Camera", MakeBoolAddress(outputDb, engineByte, 0), "Bool", 1);
				AddOutput(ch, "Trigger Ack Camera", MakeBoolAddress(outputDb, engineByte, 1), "Bool", 1);
				AddOutput(ch, "Exposure Complete", MakeBoolAddress(outputDb, engineByte, 3), "Bool", 1);
				AddOutput(ch, "Result Valid", MakeBoolAddress(outputDb, engineByte, 5), "Bool", 1);
				AddOutput(ch, "SetUserData Ack", MakeBoolAddress(outputDb, engineAckByte, 0), "Bool", 1);

				AddOutput(ch, "JobID", MakeByteAddress(outputDb, jobIdByte), "Byte", 1);
				AddOutput(ch, "PosID", MakeByteAddress(outputDb, posIdByte), "Byte", 1);
				AddOutput(ch, "Result X", MakeByteAddress(outputDb, resultBase), "Float", 4);
				AddOutput(ch, "Result Y", MakeByteAddress(outputDb, resultBase + 4), "Float", 4);
				AddOutput(ch, "Result A", MakeByteAddress(outputDb, resultBase + 8), "Float", 4);
				AddOutput(ch, "Result" + ch.ToString(), MakeByteAddress(outputDb, resultBase + 12), "Int16", 2);
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

		private void AddInput(int ch, string parameter, string address, string format, ushort length)
		{
			S7PointItem item = new S7PointItem();
			item.Channel = ch;
			item.Parameter = parameter;
			item.DbAddress = address;
			item.Format = format;
			item.Length = length;
			_inputMap[MakeKey(ch, parameter)] = item;
		}

		private void AddOutput(int ch, string parameter, string address, string format, ushort length)
		{
			S7PointItem item = new S7PointItem();
			item.Channel = ch;
			item.Parameter = parameter;
			item.DbAddress = address;
			item.Format = format;
			item.Length = length;
			_outputMap[MakeKey(ch, parameter)] = item;
		}

		private static string MakeKey(int channel, string parameter)
		{
			return channel.ToString() + "|" + (parameter ?? string.Empty).Trim();
		}

		private static string GetChildValue(XElement parent, string childName)
		{
			XElement child = parent.Element(childName);
			if (child == null)
				return string.Empty;

			return (child.Value ?? string.Empty).Trim();
		}

		private static int ToInt(string value, int defaultValue)
		{
			int result;
			if (int.TryParse(value, out result))
				return result;

			return defaultValue;
		}

		private static ushort ToUShort(string value, ushort defaultValue)
		{
			ushort result;
			if (ushort.TryParse(value, out result))
				return result;

			return defaultValue;
		}

		private static bool IsTrue(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return false;

			string s = value.Trim().ToLower();
			return s == "true" || s == "1" || s == "yes" || s == "on";
		}

		private static string NormalizeFormat(string format)
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

		private static string FormatPlcFloat(float value)
		{
			return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
		}

		private void WriteLog(string msg)
		{
			Action<string> handler = LogMessage;
			if (handler != null)
				handler(msg);
		}

		public void Dispose()
		{
			Stop();
		}
	}

	public class S7PointItem
	{
		public int Channel { get; set; }
		public string Parameter { get; set; }
		public string DbAddress { get; set; }
		public string Format { get; set; }
		public ushort Length { get; set; }
        public string VppOutput { get; set; }

		public S7PointItem()
		{
			Parameter = string.Empty;
			DbAddress = string.Empty;
			Format = "Bool";
			Length = 1;
		}

		public S7PointDataType DataType
		{
			get
			{
				string text = Format;

				if (string.IsNullOrWhiteSpace(text))
					text = "Bool";

				if (string.Equals(text, "Bit", StringComparison.OrdinalIgnoreCase))
					text = "Bool";

				if (string.Equals(text, "Short", StringComparison.OrdinalIgnoreCase))
					text = "Int16";

				if (string.Equals(text, "Real", StringComparison.OrdinalIgnoreCase))
					text = "Float";

				try
				{
					return (S7PointDataType)Enum.Parse(typeof(S7PointDataType), text, true);
				}
				catch
				{
					return S7PointDataType.Bool;
				}
			}
		}
	}
}

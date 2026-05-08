using System;

namespace Aron_V2
{
	/// <summary>
	/// Adapts the four-channel Cognex CC24/FFP transport to the same logical
	/// handshake contract used by S7.
	/// </summary>
	public sealed class ProfinetVisionCommService : IVisionCommunicationService
	{
		private readonly object _sync = new object();
		private bool _started;
		private bool _cameraReady = true;
		private volatile bool _onlineRequested = true;

		public void SetOnline(bool online)
		{
			lock (_sync)
			{
				_onlineRequested = online;
				CC24_Comm.Instance().SetOperatingOnline(online && _cameraReady);
			}
		}

		public event Action<CommunicationChannelInput> InputChanged;
		public event Action<int> TriggerRequested;
		public event Action<int> ResultAckReceived;
		public event Action<string> LogMessage;

		public bool IsStarted { get { return _started && CC24_Comm.Instance().IsConnected; } }

		public void Start()
		{
			lock (_sync)
			{
				StopCore();
				if (!_cameraReady) return;

				CC24_Comm communication = CC24_Comm.Instance();
				communication.commType = CommType.Profinet;
				communication.SetOperatingOnline(_onlineRequested && _cameraReady);
				communication.NewTrigger += Communication_NewTrigger;
				communication.NewUserData += Communication_NewUserData;

				if (!communication.InitCommCard())
				{
					communication.NewTrigger -= Communication_NewTrigger;
					communication.NewUserData -= Communication_NewUserData;
					throw new InvalidOperationException("CC24 Profinet card was not found or could not be initialized.");
				}

				communication.InitFFP();
				_started = true;
				WriteLog("Profinet service started; waiting for controller connection.");
			}
		}

		public void Stop()
		{
			lock (_sync) StopCore();
		}

		private void StopCore()
		{
			CC24_Comm communication = CC24_Comm.Instance();
			communication.NewTrigger -= Communication_NewTrigger;
			communication.NewUserData -= Communication_NewUserData;
			if (_started) communication.Close();
			_started = false;
		}

		public void SetCameraReady(bool ready)
		{
			lock (_sync)
			{
				_cameraReady = ready;
				if (!ready) StopCore();
				else if (!_started) Start();
			}
		}

		private void Communication_NewTrigger(object sender, string channelText)
		{
			if (!_onlineRequested || !_cameraReady) return;
			int channel;
			if (!int.TryParse(channelText, out channel) || channel < 0 || channel >= CommunicationConfigHelper.ProfinetChannelCount)
			{
				WriteLog("Profinet trigger channel invalid: " + channelText);
				return;
			}

			Action<int> handler = TriggerRequested;
			if (handler != null) handler(channel);
		}

		private void Communication_NewUserData(object sender, byte[] data, int channel)
		{
			if (!_onlineRequested || !_cameraReady) return;
			if (channel < 0 || channel >= CommunicationConfigHelper.ProfinetChannelCount)
			{
				WriteLog("Profinet input channel invalid: " + channel);
				return;
			}

			try
			{
				Profinet.ProfinetCommConfig config = Profinet.ProfinetCommConfigHelper.Load();
				Func<string, object> read = name => Profinet.ProfinetDataMapper.Read(data,
					config.InputPoints.Find(x => x.Channel == channel && string.Equals(x.Parameter, name, StringComparison.OrdinalIgnoreCase)));
				bool clear = Profinet.ProfinetDataMapper.ToBool(read("Clear Result"));
				CommunicationChannelInput input = new CommunicationChannelInput
				{
					Channel = channel,
					// NewUserData is raised by the fixed NDM Set User Data signal for
					// this channel. The control bit is not part of configurable UserData.
					SetUserData = true,
					ClearResult = clear ? (byte)1 : (byte)0,
					JobId = Profinet.ProfinetDataMapper.ToByte(read("JobID")),
					PosId = Profinet.ProfinetDataMapper.ToByte(read("PosID")),
					PartNo = Convert.ToString(read("PartN"))
				};

				Action<CommunicationChannelInput> inputHandler = InputChanged;
				if (inputHandler != null) inputHandler(input);
				if (clear)
				{
					Action<int> ackHandler = ResultAckReceived;
					if (ackHandler != null) ackHandler(channel);
				}
			}
			catch (Exception ex)
			{
				WriteLog("Process Profinet input failed: " + ex.Message);
			}
		}

		public void NotifyInspectionStart(int channel)
		{
			// CC24 raises the acquisition trigger and owns the low-level trigger ACK.
			// Clearing the channel result buffer here matches the S7 new-cycle rule.
			Profinet.ProfinetDataMapper.ClearOutputs(channel, false);
		}

		public void NotifyInspectionEnd(int channel)
		{
			if (!_started || !_cameraReady || !_onlineRequested) return;
			CC24_Comm.Instance().SendSharedResultData(0, channel);
		}

		public void WriteJobPosEcho(int channel, byte jobId, byte posId)
		{
			if (channel < 0 || channel >= CommunicationConfigHelper.ProfinetChannelCount) return;
			Profinet.ProfinetDataMapper.WriteFixedOutput(channel, "JobID", jobId);
			Profinet.ProfinetDataMapper.WriteFixedOutput(channel, "PosID", posId);
		}

		public void WriteInspectionResult(int channel, float x, float y, float a, short resultCode)
		{
			// Profinet result bytes are produced by the existing VPPOutput mapping.
			// NotifyInspectionEnd publishes the completed buffer as one atomic frame.
		}

		public void ClearInspectionResult(int channel)
		{
			Profinet.ProfinetDataMapper.ClearOutputs(channel, false);
		}

		public void ClearAllInspectionResult(int channel)
		{
			Profinet.ProfinetDataMapper.ClearOutputs(channel, true);
		}

		public void ClearExposureComplete(int channel)
		{
			// Exposure-complete is represented by the CC24 inspection completion.
		}

		private void WriteLog(string message)
		{
			Action<string> handler = LogMessage;
			if (handler != null) handler(message);
		}

		public void Dispose()
		{
			Stop();
		}
	}
}

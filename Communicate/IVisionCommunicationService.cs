using System;

namespace Aron_V2
{
    // Optional capability preserves existing communication/plugin implementers.
    public interface IVisionAcquisitionNotifications
    {
        void NotifyAcquisitionComplete(int channel);
    }
	/// <summary>
	/// Protocol-independent vision/PLC handshake contract. S7 defines the
	/// standard business sequence; Profinet and TCP/IP adapters must expose the
	/// same events and state transitions through this interface.
	/// </summary>
	public interface IVisionCommunicationService : IDisposable
	{
		bool IsStarted { get; }

		event Action<CommunicationChannelInput> InputChanged;
		event Action<int> TriggerRequested;
		event Action<int> ResultAckReceived;
		event Action<string> LogMessage;

		void Start();
		void Stop();
		void SetCameraReady(bool ready);
		void SetOnline(bool online);
		void NotifyInspectionStart(int channel);
		void NotifyInspectionEnd(int channel);
		void WriteJobPosEcho(int channel, byte jobId, byte posId);
		void WriteInspectionResult(int channel, float x, float y, float a, short resultCode);
		void ClearInspectionResult(int channel);
		void ClearAllInspectionResult(int channel);
		void ClearExposureComplete(int channel);
	}
}

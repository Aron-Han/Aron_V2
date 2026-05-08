using System;

namespace Aron_V2
{
    /// <summary>
    /// Any enabled transport converts its input into this protocol-independent
    /// channel snapshot before handing it to FormMain.
    /// </summary>
    public class CommunicationChannelInput
    {
        public int Channel { get; set; }

        public bool TriggerCamera { get; set; }

        public bool InspectionResultAcknowledge { get; set; }

        public bool ClearExposureComplete { get; set; }

        public bool SetUserData { get; set; }

        public byte ClearResult { get; set; }

        public byte JobId { get; set; }

        public byte PosId { get; set; }

        public string PartNo { get; set; }

        public CommunicationChannelInput()
        {
            PartNo = string.Empty;
        }
    }

	/// <summary>Compatibility name for older extensions compiled against S7.</summary>
	[Obsolete("Use CommunicationChannelInput for protocol-independent code.")]
	public class S7ChannelInput : CommunicationChannelInput
	{
	}
}

using System;
using System.Collections.Generic;

namespace Aron_V2
{
	[Serializable]
	public class S7CommConfig
	{
		public S7BasicSetting Basic { get; set; }
		public List<S7InputChannelConfig> InputChannels { get; set; }
		public List<S7OutputChannelConfig> OutputChannels { get; set; }

		public S7CommConfig()
		{
			Basic = new S7BasicSetting();
			InputChannels = new List<S7InputChannelConfig>();
			OutputChannels = new List<S7OutputChannelConfig>();
			EnsureFourChannels();
		}

		// Kept under its original name for configuration compatibility. The old
		// implementation always truncated the maps to four channels; S7 now uses
		// the selected scalable channel count (Profinet remains fixed at four).
		public void EnsureFourChannels()
		{
			if (Basic == null)
				Basic = new S7BasicSetting();

			if (Basic.ActiveChannelCount <= 0)
				Basic.ActiveChannelCount = 2;

			if (Basic.ActiveChannelCount > CommunicationConfigHelper.MaxScalableChannelCount)
				Basic.ActiveChannelCount = CommunicationConfigHelper.MaxScalableChannelCount;

			if (InputChannels == null)
				InputChannels = new List<S7InputChannelConfig>();

			if (OutputChannels == null)
				OutputChannels = new List<S7OutputChannelConfig>();

			int requiredCount = Math.Max(CommunicationConfigHelper.ProfinetChannelCount, Basic.ActiveChannelCount);

			for (int i = InputChannels.Count; i < requiredCount; i++)
				InputChannels.Add(S7InputChannelConfig.CreateDefault(i));

			for (int i = OutputChannels.Count; i < requiredCount; i++)
				OutputChannels.Add(S7OutputChannelConfig.CreateDefault(i));

			while (InputChannels.Count > CommunicationConfigHelper.MaxScalableChannelCount)
				InputChannels.RemoveAt(InputChannels.Count - 1);

			while (OutputChannels.Count > CommunicationConfigHelper.MaxScalableChannelCount)
				OutputChannels.RemoveAt(OutputChannels.Count - 1);

			for (int i = 0; i < InputChannels.Count; i++)
			{
				InputChannels[i].ChannelNo = i;
				InputChannels[i].EnsureDefaultTypes();
			}

			for (int i = 0; i < OutputChannels.Count; i++)
			{
				OutputChannels[i].ChannelNo = i;
				OutputChannels[i].EnsureDefaultTypes();
			}
		}
	}

	[Serializable]
	public class S7BasicSetting
	{
		public bool Enable { get; set; }
		public string PlcType { get; set; }
		public string IpAddress { get; set; }
		public int Port { get; set; }
		public byte Rack { get; set; }
		public byte Slot { get; set; }
		public int ConnectTimeoutMs { get; set; }
		public int PollIntervalMs { get; set; }
		public int ActiveChannelCount { get; set; }

		public string InputStartAddress { get; set; }
		public int InputLength { get; set; }

		public string OutputStartAddress { get; set; }
		public int OutputLength { get; set; }

		public bool HeartbeatEnable { get; set; }
		public string HeartbeatAddress { get; set; }
		public string HeartbeatValue { get; set; }
		public int HeartbeatIntervalMs { get; set; }

		public S7BasicSetting()
		{
			Enable = true;
			PlcType = "S1200";
			IpAddress = "192.168.0.1";
			Port = 102;
			Rack = 0;
			Slot = 0;
			ConnectTimeoutMs = 3000;
			PollIntervalMs = 50;
			ActiveChannelCount = 2;
			InputStartAddress = "DB100.0";
			InputLength = 64;
			OutputStartAddress = "DB101.0";
			OutputLength = 128;

			HeartbeatEnable = false;
			HeartbeatAddress = "DB101.100";
			HeartbeatValue = "A";
			HeartbeatIntervalMs = 1000;
		}
	}

	[Serializable]
	public class S7InputChannelConfig
	{
		public int ChannelNo { get; set; }
		public bool Enable { get; set; }

		public string TriggerAddress { get; set; }
		public string TriggerType { get; set; }

		public string PosIdAddress { get; set; }
		public string PosIdType { get; set; }

		public string JobIdAddress { get; set; }
		public string JobIdType { get; set; }

		public string SetUserDataAddress { get; set; }
		public ushort SetUserDataLength { get; set; }
		public string SetUserDataType { get; set; }

		public string ProductionIdAddress { get; set; }
		public ushort ProductionIdLength { get; set; }
		public string ProductionIdType { get; set; }

		public static S7InputChannelConfig CreateDefault(int channelNo)
		{
			int baseByte = channelNo * 24;

			S7InputChannelConfig cfg = new S7InputChannelConfig
			{
				ChannelNo = channelNo,
				Enable = true,
				TriggerAddress = "DB100." + baseByte.ToString() + ".0",
				PosIdAddress = "DB100." + (baseByte + 2).ToString(),
				JobIdAddress = "DB100." + (baseByte + 4).ToString(),
				SetUserDataAddress = "DB100." + (baseByte + 6).ToString(),
				SetUserDataLength = 8,
				ProductionIdAddress = "DB100." + (baseByte + 14).ToString(),
				ProductionIdLength = 8
			};

			cfg.EnsureDefaultTypes();
			return cfg;
		}

		public void EnsureDefaultTypes()
		{
			if (string.IsNullOrWhiteSpace(TriggerType)) TriggerType = S7PointDataType.Bool.ToString();
			if (string.IsNullOrWhiteSpace(PosIdType)) PosIdType = S7PointDataType.Int16.ToString();
			if (string.IsNullOrWhiteSpace(JobIdType)) JobIdType = S7PointDataType.Int16.ToString();
			if (string.IsNullOrWhiteSpace(SetUserDataType)) SetUserDataType = S7PointDataType.String.ToString();
			if (string.IsNullOrWhiteSpace(ProductionIdType)) ProductionIdType = S7PointDataType.String.ToString();
		}
	}

	[Serializable]
	public class S7OutputChannelConfig
	{
		public int ChannelNo { get; set; }
		public bool Enable { get; set; }

		public string BusyAddress { get; set; }
		public string BusyType { get; set; }

		public string DoneAddress { get; set; }
		public string DoneType { get; set; }

		public string OkAddress { get; set; }
		public string OkType { get; set; }

		public string NgAddress { get; set; }
		public string NgType { get; set; }

		public string ErrorCodeAddress { get; set; }
		public string ErrorCodeType { get; set; }

		public string ResultXAddress { get; set; }
		public string ResultXType { get; set; }

		public string ResultYAddress { get; set; }
		public string ResultYType { get; set; }

		public string ResultAAddress { get; set; }
		public string ResultAType { get; set; }

		public string ResultStringAddress { get; set; }
		public ushort ResultStringLength { get; set; }
		public string ResultStringType { get; set; }

		public string UserDataOutAddress { get; set; }
		public ushort UserDataOutLength { get; set; }
		public string UserDataOutType { get; set; }

		public string ProductionIdOutAddress { get; set; }
		public ushort ProductionIdOutLength { get; set; }
		public string ProductionIdOutType { get; set; }

		public static S7OutputChannelConfig CreateDefault(int channelNo)
		{
			int baseByte = channelNo * 48;

			S7OutputChannelConfig cfg = new S7OutputChannelConfig
			{
				ChannelNo = channelNo,
				Enable = true,
				BusyAddress = "DB101." + baseByte.ToString() + ".0",
				DoneAddress = "DB101." + baseByte.ToString() + ".1",
				OkAddress = "DB101." + baseByte.ToString() + ".2",
				NgAddress = "DB101." + baseByte.ToString() + ".3",
				ErrorCodeAddress = "DB101." + (baseByte + 2).ToString(),
				ResultXAddress = "DB101." + (baseByte + 4).ToString(),
				ResultYAddress = "DB101." + (baseByte + 8).ToString(),
				ResultAAddress = "DB101." + (baseByte + 12).ToString(),
				ResultStringAddress = "DB101." + (baseByte + 16).ToString(),
				ResultStringLength = 16,
				UserDataOutAddress = "DB101." + (baseByte + 32).ToString(),
				UserDataOutLength = 8,
				ProductionIdOutAddress = "DB101." + (baseByte + 40).ToString(),
				ProductionIdOutLength = 8
			};

			cfg.EnsureDefaultTypes();
			return cfg;
		}

		public void EnsureDefaultTypes()
		{
			if (string.IsNullOrWhiteSpace(BusyType)) BusyType = S7PointDataType.Bool.ToString();
			if (string.IsNullOrWhiteSpace(DoneType)) DoneType = S7PointDataType.Bool.ToString();
			if (string.IsNullOrWhiteSpace(OkType)) OkType = S7PointDataType.Bool.ToString();
			if (string.IsNullOrWhiteSpace(NgType)) NgType = S7PointDataType.Bool.ToString();
			if (string.IsNullOrWhiteSpace(ErrorCodeType)) ErrorCodeType = S7PointDataType.Int16.ToString();
			if (string.IsNullOrWhiteSpace(ResultXType)) ResultXType = S7PointDataType.Float.ToString();
			if (string.IsNullOrWhiteSpace(ResultYType)) ResultYType = S7PointDataType.Float.ToString();
			if (string.IsNullOrWhiteSpace(ResultAType)) ResultAType = S7PointDataType.Float.ToString();
			if (string.IsNullOrWhiteSpace(ResultStringType)) ResultStringType = S7PointDataType.String.ToString();
			if (string.IsNullOrWhiteSpace(UserDataOutType)) UserDataOutType = S7PointDataType.String.ToString();
			if (string.IsNullOrWhiteSpace(ProductionIdOutType)) ProductionIdOutType = S7PointDataType.String.ToString();
		}
	}
}

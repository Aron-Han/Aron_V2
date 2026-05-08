using System;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Aron_V2
{
	public static class S7ConfigXmlHelper
	{
		public static string DefaultConfigPath
		{
			get
			{
				return ProjectPaths.Config("Communication/S7/S7Config.xml");
			}
		}

		public static S7CommConfig Load()
		{
			return Load(DefaultConfigPath);
		}

		public static S7CommConfig Load(string path)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				{
					S7CommConfig cfg = new S7CommConfig();
					PatchDefaultValue(cfg);
					Save(cfg, path);
					return cfg;
				}

				XmlSerializer serializer = new XmlSerializer(typeof(S7CommConfig));
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					S7CommConfig cfg = serializer.Deserialize(fs) as S7CommConfig;
					if (cfg == null)
						cfg = new S7CommConfig();

					cfg.EnsureFourChannels();
					PatchDefaultValue(cfg);
					return cfg;
				}
			}
			catch
			{
				S7CommConfig cfg = new S7CommConfig();
				PatchDefaultValue(cfg);
				return cfg;
			}
		}

		public static void Save(S7CommConfig config)
		{
			Save(config, DefaultConfigPath);
		}

		public static void Save(S7CommConfig config, string path)
		{
			if (config == null)
				config = new S7CommConfig();

			config.EnsureFourChannels();
			PatchDefaultValue(config);

			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			XmlSerializer serializer = new XmlSerializer(typeof(S7CommConfig));
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				serializer.Serialize(fs, config);
			}
		}

		private static void PatchDefaultValue(S7CommConfig config)
		{
			if (config == null)
				return;

			if (config.Basic == null)
				config.Basic = new S7BasicSetting();

			if (string.IsNullOrWhiteSpace(config.Basic.PlcType))
				config.Basic.PlcType = "S1200";

			if (string.IsNullOrWhiteSpace(config.Basic.IpAddress))
				config.Basic.IpAddress = "192.168.0.1";

			if (config.Basic.Port <= 0)
				config.Basic.Port = 102;

			if (config.Basic.ConnectTimeoutMs <= 0)
				config.Basic.ConnectTimeoutMs = 3000;

			if (config.Basic.PollIntervalMs <= 0)
				config.Basic.PollIntervalMs = 50;

			if (config.Basic.ActiveChannelCount <= 0)
				config.Basic.ActiveChannelCount = 2;

			if (config.Basic.ActiveChannelCount > CommunicationConfigHelper.MaxScalableChannelCount)
				config.Basic.ActiveChannelCount = CommunicationConfigHelper.MaxScalableChannelCount;

			if (string.IsNullOrWhiteSpace(config.Basic.InputStartAddress))
				config.Basic.InputStartAddress = "DB100.0";

			if (config.Basic.InputLength <= 0)
				config.Basic.InputLength = 64;

			if (string.IsNullOrWhiteSpace(config.Basic.OutputStartAddress))
				config.Basic.OutputStartAddress = "DB101.0";

			if (config.Basic.OutputLength <= 0)
				config.Basic.OutputLength = 128;

			if (string.IsNullOrWhiteSpace(config.Basic.HeartbeatAddress))
				config.Basic.HeartbeatAddress = "DB101.100";

			if (string.IsNullOrWhiteSpace(config.Basic.HeartbeatValue))
				config.Basic.HeartbeatValue = "A";

			if (config.Basic.HeartbeatIntervalMs <= 0)
				config.Basic.HeartbeatIntervalMs = 1000;

			config.EnsureFourChannels();
		}
	}
}

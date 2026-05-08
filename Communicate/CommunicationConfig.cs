using System;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Aron_V2
{
	public enum CommunicationProtocol
	{
		S7,
		Profinet,
		TcpIp
	}

	[Serializable]
	public class CommunicationConfig
	{
		public CommunicationProtocol Protocol { get; set; }
		public int ChannelCount { get; set; }

		public CommunicationConfig()
		{
			Protocol = CommunicationProtocol.S7;
			ChannelCount = 4;
		}
	}

	public static class CommunicationConfigHelper
	{
		public const int ProfinetChannelCount = 4;
		public const int MaxScalableChannelCount = 64;

		public static string DefaultPath
		{
			get
			{
				return ProjectPaths.Config("Communication/Communication.xml");
			}
		}

		public static CommunicationConfig Load()
		{
			try
			{
				if (!File.Exists(DefaultPath))
				{
					CommunicationConfig initial = CreateFromExistingS7Config();
					Save(initial);
					return initial;
				}

				XmlSerializer serializer = new XmlSerializer(typeof(CommunicationConfig));
				using (FileStream stream = new FileStream(DefaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					CommunicationConfig config = serializer.Deserialize(stream) as CommunicationConfig;
					config = Normalize(config);
					if (config.Protocol == CommunicationProtocol.S7)
					{
						S7CommConfig s7 = S7ConfigXmlHelper.Load();
						if (s7 != null && s7.Basic != null)
							config.ChannelCount = Math.Max(1, Math.Min(MaxScalableChannelCount, s7.Basic.ActiveChannelCount));
					}
					return config;
				}
			}
			catch
			{
				return CreateFromExistingS7Config();
			}
		}

		public static void Save(CommunicationConfig config)
		{
			config = Normalize(config);
			string directory = Path.GetDirectoryName(DefaultPath);
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			XmlSerializer serializer = new XmlSerializer(typeof(CommunicationConfig));
			using (FileStream stream = new FileStream(DefaultPath, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				serializer.Serialize(stream, config);
			}

			// S7 retains its own connection and address configuration. Keep the
			// selected channel count synchronized so the existing S7 editors and
			// runtime immediately see the value chosen in the common dialog.
			if (config.Protocol == CommunicationProtocol.S7)
			{
				S7CommConfig s7 = S7ConfigXmlHelper.Load();
				s7.Basic.ActiveChannelCount = config.ChannelCount;
				s7.EnsureFourChannels();
				S7ConfigXmlHelper.Save(s7);
			}
		}

		public static int GetEffectiveChannelCount(CommunicationConfig config)
		{
			config = Normalize(config);
			return config.Protocol == CommunicationProtocol.Profinet
				? ProfinetChannelCount
				: config.ChannelCount;
		}

		private static CommunicationConfig CreateFromExistingS7Config()
		{
			CommunicationConfig config = new CommunicationConfig();
			try
			{
				S7CommConfig s7 = S7ConfigXmlHelper.Load();
				if (s7 != null && s7.Basic != null)
					config.ChannelCount = s7.Basic.ActiveChannelCount;
			}
			catch
			{
			}
			return Normalize(config);
		}

		private static CommunicationConfig Normalize(CommunicationConfig config)
		{
			if (config == null)
				config = new CommunicationConfig();

			if (config.Protocol == CommunicationProtocol.Profinet)
				config.ChannelCount = ProfinetChannelCount;
			else
				config.ChannelCount = Math.Max(1, Math.Min(MaxScalableChannelCount, config.ChannelCount));

			return config;
		}
	}
}

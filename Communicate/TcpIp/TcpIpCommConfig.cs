using System;
using System.IO;
using System.Xml.Serialization;

namespace Aron_V2
{
	public enum TcpIpWorkMode
	{
		Client,
		Server
	}

	[Serializable]
	public class TcpIpCommConfig
	{
		public TcpIpWorkMode Mode { get; set; }
		public string IpAddress { get; set; }
		public int Port { get; set; }
		public int ConnectTimeoutMs { get; set; }
		public int ReconnectIntervalMs { get; set; }

		public TcpIpCommConfig()
		{
			Mode = TcpIpWorkMode.Server;
			IpAddress = "0.0.0.0";
			Port = 2000;
			ConnectTimeoutMs = 3000;
			ReconnectIntervalMs = 2000;
		}
	}

	public static class TcpIpConfigXmlHelper
	{
		public static string DefaultPath
		{
			get { return ProjectPaths.Config("Communication/TcpIp/TcpIpConfig.xml"); }
		}

		public static TcpIpCommConfig Load()
		{
			try
			{
				if (!File.Exists(DefaultPath))
				{
					TcpIpCommConfig created = new TcpIpCommConfig();
					Save(created);
					return created;
				}

				XmlSerializer serializer = new XmlSerializer(typeof(TcpIpCommConfig));
				using (FileStream stream = new FileStream(DefaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					return serializer.Deserialize(stream) as TcpIpCommConfig ?? new TcpIpCommConfig();
			}
			catch
			{
				return new TcpIpCommConfig();
			}
		}

		public static void Save(TcpIpCommConfig config)
		{
			if (config == null) config = new TcpIpCommConfig();
			if (config.Port < 1 || config.Port > 65535) throw new ArgumentOutOfRangeException("Port");
			if (config.ConnectTimeoutMs < 100) config.ConnectTimeoutMs = 100;
			if (config.ReconnectIntervalMs < 100) config.ReconnectIntervalMs = 100;

			string directory = Path.GetDirectoryName(DefaultPath);
			if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
			XmlSerializer serializer = new XmlSerializer(typeof(TcpIpCommConfig));
			using (FileStream stream = new FileStream(DefaultPath, FileMode.Create, FileAccess.Write, FileShare.Read))
				serializer.Serialize(stream, config);
		}
	}
}

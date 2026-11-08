using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Aron_V2.Profinet
{
	[Serializable]
	public class ProfinetCommConfig
	{
		public List<ProfinetInputPoint> InputPoints { get; set; } = new List<ProfinetInputPoint>();
		public List<ProfinetOutputPoint> OutputPoints { get; set; } = new List<ProfinetOutputPoint>();
	}

	[Serializable]
	public class ProfinetInputPoint
	{
		public int Channel { get; set; }
		public string Parameter { get; set; }
		public int Address { get; set; }
		public string DataType { get; set; }
		public int Length { get; set; }
		public string Note { get; set; }
	}

	[Serializable]
	public class ProfinetOutputPoint
	{
		public int Channel { get; set; }
		public string Parameter { get; set; }
		public string VppOutput { get; set; }
		public int Address { get; set; }
		public string DataType { get; set; }
		public int Length { get; set; }
		public bool IsFixed { get; set; }
		public string Note { get; set; }
	}

	public static class ProfinetCommConfigHelper
	{
        private static readonly object ConfigLock=new object();
		public const int ProcessDataLength = 240;
		public static readonly string[] InputParameterNames = { "Clear Result", "JobID", "PosID", "PartN" };
		public static readonly string[] SupportedDataTypes = { "Bool", "Byte", "Int16", "Int32", "Float", "Double", "String" };
		public static string DefaultPath => ProjectPaths.Config("Communication/Profinet/ProfinetConfig.xml");

		/// <summary>Returns 0 only for variable-length types.</summary>
		public static int GetFixedLength(string dataType)
		{
			switch ((dataType ?? string.Empty).Trim().ToLowerInvariant())
			{
				case "bool":
				case "byte": return 1;
				case "int16": return 2;
				case "int32":
				case "float": return 4;
				case "double": return 8;
				default: return 0;
			}
		}

		public static ProfinetCommConfig Load()
		{
            lock(ConfigLock) {
			try
			{
				if (!File.Exists(DefaultPath)) { ProfinetCommConfig created = CreateDefault(); Save(created); return created; }
				XmlSerializer serializer = new XmlSerializer(typeof(ProfinetCommConfig));
				using (FileStream stream = new FileStream(DefaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite|FileShare.Delete))
					return Normalize(serializer.Deserialize(stream) as ProfinetCommConfig);
			}
			catch { ProfinetCommConfig created = CreateDefault(); Save(created); return created; }
            }
		}

		public static void Save(ProfinetCommConfig config)
		{
            lock(ConfigLock) {
			config = Normalize(config);
			string directory = Path.GetDirectoryName(DefaultPath);
			if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
			XmlSerializer serializer = new XmlSerializer(typeof(ProfinetCommConfig));
            string temporary=DefaultPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {serializer.Serialize(stream,config);stream.Flush(true);}
                if(File.Exists(DefaultPath)) File.Replace(temporary,DefaultPath,null);else File.Move(temporary,DefaultPath);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
            }
		}

		public static ProfinetInputPoint GetInputPoint(int channel, string parameter)
		{
			return Load().InputPoints.First(item => item.Channel == channel && string.Equals(item.Parameter, parameter, StringComparison.OrdinalIgnoreCase));
		}

		public static ProfinetOutputPoint GetOutputPoint(int channel, string parameter)
		{
			return Load().OutputPoints.First(item => item.Channel == channel && string.Equals(item.Parameter, parameter, StringComparison.OrdinalIgnoreCase));
		}

		private static ProfinetCommConfig CreateDefault()
		{
			ProfinetCommConfig config = new ProfinetCommConfig();
			for (int channel = 0; channel < CommunicationConfigHelper.ProfinetChannelCount; channel++)
			{
				config.InputPoints.Add(Input(channel, "Clear Result", channel, "Byte", 1));
				config.InputPoints.Add(Input(channel, "JobID", 4 + channel, "Byte", 1));
				config.InputPoints.Add(Input(channel, "PosID", 8 + channel, "Byte", 1));
				config.InputPoints.Add(Input(channel, "PartN", 12 + channel * 4, "String", 4));
				config.OutputPoints.Add(Output(channel, "JobID", string.Empty, channel, "Byte", 1, true));
				config.OutputPoints.Add(Output(channel, "PosID", string.Empty, 4 + channel, "Byte", 1, true));
			}
			return config;
		}

		private static ProfinetInputPoint Input(int ch, string name, int address, string type, int length) => new ProfinetInputPoint { Channel = ch, Parameter = name, Address = address, DataType = type, Length = length };
		private static ProfinetOutputPoint Output(int ch, string name, string source, int address, string type, int length, bool fixedRow) => new ProfinetOutputPoint { Channel = ch, Parameter = name, VppOutput = source, Address = address, DataType = type, Length = length, IsFixed = fixedRow };

		private static ProfinetCommConfig Normalize(ProfinetCommConfig config)
		{
			if (config == null) config = new ProfinetCommConfig();
			if (config.InputPoints == null) config.InputPoints = new List<ProfinetInputPoint>();
			if (config.OutputPoints == null) config.OutputPoints = new List<ProfinetOutputPoint>();
			ProfinetCommConfig defaults = CreateDefault();
			foreach (ProfinetInputPoint item in defaults.InputPoints)
				if (!config.InputPoints.Any(x => x != null && x.Channel == item.Channel && string.Equals(x.Parameter, item.Parameter, StringComparison.OrdinalIgnoreCase))) config.InputPoints.Add(item);
			foreach (ProfinetOutputPoint item in defaults.OutputPoints)
				if (!config.OutputPoints.Any(x => x != null && x.Channel == item.Channel && string.Equals(x.Parameter, item.Parameter, StringComparison.OrdinalIgnoreCase))) config.OutputPoints.Add(item);
			// The PLC buffer is addressed by byte offset, not by channel.  Persist and
			// display the tables in that same order so address gaps/overlaps are obvious.
			config.InputPoints = config.InputPoints.Where(IsValidInput).OrderBy(x => x.Address).ThenBy(x => x.Channel).ThenBy(x => x.Parameter).ToList();
			config.OutputPoints = config.OutputPoints.Where(IsValidOutput).OrderBy(x => x.Address).ThenBy(x => x.Channel).ThenBy(x => x.Parameter).ToList();
			foreach (ProfinetInputPoint item in config.InputPoints) NormalizePoint(item);
			foreach (ProfinetOutputPoint item in config.OutputPoints) NormalizePoint(item);
            foreach(var item in config.OutputPoints.Where(p=>p.Parameter.Equals("JobID",StringComparison.OrdinalIgnoreCase)||p.Parameter.Equals("PosID",StringComparison.OrdinalIgnoreCase))) item.IsFixed=true;
			return config;
		}

		private static bool IsValidInput(ProfinetInputPoint x) => x != null && x.Channel >= 0 && x.Channel < CommunicationConfigHelper.ProfinetChannelCount && InputParameterNames.Contains(x.Parameter);
		private static bool IsValidOutput(ProfinetOutputPoint x) => x != null && x.Channel >= 0 && x.Channel < CommunicationConfigHelper.ProfinetChannelCount && !string.IsNullOrWhiteSpace(x.Parameter);
		private static void NormalizePoint(ProfinetInputPoint x) { x.Address = Math.Max(0, x.Address); if (!SupportedDataTypes.Contains(x.DataType)) x.DataType = "Byte"; int fixedLength = GetFixedLength(x.DataType); x.Length = fixedLength > 0 ? fixedLength : Math.Max(1, x.Length); x.Note = x.Note ?? string.Empty; }
		private static void NormalizePoint(ProfinetOutputPoint x) { x.Address = Math.Max(0, x.Address); if (!SupportedDataTypes.Contains(x.DataType)) x.DataType = "Byte"; int fixedLength = GetFixedLength(x.DataType); x.Length = fixedLength > 0 ? fixedLength : Math.Max(1, x.Length); x.VppOutput = x.VppOutput ?? string.Empty; x.Note = x.Note ?? string.Empty; }
	}
}

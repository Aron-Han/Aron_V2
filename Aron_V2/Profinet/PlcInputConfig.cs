using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Aron_V2.Profinet
{
	public class PlcInputConfig
	{
		public List<PlcInputChannel> Channels { get; set; }

		public PlcInputConfig()
		{
			Channels = new List<PlcInputChannel>();
		}
	}

	public class PlcInputChannel
	{
		public int Channel { get; set; }

		public int ClearStart { get; set; }
		public int ClearLength { get; set; }

		public int JobStart { get; set; }
		public int JobLength { get; set; }

		public int PosStart { get; set; }
		public int PosLength { get; set; }

		public int PartStart { get; set; }
		public int PartLength { get; set; }
	}

	public static class PlcInputConfigHelper
	{
		public static string DefaultPath
		{
			get
			{
				string projectDir = System.IO.Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"Project");

				if (!System.IO.Directory.Exists(projectDir))
				{
					System.IO.Directory.CreateDirectory(projectDir);
				}

				return System.IO.Path.Combine(projectDir, "PlcInput.xml");
			}
		}

		public static PlcInputConfig Load(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				path = DefaultPath;

			if (!File.Exists(path))
			{
				var cfg = CreateDefault();
				Save(cfg, path);
				return cfg;
			}

			var doc = XDocument.Load(path);
			var cfg2 = new PlcInputConfig();

			foreach (var ch in doc.Root.Elements("Channel"))
			{
				cfg2.Channels.Add(new PlcInputChannel
				{
					Channel = ToInt(ch.Attribute("Index"), 0),

					ClearStart = ToInt(ch.Element("ClearStart"), 0),
					ClearLength = ToInt(ch.Element("ClearLength"), 1),

					JobStart = ToInt(ch.Element("JobStart"), 4),
					JobLength = ToInt(ch.Element("JobLength"), 1),

					PosStart = ToInt(ch.Element("PosStart"), 8),
					PosLength = ToInt(ch.Element("PosLength"), 1),

					PartStart = ToInt(ch.Element("PartStart"), 12),
					PartLength = ToInt(ch.Element("PartLength"), 4)
				});
			}

			EnsureFourChannels(cfg2);
			return cfg2;
		}

		public static void Save(PlcInputConfig cfg, string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				path = DefaultPath;

			EnsureFourChannels(cfg);

			var doc = new XDocument(
				new XElement("PlcInputConfig",
					cfg.Channels
						.OrderBy(x => x.Channel)
						.Select(x =>
							new XElement("Channel",
								new XAttribute("Index", x.Channel),

								new XElement("ClearStart", x.ClearStart),
								new XElement("ClearLength", x.ClearLength),

								new XElement("JobStart", x.JobStart),
								new XElement("JobLength", x.JobLength),

								new XElement("PosStart", x.PosStart),
								new XElement("PosLength", x.PosLength),

								new XElement("PartStart", x.PartStart),
								new XElement("PartLength", x.PartLength)
							)
						)
				)
			);

			doc.Save(path);
		}

		public static PlcInputConfig CreateDefault()
		{
			var cfg = new PlcInputConfig();

			cfg.Channels.Add(new PlcInputChannel
			{
				Channel = 0,
				ClearStart = 0,
				ClearLength = 1,
				JobStart = 4,
				JobLength = 1,
				PosStart = 8,
				PosLength = 1,
				PartStart = 12,
				PartLength = 4
			});

			cfg.Channels.Add(new PlcInputChannel
			{
				Channel = 1,
				ClearStart = 1,
				ClearLength = 1,
				JobStart = 5,
				JobLength = 1,
				PosStart = 9,
				PosLength = 1,
				PartStart = 16,
				PartLength = 4
			});

			cfg.Channels.Add(new PlcInputChannel
			{
				Channel = 2,
				ClearStart = 2,
				ClearLength = 1,
				JobStart = 6,
				JobLength = 1,
				PosStart = 10,
				PosLength = 1,
				PartStart = 20,
				PartLength = 4
			});

			cfg.Channels.Add(new PlcInputChannel
			{
				Channel = 3,
				ClearStart = 3,
				ClearLength = 1,
				JobStart = 7,
				JobLength = 1,
				PosStart = 11,
				PosLength = 1,
				PartStart = 24,
				PartLength = 4
			});

			return cfg;
		}

		private static void EnsureFourChannels(PlcInputConfig cfg)
		{
			if (cfg.Channels == null)
				cfg.Channels = new List<PlcInputChannel>();

			for (int i = 0; i < 4; i++)
			{
				if (!cfg.Channels.Any(x => x.Channel == i))
				{
					var def = CreateDefault().Channels.First(x => x.Channel == i);
					cfg.Channels.Add(def);
				}
			}

			cfg.Channels = cfg.Channels
				.Where(x => x.Channel >= 0 && x.Channel <= 3)
				.GroupBy(x => x.Channel)
				.Select(g => g.First())
				.OrderBy(x => x.Channel)
				.ToList();
		}

		private static int ToInt(XAttribute attr, int def)
		{
			if (attr == null) return def;
			int v;
			return int.TryParse(attr.Value, out v) ? v : def;
		}

		private static int ToInt(XElement ele, int def)
		{
			if (ele == null) return def;
			int v;
			return int.TryParse(ele.Value, out v) ? v : def;
		}
	}
}
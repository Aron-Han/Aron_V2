using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Aron_V2
{
	public static class XmlConfigHelper
	{
		#region XML Parameter
		public static AppConfig Load(string path)
		{
			if (!File.Exists(path))
				return new AppConfig { Models = new System.Collections.Generic.List<ModelConfig>() };

			using (var fs = File.OpenRead(path))
			{
				var ser = new XmlSerializer(typeof(AppConfig));
				return (AppConfig)ser.Deserialize(fs);
			}
		}

		public static void Save(AppConfig cfg, string path)
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			using (var fs = File.Create(path))
			{
				var ser = new XmlSerializer(typeof(AppConfig));
				ser.Serialize(fs, cfg);
			}
		}

		public static void Load_Job(AppConfig config, string jobName)
		{
			if (config == null || config.Models == null || string.IsNullOrEmpty(jobName))
				return;

			// 找到这个 Job
			var job = config.Models
				.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null) return;

			// 1) 先加载这个 Job 自己的 General（最外层的）
			if (job.General != null)
			{
				int n;
				if (int.TryParse(job.General.maxLines_Richbox, out n))
					Global.maxLines_Richbox = n;

				if (int.TryParse(job.General.CamN, out n))
					Global.CamN_Use = n;
			}

			// 2) 清掉上次的相机/工位 General 缓存
			Global.CamGeneral.Clear();

			// 3) 遍历这个 Job 的每一台相机
			if (job.Cameras == null) return;

			foreach (var cam in job.Cameras)
			{
				if (cam == null) continue;

				if (cam.Positions == null) continue;

				foreach (var pos in cam.Positions)
				{
					if (pos == null) continue;

					// 位置下面的 General
					var g = pos.General;
					if (g == null)
						continue; // 这个 pos 没配 General 就跳过

					// 你要保存的 key，可以用 "Job1.Cam1.Pos1"
					string key = job.Name + "." + cam.Name + "." + pos.Name;

					Global.CamGeneral[key] = new CameraGeneralRuntime
					{
						Exposure = g.Exposure ?? "0",
						MainUsed = g.MainUsed ?? "0",
						MainChannel = g.MainChannel ?? "0",
						SecondUsed = g.SecondUsed ?? "0",
						SecondChannel = g.SecondChannel ?? "0"
					};
				}
			}
		}

		public static Dictionary<string, string> GetParametersFor(
		AppConfig cfg, string jobName, string camName, string posName)
		{
			var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (cfg == null || cfg.Models == null) return dict;

			var job = cfg.Models.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (job == null || job.Cameras == null) return dict;

			var cam = job.Cameras.FirstOrDefault(c => string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
			if (cam == null || cam.Positions == null) return dict;

			var pos = cam.Positions.FirstOrDefault(p => string.Equals(p.Name, posName, StringComparison.OrdinalIgnoreCase));
			if (pos == null || pos.Parameters == null) return dict;

			foreach (var p in pos.Parameters)
			{
				if (p != null && !string.IsNullOrEmpty(p.Name))
					dict[p.Name] = p.Value ?? string.Empty;
			}
			return dict;
		}

		/// <summary>
		/// 可选：返回 PositionConfig 本体，给调用方自行处理。
		/// </summary>

		public static void EnsurePositionGeneral(AppConfig cfg)
		{
			if (cfg == null || cfg.Models == null) return;

			foreach (var model in cfg.Models)
			{
				if (model.Cameras == null) continue;
				foreach (var cam in model.Cameras)
				{
					if (cam.Positions == null) continue;
					foreach (var pos in cam.Positions)
					{
						if (pos.General == null)
							pos.General = CreateDefaultPositionGeneral();
					}
				}
			}
		}

		private static PositionGeneralConfig CreateDefaultPositionGeneral()
		{
			return new PositionGeneralConfig
			{
				Exposure = "10",
				MainUsed = "1",
				MainChannel = "1",
				SecondUsed = "0",
				SecondChannel = "0"
			};
		}

		public static void ini_Parameters()
		{
			Global.Result_data_send = new string[Global.CamN_Use];
			Global.Manual_Trigger_Lock = new bool[] { false, false, false, false };
		}

		public static string FindCamByChannel(string jobName, int channel)
		{
			// 遍历字典里的所有记录
			foreach (var kv in Global.CamGeneral)
			{
				string key = kv.Key;                     // 比如 "Job1.Cam2.Pos1"
				var val = kv.Value;                      // CameraGeneralRuntime

				// 先看是不是这个Job
				if (!key.StartsWith(jobName + ".", StringComparison.OrdinalIgnoreCase))
					continue;

				// 解析这条记录的 MainUsed / MainChannel
				int mainUsed = 0;
				int mainChannel = -1;
				int.TryParse(val.MainUsed, out mainUsed);
				int.TryParse(val.MainChannel, out mainChannel);

				// 满足条件就说明这条就是你要的
				if (mainUsed == 1 && mainChannel == channel)
				{
					// 从 key 里把相机名取出来：Job1.Cam2.Pos1
					// 分成 ["Job1", "Cam2", "Pos1"]
					var parts = key.Split('.');
					if (parts.Length >= 3)
					{
						return parts[1];   // Cam2
					}
				}
			}

			// 没找到就返回 null 或空
			return null;
		}

		#endregion

		#region VPP Output

		// —— VPP 输出配置的模型 —— //
		public class VppOutputConfig
		{
			public List<JobOutput> Jobs { get; set; }
		}

		public class JobOutput
		{
			public string Name { get; set; }
			public List<CamOutput> Cameras { get; set; }
		}

		public class CamOutput
		{
			public string Name { get; set; }
			public List<OutputItem> VPPOutput { get; set; }
		}

		public class OutputItem
		{
			public string Name { get; set; }     // 逻辑名（日志/调试）
			public string Type { get; set; }     // string/float/int/short/bool/double
			public string Source { get; set; }   // ToolBlock 输出端口名
			public string Required { get; set; } // "true"/"false"
			public int Channel { get; set; }     // 0..3 本输出属于哪个通道
			public int Start { get; set; }       // 在 240B 里的起始字节
			public int Length { get; set; }      // 占用字节数
		}

		// ========== VPP 输出配置（XML） ========== //
		public static VppOutputConfig LoadVppOutput(string path)
		{
			if (!File.Exists(path))
				return new VppOutputConfig { Jobs = new List<JobOutput>() };

			var x = XDocument.Load(path);
			var cfg = new VppOutputConfig { Jobs = new List<JobOutput>() };

			var xJobs = x.Root.Element("Jobs");
			if (xJobs == null) return cfg;

			foreach (var j in xJobs.Elements("Job"))
			{
				var job = new JobOutput
				{
					Name = (string)j.Attribute("Name") ?? "",
					Cameras = new List<CamOutput>()
				};

				var cams = j.Element("Cameras");
				if (cams != null)
				{
					foreach (var c in cams.Elements("Camera"))
					{
						var cam = new CamOutput
						{
							Name = (string)c.Attribute("Name") ?? "",
							VPPOutput = new List<OutputItem>()
						};

						var vpp = c.Element("VPPOutput");
						if (vpp != null)
						{
							foreach (var it in vpp.Elements("Item"))
							{
								int ch = 0, start = 0, len = 0;
								int.TryParse((string)it.Attribute("Channel") ?? "0", out ch);
								int.TryParse((string)it.Attribute("Start") ?? "0", out start);
								int.TryParse((string)it.Attribute("Length") ?? "0", out len);

								cam.VPPOutput.Add(new OutputItem
								{
									Name = (string)it.Attribute("Name") ?? "",
									Type = (string)it.Attribute("Type") ?? "string",
									Source = (string)it.Attribute("Source") ?? "",
									Required = (string)it.Attribute("Required") ?? "false",
									Channel = ch,
									Start = start,
									Length = len
								});
							}
						}
						job.Cameras.Add(cam);
					}
				}
				cfg.Jobs.Add(job);
			}
			return cfg;
		}

		public static void SaveVppOutput(VppOutputConfig cfg, string path)
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			var x = new XDocument(
				new XElement("VppOutputConfig",
					new XElement("Jobs",
						(cfg.Jobs ?? new List<JobOutput>()).Select(j =>
							new XElement("Job", new XAttribute("Name", j.Name ?? ""),
								new XElement("Cameras",
									(j.Cameras ?? new List<CamOutput>()).Select(c =>
										new XElement("Camera", new XAttribute("Name", c.Name ?? ""),
											new XElement("VPPOutput",
												(c.VPPOutput ?? new List<OutputItem>()).Select(it =>
													new XElement("Item",
														new XAttribute("Name", it.Name ?? ""),
														new XAttribute("Type", it.Type ?? "string"),
														new XAttribute("Source", it.Source ?? ""),
														new XAttribute("Required", it.Required ?? "false"),
														new XAttribute("Channel", it.Channel),
														new XAttribute("Start", it.Start),
														new XAttribute("Length", it.Length)
													)
												)
											)
										)
									)
								)
							)
						)
					)
				)
			);
			x.Save(path);
		}

		public static List<OutputItem> GetVppItemsForCam(VppOutputConfig cfg, string job, string cam)
		{
			if (cfg == null || cfg.Jobs == null) return new List<OutputItem>();
			var j = cfg.Jobs.FirstOrDefault(z => string.Equals(z.Name, job, StringComparison.OrdinalIgnoreCase));
			if (j == null || j.Cameras == null) return new List<OutputItem>();
			var c = j.Cameras.FirstOrDefault(z => string.Equals(z.Name, cam, StringComparison.OrdinalIgnoreCase));
			if (c == null || c.VPPOutput == null) return new List<OutputItem>();
			return c.VPPOutput;
		}

		public static List<string> ValidateVppOutputs(CogToolBlock tb, List<OutputItem> items)
		{
			var errors = new List<string>();
			if (tb == null || items == null) return errors;

			foreach (var it in items)
			{
				bool required = string.Equals(it.Required, "true", StringComparison.OrdinalIgnoreCase);
				if (required && !tb.Outputs.Contains(it.Source))
					errors.Add("Lack of Output: " + it.Source);
			}
			return errors;
		}

		public static void ApplyChannelOutputsToPlcBuffer(int channel, CogToolBlock tb, List<OutputItem> allItems, bool bigEndian)
		{
			if (tb == null || allItems == null) return;
			// 只拿本通道的项
			var items = allItems.FindAll(x => x != null && x.Channel == channel);

			lock (Global.PlcBufferLock)
			{
				// 可选：每通道写之前不清全局，只清各自区段（上面的 WriteValueToBuffer 已清段）
				foreach (var it in items)
				{
					// Required 的端口必须存在
					bool required = string.Equals(it.Required, "true", StringComparison.OrdinalIgnoreCase);
					if (required && !tb.Outputs.Contains(it.Source))
					{
						LogChangeEventArgs.Set("Log", $"VPP lack of Output setting: {it.Source} (ch={channel})", System.Drawing.Color.Red);
						continue;
					}

					object raw = tb.Outputs.Contains(it.Source) ? tb.Outputs[it.Source].Value : null;
					WriteValueToBuffer(Global.Result_Send, it.Start, it.Length, it.Type, raw, bigEndian);
				}
			}
		}

		public static void WriteValueToBuffer(byte[] buffer, int start, int length,string type, object value, bool bigEndian)
		{
			if (buffer == null) return;
			if (start < 0 || start >= buffer.Length) return;
			if (length <= 0) return;
			if (start + length > buffer.Length) length = buffer.Length - start;

			byte[] src = null;
			string t = (type ?? "string").ToLowerInvariant();
			try
			{
				switch (t)
				{
					case "float":
						float f = 0f; if (value != null) float.TryParse(value.ToString(), out f);
						src = BitConverter.GetBytes(f); break;
					case "double":
						double d = 0d; if (value != null) double.TryParse(value.ToString(), out d);
						src = BitConverter.GetBytes(d); break;
					case "int":
						int i = 0; if (value != null) int.TryParse(value.ToString(), out i);
						src = BitConverter.GetBytes(i); break;
					case "short":
						short s = 0; if (value != null) short.TryParse(value.ToString(), out s);
						src = BitConverter.GetBytes(s); break;
					case "bool":
						src = new byte[] { (value is bool && (bool)value) ? (byte)1 : (byte)0 }; break;
					case "string":
					default:
						src = System.Text.Encoding.ASCII.GetBytes(value == null ? "" : value.ToString()); break;
				}

				if (src != null && src.Length > 1 && BitConverter.IsLittleEndian && bigEndian)
					Array.Reverse(src);

				// 先清零目标段，再拷贝（避免残留）
				for (int k = 0; k < length; k++) buffer[start + k] = 0;
				Array.Copy(src, 0, buffer, start, Math.Min(length, src.Length));
			}
			catch { /* 出错保留已清零 */ }
		}

		#endregion
	}
}

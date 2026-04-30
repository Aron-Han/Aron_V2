using System;
using System.Linq;
using System.Text;

namespace Aron_V2.Profinet
{
	public static class input_Parameters
	{
		private static PlcInputConfig _inputCfg;
		private static readonly object _lock = new object();

		public static PlcInputConfig InputCfg
		{
			get
			{
				lock (_lock)
				{
					if (_inputCfg == null)
					{
						_inputCfg = PlcInputConfigHelper.Load(PlcInputConfigHelper.DefaultPath);
					}

					return _inputCfg;
				}
			}
		}

		public static void ReloadInputConfig()
		{
			lock (_lock)
			{
				_inputCfg = PlcInputConfigHelper.Load(PlcInputConfigHelper.DefaultPath);
			}
		}

		public static PlcInputChannel GetChannelConfig(int channel)
		{
			var cfg = InputCfg;

			if (cfg == null || cfg.Channels == null)
			{
				throw new Exception("PlcInput.xml 输入配置为空");
			}

			var ch = cfg.Channels.FirstOrDefault(x => x.Channel == channel);

			if (ch == null)
			{
				throw new Exception("PlcInput.xml 中未找到 Channel=" + channel + " 的输入配置");
			}

			return ch;
		}

		public static bool GetClear(byte[] data, int channel)
		{
			var ch = GetChannelConfig(channel);
			int v = ReadNumber(data, ch.ClearStart, ch.ClearLength);
			return v == 1;
		}

		public static int GetJobDigit(byte[] data, int channel)
		{
			var ch = GetChannelConfig(channel);
			return ReadNumber(data, ch.JobStart, ch.JobLength);
		}

		public static int GetPosDigit(byte[] data, int channel)
		{
			var ch = GetChannelConfig(channel);
			return ReadNumber(data, ch.PosStart, ch.PosLength);
		}

		public static string GetPartCode(byte[] data, int channel)
		{
			var ch = GetChannelConfig(channel);
			return ReadString(data, ch.PartStart, ch.PartLength);
		}

		private static int ReadNumber(byte[] data, int start, int length)
		{
			if (data == null) return 0;
			if (start < 0 || start >= data.Length) return 0;
			if (length <= 0) return 0;

			int take = Math.Min(length, data.Length - start);
			if (take <= 0) return 0;

			// 单字节：兼容 PLC 发 byte 数值 1，也兼容 ASCII '1'
			if (take == 1)
			{
				byte b = data[start];

				if (b >= (byte)'0' && b <= (byte)'9')
				{
					return b - (byte)'0';
				}

				return b;
			}

			// 多字节：优先按 ASCII 数字解析，例如 12、101
			var sb = new StringBuilder();

			for (int i = 0; i < take; i++)
			{
				byte b = data[start + i];

				if (b >= (byte)'0' && b <= (byte)'9')
				{
					sb.Append((char)b);
				}
				else if (b <= 9)
				{
					sb.Append(b.ToString());
				}
			}

			int v;
			return int.TryParse(sb.ToString(), out v) ? v : 0;
		}

		private static string ReadString(byte[] data, int start, int length)
		{
			if (data == null) return "";
			if (start < 0 || start >= data.Length) return "";
			if (length <= 0) return "";

			int take = Math.Min(length, data.Length - start);
			if (take <= 0) return "";

			byte[] part = new byte[take];
			Buffer.BlockCopy(data, start, part, 0, take);

			// 如果是 ASCII 条码，例如 49,50,51,52 -> "1234"
			bool looksAscii = part.All(b => b == 0 || b == 32 || (b >= 33 && b <= 126));

			if (looksAscii)
			{
				return Encoding.ASCII.GetString(part).TrimEnd('\0', ' ');
			}

			// 如果 PLC 发的是 byte 数值，例如 1,2,3,4 -> "1234"
			var sb = new StringBuilder();

			for (int i = 0; i < part.Length; i++)
			{
				if (part[i] == 0) continue;
				sb.Append(part[i].ToString());
			}

			return sb.ToString();
		}

		public static void ClearResultBufferByConfig(int channel)
		{
			var job = Global.Model_JobID[channel];
			if (string.IsNullOrEmpty(job)) return;

			var cfgPath = string.IsNullOrEmpty(Global.VppOutputCfgPath)
				? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VppOutput.xml")
				: Global.VppOutputCfgPath;

			var cfg = XmlConfigHelper.LoadVppOutput(cfgPath);
			if (cfg == null || cfg.Jobs == null) return;

			var j = cfg.Jobs.FirstOrDefault(z => string.Equals(z.Name, job, StringComparison.OrdinalIgnoreCase));
			if (j == null || j.Cameras == null) return;

			var segs = new System.Collections.Generic.List<Tuple<int, int>>();

			foreach (var cam in j.Cameras)
			{
				if (cam == null || cam.VPPOutput == null) continue;

				foreach (var it in cam.VPPOutput)
				{
					if (it == null) continue;
					if (it.Channel != channel) continue;
					if (it.Start < 0 || it.Length <= 0) continue;

					segs.Add(Tuple.Create(it.Start, it.Length));
				}
			}

			lock (Global.PlcBufferLock)
			{
				foreach (var s in segs)
				{
					int start = s.Item1;
					int len = s.Item2;

					if (start >= Global.Result_Send.Length) continue;

					if (start + len > Global.Result_Send.Length)
					{
						len = Global.Result_Send.Length - start;
					}

					for (int k = 0; k < len; k++)
					{
						Global.Result_Send[start + k] = 0;
					}
				}
			}
		}

		public static byte ToDigit(string oneDigit)
		{
			if (string.IsNullOrEmpty(oneDigit)) return 0;

			char c = oneDigit[0];
			return (byte)((c >= '0' && c <= '9') ? (c - '0') : 0);
		}
	}

	public static class PlcEchoRegion
	{
		// 约定：Result_Send 的 0..7 字节作为 PLC 回显区
		// Job 回显：0..3
		public const int IdxJobCh0 = 0;
		public const int IdxJobCh1 = 1;
		public const int IdxJobCh2 = 2;
		public const int IdxJobCh3 = 3;

		// Pos 回显：4..7
		public const int IdxPosCh0 = 4;
		public const int IdxPosCh1 = 5;
		public const int IdxPosCh2 = 6;
		public const int IdxPosCh3 = 7;

		// Clear 回显预留：8..11
		public const int IdxClrCh0 = 8;
		public const int IdxClrCh1 = 9;
		public const int IdxClrCh2 = 10;
		public const int IdxClrCh3 = 11;
	}
}

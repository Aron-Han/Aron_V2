using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Aron_V2
{
	public class Load_Job
	{
		public static class VppCache
		{
			public static readonly Dictionary<string, CogAcqFifoTool> Acq =
		new Dictionary<string, CogAcqFifoTool>(StringComparer.OrdinalIgnoreCase);

			public static readonly Dictionary<string, CogToolBlock> Tb =
				new Dictionary<string, CogToolBlock>(StringComparer.OrdinalIgnoreCase);

			// 统一的Key：Job|CHx|Cam|Pos
			public static string Key(string job, int channel, string cam, string pos)
				=> $"{job}|CH{channel}|{cam}|{pos}";

			public static bool TryGetAcq(string job, int channel, string cam, string pos, out CogAcqFifoTool acq)
				=> Acq.TryGetValue(Key(job, channel, cam, pos), out acq);

			public static bool TryGetTb(string job, int channel, string cam, string pos, out CogToolBlock tb)
				=> Tb.TryGetValue(Key(job, channel, cam, pos), out tb);

			public static void Set(string job, int channel, string cam, string pos, CogAcqFifoTool acq, CogToolBlock tb)
			{
				var k = Key(job, channel, cam, pos);
				if (acq != null) Acq[k] = acq;
				if (tb != null) Tb[k] = tb;
			}

			// 按 Job+Channel 批量清理（切程序/通道时很好用）
			public static void ClearJobChannel(string job, int channel)
			{
				var prefix = $"{job}|CH{channel}|";
				foreach (var k in Acq.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
				{ try { Acq[k]?.Dispose(); } catch { } Acq.Remove(k); }
				foreach (var k in Tb.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
				{ try { Tb[k]?.Dispose(); } catch { } Tb.Remove(k); }
			}
		}

		public static class VppPath
		{
			public static string Root = Global.Vpp_Root; // 例如 D:\VPP\
			public static string Acq(string job, string cam, string pos)
				=> System.IO.Path.Combine(Root, job, cam, pos, "Camera.VPP");
			public static string Tb(string job, string cam, string pos)
				=> System.IO.Path.Combine(Root, job, cam, pos, "Inspection.VPP");
		}

		// 切换锁，避免并发切换踩缓存
		private static readonly object _jobSwitchLock = new object();

		// 从 CamGeneral 字典中，枚举某 Job+Cam 的所有 Pos 名（不依赖 _config）
		private static IEnumerable<string> EnumPositions(string jobId, string camName)
		{
			var prefix = jobId + "." + camName + ".";
			return Global.CamGeneral.Keys
				.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.Select(k => k.Substring(prefix.Length))        // 取出 Pos 名
				.Select(pos => pos)                             // 可能重复，后面去重
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}

		// 主入口：为“指定通道”的“指定 Job”预加载所有 Cam 的所有 Pos（根据 CamGeneral 过滤）
		public static void LoadVPP_ForChannel_AllPos(int channel, string jobId)
		{
			// 刷新 CamGeneral（只装当前 Job）
			AppConfig _config = XmlConfigHelper.Load(Global.ParameterCogfig);
			XmlConfigHelper.Load_Job(_config, jobId);

			if (channel < 0 || channel > 3)
			{
				LogChangeEventArgs.Set("Log", $"ChangeJob: not correct {channel}", Color.Red);
				return;
			}
			if (string.IsNullOrWhiteSpace(jobId))
			{
				LogChangeEventArgs.Set("Log", $"ChangeJob: JobID is null（ch={channel})", Color.Red);
				return;
			}

			lock (_jobSwitchLock)
			{

				try
				{
					// 可选：先清理该 Job+Channel 的旧缓存
					VppCache.ClearJobChannel(jobId, channel);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						string camName = "Cam" + (i + 1);

						// 从 CamGeneral 的键枚举所有 Pos（CamGeneral 的 key 不包含 channel）
						foreach (var posName in EnumPositions(jobId, camName))
						{
							// 用于 CamGeneral 的键（无 channel）
							string keyCamGen = jobId + "." + camName + "." + posName;

							CameraGeneralRuntime g;
							if (!Global.CamGeneral.TryGetValue(keyCamGen, out g) || g == null)
								continue;

							bool isMain = g.MainUsed == "1" && g.MainChannel == channel.ToString();
							bool isSecond = g.SecondUsed == "1" && g.SecondChannel == channel.ToString();
							if (!isMain && !isSecond) continue; // 不属于本通道，跳过

							// 组合路径并加载
							string acqPath = VppPath.Acq(jobId, camName, posName);
							string tbPath = VppPath.Tb(jobId, camName, posName);

							if (!System.IO.File.Exists(acqPath))
							{
								LogChangeEventArgs.Set("Log", $"Lack of Acq: {acqPath}", Color.Red);
								continue;
							}
							if (!System.IO.File.Exists(tbPath))
							{
								LogChangeEventArgs.Set("Log", $"Lack of VPP: {tbPath}", Color.Red);
								continue;
							}

							var acq = CogSerializer.LoadObjectFromFile(acqPath) as CogAcqFifoTool;
							var tb = CogSerializer.LoadObjectFromFile(tbPath) as CogToolBlock;

							if (acq == null) { LogChangeEventArgs.Set("Log", $"Load Failed: {acqPath}", Color.Red); continue; }
							if (tb == null) { LogChangeEventArgs.Set("Log", $"Load Failed: {tbPath}", Color.Red); continue; }

							// 缓存键（含 channel）
							string cacheKey = VppCache.Key(jobId, channel, camName, posName);
							VppCache.Acq[cacheKey] = acq;
							VppCache.Tb[cacheKey] = tb;

							LogChangeEventArgs.Set("Log",
								$"Channel {channel}: Load Complete{camName}/{posName} {(isMain ? "[Main]" : "[Second]")}",
								Color.Black);
						}
					}
					Global.Model_JobID[channel] = jobId;
					DataChangedEventArgs.Set("JobID" + channel, jobId);

				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log",
						$"Channel {channel}: ChangeJob Failed: {ex.Message}", Color.Red);
				}
			}

		}

		public static void ReleaseAllVPP()
		{
			lock(_jobSwitchLock)
			{
				var acqs = Load_Job.VppCache.Acq.Values.ToList();
				foreach (var a in acqs) SafeDisposeAcq(a);
			}
		}

		private static void SafeDisposeAcq(CogAcqFifoTool acq)
		{
			if (acq == null) return;
			try
			{
				try
				{
					if (acq.Operator != null && acq.Operator.FrameGrabber != null)
						acq.Operator.FrameGrabber.Disconnect(true);
				}
				catch { }
				acq.Dispose();
			}
			catch {}
		}

	}
}



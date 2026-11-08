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
	public partial class Load_Job
	{
		public static class VppCache
		{
			// The last acquired image is deliberately kept separate from the VPP
			// serialization/cache.  In particular, an LMI 3D image is a live SDK
			// object and must never be written into Inspection.vpp just because an
			// engineer opened the algorithm editor.
			public sealed class LatestInspectionImage
			{
				public ICogImage Image { get; internal set; }
				public DateTime CapturedAt { get; internal set; }
				public string Serial { get; internal set; }
				public string InputData { get; internal set; }
			}

			private static readonly object LatestImageGate = new object();
			private static readonly Dictionary<string, LatestInspectionImage> LatestImages =
				new Dictionary<string, LatestInspectionImage>(StringComparer.OrdinalIgnoreCase);

			/// <summary>Raised after a successful acquisition. Subscribers must not dispose the image.</summary>
			public static event Action<string, string, string> LatestInspectionImageUpdated;
			public static readonly Dictionary<string, CogAcqFifoTool> Acq =
		new Dictionary<string, CogAcqFifoTool>(StringComparer.OrdinalIgnoreCase);

			public static readonly Dictionary<string, CogToolBlock> Tb =
				new Dictionary<string, CogToolBlock>(StringComparer.OrdinalIgnoreCase);

			// 统一的Key：Job|CHx|Cam|Pos
			public static string Key(string job, int channel, string cam, string pos)
				=> $"{job}|CH{channel}|{cam}|{pos}";

			private static string LatestImageKey(string job, string cam, string pos)
				=> $"{job}|{cam}|{pos}";

			/// <summary>
			/// Retains the newest original acquisition image for the selected
			/// Job/Camera/Position. This is an in-memory editing aid only.
			/// </summary>
			public static void RememberLatestInspectionImage(string job, string cam, string pos, ICogImage image, string serial = null, string inputData = null)
			{
				if (image == null || string.IsNullOrWhiteSpace(job) || string.IsNullOrWhiteSpace(cam) || string.IsNullOrWhiteSpace(pos)) return;
				lock (LatestImageGate)
					LatestImages[LatestImageKey(job, cam, pos)] = new LatestInspectionImage { Image = image, CapturedAt = DateTime.Now, Serial = serial, InputData = inputData };
				try { LatestInspectionImageUpdated?.Invoke(job, cam, pos); } catch { /* editor observers must not affect inspection */ }
			}

			public static bool TryGetLatestInspectionImage(string job, string cam, string pos, out LatestInspectionImage latest)
			{
				lock (LatestImageGate)
					return LatestImages.TryGetValue(LatestImageKey(job, cam, pos), out latest) && latest != null && latest.Image != null;
			}

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

			// A channel owns only its currently loaded Job. Clearing the target
			// Job alone leaves every previously selected Job resident indefinitely.
			public static void ClearChannel(int channel)
			{
				lock (_jobSwitchLock)
				{
					string marker = "|CH" + channel + "|";
					var keys = Acq.Keys.Concat(Tb.Keys).Where(k => k.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
					foreach (var key in keys)
					{
						CogAcqFifoTool acq;
						CogToolBlock tb;
						// Do not disconnect shared frame grabbers used by other channels.
						if (Acq.TryGetValue(key, out acq)) { Acq.Remove(key); try { acq?.Dispose(); } catch { } }
						if (Tb.TryGetValue(key, out tb)) { Tb.Remove(key); try { tb?.Dispose(); } catch { } }
					}
				}
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

		public static void EnsureCognexInteropLoaded()
		{
			// Load the primary interop assembly in the default CLR context before
			// the SDK creates a COM wrapper. Do not load a DLL by an arbitrary path.
			var expected = new System.Reflection.AssemblyName(typeof(CogStringCollection).Assembly.FullName);
			expected.Name = "Cognex.VisionPro.Interop.Core";
			var interop = System.Reflection.Assembly.Load(expected);
			if (interop.GetName().Version != expected.Version)
				throw new InvalidOperationException("Cognex Core/Interop version mismatch: " + interop.FullName);
		}

		// Editors can load their own tool even when camera startup validation failed.
		public static CogToolBlock GetOrLoadInspection(string job, int channel, string cam, string pos)
		{
			lock (_jobSwitchLock)
			{
				CogToolBlock tool;
				if (VppCache.TryGetTb(job, channel, cam, pos, out tool) && tool != null) return tool;
				tool = ReadVpp<CogToolBlock>(VppPath.Tb(job, cam, pos));
				VppCache.Set(job, channel, cam, pos, null, tool);
				return tool;
			}
		}

		public static CogAcqFifoTool GetOrLoadCamera(string job, int channel, string cam, string pos)
		{
			lock (_jobSwitchLock)
			{
				CogAcqFifoTool tool;
				if (VppCache.TryGetAcq(job, channel, cam, pos, out tool) && tool != null) return tool;
				tool = ReadVpp<CogAcqFifoTool>(VppPath.Acq(job, cam, pos));
				VppCache.Set(job, channel, cam, pos, tool, null);
				return tool;
			}
		}

        internal static void ReloadCameraBinding(string job,int channel,string cam,string pos)
        {
            lock(_jobSwitchLock)
            {
                // Deserialize replacements first. A broken file must not destroy the live cached tool.
                CogToolBlock next=ReadVpp<CogToolBlock>(VppPath.Tb(job,cam,pos),false);
                CogAcqFifoTool acq=null;
                try { if(!CameraIntegration.CameraAcquisition.IsSdk(CameraIntegration.CameraAcquisition.Find(Global.CurrentConfig,job,cam))) acq=ReadVpp<CogAcqFifoTool>(VppPath.Acq(job,cam,pos),false); }
                catch { next.Dispose(); throw; }
                string key=VppCache.Key(job,channel,cam,pos);
                CogToolBlock previous;CogAcqFifoTool previousAcq;
                VppCache.Tb.TryGetValue(key,out previous);VppCache.Acq.TryGetValue(key,out previousAcq);
                VppCache.Tb[key]=next;
                if(acq!=null) VppCache.Acq[key]=acq;else VppCache.Acq.Remove(key);
                previous?.Dispose();previousAcq?.Dispose();
            }
        }

		private static T ReadVpp<T>(string path,bool reportToPanel=true) where T : class
		{
			if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException("VPP file not found: " + path, path);
			EnsureCognexInteropLoaded();
			object loaded;
			DiagnosticLogStore.Stage("VPP " + path, "Deserialize begin");
			try { loaded = CogSerializer.LoadObjectFromFile(path); DiagnosticLogStore.Stage("VPP " + path, "Deserialize returned"); }
			catch (Exception ex)
			{
                DiagnosticLogStore.Exception("VPP deserialize " + path, ex);
				var root = ex.GetBaseException();
				var assemblies = AppDomain.CurrentDomain.GetAssemblies()
					.Where(a => a.GetName().Name.StartsWith("Cognex.VisionPro", StringComparison.OrdinalIgnoreCase))
					.Select(a => a.FullName + " : " + a.Location);
				if(reportToPanel) LogChangeEventArgs.Set("Log", "VPP: " + path + "\r\nCognex runtime assemblies:\r\n" + string.Join("\r\n", assemblies), Color.Red);
				if (reportToPanel && root is System.IO.FileLoadException file)
					LogChangeEventArgs.Set("Log", "Assembly binding failure: " + file.FileName + "\r\n" + file.FusionLog, Color.Red);
				throw;
			}
			var tool = loaded as T;
			if (tool == null)
			{
				(loaded as IDisposable)?.Dispose();
				throw new InvalidOperationException("Unexpected VPP type, expected " + typeof(T).Name + ": " + path);
			}
			return tool;
		}

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
			LoadVPP_ForChannel_AllPos(channel, jobId, null);
		}

		// Optional startup observer only; normal job switches retain the same
		// loading order, cache handling and exception policy.
		public static void LoadVPP_ForChannel_AllPos(int channel, string jobId, Action<string> progress)
		{
			ReportVppProgress(progress, $"CH{channel} / {jobId}: reading channel/position configuration");
			// 刷新 CamGeneral（只装当前 Job）
			AppConfig _config = XmlConfigHelper.Load(Global.ParameterCogfig);
			XmlConfigHelper.Load_Job(_config, jobId);

			int configuredChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());
			if (channel < 0 || channel >= configuredChannelCount)
			{
				LogChangeEventArgs.Set("Log", $"ChangeJob: not correct {channel}", Color.Red);
				ReportVppProgress(progress, $"FAIL CH{channel}: outside configured channel count {configuredChannelCount}");
				return;
			}
			if (string.IsNullOrWhiteSpace(jobId))
			{
				LogChangeEventArgs.Set("Log", $"ChangeJob: JobID is null（ch={channel})", Color.Red);
				ReportVppProgress(progress, $"FAIL CH{channel}: JobID is empty");
				return;
			}

			lock (_jobSwitchLock)
			{

				var channelTimer = System.Diagnostics.Stopwatch.StartNew();
				int loadedFiles = 0, failedFiles = 0;
				try
				{
					// Release old Job tools on this channel before loading the new Job.
					VppCache.ClearChannel(channel);

					for (int i = 0; i < Global.CamN_Use; i++)
					{
						string camName = "Cam" + (i + 1);
						var cameraConfig = CameraIntegration.CameraAcquisition.Find(_config, jobId, camName);
						bool sdkCamera = CameraIntegration.CameraAcquisition.IsSdk(cameraConfig);

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

							// An unavailable acquisition device must not hide the inspection algorithm.
							CogToolBlock tb = null;
							CogAcqFifoTool acq = null;
							string context = $"CH{channel} / {jobId} / {camName} / {posName} {(isMain ? "[Main]" : "[Second]")}";
							var fileTimer = System.Diagnostics.Stopwatch.StartNew();
							ReportVppProgress(progress, context + " - Loading Inspection: " + tbPath);
							try
							{
								tb = GetOrLoadInspection(jobId, channel, camName, posName);
								loadedFiles++;
								ReportVppProgress(progress, $"OK {context} - Inspection ({fileTimer.Elapsed.TotalSeconds:F2}s)");
							}
							catch (Exception ex)
							{
								failedFiles++;
								ReportVppProgress(progress, $"FAIL {context} - Inspection ({fileTimer.Elapsed.TotalSeconds:F2}s): {ex.GetBaseException().Message}");
								LogChangeEventArgs.Set("Log", "Load failed: " + tbPath + "\r\n" + ex.GetBaseException().Message, Color.Red);
							}
							fileTimer.Restart();
							if (sdkCamera)
								ReportVppProgress(progress, context + " - SDK acquisition: " + cameraConfig.ConnectionMode + " / " + cameraConfig.IpAddress + " (Camera.vpp not required)");
							else
							{
							ReportVppProgress(progress, context + " - Loading Camera: " + acqPath);
							try
							{
								acq = GetOrLoadCamera(jobId, channel, camName, posName);
								loadedFiles++;
								ReportVppProgress(progress, $"OK {context} - Camera ({fileTimer.Elapsed.TotalSeconds:F2}s)");
							}
							catch (Exception ex)
							{
								failedFiles++;
								ReportVppProgress(progress, $"FAIL {context} - Camera ({fileTimer.Elapsed.TotalSeconds:F2}s): {ex.GetBaseException().Message}");
								LogChangeEventArgs.Set("Log", "Load failed: " + acqPath + "\r\n" + ex.GetBaseException().Message, Color.Red);
							}
							}
							if (tb == null || (!sdkCamera && acq == null)) continue;

							LogChangeEventArgs.Set("Log",
								$"Channel {channel}: Load Complete{camName}/{posName} {(isMain ? "[Main]" : "[Second]")}",
								Color.Black);
						}
					}
					Global.Model_JobID[channel] = jobId;
					DataChangedEventArgs.Set("JobID" + channel, jobId);
					ReportVppProgress(progress, $"CH{channel} / {jobId}: {loadedFiles} loaded, {failedFiles} failed; {channelTimer.Elapsed.TotalSeconds:F2}s" +
						(loadedFiles + failedFiles == 0 ? " (no positions assigned to this channel)" : ""));

				}
				catch (Exception ex)
				{
					ReportVppProgress(progress, $"FAIL CH{channel} / {jobId}: {ex.GetBaseException().Message}");
					LogChangeEventArgs.Set("Log",
						$"Channel {channel}: ChangeJob Failed: {ex.Message}", Color.Red);
				}
			}

		}

		private static void ReportVppProgress(Action<string> progress, string message)
		{
			// A failed observer must never interfere with a running job/PLC.
			try { progress?.Invoke(message); } catch { }
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



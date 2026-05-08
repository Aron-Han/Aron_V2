using Aron_V2.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsFormsApp2;

namespace Aron_V2
{
	public static class Bootstrap
	{
		public static InitResult DoHeavyInit(IProgress<ProgressInfo> progress, CancellationToken ct)
		{
			LogManager.WriteLog("========== Software startup begin ==========");
			int step = 0;
			const int TOTAL = 9;
			var totalTimer = System.Diagnostics.Stopwatch.StartNew();
			var stageTimer = System.Diagnostics.Stopwatch.StartNew();
			string currentStage = string.Empty;
			Action<string> detail = msg =>
			{
                DiagnosticLogStore.Stage("Startup", msg);
				try { LogManager.WriteLog("[Startup] " + msg); } catch { }
				try { progress?.Report(new ProgressInfo(Math.Max(0, (step - 1) * 100 / TOTAL), msg)); } catch { }
			};
			Action<string> tick = msg =>
			{
				if (step > 0) detail($"Completed {currentStage} ({stageTimer.Elapsed.TotalSeconds:F2}s)");
				step++;
				currentStage = msg;
				stageTimer.Restart();
				detail($"Step {step}/{TOTAL} - {msg}");
			};

			// 0) IO 队列
			tick("Start IO …");
			IoWorkers.Start();
			ct.ThrowIfCancellationRequested();

			// 1) 配置
			tick("Load Config…");
			detail("Configuration: " + Global.ParameterCogfig);
			var config = XmlConfigHelper.Load(Global.ParameterCogfig);
			Global.CurrentConfig = config;
			XmlConfigHelper.EnsurePositionGeneral(config);
			XmlConfigHelper.Load_Job(config, Global.Model_JobID[0]);
			XmlConfigHelper.ini_Parameters();
			detail($"Configuration loaded: {Global.CamN_Use} active cameras; initial job {Global.Model_JobID[0]}");
			ct.ThrowIfCancellationRequested();

			// 2) 存图设置
			tick("Load Saveimage Config…");
			ImageRecord.LoadSettings();

			// 3) VPP 输出映射
			tick("Load VPP output config…");
			detail("Output mapping: " + (string.IsNullOrEmpty(Global.VppOutputCfgPath)
				? ProjectPaths.Config("Communication/VPPOutput.xml") : Global.VppOutputCfgPath));
			var vppOutCfg = XmlConfigHelper.LoadVppOutput(
				string.IsNullOrEmpty(Global.VppOutputCfgPath)
					? ProjectPaths.Config("Communication/VPPOutput.xml")
					: Global.VppOutputCfgPath
			);
			ct.ThrowIfCancellationRequested();

			// 4) CamLocks
			tick("ini Camera lock…");
			var camLocks = new Dictionary<string, object>();
			for (int i = 1; i <= Global.CamN_Use; i++)
				camLocks["Cam" + i] = new object();

			// 5) 预加载 VPP
			tick("Load VPP…");
			int startupChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());
			detail($"VPP preload: Job1; {startupChannelCount} channels; root {Load_Job.VppPath.Root}");
			for (int ch = 0; ch < startupChannelCount; ch++)
			{
				ct.ThrowIfCancellationRequested();
				Load_Job.LoadVPP_ForChannel_AllPos(ch, "Job1", detail);
			}

			// 6) 数据库
			tick("Load database…");
			detail("Database: " + Global.dBPath);
			DataBase.InitializeStorage();

			// 7) PLC（可放后台或这里只读状态）
			tick("Check communication status…");
			bool plcConnected = CC24_Comm.Instance().IsConnected;
			detail("Communication status: " + (plcConnected ? "connected" : "not connected (main window will perform the normal startup checks)"));

			// 8) 用户与权限
			tick("Ini access…");
			var store = UserStore.Load();
			if (store.Users.Count == 0)
			{
				string _err;
				UserStore.AddUser(store, "admin", "admin", "Admin", out _err);
				UserStore.Save(store);
			}
			var guest = new UserRecord { Username = "Guest", Role = "Guest" };
			detail($"Completed {currentStage} ({stageTimer.Elapsed.TotalSeconds:F2}s)");
			string finished = $"Initialization finished ({totalTimer.Elapsed.TotalSeconds:F2}s). See FAIL entries above for any VPP loading errors.";
			try { LogManager.WriteLog("[Startup] " + finished); } catch { }
			try { progress?.Report(new ProgressInfo(100, finished)); } catch { }

			return new InitResult(
				config,
				vppOutCfg,
				camLocks,
				Global.Model_JobID,
				null,
				plcConnected,
				store,
				guest
			);
		}
	}
}

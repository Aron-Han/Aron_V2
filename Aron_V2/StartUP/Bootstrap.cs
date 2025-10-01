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
			Action<string> tick = msg =>
			{
				step++;
				int p = step * 100 / TOTAL;

				string log = $"[Startup] Step {step}/{TOTAL} {p}% - {msg}";

				try
				{
					LogManager.WriteLog(log);
				}
				catch
				{
				}

				if (progress != null)
					progress.Report(new ProgressInfo(p, msg));
			};

			// 0) IO 队列
			tick("Start IO …");
			IoWorkers.Start();
			ct.ThrowIfCancellationRequested();

			// 1) 配置
			tick("Load Config…");
			var config = XmlConfigHelper.Load(Global.ParameterCogfig);
			Global.CurrentConfig = config;
			XmlConfigHelper.EnsurePositionGeneral(config);
			XmlConfigHelper.Load_Job(config, Global.Model_JobID[0]);
			XmlConfigHelper.ini_Parameters();
			ct.ThrowIfCancellationRequested();

			// 2) 存图设置
			tick("Load Saveimage Config…");
			ImageRecord.LoadSettings();

			// 3) VPP 输出映射
			tick("Load VPP output config…");
			var vppOutCfg = XmlConfigHelper.LoadVppOutput(
				string.IsNullOrEmpty(Global.VppOutputCfgPath)
					? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VppOutput.xml")
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
			for (int ch = 0; ch < 4; ch++)
			{
				ct.ThrowIfCancellationRequested();
				Load_Job.LoadVPP_ForChannel_AllPos(ch, "Job1");
			}

			// 6) 数据库
			tick("Load database…");
			DatabaseInitializer.Init("mydata.db");

			// 7) PLC（可放后台或这里只读状态）
			tick("Connect PLC…");
			bool plcConnected = CC24_Comm.Instance().IsConnected;

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

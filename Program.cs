using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	internal static class Program
	{
		private static int _cameraRestartRequested;
		private static int _cameraRestartCount;
		private const string RestartCountArg = "--camera-restart-count=";
		internal static int CameraRestartCount { get { return Interlocked.CompareExchange(ref _cameraRestartCount, 0, 0); } }

		internal static int ParseCameraRestartCount(string[] args)
		{
			if (args == null) return 0;
			foreach (string arg in args)
			{
				if (arg == null || !arg.StartsWith(RestartCountArg, StringComparison.OrdinalIgnoreCase)) continue;
				int count;
				if (int.TryParse(arg.Substring(RestartCountArg.Length), out count) && count >= 0)
					return count;
			}
			return 0;
		}

		internal static void ResetCameraRestartCount()
		{
			Interlocked.Exchange(ref _cameraRestartCount, 0);
		}

		internal static void RequestCameraRestart()
		{
			if (Interlocked.Exchange(ref _cameraRestartRequested, 1) == 0)
				Interlocked.Increment(ref _cameraRestartCount);
		}

		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
            // Preserve the filesystem-only maintenance command's isolation.
            if (args != null && args.Any(a => string.Equals(a, "--organize-project", StringComparison.OrdinalIgnoreCase))) { MainCore(args); return; }
            DiagnosticLogStore.Initialize();
            DiagnosticExceptionHooks.Install();
            try { MainCore(args); }
            catch (Exception ex) { DiagnosticLogStore.WriteCrashReport(ex, "Program.Main", true); throw; }
            finally { DiagnosticLogStore.Shutdown(); }
		}

        private static void MainCore(string[] args)
		{
            // File organization only: no VisionPro initialization, forms,
            // camera SDK, database access, or communication service startup.
            if (args != null && args.Any(a => string.Equals(a, "--organize-project", StringComparison.OrdinalIgnoreCase)))
            {
                try { ProjectPaths.OrganizeApplicationData(); }
                catch { Environment.ExitCode = 3; }
                return;
            }
			bool checkOnly = args != null && args.Any(a => string.Equals(a, "--visionpro-check", StringComparison.OrdinalIgnoreCase));
			string report;
			bool valid = VisionProRuntimeAudit.Start(out report);
			if (checkOnly || !valid)
			{
				Environment.ExitCode = valid ? 0 : 2;
				if (!checkOnly) MessageBox.Show(report, "VisionPro DLL check", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			RunApplication(args);
		}

		// Keep Cognex-dependent UI types out of the entry-point JIT until the
		// dependency preflight is complete. The existing application flow is intact.
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
		private static void RunApplication(string[] args)
		{
			_cameraRestartCount = ParseCameraRestartCount(args);
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			ProjectPaths.OrganizeApplicationData();
			ImageRecord.LoadSettings();
			UiLanguage.Start(ImageRecord.Current.Language);
			UI_Control.Init();
			using (var main = new FormMain())
			{
				// ② 再跑启动窗体（LoadJob 全在里面）
				using (var splash = new StartUP())
				{
					var dr = splash.ShowDialog();
					if (dr != DialogResult.OK) return;

					// ③ 将初始化结果注入已存在的主窗体
					main.ApplyInit(splash.Result);
				}

				// ④ 真正进入消息循环（这时所有控件名/文本已被 LoadJob 期间的事件更新过）
				Application.Run(main);
			}

			if (Interlocked.CompareExchange(ref _cameraRestartRequested, 0, 0) == 1)
			{
				// 旧进程释放相机资源后再启动新进程，避免抢占设备。
				Thread.Sleep(5000);
				try
				{
					Process.Start(new ProcessStartInfo(Application.ExecutablePath)
					{
						Arguments = RestartCountArg + CameraRestartCount,
						WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
						UseShellExecute = false
					});
				}
				catch (Exception ex)
				{
					LogManager.WriteLog("Camera recovery restart failed: " + ex);
				}
			}
		}
	}
}

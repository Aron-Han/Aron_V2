using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace Aron_V2
{
	public sealed class LicenseMonitor : IDisposable
	{
		private readonly string _expectedSerial;
		private readonly int _intervalMs;
		private Timer _timer;
		private int _checking = 0;
		private bool? _lastOk = null;
		private bool _disposed = false;

		public event EventHandler<LicenseStatusChangedEventArgs> StatusChanged;

		public LicenseMonitor(string expectedSerial, int intervalMs)
		{
			_expectedSerial = expectedSerial;
			_intervalMs = intervalMs <= 0 ? 10000 : intervalMs;
		}

		public void Start()
		{
			if (_timer != null) return;

			// 立即检查一次，然后按周期检查
			_timer = new Timer(CheckTimerCallback, null, 1000, _intervalMs);
		}

		public void Stop()
		{
			if (_timer != null)
			{
				_timer.Dispose();
				_timer = null;
			}
		}

		private void CheckTimerCallback(object state)
		{
			if (_disposed) return;

			// 防止上一次还没结束，下一次又进来
			if (Interlocked.Exchange(ref _checking, 1) == 1)
				return;

			try
			{
				LicenseCheckResult result = CheckLicense();

				// 只在状态变化时通知 UI，避免一直刷日志
				if (_lastOk == null || _lastOk.Value != result.IsOK)
				{
					_lastOk = result.IsOK;
					OnStatusChanged(result);
				}
			}
			catch (Exception ex)
			{
				LicenseCheckResult result = new LicenseCheckResult();
				result.IsOK = false;
				result.Message = "License monitor exception: " + ex.Message;
				OnStatusChanged(result);
			}
			finally
			{
				Interlocked.Exchange(ref _checking, 0);
			}
		}

		private void OnStatusChanged(LicenseCheckResult result)
		{
			EventHandler<LicenseStatusChangedEventArgs> handler = StatusChanged;
			if (handler != null)
			{
				handler(this, new LicenseStatusChangedEventArgs(result));
			}
		}

		public LicenseCheckResult CheckLicense()
		{
			LicenseCheckResult r = new LicenseCheckResult();
			r.IsOK = false;
			r.CodeMeterServiceOK = false;
			r.DongleFound = false;
			r.Message = "";
			r.RawOutput = "";

			// 1. 检查 CodeMeter 服务
			r.CodeMeterServiceOK = IsCodeMeterServiceRunning();
			if (!r.CodeMeterServiceOK)
			{
				r.Message = "CodeMeter Runtime Server not running";
				return r;
			}

			// 2. 查找 cmu32.exe
			string cmuPath = FindCmu32Path();
			if (string.IsNullOrEmpty(cmuPath) || !File.Exists(cmuPath))
			{
				r.Message = "donot find cmu32.exe";
				return r;
			}

			// 3. 查询加密狗列表
			string output = RunProcess(cmuPath, "--list", 5000);
			r.RawOutput = output;

			if (string.IsNullOrWhiteSpace(output))
			{
				r.Message = "cmu32 --list no output";
				return r;
			}

			// 4. 判断指定加密狗序列号是否存在
			if (!string.IsNullOrWhiteSpace(_expectedSerial))
			{
				r.DongleFound = output.IndexOf(_expectedSerial, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			else
			{
				// 没填序列号时只能粗略判断
				r.DongleFound =
					output.IndexOf("CmContainer", StringComparison.OrdinalIgnoreCase) >= 0 ||
					output.IndexOf("Serial", StringComparison.OrdinalIgnoreCase) >= 0;
			}

			if (!r.DongleFound)
			{
				r.Message = "Do not find Cognex CodeMeter Lincese: " + _expectedSerial;
				return r;
			}

			r.IsOK = true;
			r.Message = "CodeMeter Service is normal，Cognex Lincese Online";
			return r;
		}

		private bool IsCodeMeterServiceRunning()
		{
			try
			{
				ServiceController[] services = ServiceController.GetServices();

				foreach (ServiceController s in services)
				{
					string serviceName = s.ServiceName ?? "";
					string displayName = s.DisplayName ?? "";

					if (serviceName.IndexOf("CodeMeter", StringComparison.OrdinalIgnoreCase) >= 0 ||
						displayName.IndexOf("CodeMeter", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return s.Status == ServiceControllerStatus.Running;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private string FindCmu32Path()
		{
			string p1 = @"C:\Program Files (x86)\CodeMeter\Runtime\bin\cmu32.exe";
			string p2 = @"C:\Program Files\CodeMeter\Runtime\bin\cmu32.exe";

			if (File.Exists(p1)) return p1;
			if (File.Exists(p2)) return p2;

			return "";
		}

		private string RunProcess(string fileName, string arguments, int timeoutMs)
		{
			Process p = null;

			try
			{
				ProcessStartInfo psi = new ProcessStartInfo();
				psi.FileName = fileName;
				psi.Arguments = arguments;
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;
				psi.RedirectStandardError = true;
				psi.CreateNoWindow = true;
				psi.StandardOutputEncoding = Encoding.Default;
				psi.StandardErrorEncoding = Encoding.Default;

				p = new Process();
				p.StartInfo = psi;
				p.Start();

				string output = p.StandardOutput.ReadToEnd();
				string error = p.StandardError.ReadToEnd();

				bool exited = p.WaitForExit(timeoutMs);

				if (!exited)
				{
					try { p.Kill(); } catch { }
					return "cmu32 timeout";
				}

				return output + Environment.NewLine + error;
			}
			catch (Exception ex)
			{
				return "RunProcess Error: " + ex.Message;
			}
			finally
			{
				if (p != null)
					p.Dispose();
			}
		}

		public void Dispose()
		{
			_disposed = true;
			Stop();
		}
	}

	public sealed class LicenseCheckResult
	{
		public bool IsOK { get; set; }
		public bool CodeMeterServiceOK { get; set; }
		public bool DongleFound { get; set; }
		public string Message { get; set; }
		public string RawOutput { get; set; }
	}

	public sealed class LicenseStatusChangedEventArgs : EventArgs
	{
		public LicenseCheckResult Result { get; private set; }

		public LicenseStatusChangedEventArgs(LicenseCheckResult result)
		{
			Result = result;
		}
	}
}
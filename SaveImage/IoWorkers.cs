using Aron_V2;
using Aron_V2.UI_Update;
using Cognex.VisionPro;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using WindowsFormsApp2;

public static class IoWorkers
{
	// —— 1) 图片保存任务 ——
	public sealed class SaveImageTask
	{
		public string JobId, CamName, PosName, Serial;
		public bool OkFlag;
		public string FullPath;
		public string Ext;        // ".jpg" / ".png" / ".bmp"
		public Bitmap Bmp;
	}

	// —— 2) FTP 上传任务 ——
	private sealed class FtpUploadTask
	{
		public string LocalFilePath;
		public string RemoteDir;
		public string RemoteFileName;

		public string Host;
		public string Username;
		public string Password;
		public int Port;
	}

	private static readonly BlockingCollection<SaveImageTask> _imgQ =
		new BlockingCollection<SaveImageTask>(new ConcurrentQueue<SaveImageTask>(), 1024);

	private static readonly BlockingCollection<FtpUploadTask> _ftpQ =
		new BlockingCollection<FtpUploadTask>(new ConcurrentQueue<FtpUploadTask>(), 1024);

	private static CancellationTokenSource _cts;
	private static Thread _imgThread;
	private static Thread _ftpThread;

	// 默认 FTP 参数
	private const int DefaultFtpPort = 21;

	// 你现在 Settings 里没有 FTP 远程根目录配置，所以先固定一个
	// 后面如果想做成可配置，再加到 ImageRecord.Settings 里
	private const string DefaultFtpRoot = "/Images";

	public static void Start()
	{
		if (_cts != null) return;

		_cts = new CancellationTokenSource();

		_imgThread = new Thread(() => RunImageSaver(_cts.Token))
		{
			IsBackground = true,
			Name = "ImageSaver"
		};
		_imgThread.Start();

		_ftpThread = new Thread(() => RunFtpUploader(_cts.Token))
		{
			IsBackground = true,
			Name = "FtpUploader"
		};
		_ftpThread.Start();
	}

	public static void Stop()
	{
		if (_cts == null) return;

		_cts.Cancel();

		_imgQ.CompleteAdding();
		_ftpQ.CompleteAdding();

		try
		{
			if (_imgThread != null && _imgThread.IsAlive)
				_imgThread.Join(1500);
		}
		catch { }

		try
		{
			if (_ftpThread != null && _ftpThread.IsAlive)
				_ftpThread.Join(1500);
		}
		catch { }

		try { _cts.Dispose(); } catch { }
		_cts = null;
		_imgThread = null;
		_ftpThread = null;
	}

	public static void EnqueueImage(SaveImageTask job)
	{
		if (job == null)
			return;

		if (!_imgQ.TryAdd(job))
		{
			try { job.Bmp?.Dispose(); } catch { }
			LogChangeEventArgs.Set("Log", "Image queue is full, drop one frame.", Color.OrangeRed);
		}
	}

	private static void EnqueueFtp(FtpUploadTask job)
	{
		if (job == null)
			return;

		if (!_ftpQ.TryAdd(job))
		{
			LogChangeEventArgs.Set("Log", "FTP queue is full, drop one upload task.", Color.OrangeRed);
		}
	}

	private static void RunImageSaver(CancellationToken ct)
	{
		try
		{
			foreach (var job in _imgQ.GetConsumingEnumerable(ct))
			{
				try
				{
					if (job == null) continue;

					var dir = Path.GetDirectoryName(job.FullPath);
					if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
						Directory.CreateDirectory(dir);

					if (job.Ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
						job.Ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
					{
						var enc = ImageCodecInfo.GetImageEncoders()
							.FirstOrDefault(x => x.MimeType == "image/jpeg");

						if (enc != null)
						{
							using (var ep = new EncoderParameters(1))
							{
								ep.Param[0] = new EncoderParameter(Encoder.Quality, 85L);
								job.Bmp.Save(job.FullPath, enc, ep);
							}
						}
						else
						{
							job.Bmp.Save(job.FullPath, ImageFormat.Jpeg);
						}
					}
					else if (job.Ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
					{
						using (var converted = ConvertTo8bppIndexedGrayscale(job.Bmp))
						{
							converted.Save(job.FullPath, ImageFormat.Bmp);
						}
					}
					else
					{
						job.Bmp.Save(job.FullPath, ImageFormat.Png);
					}

					// ===== 本地保存成功后，再异步排队 FTP =====
					TryQueueFtpAfterLocalSaved(job);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Async save image error: " + ex.Message, Color.Red);
				}
				finally
				{
					try { job?.Bmp?.Dispose(); } catch { }
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 优雅退出
		}
	}

	private static void TryQueueFtpAfterLocalSaved(SaveImageTask job)
	{
		try
		{
			if (job == null) return;

			if (!ImageRecord.Current.FTP_Enable)
				return;

			string host = (ImageRecord.Current.FTP_Host ?? "").Trim();
			string user = (ImageRecord.Current.FTP_Username ?? "").Trim();
			string pwd = ImageRecord.Current.FTP_Password ?? "";
			string Root = (ImageRecord.Current.FTP_Root ?? "").Trim();
			Root = Root + "/" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString();

			if (string.IsNullOrWhiteSpace(host))
			{
				LogChangeEventArgs.Set("Log", "FTP enabled but host is empty, skip upload.", Color.OrangeRed);
				return;
			}

			if (string.IsNullOrWhiteSpace(user))
			{
				LogChangeEventArgs.Set("Log", "FTP enabled but username is empty, skip upload.", Color.OrangeRed);
				return;
			}

			if (string.IsNullOrWhiteSpace(job.FullPath) || !File.Exists(job.FullPath))
			{
				LogChangeEventArgs.Set("Log", "FTP skipped, local file not found: " + job.FullPath, Color.OrangeRed);
				return;
			}

			string remoteDir = BuildRemoteDir(job);
			string remoteFileName = Path.GetFileName(job.FullPath);

			EnqueueFtp(new FtpUploadTask
			{
				LocalFilePath = job.FullPath,
				RemoteDir = Root,
				RemoteFileName = remoteFileName,
				Host = host,
				Username = user,
				Password = pwd,
				Port = DefaultFtpPort
			});
		}
		catch (Exception ex)
		{
			LogChangeEventArgs.Set("Log", "Queue FTP failed: " + ex.Message, Color.Red);
		}
	}

	private static string BuildRemoteDir(SaveImageTask job)
	{
		string date = DateTime.Now.ToString("yyyyMMdd");
		string okNg = job.OkFlag ? "OK" : "NG";

		return DefaultFtpRoot + "/" +
			   SafePathPart(job.JobId) + "/" +
			   SafePathPart(job.CamName) + "/" +
			   SafePathPart(job.PosName) + "/" +
			   date + "/" +
			   okNg;
	}

	private static string SafePathPart(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return "Unknown";

		string value = s.Trim();
		char[] badChars = Path.GetInvalidFileNameChars();

		foreach (char c in badChars)
		{
			value = value.Replace(c.ToString(), "_");
		}

		value = value.Replace("/", "_").Replace("\\", "_");
		return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
	}

	private static void RunFtpUploader(CancellationToken ct)
	{
		try
		{
			foreach (var job in _ftpQ.GetConsumingEnumerable(ct))
			{
				try
				{
					if (job == null) continue;

					if (string.IsNullOrWhiteSpace(job.LocalFilePath) || !File.Exists(job.LocalFilePath))
					{
						LogChangeEventArgs.Set("Log", "FTP upload skipped, local file not found: " + job.LocalFilePath, Color.Red);
						continue;
					}

					bool ok = false;
					string msg = "";

					// 简单重试 3 次，适合工厂现场偶发网络抖动
					for (int i = 0; i < 3; i++)
					{
						if (ct.IsCancellationRequested)
							break;

						try
						{
							UploadFileToFtp(
								job.Host,
								job.Port,
								job.Username,
								job.Password,
								job.LocalFilePath,
								job.RemoteDir,
								job.RemoteFileName);

							ok = true;
							msg = "FTP upload success: " + job.RemoteDir + "/" + job.RemoteFileName;
							break;
						}
						catch (Exception ex)
						{
							msg = ex.Message;

							if (i < 2)
								Thread.Sleep(500);
						}
					}

					if (ok)
					{
						if (ImageRecord.Current.Show_upload_info)
							LogChangeEventArgs.Set("Log", msg, Color.Green);
					}
					else
					{
						LogChangeEventArgs.Set("Log", "FTP upload failed: " + msg, Color.Red);
					}
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					LogChangeEventArgs.Set("Log", "Async FTP upload error: " + ex.Message, Color.Red);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 优雅退出
		}
	}

	private static void UploadFileToFtp(
		string host,
		int port,
		string username,
		string password,
		string localFilePath,
		string remoteDir,
		string remoteFileName)
	{
		string baseUrl = BuildBaseFtpUrl(host, port);
		string normalizedRemoteDir = NormalizeRemoteDir(remoteDir);

		EnsureFtpDirectoryExists(baseUrl, normalizedRemoteDir, username, password);

		string remoteFileUrl = baseUrl + normalizedRemoteDir + "/" + remoteFileName;
		byte[] fileBytes = File.ReadAllBytes(localFilePath);

		FtpWebRequest request = (FtpWebRequest)WebRequest.Create(remoteFileUrl);
		request.Method = WebRequestMethods.Ftp.UploadFile;
		request.Credentials = new NetworkCredential(username, password);
		request.UseBinary = true;
		request.UsePassive = true;
		request.KeepAlive = false;
		request.Timeout = 15000;
		request.ReadWriteTimeout = 15000;
		request.ContentLength = fileBytes.Length;

		using (Stream requestStream = request.GetRequestStream())
		{
			requestStream.Write(fileBytes, 0, fileBytes.Length);
		}

		using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
		{
			string status = response.StatusDescription;
		}
	}

	private static string BuildBaseFtpUrl(string host, int port)
	{
		string ftpHost = (host ?? "").Trim();

		if (!ftpHost.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
			ftpHost = "ftp://" + ftpHost;

		ftpHost = ftpHost.TrimEnd('/');

		return ftpHost + ":" + port.ToString();
	}

	private static string NormalizeRemoteDir(string remoteDir)
	{
		if (string.IsNullOrWhiteSpace(remoteDir))
			return "";

		string dir = remoteDir.Replace("\\", "/").Trim();

		if (!dir.StartsWith("/"))
			dir = "/" + dir;

		return dir.TrimEnd('/');
	}

	private static void EnsureFtpDirectoryExists(
		string baseUrl,
		string remoteDir,
		string username,
		string password)
	{
		if (string.IsNullOrWhiteSpace(remoteDir) || remoteDir == "/")
			return;

		string[] parts = remoteDir.Trim('/').Split('/');
		string currentPath = "";

		for (int i = 0; i < parts.Length; i++)
		{
			currentPath += "/" + parts[i];
			string currentUrl = baseUrl + currentPath;

			try
			{
				FtpWebRequest request = (FtpWebRequest)WebRequest.Create(currentUrl);
				request.Method = WebRequestMethods.Ftp.MakeDirectory;
				request.Credentials = new NetworkCredential(username, password);
				request.UseBinary = true;
				request.UsePassive = true;
				request.KeepAlive = false;
				request.Timeout = 10000;

				using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
				{
				}
			}
			catch (WebException ex)
			{
				var response = ex.Response as FtpWebResponse;
				if (response != null)
				{
					// 很多 FTP 服务器目录已存在时会报 550，这里直接忽略
					if ((int)response.StatusCode == 550)
						continue;
				}

				throw;
			}
		}
	}

	/// <summary>
	/// 将任意 Bitmap 转为 8bpp 索引灰度图（设置灰度调色板）。
	/// 返回新的 Bitmap（调用者负责 Dispose）。
	/// </summary>
	private static Bitmap ConvertTo8bppIndexedGrayscale(Bitmap src)
	{
		if (src == null) return null;

		Bitmap src24 = src;
		bool tempSrcCreated = false;

		if (src.PixelFormat != PixelFormat.Format24bppRgb)
		{
			src24 = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
			using (Graphics g = Graphics.FromImage(src24))
			{
				g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height));
			}
			tempSrcCreated = true;
		}

		int w = src24.Width;
		int h = src24.Height;
		Bitmap dst = new Bitmap(w, h, PixelFormat.Format8bppIndexed);

		ColorPalette pal = dst.Palette;
		for (int i = 0; i < 256; i++)
			pal.Entries[i] = Color.FromArgb(i, i, i);
		dst.Palette = pal;

		BitmapData srcData = null;
		BitmapData dstData = null;

		try
		{
			srcData = src24.LockBits(
				new Rectangle(0, 0, w, h),
				ImageLockMode.ReadOnly,
				PixelFormat.Format24bppRgb);

			dstData = dst.LockBits(
				new Rectangle(0, 0, w, h),
				ImageLockMode.WriteOnly,
				PixelFormat.Format8bppIndexed);

			int srcStride = Math.Abs(srcData.Stride);
			int dstStride = Math.Abs(dstData.Stride);

			int srcBytes = srcStride * h;
			int dstBytes = dstStride * h;

			byte[] srcBuf = new byte[srcBytes];
			byte[] dstBuf = new byte[dstBytes];

			Marshal.Copy(srcData.Scan0, srcBuf, 0, srcBytes);

			for (int y = 0; y < h; y++)
			{
				int srcRow = y * srcStride;
				int dstRow = y * dstStride;
				int srcIdx = srcRow;
				int dstIdx = dstRow;

				for (int x = 0; x < w; x++)
				{
					byte b = srcBuf[srcIdx++];
					byte g = srcBuf[srcIdx++];
					byte r = srcBuf[srcIdx++];

					int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);
					if (gray < 0) gray = 0;
					else if (gray > 255) gray = 255;

					dstBuf[dstIdx++] = (byte)gray;
				}
			}

			Marshal.Copy(dstBuf, 0, dstData.Scan0, dstBytes);
		}
		finally
		{
			if (srcData != null) src24.UnlockBits(srcData);
			if (dstData != null) dst.UnlockBits(dstData);

			if (tempSrcCreated && src24 != null)
			{
				src24.Dispose();
			}
		}

		return dst;
	}
}
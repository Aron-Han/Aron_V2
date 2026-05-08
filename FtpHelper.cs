using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public class FtpUploadResult
	{
		public bool Success { get; set; }
		public string Message { get; set; }
		public string RemotePath { get; set; }
	}

	public static class FtpHelper
	{
		/// <summary>
		/// 上传本地图片到FTP服务器
		/// </summary>
		/// <param name="serverIp">FTP服务器IP，例如 192.168.1.100</param>
		/// <param name="userName">FTP用户名</param>
		/// <param name="password">FTP密码</param>
		/// <param name="localFilePath">本地文件完整路径</param>
		/// <param name="remoteDir">远程目录，例如 /Images/20260418</param>
		/// <param name="remoteFileName">远程文件名，例如 test001.jpg</param>
		/// <param name="port">FTP端口，默认21</param>
		public static FtpUploadResult UploadFile(
			string serverIp,
			string userName,
			string password,
			string localFilePath,
			string remoteDir,
			string remoteFileName,
			int port = 21)
		{
			FtpUploadResult result = new FtpUploadResult();

			try
			{
				if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
				{
					result.Success = false;
					result.Message = "local file not exist";
					return result;
				}

				string baseUrl = "ftp://" + serverIp + ":" + port.ToString();

				remoteDir = NormalizeRemoteDir(remoteDir);

				// 先确保远程目录存在
				CreateDirectoryIfNotExists(baseUrl, remoteDir, userName, password);

				string remotePath = baseUrl + remoteDir + "/" + remoteFileName;

				byte[] fileBytes = File.ReadAllBytes(localFilePath);

				FtpWebRequest request = (FtpWebRequest)WebRequest.Create(remotePath);
				request.Method = WebRequestMethods.Ftp.UploadFile;
				request.Credentials = new NetworkCredential(userName, password);
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
					result.Success = true;
					result.RemotePath = remotePath;
					result.Message = "上传成功：" + response.StatusDescription;
				}
			}
			catch (WebException ex)
			{
				FtpWebResponse ftpResponse = ex.Response as FtpWebResponse;
				if (ftpResponse != null)
				{
					result.Message = "上传失败：" + ftpResponse.StatusCode + " - " + ftpResponse.StatusDescription;
				}
				else
				{
					result.Message = "上传失败：" + ex.Message;
				}

				result.Success = false;
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Message = "上传失败：" + ex.Message;
			}

			return result;
		}

		/// <summary>
		/// 标准化远程目录
		/// </summary>
		private static string NormalizeRemoteDir(string remoteDir)
		{
			if (string.IsNullOrWhiteSpace(remoteDir))
				return "";

			string dir = remoteDir.Replace("\\", "/").Trim();

			if (!dir.StartsWith("/"))
				dir = "/" + dir;

			return dir.TrimEnd('/');
		}

		/// <summary>
		/// 递归创建FTP目录
		/// </summary>
		private static void CreateDirectoryIfNotExists(
			string baseUrl,
			string remoteDir,
			string userName,
			string password)
		{
			if (string.IsNullOrWhiteSpace(remoteDir))
				return;

			string[] dirs = remoteDir.Trim('/').Split('/');
			string currentDir = "";

			foreach (string dir in dirs)
			{
				currentDir += "/" + dir;
				string dirUrl = baseUrl + currentDir;

				try
				{
					FtpWebRequest request = (FtpWebRequest)WebRequest.Create(dirUrl);
					request.Method = WebRequestMethods.Ftp.MakeDirectory;
					request.Credentials = new NetworkCredential(userName, password);
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
					FtpWebResponse response = ex.Response as FtpWebResponse;
					if (response != null)
					{
						// 目录已存在时，很多FTP服务器会返回550，这里直接忽略
						if ((int)response.StatusCode == 550)
							continue;
					}

					throw;
				}
			}
		}
	}
}

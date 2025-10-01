using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public static class LogManager
	{
		private static readonly object lockObject = new object();
		private static string _logRootDirectory = Path.Combine(Global.LogMangerPath, "ApplicationLogs");

		/// <summary>
		/// 设置日志根目录（可选，默认在程序运行目录下）
		/// </summary>
		/// <param name="path">新的根目录路径</param>
		public static void SetRootDirectory(string path)
		{
			_logRootDirectory = path;
		}

		/// <summary>
		/// 获取当前日期对应的日志文件路径，并创建必要的文件夹。
		/// 结构: 根目录\年\月\日\Log_yyyyMMdd.txt
		/// </summary>
		/// <returns>日志文件的完整路径</returns>
		private static string GetLogFilePath()
		{
			DateTime now = DateTime.Now;
			string year = now.ToString("yyyy");
			string month = now.ToString("MM");
			string day = now.ToString("dd");
			string dateString = now.ToString("yyyyMMdd");

			// 路径: 根目录\年\月\日\
			string logDirectory = Path.Combine(_logRootDirectory, year, month, day);

			// 确保文件夹存在
			if (!Directory.Exists(logDirectory))
			{
				Directory.CreateDirectory(logDirectory);
			}

			// 文件名: Log_yyyyMMdd.txt
			return Path.Combine(logDirectory, $"Log_{dateString}.txt");
		}

		/// <summary>
		/// 实时记录日志信息到文件。
		/// </summary>
		/// <param name="logMessage">要记录的日志内容</param>
		/// <param name="logType">日志类型 (例如：INFO, ERROR, VISION)</param>
		public static void WriteLog(string logMessage, string logType = "INFO")
		{
			// 1. 获取带时间戳的完整日志行
			string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
			string fullLogLine = $"[{timestamp}][{logType}] {logMessage}{Environment.NewLine}";

			// 2. 获取当天的文件路径
			string filePath = GetLogFilePath();

			// 3. 线程安全地写入文件
			lock (lockObject)
			{
				try
				{
					// 使用 File.AppendAllText 以追加模式写入文件
					File.AppendAllText(filePath, fullLogLine);
				}
				catch (Exception ex)
				{
					// 写入失败时，可以考虑向控制台或Debug输出
					System.Diagnostics.Debug.WriteLine($"日志写入失败: {ex.Message}");
					// 通常不在这里抛出异常，以免影响主程序运行
				}
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WindowsFormsApp2
{
	public static class DatabaseInitializer
	{
		public static void Init(string dbPath)
		{
			if (!File.Exists(dbPath))
			{
				SQLiteConnection.CreateFile(dbPath);
			}

			using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
			{
				conn.Open();

				// 建表
				string createSql = @"
                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Serial TEXT,
                    Result TEXT,
                    Result_X TEXT,
                    Result_Y TEXT,
                    Result_A TEXT,
                    CamN TEXT,
                    JobN TEXT,
                    CreatedAt TEXT DEFAULT (datetime('now', 'localtime'))
                );";

				var cmd = new SQLiteCommand(createSql, conn);
				cmd.ExecuteNonQuery();
			}
		}
	}
}

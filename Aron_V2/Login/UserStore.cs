using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace Aron_V2.Security
{
	public class UserRecord
	{
		public string Username { get; set; }          // 登录名
		public string Role { get; set; } = "Operator"; // Admin / Operator
		public string Salt { get; set; }
		public string PasswordHash { get; set; }
	}

	[XmlRoot("UserStore")]
	public class UserStore
	{
		public List<UserRecord> Users { get; set; } = new List<UserRecord>();

		public static string FilePath { get; set; } =
			Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users.xml");

		public static UserStore Load()
		{
			try
			{
				if (!File.Exists(FilePath)) return new UserStore();
				using (var fs = File.OpenRead(FilePath))
				{
					var ser = new XmlSerializer(typeof(UserStore));
					return (UserStore)ser.Deserialize(fs);
				}
			}
			catch { return new UserStore(); }
		}

		public static void Save(UserStore store)
		{
			using (var fs = File.Create(FilePath))
			{
				var ser = new XmlSerializer(typeof(UserStore));
				ser.Serialize(fs, store);
			}
		}

		public static bool VerifyLogin(UserStore store, string username, string password, out UserRecord user)
		{
			user = store.Users.FirstOrDefault(u =>
				string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
			if (user == null) return false;
			var ok = Hash(password, user.Salt) == user.PasswordHash;
			if (!ok) user = null;
			return ok;
		}

		public static bool AddUser(UserStore store, string username, string password, string role, out string error)
		{
			error = null;
			if (string.IsNullOrWhiteSpace(username)) { error = "用户名不能为空"; return false; }
			if (string.IsNullOrEmpty(password)) { error = "密码不能为空"; return false; }
			if (store.Users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
			{ error = "用户已存在"; return false; }

			var salt = GenerateSalt();
			store.Users.Add(new UserRecord
			{
				Username = username.Trim(),
				Role = string.IsNullOrWhiteSpace(role) ? "Operator" : role,
				Salt = salt,
				PasswordHash = Hash(password, salt)
			});
			return true;
		}

		public static bool ChangePassword(UserStore store, string username, string oldPwd, string newPwd, out string error)
		{
			error = null;
			var u = store.Users.FirstOrDefault(x =>
				string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
			if (u == null) { error = "用户不存在"; return false; }
			if (Hash(oldPwd, u.Salt) != u.PasswordHash) { error = "旧密码不正确"; return false; }

			u.Salt = GenerateSalt();
			u.PasswordHash = Hash(newPwd, u.Salt);
			return true;
		}

		// ========= 新增：删除用户（禁止删掉系统里最后一个管理员） =========
		public static bool DeleteUser(UserStore store, string username, out string error)
		{
			error = null;
			var u = store.Users.FirstOrDefault(x =>
				string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
			if (u == null) { error = "用户不存在"; return false; }

			if (IsAdmin(u))
			{
				if (CountAdmins(store) <= 1)
				{
					error = "无法删除最后一个管理员账号";
					return false;
				}
			}

			store.Users.Remove(u);
			return true;
		}

		public static bool IsAdmin(UserRecord u)
			=> string.Equals(u?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

		public static int CountAdmins(UserStore store)
			=> store.Users.Count(u => IsAdmin(u));

		private static string GenerateSalt() => Guid.NewGuid().ToString("N");

		private static string Hash(string input, string salt)
		{
			using (var sha = SHA256.Create())
			{
				var bytes = Encoding.UTF8.GetBytes(input + "|" + salt);
				var h = sha.ComputeHash(bytes);
				var sb = new StringBuilder(h.Length * 2);
				foreach (var b in h) sb.Append(b.ToString("x2"));
				return sb.ToString();
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aron_V2.Security; // UserRecord

namespace Aron_V2
{
	public sealed class PermissionSet
	{
		public HashSet<string> Allow { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		public HashSet<string> Deny { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public bool IsAllowed(string key)
		{
			if (Deny.Contains("*")) return false;         // 全禁
			if (Deny.Contains(key)) return false;
			if (Allow.Contains("*")) return true;         // 全开
			return Allow.Contains(key);
		}
	}

	public static class PermissionManager
	{
		public static string XmlPath { get; set; } = Global.PermissionsPath;

		public static PermissionSet Resolve(UserRecord user)
		{
			var ps = new PermissionSet();

			if (!File.Exists(XmlPath))
			{
				// 没有配置文件：默认对 Admin 全开，其它全禁
				if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
					ps.Allow.Add("*");
				else
					ps.Deny.Add("*");
				return ps;
			}

			var doc = XDocument.Load(XmlPath);

			// 角色默认
			var role = doc.Root.Element("Roles")
				?.Elements("Role")
				?.FirstOrDefault(x => (string)x.Attribute("name") == user.Role);
			if (role != null)
			{
				foreach (var a in role.Elements("Allow"))
					ps.Allow.Add((string)a.Attribute("key"));
				foreach (var d in role.Elements("Deny"))
					ps.Deny.Add((string)d.Attribute("key"));
			}

			// 用户覆盖
			var userNode = doc.Root.Element("Users")
				?.Elements("User")
				?.FirstOrDefault(x => (string)x.Attribute("name") == user.Username);
			if (userNode != null)
			{
				foreach (var a in userNode.Elements("Allow"))
					ps.Allow.Add((string)a.Attribute("key"));
				foreach (var d in userNode.Elements("Deny"))
					ps.Deny.Add((string)d.Attribute("key"));
			}

			return ps;
		}
	}
}
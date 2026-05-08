using Aron_V2.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Aron_V2.XmlConfigHelper;
using WindowsFormsApp2;

namespace Aron_V2
{
	public sealed class InitResult
	{
		// 用只读字段 + 构造函数，保持“只读”语义（推荐）
		public readonly AppConfig Config;
		public readonly VppOutputConfig VppOutCfg;
		public readonly Dictionary<string, object> CamLocks;
		public readonly string[] ModelJobIds;
		public readonly DataBase Db;
		public readonly bool PlcConnected;
		public readonly UserStore UserStore;
		public readonly UserRecord DefaultUser;

		public InitResult(
			AppConfig config,
			VppOutputConfig vppOutCfg,
			Dictionary<string, object> camLocks,
			string[] modelJobIds,
			DataBase db,
			bool plcConnected,
			UserStore userStore,
			UserRecord defaultUser)
		{
			this.Config = config;
			this.VppOutCfg = vppOutCfg;
			this.CamLocks = camLocks;
			this.ModelJobIds = modelJobIds;
			this.Db = db;
			this.PlcConnected = plcConnected;
			this.UserStore = userStore;
			this.DefaultUser = defaultUser;
		}
	}
}

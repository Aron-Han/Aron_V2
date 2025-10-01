using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	internal static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
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
		}
	}
}

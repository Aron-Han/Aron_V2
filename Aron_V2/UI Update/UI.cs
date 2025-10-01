using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	public static class UI_Control
	{
		private static SynchronizationContext _ctx;   // 优先使用
		private static Control _ctrl;                  // 兜底

		// 方式一：主 UI 线程上调用，无需参数（推荐）
		public static void Init()
		{
			_ctx = SynchronizationContext.Current;
		}

		// 方式二：传入任意已创建的控件作为调度器（例如 this 或 Application.OpenForms[0]）
		public static void Init(Control anyUiControl)
		{
			_ctrl = anyUiControl;
			if (_ctx == null) _ctx = SynchronizationContext.Current; // 尝试同步上下文
		}

		// 将动作安全派发到 UI 线程
		public static void Post(Action action)
		{
			if (action == null) return;

			// 首选 SynchronizationContext（不依赖句柄）
			if (_ctx != null)
			{
				_ctx.Post(_ => action(), null);
				return;
			}

			// 兜底：通过控件调度
			if (_ctrl != null && !_ctrl.IsDisposed)
			{
				try
				{
					if (_ctrl.InvokeRequired) _ctrl.BeginInvoke(action);
					else action();
					return;
				}
				catch { /* 控件可能已释放，忽略 */ }
			}

			// 最后兜底（不理想）：直接执行
			action();
		}
	}
}

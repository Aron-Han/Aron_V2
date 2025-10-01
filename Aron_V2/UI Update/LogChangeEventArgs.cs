using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Aron_V2.UI_Update
{
	public static class LogChangeEventArgs
	{
		// 事件，通知哪个key发生了变化，以及新值
		public static event Action<string, Tuple<object, object>> StateChanged;
		private static Dictionary<string, Tuple<object,object>> _state = new Dictionary<string, Tuple<object,object>>();

		public static (T1,T2) Get<T1,T2>(string key)
		{
			if (_state.TryGetValue(key, out var val))
				return ((T1)val.Item1,(T2)val.Item2);
			return default;
		}

		public static void Set(string key, string text, Color color)
		{
			_state[key] = Tuple.Create((object)text, (object)color);
			StateChanged?.Invoke(key, _state[key]);
		}
	}
}

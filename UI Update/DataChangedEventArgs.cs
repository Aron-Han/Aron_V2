using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public static class DataChangedEventArgs
	{
		// 事件，通知哪个key发生了变化，以及新值
		public static event Action<string, object> StateChanged;
		private static Dictionary<string, object> _state = new Dictionary<string, object>();

		public static T Get<T>(string key)
		{
			if (_state.TryGetValue(key, out var val))
				return (T)val;
			return default;
		}

		public static void Set<T>(string key, T value)
		{
			_state[key] = value;
			StateChanged?.Invoke(key, value);
		}
	}
}

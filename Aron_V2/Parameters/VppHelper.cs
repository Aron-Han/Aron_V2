using Aron_V2.UI_Update;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Aron_V2
{
	public static class VppHelper
	{
		public static void ApplyParametersToToolBlock(
			CogToolBlock tool, string jobName, string camName, string posName,
			Dictionary<string, string> parameters,
			Action<string, Color> log = null)
		{
			if (tool == null || parameters == null) return;

			foreach (var kv in parameters)
			{
				string name = kv.Key;
				string text = kv.Value;

				if (!tool.Inputs.Contains(name))
				{
					SafeLog(log, $"[{jobName}/{camName}/{posName}] Inport not existed: {name}", Color.Orange);
					continue;
				}

				var term = tool.Inputs[name]; // CogToolBlockTerminal
				object value;
				if (!TryCoerce(text, term.Value != null ? term.Value.GetType() : typeof(object), out value))
				{
					if (!TryCoerceCommon(text, out value))
					{
						SafeLog(log, $"[{jobName}/{camName}/{posName}] Type not match: {name} = {text}", Color.Orange);
						continue;
					}
				}

				try { term.Value = value; }
				catch (Exception ex)
				{
					SafeLog(log, $"[{jobName}/{camName}/{posName}] Write in fail: {name} = {text}，{ex.Message}", Color.OrangeRed);
				}
			}
		}

		private static bool TryCoerce(string text, Type targetType, out object value)
		{
			value = null;
			if (targetType == null || targetType == typeof(object))
			{ value = text; return true; }

			try
			{
				if (targetType == typeof(string)) { value = text; return true; }
				if (targetType == typeof(int)) { int n; if (int.TryParse(text, out n)) { value = n; return true; } }
				if (targetType == typeof(float)) { float f; if (float.TryParse(text, out f)) { value = f; return true; } }
				if (targetType == typeof(double)) { double d; if (double.TryParse(text, out d)) { value = d; return true; } }
				if (targetType == typeof(bool)) { bool b; if (bool.TryParse(text, out b)) { value = b; return true; } }

				var conv = TypeDescriptor.GetConverter(targetType);
				if (conv != null && conv.CanConvertFrom(typeof(string)))
				{ value = conv.ConvertFromInvariantString(text); return true; }
			}
			catch {}

			return false;
		}

		private static bool TryCoerceCommon(string text, out object value)
		{
			value = null;
			int n; if (int.TryParse(text, out n)) { value = n; return true; }
			float f; if (float.TryParse(text, out f)) { value = f; return true; }
			double d; if (double.TryParse(text, out d)) { value = d; return true; }
			bool b; if (bool.TryParse(text, out b)) { value = b; return true; }
			value = text; // 兜底
			return true;
		}

		private static void SafeLog(Action<string, Color> log, string msg, Color color)
		{
			try
			{
				if (log != null) log(msg, color);
				else LogChangeEventArgs.Set("Log", msg, color); 
			}
			catch {}
		}
	}
}

using Cognex.VisionPro.ToolBlock;
using System;
using System.Linq;
using System.Text;

namespace Aron_V2.Profinet
{
	public static class ProfinetDataMapper
	{
		public static object Read(byte[] buffer, ProfinetInputPoint point)
		{
			if (buffer == null || point == null || point.Address < 0 || point.Length <= 0 || point.Address + point.Length > buffer.Length)
				throw new ArgumentOutOfRangeException("point", "Configured UserData range is outside the received buffer.");
			byte[] bytes = new byte[point.Length]; Buffer.BlockCopy(buffer, point.Address, bytes, 0, point.Length);
			string type = (point.DataType ?? "Byte").ToLowerInvariant();
			if (type == "string") return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
			if (type == "bool") return bytes[0] != 0;
			if (type == "byte") return bytes[0];
			int required = type == "int16" ? 2 : (type == "int32" || type == "float" ? 4 : (type == "double" ? 8 : bytes.Length));
			bytes = PadBigEndian(bytes, required);
			if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
			if (type == "int16") return BitConverter.ToInt16(bytes, 0);
			if (type == "int32") return BitConverter.ToInt32(bytes, 0);
			if (type == "float") return BitConverter.ToSingle(bytes, 0);
			if (type == "double") return BitConverter.ToDouble(bytes, 0);
			return bytes[0];
		}

		public static bool ToBool(object value)
		{
			if (value is bool) return (bool)value;
			double number; return value != null && double.TryParse(value.ToString(), out number) && Math.Abs(number) > double.Epsilon;
		}

		public static byte ToByte(object value)
		{
			double number; if (value == null || !double.TryParse(value.ToString(), out number)) return 0;
			return (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, number));
		}

		public static void WriteFixedOutput(int channel, string parameter, object value)
		{
			ProfinetOutputPoint point = ProfinetCommConfigHelper.GetOutputPoint(channel, parameter);
			lock (Global.PlcBufferLock) Write(Global.Result_Send, point.Address, point.Length, point.DataType, value);
		}
        internal static byte[] PrepareJobPosEcho(int channel,byte job,byte pos)
        {
            var points=ProfinetCommConfigHelper.Load().OutputPoints.Where(p=>p.Channel==channel).ToArray();
            var jobPoint=points.Single(p=>p.IsFixed && p.Parameter.Equals("JobID",StringComparison.OrdinalIgnoreCase));
            var posPoint=points.Single(p=>p.IsFixed && p.Parameter.Equals("PosID",StringComparison.OrdinalIgnoreCase));
            foreach(var p in points)
                if(p.Address<0||p.Length<=0||(long)p.Address+p.Length>Global.Result_Send.Length)
                    throw new InvalidOperationException("Profinet echo mapping outside Result buffer: "+p.Parameter);
            lock(Global.PlcBufferLock)
            {
                foreach(var p in points.Where(p=>!p.IsFixed)) Array.Clear(Global.Result_Send,p.Address,p.Length);
                Write(Global.Result_Send,jobPoint.Address,jobPoint.Length,jobPoint.DataType,job);
                Write(Global.Result_Send,posPoint.Address,posPoint.Length,posPoint.DataType,pos);
                return (byte[])Global.Result_Send.Clone();
            }
        }

		public static void ApplyVppOutputs(int channel, CogToolBlock tool)
		{
			if (tool == null) return;
			var points = ProfinetCommConfigHelper.Load().OutputPoints.Where(x => x.Channel == channel && !x.IsFixed);
			lock (Global.PlcBufferLock)
				foreach (ProfinetOutputPoint point in points)
					if (!string.IsNullOrWhiteSpace(point.VppOutput) && tool.Outputs.Contains(point.VppOutput))
						Write(Global.Result_Send, point.Address, point.Length, point.DataType, tool.Outputs[point.VppOutput].Value);
		}

		public static void ClearOutputs(int channel, bool includeFixed)
		{
			var points = ProfinetCommConfigHelper.Load().OutputPoints.Where(x => x.Channel == channel && (includeFixed || !x.IsFixed));
			lock (Global.PlcBufferLock)
				foreach (ProfinetOutputPoint point in points)
					for (int i = 0; i < point.Length && point.Address + i < Global.Result_Send.Length; i++) Global.Result_Send[point.Address + i] = 0;
		}

		public static void Write(byte[] buffer, int address, int length, string dataType, object value)
		{
			if (buffer == null || address < 0 || length <= 0 || address + length > buffer.Length) return;
			for (int i = 0; i < length; i++) buffer[address + i] = 0;
			string type = (dataType ?? "Byte").ToLowerInvariant(); byte[] bytes;
			try
			{
				if (type == "string") bytes = Encoding.ASCII.GetBytes(value == null ? string.Empty : value.ToString());
				else if (type == "bool") bytes = new[] { ToBool(value) ? (byte)1 : (byte)0 };
				else if (type == "byte") bytes = new[] { ToByte(value) };
				else if (type == "int16") bytes = BitConverter.GetBytes(Convert.ToInt16(value));
				else if (type == "int32") bytes = BitConverter.GetBytes(Convert.ToInt32(value));
				else if (type == "float") bytes = BitConverter.GetBytes(Convert.ToSingle(value));
				else if (type == "double") bytes = BitConverter.GetBytes(Convert.ToDouble(value));
				else bytes = Encoding.ASCII.GetBytes(value == null ? string.Empty : value.ToString());
				if (bytes.Length > 1 && type != "string" && BitConverter.IsLittleEndian) Array.Reverse(bytes);
				Buffer.BlockCopy(bytes, 0, buffer, address, Math.Min(length, bytes.Length));
			}
			catch { }
		}

		private static byte[] PadBigEndian(byte[] bytes, int length)
		{
			if (bytes.Length == length) return bytes;
			byte[] result = new byte[length]; Buffer.BlockCopy(bytes, 0, result, Math.Max(0, length - bytes.Length), Math.Min(bytes.Length, length)); return result;
		}
	}
}

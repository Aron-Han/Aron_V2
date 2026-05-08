using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Aron_V2
{
    // A per-inspection value snapshot: never pass a live ToolBlock into the PLC writer.
    public sealed class S7InspectionOutputs
    {
        public string Job { get; set; }
        public string Camera { get; set; }
        public string Position { get; set; }
        public bool Succeeded { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    internal static class S7OutputData
    {
        internal static bool IsNdmExtension(string name) => name == "Buffer Results Enable" || name == "Inspection Complete" || name == "Results Buffer Overrun";
        internal static string NdmAddress(string reference, int bit)
        {
            var match = System.Text.RegularExpressions.Regex.Match(reference ?? "", @"^(DB\d+\.\d+)(?:\.\d+)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value + "." + bit : "";
        }
        internal static bool IsFixed(string name)
        {
            return new[] { "System Ready", "System Busy", "Online", "Trigger Ready Camera", "Trigger Ack Camera", "Exposure Complete", "Inspection Complete", "Results Buffer Overrun", "Result Valid", "SetUserData Ack", "JobID", "PosID" }
                .Contains((name ?? "").Trim(), StringComparer.OrdinalIgnoreCase);
        }
        internal static bool IsLegacy(string name, int channel)
        {
            return new[] { "Result X", "Result Y", "Result A", "Result", "Result" + channel }.Contains(name, StringComparer.OrdinalIgnoreCase);
        }
        private static string CanonicalFormat(string format) => new[] { "Bool", "Byte", "Int16", "Int32", "Float", "Double", "String" }.FirstOrDefault(f => f.Equals(format, StringComparison.OrdinalIgnoreCase)) ?? format;
        internal static void ValidateDeclaredType(Type type, string format)
        {
            format = CanonicalFormat(format);
            if (type == typeof(string) || type == typeof(object)) return; // validate their actual value at send time
            bool numeric = type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
            if (format == "String" || (format == "Bool" ? type != typeof(bool) && !numeric : !numeric))
                throw new InvalidOperationException("VPP type " + type.Name + " is incompatible with " + format + ".");
        }
        internal static string Serialize(object value, string format, int length)
        {
            format = CanonicalFormat(format);
            if (value == null) throw new InvalidOperationException("VPP output is null.");
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            switch (format)
            {
                case "Bool":
                    if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1") return "true";
                    if (text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0") return "false";
                    break;
                case "Byte": return byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case "Int16": return short.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case "Int32": return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                case "Float":
                    float f = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (float.IsNaN(f) || float.IsInfinity(f)) break;
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case "Double":
                    double d = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (double.IsNaN(d) || double.IsInfinity(d)) break;
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case "String":
                    if (!(value is string) || length < 1 || text.Length > length || text.Any(c => c > 127 || c == '\0')) break;
                    return text.PadRight(length, '\0'); // deterministic fixed-width ASCII; clear the previous tail
            }
            throw new InvalidOperationException("Value cannot be encoded as " + format + " (length " + length + ").");
        }
    }
}

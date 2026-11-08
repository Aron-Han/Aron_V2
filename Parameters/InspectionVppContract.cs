using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aron_V2
{
    internal static class InspectionVppContract
    {
        internal static void EnsureContextInputs(CogToolBlock tool)
        {
            foreach (string name in new[] { "JobID", "PosID", "SN" })
                if (!tool.Inputs.Contains(name)) tool.Inputs.Add(new CogToolBlockTerminal(name, typeof(string)));
            // Upgrade only the detached editor draft of legacy VPPs. Never rewrite disk on load.
            if(!tool.Outputs.Contains("ImageFileName")) tool.Outputs.Add(new CogToolBlockTerminal("ImageFileName",typeof(string)) { Value=string.Empty });
        }
        internal static void ApplyContext(CogToolBlock tool, string job, string position, string serial)
        {
            string[] names = { "JobID", "PosID", "SN" };
            string[] values = { job, position, serial };
            for (int i = 0; i < names.Length; i++)
            {
                // Optional named metadata never changes the positional image/data
                // contract or interferes with a legacy custom numeric input.
                if (tool == null || !tool.Inputs.Contains(names[i])) continue;
                var terminal = tool.Inputs[names[i]];
                if (terminal.ValueType != typeof(string) && terminal.ValueType != typeof(object)) continue;
                terminal.Value = string.IsNullOrWhiteSpace(values[i]) ? null : values[i];
            }
        }
        internal static List<string> Validate(CogToolBlock tool, string[] originalInputs, IEnumerable<string> requiredOutputs)
        {
            var errors = new List<string>();
            if (tool == null) { errors.Add("No algorithm loaded."); return errors; }
            if(InspectionImageStorage.DealTool(tool)==null) errors.Add("Missing tool: CogIPOneImageTool");
            foreach (string name in new[] { "JobID", "PosID", "SN" })
            {
                if (!tool.Inputs.Contains(name)) errors.Add("Missing input: " + name);
                else if (tool.Inputs[name].ValueType != typeof(string) && tool.Inputs[name].ValueType != typeof(object))
                    errors.Add("Input type mismatch: " + name + " (expected String)");
            }
            for (int index = 0; index < 2; index++)
            {
                if (tool.Inputs.Count <= index) { errors.Add("Missing input slot: [" + index + "]" + (index == 0 ? " Image" : " Data")); continue; }
                var input = tool.Inputs[index];
                if (originalInputs != null && originalInputs.Length > index && input.Name != originalInputs[index])
                    errors.Add("Input renamed or reordered: [" + index + "] " + originalInputs[index] + " -> " + input.Name);
                Type type = input.ValueType;
                bool valid = type == typeof(object) || (index == 0 ? typeof(ICogImage).IsAssignableFrom(type) : type == typeof(string));
                if (!valid) errors.Add("Input type mismatch: [" + index + "] " + input.Name + " (" + type.Name + ")");
            }
            var names = new HashSet<string>(tool.Outputs.Cast<CogToolBlockTerminal>().Select(t => t.Name), StringComparer.Ordinal);
            foreach (string name in new[] { "Result_Cam", "resultSend", "ImageFileName" }.Concat(requiredOutputs ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
            {
                if (!names.Contains(name)) { errors.Add("Missing output: " + name); continue; }
                Type type = tool.Outputs[name].ValueType;
                if (name == "resultSend" && type != typeof(string) && type != typeof(object)) errors.Add("Output type mismatch: " + name + " (expected String)");
                if (name == "ImageFileName" && type != typeof(string)) errors.Add("Output type mismatch: " + name + " (expected String)");
                if (name == "Result_Cam" && type != typeof(string) && type != typeof(object) && !IntegerType(type)) errors.Add("Output type mismatch: " + name + " (expected String or integer)");
            }
            // Inspect declared interfaces only. Never Run, acquire an image,
            // require non-null outputs, or manufacture unconnected terminals.
            return errors;
        }
        private static bool IntegerType(Type t) => t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong);
        internal static void SaveChecked(CogToolBlock tool, string path, string[] originalInputs, IEnumerable<string> requiredOutputs)
        {
            var errors = Validate(tool, originalInputs, requiredOutputs);
            if (errors.Count > 0) throw new InvalidOperationException(FailureMessage(errors));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // A live 3D image may be attached to Input1 while the editor is
                // used for tuning. Persist the algorithm only: serializing the
                // image would produce huge VPP files and make the next load use
                // stale production data.
                using (var persisted = CreateCopyWithoutTransientInputs(tool))
                    CogSerializer.SaveObjectToFile(persisted, temporary);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static CogToolBlock CreateCopyWithoutTransientInputs(CogToolBlock source)
        {
            var copy = (CogToolBlock)CogSerializer.DeepCopyObject(source);
            try
            {
                // Input 0 is the acquired image and input 1 is live PLC/manual
                // data. Neither belongs to the saved algorithm definition.
                if (copy.Inputs.Count > 0) copy.Inputs[0].Value = null;
                if (copy.Inputs.Count > 1) copy.Inputs[1].Value = null;
                ApplyContext(copy, null, null, null);
                return copy;
            }
            catch { copy.Dispose(); throw; }
        }
        internal static string FailureMessage(IEnumerable<string> errors)
        {
            bool chinese = UiLanguage.Current == "zh-CN";
            if(chinese) errors=errors.Select(e=>e.Replace("Missing tool:","缺少工具："));
            string detail = string.Join("\r\n", errors.Select(e => "• " + (chinese ? e.Replace("Missing output:", "缺少输出引脚：").Replace("Missing input:", "缺少输入引脚：").Replace("Missing input slot:", "缺少输入引脚位置：").Replace("Input renamed or reordered:", "输入引脚被改名或顺序改变：").Replace("Input type mismatch:", "输入引脚类型错误：").Replace("Output type mismatch:", "输出引脚类型错误：") : e)));
            return (chinese ? "保存失败：算法接口校验未通过。\r\n" : "Save blocked: algorithm interface validation failed.\r\n") + detail +
                (chinese ? "\r\n\r\n请恢复引脚及其连接后重试。原 VPP 文件和运行缓存未替换。" : "\r\n\r\nRestore the terminals and their connections, then retry. The existing VPP file and runtime cache were not replaced.");
        }
    }
}

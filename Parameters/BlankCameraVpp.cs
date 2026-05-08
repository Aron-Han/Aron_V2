using Aron_V2.CameraIntegration;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aron_V2
{
    // Provision files only. Never run tools, touch runtime caches, connect a
    // device, or delete a camera's retained files when configuration is removed.
    internal static class BlankCameraVpp
    {
        internal static IReadOnlyList<string> EnsureForCamera(string root, string job, CameraConfig camera,
            Action<string, bool> writeBlank = null)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("VPP root is empty.", nameof(root));
            ValidateName(job); ValidateName(camera.Name);
            var positions = (camera.Positions ?? new List<PositionConfig>()).Where(p => p != null).Select(p => p.Name).ToList();
            // The existing Add Camera UI shows a Pos1 placeholder until Save.
            // Create its files without changing position/channel configuration.
            if (positions.Count == 0) positions.Add("Pos1");
            foreach (string position in positions) ValidateName(position);
            var created = new List<string>();
            foreach (string position in positions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string directory = Path.Combine(Path.GetFullPath(root), job, camera.Name, position);
                if (!CameraAcquisition.IsSdk(camera))
                    EnsureFile(Path.Combine(directory, "Camera.vpp"), true, writeBlank ?? WriteBlank, created);
                EnsureFile(Path.Combine(directory, "Inspection.vpp"), false, writeBlank ?? WriteBlank, created);
            }
            return created;
        }

        private static void ValidateName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value != value.TrimEnd(' ', '.'))
                throw new ArgumentException("Invalid Job / Camera / Position name: " + value);
        }

        private static void EnsureFile(string path, bool camera, Action<string, bool> writeBlank, List<string> created)
        {
            // Existing files, including old files retained after deletion or
            // even damaged/empty files, belong to the user and are never replaced.
            if (File.Exists(path)) return;
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, ".blank-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                writeBlank(temporary, camera);
                if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                    throw new IOException("Blank VPP serialization produced no data: " + path);
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { return; } // another writer won; preserve it
                created.Add(path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); } // only our unique staging file
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteBlank(string path, bool camera)
        {
            Load_Job.EnsureCognexInteropLoaded();
            if (camera)
            {
                using (var tool = new CogAcqFifoTool())
                {
                    // No frame grabber/operator is bound, hence no physical
                    // sensor, acquisition, trigger or live mode is initialized.
                    if (tool.Operator != null) throw new InvalidOperationException("A blank camera tool must not have an acquisition operator.");
                    SaveNew(tool, path);
                }
            }
            else
            {
                using (var tool = new CogToolBlock())
                {
                    InitializeInspectionInterface(tool);
                    SaveNew(tool, path);
                }
            }
        }

        // Only a completely empty block may be initialized. Existing algorithms,
        // terminal names, connections and configured values are never replaced.
        internal static bool InitializeInspectionInterface(CogToolBlock tool)
        {
            if (tool == null || tool.Tools.Count != 0 || tool.Inputs.Count != 0 || tool.Outputs.Count != 0) return false;
            tool.Inputs.Add(new CogToolBlockTerminal("Input1", typeof(ICogImage)));
            tool.Inputs.Add(new CogToolBlockTerminal("Send_Data", typeof(string)));
            foreach (string name in new[] { "JobID", "PosID", "SN" })
            {
                tool.Inputs.Add(new CogToolBlockTerminal(name, typeof(string)));
            }
            var result = new CogToolBlockTerminal("Result_Cam", typeof(string)); result.Value = "2"; tool.Outputs.Add(result);
            var send = new CogToolBlockTerminal("resultSend", typeof(string)); send.Value = string.Empty; tool.Outputs.Add(send);
            return true;
        }

        private static void SaveNew(object tool, string path)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                CogSerializer.SaveObjectToStream(tool, stream);
                stream.Flush(true);
            }
        }
    }
}

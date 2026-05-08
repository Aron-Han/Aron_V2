using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Xml.Linq;

namespace Aron_V2
{
    // Configuration/file operations only. Never acquire, run VPPs, or write PLC data.
    internal sealed class JobCatalog
    {
        private readonly string _path, _root;
        private readonly string _outputs;
        private readonly Action<string, bool> _blank;
        internal JobCatalog(string path, string root, Action<string, bool> blank = null, string outputs = null)
        { _path = path; _root = Path.GetFullPath(root); _blank = blank; _outputs = outputs ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "VPPOutput.xml"); }
        internal AppConfig Read() => XmlConfigHelper.Load(_path);
        internal static T Copy<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new XmlSerializer(typeof(T));
                serializer.Serialize(stream, value); stream.Position = 0;
                return (T)serializer.Deserialize(stream);
            }
        }
        internal static string NextName(IEnumerable<string> names, string prefix)
        {
            var used = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i <= 255; i++) if (!used.Contains(prefix + i)) return prefix + i;
            throw new InvalidOperationException("No available numeric ID (1–255).");
        }
        private static void CheckName(string name, string prefix)
        {
            int id;
            if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(name.Substring(prefix.Length), out id) || id < 1 || id > 255 || name != prefix + id)
                throw new InvalidOperationException("Use " + prefix + "1–" + prefix + "255. Numeric IDs must match PLC program/camera IDs.");
        }
        internal void CreateJob(string name, string source)
        {
            CheckName(name, "Job");
            var config = Read();
            if (config.Models == null) config.Models = new List<ModelConfig>();
            if (config.Models.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Job already exists.");
            var model = string.IsNullOrEmpty(source) ? new ModelConfig {
                General = new GeneralConfig { CamN = "0", maxLines_Richbox = "500" }, Cameras = new List<CameraConfig>()
            } : Copy(Find(config, source));
            model.Name = name;
            // A clone is all-or-nothing: never silently combine retained and source files.
            string destination = Path.Combine(_root, name);
            string stage = null;
            bool moved = false;
            try
            {
                if (!string.IsNullOrEmpty(source))
                {
                    if (Directory.Exists(destination)) throw new InvalidOperationException("Retained VPP directory exists. Choose another Job ID; existing files will not be overwritten.");
                    stage = Path.Combine(_root, ".job-clone-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(stage);
                    foreach (var camera in model.Cameras ?? new List<CameraConfig>())
                    foreach (var position in camera.Positions ?? new List<PositionConfig>())
                    {
                        foreach (string filename in new[] { "Camera.vpp", "Inspection.vpp" })
                        {
                            if (filename == "Camera.vpp" && CameraIntegration.CameraAcquisition.IsSdk(camera)) continue;
                            string input = SafeFile(source, camera.Name, position.Name, filename);
                            if (!File.Exists(input)) throw new FileNotFoundException("Clone source VPP is missing.", input);
                            string output = Path.Combine(stage, camera.Name, position.Name, filename);
                            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(input, output, false);
                        }
                    }
                    EnsureUnderRoot(stage); EnsureUnderRoot(destination);
                    Directory.Move(stage, destination); stage = null; moved = true;
                }
                config.Models.Add(model); Save(config, CreateOutputMapping(name, source));
            }
            catch
            {
                // Remove only directories uniquely created by this operation.
                if (stage != null) RemoveOwnedDirectory(stage);
                if (moved) RemoveOwnedDirectory(destination);
                throw;
            }
        }
        private void RemoveOwnedDirectory(string directory)
        {
            EnsureUnderRoot(directory);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        private void EnsureUnderRoot(string directory)
        {
            string absolute = Path.GetFullPath(directory);
            if (!absolute.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup path is outside VPP root.");
        }
        private XDocument CreateOutputMapping(string name, string source)
        {
            if (!File.Exists(_outputs)) return null;
            var doc = XDocument.Load(_outputs); var jobs = doc.Root?.Element("Jobs");
            if (jobs == null) throw new InvalidOperationException("Invalid VPP output configuration.");
            var template = jobs.Elements("Job").FirstOrDefault(j => (string)j.Attribute("Name") == (source ?? "Job1")) ?? jobs.Elements("Job").FirstOrDefault();
            if (template == null) return null;
            var copy = new XElement(template); copy.SetAttributeValue("Name", name);
            // Only the new recipe's entry changes. Protocol layouts and all other Jobs remain intact.
            jobs.Elements("Job").Where(j => (string)j.Attribute("Name") == name).Remove(); jobs.Add(copy);
            return doc;
        }
        private string SafeFile(string job, string camera, string position, string filename)
        {
            foreach (string name in new[] { job, camera, position })
                if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException("Invalid VPP path component.");
            return Path.Combine(_root, job, camera, position, filename);
        }
        internal void AddCamera(string jobName, CameraConfig camera)
        {
            var config = Read(); var job = Find(config, jobName);
            CheckName(camera.Name, "Cam");
            if (job.Cameras == null) job.Cameras = new List<CameraConfig>();
            if (job.Cameras.Any(c => string.Equals(c.Name, camera.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Camera already exists.");
            if (camera.Positions == null || camera.Positions.Count == 0)
                camera.Positions = new List<PositionConfig> { new PositionConfig { Name = "Pos1", Parameters = new List<ParameterConfig>(),
                    General = new PositionGeneralConfig { Exposure = "10", MainUsed = "0", MainChannel = "0", SecondUsed = "0", SecondChannel = "0" } } };
            BlankCameraVpp.EnsureForCamera(_root, jobName, camera, _blank);
            job.Cameras.Add(Copy(camera)); job.General = job.General ?? new GeneralConfig();
            job.General.CamN = job.Cameras.Count.ToString(); Save(config, CameraOutputMapping(jobName, camera.Name));
        }
        private XDocument CameraOutputMapping(string job, string camera)
        {
            if (!File.Exists(_outputs)) return null;
            var doc = XDocument.Load(_outputs); var jobs = doc.Root?.Element("Jobs"); if (jobs == null) return null;
            var target = jobs.Elements("Job").FirstOrDefault(j => (string)j.Attribute("Name") == job);
            if (target == null) { target = new XElement("Job", new XAttribute("Name", job), new XElement("Cameras")); jobs.Add(target); }
            var cameras = target.Element("Cameras"); if (cameras == null) { cameras = new XElement("Cameras"); target.Add(cameras); }
            if (cameras.Elements("Camera").Any(c => (string)c.Attribute("Name") == camera)) return null;
            var template = jobs.Elements("Job").Where(j => j != target).SelectMany(j => j.Element("Cameras")?.Elements("Camera") ?? Enumerable.Empty<XElement>())
                .FirstOrDefault(c => (string)c.Attribute("Name") == camera);
            if (template == null) return null; // Never invent PLC addresses for a new camera ID.
            cameras.Add(new XElement(template)); return doc;
        }
        internal void DeleteCamera(string jobName, string cameraName)
        {
            var config = Read(); var job = Find(config, jobName);
            var camera = job.Cameras?.FirstOrDefault(c => c.Name == cameraName);
            if (camera == null) throw new InvalidOperationException("Camera not found.");
            job.Cameras.Remove(camera); job.General = job.General ?? new GeneralConfig();
            job.General.CamN = job.Cameras.Count.ToString(); Save(config, RemoveOutputMapping(jobName, cameraName));
        }
        internal void DeleteJob(string name)
        {
            var config = Read(); var model = Find(config, name);
            if (config.Models.Count <= 1) throw new InvalidOperationException("Keep at least one Job.");
            config.Models.Remove(model); Save(config, RemoveOutputMapping(name, null));
        }
        private XDocument RemoveOutputMapping(string job, string camera)
        {
            if (!File.Exists(_outputs)) return null;
            var doc = XDocument.Load(_outputs);
            var matches = doc.Root?.Element("Jobs")?.Elements("Job").Where(j => (string)j.Attribute("Name") == job).ToList();
            if (matches != null) foreach (var entry in matches)
            {
                if (camera == null) entry.Remove();
                else entry.Element("Cameras")?.Elements("Camera").Where(c => (string)c.Attribute("Name") == camera).Remove();
            }
            return doc;
        }
        private static ModelConfig Find(AppConfig config, string name) => config.Models?.FirstOrDefault(m => m.Name == name)
            ?? throw new InvalidOperationException("Job not found.");
        private void Save(AppConfig config, XDocument outputs = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path)));
            string temporary = _path + ".job-" + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] oldOutputs = outputs == null ? null : File.ReadAllBytes(_outputs);
            bool outputChanged = false;
            try
            {
                XmlConfigHelper.Save(config, temporary);
                if (outputs != null) { WriteAtomic(_outputs, System.Text.Encoding.UTF8.GetBytes(outputs.ToString())); outputChanged = true; }
                if (File.Exists(_path)) File.Replace(temporary, _path, null); else File.Move(temporary, _path);
            }
            catch { if (outputChanged) WriteAtomic(_outputs, oldOutputs); throw; }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void WriteAtomic(string path, byte[] bytes)
        {
            string temporary = path + ".job-" + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, bytes); if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

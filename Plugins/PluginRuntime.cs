using Aron.PluginContracts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Aron_V2
{
    internal sealed class PluginDescriptor
    {
        internal string Id, Name, Folder, AssemblyFile, Type, Error;
        internal bool Enabled;
    }
    internal sealed class PluginRuntime : IDisposable
    {
        internal readonly string ConfigPath;
        internal readonly List<PluginDescriptor> Available = new List<PluginDescriptor>();
        private readonly List<Slot> _slots = new List<Slot>();
        private volatile bool _stopping;
        internal HashSet<string> SelectedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string, string> _log;
        internal bool Observing { get { return !_stopping && _slots.Any(s => !s.Failed); } }
        internal bool IsIdle { get { return _slots.All(s => s.IsIdle); } }
        internal IEnumerable<Slot> Loaded { get { return _slots.Where(s => !s.Failed); } }

        internal PluginRuntime(string applicationDirectory, Func<string, IPluginHost> host, Action<string, string> log, string visionProVersion = "71.2.0.0")
        {
            _log = log;
            ConfigPath = Path.Combine(applicationDirectory, "Project", "Config", "Plugins", "Enabled.xml");
            var enabled = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(ConfigPath))
                    foreach (var item in ReadXml(ConfigPath).Root.Elements("Plugin"))
                        if ((string)item.Attribute("enabled") == "true") enabled.Add((string)item.Attribute("id") ?? "");
            }
            catch (Exception ex) { log("ERROR", "Plugin enable configuration invalid; no plugins loaded: " + ex.Message); return; }
            SelectedIds = new HashSet<string>(enabled, StringComparer.Ordinal);
            string root = Path.Combine(applicationDirectory, "Plugins");
            if (Directory.Exists(root))
                foreach (string folder in Directory.GetDirectories(root).OrderBy(p => p))
                {
                    if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) continue;
                    string manifest = Path.Combine(folder, "plugin.xml"); if (!File.Exists(manifest)) continue;
                    var descriptor = new PluginDescriptor { Folder = folder, Id = Path.GetFileName(folder), Name = Path.GetFileName(folder) };
                    try
                    {
                        var xml = ReadXml(manifest).Root;
                        descriptor.Id = (string)xml.Attribute("id"); descriptor.Name = (string)xml.Attribute("name") ?? descriptor.Id;
                        descriptor.AssemblyFile = (string)xml.Attribute("assembly"); descriptor.Type = (string)xml.Attribute("type");
                        descriptor.Enabled = enabled.Contains(descriptor.Id ?? "");
                        if (descriptor.Id == null || !Regex.IsMatch(descriptor.Id, "^[A-Za-z][A-Za-z0-9_-]{0,63}$")) throw new InvalidDataException("Invalid plugin ID");
                        if (Available.Any(p => p.Id == descriptor.Id)) throw new InvalidDataException("Duplicate plugin ID");
                        if ((string)xml.Attribute("api") != "1" || (string)xml.Attribute("visionPro") != visionProVersion) throw new InvalidDataException("Requires plugin API 1 and matching host VisionPro " + visionProVersion);
                        if (string.IsNullOrWhiteSpace(descriptor.AssemblyFile) || Path.GetFileName(descriptor.AssemblyFile) != descriptor.AssemblyFile || !descriptor.AssemblyFile.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Assembly must be a DLL filename in this plugin folder");
                    }
                    catch (Exception ex) { descriptor.Error = ex.Message; }
                    Available.Add(descriptor);
                }
            foreach (string id in enabled.Where(id => !Available.Any(p => p.Id == id)))
                Available.Add(new PluginDescriptor { Id = id, Name = id, Enabled = true, Error = "Enabled plugin is not installed" });
            AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;
            foreach (var item in Available.Where(p => p.Enabled))
            {
                IAronPlugin plugin = null;
                try
                {
                    if (item.Error != null) throw new InvalidDataException(item.Error);
                    if (Directory.GetFiles(item.Folder, "Cognex*.dll").Length > 0 || File.Exists(Path.Combine(item.Folder, "Aron.PluginContracts.dll")))
                        throw new InvalidDataException("Do not bundle Cognex or host contract DLLs in plugin folders");
                    string dll = Path.Combine(item.Folder, item.AssemblyFile);
                    if ((File.GetAttributes(dll) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Plugin DLL cannot be a link");
                    Assembly assembly = Assembly.LoadFrom(dll);
                    foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                    {
                        if (reference.Name.StartsWith("Cognex", StringComparison.OrdinalIgnoreCase) && reference.Version != new Version(visionProVersion)) throw new InvalidDataException("Incompatible VisionPro reference");
                        if (reference.Name == "Aron.PluginContracts" && reference.Version != new Version(1, 0, 0, 0)) throw new InvalidDataException("Incompatible plugin contract");
                    }
                    plugin = (IAronPlugin)Activator.CreateInstance(assembly.GetType(item.Type, true));
                    if (plugin.Id != item.Id) throw new InvalidDataException("Plugin ID does not match manifest");
                    plugin.Initialize(host(item.Id));
                    var slot = new Slot(item, plugin, log); _slots.Add(slot);
                    string hash;
                    using (var input = File.OpenRead(dll)) using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
                    log("INFO", "Plugin loaded: " + item.Id + " | " + assembly.GetName().Version + " | " + dll + " | SHA256=" + hash);
                }
                catch (Exception ex)
                {
                    item.Error = ex.GetBaseException().Message;
                    try { plugin?.Dispose(); } catch { }
                    log("ERROR", "Plugin not loaded: " + item.Id + " | " + ex);
                }
            }
        }
        internal static XDocument ReadXml(string path)
        {
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) return XDocument.Load(reader);
        }
        internal void SaveEnabled(IEnumerable<string> ids)
        {
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (string id in selected) if (!Available.Any(p => p.Id == id && p.Error == null)) throw new InvalidDataException("Cannot enable invalid/unavailable plugin: " + id);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            string temp = ConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                new XDocument(new XElement("Plugins", new XAttribute("api", "1"), selected.OrderBy(p => p).Select(id => new XElement("Plugin", new XAttribute("id", id), new XAttribute("enabled", "true"))))).Save(temp);
                if (File.Exists(ConfigPath)) File.Replace(temp, ConfigPath, null); else File.Move(temp, ConfigPath);
                SelectedIds = selected;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private Assembly ResolveDependency(object sender, ResolveEventArgs args)
        {
            try
            {
                var name = new AssemblyName(args.Name);
                if (name.Name.StartsWith("Cognex", StringComparison.OrdinalIgnoreCase) || name.Name == "Aron.PluginContracts" || args.RequestingAssembly == null) return null;
                string owner = Path.GetDirectoryName(args.RequestingAssembly.Location);
                if (!Available.Any(p => p.Enabled && p.Error == null && string.Equals(p.Folder, owner, StringComparison.OrdinalIgnoreCase))) return null;
                string path = Path.Combine(owner, name.Name + ".dll");
                if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
                if (!AssemblyName.ReferenceMatchesDefinition(name, AssemblyName.GetAssemblyName(path)) || AssemblyName.GetAssemblyName(path).Version != name.Version) return null;
                return Assembly.LoadFrom(path);
            }
            catch { return null; }
        }
        internal void Publish(InspectionSnapshot snapshot)
        {
            if (_stopping || snapshot.LocalOnly) return;
            foreach (var slot in _slots) slot.Enqueue(snapshot);
        }
        public void Dispose()
        {
            _stopping = true; AppDomain.CurrentDomain.AssemblyResolve -= ResolveDependency;
            foreach (var slot in _slots) slot.Stop();
        }
        internal sealed class Slot
        {
            internal readonly PluginDescriptor Descriptor;
            internal readonly IAronPlugin Plugin;
            internal volatile bool Failed;
            private readonly BlockingCollection<InspectionSnapshot> _queue = new BlockingCollection<InspectionSnapshot>(256);
            private readonly Thread _worker;
            private readonly Action<string, string> _log;
            private long _bytes;
            private long _outstanding;
            internal bool IsIdle { get { return Interlocked.Read(ref _outstanding) == 0; } }
            internal Slot(PluginDescriptor descriptor, IAronPlugin plugin, Action<string, string> log)
            {
                Descriptor = descriptor; Plugin = plugin; _log = log;
                _worker = new Thread(Work) { IsBackground = true, Name = "Plugin " + descriptor.Id }; _worker.Start();
            }
            private static long Size(InspectionSnapshot snapshot) { return snapshot.Outputs.Sum(p => 2L * ((p.Key ?? "").Length + (p.Value ?? "").Length)) + 1024; }
            internal void Enqueue(InspectionSnapshot snapshot)
            {
                if (Failed) return;
                long size = Size(snapshot);
                Interlocked.Add(ref _bytes, size);
                Interlocked.Increment(ref _outstanding);
                bool added = false;
                try { if (Interlocked.Read(ref _bytes) > 32L * 1024 * 1024 || !(added = _queue.TryAdd(snapshot))) Fail("Observer queue exceeded capacity; plugin paused to prevent incomplete summaries. Restart required."); }
                catch (InvalidOperationException) { }
                finally { if (!added) { Interlocked.Add(ref _bytes, -size); Interlocked.Decrement(ref _outstanding); } }
            }
            internal void Fail(string reason)
            {
                if (Failed) return;
                Failed = true; Descriptor.Error = reason; _log("ERROR", "Plugin " + Descriptor.Id + " stopped: " + reason);
            }
            private void Work()
            {
                try
                {
                    foreach (var item in _queue.GetConsumingEnumerable())
                    {
                        Interlocked.Add(ref _bytes, -Size(item));
                        try { if (!Failed) try { Plugin.OnInspection(item); } catch (Exception ex) { Fail(ex.ToString()); } }
                        finally { Interlocked.Decrement(ref _outstanding); }
                    }
                }
                finally { try { Plugin.Dispose(); } catch (Exception ex) { _log("ERROR", "Plugin disposal: " + ex.Message); } }
            }
            internal void Stop() { _queue.CompleteAdding(); if (!_worker.Join(1500)) _log("WARN", "Plugin worker still draining: " + Descriptor.Id); }
        }
    }
}

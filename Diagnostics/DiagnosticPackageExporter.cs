using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Aron_V2
{
    internal static class DiagnosticPackageExporter
    {
        internal static string ExportPackage()
        {
            DiagnosticLogStore.Initialize();
            if (DiagnosticLogStore.Root == null) throw new IOException("No writable diagnostic directory.");
            string folder = Path.Combine(DiagnosticLogStore.Root, "Packages"); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "AronDiagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");
            var notes = new StringBuilder("Diagnostic package: local files only. No hardware access.\r\n");
            notes.AppendLine("Configuration is captured at export time, not necessarily at the incident time.")
                .AppendLine("Security directory, databases, images, VPP and DLL binaries are excluded.")
                .AppendLine("Known password/secret XML fields are redacted. Logs can contain identifiers/network/product data; review before sharing.")
                .AppendLine("Log selection: recent 3 days, newest first; max 150 files, 100 MiB total; per file max 20 MiB tail.")
                .AppendLine("Queued events are included in snapshot even if the background writer has not yet persisted them.");
            try
            {
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    Text(zip, "snapshot.txt", DiagnosticLogStore.SnapshotText("Manual export"));
                    string appRoot = AppDomain.CurrentDomain.BaseDirectory;
                    string logsRoot = Path.Combine(appRoot, "Logs");
                    var candidates = Files(logsRoot, notes).Concat(Files(DiagnosticLogStore.Root, notes)).Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(p => !p.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        .Where(p => new[] { ".log", ".txt", ".jsonl" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                        .Select(p => new FileInfo(p)).Where(f => f.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-3))
                        .OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                    long budget = 100L * 1024 * 1024; int count = 0;
                    foreach (var source in candidates)
                    {
                        if (count >= 150 || budget <= 0) { notes.AppendLine("Log count/size limit reached; older logs omitted."); break; }
                        long max = Math.Min(budget, 20L * 1024 * 1024);
                        string name = source.FullName.StartsWith(logsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                            ? "Logs/" + source.FullName.Substring(logsRoot.Length + 1).Replace('\\', '/')
                            : "FallbackDiagnostics/" + source.FullName.Substring(DiagnosticLogStore.Root.Length + 1).Replace('\\', '/');
                        try
                        {
                            using (var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            using (var output = zip.CreateEntry(name, CompressionLevel.Fastest).Open())
                            {
                                long length = Math.Min(input.Length, max);
                                if (length < input.Length) notes.AppendLine("Tail only: " + name);
                                input.Seek(Math.Max(0, input.Length - length), SeekOrigin.Begin);
                                var buffer = new byte[32768]; long remaining = length;
                                while (remaining > 0)
                                { int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining)); if (read == 0) break; output.Write(buffer, 0, read); remaining -= read; budget -= read; }
                                count++;
                            }
                        }
                        catch (Exception ex) { notes.AppendLine("Log copy incomplete: " + name + " | " + ex.Message); }
                    }
                    string config = Path.Combine(appRoot, "Project", "Config");
                    foreach (string source in Files(config, notes).Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).Take(200))
                    {
                        string relative = source.Substring(config.Length + 1);
                        if (relative.Split(Path.DirectorySeparatorChar).Any(p => p.Equals("Security", StringComparison.OrdinalIgnoreCase))) continue;
                        try
                        {
                            if (new FileInfo(source).Length > 2 * 1024 * 1024) { notes.AppendLine("Oversize config omitted: " + relative); continue; }
                            // Parse a copy with DTD disabled; never alter the live configuration.
                            XDocument doc;
                            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var reader = System.Xml.XmlReader.Create(input, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null })) doc = XDocument.Load(reader);
                            foreach (var element in doc.Descendants().ToArray())
                            {
                                if (Sensitive(element.Name.LocalName) || element.Attributes().Any(a => (a.Name.LocalName.Equals("name", StringComparison.OrdinalIgnoreCase) || a.Name.LocalName.Equals("key", StringComparison.OrdinalIgnoreCase)) && Sensitive(a.Value)))
                                { element.RemoveNodes(); element.Value = "[REDACTED]"; foreach (var a in element.Attributes().Where(a => a.Name.LocalName.Equals("value", StringComparison.OrdinalIgnoreCase))) a.Value = "[REDACTED]"; }
                                foreach (var attribute in element.Attributes().Where(a => Sensitive(a.Name.LocalName)).ToArray()) attribute.Value = "[REDACTED]";
                            }
                            Text(zip, "Config/" + relative.Replace('\\', '/'), doc.ToString());
                        }
                        catch (Exception ex) { notes.AppendLine("Config omitted (unreadable/invalid): " + relative + " | " + ex.Message); }
                    }
                    var hashes = new StringBuilder("File SHA256 (export-time identity; not incident-time proof)\r\n");
                    foreach (string source in new[] { Path.Combine(appRoot, "Visual Inspection.exe"), Path.Combine(appRoot, "Visual Inspection.pdb"), Path.Combine(appRoot, "Visual Inspection.exe.visionpro-references.txt") }
                        .Concat(Files(Path.Combine(appRoot, "LmiSdk"), notes).Where(p => Path.GetExtension(p).Equals(".dll", StringComparison.OrdinalIgnoreCase)))
                        .Concat(new[] { Path.Combine(appRoot, "Aron.PluginContracts.dll") })
                        .Concat(Files(Path.Combine(appRoot, "Plugins"), notes).Where(p => Path.GetExtension(p).Equals(".dll", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(p).Equals("plugin.xml", StringComparison.OrdinalIgnoreCase))))
                    {
                        try { using (var input = File.OpenRead(source)) using (var sha = SHA256.Create()) hashes.AppendLine(source.Substring(appRoot.Length).TrimStart('\\', '/') + "=" + BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "")); }
                        catch (Exception ex) { hashes.AppendLine(Path.GetFileName(source) + " unavailable: " + ex.Message); }
                    }
                    Text(zip, "file-identities.txt", hashes.ToString());
                    Text(zip, "README.txt", notes.ToString());
                }
                DiagnosticLogStore.Record("INFO", "Diagnostics", "Package exported: " + path); return path;
            }
            catch { try { File.Delete(path); } catch { } throw; }
        }

        private static bool Sensitive(string name)
        { string text = name.ToLowerInvariant(); return new[] { "password", "passwd", "pwd", "secret", "token", "credential", "apikey", "connectionstring" }.Any(text.Contains); }

        private static IEnumerable<string> Files(string root, StringBuilder notes)
        {
            var result = new List<string>(); var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0 && result.Count < 5000)
            {
                string dir = pending.Pop();
                try
                {
                    if (!Directory.Exists(dir) || (File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0 || Path.GetFileName(dir).Equals("Packages", StringComparison.OrdinalIgnoreCase)) continue;
                    result.AddRange(Directory.GetFiles(dir).Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0));
                    foreach (string child in Directory.GetDirectories(dir)) pending.Push(child);
                }
                catch (Exception ex) { notes.AppendLine("Directory omitted: " + dir + " | " + ex.Message); }
            }
            return result;
        }

        private static void Text(ZipArchive zip, string name, string content)
        { using (var output = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Fastest).Open(), Encoding.UTF8)) output.Write(content); }
    }
}

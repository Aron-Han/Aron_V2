using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace Aron_V2
{
    // Dependency diagnostics only: no Cognex types, COM objects, VPPs or devices.
    internal static class VisionProRuntimeAudit
    {
        private static readonly object Gate = new object();
        private static readonly List<string> Errors = new List<string>();
        private static Version _expected;
        private static bool _started;
        internal static string LogPath { get; private set; }
        private const string Token = "ef0f902af9dee505";

        private static bool IsVisionPro(AssemblyName name)
        {
            return name.Name.StartsWith("Cognex.VisionPro", StringComparison.OrdinalIgnoreCase);
        }

        private static void Write(string text)
        {
            DiagnosticLogStore.Record(text.StartsWith("ERROR") ? "ERROR" : "INFO", "VisionPro", text);
            lock (Gate)
            {
                string line = DateTime.Now.ToString("o") + " " + text;
                Trace.WriteLine(line);
                if (LogPath != null) File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }

        private static void Error(string text)
        {
            lock (Gate) Errors.Add(text);
            Write("ERROR " + text);
        }

        private static void ValidateIdentity(AssemblyName identity, string path)
        {
            string token = string.Concat(identity.GetPublicKeyToken().Select(b => b.ToString("x2")));
            if (identity.Version != _expected || token != Token)
                throw new FileLoadException("VisionPro identity mismatch. Expected version " + _expected + ", token " + Token + "; found " + identity.FullName, path);
        }

        private static void InspectLoaded(Assembly assembly)
        {
            AssemblyName identity = assembly.GetName();
            if (!IsVisionPro(identity)) return;
            string path = assembly.IsDynamic ? "<dynamic>" : assembly.Location;
            Write("LOADED " + identity.FullName + " | " + path);
            try { ValidateIdentity(identity, path); }
            catch (Exception ex) { Error(ex.Message + " | " + path); }
        }

        private static IEnumerable<string> NativeBinCandidates()
        {
            string root = Environment.GetEnvironmentVariable("VPRO_ROOT");
            if (!string.IsNullOrWhiteSpace(root)) yield return Path.Combine(root, "bin");
            // Read only the x64 installer registration. Never edit machine PATH,
            // registration, DLL directories or the GoSDK runtime search policy.
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = machine.OpenSubKey(@"SOFTWARE\Cognex\VisionPro"))
            {
                root = key?.GetValue("VPROINSTDIR") as string;
                if (!string.IsNullOrWhiteSpace(root)) yield return Path.Combine(root, "VisionPro", "bin");
            }
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cognex", "VisionPro", "bin");
        }

        internal static string PrepareNativeRuntime(Version expected, IEnumerable<string> candidates)
        {
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string bin = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
                string native = Path.Combine(bin, "cogxstd.dll");
                string core = Path.Combine(Path.GetDirectoryName(bin), "ReferencedAssemblies", "Cognex.VisionPro.Core.dll");
                if (!File.Exists(native) || !File.Exists(core)) continue;
                var nativeVersion = FileVersionInfo.GetVersionInfo(native);
                var identity = AssemblyName.GetAssemblyName(core);
                string token = string.Concat(identity.GetPublicKeyToken().Select(b => b.ToString("x2")));
                // Native patch revisions differ legitimately from managed .0.
                // Compare release components, and require exact managed identity.
                if (identity.Version != expected || token != Token ||
                    nativeVersion.FileMajorPart != expected.Major || nativeVersion.FileMinorPart != expected.Minor || nativeVersion.FileBuildPart != expected.Build)
                {
                    Write("REJECTED native runtime directory " + bin + "; native=" + nativeVersion.FileVersion + "; managed=" + identity.FullName);
                    continue;
                }
                string oldPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
                // Cognex's mixed-mode initializer requires its installed bin on
                // PATH even when the managed assembly resolves correctly in GAC.
                // Scope this to the application process. Keep other SDK entries,
                // and never add LmiSdk here (it uses absolute-path LoadLibraryEx).
                var rest = oldPath.Split(';').Where(p => !string.Equals(p.Trim().Trim('"').TrimEnd('\\'), bin, StringComparison.OrdinalIgnoreCase));
                string newPath = bin + ";" + string.Join(";", rest);
                if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
                // Edit controls use VPRO_ROOT for tool icons/resources even when
                // the native and managed assemblies themselves loaded normally.
                string selectedRoot = Path.GetDirectoryName(bin);
                Environment.SetEnvironmentVariable("VPRO_ROOT", selectedRoot, EnvironmentVariableTarget.Process);
                Write("NATIVE_RUNTIME " + bin + "; cogxstd=" + nativeVersion.FileVersion + "; managed=" + identity.Version + "; PATH/VPRO_ROOT scope=process only; VPRO_ROOT=" + selectedRoot);
                return bin;
            }
            throw new DirectoryNotFoundException("No matching installed VisionPro native runtime for " + expected + ". Repair/install the matching x64 VisionPro runtime; do not copy native DLLs from another release. Checked: " + string.Join("; ", candidates));
        }

        internal static bool Start(out string message)
        {
            try
            {
                if (_started) throw new InvalidOperationException("Runtime audit already started.");
                _started = true;
                Assembly application = typeof(VisionProRuntimeAudit).Assembly;
                _expected = application.GetReferencedAssemblies().Single(a => a.Name == "Cognex.VisionPro.Core").Version;
                string file = "VisionPro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Process.GetCurrentProcess().Id + ".log";
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "VisionPro");
                try { Directory.CreateDirectory(folder); LogPath = Path.Combine(folder, file); File.AppendAllText(LogPath, string.Empty); }
                catch (UnauthorizedAccessException)
                {
                    folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aron", "Logs", "VisionPro");
                    Directory.CreateDirectory(folder); LogPath = Path.Combine(folder, file);
                }
                Write("Expected managed VisionPro version=" + _expected + "; x64=" + Environment.Is64BitProcess + "; app=" + application.Location);
                if (!Environment.Is64BitProcess) throw new BadImageFormatException("This VisionPro application requires an x64 process.");
                // Subscribe before loading the dependency closure. Do not install
                // AssemblyResolve/binding redirects that silently swap SDK releases.
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) InspectLoaded(assembly);
                foreach (string path in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "Cognex.VisionPro*.dll"))
                {
                    AssemblyName identity = AssemblyName.GetAssemblyName(path);
                    Write("LOCAL " + identity.FullName + " | " + path);
                    ValidateIdentity(identity, path);
                }
                string manifest = application.Location + ".visionpro-references.txt";
                if (!File.Exists(manifest)) throw new FileNotFoundException("VisionPro reference manifest missing. Rebuild and deploy it alongside the executable.", manifest);
                var pending = new Queue<AssemblyName>();
                foreach (string line in File.ReadAllLines(manifest))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    AssemblyName identity = new AssemblyName(line.Split('|')[0]);
                    ValidateIdentity(identity, manifest);
                    pending.Enqueue(identity);
                }
                if (pending.Count == 0) throw new InvalidDataException("Empty VisionPro reference manifest.");
                // Before the first mixed-mode Assembly.Load, not after an
                // initialization failure (a failed DLL cannot be safely retried).
                PrepareNativeRuntime(_expected, NativeBinCandidates().ToArray());
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (pending.Count != 0)
                {
                    AssemblyName requested = pending.Dequeue();
                    if (!seen.Add(requested.FullName)) continue;
                    ValidateIdentity(requested, "assembly reference");
                    Assembly loaded = Assembly.Load(requested);
                    ValidateIdentity(loaded.GetName(), loaded.Location);
                    if (!string.Equals(requested.Name, loaded.GetName().Name, StringComparison.OrdinalIgnoreCase))
                        throw new FileLoadException("VisionPro assembly name changed during resolution.", loaded.Location);
                    Write("VERIFIED " + loaded.FullName + " | " + loaded.Location);
                    foreach (AssemblyName dependency in loaded.GetReferencedAssemblies().Where(IsVisionPro)) pending.Enqueue(dependency);
                }
                lock (Gate)
                    if (Errors.Count != 0) throw new FileLoadException(string.Join(Environment.NewLine, Errors.ToArray()));
                Write("PASS Managed DLL dependency closure is coherent. Native COM, license, camera, PLC and VPP compatibility are NOT tested.");
                message = "VisionPro DLL check passed.\r\n" + LogPath;
                return true;
            }
            catch (Exception ex)
            {
                try { Error(ex.ToString()); } catch { Trace.WriteLine(ex); }
                message = "VisionPro DLL 检查失败，未启动设备或通讯。\r\n" + ex.Message + "\r\n日志：" + LogPath;
                return false;
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs e)
        {
            // Observe late loads (including VPP dependencies); never throw from
            // this CLR callback or abruptly terminate a running inspection/PLC.
            try { InspectLoaded(e.LoadedAssembly); }
            catch (Exception ex) { Trace.WriteLine("VisionPro runtime audit: " + ex); }
        }
    }
}

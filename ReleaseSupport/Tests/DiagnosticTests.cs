using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Aron_V2;

internal static class DiagnosticTests
{
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static string Read(ZipArchive zip, string name) { using (var input = new StreamReader(zip.GetEntry(name).Open())) return input.ReadToEnd(); }
    [STAThread] static int Main(string[] args)
    {
        if (args.Contains("--fatal"))
        {
            SetErrorMode(2); DiagnosticLogStore.Initialize(); DiagnosticExceptionHooks.Install();
            DiagnosticLogStore.Stage("CH2 Job1 Cam3 Pos2", "ToolBlock.Run begin");
            throw new InvalidOperationException("Isolated fatal diagnostic test", new IOException("inner sentinel"));
        }
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string diagnostics = Path.Combine(root, "Logs", "Diagnostics"); Directory.CreateDirectory(diagnostics);
            string old = Path.Combine(diagnostics, "events_oldtest.jsonl"); File.WriteAllText(old, "old"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-40));
            string keep = Path.Combine(diagnostics, "unrelated.txt"); File.WriteAllText(keep, "leave unchanged");
            Check(DiagnosticLogStore.Root == null, "Diagnostic source is inactive until explicit startup");
            DiagnosticLogStore.Initialize();
            Check(DiagnosticLogStore.Root == diagnostics, "Default location is Logs/Diagnostics outside Project");
            using (DiagnosticLogStore.BeginOperation("CH1 Job2 Pos3"))
            {
                Task.Run(() => DiagnosticLogStore.Stage("CH1 Cam4", "Acquire begin")).Wait();
                DiagnosticLogStore.Record("WARN", "Test", "换行\nQuote \" and slash \\ sentinel");
            }
            string snapshot = DiagnosticLogStore.SnapshotText("offline test");
            Check(snapshot.Contains("Acquire begin") && snapshot.Contains("CH1 Cam4") && snapshot.Contains("\"operation\":\"" + DiagnosticLogStore.Session), "Thread-independent stages and async operation context survive without a UI loop");
            string crash = DiagnosticLogStore.WriteCrashReport(new InvalidOperationException("outer", new IOException("inner")), "test", false);
            Check(File.ReadAllText(crash).Contains("System.IO.IOException: inner") && File.ReadAllText(crash).Contains("[Recent events]"), "Crash report contains full exception chain and recent events");
            var stopwatch = Stopwatch.StartNew();
            Parallel.For(0, 10000, i => DiagnosticLogStore.Record("INFO", "Stress", "sample " + i));
            Check(stopwatch.ElapsedMilliseconds < 10000, "Bounded producer path completes under burst load");
            string config = Path.Combine(root, "Project", "Config", "General"); Directory.CreateDirectory(config);
            string xml = "<Settings><Password>dont-export-password</Password><FtpPassword>dont-export-ftp</FtpPassword><Host>10.0.0.2</Host><Item key=\"apiKey\" value=\"dont-export-key\"/><Node token=\"dont-export-token\"/></Settings>";
            string file = Path.Combine(config, "settings.xml"); File.WriteAllText(file, xml);
            File.WriteAllText(Path.Combine(config, "malformed.xml"), "<broken");
            string security = Path.Combine(root, "Project", "Config", "Security"); Directory.CreateDirectory(security); File.WriteAllText(Path.Combine(security, "users.xml"), "private-users");
            File.WriteAllText(Path.Combine(root, "Logs", "Application.log"), "fixture application log");
            string package = DiagnosticPackageExporter.ExportPackage();
            using (var input = File.OpenRead(package))
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
            {
                string exported = Read(zip, "Config/General/settings.xml");
                Check(!exported.Contains("dont-export") && exported.Contains("10.0.0.2"), "Known secret XML elements, key/value entries and attributes are redacted");
                Check(!zip.Entries.Any(e => e.FullName.Contains("Security") || e.FullName.EndsWith(".zip") || e.FullName.EndsWith(".db") || e.FullName.EndsWith(".vpp")), "Security, database, VPP and recursive packages are excluded");
                Check(Read(zip, "README.txt").Contains("malformed.xml") && Read(zip, "snapshot.txt").Contains("Dropped queued events"), "Omitted files and dropped-event status are visible in export");
                Check(zip.Entries.Any(e => e.FullName == "Logs/Application.log") && zip.GetEntry("file-identities.txt") != null, "Package contains application evidence and build/file identity summary");
            }
            Check(File.ReadAllText(file) == xml, "Export never modifies live configuration");
            DiagnosticLogStore.Shutdown();
            var lines = Directory.GetFiles(diagnostics, "events_*.jsonl").SelectMany(File.ReadAllLines).ToArray();
            var json = new JavaScriptSerializer(); foreach (string line in lines) json.DeserializeObject(line);
            Check(lines.Length > 0 && lines.Any(l => l.Contains("\\u000a")), "Persisted events are valid JSONL with escaped multiline text");
            Check(!File.Exists(old) && File.ReadAllText(keep) == "leave unchanged", "Retention deletes only owned obsolete diagnostic files");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Cognex")), "Diagnostics and export require no camera SDK or PLC service");
            string childDir = Path.Combine(root, "FatalChild"); Directory.CreateDirectory(childDir);
            string childExe = Path.Combine(childDir, "DiagnosticTests.exe"); File.Copy(typeof(DiagnosticTests).Assembly.Location, childExe, true);
            using (var child = Process.Start(new ProcessStartInfo(childExe, "--fatal") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true }))
            {
                if (!child.WaitForExit(10000)) { child.Kill(); throw new Exception("Fatal child did not exit"); }
                Check(child.ExitCode != 0, "Unhandled exception still terminates isolated child; no swallowing/restart");
            }
            string fatal = Directory.GetFiles(Path.Combine(childDir, "Logs", "Diagnostics", "Crash"), "*.txt").Select(File.ReadAllText).First();
            Check(fatal.Contains("AppDomainUnhandledException") && fatal.Contains("Terminating=True") && fatal.Contains("ToolBlock.Run begin") && fatal.Contains("inner sentinel"), "Actual global unhandled hook writes fatal exception and last stage before child exit");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

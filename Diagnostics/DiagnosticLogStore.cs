using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Aron_V2
{
    // No vendor types or UI dependency: usable before VisionPro preflight and
    // directly on publisher threads even when the main message loop is blocked.
    internal static class DiagnosticLogStore
    {
        private static readonly object Gate = new object(), Disk = new object();
        private static readonly Queue<string> Pending = new Queue<string>(), Recent = new Queue<string>();
        private static readonly Dictionary<string, string> Stages = new Dictionary<string, string>();
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
        private static readonly AsyncLocal<string> Operation = new AsyncLocal<string>();
        private static Thread _writer;
        private static volatile bool _enabled, _stopping;
        private static long _sequence, _dropped;
        private static string _root, _file, _day;
        private static int _part;
        internal static readonly string Session = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Process.GetCurrentProcess().Id;
        internal static string LastFailure { get; private set; }
        internal static string Root { get { return _root; } }

        internal static void Initialize()
        {
            try
            {
            lock (Gate)
            {
                if (_enabled) return;
                foreach (string candidate in new[] {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Diagnostics"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aron", "Logs", "Diagnostics"),
                    Path.Combine(Path.GetTempPath(), "Aron", "Logs", "Diagnostics") })
                {
                    try
                    {
                        Directory.CreateDirectory(candidate);
                        string probe = Path.Combine(candidate, ".probe-" + Session);
                        File.WriteAllText(probe, ""); File.Delete(probe); _root = candidate; break;
                    }
                    catch (Exception ex) { LastFailure = ex.Message; }
                }
                if (_root == null) return;
                _enabled = true;
                _writer = new Thread(WriteLoop) { IsBackground = true, Name = "Aron diagnostics" };
                _writer.Start();
            }
            Record("INFO", "Application", "Diagnostics initialized; " + EnvironmentSummary());
            }
            catch (Exception ex)
            {
                LastFailure = ex.ToString();
                _enabled = false; // Diagnostic setup must never stop application startup.
            }
        }

        internal static string EnvironmentSummary()
        {
            return "Build=" + BuildInfo.BuildDate + "; Session=" + Session + "; x64=" + Environment.Is64BitProcess
                + "; CLR=" + Environment.Version + "; OS=" + Environment.OSVersion + "; Base=" + AppDomain.CurrentDomain.BaseDirectory
                + "; LocalTime=" + DateTimeOffset.Now.ToString("o");
        }

        internal static void Record(string level, string category, string message)
        {
            try
            {
                if (!_enabled) return; // Explicit activation in Program; designers/tests have no side effects.
                string operation = Operation.Value ?? "";
                string line = "{\"utc\":" + Quote(DateTime.UtcNow.ToString("o")) + ",\"seq\":" + Interlocked.Increment(ref _sequence)
                    + ",\"session\":" + Quote(Session) + ",\"build\":" + Quote(BuildInfo.BuildDate)
                    + ",\"thread\":" + Thread.CurrentThread.ManagedThreadId + ",\"operation\":" + Quote(operation)
                    + ",\"level\":" + Quote(level) + ",\"category\":" + Quote(category) + ",\"message\":" + Quote(Limit(message, 32768)) + "}";
                lock (Gate)
                {
                    Recent.Enqueue(line); while (Recent.Count > 200) Recent.Dequeue();
                    if (Pending.Count < 4096) Pending.Enqueue(line); else Interlocked.Increment(ref _dropped);
                }
                Wake.Set();
            }
            catch (Exception ex) { LastFailure = ex.Message; }
        }

        internal static void Exception(string source, Exception ex)
        { try { Record("ERROR", source, ex == null ? "Unknown exception" : ex.ToString()); } catch { } }

        internal static void Stage(string context, string stage)
        {
            try
            {
                if (!_enabled) return;
                lock (Gate)
                {
                    if (Stages.Count >= 256 && !Stages.ContainsKey(context)) Stages.Clear();
                    Stages[context] = DateTime.UtcNow.ToString("o") + " | " + (Operation.Value ?? "") + " | " + stage;
                }
                Record("INFO", "Stage", context + " | " + stage);
            }
            catch (Exception ex) { LastFailure = ex.Message; }
        }

        internal static IDisposable BeginOperation(string context)
        { return new OperationScope(context); }

        private sealed class OperationScope : IDisposable
        {
            readonly string _previous, _context; readonly Stopwatch _watch = Stopwatch.StartNew();
            internal OperationScope(string context)
            {
                _previous = Operation.Value; _context = context;
                Operation.Value = Session + ":" + Guid.NewGuid().ToString("N").Substring(0, 12);
                Stage(context, "Begin; parentOperation=" + (_previous ?? "none"));
            }
            public void Dispose()
            {
                Stage(_context, "Scope ended (not a success assertion); elapsedMs=" + _watch.ElapsedMilliseconds);
                Operation.Value = _previous;
            }
        }

        internal static string SnapshotText(string reason, Exception ex = null)
        {
            string[] recent; KeyValuePair<string, string>[] stages;
            lock (Gate) { recent = Recent.ToArray(); stages = Stages.ToArray(); }
            var text = new StringBuilder("Aron V2 diagnostic snapshot\r\n");
            text.AppendLine(EnvironmentSummary()).AppendLine("Reason=" + reason)
                .AppendLine("Dropped queued events=" + Interlocked.Read(ref _dropped))
                .AppendLine("Last diagnostic write failure=" + LastFailure)
                .AppendLine("Stages are last observations, not proof of a fault; completed scopes may remain listed.")
                .AppendLine("[Exception]").AppendLine(ex == null ? "none" : ex.ToString()).AppendLine("[Last stages]");
            foreach (var stage in stages) text.AppendLine(stage.Key + "=" + stage.Value);
            text.AppendLine("[Recent events]"); foreach (string line in recent) text.AppendLine(line);
            return text.ToString();
        }

        internal static string WriteCrashReport(Exception ex, string source, bool terminating)
        {
            try
            {
                Initialize();
                string contents = SnapshotText(source + "; Terminating=" + terminating, ex);
                foreach (string root in new[] { _root, Path.Combine(Path.GetTempPath(), "Aron", "Logs", "Diagnostics") }.Distinct())
                {
                    if (root == null) continue;
                    try
                    {
                        string dir = Path.Combine(root, "Crash"); Directory.CreateDirectory(dir);
                        string path = Path.Combine(dir, "crash_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".txt");
                        File.WriteAllText(path, contents, Encoding.UTF8);
                        Record("CRITICAL", "Crash", source + "; Report=" + path); return path;
                    }
                    catch (Exception error) { LastFailure = error.Message; }
                }
            }
            catch (Exception error) { LastFailure = error.Message; }
            return null;
        }

        private static void WriteLoop()
        {
            try { Cleanup(); } catch (Exception ex) { LastFailure = ex.Message; }
            while (true)
            {
                string[] batch;
                lock (Gate) { batch = Pending.ToArray(); Pending.Clear(); if (batch.Length == 0 && _stopping) return; }
                if (batch.Length == 0) { Wake.WaitOne(500); continue; }
                try
                {
                    lock (Disk)
                    {
                        string day = DateTime.UtcNow.ToString("yyyyMMdd");
                        if (_day != day) { _day = day; _part = 0; _file = null; Cleanup(); }
                        if (_file == null || (File.Exists(_file) && new FileInfo(_file).Length >= 20L * 1024 * 1024))
                        {
                            _file = Path.Combine(_root, "events_" + day + "_" + Session + "_" + (++_part).ToString("000") + ".jsonl");
                            Cleanup();
                        }
                        File.AppendAllLines(_file, batch, Encoding.UTF8);
                    }
                }
                catch (Exception ex) { LastFailure = ex.ToString(); Interlocked.Add(ref _dropped, batch.Length); }
            }
        }

        // Deletes only this feature's own event/report files, never application logs or Project data.
        private static void Cleanup()
        {
            var files = Directory.GetFiles(_root, "events_*.jsonl").Select(p => new FileInfo(p))
                .Concat(Directory.Exists(Path.Combine(_root, "Crash")) ? Directory.GetFiles(Path.Combine(_root, "Crash"), "crash_*.txt").Select(p => new FileInfo(p)) : Enumerable.Empty<FileInfo>())
                .OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            long total = 0;
            foreach (var file in files)
            {
                total += file.Length;
                if (file.FullName != _file && (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30) || total > 512L * 1024 * 1024))
                    try { file.Delete(); } catch { }
            }
        }

        internal static void Shutdown()
        {
            try
            {
                if (!_enabled) return;
                Record("INFO", "Application", "Diagnostic shutdown requested");
                lock (Gate) _stopping = true;
                Wake.Set(); _writer?.Join(1500);
            }
            catch (Exception ex) { LastFailure = ex.Message; }
        }

        internal static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else result.Append(c);
            }
            return result.Append('"').ToString();
        }
        private static string Limit(string value, int size)
        { return value == null ? "" : value.Length <= size ? value : value.Substring(0, size) + " [truncated]"; }
    }
}

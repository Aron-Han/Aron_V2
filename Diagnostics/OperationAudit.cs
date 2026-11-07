using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Aron_V2
{
    internal sealed class OperationRecord
    {
        public DateTimeOffset Time { get; set; }
        public string User { get; set; }
        public string Window { get; set; }
        public string Action { get; set; }
        public string Content { get; set; }
        public string Version { get; set; }
        public string Build { get; set; }
        public string Session { get; set; }
    }

    // Separate from transient UI logs. One disk writer, shared readers, no automatic deletion.
    internal static class OperationAudit
    {
        private static readonly object Gate = new object();
        private static readonly BlockingCollection<OperationRecord> Pending = new BlockingCollection<OperationRecord>();
        private static Thread _writer;
        private static volatile bool _stopped;
        private static string _root;
        internal static string Root { get { return _root ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Operations"); } }
        internal static string LastFailure { get; private set; }
        private static string _user = "Guest";
        internal static string CurrentUser { get { return _user; } }
        internal static void SetUser(string user) { _user = string.IsNullOrWhiteSpace(user) ? "Guest" : user; }
        internal static void Initialize(string root = null)
        {
            lock (Gate)
            {
                if (_writer != null || _stopped) return;
                _root = root ?? Root;
                _writer = new Thread(WriteLoop) { IsBackground = true, Name = "Operation audit writer" };
                _writer.Start();
            }
        }
        internal static void Record(string window, string action, string content)
        {
            try
            {
                Initialize();
                Pending.Add(new OperationRecord { Time = DateTimeOffset.Now, User = CurrentUser,
                    Window = window, Action = action, Content = content, Version = BuildInfo.SoftwareVersion,
                    Build = BuildInfo.BuildDate, Session = DiagnosticLogStore.Session });
            }
            catch (InvalidOperationException) { /* shutdown has completed */ }
        }
        private static void WriteLoop()
        {
            foreach (var record in Pending.GetConsumingEnumerable())
            {
                // A failed write stays at the head until recovery; never silently drop it.
                while (true)
                {
                    try
                    {
                        Directory.CreateDirectory(Root);
                        string path = Path.Combine(Root, record.Time.ToString("yyyy-MM-dd") + ".jsonl");
                        byte[] line = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(record) + "\r\n");
                        using (var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                        { file.Write(line, 0, line.Length); file.Flush(true); }
                        LastFailure = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (LastFailure == null) DiagnosticLogStore.Exception("Operation audit write", ex);
                        LastFailure = ex.Message;
                        if (_stopped) return;
                        Thread.Sleep(1000);
                    }
                }
            }
        }
        internal static void Shutdown()
        {
            lock (Gate) { _stopped = true; Pending.CompleteAdding(); }
            if (_writer != null && _writer != Thread.CurrentThread) _writer.Join(3000);
        }
        internal static List<OperationRecord> Read(DateTime start, DateTime end, string search, int limit = 5000)
        {
            var result = new List<OperationRecord>();
            if (!Directory.Exists(Root)) return result;
            foreach (string path in Directory.EnumerateFiles(Root, "????-??-??.jsonl").OrderByDescending(p => p))
            {
                DateTime day;
                if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out day) || day.Date < start.Date || day.Date > end.Date) continue;
                // Keep the newest matching rows only, even for large daily files.
                var tail = new Queue<OperationRecord>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        OperationRecord record;
                        try { record = JsonConvert.DeserializeObject<OperationRecord>(line); } catch (JsonException) { continue; }
                        if (record == null || record.Time.LocalDateTime < start || record.Time.LocalDateTime > end) continue;
                        if (!string.IsNullOrWhiteSpace(search) && string.Join(" ", record.User, record.Window, record.Action, record.Content)
                            .IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        tail.Enqueue(record); if (tail.Count > limit) tail.Dequeue();
                    }
                }
                result.AddRange(tail.Reverse().Take(limit - result.Count));
                if (result.Count >= limit) break;
            }
            return result.OrderByDescending(r => r.Time).ToList();
        }
    }
}

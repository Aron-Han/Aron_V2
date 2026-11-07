using System;
using System.Collections.Generic;
using System.IO;

namespace Aron_V2
{
    // Application-owned data only. CLR .exe.config and native libraries keep
    // their required loader locations beside the executable.
    public static class ProjectPaths
    {
        private static readonly object Gate = new object();
        internal static readonly Dictionary<string, string[]> ConfigLocations = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["General/Branding.xml"] = new string[0],
            ["General/ConfigSaveImage.xml"] = new[] { "Project/Config/ConfigSaveImage.xml" },
            ["General/DisplayLayout.xml"] = new[] { "Project/Config/DisplayLayout.xml" },
            ["General/LogView.xml"] = new[] { "Project/Config/LogView.xml" },
            ["General/MainLayout.xml"] = new string[0],
            ["General/ProjectBackup.xml"] = new string[0],
            ["Jobs/Parameter.xml"] = new[] { "Project/Config/Parameter.xml" },
            ["Security/users.xml"] = new[] { "users.xml" },
            ["Security/UserAccess.xml"] = new[] { "Project/Config/UserAccess.xml" },
            ["Security/Permissions.xml"] = new[] { "Project/Config/Permissions.xml" },
            ["Communication/Communication.xml"] = new[] { "Project/Config/Communication.xml" },
            ["Communication/VPPOutput.xml"] = new[] { "Project/Config/VPPOutput.xml", "VppOutput.xml" },
            ["Communication/Profinet/ProfinetConfig.xml"] = new[] { "Project/Config/ProfinetConfig.xml" },
            // Project/PlcInput.xml was the active path; a root-level copy was not used.
            ["Communication/Profinet/PlcInput.xml"] = new[] { "Project/PlcInput.xml", "PlcInput.xml" },
            ["Communication/S7/S7Config.xml"] = new[] { "Config/S7Config.xml" },
            ["Communication/S7/S7InputTableConfig.xml"] = new[] { "Config/S7InputTableConfig.xml" },
            ["Communication/S7/S7OutputTableConfig.xml"] = new[] { "Config/S7OutputTableConfig.xml" },
            ["Communication/TcpIp/TcpIpConfig.xml"] = new[] { "Project/Config/TcpIpConfig.xml" }
        };
        public static string Config(string relative)
        {
            lock (Gate) return ResolveConfig(AppDomain.CurrentDomain.BaseDirectory, relative);
        }
        internal static string ResolveConfig(string applicationRoot, string relative)
        {
            string[] previous;
            if (!ConfigLocations.TryGetValue(relative, out previous)) throw new ArgumentException("Unknown configuration path: " + relative);
            string target = Path.Combine(applicationRoot, "Project", "Config", relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (File.Exists(target)) return target;
            foreach (string oldRelative in previous)
            {
                string old = Path.Combine(applicationRoot, oldRelative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(old)) continue;
                // Move the actual settings unchanged; never regenerate defaults,
                // merge address tables, or overwrite a newer categorized file.
                File.Move(old, target); break;
            }
            return target;
        }
        public static void MigrateConfiguration()
        { foreach (string key in ConfigLocations.Keys) Config(key); }
        public static string ApplicationLogs => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Application");
        public static string DatabaseDirectory => ResolveDataDirectory(AppDomain.CurrentDomain.BaseDirectory, "Data/Database", "Project/Database");
        public static string StatsDirectory => ResolveDataDirectory(AppDomain.CurrentDomain.BaseDirectory, "Data/Stats", "Project/Stats");
        internal static string ResolveDataDirectory(string applicationRoot, string relative, string legacy)
        {
            lock (Gate)
            {
                string root = Path.GetFullPath(applicationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                string old = Path.GetFullPath(Path.Combine(root, legacy.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !old.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Data directory must stay inside the application folder.");
                if (Directory.Exists(old))
                {
                    var files = Directory.GetFiles(old, "*", SearchOption.AllDirectories);
                    // Preflight conflicts before moving any file. Keep unique
                    // historical data instead of silently replacing it.
                    foreach (string source in files)
                        if (File.Exists(Path.Combine(target, source.Substring(old.Length + 1))))
                            throw new IOException("Data migration destination already exists: " + target);
                    foreach (string source in files)
                    {
                        string destination = Path.Combine(target, source.Substring(old.Length + 1));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Move(source, destination);
                    }
                    // Only empty directories remain, and both absolute paths
                    // were checked against the application root above.
                    Directory.Delete(old, true);
                }
                Directory.CreateDirectory(target); return target;
            }
        }
        public static void OrganizeApplicationData()
        {
            MigrateConfiguration();
            string database = DatabaseDirectory;
            string stats = StatsDirectory;
            ResolveDataDirectory(AppDomain.CurrentDomain.BaseDirectory, "Logs/Application", "Log/ApplicationLogs");
            // The obsolete database initializer created this separate store.
            // Retain its existing records as historical data, without using it
            // as the production database or generating another one at startup.
            string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mydata.db");
            foreach (string suffix in new[] { "", "-wal", "-shm", "-journal" })
                if (File.Exists(legacy + suffix)) File.Move(legacy + suffix, Path.Combine(database, "LegacyOrders.db" + suffix));
        }
    }
}

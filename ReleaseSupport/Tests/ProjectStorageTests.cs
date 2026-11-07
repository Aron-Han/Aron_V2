using Aron_V2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class ProjectStorageTests
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static string Resolve(string root, string key) => (string)typeof(ProjectPaths).GetMethod("ResolveConfig", Flags).Invoke(null, new object[] { root, key });
    static int Main()
    {
        try
        {
            string root = Path.Combine(Path.GetTempPath(), "AronProjectStorage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var locations = (Dictionary<string, string[]>)typeof(ProjectPaths).GetField("ConfigLocations", Flags).GetValue(null);
            foreach (var entry in locations)
            {
                string legacy = Path.Combine(root, entry.Value[0]); Directory.CreateDirectory(Path.GetDirectoryName(legacy));
                byte[] content = System.Text.Encoding.UTF8.GetBytes("original bytes / " + entry.Key + " / 中文"); File.WriteAllBytes(legacy, content);
                string target = Resolve(root, entry.Key);
                Check(target == Path.Combine(root, "Project", "Config", entry.Key.Replace('/', Path.DirectorySeparatorChar)) && File.ReadAllBytes(target).SequenceEqual(content) && !File.Exists(legacy), "Categorized migration preserves original bytes: " + entry.Key);
                Check(Resolve(root, entry.Key) == target && File.ReadAllBytes(target).SequenceEqual(content), "Migration is idempotent: " + entry.Key);
            }
            string obsolete = Path.Combine(root, "PlcInput.xml"); File.WriteAllText(obsolete, "inactive root copy");
            string active = Resolve(root, "Communication/Profinet/PlcInput.xml");
            Check(!File.ReadAllText(active).Contains("inactive"), "An inactive root-level PlcInput copy cannot override the active Project version");
            string targetUsers = Resolve(root, "Security/users.xml"); File.WriteAllText(Path.Combine(root, "users.xml"), "obsolete users copy");
            Check(!File.ReadAllText(Resolve(root, "Security/users.xml")).Contains("obsolete"), "An established categorized user store is never overwritten");
            string oldDatabase = Path.Combine(root, "Project", "Database"); Directory.CreateDirectory(oldDatabase);
            byte[] database = Enumerable.Range(0, 255).Select(i => (byte)i).ToArray(); File.WriteAllBytes(Path.Combine(oldDatabase, "VisionData.db"), database);
            var method = typeof(ProjectPaths).GetMethod("ResolveDataDirectory", Flags);
            string data = (string)method.Invoke(null, new object[] { root, "Data/Database", "Project/Database" });
            Check(File.ReadAllBytes(Path.Combine(data, "VisionData.db")).SequenceEqual(database) && !Directory.Exists(oldDatabase), "Database moves intact outside the configuration-only Project directory");
            Directory.CreateDirectory(oldDatabase); File.WriteAllText(Path.Combine(oldDatabase, "VisionData.db"), "conflicting data");
            bool rejected = false; try { method.Invoke(null, new object[] { root, "Data/Database", "Project/Database" }); } catch (TargetInvocationException e) { rejected = e.InnerException is IOException; }
            Check(rejected && File.ReadAllBytes(Path.Combine(data, "VisionData.db")).SequenceEqual(database), "Conflicting database destination is rejected without overwriting records");
            rejected = false; try { method.Invoke(null, new object[] { root, "../outside", "Project/Stats" }); } catch (TargetInvocationException e) { rejected = e.InnerException is InvalidOperationException; }
            Check(rejected, "Directory moves reject paths outside the application folder");
            Check(!Directory.Exists(Path.Combine(root, "Project", "Logs")) && !Directory.Exists(Path.Combine(root, "Project", "LmiSdk")), "Project migration does not mix logs or SDK libraries into configuration");
            Console.WriteLine("Fixture: " + root); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

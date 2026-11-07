using System;
using System.IO;
using System.Reflection;
internal static class LogSplitterPreferencesTests
{
    static void Main(string[] args)
    {
        var app = Assembly.LoadFrom(args[0]);
        var type = app.GetType("Aron_V2.LogSplitterPreferences", true);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var load = type.GetMethod("Load", flags); var save = type.GetMethod("Save", flags);
        var paths = app.GetType("Aron_V2.ProjectPaths", true);
        string path = (string)paths.GetMethod("Config").Invoke(null, new object[] { "General/MainLayout.xml" });
        byte[] previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            save.Invoke(null, new object[] { 247.5 });
            double restored = (double)load.Invoke(null, null);
            if (restored != 247.5) throw new Exception("Position not retained");
            Console.WriteLine("PASS persisted log pane height retains DPI-independent precision");
            File.WriteAllText(path, "<MainLayout LogHeight96Dpi=\"NaN\" />");
            if ((double)load.Invoke(null, null) != 0) throw new Exception("Invalid height accepted");
            File.WriteAllText(path, "broken xml");
            if ((double)load.Invoke(null, null) != 0) throw new Exception("Invalid XML accepted");
            Console.WriteLine("PASS malformed settings fall back to default layout");
        }
        finally { if (previous == null) File.Delete(path); else File.WriteAllBytes(path, previous); }
    }
}

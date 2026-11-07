using System;
using System.IO;
using System.Reflection;
class ConsolidatedLogTests
{
    [STAThread] static int Main(string[] args)
    {
        var assembly=Assembly.LoadFrom(args[0]);
        var flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        var diagnostics=assembly.GetType("Aron_V2.DiagnosticLogStore");
        diagnostics.GetMethod("Initialize",flags).Invoke(null,null);
        var hostType=assembly.GetType("Aron_V2.FormMain").GetNestedType("PluginHost",flags);
        var host=Activator.CreateInstance(hostType,flags,null,new object[]{null,"LogFixture"},null);
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string folder=(string)hostType.GetProperty("LogDirectory").GetValue(host,null);
        if(folder!=Path.Combine(root,"Logs","Diagnostics")) throw new Exception("Plugin log directory is not consolidated");
        hostType.GetMethod("Log").Invoke(host,new object[]{"INFO","NormalFixtureShouldNotPersist"});
        hostType.GetMethod("Log").Invoke(host,new object[]{"ERROR","ErrorFixtureMustPersist"});
        diagnostics.GetMethod("Shutdown",flags).Invoke(null,null);
        string all=string.Join("\n",Array.ConvertAll(Directory.GetFiles(folder,"*.jsonl"),File.ReadAllText));
        if(all.Contains("NormalFixtureShouldNotPersist")||!all.Contains("LogFixture | ErrorFixtureMustPersist")) throw new Exception("Plugin severity filtering failed");
        if(Directory.Exists(Path.Combine(root,"Logs","Plugins"))||Directory.Exists(Path.Combine(root,"Logs","VisionPro"))) throw new Exception("Legacy log directories created");
        Console.WriteLine("PASS plugin errors include plugin ID in Diagnostics, INFO suppressed, no separate log directories; no device/PLC startup.");
        return 0;
    }
}

using System;
using System.Collections;
using System.Reflection;
class VppChannelEvictionTests
{
    static int Main(string[] args)
    {
        var app = Assembly.LoadFrom(args[0]);
        var cache = app.GetType("Aron_V2.Load_Job+VppCache");
        var acq = (IDictionary)cache.GetField("Acq").GetValue(null);
        var tb = (IDictionary)cache.GetField("Tb").GetValue(null);
        // Null entries exercise ownership/removal without acquiring hardware.
        foreach (var map in new[] { acq, tb })
            foreach (string key in new[] { "Job1|CH0|Cam1|Pos1", "Job2|CH0|Cam1|Pos1", "Job1|CH1|Cam1|Pos1", "Job3|CH10|Cam1|Pos1" }) map.Add(key, null);
        cache.GetMethod("ClearChannel").Invoke(null, new object[] { 0 });
        foreach (var map in new[] { acq, tb })
            if (map.Count != 2 || !map.Contains("Job1|CH1|Cam1|Pos1") || !map.Contains("Job3|CH10|Cam1|Pos1")) throw new Exception("Incorrect channel eviction");
        cache.GetMethod("ClearChannel").Invoke(null, new object[] { 0 });
        Console.WriteLine("PASS all old Jobs removed from both VPP caches; other channels preserved; repeated eviction safe.");
        return 0;
    }
}

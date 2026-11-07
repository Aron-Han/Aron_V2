using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

// Reproduce only startup's STA preflight + MTA sequential deserialization.
// No main form, Bootstrap, tool execution, acquisition or communication.
internal static class VppStartupSequenceProbe
{
    [STAThread] static void Main(string[] args)
    {
        try
        {
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            object[] report = { null };
            if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report)) throw new Exception((string)report[0]);
            Task.Run(() =>
            {
                Console.WriteLine("APARTMENT " + System.Threading.Thread.CurrentThread.GetApartmentState());
                var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
                MethodInfo load = core.GetType("Cognex.VisionPro.CogSerializer").GetMethod("LoadObjectFromFile", new[] { typeof(string) });
                object inspection = null, camera = null;
                try
                {
                    var watch = Stopwatch.StartNew();
                    Console.WriteLine("BEGIN Inspection " + args[1]);
                    inspection = load.Invoke(null, new object[] { Path.GetFullPath(args[1]) });
                    Console.WriteLine("OK Inspection " + watch.ElapsedMilliseconds + "ms");
                    watch.Restart();
                    Console.WriteLine("BEGIN Camera " + args[2]);
                    camera = load.Invoke(null, new object[] { Path.GetFullPath(args[2]) });
                    Console.WriteLine("OK Camera " + watch.ElapsedMilliseconds + "ms");
                }
                finally { (camera as IDisposable)?.Dispose(); (inspection as IDisposable)?.Dispose(); }
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

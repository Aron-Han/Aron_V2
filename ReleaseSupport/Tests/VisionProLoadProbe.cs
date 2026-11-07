using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class VisionProLoadProbe
{
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    [STAThread] static void Main(string[] args)
    {
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        var watch = Stopwatch.StartNew();
        try
        {
            if (args.Contains("--cognex-path"))
                Environment.SetEnvironmentVariable("PATH", @"C:\Program Files\Cognex\VisionPro\bin;" + Environment.GetEnvironmentVariable("PATH"));
            if (args.Contains("--core-first")) Load("Cognex.VisionPro.Core");
            Load("Cognex.VisionPro.Comm");
            Console.WriteLine("PASS " + watch.ElapsedMilliseconds + " ms");
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + watch.ElapsedMilliseconds + " ms: " + ex); Environment.ExitCode = 2; }
        finally
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (new[] { "cog", "vcruntime", "msvcp", "wibu", "codemeter" }.Any(s => module.ModuleName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                    Console.WriteLine("MODULE " + module.ModuleName + " | " + module.FileName);
        }
    }
    static void Load(string name)
    {
        var assembly = Assembly.Load(name + ", Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
        Console.WriteLine("LOAD " + assembly.FullName + " | " + assembly.Location);
    }
}

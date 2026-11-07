using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

// Separate x64 process: no VisionPro, GoSystem, sensor discovery, camera or PLC.
internal static class LmiRuntimeSmokeTests
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);

    static void Main(string[] args)
    {
        try
        {
            if (!Environment.Is64BitProcess || (args.Length != 1 && args.Length != 3)) throw new Exception("Pass x64 LmiSdk directory, optionally application path and --visionpro-first/--gosdk-first.");
            bool combined = args.Length == 3;
            if (combined && args[2] != "--visionpro-first" && args[2] != "--gosdk-first") throw new Exception("Invalid load order.");
            if (combined && args[2] == "--visionpro-first") Audit(args[1]);
            string directory = Path.GetFullPath(args[0]);
            foreach (string name in new[] { "kApi.dll", "GoSdk.dll" })
                if (LoadLibraryEx(Path.Combine(directory, name), IntPtr.Zero, 0x1100) == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), name);
            foreach (var item in new[] { new[] { "kApiNet.dll", "Lmi3d.Zen.KApiLib" }, new[] { "GoSdkNet.dll", "Lmi3d.GoSdk.GoSdkLib" } })
            {
                var assembly = Assembly.LoadFrom(Path.Combine(directory, item[0]));
                assembly.GetType(item[1], true).GetMethod("Construct", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                Console.WriteLine("PASS initialized " + assembly.FullName + " from " + assembly.Location);
            }
            if (combined && args[2] == "--gosdk-first") Audit(args[1]);
            var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().ToArray();
            foreach (string name in new[] { "kApi.dll", "GoSdk.dll", "kApiNet.dll", "GoSdkNet.dll" })
            {
                var module = modules.Single(m => string.Equals(m.ModuleName, name, StringComparison.OrdinalIgnoreCase));
                if (!string.Equals(module.FileName, Path.Combine(directory, name), StringComparison.OrdinalIgnoreCase)) throw new Exception("Wrong module path: " + module.FileName);
                Console.WriteLine("PASS module path " + module.FileName);
            }
            if (!combined && modules.Any(m => m.ModuleName.StartsWith("Cognex", StringComparison.OrdinalIgnoreCase))) throw new Exception("Unexpected VisionPro module in SDK-only test.");
            Console.WriteLine(combined ? "PASS VisionPro/GoSDK coexistence " + args[2] + "; no GoSystem or sensor created." : "PASS GoSDK native/managed initialization; no Cognex loaded; no GoSystem or sensor created.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    static void Audit(string applicationPath)
    {
        var application = Assembly.LoadFrom(Path.GetFullPath(applicationPath));
        object[] report = { null };
        bool success = (bool)application.GetType("Aron_V2.VisionProRuntimeAudit", true).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, report);
        if (!success) throw new Exception((string)report[0]);
        Console.WriteLine("PASS " + report[0]);
    }
}

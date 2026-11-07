using Aron_V2;
using System;
using System.IO;

internal static class VisionProNativeBootstrapTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main()
    {
        string oldPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
        string machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
        string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
        string oldRoot = Environment.GetEnvironmentVariable("VPRO_ROOT", EnvironmentVariableTarget.Process);
        string machineRoot = Environment.GetEnvironmentVariable("VPRO_ROOT", EnvironmentVariableTarget.Machine);
        string userRoot = Environment.GetEnvironmentVariable("VPRO_ROOT", EnvironmentVariableTarget.User);
        string bin = @"C:\Program Files\Cognex\VisionPro\bin";
        try
        {
            string otherSdkEntries = @"C:\Windows\System32;D:\OtherCameraSdk\bin";
            Environment.SetEnvironmentVariable("PATH", otherSdkEntries, EnvironmentVariableTarget.Process);
            string selected = VisionProRuntimeAudit.PrepareNativeRuntime(new Version(71, 2, 0, 0), new[] { Path.Combine(Path.GetTempPath(), "missing-visionpro-runtime"), bin });
            Check(selected == bin && Environment.GetEnvironmentVariable("PATH") == bin + ";" + otherSdkEntries, "Matched native bin is first; all other SDK path entries are preserved");
            Check(Environment.GetEnvironmentVariable("VPRO_ROOT") == Path.GetDirectoryName(bin), "Editor resource root is set to the validated installation");
            string prepared = Environment.GetEnvironmentVariable("PATH");
            VisionProRuntimeAudit.PrepareNativeRuntime(new Version(71, 2, 0, 0), new[] { bin });
            Check(Environment.GetEnvironmentVariable("PATH") == prepared, "Repeated bootstrap does not grow PATH");
            bool rejected = false;
            try { VisionProRuntimeAudit.PrepareNativeRuntime(new Version(99, 0, 0, 0), new[] { bin }); }
            catch (DirectoryNotFoundException) { rejected = true; }
            Check(rejected && Environment.GetEnvironmentVariable("PATH") == prepared, "Wrong runtime version is rejected without changing PATH");
            Check(machinePath == Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) && userPath == Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User), "Machine and user PATH remain unchanged");
            Check(machineRoot == Environment.GetEnvironmentVariable("VPRO_ROOT", EnvironmentVariableTarget.Machine) && userRoot == Environment.GetEnvironmentVariable("VPRO_ROOT", EnvironmentVariableTarget.User), "Machine and user VPRO_ROOT remain unchanged");
            Check(!Array.Exists(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name.StartsWith("Cognex.", StringComparison.OrdinalIgnoreCase)), "Bootstrap loads no Cognex assemblies; metadata checks only");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        finally { Environment.SetEnvironmentVariable("PATH", oldPath, EnvironmentVariableTarget.Process); Environment.SetEnvironmentVariable("VPRO_ROOT", oldRoot, EnvironmentVariableTarget.Process); }
    }
}

using System;
using System.IO;
using System.Reflection;
internal static class CognexRuntimeProbe
{
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("64 bit: " + Environment.Is64BitProcess);
            if (args.Length >= 2 && args[0] == "--with-app")
            {
                var application = Assembly.LoadFrom(args[1]);
                object[] report = { null };
                var start = application.GetType("Aron_V2.VisionProRuntimeAudit", true).GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic);
                if (!(bool)start.Invoke(null, report)) throw new InvalidOperationException((string)report[0]);
                Console.WriteLine(report[0]);
                string[] rest = new string[args.Length - 2];
                Array.Copy(args, 2, rest, 0, rest.Length);
                args = rest;
            }
            if (args.Length > 0 && (args[0] == "--com" || args[0] == "--com-first"))
            {
                var comType = Type.GetTypeFromCLSID(new Guid("D0411ABA-1DA0-11D3-AD39-006097FE57F1"), true);
                object native = Activator.CreateInstance(comType);
                Console.WriteLine("PASS native COM object: " + native.GetType().FullName);
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(native);
                if (args[0] == "--com") Environment.Exit(0);
                args = new string[0];
            }
            var core = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
            var interop = Assembly.Load("Cognex.VisionPro.Interop.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
            Console.WriteLine("Interop loaded from " + interop.Location);
            if (args.Length == 0)
            {
                var type = core.GetType("Cognex.VisionPro.CogStringCollection", true);
                object collection = Activator.CreateInstance(type);
                Console.WriteLine("PASS CogStringCollection Count=" + type.GetProperty("Count").GetValue(collection));
                (collection as IDisposable)?.Dispose();
            }
            else
            {
                var method = core.GetType("Cognex.VisionPro.CogSerializer").GetMethod("LoadObjectFromFile", new[] { typeof(string) });
                object tool = method.Invoke(null, new object[] { args[0] });
                Console.WriteLine("PASS VPP: " + tool.GetType());
                (tool as IDisposable)?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            for (var error = ex; error != null; error = error.InnerException)
                if (error is FileLoadException) Console.WriteLine("FILE: " + ((FileLoadException)error).FileName + "\n" + ((FileLoadException)error).FusionLog);
            Dump(); Environment.Exit(1);
        }
        Dump(); Environment.Exit(0);
    }
    static void Dump()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if (asm.GetName().Name.StartsWith("Cognex")) Console.WriteLine(asm.FullName + " : " + asm.Location);
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
internal static class CognexEditorProbe
{
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        AppDomain.CurrentDomain.FirstChanceException += (s, e) => {
            if (e.Exception is FileLoadException || e.Exception is FileNotFoundException) Print(e.Exception);
        };
        var app = Assembly.LoadFrom(args[0]);
        foreach (string name in args.Length > 2 ? new[] { args[2] } : new string[0])
        {
            try
            {
                Console.WriteLine("START " + name);
                var cfg = Activator.CreateInstance(app.GetType("Aron_V2.AppConfig", true));
                using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2." + name, true), cfg))
                    Console.WriteLine("PASS " + name + " constructor");
            }
            catch (Exception ex) { Print(ex); }
        }
        if (args.Length > 1)
        {
            try
            {
                Console.WriteLine("START VPP deserialize");
                var sdk = Assembly.Load("Cognex.VisionPro.Core, Version=71.2.0.0, Culture=neutral, PublicKeyToken=ef0f902af9dee505");
                var method = sdk.GetType("Cognex.VisionPro.CogSerializer").GetMethod("LoadObjectFromFile", new[] { typeof(string) });
                var tool = method.Invoke(null, new object[] { args[1] });
                Console.WriteLine("PASS VPP load " + tool.GetType());
                (tool as IDisposable)?.Dispose();
            }
            catch (Exception ex) { Print(ex); }
        }
        Environment.Exit(0);
    }
    static void Print(Exception ex)
    {
        for (; ex != null; ex = ex.InnerException)
        {
            Console.WriteLine(ex.GetType() + ": " + ex.Message);
            if (ex is FileLoadException) Console.WriteLine("FILE: " + ((FileLoadException)ex).FileName);
        }
    }
}

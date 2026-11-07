using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
internal static class AlgorithmDesignerTests
{
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            LicenseManager.CurrentContext = new DesignerContext();
            var app = Assembly.LoadFrom(args[0]);
            var type = app.GetType("Aron_V2.Algorithm", true);
            if (type.GetConstructor(Type.EmptyTypes) == null) throw new Exception("No designer constructor");
            using (var form = (Form)Activator.CreateInstance(type))
            {
                form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
                if (type.GetField("_draft", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form) != null) throw new Exception("Designer created runtime VPP draft");
                form.Close();
            }
            Console.WriteLine("PASS Algorithm parameterless designer construction/show/disposal; no VPP loading or device startup");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}

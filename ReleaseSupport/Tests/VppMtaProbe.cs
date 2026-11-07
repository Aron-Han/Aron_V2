using System;
using System.Reflection;

// Diagnostic only: execute the exact existing deserialize-only probe on the
// MTA used by Task.Run. Never call application startup, acquisition or PLC.
internal static class VppMtaProbe
{
    [MTAThread] static void Main(string[] args)
    {
        Console.WriteLine("APARTMENT " + System.Threading.Thread.CurrentThread.GetApartmentState());
        typeof(VppOfflineProbe).GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { args });
    }
}

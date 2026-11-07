using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class DpiInitializationProbe
{
	[DllImport("shcore.dll")] private static extern int GetProcessDpiAwareness(IntPtr process, out int awareness);
	[DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
	[DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
	private static void Print(string stage)
	{
		int awareness; GetProcessDpiAwareness(IntPtr.Zero, out awareness);
		Console.WriteLine(stage + ": process=" + awareness + ", thread=" + GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()));
	}
	private static int Awareness()
	{
		int value;
		if (GetProcessDpiAwareness(IntPtr.Zero, out value) != 0) throw new InvalidOperationException("Cannot query process DPI awareness.");
		return value;
	}
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		using (Form form = new Form())
		{
			IntPtr handle = form.Handle;
			int before = Awareness();
			var bounds = form.Bounds;
			Print("Before loading application");
			Assembly app = Assembly.LoadFrom(args[0]);
			object service = Activator.CreateInstance(app.GetType("Aron_V2.ProfinetVisionCommService", true));
			Print("After constructing Profinet service");
			try
			{
				service.GetType().GetMethod("SetOnline").Invoke(service, new object[] { true });
				Print("After SetOnline");
				service.GetType().GetMethod("Start").Invoke(service, null);
			}
			catch (TargetInvocationException ex) { Console.WriteLine(ex.InnerException.Message); }
			Print("After Profinet initialization");
			Application.DoEvents();
			if (args.Length > 1 && args[1] == "--expect-stable")
			{
				if (before != 1 || Awareness() != before || GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != 1 || form.Bounds != bounds)
				{
					Console.WriteLine("FAIL DPI mode or window bounds changed.");
					Environment.Exit(1);
				}
				Console.WriteLine("PASS System DPI was selected before window creation and remained stable after Profinet initialization; window bounds unchanged.");
			}
			((IDisposable)service).Dispose();
		}
		// Some SDK dependencies create foreground dispatcher threads in a
		// diagnostic console process. Do not leave that process running.
		Environment.Exit(0);
	}
}

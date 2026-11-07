using System;
using System.ComponentModel;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;

internal static class DesignModeStartupTests
{
	private static Rectangle BoundsInForm(Control control)
	{
		Point location = control.Location;
		for (Control parent = control.Parent; parent != null && !(parent is Form); parent = parent.Parent)
			location.Offset(parent.Location);
		return new Rectangle(location, control.Size);
	}
	private sealed class DesignerContext : LicenseContext
	{
		public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } }
	}

	[STAThread]
	private static void Main(string[] args)
	{
		LicenseManager.CurrentContext = new DesignerContext();
		Assembly assembly = Assembly.LoadFrom(args[0]);
		Type type = assembly.GetType("Aron_V2.FormMain", true);
		using (var form = (Form)Activator.CreateInstance(type))
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
			var timer = (Timer)type.GetField("_communicationStatusTimer", flags).GetValue(form);
			if (timer.Enabled) throw new Exception("Design mode started the runtime timer.");
			if ((bool)type.GetField("_eventsHooked", flags).GetValue(form)) throw new Exception("Design mode subscribed runtime events.");
			type.GetMethod("FormMain_Load", flags).Invoke(form, new object[] { form, EventArgs.Empty });
			if (type.GetField("_communicationService", flags).GetValue(form) != null) throw new Exception("Design mode initialized communication.");
			Console.WriteLine("PASS FormMain constructs and loads in a designer license context without timers, runtime subscriptions or communication.");
			var layout = (TableLayoutPanel)type.GetField("tableLayoutPanel1", flags).GetValue(form);
			var cameras = (TableLayoutPanel)type.GetField("tableLayoutPanel5", flags).GetValue(form);
			var menu = (MenuStrip)type.GetField("menuStrip1", flags).GetValue(form);
			var status = (StatusStrip)type.GetField("statusStrip1", flags).GetValue(form);
			var log = (Control)type.GetField("_logPanel", flags).GetValue(form);
			var online = (Button)type.GetField("button3", flags).GetValue(form);
			var split = (SplitContainer)type.GetField("_inspectionLogSplit", flags).GetValue(form);
			if (form.AutoScaleMode != AutoScaleMode.Dpi) throw new Exception("Main form scaling must use DPI consistently.");
			if (cameras.RowCount != 1 || cameras.RowStyles.Count != 1 || cameras.ColumnCount != 1) throw new Exception("Unexpected designer placeholder rows/columns.");
			if (layout.ColumnCount != 1 || layout.RowCount != 3 || split.Orientation != Orientation.Horizontal || cameras.Parent != split.Panel1 || log.Parent != split.Panel2 || online.Parent.Name != "OnlineHeaderHost") throw new Exception("Unexpected bottom-log/header layout.");
			foreach (Size size in new[] { new Size(1280, 720), new Size(1920, 1080), new Size(2560, 1440) })
			{
				form.ClientSize = size;
				form.PerformLayout(); layout.PerformLayout();
				Rectangle cameraBounds = BoundsInForm(cameras), logBounds = BoundsInForm(log), onlineBounds = BoundsInForm(online), statusBounds = BoundsInForm(status);
				if (cameraBounds.IntersectsWith(logBounds) || logBounds.IntersectsWith(onlineBounds) || statusBounds.Top < onlineBounds.Bottom || cameraBounds.Bottom > statusBounds.Top)
					throw new Exception("Overlapping main layout at " + size);
				Console.WriteLine("PASS Camera, log, online button and status bounds do not overlap at " + size);
			}
		}
		Environment.Exit(0);
	}
}

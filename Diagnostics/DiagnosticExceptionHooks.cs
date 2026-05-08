using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    internal static class DiagnosticExceptionHooks
    {
        private static int _installed;
        internal static void Install()
        {
            if (Interlocked.Exchange(ref _installed, 1) != 0) return;
            Application.ThreadException += (s, e) =>
            {
                DiagnosticLogStore.WriteCrashReport(e.Exception, "WinFormsThreadException", false);
                // Preserve the usual WinForms continue/quit choice. Do not
                // automatically continue, restart, stop PLC or alter outputs.
                using (var dialog = new ThreadExceptionDialog(e.Exception))
                    if (dialog.ShowDialog() == DialogResult.Abort) Application.Exit();
            };
            TaskScheduler.UnobservedTaskException += (s, e) => DiagnosticLogStore.WriteCrashReport(e.Exception, "UnobservedTaskException", false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => DiagnosticLogStore.WriteCrashReport(e.ExceptionObject as Exception, "AppDomainUnhandledException", e.IsTerminating);
        }
    }
}

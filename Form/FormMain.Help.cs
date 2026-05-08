using System;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class FormMain
    {
        private HelpForm _helpWindow;
        private void InitializeHelpMenu()
        {
            var help = new ToolStripMenuItem("&Help") { Name = "helpToolStripMenuItem" };
            var software = new ToolStripMenuItem("Software Operation") { Name = "helpSoftwareMenuItem" };
            var communication = new ToolStripMenuItem("Communication Help") { Name = "helpCommunicationMenuItem" };
            software.Click += (s, e) => OpenHelp("SoftwareOperation");
            communication.Click += (s, e) => OpenHelp("Communication");
            help.DropDownItems.AddRange(new ToolStripItem[] { software, communication });
            help.DropDownItems.Add(new ToolStripSeparator());
            var export = new ToolStripMenuItem("Export diagnostics") { Name = "helpExportDiagnosticsMenuItem" };
            export.Click += async (s, e) =>
            {
                export.Enabled = false;
                try
                {
                    string path = await System.Threading.Tasks.Task.Run(() => DiagnosticPackageExporter.ExportPackage());
                    if (!IsDisposed) MessageBox.Show(this, UiLanguage.Text("Diagnostic package saved. Review logs before sharing:") + "\r\n" + path,
                        UiLanguage.Text("Help"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    DiagnosticLogStore.Exception("Diagnostic export", ex);
                    if (!IsDisposed) MessageBox.Show(this, ex.Message, UiLanguage.Text("Help"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { if (!export.IsDisposed) export.Enabled = true; }
            };
            help.DropDownItems.Add(export);
            menuStrip1.Items.Insert(menuStrip1.Items.IndexOf(manualTestToolStripMenuItem) + 1, help);
        }
        private void OpenHelp(string module)
        {
            try
            {
                if (_helpWindow == null || _helpWindow.IsDisposed)
                {
                    _helpWindow = new HelpForm(module);
                    _helpWindow.FormClosed += (s, e) => _helpWindow = null;
                    _helpWindow.Show(this);
                }
                else { _helpWindow.SelectModule(module); _helpWindow.Activate(); }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.GetBaseException().Message, UiLanguage.Text("Help"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}

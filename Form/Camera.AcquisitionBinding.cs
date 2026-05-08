using Aron_V2.UI_Update;
using Cognex.VisionPro;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class Camera
    {
        private Panel _cognexHost;
        private bool _cognexInitialized;

        private void InitializeAcquisitionBinding()
        {
            // Visionpro-only page: original full-height editor, no mode strip.
            Text = "Camera — Visionpro";
            _cognexHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            tableLayoutPanel1.Controls.Add(_cognexHost, 0, 1);
            _cognexHost.Controls.Add(new Label { Text = "Select a Visionpro camera. SDK cameras have separate Camera submenu pages.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool EnsureCognexEditor()
        {
            if (_cognexInitialized) return true;
            CogAcqFifoEditV2 editor = null;
            try
            {
                Load_Job.EnsureCognexInteropLoaded();
                editor = new CogAcqFifoEditV2();
                ((ISupportInitialize)editor).BeginInit();
                editor.Name = "cogAcqFifoEditV21"; editor.Dock = DockStyle.Fill;
                editor.Margin = new Padding(4); editor.SuspendElectricRuns = false;
                ((ISupportInitialize)editor).EndInit();
                _cognexHost.Controls.Clear(); _cognexHost.Controls.Add(editor);
                cogAcqFifoEditV21 = editor; _cognexInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                try { editor?.Dispose(); } catch { }
                ShowCognexEditorFailure(ex); return false;
            }
        }

        private void ShowCognexEditorFailure(Exception ex)
        {
            try { ResetCognexEditor(); } catch { }
            _cognexHost.Controls.Clear();
            _cognexHost.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, Dock = DockStyle.Fill,
                Text = "Camera VPP/editor could not be loaded. Check the VPP file and VisionPro runtime, then select again to retry. SDK cameras have separate Camera submenu pages.\r\n" + ex.GetBaseException().Message });
            LogChangeEventArgs.Set("Log", "Camera VPP/editor failed: " + ex, Color.Red);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void ResetCognexEditor()
        {
            var previous = cogAcqFifoEditV21;
            cogAcqFifoEditV21 = null; _cognexInitialized = false;
            previous?.Dispose();
        }

        private void BindCameraSubject(CogAcqFifoTool subject)
        {
            // Loading/binding is not an acquisition request. Restore the user's
            // original electric-run setting afterwards; manual controls remain.
            bool suspended = cogAcqFifoEditV21.SuspendElectricRuns;
            try
            {
                cogAcqFifoEditV21.SuspendElectricRuns = true;
                cogAcqFifoEditV21.Subject = subject;
                cogAcqFifoEditV21.Refresh();
            }
            finally { cogAcqFifoEditV21.SuspendElectricRuns = suspended; }
        }
    }
}

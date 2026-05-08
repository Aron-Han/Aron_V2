using Aron_V2.CameraIntegration;
using Aron_V2.UI_Update;
using Cognex.VisionPro;
using System;
using System.Drawing;
using System.Linq;

namespace Aron_V2
{
    public partial class FormMain
    {
        // Read-only UI guard. No PLC/service state is changed by camera settings.
        internal bool CanTestSdkCamera { get { return (!_runOnline || !_cameraValidationComplete) && _cameraRecovering == 0 && _chGates.All(g => g.CurrentCount > 0); } }

        private bool TryAcquireInspectionImage(CogAcqFifoTool camera, string camName, string jobId, string posName, out ICogImage image)
        {
            image = null;
            var binding = CameraAcquisition.Find(Global.CurrentConfig, jobId, camName);
            if (!CameraAcquisition.IsSdk(binding))
            {
                // Preserve the existing SN checks, Trigger error recovery and
                // acquisition semantics for every ordinary Cognex camera.
                if (!Trigger(camera, camName, jobId, posName)) return false;
                image = camera.OutputImage;
                return true;
            }
            try
            {
                image = CameraAcquisition.Capture(binding);
                int index;
                if (int.TryParse(camName.Substring(3), out index) && index > 0 && index <= camViews.Count)
                    camViews[index - 1].SetButton_CamStatus(Color.Green, camName);
                LogChangeEventArgs.Set("Log", camName + " SDK capture complete (" + binding.ConnectionMode + ", " + binding.IpAddress + ")", Color.Black);
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticLogStore.Exception("SDK acquisition " + jobId + "/" + camName + "/" + posName, ex);
                Trigger_Failed(camName, ex.GetBaseException().Message);
                return false;
            }
        }
    }
}

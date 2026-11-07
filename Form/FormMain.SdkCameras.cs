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
        private bool IsEffectivelyOnline => _runOnline && _licenseOk && _cameraValidationComplete &&
            _cameraRestartRequested == 0 && _communicationService != null && _communicationService.IsStarted;
        internal bool CanTestSdkCamera => SdkCameraTestBlockReason() == null;
        private string SdkCameraStateBlockReason()
        {
            if (_cameraRecoveryStopping) return UiLanguage.Text("The application is stopping. SDK testing is unavailable.");
            if (_switchingOnline) return UiLanguage.Text("Online/Offline switching is in progress. Please wait.");
            if (_cameraRecovering != 0) return UiLanguage.Text("Camera recovery is in progress. Please wait.");
            if (_cameraRestartRequested != 0) return UiLanguage.Text("Camera restart is pending. SDK testing is unavailable.");
            if (IsEffectivelyOnline) return UiLanguage.Text("The system is Online. Switch Offline before SDK testing.");
            return null;
        }
        internal string SdkCameraTestBlockReason()
        {
            string reason=SdkCameraStateBlockReason(); if(reason!=null) return reason;
            var busy=_chGates.Select((g,ch)=>new { Gate=g,Channel=ch }).Where(x=>x.Gate.CurrentCount==0).Select(x=>"CH"+x.Channel).ToArray();
            return busy.Length==0 ? null : UiLanguage.Text("Channel operations are still in progress:")+" "+string.Join(", ",busy);
        }
        internal string SdkCameraTestState => "RequestedOnline="+_runOnline+"; EffectiveOnline="+IsEffectivelyOnline+
            "; CameraValidated="+_cameraValidationComplete+"; LicenseOK="+_licenseOk+
            "; ServiceStarted="+(_communicationService!=null&&_communicationService.IsStarted)+
            "; Recovering="+_cameraRecovering+"; Switching="+_switchingOnline+"; RestartPending="+_cameraRestartRequested;
        internal IDisposable AcquireSdkTestGates()
        {
            string reason=SdkCameraTestBlockReason(); if(reason!=null) throw new InvalidOperationException(reason);
            var held=new System.Collections.Generic.List<System.Threading.SemaphoreSlim>();
            try
            {
                // Protect the whole test, not only the instant the button was clicked.
                for(int channel=0;channel<_chGates.Length;channel++)
                {
                    if(!_chGates[channel].Wait(0)) throw new InvalidOperationException(UiLanguage.Text("Channel operations are still in progress:")+" CH"+channel);
                    held.Add(_chGates[channel]);
                }
                reason=SdkCameraStateBlockReason(); if(reason!=null) throw new InvalidOperationException(reason);
                return new AlgorithmSaveLease(()=> { foreach(var gate in held) gate.Release(); });
            }
            catch { foreach(var gate in held) gate.Release(); throw; }
        }

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

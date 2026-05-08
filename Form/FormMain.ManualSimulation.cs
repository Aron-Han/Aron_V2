using Aron_V2.UI_Update;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class FormMain
    {
        // Execution-context-local, including nested Task.Run camera work.
        // Never disable a shared PLC service or suppress another channel.
        private static readonly AsyncLocal<bool> _manualLocalOnly = new AsyncLocal<bool>();
        private int _manualSimulationBusy;

        private static string NormalizeInspectionPosition(string position)
        {
            // Preserve legacy named positions, while accepting complete numeric
            // IDs from the same byte-valued user-data input (e.g. 10 -> Pos10).
            if (position == null) throw new ArgumentNullException(nameof(position));
            int number;
            return position.Length == 1 || int.TryParse(position, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out number) ? "Pos" + position : position;
        }

        private static string InspectionPositionNumber(string position)
        {
            return position.StartsWith("Pos", StringComparison.OrdinalIgnoreCase) && position.Length > 3
                ? position.Substring(3) : position.Substring(position.Length - 1, 1);
        }

        private void InitializeManualSimulationMenu()
        {
            trigger0ToolStripMenuItem.Text = "Trigger";
            trigger1ToolStripMenuItem.Visible = false; // Retain original menu/permission objects.
        }

        private async void ShowManualSimulation(bool receiveData, int initialChannel)
        {
            if (_manualSimulationBusy != 0) return;
            try
            {
                var config = CommunicationConfigHelper.Load();
                using (var dialog = new ManualSimulationDialog(config, receiveData, initialChannel))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    var request = dialog.Request;
                    if (Interlocked.CompareExchange(ref _manualSimulationBusy, 1, 0) != 0) return;
                    try
                    {
                        if (!ValidateManualRequest(config, request, receiveData)) return;
                        LogChangeEventArgs.Set("Log", request.Describe(config.Protocol, receiveData), Color.Blue);
                        if (receiveData)
                        {
                            await Task.Run(() => ProcessCommunicationInputCore(request.ToInput(), !request.PlcInteraction));
                            RefreshManualChannelViews(request.Channel);
                            LogChangeEventArgs.Set("Log", "Manual Receive Data processing finished. CH=" + request.Channel +
                                ", current Job=" + Global.Model_JobID[request.Channel] + ", Pos=" + Global.Position_ID[request.Channel] +
                                ", PartN=" + Newtonsoft.Json.JsonConvert.SerializeObject(Global.PartCode[request.Channel]), Color.Blue);
                        }
                        else
                        {
                            await ProcessCommunicationTriggerCore(request.Channel, !request.PlcInteraction, null);
                            RefreshManualChannelViews(request.Channel);
                        }
                    }
                    finally { Interlocked.Exchange(ref _manualSimulationBusy, 0); }
                }
            }
            catch (Exception ex)
            {
                LogChangeEventArgs.Set("Log", "Manual simulation failed: " + ex.GetBaseException().Message, Color.Red);
            }
        }

        private void RefreshManualChannelViews(int channel)
        {
            if (IsDisposed || Disposing || !IsValidChannel(channel)) return;
            string job = Global.Model_JobID[channel];
            string pos = NormalizeInspectionPosition(Global.Position_ID[channel]);
            for (int i = 0; i < camViews.Count; i++)
            {
                CameraGeneralRuntime general;
                if (!Global.CamGeneral.TryGetValue(job + ".Cam" + (i + 1) + "." + pos, out general) || general == null) continue;
                if ((general.MainUsed == "1" && general.MainChannel == channel.ToString()) ||
                    (general.SecondUsed == "1" && general.SecondChannel == channel.ToString()))
                {
                    camViews[i].SetButton_JobID(Color.Green, job);
                    camViews[i].SetButton_PosID(Color.Green, pos);
                }
            }
        }

        private bool ValidateManualRequest(CommunicationConfig config, ManualSimulationRequest request, bool receiveData)
        {
            // Re-read configuration after the modal dialog: stale selection
            // must not run against another transport/channel count.
            var current = CommunicationConfigHelper.Load();
            string reason = null;
            if (current.Protocol != config.Protocol || request.Channel < 0 || request.Channel >= CommunicationConfigHelper.GetEffectiveChannelCount(current))
                reason = "Communication configuration changed; reopen the manual dialog.";
            else if (!IsHandleCreated || IsDisposed || _cameraRecoveryStopping || _cameraRestartRequested != 0)
                reason = "Application/camera recovery is not ready.";
            else if (!receiveData && (!_cameraValidationComplete || !_licenseOk))
                reason = "Camera validation/license is not ready; no capture was started.";
            else if (request.PlcInteraction && (config.Protocol == CommunicationProtocol.TcpIp || _communicationService == null ||
                !_communicationService.IsStarted || !_runOnline || !_cameraValidationComplete || !_licenseOk ||
                (_displayProtocol.HasValue && _displayProtocol.Value != config.Protocol)))
                reason = "PLC interaction requires the selected running transport, Online and camera/license readiness. TCP/IP transport is not implemented.";
            if (reason == null) return true;
            LogChangeEventArgs.Set("Log", "Manual simulation blocked: " + reason, Color.Red);
            return false;
        }
    }
}

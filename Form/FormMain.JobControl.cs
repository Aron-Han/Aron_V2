using System;
using System.Drawing;
using System.Linq;

namespace Aron_V2
{
    public partial class FormMain
    {
        private System.Collections.Generic.HashSet<int> _catalogCameraIds;
        internal void LoadAlgorithmJob(int channel, string job)
        {
            string reason = JobControlBlockReason();
            if (reason != null) throw new InvalidOperationException(reason);
            using (AcquireAlgorithmSaveGates(new[] { channel }))
                Load_Job.LoadVPP_ForChannel_AllPos(channel, job);
        }
        internal IDisposable AcquireAlgorithmSaveGates(int[] channels)
        {
            if (_cameraRecovering != 0 || _cameraRecoveryStopping || _switchingOnline)
                throw new InvalidOperationException(UiLanguage.Text("Wait for camera recovery or Online/Offline switching before saving the algorithm."));
            var held = new System.Collections.Generic.List<System.Threading.SemaphoreSlim>();
            try
            {
                foreach (int channel in channels.Distinct().OrderBy(c => c))
                {
                    if (channel < 0 || channel >= _chGates.Length) throw new InvalidOperationException("Invalid channel: " + channel);
                    if (!_chGates[channel].Wait(0)) throw new InvalidOperationException(UiLanguage.Text("Algorithm save blocked: channel is busy.") + " CH" + channel);
                    held.Add(_chGates[channel]);
                }
                return new AlgorithmSaveLease(() => { foreach (var gate in held) gate.Release(); });
            }
            catch { foreach (var gate in held) gate.Release(); throw; }
        }
        private sealed class AlgorithmSaveLease : IDisposable
        {
            private Action _release;
            internal AlgorithmSaveLease(Action release) { _release = release; }
            public void Dispose() { System.Threading.Interlocked.Exchange(ref _release, null)?.Invoke(); }
        }
        private string JobControlBlockReason()
        {
            // _runOnline is an operator request (true at startup), not proof
            // that the system is Online. Match the effective status conditions
            // without changing that request or writing communication signals.
            if (_switchingOnline) return UiLanguage.Text("Online/Offline switching is in progress. Please wait.");
            if (_cameraRecoveryStopping) return UiLanguage.Text("The application is stopping. Job Control is unavailable.");
            if (_cameraRecovering != 0) return UiLanguage.Text("Camera recovery is in progress. Please wait.");
            var busy = _chGates.Select((gate, channel) => new { gate, channel }).Where(x => x.gate.CurrentCount == 0).Select(x => "CH" + x.channel).ToArray();
            if (busy.Length > 0) return UiLanguage.Text("Channel operations are still in progress:") + " " + string.Join(", ", busy);
            if (_runOnline && _licenseOk && _communicationService != null && _cameraValidationComplete && _cameraRestartRequested == 0)
                return UiLanguage.Text("The system is Online. Switch Offline before editing Job Control.");
            return null;
        }
        // Refresh configuration and presentation only. Do not open a device,
        // reset PLC signals, change selected recipes, or reload running tools.
        private void ApplyCatalogConfiguration()
        {
            var config = XmlConfigHelper.Load(Global.ParameterCogfig);
            Global.CurrentConfig = config;
            _vppOutCfg = XmlConfigHelper.LoadVppOutput(Global.VppOutputCfgPath);
            // Build the entire lookup before publishing it; other channels may
            // use different Jobs. Keep the existing runtime container identity.
            var entries = new System.Collections.Generic.Dictionary<string, CameraGeneralRuntime>(StringComparer.OrdinalIgnoreCase);
            int highest = 0;
            var cameraIds = new System.Collections.Generic.HashSet<int>();
            foreach (var job in config.Models)
            foreach (var camera in job.Cameras ?? Enumerable.Empty<CameraConfig>())
            {
                int id;
                if (camera.Name != null && camera.Name.StartsWith("Cam") && int.TryParse(camera.Name.Substring(3), out id))
                { highest = Math.Max(highest, id); cameraIds.Add(id); }
                if (camera.Name != null) _camLocks.GetOrAdd(camera.Name, _ => new object());
                foreach (var pos in camera.Positions ?? Enumerable.Empty<PositionConfig>())
                {
                    var g = pos.General; if (g == null) continue;
                    entries[job.Name + "." + camera.Name + "." + pos.Name] = new CameraGeneralRuntime {
                        Exposure = g.Exposure ?? "10", MainUsed = g.MainUsed ?? "0", MainChannel = g.MainChannel ?? "0",
                        SecondUsed = g.SecondUsed ?? "0", SecondChannel = g.SecondChannel ?? "0" };
                }
            }
            Global.CamGeneral = entries;
            Global.CamN_Use = highest;
            _catalogCameraIds = cameraIds;
            // Preserve existing controls and counters, including numeric slots
            // retained after deleting a camera. Never renumber Cam IDs.
            if (_stats == null) _stats = new CameraStats[0];
            if (highest > _stats.Length) Array.Resize(ref _stats, highest);
            if (Global.Result_data_send == null) Global.Result_data_send = new string[highest];
            else if (highest > Global.Result_data_send.Length)
            { var buffer = Global.Result_data_send; Array.Resize(ref buffer, highest); Global.Result_data_send = buffer; }
            while (camViews.Count < highest)
            {
                int index = camViews.Count;
                var view = new UserControl_ImageShow { Name = (index + 1).ToString(), Dock = System.Windows.Forms.DockStyle.Fill };
                _stats[index] = new CameraStats("Cam" + (index + 1)); view.BindStats(_stats[index]);
                view.SetButton_CamID(Color.Gainsboro, "Cam" + (index + 1));
                view.SetButton_JobID(Color.Gainsboro, Global.Model_JobID[0]);
                view.Trigger_Manual += CamView_Trigger_Manual; view.Reset_Count += CamView_Reset_Count; view.Replay += CamView_Replay;
                camViews.Add(view);
            }
            ApplyCameraViewLayout();
            foreach (var output in System.Windows.Forms.Application.OpenForms.OfType<Out_put_Parameters>().ToArray())
                output.RefreshCatalogEntries();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using Aron_V2.UI_Update;

namespace Aron_V2
{
    public sealed class CameraRecoveryOptions
    {
        // Old configurations retain automatic recovery, but no longer reload every VPP by default.
        public bool Enabled=true, ReloadVpp=false, OnlyOffline=true;
        public int IntervalSeconds=30;
        public CameraRecoveryOptions Copy()=>new CameraRecoveryOptions { Enabled=Enabled,ReloadVpp=ReloadVpp,OnlyOffline=OnlyOffline,IntervalSeconds=IntervalSeconds };
    }
    public partial class FormMain
    {
        private readonly HashSet<string> _healthyRecoveryCameras=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _recoveryAnnounced;
        private CameraRecoveryOptions RecoveryOptions => ImageRecord.Current.Recovery??new CameraRecoveryOptions();
        private string RecoveryKey(string cam)
        {
            // Use the loaded binding, not only CamN: a Job/binding change invalidates prior health.
            var bindings=Global.CamGeneral.Where(k=>k.Value!=null && (k.Value.MainUsed=="1"||k.Value.SecondUsed=="1"))
                .Select(k=>k.Key.Split('.')).Where(p=>p.Length>=3&&string.Equals(p[1],cam,StringComparison.OrdinalIgnoreCase))
                .Select(p=> {
                    var c=CameraIntegration.CameraAcquisition.Find(Global.CurrentConfig,p[0],cam);
                    return p[0]+"|"+cam+"|"+c?.ConnectionMode+"|"+c?.IpAddress+"|"+c?.SerialNumber;
                }).Distinct().OrderBy(x=>x);
            return string.Join(";",bindings);
        }
        private bool SkipHealthyRecovery(string cam,bool recovering) { lock(_healthyRecoveryCameras) return recovering && RecoveryOptions.OnlyOffline && _healthyRecoveryCameras.Contains(RecoveryKey(cam)); }
        private void MarkRecoveryHealthy(string cam) { lock(_healthyRecoveryCameras) _healthyRecoveryCameras.Add(RecoveryKey(cam)); }
        private void InvalidateRecoveryHealth(string cam) { lock(_healthyRecoveryCameras) { if(cam==null) _healthyRecoveryCameras.Clear();else _healthyRecoveryCameras.Remove(RecoveryKey(cam)); } }
        private void ReloadRecoveryTools()
        {
            if(!RecoveryOptions.ReloadVpp) return;
            var keys=Load_Job.VppCache.Acq.Keys.Concat(Load_Job.VppCache.Tb.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            foreach(string key in keys)
            {
                var p=key.Split('|');int ch;
                if(p.Length!=4||!int.TryParse(p[1].Substring(2),out ch)||SkipHealthyRecovery(p[2],true)) continue;
                Load_Job.ReloadCameraBinding(p[0],ch,p[2],p[3]);
            }
        }
        private void RecoveryStatus(string message,Color color)
        {
            // Prefix is a stable UI status key, replaced rather than appended by LogPanel.
            LogChangeEventArgs.Set("Log",UiLanguage.Text("Camera recovery: ")+message,color);
        }
    }
}

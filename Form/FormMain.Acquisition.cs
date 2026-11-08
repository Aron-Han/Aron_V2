using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Drawing;
using Aron_V2.UI_Update;

namespace Aron_V2
{
    internal sealed class ChannelAcquisitionCycle
    {
        private readonly object _gate=new object();
        private readonly HashSet<string> _pending;
        private readonly Action _completed;
        private bool _failed,_sent;
        internal ChannelAcquisitionCycle(IEnumerable<string> expected,Action completed)
        { _pending=new HashSet<string>(expected,StringComparer.OrdinalIgnoreCase);_completed=completed; }
        internal void Failed() { lock(_gate)_failed=true; }
        internal void Acquired(string key)
        {
            bool send=false;
            lock(_gate)
            {
                if(_failed||_sent||!_pending.Remove(key))return;
                if(_pending.Count==0) {_sent=true;send=true;}
            }
            if(send)_completed();
        }
    }
    public partial class FormMain
    {
        private static readonly AsyncLocal<ChannelAcquisitionCycle> _acquisitionCycle=new AsyncLocal<ChannelAcquisitionCycle>();
        private ChannelAcquisitionCycle BeginAcquisitionCycle(string job,string pos,int channel)
        {
            var service=_communicationService;
            var notifications=service as IVisionAcquisitionNotifications;
            if(notifications==null||_manualLocalOnly.Value||channel<0||Global.Manual_Trigger_Lock==null||channel>=Global.Manual_Trigger_Lock.Length||Global.Manual_Trigger_Lock[channel])return null;
            var model=Global.CurrentConfig?.Models?.FirstOrDefault(m=>string.Equals(m.Name,job,StringComparison.OrdinalIgnoreCase));
            if(model?.Cameras==null)return null;
            var main=new List<string>();var second=new List<string>();
            for(int i=0;i<model.Cameras.Count;i++)
            {
                var general=model.Cameras[i]?.Positions?.FirstOrDefault(p=>p.Name==pos)?.General;
                if(general==null)continue;
                if(general.MainUsed=="1"&&general.MainChannel==channel.ToString())main.Add("Main|Cam"+(i+1));
                if(general.SecondUsed=="1"&&general.SecondChannel==channel.ToString())second.Add("Second|Cam"+(i+1));
            }
            // Second-group acquisition and analysis already precede the main
            // group in the existing workflow. Do not change that data dependency.
            var expected=main.Count>0?main:second;
            if(expected.Count==0)return null;
            return new ChannelAcquisitionCycle(expected,()=>
            {
                if(!ReferenceEquals(service,_communicationService)||!_runOnline||!_licenseOk||!_cameraValidationComplete||_cameraRestartRequested!=0||_cameraRecoveryStopping||!service.IsStarted)return;
                try
                {
                    notifications.NotifyAcquisitionComplete(channel);
                    DiagnosticLogStore.Stage("Acquisition CH="+channel,"All required main-camera images acquired; Exposure Complete notified before VPP analysis finished.");
                }
                catch(Exception ex)
                {
                    DiagnosticLogStore.Exception("Notify acquisition complete CH="+channel,ex);
                    LogChangeEventArgs.Set("Log","Communication Exposure Complete notification failed. CH="+channel+": "+ex.Message,Color.Red);
                }
            });
        }
    }
}

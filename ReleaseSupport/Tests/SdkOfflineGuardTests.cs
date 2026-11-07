using Aron_V2;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
class SdkOfflineGuardTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    sealed class Designer : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    sealed class FakeService : IVisionCommunicationService
    {
        internal int Calls; public bool IsStarted { get; set; }
        public event Action<CommunicationChannelInput> InputChanged { add {} remove {} }
        public event Action<int> TriggerRequested { add {} remove {} }
        public event Action<int> ResultAckReceived { add {} remove {} }
        public event Action<string> LogMessage { add {} remove {} }
        public void Start() { Calls++; throw new Exception("No PLC startup allowed"); }
        public void Stop() { Calls++; } public void Dispose() { Calls++; }
        public void SetCameraReady(bool value) { Calls++; } public void SetOnline(bool value) { Calls++; }
        public void NotifyInspectionStart(int c) { Calls++; } public void NotifyInspectionEnd(int c) { Calls++; }
        public void WriteJobPosEcho(int c,byte j,byte p) { Calls++; }
        public void WriteInspectionResult(int c,float x,float y,float a,short result) { Calls++; }
        public void ClearInspectionResult(int c) { Calls++; } public void ClearAllInspectionResult(int c) { Calls++; }
        public void ClearExposureComplete(int c) { Calls++; }
    }
    static void Set(object target,string name,object value) { target.GetType().GetField(name,Flags).SetValue(target,value); }
    static object Call(object target,string name) { return target.GetType().GetMethod(name,Flags).Invoke(target,null); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS "+message); }
    [STAThread] static int Main(string[] args)
    {
        if(args.Length>0) Assembly.LoadFrom(args[0]);
        return Run();
    }
    static int Run()
    {
        try
        {
            LicenseManager.CurrentContext=new Designer();
            using(var main=new FormMain())
            {
                var type=main.GetType(); Func<string> reason=()=> (string)Call(main,"SdkCameraTestBlockReason");
                var fake=new FakeService();
                Set(main,"_runOnline",true); Set(main,"_licenseOk",true); Set(main,"_cameraValidationComplete",true); Set(main,"_communicationService",null);
                Check(reason()==null,"Requested Online with no service is effectively Offline and permits SDK testing");
                Set(main,"_communicationService",fake); Check(reason()==null,"Disconnected service does not masquerade as Online");
                fake.IsStarted=true; Check(reason().Contains("Online"),"Genuinely Online state is blocked");
                Set(main,"_licenseOk",false); Check(reason()==null,"License-disabled red Offline uses same effective-state policy");
                Set(main,"_licenseOk",true); Set(main,"_runOnline",false); Check(reason()==null,"Operator-selected Offline permits SDK tests");
                Set(main,"_cameraRecovering",1); Check(reason().Contains("recovery"),"Recovery gets its own reason"); Set(main,"_cameraRecovering",0);
                Set(main,"_switchingOnline",true); Check(reason().Contains("switching"),"Online switching remains protected"); Set(main,"_switchingOnline",false);
                Set(main,"_cameraRecoveryStopping",true); Check(reason().Contains("stopping"),"Shutdown remains protected"); Set(main,"_cameraRecoveryStopping",false);
                var gates=(SemaphoreSlim[])type.GetField("_chGates",Flags).GetValue(null);
                gates[2].Wait(); try { Check(reason().Contains("CH2"),"Busy reason reports channel number"); } finally { gates[2].Release(); }
                using(var lease=(IDisposable)Call(main,"AcquireSdkTestGates")) Check(gates.All(g=>g.CurrentCount==0),"SDK operation holds gates until completion");
                Check(gates.All(g=>g.CurrentCount==1),"Completion returns every gate");
                gates[2].Wait();
                try { try { Call(main,"AcquireSdkTestGates"); throw new Exception("Busy acquisition unexpectedly allowed"); } catch(TargetInvocationException ex) { Check(ex.InnerException.Message.Contains("CH2"),"Lease blocks busy channel"); } }
                finally { gates[2].Release(); }
                Check(gates.All(g=>g.CurrentCount==1),"Rejected operation leaves no stale locks");
                var language=type.Assembly.GetType("Aron_V2.UiLanguage"); language.GetMethod("SetLanguage",Flags).Invoke(null,new object[]{"zh-CN"});
                Set(main,"_cameraRecovering",1); Check(reason().Contains("恢复相机"),"Specific reason follows Chinese language"); Set(main,"_cameraRecovering",0);
                Check(fake.Calls==0,"Tests and guards send no PLC signals and do not start/stop transports");
            }
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}

using Aron_V2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
class AcquisitionCompletionTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    sealed class Designer:LicenseContext { public override LicenseUsageMode UsageMode {get{return LicenseUsageMode.Designtime;}} }
    sealed class Fake:IVisionCommunicationService,IVisionAcquisitionNotifications
    {
        internal readonly List<int> Completed=new List<int>();
        public bool IsStarted {get{return true;}}
        public event Action<CommunicationChannelInput> InputChanged {add{}remove{}}
        public event Action<int> TriggerRequested {add{}remove{}}
        public event Action<int> ResultAckReceived {add{}remove{}}
        public event Action<string> LogMessage {add{}remove{}}
        public void Start(){throw new Exception("Transport startup forbidden");}public void Stop(){}public void Dispose(){}
        public void SetOnline(bool v){}public void SetCameraReady(bool v){}
        public void NotifyInspectionStart(int c){}public void NotifyInspectionEnd(int c){throw new Exception("No analysis completion expected");}
        public void NotifyAcquisitionComplete(int c){lock(Completed)Completed.Add(c);}
        public void WriteJobPosEcho(int c,byte j,byte p){}public void WriteInspectionResult(int c,float x,float y,float a,short r){throw new Exception("No result may be sent yet");}
        public void ClearInspectionResult(int c){}public void ClearAllInspectionResult(int c){}public void ClearExposureComplete(int c){}
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static void Set(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
    static object Call(object o,string n,params object[] v){return o.GetType().GetMethod(n,Flags).Invoke(o,v);}
    static CameraConfig Cam(string name,string main,string second,string channel)
    {return new CameraConfig {Name=name,Positions=new List<PositionConfig>{new PositionConfig{Name="Pos1",General=new PositionGeneralConfig {MainUsed=main,MainChannel=channel,SecondUsed=second,SecondChannel=channel}}}};}
    static void Config(params CameraConfig[] cameras){Global.CurrentConfig=new AppConfig {Models=new List<ModelConfig>{new ModelConfig {Name="Job1",Cameras=new List<CameraConfig>(cameras)}}};}
    [STAThread]static int Main()
    {
        try
        {
            LicenseManager.CurrentContext=new Designer();
            Global.Manual_Trigger_Lock=new bool[64];
            using(var main=new FormMain())
            {
                var fake=new Fake();Set(main,"_communicationService",fake);Set(main,"_cameraValidationComplete",true);Set(main,"_runOnline",true);Set(main,"_licenseOk",true);
                Config(Cam("Cam1","0","1","0"),Cam("Cam2","0","1","0"),Cam("Cam3","1","0","0"));
                var cycle=Call(main,"BeginAcquisitionCycle","Job1","Pos1",0);
                Call(cycle,"Acquired","Second|Cam1");Call(cycle,"Acquired","Second|Cam2");Check(fake.Completed.Count==0,"Multiple auxiliary cameras do not assert Exposure Complete before main capture");
                Call(cycle,"Acquired","Main|Cam3");Check(fake.Completed.Count==1&&fake.Completed[0]==0,"Main acquisition asserts Exposure Complete immediately, without inspection/result notification");
                Call(cycle,"Acquired","Main|Cam3");Check(fake.Completed.Count==1,"Completion is sent once per cycle");
                fake.Completed.Clear();Config(Cam("Cam1","0","1","0"),Cam("Cam2","1","0","0"),Cam("Cam3","1","0","0"));
                cycle=Call(main,"BeginAcquisitionCycle","Job1","Pos1",0);Call(cycle,"Acquired","Main|Cam2");Check(fake.Completed.Count==0,"Two main cameras wait for both images");
                var concurrentCycle=cycle;Task.WaitAll(Task.Run(()=>Call(concurrentCycle,"Acquired","Main|Cam3")),Task.Run(()=>Call(concurrentCycle,"Acquired","Main|Cam3")));Check(fake.Completed.Count==1,"Concurrent capture completions cannot duplicate the notification");
                fake.Completed.Clear();cycle=Call(main,"BeginAcquisitionCycle","Job1","Pos1",0);Call(cycle,"Failed");Call(cycle,"Acquired","Main|Cam2");Call(cycle,"Acquired","Main|Cam3");Check(fake.Completed.Count==0,"Failed capture cannot report successful channel exposure");
                cycle=Call(main,"BeginAcquisitionCycle","Job1","Pos1",0);Set(main,"_runOnline",false);Call(cycle,"Acquired","Main|Cam2");Call(cycle,"Acquired","Main|Cam3");Check(fake.Completed.Count==0,"Switching Offline before capture finishes suppresses stale completion");Set(main,"_runOnline",true);
                Global.Manual_Trigger_Lock[0]=true;Check(Call(main,"BeginAcquisitionCycle","Job1","Pos1",0)==null,"Image-panel local manual trigger never sends PLC exposure");Global.Manual_Trigger_Lock[0]=false;
                var local=(AsyncLocal<bool>)typeof(FormMain).GetField("_manualLocalOnly",Flags).GetValue(null);local.Value=true;Check(Call(main,"BeginAcquisitionCycle","Job1","Pos1",0)==null,"Manual no-PLC simulation sends no exposure");local.Value=false;
                Config(Cam("Cam1","1","0","0"),Cam("Cam2","1","0","1"));var a=Call(main,"BeginAcquisitionCycle","Job1","Pos1",0);var b=Call(main,"BeginAcquisitionCycle","Job1","Pos1",1);
                Call(b,"Acquired","Main|Cam2");Call(a,"Acquired","Main|Cam1");Check(fake.Completed.Count==2&&fake.Completed[0]==1&&fake.Completed[1]==0,"Channels maintain independent acquisition completion");
                Set(main,"_communicationService",null);
            }
            Console.WriteLine("No camera SDK, VPP execution or PLC connection used.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}

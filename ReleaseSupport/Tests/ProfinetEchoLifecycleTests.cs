using Aron_V2;
using Aron_V2.Profinet;
using Cognex.VisionPro.Comm;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
class ProfinetEchoLifecycleTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    sealed class Designer:LicenseContext { public override LicenseUsageMode UsageMode {get{return LicenseUsageMode.Designtime;}} }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    [STAThread]static int Main()
    {
        try
        {
            var app=typeof(FormMain).Assembly;object[] report={null};Check((bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start",Flags).Invoke(null,report),"Pinned runtime for packet-only tests");
            var config=(ProfinetCommConfig)typeof(ProfinetCommConfigHelper).GetMethod("CreateDefault",Flags).Invoke(null,null);config.OutputPoints.Add(new ProfinetOutputPoint {Channel=0,Parameter="Width",VppOutput="Width",DataType="Float",Length=4,Address=8});
            ProfinetCommConfigHelper.Save(config);Global.Result_Send[8]=99;Global.Result_Send[1]=77;
            var frame=(CogNdmInspectionResult)app.GetType("Aron_V2.CC24_Comm").GetMethod("CreateJobPosEchoFrame",Flags).Invoke(null,new object[]{0,(byte)3,(byte)2});
            Check(frame.ResultData[0]==3&&frame.ResultData[4]==2,"Immediate Job/Pos frame carries selected channel feedback before any acquisition");
            Check(frame.ResultData[8]==0&&frame.ResultData[1]==77,"Metadata echo clears this channel's previous measurements, preserves other channels");
            Check(frame.InspectionIndex==0&&frame.ResultDataOffset==0&&frame.UsedAcquisitionIDs.Count==1,"Metadata frame contains required NDM ID entry; no acquisition notification is executed");
            var otherFrame=(CogNdmInspectionResult)app.GetType("Aron_V2.CC24_Comm").GetMethod("CreateJobPosEchoFrame",Flags).Invoke(null,new object[]{2,(byte)3,(byte)2});
            Check(otherFrame.UsedAcquisitionIDs[0].CameraIndex==2&&otherFrame.UsedAcquisitionIDs[0].AcquisitionID==0,"Acquisition ID defaults to zero; camera index follows channel");
            config.OutputPoints.Find(p=>p.Channel==0&&p.Parameter=="JobID").Address=20;
            config.OutputPoints.Find(p=>p.Channel==0&&p.Parameter=="PosID").Address=21;ProfinetCommConfigHelper.Save(config);
            frame=(CogNdmInspectionResult)app.GetType("Aron_V2.CC24_Comm").GetMethod("CreateJobPosEchoFrame",Flags).Invoke(null,new object[]{0,(byte)4,(byte)5});
            Check(frame.ResultData[20]==4&&frame.ResultData[21]==5,"Saved output address changes apply to next echo without transport restart");
            Parallel.For(0,40,i=>{if(i%2==0)ProfinetCommConfigHelper.Save(config);else Check(ProfinetCommConfigHelper.Load().OutputPoints.Exists(p=>p.Channel==0&&p.Parameter=="JobID"&&p.Address==20),"Concurrent mapping read retained saved address");});
            LicenseManager.CurrentContext=new Designer();using(var main=new FormMain())
            {
                var service=new ProfinetVisionCommService();typeof(ProfinetVisionCommService).GetField("_started",Flags).SetValue(service,true);
                typeof(FormMain).GetField("_communicationService",Flags).SetValue(main,service);
                typeof(FormMain).GetMethod("StartProfinetCommunication",Flags).Invoke(main,null);
                Check(ReferenceEquals(typeof(FormMain).GetField("_communicationService",Flags).GetValue(main),service),"Same-protocol Save & Apply retains existing service, including when waiting for controller");
                // Prevent even Stop on this artificial service during disposal.
                typeof(FormMain).GetField("_communicationService",Flags).SetValue(main,null);
            }
            Console.WriteLine("No CC24 enumeration, NDM.Start or PLC connection executed.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}

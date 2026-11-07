using System;
using System.Collections.Generic;
using System.Reflection;
using Aron_V2;

// Isolated test transport: never creates a socket or contacts a PLC.
namespace HslCommunication {
 public class OperateResult { public bool IsSuccess=true; public string Message="test failure"; }
 public class OperateResult<T> : OperateResult { public T Content; }
}
namespace Aron_V2 {
 public static class CommunicationConfigHelper { public const int MaxScalableChannelCount=64; }
 public static class ProjectPaths { public static string Config(string path){return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Config",path);} }
 public enum S7PointDataType { Bool, Byte, Int16, Float, String }
 public class S7CommConfig { public S7BasicSetting Basic=new S7BasicSetting(); }
 public class S7BasicSetting {
  public bool Enable=true, HeartbeatEnable=false;
  public int ActiveChannelCount=2, PollIntervalMs=600000, HeartbeatIntervalMs=600000;
  public string InputStartAddress="DB100.0", OutputStartAddress="DB101.0";
 }
 public static class S7ConfigXmlHelper { public static S7CommConfig Load(){return new S7CommConfig();} }
 public class CommunicationChannelInput {
  public int Channel; public bool TriggerCamera, InspectionResultAcknowledge, ClearExposureComplete, SetUserData;
  public byte ClearResult, JobId, PosId; public string PartNo;
 }
 public class S7AddressTester : IDisposable {
  public static Dictionary<string,string> Values=new Dictionary<string,string>();
  public static string FailWrite, FailRead;
  public static List<string> Writes=new List<string>();
  public static int ConnectCount, DisposeCount;
  public S7AddressTester(S7BasicSetting b){}
  public HslCommunication.OperateResult Connect(){ConnectCount++; return new HslCommunication.OperateResult();}
  public HslCommunication.OperateResult WriteHeartbeatOnce(){return new HslCommunication.OperateResult();}
  public HslCommunication.OperateResult<string> ReadAsString(string a,S7PointDataType t,ushort l) {
   string v; Values.TryGetValue(a,out v);
   return new HslCommunication.OperateResult<string>{IsSuccess=a!=FailRead,Content=v??"false"};
  }
  public HslCommunication.OperateResult WriteByString(string a,S7PointDataType t,string v){
   Writes.Add(a+"="+v);
   if(a==FailWrite) return new HslCommunication.OperateResult{IsSuccess=false};
   Values[a]=v; return new HslCommunication.OperateResult();
  }
  public void Dispose(){DisposeCount++;}
 }
}
class RecoveryTests {
 static void Call(object o,string name){typeof(S7VisionCommService).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);}
 static void Set(object o,string name,object v){typeof(S7VisionCommService).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);}
 static void Expect(bool ok,string name){if(!ok)throw new Exception(name); Console.WriteLine("PASS "+name);}
 static string V(string a){return S7AddressTester.Values[a];}
 static void Main(){
  var s=new S7VisionCommService();
  Set(s,"_config",new S7CommConfig()); Set(s,"_tester",new S7AddressTester(null)); Set(s,"_started",true);
  Call(s,"LoadDefaultInputMap"); Call(s,"LoadDefaultOutputMap");
  var v=S7AddressTester.Values; v["DB100.0.0"]="true"; v["DB100.16.0"]="true";
  int triggers=0; s.TriggerRequested+=c=>triggers++;
  Call(s,"InitializeOutputsAfterConnected");
  Expect(V("DB101.0.7")=="true" && V("DB101.0.0")=="true","Online and Ready published");
  foreach(int ch in new[]{0,1}) {
   int b=16+ch*8, r=56+ch*14;
   Expect(V("DB101."+b+".1")=="false" && V("DB101."+b+".3")=="false" && V("DB101."+b+".5")=="false" && V("DB101."+(b+1)+".0")=="false","CH"+ch+" handshakes cleared");
   Expect(V("DB101."+r)=="0" && V("DB101."+(r+4))=="0" && V("DB101."+(r+8))=="0" && V("DB101."+(r+12))=="0","CH"+ch+" results cleared");
  }
  Call(s,"PollConnected"); Expect(triggers==0,"stale high trigger not replayed");
  int connectionsBeforeToggle=S7AddressTester.ConnectCount, disposalsBeforeToggle=S7AddressTester.DisposeCount;
  s.SetOnline(false);
  Expect(s.IsStarted && V("DB101.0.7")=="false" && V("DB101.16.0")=="false","operator Offline retains connection and clears Online/Ready");
  Set(s,"_lastOnlinePublish",DateTime.MinValue); Call(s,"PollConnected");
  Expect(V("DB101.0.7")=="false" && triggers==0,"offline polling cannot restore Online or trigger inspection");
  v["DB100.0.0"]="false"; s.SetOnline(true);
  Expect(V("DB101.0.7")=="true" && V("DB101.16.0")=="false","Online initializes with Ready false when PLC Trigger Enable is false");
  v["DB100.0.0"]="true"; Call(s,"PollConnected");
  Expect(V("DB101.16.0")=="true" && triggers==0,"PLC Trigger Enable asserts Ready without replaying a held trigger");
  Expect(S7AddressTester.ConnectCount==connectionsBeforeToggle && S7AddressTester.DisposeCount==disposalsBeforeToggle,"Offline/Online switches neither reconnect nor dispose PLC transport");
  v["DB100.16.0"]="false"; Call(s,"PollConnected");
  v["DB100.16.0"]="true"; Call(s,"PollConnected"); Expect(triggers==1,"fresh trigger accepted");
  s.SetCameraReady(false);
  s.NotifyInspectionEnd(0); s.WriteInspectionResult(0,1,2,3,1);
  Expect(!s.IsStarted && V("DB101.0.7")=="false" && V("DB101.0.0")=="false" && V("DB101.16.0")=="false" && V("DB101.16.5")=="false","camera offline disconnects S7 and clears Online/Ready");
  Expect(S7AddressTester.DisposeCount==1,"camera offline disposes connection");
  v["DB101.0.7"]="false"; Set(s,"_lastOnlinePublish",DateTime.MinValue); Call(s,"PollConnected");
  Expect(V("DB101.0.7")=="false","offline polling cannot reassert Online");
  s.Start(); Call(s,"ReconnectCore");
  Expect(!s.IsStarted && S7AddressTester.ConnectCount==0,"camera offline blocks start and queued reconnect");
  s.SetCameraReady(true);
  Expect(s.IsStarted && S7AddressTester.ConnectCount==1 && V("DB101.0.7")=="true" && V("DB101.0.0")=="true" && V("DB101.16.3")=="false","camera recovery reconnects and initializes again");
  S7AddressTester.FailWrite="DB101.56";
  bool failed=false; try {Call(s,"InitializeOutputsAfterConnected");}catch(TargetInvocationException){failed=true;}
  Expect(failed && V("DB101.0.0")=="false","failed initialization does not assert Ready");
  S7AddressTester.FailWrite=null; S7AddressTester.FailRead="DB100.16.0";
  failed=false; try{Call(s,"InitializeOutputsAfterConnected");}catch(TargetInvocationException){failed=true;}
  Expect(failed && V("DB101.0.0")=="false","failed input snapshot blocks Ready");
  S7AddressTester.FailRead=null; Call(s,"InitializeOutputsAfterConnected");
  Expect(V("DB101.0.0")=="true","retry initialization succeeds");
  s.SetCameraReady(false); Call(s,"ReconnectCore");
  Expect(!s.IsStarted && V("DB101.0.7")=="false" && V("DB101.0.0")=="false","second camera loss blocks PLC reconnect");
  s.SetCameraReady(true);
  s.InputChanged+=input=>{
   var worker=new System.Threading.Thread(()=>s.ClearExposureComplete(input.Channel));
   worker.IsBackground=true; worker.Start();
   Expect(worker.Join(1000),"application callback does not hold service write lock");
  };
  v["DB100.16.3"]="true"; Call(s,"PollConnected");
  s.Stop(); Expect(V("DB101.0.7")=="false","shutdown clears Online");
 }
}


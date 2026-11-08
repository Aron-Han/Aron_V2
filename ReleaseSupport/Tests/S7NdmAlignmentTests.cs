using System;
using System.Collections.Generic;
using System.Reflection;
using Aron_V2;

class S7NdmAlignmentTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object service,string key,object value){service.GetType().GetField(key,Flags).SetValue(service,value);}
    static object Call(object service,string key,params object[] args){return service.GetType().GetMethod(key,Flags).Invoke(service,args);}
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static string Value(string address){string value;return S7AddressTester.Values.TryGetValue(address,out value)?value:"false";}
    static S7VisionCommService Service()
    {
        S7AddressTester.Values.Clear();S7AddressTester.Writes.Clear();S7AddressTester.FailRead=null;S7AddressTester.FailWrite=null;
        var s=new S7VisionCommService();Set(s,"_config",new S7CommConfig());Set(s,"_tester",new S7AddressTester(null));
        Set(s,"_started",true);Set(s,"_onlineRequested",true);Set(s,"_cameraReady",true);Call(s,"LoadAddressMaps");return s;
    }
    static void Main()
    {
        var s=Service();
        S7AddressTester.Values["DB100.0.0"]="true";Call(s,"PollConnected");
        s.NotifyInspectionStart(0);Check(Value("DB101.16.0")=="false","Acquisition start clears Ready");
        s.NotifyAcquisitionComplete(0);Check(Value("DB101.16.0")=="true","Ready restored before inspection completion");
        s.NotifyInspectionStart(0);s.NotifyInspectionEnd(0);Check(Value("DB101.16.0")=="false","An inspection end cannot rearm an unfinished acquisition");
        S7AddressTester.Values["DB100.0.0"]="false";s.NotifyAcquisitionComplete(0);Check(Value("DB101.16.0")=="false","Latest PLC Trigger Enable low prevents Ready");
        s.NotifyInspectionStart(0);S7AddressTester.Values["DB100.0.0"]="true";Call(s,"PollConnected");Check(Value("DB101.16.0")=="false","Enable rising during acquisition does not prematurely rearm");
        S7AddressTester.Values["DB100.16.2"]="true";s.NotifyAcquisitionComplete(0);
        Check(Value("DB101.16.0")=="true"&&Value("DB101.16.3")=="false","Clear Exposure high suppresses Exposure but not acquisition Ready");
        Set(s,"_onlineRequested",false);S7AddressTester.Writes.Clear();s.NotifyAcquisitionComplete(0);Check(S7AddressTester.Writes.Count==0,"Offline completion performs no PLC writes");

        s=Service();S7AddressTester.Values["DB101.24.3"]="true";S7AddressTester.Values["DB101.49"]="8";
        s.WriteJobPosEcho(0,3,7);
        Check(Value("DB101.48")=="3"&&Value("DB101.52")=="7","Job/Pos echo publishes without camera or VPP execution");
        Check(Value("DB101.16.5")=="true"&&Value("DB101.16.4")=="True","Echo asserts Result Valid and toggles Inspection Complete");
        int valid=S7AddressTester.Writes.IndexOf("DB101.16.5=true"),complete=S7AddressTester.Writes.FindIndex(x=>x.StartsWith("DB101.16.4="));
        Check(valid>S7AddressTester.Writes.IndexOf("DB101.52=7")&&complete>valid,"Payload precedes Valid, then Complete");
        Check(Value("DB101.49")=="8"&&Value("DB101.24.3")=="true","Echo preserves other channel metadata and acquisition signals");
        Check(!S7AddressTester.Writes.Exists(x=>x.StartsWith("DB101.16.0=")||x.StartsWith("DB101.16.3=")),"Metadata echo does not fake camera Ready/Exposure");
        S7AddressTester.Values["DB100.16.1"]="true";Call(s,"PollConnected");Check(Value("DB101.16.5")=="false","PLC Ack clears echo Result Valid");
        S7AddressTester.Values["DB100.16.1"]="false";s.WriteJobPosEcho(0,4,9);Check(Value("DB101.16.4")=="False","Subsequent echo toggles Complete once again");

        s=Service();S7AddressTester.FailWrite="DB101.52";
        try{s.WriteJobPosEcho(0,2,5);throw new Exception("Expected failed echo");}catch(System.IO.IOException){}
        Check(!S7AddressTester.Writes.Contains("DB101.16.5=true")&&!S7AddressTester.Writes.Exists(x=>x.StartsWith("DB101.16.4=")),"Failed echo payload never asserts Valid or toggles Complete");

        s=Service();S7AddressTester.Values["DB100.0.1"]="true";Call(s,"PollConnected");
        s.WriteJobPosEcho(0,1,1);s.WriteJobPosEcho(0,2,2);
        Check(Value("DB101.48")=="1","Buffered echo retains published payload until PLC Ack");
        S7AddressTester.Values["DB100.16.1"]="true";Call(s,"PollConnected");Check(Value("DB101.16.5")=="false","Buffered Ack clears Valid");
        S7AddressTester.Values["DB100.16.1"]="false";Call(s,"PollConnected");
        Check(Value("DB101.48")=="2"&&Value("DB101.52")=="2"&&Value("DB101.16.5")=="true","Ack falling publishes the queued echo snapshot");
        Check(Value("DB101.16.4")=="False","Publishing queued frame does not double-toggle Complete");
        S7AddressTester.Values["DB100.16.1"]="true";Call(s,"PollConnected");
        S7AddressTester.Values["DB100.16.1"]="false";Call(s,"PollConnected");
        Check(s.WriteInspectionResult(0,1.5F,2.5F,3.5F,1,"Job2","Pos2",null),"Normal inspection uses the same frame handshake after echo");
        Check(Value("DB101.56")=="1.5"&&Value("DB101.16.5")=="true"&&Value("DB101.16.4")=="True","Detection payload still publishes correctly and toggles Complete once");
        s.NotifyInspectionEnd(0);Check(Value("DB101.16.4")=="True","Inspection end does not duplicate completion");
        Console.WriteLine("No physical PLC/camera or production configuration used.");
    }
}

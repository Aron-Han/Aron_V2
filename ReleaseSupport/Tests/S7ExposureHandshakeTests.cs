using System;
using System.Collections.Generic;
using System.Reflection;
using Aron_V2;

// Editor-only dependency; the service/transport handshake below uses production code.
namespace Aron_V2.Profinet
{
    static class ProfinetCommConfigHelper
    {
        public static int GetFixedLength(string type){return type=="Float"||type=="Int32"?4:type=="Double"?8:type=="Int16"?2:1;}
    }
}

class S7ExposureHandshakeTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object service,string key,object value){service.GetType().GetField(key,Flags).SetValue(service,value);}
    static void Call(object service,string key){service.GetType().GetMethod(key,Flags).Invoke(service,null);}
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static bool Complete(int channel){string value;return S7AddressTester.Values.TryGetValue("DB101."+(16+8*channel)+".3",out value)&&value=="true";}
    static void Main()
    {
        var service=new S7VisionCommService();
        Set(service,"_config",new S7CommConfig());Set(service,"_tester",new S7AddressTester(null));
        Set(service,"_started",true);Set(service,"_onlineRequested",true);Set(service,"_cameraReady",true);
        Call(service,"LoadAddressMaps");
        S7AddressTester.Values["DB100.16.2"]="false";
        service.NotifyAcquisitionComplete(0);Check(Complete(0),"Acquisition asserts completion when PLC Clear is low");
        service.NotifyInspectionStart(0);Check(Complete(0),"Next inspection start retains completion");
        service.NotifyInspectionEnd(0);Check(Complete(0),"Inspection end retains completion");
        service.ClearAllInspectionResult(0);Check(Complete(0),"Clear Result does not clear acquisition completion");
        S7AddressTester.Values["DB100.16.2"]="true";
        Call(service,"PollConnected");Check(!Complete(0),"PLC Clear high clears completion during polling");
        service.NotifyAcquisitionComplete(0);Check(!Complete(0),"Clear held high suppresses acquisition completion");
        Call(service,"PollConnected");service.NotifyAcquisitionComplete(0);Check(!Complete(0),"Repeated completion remains reset while Clear stays high");
        S7AddressTester.Values["DB100.16.2"]="false";
        Call(service,"PollConnected");Check(!Complete(0),"Clear falling does not replay the suppressed completion");
        service.NotifyAcquisitionComplete(0);Check(Complete(0),"Only next acquisition reasserts after Clear falls");
        S7AddressTester.Values["DB100.17.2"]="false";
        service.NotifyAcquisitionComplete(1);Check(Complete(1),"Second channel completion is independent");
        S7AddressTester.Values["DB100.16.2"]="true";Call(service,"PollConnected");
        Check(!Complete(0)&&Complete(1),"Clearing channel zero preserves channel one");
        S7AddressTester.FailRead="DB100.16.2";S7AddressTester.Writes.Clear();
        service.NotifyAcquisitionComplete(0);
        Check(!S7AddressTester.Writes.Contains("DB101.16.3=true"),"Failed Clear read never permits completion assertion");
        Console.WriteLine("No camera or PLC connection was started.");
    }
}

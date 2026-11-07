using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Aron_V2;

class S7CustomOutputTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string name, object value) { obj.GetType().GetField(name, Flags).SetValue(obj, value); }
    static void Call(object obj, string name) { obj.GetType().GetMethod(name, Flags).Invoke(obj, null); }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static string Valid => S7AddressTester.Values["DB101.16.5"];
    static S7PointItem Point(string name, string address, string type, ushort length, string pin)
    { return new S7PointItem { Channel = 0, Parameter = name, DbAddress = address, Format = type, Length = length,
        VppOutput = pin }; }
    static void Main()
    {
        var service = new S7VisionCommService();
        Set(service, "_config", new S7CommConfig()); Set(service, "_tester", new S7AddressTester(null)); Set(service, "_started", true);
        Call(service, "LoadAddressMaps");
        var map = (Dictionary<string, S7PointItem>)typeof(S7VisionCommService).GetField("_outputMap", Flags).GetValue(service);
        Func<bool> legacy = () => service.WriteInspectionResult(0, 1.25f, 2.5f, 3.75f, 1, "Job1", "Pos1", new List<S7InspectionOutputs>());
        S7AddressTester.Writes.Clear(); Check(legacy() && Valid == "true", "Existing default X/Y/A and summary publish successfully");
        Check(S7AddressTester.Values["DB101.56"] == "1.25" && S7AddressTester.Values["DB101.68"] == "1", "Existing addresses and values preserved");
        S7AddressTester.FailWrite = "DB101.60"; S7AddressTester.Writes.Clear();
        Check(!legacy() && Valid == "false" && !S7AddressTester.Writes.Contains("DB101.16.5=true"), "Legacy result write failure never asserts Result Valid");
        S7AddressTester.FailWrite = null;
        map["0|Width"] = Point("Width", "DB101.100", "Float", 4, "WidthPin");
        map["0|Count"] = Point("Count", "DB101.104", "Int16", 2, "CountPin");
        map["0|Text"] = Point("Text", "DB101.106", "String", 6, "TextPin");
        var source = new S7InspectionOutputs { Job = "Job1", Camera = "Cam1", Position = "Pos1", Succeeded = true };
        source.Values["WidthPin"] = 12.5; source.Values["CountPin"] = 4; source.Values["TextPin"] = "OK";
        var sources = new List<S7InspectionOutputs> { source };
        Func<bool> send = () => service.WriteInspectionResult(0, 1, 2, 3, 1, "Job1", "Pos1", sources);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        S7AddressTester.Writes.Clear(); Check(send(), "Custom VPP numeric/string values publish under comma-decimal Windows culture");
        Check(S7AddressTester.Writes.Last() == "DB101.16.5=true", "Result Valid is asserted only after every data write");
        Check(S7AddressTester.Values["DB101.100"] == "12.5" && S7AddressTester.Values["DB101.106"] == "OK\0\0\0\0", "Numeric conversion is invariant and string tail is cleared");
        source.Values.Remove("WidthPin"); S7AddressTester.Writes.Clear();
        Check(!send() && Valid == "false" && !S7AddressTester.Writes.Any(x => x.StartsWith("DB101.56=")), "Missing pin rejects the batch before any data write");
        source.Values["WidthPin"] = double.NaN;
        Check(!send() && Valid == "false", "Non-finite floating point value blocks publication");
        source.Values["WidthPin"] = 12.5; source.Values["CountPin"] = 40000;
        Check(!send() && Valid == "false", "Int16 overflow blocks publication");
        source.Values["CountPin"] = 4; source.Values["TextPin"] = "TOO LONG";
        Check(!send() && Valid == "false", "String length overflow blocks publication");
        source.Values["TextPin"] = "OK"; source.Succeeded = false;
        Check(!send() && Valid == "false", "Failed ToolBlock source cannot publish stale outputs");
        source.Succeeded = true; S7AddressTester.FailWrite = "DB101.104"; S7AddressTester.Writes.Clear();
        Check(!send() && Valid == "false" && !S7AddressTester.Writes.Contains("DB101.16.5=true"), "Custom address write failure keeps Result Valid false");
        S7AddressTester.FailWrite = null; sources.Add(source);
        Check(send(), "Shared-channel pin uses the original summary source priority"); sources.RemoveAt(1);
        Check(!service.WriteInspectionResult(0, 1, 2, 3, 1, null, null, null) && Valid == "false", "Missing current cycle context cannot silently skip bound outputs");
        S7AddressTester.Writes.Clear();
        Check(!service.WriteInspectionResult(0, 1, 2, 3, 1, "Job1", "Pos2", sources), "A prior-position snapshot cannot satisfy the current-cycle binding");
        source.Job = "Job2"; source.Position = "Pos2";
        Check(service.WriteInspectionResult(0, 1, 2, 3, 1, "Job2", "Pos2", sources), "The same pin-name binding works for another Job/Position without reconfiguration");
        source.Job = "Job1"; source.Position = "Pos1";
        map.Remove("0|Result X"); map.Remove("0|Result Y"); map.Remove("0|Result A"); map.Remove("0|Result0");
        Check(send() && Valid == "true", "Custom-only data output tables do not require the legacy four data rows");
        string table = ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(table));
        var xml = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("S7OutputSignalTableConfig",
            new System.Xml.Linq.XElement("Rows", map.Values.Select(p => new System.Xml.Linq.XElement("S7OutputSignalTableRow",
                new System.Xml.Linq.XElement("Channel", p.Channel), new System.Xml.Linq.XElement("Parameter", p.Parameter),
                new System.Xml.Linq.XElement("DbAddress", p.DbAddress), new System.Xml.Linq.XElement("Format", p.Format), new System.Xml.Linq.XElement("Length", p.Length),
                new System.Xml.Linq.XElement("VppOutput", p.VppOutput))))));
        xml.Save(table); Call(service, "LoadAddressMaps");
        Check(map["0|Width"].VppOutput == "WidthPin", "Runtime loads the VPP pin name from the saved point-table XML");
        Check(!map.ContainsKey("0|Result0") && send(), "Reload does not fabricate a deleted legacy summary row");
        S7AddressTester.FailWrite = "DB101.16.5"; S7AddressTester.Writes.Clear();
        Check(!send() && S7AddressTester.Writes.Count == 1, "Failure to clear Result Valid aborts before any payload write");
        S7AddressTester.FailWrite = null;
        Check(S7OutputData.IsFixed("Result Valid") && !S7OutputData.IsFixed("Result X"), "Handshake rows remain fixed; result data rows are configurable");
        Console.WriteLine("PASS No physical camera, PLC, or production configuration was used.");
    }
}

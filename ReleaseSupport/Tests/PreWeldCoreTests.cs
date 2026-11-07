using Aron.PluginContracts;
using Aron_V2;
using Aron_V2.CellInspection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

internal static class PreWeldCoreTests
{
    private sealed class Host : IPluginHost
    {
        public string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Project", "Config", "Plugins", "PreWeld");
        public string DataDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Plugins", "PreWeld");
        public string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Plugins", "PreWeld");
        public bool CanEdit => true; public string Language => "en";
        public IList<CameraPosition> GetCatalog() => new List<CameraPosition>();
        public void Log(string level, string message) { }
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static CellCaptureRecord Capture(string camera, int order, string text)
    {
        List<CellMeasuredPoint> points; CellInspectionService.TryParseCoordinatePayload(text, out points);
        return new CellCaptureRecord { Job = "Job1", PartNumber = "P", Camera = camera, PlcPosition = "Pos1", Channel = order, CaptureOrder = order, Points = points, CaptureTime = DateTime.Now.ToString("o") };
    }
    static int Main()
    {
        try
        {
            using (var plugin = new PreWeldPlugin())
            {
                plugin.Initialize(new Host());
                List<CellMeasuredPoint> parsed;
                CellInspectionService.TryParseCoordinatePayload("1,1,2;2,NaN,3;3,Infinity,4;", out parsed);
                Check(parsed.Count == 1, "Non-finite coordinates cannot enter the spatial grouping algorithm");
                var captures = new List<CellCaptureRecord> { Capture("Cam2", 1, "1,10,10;2,20,10;"), Capture("Cam1", 2, "1,10.1,10;2,20.1,10;"), Capture("Cam3", 3, "1,10,10,3;2,20,10,4;") };
                var settings = new CellInspectionSettings { RequireHeightData = true };
                var original = CellInspectionService.BuildDocument("Job1", "P", captures, settings);
                Check(original.FinalCellCount == 2 && original.MatchedHeightCount == 2, "2D duplicates and separate 3D heights merge into two final cells");
                Check(original.Cells.All(c => c.SelectedSource.Camera == "Cam1"), "Legacy Cam1 preference and source trace preserved");
                Check(original.Cells.First().Sequence == 1 && double.Parse(original.Cells.First().X, CultureInfo.InvariantCulture) > double.Parse(original.Cells.Last().X, CultureInfo.InvariantCulture), "Legacy right-to-left row ordering preserved");
                CellInspectionService.SaveCompensations("Job1", new[] { new CellCompensationEntry { Sequence = 1, OffsetX = 1.25, OffsetY = -2.5, OffsetZ = 0.75 } });
                var compensated = CellInspectionService.BuildDocument("Job1", "P", captures, settings);
                Check(Math.Abs(double.Parse(compensated.Cells[0].X, CultureInfo.InvariantCulture) - double.Parse(original.Cells[0].X, CultureInfo.InvariantCulture) - 1.25) < 0.00001, "Job/sequence compensation applied without changing raw coordinates");
                var otherJob = CellInspectionService.BuildDocument("Job2", "P", captures, settings);
                Check(otherJob.Cells[0].OffsetX == 0, "Compensation stays isolated by Job");
                var missing = captures.Take(2).ToList(); bool blocked = false;
                try { CellInspectionService.BuildDocument("Job1", "P", missing, settings); } catch (InvalidOperationException) { blocked = true; }
                Check(blocked, "Required missing heights block a misleading complete summary");
                List<MarkMeasuredPoint> marks; CellInspectionService.TryParseMarkPayload("Mark1,1,2,3;", out marks);
                captures[0].Marks = marks; captures[1].Marks = marks; blocked = false;
                try { CellInspectionService.BuildDocument("Job1", "P", captures, settings); } catch (InvalidOperationException) { blocked = true; }
                Check(blocked, "Duplicate Mark names still block the summary");
                string compensationPath = CellInspectionService.CompensationPath; File.WriteAllText(compensationPath, "<invalid"); blocked = false;
                try { CellInspectionService.SaveCompensations("Job1", new CellCompensationEntry[0]); } catch (InvalidDataException) { blocked = true; }
                Check(blocked && File.ReadAllText(compensationPath) == "<invalid", "Invalid existing compensation is preserved instead of silently overwritten");
            }
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Cognex")), "Math/XML validation uses no VisionPro or device transport");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

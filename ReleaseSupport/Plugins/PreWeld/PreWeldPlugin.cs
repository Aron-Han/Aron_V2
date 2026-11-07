using Aron.PluginContracts;
using Aron_V2.CellInspection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]

namespace Aron_V2
{
    internal static class PreWeldEnvironment { internal static IPluginHost Host; }
    public sealed class PreWeldPlugin : IAronPlugin
    {
        public string Id { get { return "PreWeld"; } }
        private IPluginHost _host;
        private readonly Dictionary<string, HashSet<string>> _completed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private string _eventSignature;
        public void Initialize(IPluginHost host)
        {
            _host = host; PreWeldEnvironment.Host = host;
            CellInspectionService.LoadFinalizeEvent(); // Fail closed on invalid module configuration.
        }
        public IEnumerable<PluginPage> Pages
        {
            get
            {
                yield return new PluginPage { Title = "Pre-weld setup", Create = () => new FormCellFinalizeEvent(_host) };
                yield return new PluginPage { Title = "Cell replay and compensation", Create = () => new FormCellDataReplay() };
            }
        }
        public void OnInspection(InspectionSnapshot snapshot)
        {
            if (snapshot.LocalOnly) return;
            CellFinalizeEventSettings settings = CellInspectionService.LoadFinalizeEvent();
            string signature = settings.Enabled + "|" + settings.Job + "|" + settings.Position;
            if (_eventSignature != signature)
            {
                _completed.Clear(); CellInspectionService.ResetSessions(); _eventSignature = signature;
            }
            if (!settings.Enabled || !string.Equals(settings.Job, snapshot.Job, StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(snapshot.PartNumber)) { _host.Log("WARN", "Pre-weld collection skipped: product code is empty, CH=" + snapshot.Channel); return; }
            string key = snapshot.Job + "\u001f" + snapshot.PartNumber.Trim();
            HashSet<string> completed;
            if (!_completed.TryGetValue(key, out completed))
            {
                if (_completed.Count >= 256) throw new InvalidOperationException("Too many unfinished parts; check the configured summary position and product codes.");
                completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase); _completed.Add(key, completed);
            }
            bool final = string.Equals(settings.Position, snapshot.Position, StringComparison.OrdinalIgnoreCase);
            if (!snapshot.Successful) { if (final) completed.Remove(snapshot.Camera); _host.Log("WARN", "Pre-weld capture not accepted: " + snapshot.Job + "/" + snapshot.Camera + "/" + snapshot.Position); return; }
            string cellPayload = "", markPayload = ""; var cells = new List<CellMeasuredPoint>(); var marks = new List<MarkMeasuredPoint>();
            // Preserve the legacy preferred names, then any parseable String output.
            string[] preferred = { "CellCoordinates", "CellCoordinates3D", "CellXYZ", "CellHeights", "CellCoordinate", "CellData", "CellXY", "Cells" };
            foreach (string name in preferred.Concat(snapshot.Outputs.Keys).Distinct(StringComparer.Ordinal))
            {
                string text;
                if (snapshot.Outputs.TryGetValue(name, out text) && CellInspectionService.TryParseCoordinatePayload(text, out cells)) { cellPayload = text; break; }
            }
            string mark;
            if (snapshot.Outputs.TryGetValue("Mark", out mark) && !string.IsNullOrWhiteSpace(mark))
            {
                if (!CellInspectionService.TryParseMarkPayload(mark, out marks)) throw new InvalidOperationException("Invalid Mark output; expected Mark1,X,Y,Z;...");
                markPayload = mark;
            }
            if (cells.Count == 0 && marks.Count == 0)
            {
                if (final) completed.Remove(snapshot.Camera);
                _host.Log("WARN", "No Cell/Mark coordinates: " + snapshot.Camera + "/" + snapshot.Position); return;
            }
            string raw = CellInspectionService.RecordCapture(snapshot.PartNumber, snapshot.Job, snapshot.Camera, snapshot.Position,
                snapshot.Channel, snapshot.CaptureTime, snapshot.ImagePath ?? "", cellPayload, cells, markPayload, marks);
            _host.Log("INFO", "Pre-weld raw saved: " + raw);
            if (!final) return;
            var required = _host.GetCatalog().Where(p => p.Job == snapshot.Job && p.Position == settings.Position && (p.MainUsed || p.SecondUsed))
                .Select(p => p.Camera).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (required.Length == 0) throw new InvalidOperationException("Summary position has no enabled cameras.");
            completed.Add(snapshot.Camera);
            string[] missing = required.Where(p => !completed.Contains(p)).ToArray();
            if (missing.Length > 0) { _host.Log("INFO", "Pre-weld summary waiting: " + string.Join(", ", missing)); return; }
            string summary = CellInspectionService.FinalizePart(snapshot.Job, snapshot.PartNumber);
            _completed.Remove(key);
            _host.Log("INFO", "Pre-weld complete XML: " + summary + " (vision data only, no PLC send/ack)");
        }
        public void Dispose() { _completed.Clear(); CellInspectionService.ResetSessions(); }
    }
}

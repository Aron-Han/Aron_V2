using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyVersion("1.0.0.0")]
namespace Aron.PluginContracts
{
    // Versioned, vendor-neutral contract. Plugins never receive a live ToolBlock,
    // camera, host Form, mutable configuration, or a PLC transport.
    public interface IAronPlugin : IDisposable
    {
        string Id { get; }
        void Initialize(IPluginHost host);
        IEnumerable<PluginPage> Pages { get; }
        void OnInspection(InspectionSnapshot snapshot);
    }
    public interface IPluginHost
    {
        string ConfigDirectory { get; }
        string DataDirectory { get; }
        string LogDirectory { get; }
        bool CanEdit { get; }
        string Language { get; }
        IList<CameraPosition> GetCatalog();
        void Log(string level, string message);
    }
    public sealed class PluginPage
    {
        public string Title { get; set; }
        public Func<Form> Create { get; set; }
    }
    public sealed class CameraPosition
    {
        public string Job { get; set; }
        public string Camera { get; set; }
        public string Position { get; set; }
        public bool MainUsed { get; set; }
        public bool SecondUsed { get; set; }
    }
    public sealed class InspectionSnapshot
    {
        public string Id { get; private set; }
        public string Job { get; private set; }
        public string Camera { get; private set; }
        public string Position { get; private set; }
        public string PartNumber { get; private set; }
        public int Channel { get; private set; }
        public bool Successful { get; private set; }
        public bool LocalOnly { get; private set; }
        public DateTime CaptureTime { get; private set; }
        public string ImagePath { get; private set; }
        public IReadOnlyDictionary<string, string> Outputs { get; private set; }
        public InspectionSnapshot(string job, string camera, string position, string partNumber, int channel,
            bool successful, bool localOnly, DateTime time, IDictionary<string, string> outputs)
        {
            Id = Guid.NewGuid().ToString("N"); Job = job; Camera = camera; Position = position;
            PartNumber = partNumber; Channel = channel; Successful = successful; LocalOnly = localOnly; CaptureTime = time;
            Outputs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(outputs ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        }
        public InspectionSnapshot WithImagePath(string path)
        { var copy = (InspectionSnapshot)MemberwiseClone(); copy.ImagePath = path; return copy; }
    }
}

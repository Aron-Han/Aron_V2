using Cognex.VisionPro.ToolBlock;
using Cognex.VisionPro;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aron_V2
{
    public partial class Load_Job
    {
        internal static int[] InspectionEditChannels(string job, string cam, string pos, IEnumerable<int> configured)
        {
            lock (_jobSwitchLock)
                return configured.Concat(VppCache.Tb.Keys.Where(k => MatchesInspection(k, job, cam, pos)).Select(k => int.Parse(k.Split('|')[1].Substring(2)))).Distinct().ToArray();
        }
        private static bool MatchesInspection(string key, string job, string cam, string pos)
        {
            string[] parts = key.Split('|');
            return parts.Length == 4 && string.Equals(parts[0], job, StringComparison.OrdinalIgnoreCase) && string.Equals(parts[2], cam, StringComparison.OrdinalIgnoreCase) && string.Equals(parts[3], pos, StringComparison.OrdinalIgnoreCase);
        }
        internal sealed class InspectionEdit : IDisposable
        {
            private readonly Dictionary<string, CogToolBlock> _copies = new Dictionary<string, CogToolBlock>();
            private bool _committed;
            internal InspectionEdit(CogToolBlock draft, string job, string cam, string pos, IEnumerable<int> channels)
            {
                // Do not duplicate a possibly very large live 3D image into all
                // channel caches when an engineer saves algorithm parameters.
                try { foreach (int channel in channels) _copies.Add(VppCache.Key(job, channel, cam, pos), InspectionVppContract.CreateCopyWithoutTransientInputs(draft)); }
                catch { Dispose(); throw; }
            }
            internal void Commit()
            {
                lock (_jobSwitchLock)
                {
                    var previous = _copies.Keys.Where(k => VppCache.Tb.ContainsKey(k)).Select(k => VppCache.Tb[k]).Distinct().ToList();
                    foreach (var copy in _copies) VppCache.Tb[copy.Key] = copy.Value;
                    _committed = true;
                    foreach (var tool in previous) try { tool?.Dispose(); } catch { }
                }
            }
            public void Dispose() { if (!_committed) foreach (var copy in _copies.Values) try { copy.Dispose(); } catch { } }
        }
    }
}

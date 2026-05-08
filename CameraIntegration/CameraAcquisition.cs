using Cognex.VisionPro;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Aron_V2.CameraIntegration
{
    internal interface ISdkCameraDevice : IDisposable
    {
        // Keep the provider contract independent of VisionPro's native module;
        // the acquisition boundary validates/casts the returned image.
        object Capture(int timeoutMs);
        string Diagnose();
    }

    internal sealed class SdkCameraProvider
    {
        internal readonly string Id, Name, Category;
        internal readonly Func<CameraConfig, ISdkCameraDevice> Create;
        internal SdkCameraProvider(string id, string name, Func<CameraConfig, ISdkCameraDevice> create, string category = CameraAcquisition.Sdk3D)
        { Id = id; Name = name; Create = create; Category = category; }
        public override string ToString() { return Name; }
    }

    // Future vendors implement ISdkCameraDevice and add one provider here;
    // PLC handshakes and inspection execution do not need to know the brand.
    internal static class CameraAcquisition
    {
        internal const string Cognex = "Cognex", Lmi = "LmiSdk";
        internal const string Visionpro = "Visionpro", Sdk3D = "SDK-3D", Sdk2DLine = "SDK-2Dline", Sdk2D = "SDK-2D";
        internal static readonly SdkCameraProvider[] Providers =
        { new SdkCameraProvider(Lmi, "LMI Gocator / GoSDK", c => new LmiGoSdkCamera(c)) };
        private static readonly ConcurrentDictionary<string, DeviceSlot> Devices =
            new ConcurrentDictionary<string, DeviceSlot>(StringComparer.OrdinalIgnoreCase);
        private static readonly object SettingsLock = new object();

        private sealed class DeviceSlot
        {
            internal readonly object Gate = new object();
            internal ISdkCameraDevice Device;
            internal string Binding;
        }

        internal static CameraConfig Find(AppConfig config, string job, string cam)
        {
            return config?.Models?.FirstOrDefault(m => string.Equals(m?.Name, job, StringComparison.OrdinalIgnoreCase))?
                .Cameras?.FirstOrDefault(c => string.Equals(c?.Name, cam, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool IsSdk(CameraConfig config)
        { return config != null && !string.IsNullOrWhiteSpace(config.ConnectionMode) && !string.Equals(config.ConnectionMode, Cognex, StringComparison.OrdinalIgnoreCase); }

        internal static string CategoryOf(CameraConfig config)
        {
            if (!IsSdk(config)) return Visionpro;
            if (!string.IsNullOrWhiteSpace(config.SdkCategory)) return config.SdkCategory;
            // Compatibility with the first LMI migration and older LMI XML.
            return Providers.FirstOrDefault(p => string.Equals(p.Id, config.ConnectionMode, StringComparison.OrdinalIgnoreCase))?.Category ?? Sdk3D;
        }

        internal static bool MatchesCategory(CameraConfig config, string category)
        { return config != null && string.Equals(CategoryOf(config), category, StringComparison.OrdinalIgnoreCase); }

        internal static CameraConfig Snapshot(CameraConfig config)
        {
            if (config == null) return null;
            return new CameraConfig { Name = config.Name, ConnectionMode = config.ConnectionMode, SdkCategory = config.SdkCategory,
                IpAddress = config.IpAddress, SerialNumber = config.SerialNumber,
                SdkDirectory = config.SdkDirectory, CaptureTimeoutMs = config.CaptureTimeoutMs };
        }

        internal static string Validate(CameraConfig config)
        {
            if (config == null) return "Select a camera first.";
            if (!IsSdk(config)) return null;
            if (!Providers.Any(p => string.Equals(p.Id, config.ConnectionMode, StringComparison.OrdinalIgnoreCase)))
                return "Unsupported camera SDK provider: " + config.ConnectionMode;
            if (!string.Equals(CategoryOf(config), Providers.First(p => string.Equals(p.Id, config.ConnectionMode, StringComparison.OrdinalIgnoreCase)).Category, StringComparison.OrdinalIgnoreCase))
                return "The SDK provider does not support this camera category.";
            IPAddress ip;
            if (!IPAddress.TryParse((config.IpAddress ?? "").Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
                ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224)
                return "Enter a valid unicast sensor IPv4 address.";
            if (config.CaptureTimeoutMs < 100 || config.CaptureTimeoutMs > 120000)
                return "Capture timeout must be between 100 and 120000 ms.";
            if ((config.SerialNumber ?? "").Any(char.IsControl)) return "Serial number cannot contain control characters.";
            return null;
        }

        private static T WithDevice<T>(CameraConfig config, Func<ISdkCameraDevice, T> action)
        {
            config = Snapshot(config);
            string error = Validate(config);
            if (error != null) throw new InvalidOperationException(error);
            if (!IsSdk(config)) throw new InvalidOperationException("This camera uses a Cognex acquisition tool.");
            // Same endpoint shares a slot even if two logical bindings refer
            // to it. Different endpoints never share a capture lock or image.
            string key = config.ConnectionMode + "|" + IPAddress.Parse(config.IpAddress.Trim());
            string binding = (config.SerialNumber ?? "").Trim() + "|" + (config.SdkDirectory ?? "").Trim();
            var slot = Devices.GetOrAdd(key, _ => new DeviceSlot());
            lock (slot.Gate)
            {
                if (slot.Device == null || !string.Equals(slot.Binding, binding, StringComparison.OrdinalIgnoreCase))
                {
                    slot.Device?.Dispose(); slot.Device = null;
                    slot.Device = Providers.First(p => string.Equals(p.Id, config.ConnectionMode, StringComparison.OrdinalIgnoreCase)).Create(config);
                    slot.Binding = binding;
                }
                try { return action(slot.Device); }
                catch
                {
                    // A failed connection/capture must reconnect, not reuse a
                    // half-connected sensor on the next normal recovery attempt.
                    try { slot.Device.Dispose(); } catch { }
                    slot.Device = null;
                    throw;
                }
            }
        }

        internal static ICogImage Capture(CameraConfig config)
        {
            int timeout = config?.CaptureTimeoutMs > 0 ? config.CaptureTimeoutMs : 10000;
            ICogImage image = WithDevice(config, device => device.Capture(timeout)) as ICogImage;
            if (image == null) throw new InvalidOperationException("SDK camera returned no image.");
            return image;
        }

        internal static string Diagnose(CameraConfig config)
        { return WithDevice(config, device => device.Diagnose()); }

        internal static void DisposeAll()
        {
            // Keep slot identities: never create a second connection while an
            // already queued capture is still waiting for this physical device.
            foreach (var slot in Devices.Values)
                lock (slot.Gate)
                { try { slot.Device?.Dispose(); } catch { } finally { slot.Device = null; } }
        }

        internal static void SaveBinding(string path, string jobName, string camName, CameraConfig binding)
        {
            string error = Validate(binding);
            if (error != null) throw new InvalidOperationException(error);
            lock (SettingsLock)
            {
                // Merge only device fields into fresh XML. Never overwrite
                // limits, positions, channel assignments or other camera edits.
                var fresh = XmlConfigHelper.Load(path);
                var target = Find(fresh, jobName, camName);
                if (target == null) throw new InvalidOperationException("Camera is no longer present in the saved configuration.");
                if (IsSdk(binding))
                {
                    var job = fresh.Models.First(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
                    if (job.Cameras.Any(c => !ReferenceEquals(c, target) && IsSdk(c) &&
                        string.Equals(c.ConnectionMode, binding.ConnectionMode, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals((c.IpAddress ?? "").Trim(), (binding.IpAddress ?? "").Trim(), StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("This sensor IP is already assigned to another camera in this job. Use a different IP for each physical camera.");
                }
                CopyBinding(binding, target);
                string temporary = path + ".camera-" + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    XmlConfigHelper.Save(fresh, temporary);
                    if (File.Exists(path)) File.Replace(temporary, path, path + ".camera.bak");
                    else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        internal static void CopyBinding(CameraConfig source, CameraConfig target)
        {
            target.ConnectionMode = string.IsNullOrWhiteSpace(source.ConnectionMode) ? Cognex : source.ConnectionMode;
            target.SdkCategory = IsSdk(source) ? CategoryOf(source) : null;
            target.IpAddress = (source.IpAddress ?? "").Trim();
            target.SerialNumber = (source.SerialNumber ?? "").Trim();
            target.SdkDirectory = (source.SdkDirectory ?? "").Trim();
            target.CaptureTimeoutMs = source.CaptureTimeoutMs > 0 ? source.CaptureTimeoutMs : 10000;
        }
    }
}

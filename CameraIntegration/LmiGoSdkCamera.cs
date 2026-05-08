using Cognex.VisionPro;
using Cognex.VisionPro3D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2.CameraIntegration
{
	/// <summary>
	/// Late-bound GoSDK adapter. The application can still start on stations without GoSDK;
	/// the SDK is required only when an LMI-configured camera is actually captured.
	/// </summary>
	internal sealed class LmiGoSdkCamera : ISdkCameraDevice
	{
		private static readonly object SdkInitLock = new object();
		private static bool _sdkConstructed;
		private static Assembly _goSdkAssembly;
		private static Assembly _kApiAssembly;
		private static string _loadedSdkDirectory;
		private static readonly List<IntPtr> NativeLibraries = new List<IntPtr>();

		private readonly object _captureLock = new object();
		private readonly string _ipAddress;
		private readonly string _serialNumber;
		private readonly string _configuredSdkDirectory;
		private object _system;
		private object _sensor;
		private Delegate _dataHandler;
		private volatile TaskCompletionSource<ICogImage> _pendingCapture;
		private string _lastMessageTypes;
		private volatile bool _surfaceDataReceived;
		private Stopwatch _captureStopwatch;
		private bool _disposed;

		public long LastAcquisitionMs { get; private set; }
		public long LastConversionMs { get; private set; }
		public long LastTotalMs { get; private set; }

		public LmiGoSdkCamera(CameraConfig config)
		{
			_ipAddress = (config.IpAddress ?? "").Trim();
			_serialNumber = (config.SerialNumber ?? "").Trim();
			_configuredSdkDirectory = (config.SdkDirectory ?? "").Trim();
		}

		public object Capture(int timeoutMs)
		{
			lock (_captureLock)
			{
				ThrowIfDisposed();
				EnsureConnected();

				_lastMessageTypes = null;
				_surfaceDataReceived = false;
				LastAcquisitionMs = 0;
				LastConversionMs = 0;
				LastTotalMs = 0;
				_pendingCapture = new TaskCompletionSource<ICogImage>(TaskCreationOptions.RunContinuationsAsynchronously);
				try
				{
					var sink = new CaptureSink(this, _pendingCapture);
					var setHandler = _sensor.GetType().GetMethods().First(m => m.Name == "SetDataHandler" && m.GetParameters().Length == 1);
					_dataHandler = Delegate.CreateDelegate(setHandler.GetParameters()[0].ParameterType, sink,
						typeof(CaptureSink).GetMethod("OnData"));
					setHandler.Invoke(_sensor, new object[] { _dataHandler });
					string state = Convert.ToString(GetMemberValue(_sensor, "State"));
					if (!string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
						Invoke(_sensor, "Start");

					_captureStopwatch = Stopwatch.StartNew();
					Invoke(_sensor, "Trigger");
					if (!_pendingCapture.Task.Wait(Math.Max(100, timeoutMs)))
					{
						// Receiving a large surface and converting it to CogImage16Range are
						// separate phases. The configured timeout applies to receiving data;
						// once a surface has arrived, allow conversion to finish.
						if (_surfaceDataReceived && _pendingCapture.Task.Wait(60000))
							return CompleteCapture();

						string received = string.IsNullOrWhiteSpace(_lastMessageTypes)
							? "No data messages were received."
							: "Received: " + _lastMessageTypes + ". The LMI job must output UniformSurface data; a Profile-mode job is not a 3D surface.";
						throw new TimeoutException("LMI GoSDK capture timed out after " + timeoutMs + " ms (IP " + _ipAddress + "). " + received);
					}

					return CompleteCapture();
				}
				finally
				{
					if (_captureStopwatch != null)
						_captureStopwatch.Stop();
					_pendingCapture?.TrySetCanceled();
					_pendingCapture = null;
					try
					{
						string state = Convert.ToString(GetMemberValue(_sensor, "State"));
						if (string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
							Invoke(_sensor, "Stop");
					}
					catch { }
				}
			}
		}

		private ICogImage CompleteCapture()
		{
			ICogImage image = _pendingCapture.Task.GetAwaiter().GetResult();
			LastTotalMs = _captureStopwatch == null ? 0 : _captureStopwatch.ElapsedMilliseconds;
			return image;
		}

		private void EnsureConnected()
		{
			if (_sensor != null)
			{
				string state = Convert.ToString(GetMemberValue(_sensor, "State"));
				if (!string.Equals(state, "Offline", StringComparison.OrdinalIgnoreCase))
					return;
			}

			EnsureSdkLoaded(_configuredSdkDirectory);
			Type goSystemType = FindSdkType("Lmi3d.GoSdk.GoSystem");
			// GoSDK 6.x moved KIpAddress into Lmi3d.Zen.Io. Keep the old
			// namespace as a fallback so older SDK installations still work.
			Type ipType = FindSdkType("Lmi3d.Zen.Io.KIpAddress")
				?? FindSdkType("Lmi3d.Zen.KIpAddress");
			if (goSystemType == null || ipType == null)
				throw new InvalidOperationException("GoSDK managed types could not be loaded. Check GoSdkNet.dll and kApiNet.dll.");

			_system = Activator.CreateInstance(goSystemType, new object[] { false });
			object ip = InvokeStatic(ipType, "Parse", _ipAddress);
			// The discovery interface must be a local NIC address, not the
			// sensor address. Let GoSDK enable all available local interfaces;
			// this also supports an Accelerator virtual sensor on the PC.
			TryInvoke(_system, "SetAllDiscoveryInterface", true);
			TryInvoke(_system, "StartDiscovery");
			_sensor = Invoke(_system, "FindSensorByIpAddress", ip);
			if (_sensor == null)
				throw new InvalidOperationException("No LMI sensor was found at " + _ipAddress + ".");

			Invoke(_sensor, "Connect");
			Invoke(_system, "EnableData", true);

			if (!string.IsNullOrWhiteSpace(_serialNumber))
			{
				string actual = Convert.ToString(GetMemberValue(_sensor, "Id"));
				if (!string.Equals(actual, _serialNumber, StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("LMI serial mismatch. Configured " + _serialNumber + ", connected " + actual + ".");
			}
		}

		private sealed class CaptureSink
		{
			private readonly LmiGoSdkCamera _owner;
			private readonly TaskCompletionSource<ICogImage> _pending;
			internal CaptureSink(LmiGoSdkCamera owner, TaskCompletionSource<ICogImage> pending) { _owner = owner; _pending = pending; }
			public void OnData(object data) { _owner.OnData(data, _pending); }
		}

		private void OnData(object data, TaskCompletionSource<ICogImage> pending)
		{
			if (!ReferenceEquals(pending, _pendingCapture) || pending.Task.IsCompleted)
			{
				TryDispose(data);
				return;
			}

			try
			{
				_lastMessageTypes = DescribeMessageTypes(data);
				ICogImage image = ExtractUniformSurface(data);
				if (image != null && !pending.TrySetResult(image)) TryDispose(image);
			}
			catch (Exception ex)
			{
				pending.TrySetException(Unwrap(ex));
			}
			finally
			{
				TryDispose(data);
			}
		}

		private static string DescribeMessageTypes(object dataSet)
		{
			var types = new List<string>();
			long count = Convert.ToInt64(GetMemberValue(dataSet, "Count"));
			for (long i = 0; i < count; i++)
			{
				object message = InvokeIndexed(dataSet, "Get", i);
				string messageType = Convert.ToString(GetMemberValue(message, "MessageType"));
				if (!string.IsNullOrWhiteSpace(messageType) && !types.Contains(messageType))
					types.Add(messageType);
			}
			return string.Join(", ", types);
		}

		private ICogImage ExtractUniformSurface(object dataSet)
		{
			long count = Convert.ToInt64(GetMemberValue(dataSet, "Count"));
			for (long i = 0; i < count; i++)
			{
				object message = InvokeIndexed(dataSet, "Get", i);
				string messageType = Convert.ToString(GetMemberValue(message, "MessageType"));
				// GoSDK 6.5 exposes GoUniformSurfaceMsg, while MessageType.ToString()
				// returns "Surface". Older SDK wrappers may return "UniformSurface".
				bool isUniformSurface =
					string.Equals(message.GetType().Name, "GoUniformSurfaceMsg", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(messageType, "Surface", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(messageType, "UniformSurface", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(messageType, "Uniform Surface", StringComparison.OrdinalIgnoreCase);
				if (!isUniformSurface)
					continue;
				_surfaceDataReceived = true;
				LastAcquisitionMs = _captureStopwatch == null ? 0 : _captureStopwatch.ElapsedMilliseconds;
				Stopwatch conversionStopwatch = Stopwatch.StartNew();

				try
				{
					int width = Convert.ToInt32(GetMemberValue(message, "Width"));
					int height = Convert.ToInt32(GetMemberValue(message, "Length"));
					if (width <= 0 || height <= 0)
						throw new InvalidOperationException("LMI returned an empty uniform surface.");

					short[] samples = ReadSurfaceSamples(message, width, height);
					for (int index = 0; index < samples.Length; index++)
					{
						// Shift signed GoSDK heights into the unsigned VisionPro range.
						// short.MinValue becomes zero, which the range constructor treats
						// as the invalid/invisible pixel value.
						samples[index] = unchecked((short)(samples[index] ^ short.MinValue));
					}

					using (var buffer = new UnmanagedHeightBuffer(checked(samples.Length * sizeof(short))))
					{
						Marshal.Copy(samples, 0, buffer.Pointer, samples.Length);

						var root = new CogImage16Root();
						root.Initialize(width, height, buffer.Pointer, width, buffer);
						var wrapped = new CogImage16Grey();
						wrapped.SetRoot(root);
						CogImage16Grey ownedHeight = wrapped.Copy();

						double xResolution = GetResolutionMm(message, "XResolution");
						double yResolution = GetResolutionMm(message, "YResolution");
						double zResolution = GetResolutionMm(message, "ZResolution");
						double xOffsetMm = GetOffsetMm(message, "XOffset");
						double yOffsetMm = GetOffsetMm(message, "YOffset");
						double zOffsetMm = GetOffsetMm(message, "ZOffset");

						var matrix = new Cog3DMatrix3x3(
							1.0 / xResolution, 0, 0,
							0, 1.0 / yResolution, 0,
							0, 0, 1.0 / zResolution);
						var translation = new Cog3DVect3(
							-xOffsetMm / xResolution,
							-yOffsetMm / yResolution,
							32768.0 - zOffsetMm / zResolution);
						var pixelFromRoot = new Cog3DTransformLinear(matrix, translation);

						return new CogImage16Range(ownedHeight, 0, pixelFromRoot);
					}
				}
				finally
				{
					conversionStopwatch.Stop();
					LastConversionMs = conversionStopwatch.ElapsedMilliseconds;
				}
			}
			return null;
		}

		private static double GetResolutionMm(object message, string memberName)
		{
			double value = Convert.ToDouble(GetMemberValue(message, memberName)) / 1000000.0;
			if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
				throw new InvalidOperationException("LMI returned an invalid " + memberName + ".");
			return value;
		}

		private static double GetOffsetMm(object message, string memberName)
		{
			return Convert.ToDouble(GetMemberValue(message, memberName)) / 1000.0;
		}

		private sealed class UnmanagedHeightBuffer : SafeBuffer
		{
			public UnmanagedHeightBuffer(int byteLength) : base(true)
			{
				SetHandle(Marshal.AllocHGlobal(byteLength));
				Initialize((ulong)byteLength);
			}

			public IntPtr Pointer { get { return handle; } }

			protected override bool ReleaseHandle()
			{
				Marshal.FreeHGlobal(handle);
				return true;
			}
		}

		private static short[] ReadSurfaceSamples(object message, int width, int height)
		{
			int length = checked(width * height);
			short[] samples = new short[length];
			object pointerValue = GetMemberValue(message, "Data");
			if (pointerValue is IntPtr && (IntPtr)pointerValue != IntPtr.Zero)
			{
				Marshal.Copy((IntPtr)pointerValue, samples, 0, length);
				return samples;
			}

			MethodInfo get = message.GetType().GetMethods()
				.FirstOrDefault(m => m.Name == "Get" && m.GetParameters().Length == 2);
			if (get == null)
				throw new MissingMethodException(message.GetType().FullName, "Get(row, column)");

			ParameterInfo[] parameters = get.GetParameters();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					object row = Convert.ChangeType(y, parameters[0].ParameterType);
					object column = Convert.ChangeType(x, parameters[1].ParameterType);
					samples[y * width + x] = Convert.ToInt16(get.Invoke(message, new[] { row, column }));
				}
			}
			return samples;
		}

		private static void EnsureSdkLoaded(string configuredDirectory)
		{
			lock (SdkInitLock)
			{
				if (_sdkConstructed && string.IsNullOrWhiteSpace(configuredDirectory)) return;
				string sdkDirectory = FindSdkDirectory(configuredDirectory);
				if (sdkDirectory == null)
				{
					throw new FileNotFoundException(
						"LMI GoSDK was not found. Check the legacy SdkDirectory override in the camera XML if present, " +
						"or copy GoSdkNet.dll, kApiNet.dll and their native x64 dependencies into the application's LmiSdk folder.");
				}

				if (_loadedSdkDirectory != null && !string.Equals(_loadedSdkDirectory, sdkDirectory, StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException("All LMI cameras in this process must use the same GoSDK runtime. Loaded: " + _loadedSdkDirectory + "; requested: " + sdkDirectory + ". Restart after changing SDK versions.");
				if (_sdkConstructed) return;
				if (!Environment.Is64BitProcess) throw new InvalidOperationException("LMI GoSDK requires the x64 build.");
				_loadedSdkDirectory = sdkDirectory;
				// Load from an explicit directory without changing the process-wide
				// DLL search path used by VisionPro/CC24 and other camera brands.
				foreach (string nativeName in new[] { "kApi.dll", "GoSdk.dll" })
				{
					string nativePath = Path.Combine(sdkDirectory, nativeName);
					IntPtr existing = GetModuleHandle(nativeName);
					if (existing != IntPtr.Zero)
					{
						var location = new StringBuilder(32768);
						GetModuleFileName(existing, location, location.Capacity);
						if (!string.Equals(Path.GetFullPath(location.ToString()), Path.GetFullPath(nativePath), StringComparison.OrdinalIgnoreCase))
							throw new InvalidOperationException("Conflicting SDK native DLL is already loaded: " + location + ". Requested: " + nativePath + ". Restart with one SDK version.");
					}
					IntPtr library = LoadLibraryEx(nativePath, IntPtr.Zero, 0x00001100);
					if (library == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot load x64 SDK dependency: " + nativePath);
					NativeLibraries.Add(library); // retain for the process lifetime
				}
				_kApiAssembly = LoadManagedSdkAssembly(sdkDirectory, "kApiNet.dll", "Lmi3d.Zen.dll");
				_goSdkAssembly = LoadManagedSdkAssembly(sdkDirectory, "GoSdkNet.dll", "Lmi3d.GoSdk.dll");

				Type kApiLib = FindSdkType("Lmi3d.Zen.KApiLib");
				Type goSdkLib = FindSdkType("Lmi3d.GoSdk.GoSdkLib");
				if (kApiLib == null || goSdkLib == null)
					throw new InvalidOperationException("The selected folder does not contain a compatible LMI GoSDK .NET x64 runtime.");

				InvokeStatic(kApiLib, "Construct");
				InvokeStatic(goSdkLib, "Construct");
				_sdkConstructed = true;
			}
		}

		private static string FindSdkDirectory(string configuredDirectory)
		{
			var candidates = GetSdkDirectoryCandidates(configuredDirectory);
			foreach (string candidate in candidates)
			{
				string full;
				try { full = Path.GetFullPath(candidate); } catch { continue; }
				if (!Directory.Exists(full)) continue;
				if ((File.Exists(Path.Combine(full, "GoSdkNet.dll")) || File.Exists(Path.Combine(full, "Lmi3d.GoSdk.dll"))) &&
					(File.Exists(Path.Combine(full, "kApiNet.dll")) || File.Exists(Path.Combine(full, "Lmi3d.Zen.dll"))))
					return full;
			}
			return null;
		}

		private static IList<string> GetSdkDirectoryCandidates(string configuredDirectory)
		{
			var candidates = new List<string>();
			if (!string.IsNullOrWhiteSpace(configuredDirectory))
			{
				string configured = configuredDirectory.Trim();
				candidates.Add(configured);
				// Accept either the exact runtime folder or a commonly selected GoSDK
				// parent folder. This makes field deployment less path-sensitive.
				candidates.Add(Path.Combine(configured, "bin", "win64"));
				candidates.Add(Path.Combine(configured, "GoSDK", "bin", "win64"));
				candidates.Add(Path.Combine(configured, "GoSdk", "bin", "win64"));
				candidates.Add(Path.Combine(configured, "Emulator and Accelerator", "bin", "win64"));
				// An explicit legacy override must not silently fall back to another
				// installed version when the configured runtime cannot be found.
				return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
			}
			candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LmiSdk"));
			candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LMI Technologies", "Gocator", "GoSdk", "bin", "win64"));
			candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LMI Technologies", "GoSDK", "bin", "win64"));
			return candidates.Where(x => !string.IsNullOrWhiteSpace(x))
				.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		internal static string Diagnose(CameraConfig config, string parameterConfigPath)
		{
			var report = new StringBuilder();
			report.AppendLine("LMI 3D Camera Diagnostics");
			report.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			report.AppendLine("Application: " + AppDomain.CurrentDomain.BaseDirectory);
			report.AppendLine("Parameter XML: " + (string.IsNullOrWhiteSpace(parameterConfigPath) ? "(unknown)" : parameterConfigPath));
			if (!string.IsNullOrWhiteSpace(parameterConfigPath))
			{
				report.AppendLine("Parameter XML exists: " + File.Exists(parameterConfigPath));
				if (File.Exists(parameterConfigPath))
					report.AppendLine("Parameter XML modified: " + File.GetLastWriteTime(parameterConfigPath).ToString("yyyy-MM-dd HH:mm:ss"));
			}
			report.AppendLine("Process: " + (Environment.Is64BitProcess ? "x64" : "x86") +
				"; OS: " + (Environment.Is64BitOperatingSystem ? "x64" : "x86"));
			report.AppendLine();

			if (config == null)
			{
				report.AppendLine("RESULT: FAIL - camera configuration is null.");
				return report.ToString();
			}

			report.AppendLine("CURRENT CAMERA CONFIGURATION");
			report.AppendLine("Name: " + DisplayValue(config.Name));
			report.AppendLine("ConnectionMode: " + DisplayValue(config.ConnectionMode));
			report.AppendLine("IP: " + DisplayValue(config.IpAddress));
			report.AppendLine("Serial (optional): " + DisplayValue(config.SerialNumber));
			report.AppendLine("Configured GoSDK directory: " + DisplayValue(config.SdkDirectory));
			report.AppendLine("Capture timeout: " + (config.CaptureTimeoutMs > 0 ? config.CaptureTimeoutMs : 10000) + " ms");
			report.AppendLine();

			report.AppendLine("SDK DIRECTORY SEARCH");
			foreach (string candidate in GetSdkDirectoryCandidates(config.SdkDirectory))
			{
				string full;
				try { full = Path.GetFullPath(candidate); }
				catch (Exception ex)
				{
					report.AppendLine("[INVALID] " + candidate + " - " + ex.Message);
					continue;
				}
				report.AppendLine((Directory.Exists(full) ? "[EXISTS] " : "[MISSING] ") + full);
			}

			string resolved = FindSdkDirectory(config.SdkDirectory);
			report.AppendLine("Resolved SDK directory: " + (resolved ?? "(not found)"));
			if (resolved != null)
			{
				report.AppendLine();
				report.AppendLine("SDK FILES IN RESOLVED DIRECTORY");
				AppendFileStatus(report, resolved, "GoSdkNet.dll", "Lmi3d.GoSdk.dll");
				AppendFileStatus(report, resolved, "kApiNet.dll", "Lmi3d.Zen.dll");
				AppendFileStatus(report, resolved, "GoSdk.dll");
				AppendFileStatus(report, resolved, "kApi.dll");
			}

			report.AppendLine();
			report.AppendLine("NETWORK CHECK");
			string ipAddress = (config.IpAddress ?? "").Trim();
			string[] localIpv4 = GetLocalIpv4Addresses();
			report.AppendLine("Local IPv4: " + (localIpv4.Length == 0 ? "(none found)" : string.Join(", ", localIpv4)));
			if (ipAddress.Length == 0)
			{
				report.AppendLine("IP: FAIL - empty");
			}
			else
			{
				if (localIpv4.Contains(ipAddress, StringComparer.OrdinalIgnoreCase))
					report.AppendLine("Notice: configured sensor IP is also a local PC address. This is expected only when using a local Gocator Accelerator/virtual sensor.");
				report.AppendLine("Ping " + ipAddress + ": " + CheckPing(ipAddress));
				report.AppendLine("TCP " + ipAddress + ":3190: " + CheckTcp(ipAddress, 3190));
			}

			report.AppendLine();
			report.AppendLine("SDK LOAD AND SENSOR CHECK");
			LmiGoSdkCamera camera = null;
			try
			{
				camera = new LmiGoSdkCamera(config);
				camera.EnsureConnected();
				report.AppendLine("SDK load: OK");
				report.AppendLine("GoSDK managed assembly: " + GetAssemblyDescription(_goSdkAssembly));
				report.AppendLine("kApi managed assembly: " + GetAssemblyDescription(_kApiAssembly));
				report.AppendLine("Sensor connection: OK");
				report.AppendLine("State: " + DisplayValue(GetMemberValue(camera._sensor, "State")));
				report.AppendLine("Model: " + DisplayValue(GetMemberValue(camera._sensor, "Model")));
				report.AppendLine("Model number: " + DisplayValue(GetMemberValue(camera._sensor, "ModelNumber")));
				report.AppendLine("Sensor ID / Serial: " + DisplayValue(GetMemberValue(camera._sensor, "Id")));
				object setup = GetMemberValue(camera._sensor, "Setup");
				report.AppendLine("Scan mode: " + DisplayValue(GetMemberValue(setup, "ScanMode")));
				report.AppendLine("Trigger source: " + DisplayValue(GetMemberValue(setup, "TriggerSource")));
				report.AppendLine("Default job: " + DisplayValue(GetMemberValue(camera._sensor, "DefaultJob")));
				report.AppendLine();
				report.AppendLine("RESULT: OK - GoSDK loaded and the configured sensor is reachable.");
			}
			catch (Exception ex)
			{
				ex = Unwrap(ex);
				report.AppendLine("RESULT: FAIL");
				report.AppendLine("Error type: " + ex.GetType().FullName);
				report.AppendLine("Error: " + ex.Message);
				Exception inner = ex.InnerException;
				while (inner != null)
				{
					report.AppendLine("Inner: " + inner.GetType().FullName + " - " + inner.Message);
					inner = inner.InnerException;
				}
				report.AppendLine();
				report.AppendLine("LIKELY CAUSE");
				if (!Environment.Is64BitProcess)
					report.AppendLine("The application is running as x86. LMI GoSDK must run in the x64 application build.");
				else if (resolved == null)
					report.AppendLine("The configured/search directories do not contain both GoSDK .NET managed assemblies. Deploy a complete x64 runtime in the application's LmiSdk folder, or check the legacy XML SdkDirectory override.");
				else if (!File.Exists(Path.Combine(resolved, "GoSdk.dll")) || !File.Exists(Path.Combine(resolved, "kApi.dll")))
					report.AppendLine("The managed assemblies were found, but native x64 GoSdk.dll or kApi.dll is missing from the same directory.");
				else if (ex.Message.IndexOf("Not Found", StringComparison.OrdinalIgnoreCase) >= 0)
					report.AppendLine("GoSDK loaded correctly, but no sensor was found at the configured IP. Connect and power the camera, verify the PC NIC is on the same subnet, then run diagnostics again.");
				else
					report.AppendLine("The SDK files were found. Check native dependency versions, IP/NIC subnet, Accelerator state, and whether another program already owns the sensor connection.");
			}
			finally
			{
				if (camera != null) camera.Dispose();
			}

			return report.ToString();
		}

		private static void AppendFileStatus(StringBuilder report, string directory, params string[] names)
		{
			string path = names.Select(name => Path.Combine(directory, name)).FirstOrDefault(File.Exists);
			if (path == null)
			{
				report.AppendLine("[MISSING] " + string.Join(" or ", names));
				return;
			}

			string version = "";
			try { version = FileVersionInfo.GetVersionInfo(path).FileVersion; } catch { }
			report.AppendLine("[OK] " + path + (string.IsNullOrWhiteSpace(version) ? "" : " (version " + version + ")"));
		}

		private static string GetAssemblyDescription(Assembly assembly)
		{
			if (assembly == null) return "(not loaded)";
			return assembly.FullName + " | " + assembly.Location;
		}

		private static string DisplayValue(object value)
		{
			string text = Convert.ToString(value);
			return string.IsNullOrWhiteSpace(text) ? "(empty / unavailable)" : text;
		}

		private static string CheckPing(string host)
		{
			try
			{
				using (var ping = new Ping())
				{
					PingReply reply = ping.Send(host, 1000);
					return reply != null && reply.Status == IPStatus.Success
						? "OK (" + reply.RoundtripTime + " ms)"
						: "FAIL (" + (reply == null ? "no reply" : reply.Status.ToString()) + ")";
				}
			}
			catch (Exception ex) { return "FAIL (" + ex.Message + ")"; }
		}

		private static string[] GetLocalIpv4Addresses()
		{
			try
			{
				return NetworkInterface.GetAllNetworkInterfaces()
					.Where(network => network.OperationalStatus == OperationalStatus.Up)
					.SelectMany(network => network.GetIPProperties().UnicastAddresses)
					.Select(address => address.Address)
					.Where(address => address.AddressFamily == AddressFamily.InterNetwork)
					.Select(address => address.ToString())
					.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			}
			catch { return new string[0]; }
		}

		private static string CheckTcp(string host, int port)
		{
			try
			{
				using (var client = new TcpClient())
				{
					IAsyncResult result = client.BeginConnect(host, port, null, null);
					if (!result.AsyncWaitHandle.WaitOne(1000)) return "TIMEOUT (informational only)";
					client.EndConnect(result);
					return "OPEN";
				}
			}
			catch (Exception ex) { return "CLOSED/FAILED (" + ex.Message + "; informational only)"; }
		}

		private static Assembly LoadManagedSdkAssembly(string directory, params string[] names)
		{
			foreach (string name in names)
			{
				string path = Path.Combine(directory, name);
				if (File.Exists(path))
				{
					Assembly loaded = Assembly.LoadFrom(path);
					if (!string.Equals(Path.GetFullPath(loaded.Location), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
						throw new InvalidOperationException("Conflicting GoSDK managed assembly already loaded: " + loaded.Location + ". Requested: " + path);
					return loaded;
				}
			}
			throw new FileNotFoundException("Required GoSDK managed assembly is missing in " + directory + ".");
		}

		private static Type FindSdkType(string fullName)
		{
			Type type = _goSdkAssembly == null ? null : _goSdkAssembly.GetType(fullName, false);
			if (type == null && _kApiAssembly != null) type = _kApiAssembly.GetType(fullName, false);
			return type;
		}

		private static object GetMemberValue(object target, string name)
		{
			if (target == null) return null;
			PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if (property != null) return property.GetValue(target, null);
			MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			return method == null ? null : method.Invoke(target, null);
		}

		private static object Invoke(object target, string methodName, params object[] args)
		{
			MethodInfo method = FindCompatibleMethod(target.GetType(), methodName, args);
			if (method == null) throw new MissingMethodException(target.GetType().FullName, methodName);
			try { return method.Invoke(target, args); }
			catch (TargetInvocationException ex) { throw Unwrap(ex); }
		}

		private static bool TryInvoke(object target, string methodName, params object[] args)
		{
			if (target == null) return false;
			MethodInfo method = FindCompatibleMethod(target.GetType(), methodName, args);
			if (method == null) return false;
			method.Invoke(target, args);
			return true;
		}

		private static object InvokeIndexed(object target, string methodName, long index)
		{
			MethodInfo method = target.GetType().GetMethods().FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == 1);
			if (method == null) throw new MissingMethodException(target.GetType().FullName, methodName);
			object converted = Convert.ChangeType(index, method.GetParameters()[0].ParameterType);
			return method.Invoke(target, new[] { converted });
		}

		private static object InvokeStatic(Type type, string methodName, params object[] args)
		{
			MethodInfo method = FindCompatibleMethod(type, methodName, args, true);
			if (method == null) throw new MissingMethodException(type.FullName, methodName);
			return method.Invoke(null, args);
		}

		private static MethodInfo FindCompatibleMethod(Type type, string name, object[] args, bool isStatic = false)
		{
			BindingFlags flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
			return type.GetMethods(flags).FirstOrDefault(m =>
			{
				if (m.Name != name) return false;
				ParameterInfo[] p = m.GetParameters();
				if (p.Length != args.Length) return false;
				for (int i = 0; i < p.Length; i++)
				{
					if (args[i] != null && !p[i].ParameterType.IsInstanceOfType(args[i]) &&
						!IsSimpleConvertible(args[i].GetType(), p[i].ParameterType)) return false;
				}
				return true;
			});
		}

		private static bool IsSimpleConvertible(Type source, Type destination)
		{
			return typeof(IConvertible).IsAssignableFrom(source) && typeof(IConvertible).IsAssignableFrom(destination);
		}

		private static Exception Unwrap(Exception ex)
		{
			while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
			return ex;
		}

		private static void TryDispose(object value)
		{
			try
			{
				IDisposable disposable = value as IDisposable;
				if (disposable != null) disposable.Dispose();
				else if (value != null) TryInvoke(value, "Dispose");
			}
			catch { }
		}

		private void ThrowIfDisposed()
		{
			if (_disposed) throw new ObjectDisposedException(GetType().Name);
		}

		public string Diagnose()
		{
			lock (_captureLock)
			{
				ThrowIfDisposed(); EnsureConnected();
				return "LMI Gocator connected\r\nIP: " + _ipAddress +
					"\r\nSerial: " + GetMemberValue(_sensor, "Id") +
					"\r\nState: " + GetMemberValue(_sensor, "State") +
					"\r\nGoSDK: " + GetAssemblyDescription(_goSdkAssembly) +
					"\r\nkApi: " + GetAssemblyDescription(_kApiAssembly) +
					"\r\nVisionPro: " + typeof(CogImage16Range).Assembly.FullName;
			}
		}

		public void Dispose()
		{
			lock (_captureLock)
			{
				if (_disposed) return;
				_disposed = true;
				try { TryInvoke(_sensor, "Stop"); } catch { }
				try { TryInvoke(_sensor, "Disconnect"); } catch { }
				TryDispose(_sensor);
				TryDispose(_system);
				_sensor = null;
				_system = null;
				_dataHandler = null;
			}
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint flags);
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr GetModuleHandle(string name);
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int size);
	}
}

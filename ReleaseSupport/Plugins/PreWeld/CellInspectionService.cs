
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace Aron_V2.CellInspection
{
	[XmlRoot("CellInspectionSettings")]
	public class CellInspectionSettings
	{
		public double DuplicateDistanceMm { get; set; }
		public string DuplicateSelectionMode { get; set; }
		public double RowToleranceMm { get; set; }
		public double HeightDuplicateDistanceMm { get; set; }
		public double HeightRowToleranceMm { get; set; }
		public bool RequireHeightData { get; set; }
		public bool BottomIsMinimumY { get; set; }
		public bool RightIsMaximumX { get; set; }
		public int ExpectedCellCount { get; set; }

		public CellInspectionSettings()
		{
			DuplicateDistanceMm = 1.0;
			DuplicateSelectionMode = "PreferCam1";
			RowToleranceMm = 1.0;
			HeightDuplicateDistanceMm = 1.0;
			HeightRowToleranceMm = 1.0;
			RequireHeightData = true;
			BottomIsMinimumY = true;
			RightIsMaximumX = true;
			ExpectedCellCount = 0;
		}
	}

	[XmlRoot("CellCompensationTable")]
	public class CellCompensationTable
	{
		[XmlArray("Jobs"), XmlArrayItem("Job")]
		public List<CellCompensationJob> Jobs { get; set; }

		public CellCompensationTable() { Jobs = new List<CellCompensationJob>(); }
	}

	public class CellCompensationJob
	{
		[XmlAttribute] public string Name { get; set; }
		[XmlArray("Cells"), XmlArrayItem("Cell")]
		public List<CellCompensationEntry> Cells { get; set; }

		public CellCompensationJob() { Cells = new List<CellCompensationEntry>(); }
	}

	public class CellCompensationEntry
	{
		[XmlAttribute] public int Sequence { get; set; }
		[XmlAttribute] public double OffsetX { get; set; }
		[XmlAttribute] public double OffsetY { get; set; }
		[XmlAttribute] public double OffsetZ { get; set; }
	}

	[XmlRoot("CellFinalizeEvent")]
	public class CellFinalizeEventSettings
	{
		public bool Enabled { get; set; }
		public string Job { get; set; }
		// Retained for backward compatibility with existing configuration files.
		// Finalization is now based on all cameras completing the configured position.
		public string Camera { get; set; }
		public string Position { get; set; }
	}

	public class CellMeasuredPoint
	{
		[XmlAttribute] public int TemporarySequence { get; set; }
		[XmlAttribute] public string RawX { get; set; }
		[XmlAttribute] public string RawY { get; set; }
		[XmlAttribute] public string RawZ { get; set; }
		[XmlAttribute] public double X { get; set; }
		[XmlAttribute] public double Y { get; set; }
		[XmlAttribute] public double Z { get; set; }
		[XmlAttribute] public bool HasHeight { get; set; }
	}

	public class MarkMeasuredPoint
	{
		[XmlAttribute] public string Name { get; set; }
		[XmlAttribute] public string RawX { get; set; }
		[XmlAttribute] public string RawY { get; set; }
		[XmlAttribute] public string RawZ { get; set; }
		[XmlAttribute] public double X { get; set; }
		[XmlAttribute] public double Y { get; set; }
		[XmlAttribute] public double Z { get; set; }
	}

	[XmlRoot("CellCapture")]
	public class CellCaptureRecord
	{
		[XmlAttribute] public string CaptureId { get; set; }
		[XmlAttribute] public string PartNumber { get; set; }
		[XmlAttribute] public string Job { get; set; }
		[XmlAttribute] public string Camera { get; set; }
		[XmlAttribute] public string PlcPosition { get; set; }
		[XmlAttribute] public int Channel { get; set; }
		[XmlAttribute] public int CaptureOrder { get; set; }
		[XmlAttribute] public string CaptureTime { get; set; }
		[XmlAttribute] public string ImagePath { get; set; }
		[XmlAttribute] public string RawXmlPath { get; set; }
		[XmlElement("VppOutput")] public string RawVppOutput { get; set; }
		[XmlElement("MarkVppOutput")] public string RawMarkVppOutput { get; set; }
		[XmlArray("Points"), XmlArrayItem("Point")]
		public List<CellMeasuredPoint> Points { get; set; }
		[XmlArray("Marks"), XmlArrayItem("Mark")]
		public List<MarkMeasuredPoint> Marks { get; set; }

		public CellCaptureRecord()
		{
			Points = new List<CellMeasuredPoint>();
			Marks = new List<MarkMeasuredPoint>();
		}
	}

	public class MarkPointSource
	{
		[XmlAttribute] public string CaptureId { get; set; }
		[XmlAttribute] public string Camera { get; set; }
		[XmlAttribute] public string PlcPosition { get; set; }
		[XmlAttribute] public int Channel { get; set; }
		[XmlAttribute] public int CaptureOrder { get; set; }
		[XmlAttribute] public string CaptureTime { get; set; }
		[XmlAttribute] public string RawXmlPath { get; set; }
	}

	public class FinalMarkRecord
	{
		[XmlAttribute] public string Name { get; set; }
		[XmlAttribute] public string X { get; set; }
		[XmlAttribute] public string Y { get; set; }
		[XmlAttribute] public string Z { get; set; }
		[XmlElement("Source")] public MarkPointSource Source { get; set; }
	}

	public class CellCombinedData
	{
		[XmlElement("Row")] public CellCombinedRow Row { get; set; }
	}

	public class CellCombinedRow
	{
		[XmlAnyAttribute] public XmlAttribute[] Fields { get; set; }
	}

	/// <summary>
	/// Small customer-facing result. The large replay/trace structure is stored in
	/// a companion file so opening this XML in Excel/WPS produces one result row.
	/// </summary>
	[XmlRoot("CellInspectionResult")]
	public class CellInspectionResultDocument
	{
		[XmlAttribute] public string Version { get; set; }
		[XmlAttribute] public string PartNumber { get; set; }
		[XmlAttribute] public string Job { get; set; }
		[XmlAttribute] public string CreatedAt { get; set; }
		[XmlAttribute] public string CompletedAt { get; set; }
		[XmlAttribute] public int FinalCellCount { get; set; }
		[XmlAttribute] public int MarkPointCount { get; set; }
		[XmlAttribute] public int MatchedHeightCount { get; set; }
		[XmlAttribute] public bool BottomIsMinimumY { get; set; }
		[XmlAttribute] public bool RightIsMaximumX { get; set; }
		[XmlAttribute] public string ReplayFile { get; set; }
		[XmlElement("CombinedData")] public CellCombinedData CombinedData { get; set; }

		public CellInspectionResultDocument()
		{
			Version = "1.0";
		}
	}

	public class CellPointSource
	{
		[XmlAttribute] public string CaptureId { get; set; }
		[XmlAttribute] public string Camera { get; set; }
		[XmlAttribute] public string PlcPosition { get; set; }
		[XmlAttribute] public int Channel { get; set; }
		[XmlAttribute] public int CaptureOrder { get; set; }
		[XmlAttribute] public string CaptureTime { get; set; }
		[XmlAttribute] public int VppSequence { get; set; }
		[XmlAttribute] public string RawX { get; set; }
		[XmlAttribute] public string RawY { get; set; }
		[XmlAttribute] public string RawZ { get; set; }
		[XmlAttribute] public double X { get; set; }
		[XmlAttribute] public double Y { get; set; }
		[XmlAttribute] public double Z { get; set; }
		[XmlAttribute] public bool HasHeight { get; set; }
		[XmlAttribute] public string ImagePath { get; set; }
		[XmlAttribute] public string RawXmlPath { get; set; }
	}

	public class FinalCellRecord
	{
		[XmlAttribute] public int Sequence { get; set; }
		[XmlAttribute] public int Row { get; set; }
		[XmlAttribute] public int RowPosition { get; set; }
		[XmlAttribute] public int TriangleBand { get; set; }
		[XmlAttribute] public string X { get; set; }
		[XmlAttribute] public string Y { get; set; }
		[XmlAttribute] public string Height { get; set; }
		[XmlAttribute] public double OffsetX { get; set; }
		[XmlAttribute] public double OffsetY { get; set; }
		[XmlAttribute] public double OffsetZ { get; set; }
		[XmlElement("SelectedSource")] public CellPointSource SelectedSource { get; set; }
		[XmlArray("DuplicateSources"), XmlArrayItem("Source")]
		public List<CellPointSource> DuplicateSources { get; set; }
		[XmlElement("HeightSource")] public CellPointSource HeightSource { get; set; }
		[XmlArray("DuplicateHeightSources"), XmlArrayItem("Source")]
		public List<CellPointSource> DuplicateHeightSources { get; set; }

		public FinalCellRecord()
		{
			DuplicateSources = new List<CellPointSource>();
			DuplicateHeightSources = new List<CellPointSource>();
		}
		[XmlIgnore] public int DuplicateCount { get { return DuplicateSources == null ? 0 : DuplicateSources.Count; } }
		[XmlIgnore] public int HeightDuplicateCount { get { return DuplicateHeightSources == null ? 0 : DuplicateHeightSources.Count; } }
	}

	[XmlRoot("CellInspection")]
	public class CellInspectionDocument
	{
		[XmlAttribute] public string Version { get; set; }
		[XmlAttribute] public string PartNumber { get; set; }
		[XmlAttribute] public string Job { get; set; }
		[XmlAttribute] public string CreatedAt { get; set; }
		[XmlAttribute] public string CompletedAt { get; set; }
		[XmlAttribute] public double DuplicateDistanceMm { get; set; }
		[XmlAttribute] public string DuplicateSelectionMode { get; set; }
		[XmlAttribute] public double RowToleranceMm { get; set; }
		[XmlAttribute] public double HeightDuplicateDistanceMm { get; set; }
		[XmlAttribute] public double HeightRowToleranceMm { get; set; }
		[XmlAttribute] public bool RequireHeightData { get; set; }
		[XmlAttribute] public bool BottomIsMinimumY { get; set; }
		[XmlAttribute] public bool RightIsMaximumX { get; set; }
		[XmlAttribute] public int ExpectedCellCount { get; set; }
		[XmlAttribute] public int RawPointCount { get; set; }
		[XmlAttribute] public int FinalCellCount { get; set; }
		[XmlAttribute] public int HeightPointCount { get; set; }
		[XmlAttribute] public int MarkPointCount { get; set; }
		[XmlAttribute] public int MatchedHeightCount { get; set; }
		[XmlAttribute] public int MissingHeightCount { get; set; }
		[XmlArray("RawCaptures"), XmlArrayItem("Capture")]
		public List<CellCaptureRecord> RawCaptures { get; set; }
		[XmlElement("CombinedData")] public CellCombinedData CombinedData { get; set; }
		[XmlArray("Marks"), XmlArrayItem("Mark")]
		public List<FinalMarkRecord> Marks { get; set; }
		[XmlArray("SortedCells"), XmlArrayItem("Cell")]
		public List<FinalCellRecord> Cells { get; set; }

		public CellInspectionDocument()
		{
			Version = "1.0";
			RawCaptures = new List<CellCaptureRecord>();
			Marks = new List<FinalMarkRecord>();
			Cells = new List<FinalCellRecord>();
		}

		// Raw captures already exist as one XML per camera/PLC position. Embedding
		// them again made every complete result unnecessarily large.
		public bool ShouldSerializeRawCaptures() { return false; }

		// CombinedData is written to the small customer result file. It is rebuilt
		// in memory whenever a replay document is opened.
		public bool ShouldSerializeCombinedData() { return false; }
	}

	internal sealed class SessionState
	{
		public string Directory;
		public string PartNumber;
		public string Job;
		public int NextCaptureOrder;
		public readonly object Sync = new object();
	}

	internal sealed class SourceWithPoint
	{
		public CellPointSource Source;
		public int Index;
	}

	internal sealed class SourceWithMark
	{
		public MarkMeasuredPoint Mark;
		public MarkPointSource Source;
	}

	public static class CellInspectionService
	{
		private static readonly object CompensationSync = new object();
		private static readonly ConcurrentDictionary<string, SessionState> Sessions =
			new ConcurrentDictionary<string, SessionState>(StringComparer.OrdinalIgnoreCase);
		private static readonly ConcurrentDictionary<string, string> LastFinalSummaries =
			new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly string[] PreferredOutputNames =
			{ "CellCoordinates", "CellCoordinates3D", "CellXYZ", "CellHeights", "CellCoordinate", "CellData", "CellXY", "Cells" };

		public static void ResetSessions() { Sessions.Clear(); LastFinalSummaries.Clear(); }
        public static string RootDirectory
		{
			get { return Path.Combine(PreWeldEnvironment.Host.DataDirectory, "CellData"); }
		}

		public static string SettingsPath
		{
			get { return Path.Combine(PreWeldEnvironment.Host.ConfigDirectory, "CellInspection.xml"); }
		}

		public static string FinalizeEventPath
		{
			get { return Path.Combine(PreWeldEnvironment.Host.ConfigDirectory, "CellFinalizeEvent.xml"); }
		}

		public static string CompensationPath
		{
			get { return Path.Combine(PreWeldEnvironment.Host.ConfigDirectory, "CellCompensation.xml"); }
		}

		public static List<CellCompensationEntry> LoadCompensations(string job)
		{
			lock (CompensationSync)
			{
				try
				{
					if (!File.Exists(CompensationPath)) return new List<CellCompensationEntry>();
					CellCompensationTable table = Deserialize<CellCompensationTable>(CompensationPath) ?? new CellCompensationTable();
					CellCompensationJob match = (table.Jobs ?? new List<CellCompensationJob>())
						.FirstOrDefault(item => item != null && string.Equals(item.Name, job, StringComparison.OrdinalIgnoreCase));
					return match == null
						? new List<CellCompensationEntry>()
						: (match.Cells ?? new List<CellCompensationEntry>()).Select(CloneCompensation).ToList();
				}
				catch (Exception ex) { throw new InvalidDataException("Invalid compensation file: " + CompensationPath, ex); }
			}
		}

		public static void SaveCompensations(string job, IEnumerable<CellCompensationEntry> entries)
		{
			if (string.IsNullOrWhiteSpace(job)) throw new InvalidOperationException("Job is empty; compensation cannot be saved.");
			lock (CompensationSync)
			{
				CellCompensationTable table;
				try { table = File.Exists(CompensationPath) ? Deserialize<CellCompensationTable>(CompensationPath) : new CellCompensationTable(); }
				catch (Exception ex) { throw new InvalidDataException("Existing compensation is invalid; save blocked.", ex); }
				if (table == null) table = new CellCompensationTable();
				if (table.Jobs == null) table.Jobs = new List<CellCompensationJob>();
				table.Jobs.RemoveAll(item => item != null && string.Equals(item.Name, job, StringComparison.OrdinalIgnoreCase));
				table.Jobs.Add(new CellCompensationJob
				{
					Name = job,
					Cells = (entries ?? Enumerable.Empty<CellCompensationEntry>())
						.Where(item => item != null && item.Sequence > 0)
						.GroupBy(item => item.Sequence).Select(group => CloneCompensation(group.Last()))
						.OrderBy(item => item.Sequence).ToList()
				});
				SerializeAtomic(table, CompensationPath);
			}
		}

		private static CellCompensationEntry CloneCompensation(CellCompensationEntry item)
		{
			return new CellCompensationEntry
			{
				Sequence = item.Sequence,
				OffsetX = item.OffsetX,
				OffsetY = item.OffsetY,
				OffsetZ = item.OffsetZ
			};
		}

		public static CellInspectionSettings LoadSettings()
		{
			try
			{
				if (!File.Exists(SettingsPath))
				{
					var defaults = new CellInspectionSettings();
					SaveSettings(defaults);
					return defaults;
				}
				return Deserialize<CellInspectionSettings>(SettingsPath);
			}
			catch (Exception ex) { throw new InvalidDataException("Invalid CellInspection settings", ex); }
		}

		public static void SaveSettings(CellInspectionSettings settings)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			SerializeAtomic(settings, SettingsPath);
		}

		public static CellFinalizeEventSettings LoadFinalizeEvent()
		{
			try
			{
				if (!File.Exists(FinalizeEventPath)) return new CellFinalizeEventSettings();
				return Deserialize<CellFinalizeEventSettings>(FinalizeEventPath) ?? new CellFinalizeEventSettings();
			}
			catch (Exception ex) { throw new InvalidDataException("Invalid summary event settings", ex); }
		}

		public static void SaveFinalizeEvent(CellFinalizeEventSettings settings)
		{
			if (settings == null) throw new ArgumentNullException("settings");
			SerializeAtomic(settings, FinalizeEventPath);
		}

		public static bool IsFinalizeEventMatch(string job, string position)
		{
			CellFinalizeEventSettings settings = LoadFinalizeEvent();
			return settings.Enabled &&
				string.Equals(settings.Job, job, StringComparison.OrdinalIgnoreCase) &&
				string.Equals(settings.Position, position, StringComparison.OrdinalIgnoreCase);
		}

		public static bool IsCollectionEnabledForJob(string job)
		{
			CellFinalizeEventSettings settings = LoadFinalizeEvent();
			return settings.Enabled && string.Equals(settings.Job, job, StringComparison.OrdinalIgnoreCase);
		}

		public static bool HasCapture(string job, string partNumber, string camera, string position)
		{
			if (string.IsNullOrWhiteSpace(partNumber)) return false;
			SessionState session = GetOrCreateSession(job, partNumber);
			string rawDirectory = Path.Combine(session.Directory, "Raw");
			if (!Directory.Exists(rawDirectory)) return false;
			foreach (string path in Directory.GetFiles(rawDirectory, "*.xml"))
			{
				try
				{
					CellCaptureRecord capture = Deserialize<CellCaptureRecord>(path);
					if (string.Equals(capture.Camera, camera, StringComparison.OrdinalIgnoreCase) &&
						string.Equals(capture.PlcPosition, position, StringComparison.OrdinalIgnoreCase)) return true;
				}
				catch { }
			}
			return false;
		}





		public static bool TryParseCoordinatePayload(string payload, out List<CellMeasuredPoint> points)
		{
			points = new List<CellMeasuredPoint>();
			if (string.IsNullOrWhiteSpace(payload)) return false;
			string normalized = payload.Replace('；', ';').Replace('，', ',');
			string[] records = normalized.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string record in records)
			{
				string[] fields = record.Split(',');
				if (fields.Length < 3) continue;
				int temporarySequence;
				double x, y, z = 0;
				if (!int.TryParse(fields[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out temporarySequence)) continue;
				if (!TryParseDouble(fields[1], out x) || !TryParseDouble(fields[2], out y)) continue;
				bool hasHeight = fields.Length >= 4;
				if (hasHeight && !TryParseDouble(fields[3], out z)) continue;
				points.Add(new CellMeasuredPoint
				{
					TemporarySequence = temporarySequence,
					RawX = fields[1].Trim(),
					RawY = fields[2].Trim(),
					RawZ = hasHeight ? fields[3].Trim() : "",
					X = x,
					Y = y,
					Z = z,
					HasHeight = hasHeight
				});
			}
			return points.Count > 0;
		}



		public static bool TryParseMarkPayload(string payload, out List<MarkMeasuredPoint> marks)
		{
			marks = new List<MarkMeasuredPoint>();
			if (string.IsNullOrWhiteSpace(payload)) return false;
			string normalized = payload.Replace('；', ';').Replace('，', ',');
			foreach (string record in normalized.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string[] fields = record.Split(',');
				if (fields.Length < 4) continue;
				string name = (fields[0] ?? "").Trim();
				double x, y, z;
				if (name.Length == 0 || !TryParseDouble(fields[1], out x) ||
					!TryParseDouble(fields[2], out y) || !TryParseDouble(fields[3], out z)) continue;
				marks.Add(new MarkMeasuredPoint
				{
					Name = name,
					RawX = fields[1].Trim(),
					RawY = fields[2].Trim(),
					RawZ = fields[3].Trim(),
					X = x,
					Y = y,
					Z = z
				});
			}
			return marks.Count > 0;
		}

		public static string RecordCapture(
			string partNumber,
			string job,
			string camera,
			string plcPosition,
			int channel,
			DateTime captureTime,
			string imagePath,
			string payload,
			List<CellMeasuredPoint> points,
			string markPayload,
			List<MarkMeasuredPoint> marks)
		{
			bool hasCells = points != null && points.Count > 0;
			bool hasMarks = marks != null && marks.Count > 0;
			if (string.IsNullOrWhiteSpace(partNumber) || (!hasCells && !hasMarks)) return null;
			string previousSummary;
			LastFinalSummaries.TryRemove(SessionKey(job, partNumber), out previousSummary);
			SessionState session = GetOrCreateSession(job, partNumber);
			lock (session.Sync)
			{
				int order = ++session.NextCaptureOrder;
				string captureId = captureTime.ToString("yyyyMMdd_HHmmss_fff") + "_" + SafeName(camera) + "_" + SafeName(plcPosition) + "_" + order;
				string rawDirectory = Path.Combine(session.Directory, "Raw");
				Directory.CreateDirectory(rawDirectory);
				string path = Path.Combine(rawDirectory, captureId + ".xml");
				var record = new CellCaptureRecord
				{
					CaptureId = captureId,
					PartNumber = partNumber.Trim(),
					Job = job ?? "",
					Camera = camera ?? "",
					PlcPosition = plcPosition ?? "",
					Channel = channel,
					CaptureOrder = order,
					CaptureTime = captureTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
					ImagePath = imagePath ?? "",
					RawXmlPath = path,
					RawVppOutput = payload ?? "",
					RawMarkVppOutput = markPayload ?? "",
					Points = points ?? new List<CellMeasuredPoint>(),
					Marks = marks ?? new List<MarkMeasuredPoint>()
				};
				SerializeAtomic(record, path);
				return path;
			}
		}

		public static string FinalizePart(string job, string partNumber)
		{
			if (string.IsNullOrWhiteSpace(partNumber))
				throw new InvalidOperationException("PLC Partnumber is empty; cell data cannot be finalized.");
			string existingSummary;
			if (LastFinalSummaries.TryGetValue(SessionKey(job, partNumber), out existingSummary) && File.Exists(existingSummary))
				return existingSummary;

			SessionState session = GetOrCreateSession(job, partNumber);
			lock (session.Sync)
			{
				string rawDirectory = Path.Combine(session.Directory, "Raw");
				var captures = new List<CellCaptureRecord>();
				if (Directory.Exists(rawDirectory))
				{
					foreach (string path in Directory.GetFiles(rawDirectory, "*.xml").OrderBy(x => x))
					{
						try { captures.Add(Deserialize<CellCaptureRecord>(path)); }
						catch { }
					}
				}
				if (captures.Count == 0)
					throw new InvalidOperationException("No position-level cell XML was found for Partnumber " + partNumber + ".");

				CellInspectionSettings settings = LoadSettings();
				CellInspectionDocument document = BuildDocument(job, partNumber, captures, settings);
				string completedAt = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
				string summaryDirectory = Path.Combine(RootDirectory, "Summary", SafeName(job), DateTime.Now.ToString("yyyyMMdd"));
				Directory.CreateDirectory(summaryDirectory);
				string summaryPath = Path.Combine(summaryDirectory, SafeName(partNumber) + "_" + completedAt + ".xml");
				string replayFileName = SafeName(partNumber) + "_" + completedAt + "_Replay.xml";
				string replayPath = Path.Combine(summaryDirectory, replayFileName);
				var result = BuildCustomerResult(document, replayFileName);
				SerializeAtomic(result, summaryPath);
				SerializeAtomic(document, replayPath);
				// Complete.xml is a small completion marker and fallback result inside the
				// session. It points to the actual replay file instead of duplicating it.
				CellInspectionResultDocument sessionResult = BuildCustomerResult(document, replayPath);
				SerializeAtomic(sessionResult, Path.Combine(session.Directory, "Complete.xml"));
                if (LastFinalSummaries.Count >= 1024) LastFinalSummaries.Clear();
				LastFinalSummaries[SessionKey(job, partNumber)] = summaryPath;
				SessionState removed;
				Sessions.TryRemove(SessionKey(job, partNumber), out removed);
				return summaryPath;
			}
		}

		public static CellInspectionDocument LoadDocument(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");
			CellInspectionDocument document;
			bool hasReplayDetail = true;
			string rootName = ReadRootName(path);
			if (string.Equals(rootName, "CellInspectionResult", StringComparison.OrdinalIgnoreCase))
			{
				CellInspectionResultDocument result = Deserialize<CellInspectionResultDocument>(path);
				string replayPath = ResolveReplayPath(path, result == null ? null : result.ReplayFile);
				hasReplayDetail = !string.IsNullOrWhiteSpace(replayPath) && File.Exists(replayPath);
				document = hasReplayDetail
					? Deserialize<CellInspectionDocument>(replayPath)
					: BuildDocumentFromCustomerResult(result);
			}
			else
			{
				document = Deserialize<CellInspectionDocument>(path);
			}
			if (document != null)
			{
				// The compact standalone file already contains compensated final values.
				// Reapplying offsets requires the raw source values from the replay file.
				if (hasReplayDetail) ApplyCompensations(document.Job, document.Cells, true);
				document.CombinedData = BuildCombinedData(document.Marks, document.Cells);
			}
			return document;
		}

		private static CellInspectionResultDocument BuildCustomerResult(
			CellInspectionDocument document, string replayFile)
		{
			return new CellInspectionResultDocument
			{
				Version = document.Version,
				PartNumber = document.PartNumber,
				Job = document.Job,
				CreatedAt = document.CreatedAt,
				CompletedAt = document.CompletedAt,
				FinalCellCount = document.FinalCellCount,
				MarkPointCount = document.MarkPointCount,
				MatchedHeightCount = document.MatchedHeightCount,
				BottomIsMinimumY = document.BottomIsMinimumY,
				RightIsMaximumX = document.RightIsMaximumX,
				ReplayFile = replayFile ?? "",
				CombinedData = document.CombinedData ?? BuildCombinedData(document.Marks, document.Cells)
			};
		}

		private static string ReadRootName(string path)
		{
			using (XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true }))
			{
				reader.MoveToContent();
				return reader.LocalName;
			}
		}

		private static string ResolveReplayPath(string resultPath, string replayFile)
		{
			if (string.IsNullOrWhiteSpace(replayFile)) return null;
			if (Path.IsPathRooted(replayFile)) return replayFile;
			return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(resultPath)) ?? "", replayFile);
		}

		private static CellInspectionDocument BuildDocumentFromCustomerResult(CellInspectionResultDocument result)
		{
			if (result == null) throw new InvalidOperationException("The compact result XML is empty.");
			var marks = new List<FinalMarkRecord>();
			var cells = new List<FinalCellRecord>();
			IEnumerable<XmlAttribute> fields = result.CombinedData == null || result.CombinedData.Row == null ||
				result.CombinedData.Row.Fields == null
				? Enumerable.Empty<XmlAttribute>() : result.CombinedData.Row.Fields;
			foreach (XmlAttribute field in fields)
			{
				if (field == null) continue;
				string[] values = (field.Value ?? "").Split(',');
				if (values.Length < 3) continue;
				if (field.Name.StartsWith("Mark", StringComparison.OrdinalIgnoreCase))
				{
					marks.Add(new FinalMarkRecord { Name = field.Name, X = values[0], Y = values[1], Z = values[2] });
					continue;
				}
				if (!field.Name.StartsWith("Cell", StringComparison.OrdinalIgnoreCase)) continue;
				int sequence;
				if (!int.TryParse(field.Name.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence)) continue;
				cells.Add(new FinalCellRecord { Sequence = sequence, X = values[0], Y = values[1], Height = values[2] });
			}
			return new CellInspectionDocument
			{
				Version = result.Version,
				PartNumber = result.PartNumber,
				Job = result.Job,
				CreatedAt = result.CreatedAt,
				CompletedAt = result.CompletedAt,
				FinalCellCount = cells.Count,
				MarkPointCount = marks.Count,
				MatchedHeightCount = result.MatchedHeightCount,
				MissingHeightCount = Math.Max(0, cells.Count - result.MatchedHeightCount),
				BottomIsMinimumY = result.BottomIsMinimumY,
				RightIsMaximumX = result.RightIsMaximumX,
				RawCaptures = new List<CellCaptureRecord>(),
				CombinedData = result.CombinedData,
				Marks = marks.OrderBy(mark => MarkSortNumber(mark.Name)).ToList(),
				Cells = cells.OrderBy(cell => cell.Sequence).ToList()
			};
		}

		public static CellInspectionDocument BuildDocument(
			string job,
			string partNumber,
			List<CellCaptureRecord> captures,
			CellInspectionSettings settings)
		{
			settings = settings ?? new CellInspectionSettings();
			var sources = new List<SourceWithPoint>();
			var markSources = new List<SourceWithMark>();
			foreach (CellCaptureRecord capture in captures ?? new List<CellCaptureRecord>())
			{
				foreach (CellMeasuredPoint point in capture.Points ?? new List<CellMeasuredPoint>())
				{
					sources.Add(new SourceWithPoint
					{
						Index = sources.Count,
						Source = new CellPointSource
						{
							CaptureId = capture.CaptureId,
							Camera = capture.Camera,
							PlcPosition = capture.PlcPosition,
							Channel = capture.Channel,
							CaptureOrder = capture.CaptureOrder,
							CaptureTime = capture.CaptureTime,
							VppSequence = point.TemporarySequence,
							RawX = point.RawX,
							RawY = point.RawY,
							RawZ = point.RawZ,
							X = point.X,
							Y = point.Y,
							Z = point.Z,
							HasHeight = point.HasHeight,
							ImagePath = capture.ImagePath,
							RawXmlPath = capture.RawXmlPath
						}
					});
				}
				foreach (MarkMeasuredPoint mark in capture.Marks ?? new List<MarkMeasuredPoint>())
				{
					markSources.Add(new SourceWithMark
					{
						Mark = mark,
						Source = new MarkPointSource
						{
							CaptureId = capture.CaptureId,
							Camera = capture.Camera,
							PlcPosition = capture.PlcPosition,
							Channel = capture.Channel,
							CaptureOrder = capture.CaptureOrder,
							CaptureTime = capture.CaptureTime,
							RawXmlPath = capture.RawXmlPath
						}
					});
				}
			}

			List<IGrouping<string, SourceWithMark>> duplicateMarks = markSources
				.GroupBy(item => (item.Mark.Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
				.Where(group => group.Count() > 1).ToList();
			if (duplicateMarks.Count > 0)
			{
				string detail = string.Join("; ", duplicateMarks.Select(group => group.Key + " from " +
					string.Join(", ", group.Select(item => item.Source.Camera + "/" + item.Source.PlcPosition))));
				throw new InvalidOperationException("Duplicate Mark name detected. Every Mark name must be unique: " + detail);
			}
			var finalMarks = markSources.OrderBy(item => MarkSortNumber(item.Mark.Name))
				.ThenBy(item => item.Mark.Name, StringComparer.OrdinalIgnoreCase)
				.Select(item => new FinalMarkRecord
				{
					Name = (item.Mark.Name ?? "").Trim(),
					X = item.Mark.RawX,
					Y = item.Mark.RawY,
					Z = item.Mark.RawZ,
					Source = item.Source
				}).ToList();

			List<SourceWithPoint> coordinateSources = sources.Where(item => !item.Source.HasHeight).ToList();
			for (int index = 0; index < coordinateSources.Count; index++) coordinateSources[index].Index = index;
			List<SourceWithPoint> heightSources = sources.Where(item => item.Source.HasHeight).ToList();
			for (int index = 0; index < heightSources.Count; index++) heightSources[index].Index = index;
			List<List<SourceWithPoint>> groups = GroupDuplicates(coordinateSources, Math.Max(0, settings.DuplicateDistanceMm));
			var cells = new List<FinalCellRecord>();
			foreach (List<SourceWithPoint> group in groups)
			{
				SourceWithPoint selected = SelectSource(group, settings.DuplicateSelectionMode);
				cells.Add(new FinalCellRecord
				{
					X = selected.Source.RawX,
					Y = selected.Source.RawY,
					SelectedSource = selected.Source,
					DuplicateSources = group.Where(x => x != selected).Select(x => x.Source).ToList()
				});
			}

			ApplyTriangleOrder(cells, settings);
			List<List<SourceWithPoint>> heightGroups = GroupDuplicates(heightSources,
				Math.Max(0, settings.HeightDuplicateDistanceMm));
			var heightCells = new List<FinalCellRecord>();
			foreach (List<SourceWithPoint> group in heightGroups)
			{
				SourceWithPoint selected = SelectSource(group, "PreferEarlierCapture");
				heightCells.Add(new FinalCellRecord
				{
					X = selected.Source.RawX,
					Y = selected.Source.RawY,
					Height = selected.Source.RawZ,
					SelectedSource = selected.Source,
					DuplicateSources = group.Where(item => item != selected).Select(item => item.Source).ToList()
				});
			}
			var heightSettings = new CellInspectionSettings
			{
				RowToleranceMm = settings.HeightRowToleranceMm,
				BottomIsMinimumY = settings.BottomIsMinimumY,
				RightIsMaximumX = settings.RightIsMaximumX
			};
			ApplyTriangleOrder(heightCells, heightSettings);
			MergeHeightBySequence(cells, heightCells, settings.RequireHeightData);
			ApplyCompensations(job, cells, false);
			CellCombinedData combinedData = BuildCombinedData(finalMarks, cells);
			int matchedHeightCells = cells.Count(cell => cell.HeightSource != null);
			return new CellInspectionDocument
			{
				Version = "1.0",
				PartNumber = partNumber ?? "",
				Job = job ?? "",
				CreatedAt = captures.OrderBy(x => x.CaptureOrder).Select(x => x.CaptureTime).FirstOrDefault() ?? "",
				CompletedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
				DuplicateDistanceMm = settings.DuplicateDistanceMm,
				DuplicateSelectionMode = settings.DuplicateSelectionMode,
				RowToleranceMm = settings.RowToleranceMm,
				HeightDuplicateDistanceMm = settings.HeightDuplicateDistanceMm,
				HeightRowToleranceMm = settings.HeightRowToleranceMm,
				RequireHeightData = settings.RequireHeightData,
				BottomIsMinimumY = settings.BottomIsMinimumY,
				RightIsMaximumX = settings.RightIsMaximumX,
				ExpectedCellCount = settings.ExpectedCellCount,
				RawPointCount = sources.Count,
				FinalCellCount = cells.Count,
				HeightPointCount = heightSources.Count,
				MarkPointCount = finalMarks.Count,
				MatchedHeightCount = matchedHeightCells,
				MissingHeightCount = cells.Count - matchedHeightCells,
				RawCaptures = captures.OrderBy(x => x.CaptureOrder).ToList(),
				CombinedData = combinedData,
				Marks = finalMarks,
				Cells = cells.OrderBy(x => x.Sequence).ToList()
			};
		}

		private static int MarkSortNumber(string name)
		{
			string text = (name ?? "").Trim();
			if (!text.StartsWith("Mark", StringComparison.OrdinalIgnoreCase)) return int.MaxValue;
			int value;
			return int.TryParse(text.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
				? value : int.MaxValue;
		}

		private static CellCombinedData BuildCombinedData(
			IEnumerable<FinalMarkRecord> marks,
			IEnumerable<FinalCellRecord> cells)
		{
			var document = new XmlDocument();
			var fields = new List<XmlAttribute>();
			foreach (FinalMarkRecord mark in marks ?? Enumerable.Empty<FinalMarkRecord>())
			{
				string name;
				try { name = XmlConvert.VerifyNCName(mark.Name); }
				catch (Exception ex)
				{
					throw new InvalidOperationException("Mark name is not a valid XML field name: " + mark.Name, ex);
				}
				XmlAttribute attribute = document.CreateAttribute(name);
				attribute.Value = (mark.X ?? "") + "," + (mark.Y ?? "") + "," + (mark.Z ?? "");
				fields.Add(attribute);
			}
			foreach (FinalCellRecord cell in (cells ?? Enumerable.Empty<FinalCellRecord>()).OrderBy(item => item.Sequence))
			{
				XmlAttribute attribute = document.CreateAttribute("Cell" + cell.Sequence.ToString(CultureInfo.InvariantCulture));
				attribute.Value = (cell.X ?? "") + "," + (cell.Y ?? "") + "," + (cell.Height ?? "");
				fields.Add(attribute);
			}
			return new CellCombinedData { Row = new CellCombinedRow { Fields = fields.ToArray() } };
		}

		public static void ApplyCompensations(string job, IList<FinalCellRecord> cells)
		{
			ApplyCompensations(job, cells, true);
		}

		private static void ApplyCompensations(string job, IList<FinalCellRecord> cells, bool preserveEmbeddedOffsets)
		{
			Dictionary<int, CellCompensationEntry> entries = LoadCompensations(job)
				.GroupBy(item => item.Sequence).ToDictionary(group => group.Key, group => group.Last());
			foreach (FinalCellRecord cell in cells ?? new List<FinalCellRecord>())
			{
				CellCompensationEntry entry;
				if (entries.TryGetValue(cell.Sequence, out entry))
				{
					cell.OffsetX = entry.OffsetX;
					cell.OffsetY = entry.OffsetY;
					cell.OffsetZ = entry.OffsetZ;
				}
				else if (!preserveEmbeddedOffsets)
				{
					cell.OffsetX = 0;
					cell.OffsetY = 0;
					cell.OffsetZ = 0;
				}
				RefreshFinalValues(cell);
			}
		}

		public static void RefreshFinalValues(FinalCellRecord cell)
		{
			if (cell == null) return;
			double baseX, baseY;
			if (cell.SelectedSource != null &&
				TryParseDouble(cell.SelectedSource.RawX, out baseX) &&
				TryParseDouble(cell.SelectedSource.RawY, out baseY))
			{
				cell.X = FormatNumber(baseX + cell.OffsetX);
				cell.Y = FormatNumber(baseY + cell.OffsetY);
			}

			double baseZ;
			if (cell.HeightSource != null && TryParseDouble(cell.HeightSource.RawZ, out baseZ))
				cell.Height = FormatNumber(baseZ + cell.OffsetZ);
			else if (cell.HeightSource == null)
				cell.Height = "";
		}

		private static string FormatNumber(double value)
		{
			return value.ToString("0.###############", CultureInfo.InvariantCulture);
		}

		private static void MergeHeightBySequence(
			List<FinalCellRecord> coordinateCells,
			List<FinalCellRecord> heightCells,
			bool requireHeightData)
		{
			coordinateCells = coordinateCells ?? new List<FinalCellRecord>();
			heightCells = heightCells ?? new List<FinalCellRecord>();
			if (heightCells.Count == 0)
			{
				if (requireHeightData)
					throw new InvalidOperationException("No 3D cell height data was found. Expected VPP String format: sequence,X,Y,Z;...");
				return;
			}
			if (coordinateCells.Count != heightCells.Count)
				throw new InvalidOperationException("2D/3D cell count mismatch after independent deduplication: 2D=" +
					coordinateCells.Count + ", 3D=" + heightCells.Count + ". Height merge was stopped.");

			int[] coordinateRows = coordinateCells.GroupBy(cell => cell.Row).OrderBy(group => group.Key)
				.Select(group => group.Count()).ToArray();
			int[] heightRows = heightCells.GroupBy(cell => cell.Row).OrderBy(group => group.Key)
				.Select(group => group.Count()).ToArray();
			if (!coordinateRows.SequenceEqual(heightRows))
				throw new InvalidOperationException("2D/3D row structure mismatch after sorting. 2D rows=" +
					string.Join(",", coordinateRows) + "; 3D rows=" + string.Join(",", heightRows) +
					". Height merge was stopped.");

			Dictionary<int, FinalCellRecord> heightBySequence = heightCells.ToDictionary(cell => cell.Sequence);
			foreach (FinalCellRecord coordinateCell in coordinateCells)
			{
				FinalCellRecord heightCell = heightBySequence[coordinateCell.Sequence];
				coordinateCell.Height = heightCell.Height;
				coordinateCell.HeightSource = heightCell.SelectedSource;
				coordinateCell.DuplicateHeightSources = heightCell.DuplicateSources ?? new List<CellPointSource>();
			}
		}

		private static List<List<SourceWithPoint>> GroupDuplicates(List<SourceWithPoint> points, double threshold)
		{
			int count = points.Count;
			int[] parent = Enumerable.Range(0, count).ToArray();
			double squared = threshold * threshold;
			for (int i = 0; i < count; i++)
			{
				for (int j = i + 1; j < count; j++)
				{
					double dx = points[i].Source.X - points[j].Source.X;
					double dy = points[i].Source.Y - points[j].Source.Y;
					if (dx * dx + dy * dy <= squared) Union(parent, i, j);
				}
			}
			return points.GroupBy(point => Find(parent, point.Index)).Select(group => group.ToList()).ToList();
		}

		private static SourceWithPoint SelectSource(List<SourceWithPoint> group, string mode)
		{
			if (string.Equals(mode, "PreferCam1", StringComparison.OrdinalIgnoreCase))
			{
				return group.OrderBy(x => string.Equals(x.Source.Camera, "Cam1", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
					.ThenBy(x => CaptureSortValue(x.Source)).ThenBy(x => x.Source.CaptureOrder)
					.ThenBy(x => x.Source.VppSequence).First();
			}
			return group.OrderBy(x => CaptureSortValue(x.Source)).ThenBy(x => x.Source.CaptureOrder)
				.ThenBy(x => x.Source.VppSequence).First();
		}

		private static DateTime CaptureSortValue(CellPointSource source)
		{
			DateTime time;
			return DateTime.TryParseExact(source == null ? null : source.CaptureTime,
				"yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out time) ? time : DateTime.MaxValue;
		}

		private static void ApplyTriangleOrder(List<FinalCellRecord> cells, CellInspectionSettings settings)
		{
			double tolerance = settings.RowToleranceMm > 0 ? settings.RowToleranceMm : 1.0;
			var byY = cells.OrderBy(cell => cell.SelectedSource.Y).ToList();
			var rowsAscending = new List<List<FinalCellRecord>>();
			var rowMeans = new List<double>();
			foreach (FinalCellRecord cell in byY)
			{
				int nearest = -1;
				double nearestDistance = double.MaxValue;
				for (int i = 0; i < rowMeans.Count; i++)
				{
					double distance = Math.Abs(cell.SelectedSource.Y - rowMeans[i]);
					if (distance <= tolerance && distance < nearestDistance) { nearest = i; nearestDistance = distance; }
				}
				if (nearest < 0)
				{
					rowsAscending.Add(new List<FinalCellRecord> { cell });
					rowMeans.Add(cell.SelectedSource.Y);
				}
				else
				{
					rowsAscending[nearest].Add(cell);
					rowMeans[nearest] = rowsAscending[nearest].Average(x => x.SelectedSource.Y);
				}
			}

			var rowPairs = rowsAscending.Select((row, index) => new { Row = row, Mean = rowMeans[index] });
			List<List<FinalCellRecord>> rows = (settings.BottomIsMinimumY
				? rowPairs.OrderBy(x => x.Mean)
				: rowPairs.OrderByDescending(x => x.Mean))
				.Select(x => settings.RightIsMaximumX
					? x.Row.OrderByDescending(c => c.SelectedSource.X).ToList()
					: x.Row.OrderBy(c => c.SelectedSource.X).ToList())
				.ToList();

			int[] positions = new int[rows.Count];
			int sequence = 1;
			int band = 0;
			while (positions.Select((position, row) => position < rows[row].Count).Any(has => has))
			{
				bool consumed = false;
				for (int row = 0; row < rows.Count; row++)
				{
					int take = 1 + ((band + row) % 2);
					for (int n = 0; n < take && positions[row] < rows[row].Count; n++)
					{
						FinalCellRecord cell = rows[row][positions[row]];
						cell.Sequence = sequence++;
						cell.Row = row + 1;
						cell.RowPosition = positions[row] + 1;
						cell.TriangleBand = band + 1;
						positions[row]++;
						consumed = true;
					}
				}
				if (!consumed) break;
				band++;
			}
		}

		private static SessionState GetOrCreateSession(string job, string partNumber)
		{
			string key = SessionKey(job, partNumber);
			return Sessions.GetOrAdd(key, delegate
			{
				string partRoot = Path.Combine(RootDirectory, "Sessions", SafeName(job), SafeName(partNumber) + "_" + PartHash(partNumber));
				Directory.CreateDirectory(partRoot);
                string existing = Path.Combine(partRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(existing);
				int maxOrder = 0;
				string raw = Path.Combine(existing, "Raw");
				if (Directory.Exists(raw))
				{
					foreach (string file in Directory.GetFiles(raw, "*.xml"))
					{
						try { maxOrder = Math.Max(maxOrder, Deserialize<CellCaptureRecord>(file).CaptureOrder); } catch { }
					}
				}
				return new SessionState { Directory = existing, Job = job ?? "", PartNumber = partNumber.Trim(), NextCaptureOrder = maxOrder };
			});
		}

		private static string PartHash(string value) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value ?? ""))).Replace("-", "").Substring(0, 12); }

        private static string SessionKey(string job, string partNumber)
		{
			return (job ?? "") + "|" + (partNumber ?? "").Trim();
		}

		private static string SafeName(string value)
		{
			string text = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
			foreach (char invalid in Path.GetInvalidFileNameChars()) text = text.Replace(invalid, '_');
			text = text.Length > 24 ? text.Substring(0, 24) : text;
            return text == "." || text == ".." ? "_" + text : text;
		}

		private static bool TryParseDouble(string value, out double result)
		{
			bool parsed = double.TryParse((value ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
				double.TryParse((value ?? "").Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out result);
            return parsed && !double.IsNaN(result) && !double.IsInfinity(result);
		}

		private static int Find(int[] parent, int index)
		{
			while (parent[index] != index) { parent[index] = parent[parent[index]]; index = parent[index]; }
			return index;
		}

		private static void Union(int[] parent, int left, int right)
		{
			int a = Find(parent, left), b = Find(parent, right);
			if (a != b) parent[b] = a;
		}

		private static T Deserialize<T>(string path)
		{
			using (FileStream stream = File.OpenRead(path))
				return (T)new XmlSerializer(typeof(T)).Deserialize(stream);
		}

        private static void SerializeAtomic<T>(T value, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".write-" + Guid.NewGuid().ToString("N").Substring(0, 12) + ".tmp");
            try {
                using (var stream = File.Create(temporary)) new XmlSerializer(typeof(T)).Serialize(stream, value);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
	}
}


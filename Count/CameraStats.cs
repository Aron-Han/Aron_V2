using Aron_V2;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml.Linq;

public sealed class CameraStats : INotifyPropertyChanged
{
	private readonly string _key;

	private int _pass;
	private int _total;

	public string Pass => $"Pass：{_pass:0}";
	public string Total => $"Total：{_total:0}";
	public double PassRate => _total > 0 ? (double)_pass / _total * 100.0 : 0.0;
	public string PassRateText => $"PassRate：{PassRate:0.00}%";

	public event PropertyChangedEventHandler PropertyChanged;

	public CameraStats(string key)
	{
		_key = string.IsNullOrWhiteSpace(key) ? "Unknown" : key;

		int pass;
		int total;
		CameraStatsStorage.Load(_key, out pass, out total);

		_pass = pass;
		_total = total;
	}

	public void Increment(bool isOk)
	{
		if (isOk) Interlocked.Increment(ref _pass);
		Interlocked.Increment(ref _total);

		CameraStatsStorage.Save(_key, _pass, _total);

		UI.Post(() =>
		{
			OnPropertyChanged(nameof(Pass));
			OnPropertyChanged(nameof(Total));
			OnPropertyChanged(nameof(PassRate));
			OnPropertyChanged(nameof(PassRateText));
		});
	}

	public void Reset()
	{
		Interlocked.Exchange(ref _pass, 0);
		Interlocked.Exchange(ref _total, 0);

		CameraStatsStorage.Save(_key, 0, 0);

		UI.Post(() =>
		{
			OnPropertyChanged(nameof(Pass));
			OnPropertyChanged(nameof(Total));
			OnPropertyChanged(nameof(PassRate));
			OnPropertyChanged(nameof(PassRateText));
		});
	}

	private void OnPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}

public static class CameraStatsStorage
{
	private static readonly object _lock = new object();

	private static string StatsPath
	{
		get
		{
			string dir = Global.ProjectStatsDir;
			return Path.Combine(dir, "CameraStats.xml");
		}
	}

	public static void Load(string key, out int pass, out int total)
	{
		pass = 0;
		total = 0;

		lock (_lock)
		{
			try
			{
				if (!File.Exists(StatsPath))
					return;

				var doc = XDocument.Load(StatsPath);
				var cam = doc.Root
					.Elements("Camera")
					.FirstOrDefault(x => string.Equals((string)x.Attribute("Key"), key, StringComparison.OrdinalIgnoreCase));

				if (cam == null)
					return;

				int.TryParse((string)cam.Attribute("Pass"), out pass);
				int.TryParse((string)cam.Attribute("Total"), out total);
			}
			catch
			{
				pass = 0;
				total = 0;
			}
		}
	}

	public static void Save(string key, int pass, int total)
	{
		lock (_lock)
		{
			try
			{
				XDocument doc;

				if (File.Exists(StatsPath))
				{
					doc = XDocument.Load(StatsPath);
				}
				else
				{
					doc = new XDocument(new XElement("CameraStats"));
				}

				if (doc.Root == null)
					doc.Add(new XElement("CameraStats"));

				var cam = doc.Root
					.Elements("Camera")
					.FirstOrDefault(x => string.Equals((string)x.Attribute("Key"), key, StringComparison.OrdinalIgnoreCase));

				if (cam == null)
				{
					cam = new XElement("Camera",
						new XAttribute("Key", key),
						new XAttribute("Pass", pass),
						new XAttribute("Total", total));

					doc.Root.Add(cam);
				}
				else
				{
					cam.SetAttributeValue("Pass", pass);
					cam.SetAttributeValue("Total", total);
				}

				doc.Save(StatsPath);
			}
			catch
			{
				// 统计保存失败不建议影响主流程
			}
		}
	}
}
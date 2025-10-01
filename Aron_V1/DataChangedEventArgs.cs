using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V1
{
	public class DataChangedEventArgs : EventArgs
	{
		public string PropertyName { get; set; }
	}
	public static class GlobalData
	{
		private static string _statusJobID;
		private static string _statusPosition;
		private static int _progress;

		public static event EventHandler<DataChangedEventArgs> DataChanged;

		public static string statusJobID
		{
			get => _statusJobID;
			set
			{
				if (_statusJobID != value)
				{
					_statusJobID = value;
					OnDataChanged(nameof(statusJobID));
				}
			}
		}

		public static string statusPosition
		{
			get => _statusPosition;
			set
			{
				if (_statusPosition != value)
				{
					_statusPosition = value;
					OnDataChanged(nameof(statusPosition));
				}
			}
		}

		public static int Progress
		{
			get => _progress;
			set
			{
				if (_progress != value)
				{
					_progress = value;
					OnDataChanged(nameof(Progress));
				}
			}
		}

		private static void OnDataChanged(string propertyName)
		{
			DataChanged?.Invoke(null, new DataChangedEventArgs { PropertyName = propertyName });
		}
	}
}

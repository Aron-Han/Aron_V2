using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public sealed class ProgressInfo
	{
		public int Percent { get; private set; }
		public string Message { get; private set; }

		public ProgressInfo(int percent, string message)
		{
			Percent = percent;
			Message = message;
		}
	}
}

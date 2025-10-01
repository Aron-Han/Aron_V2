using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public class CamRunResult
	{
		public string CamName;
		public string ResultTotal;
		public override string ToString()
		{
			// 你想要的格式可以自己改
			return $"[{CamName}]{ResultTotal}";
		}
	}
}

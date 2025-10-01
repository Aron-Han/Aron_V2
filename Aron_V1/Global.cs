using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;

namespace Aron_V1
{
	public static class Global
	{
		public static int Model_JobID { get; set; }
		public static string Position_ID { get; set; }
		public static string basePath = AppDomain.CurrentDomain.BaseDirectory;//Get the root directory of the current application

		public static string Vpp_Root_Cam1 = basePath + "VPP\\Cam1\\";
		public static string Vpp_Root_Cam2 = basePath + "VPP\\Cam2\\";
		public static string Camera_Root_Cam1 = basePath + "Camera\\Cam";
		public static string CsvPath = basePath + "CSV\\data.csv";
		public static string dBPath = basePath + "mydata.db";
		public static string UserAccessPath = basePath + "CSV\\UserAccess.xml";

		#region Cognex 
		public static CogAcqFifoTool Camera_Tool_Cam1;
		public static CogAcqFifoTool Camera_Tool_Cam2;
		public static CogToolBlock Vpp_Tool_Cam1;
		public static CogToolBlock Vpp_Tool_Cam2;

		#endregion


		#region event
		public static event EventHandler StatusTextChanged;
		private static string _StatusStrips_JobID;
		public static string StatusStrips_JobID
		{
			get => _StatusStrips_JobID;
			set
			{
				if (_StatusStrips_JobID != value)
				{
					_StatusStrips_JobID = value;
					StatusTextChanged?.Invoke(null, EventArgs.Empty); 
				}
			}
		}
		#endregion
	}
}

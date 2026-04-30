using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    public static class Global
    {

        public static string basePath = AppDomain.CurrentDomain.BaseDirectory;//Get the root directory of the current application
		public static Dictionary<string, CameraGeneralRuntime> CamGeneral
	   = new Dictionary<string, CameraGeneralRuntime>(StringComparer.OrdinalIgnoreCase);


		public static string Vpp_Root = basePath + "Project\\VPP\\";
        public static string CogfigSaveImage = basePath + "Project\\Config\\ConfigSaveImage.xml";
        public static string LogMangerPath = basePath + "Log\\";
		public static string VppOutputCfgPath = basePath + "Project\\Config\\VPPOutput.xml";
		public static string ParameterCogfig = basePath + "Project\\Config\\Parameter.xml";
        
		public static string UserAccessPath = basePath + "Project\\Config\\UserAccess.xml";
        public static string PermissionsPath = basePath + "Project\\Config\\Permissions.xml";
		public static string Save_Image_Root = @"C:\image\";

		public static int maxLines_Richbox { get; set; }

        public static string currentUser { get; set; }

		public static string Replay_Send_Data { get; set; }//回放图片寄存的检测数据作为相机间传递

		public static string dBPath
		{
			get
			{
				return System.IO.Path.Combine(ProjectDatabaseDir, "VisionData.db");
			}
		}
		#region [Camera config]
		public static int CamN_Use { get; set; }//number of Cam to use

        public static string[] Engine_Used = new string[] { "0", "0", "0", "0" };
        #endregion

        #region [Profinet]
        public static byte[] Result_Send = new byte[240];
        public static string[] Model_JobID = new string[] { "Job1", "Job1", "Job1", "Job1" }; //JobID
        public static string[] Position_ID = new string[] { "1", "1", "1", "1" }; //PosID
        public static string[] Model_JobID_Send = new string[] { "Job0", "Job0", "Job0", "Job0" };//Send to PLC
        public static string[] Position_ID_Send = new string[] { "0", "0", "0", "0" };  //Send to PLC
        public static string[] PartCode = new string[] { "0", "0","0","0" };              //SN
        public static int Input_Length {  get; set; }

		#endregion

		#region[Lock]
		public static readonly object PlcBufferLock = new object();
		public static readonly object SaveImage = new object();

		#endregion

		#region [Inspection]
		public static string[] ResultX_Cam = new string[] { "0", "0", "0", "0" };
        public static string[] ResultY_Cam = new string[] { "0", "0", "0", "0" };
        public static string[] ResultA_Cam = new string[] { "0", "0", "0", "0" };
        public static string[] ResultTotal_Cam = new string[] { "0", "0", "0", "0" };
        public static string[] Result_data_send {  get; set; }//传递给主相机的副相机汇总结果 
        public static bool[] Manual_Trigger_Lock { get; set; }
        public static AppConfig CurrentConfig;
        public static int[] Count_Pass {  get; set; }
        public static int[] Count_Total {  get; set; }





		#endregion

		#region [Cognex]
		public static CogAcqFifoTool[] Camera_Acq_tool { get; set; }
        public static Bitmap imageRecord;
        public static ICogImage image_Replay;


		#endregion

		#region

		public static string FTP_Host { get; set; }
		public static string FTP_User { get; set; }
		public static string FTP_Password { get; set; }
		public static string FTP_Root { get; set; }

		#endregion



		public static string ProjectDir
		{
			get
			{
				string dir = System.IO.Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					"Project");

				if (!System.IO.Directory.Exists(dir))
					System.IO.Directory.CreateDirectory(dir);

				return dir;
			}
		}

		public static string ProjectConfigDir
		{
			get
			{
				string dir = System.IO.Path.Combine(ProjectDir, "Config");

				if (!System.IO.Directory.Exists(dir))
					System.IO.Directory.CreateDirectory(dir);

				return dir;
			}
		}

		public static string ProjectDatabaseDir
		{
			get
			{
				string dir = System.IO.Path.Combine(ProjectDir, "Database");

				if (!System.IO.Directory.Exists(dir))
					System.IO.Directory.CreateDirectory(dir);

				return dir;
			}
		}

		public static string ProjectStatsDir
		{
			get
			{
				string dir = System.IO.Path.Combine(ProjectDir, "Stats");

				if (!System.IO.Directory.Exists(dir))
					System.IO.Directory.CreateDirectory(dir);

				return dir;
			}
		}
	}

	public class CameraGeneralRuntime
	{
		public string Exposure { get; set; }
		public string MainUsed { get; set; }
        public string MainChannel { get; set; }
		public string SecondUsed { get; set; }
		public string SecondChannel { get; set; }
	}

	public static class UI
	{
		public static SynchronizationContext Context { get; private set; }

		public static void Init(Control anyUiControl)
		{
			// 在 UI 线程调用
			Context = SynchronizationContext.Current
					  ?? new WindowsFormsSynchronizationContext();
		}

		public static void Post(Action a)
		{
			var ctx = Context;
			if (ctx == null) { a(); return; }   // 兜底
			ctx.Post(_ => a(), null);
		}
	}


}

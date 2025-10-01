using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;

namespace Aron_V1
{
	public static class Load_Job
	{
		
		public static void LoadVPP(int JobID, string CamN)
		{
			try
			{
				switch (CamN)
				{
					case "1":
						Global.Vpp_Tool_Cam1 = (CogToolBlock)CogSerializer.LoadObjectFromFile(Global.Vpp_Root_Cam1 + JobID.ToString() + "\\Inspection.VPP");
						Global.Camera_Tool_Cam1 = (CogAcqFifoTool)CogSerializer.LoadObjectFromFile(Global.Vpp_Root_Cam1 + JobID.ToString() + "\\Camera.VPP");
						Global.Model_JobID = JobID;
						Global.StatusStrips_JobID = JobID.ToString();
						break;
					case "2":
						Global.Vpp_Tool_Cam2 = (CogToolBlock)CogSerializer.LoadObjectFromFile(Global.Vpp_Root_Cam2 + JobID.ToString() + "\\Inspection.VPP");
						Global.Camera_Tool_Cam2 = (CogAcqFifoTool)CogSerializer.LoadObjectFromFile(Global.Vpp_Root_Cam2 + JobID.ToString() + "\\Camera.VPP");
						Global.Model_JobID = JobID;
						Global.StatusStrips_JobID = JobID.ToString();
						break;
				}
			}
			catch (Exception e)
			{
				MessageBox.Show("ChangeJob Failed:" + e.Message);
			}
		}
	}
}

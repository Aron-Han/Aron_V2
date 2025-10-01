using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;

namespace Aron_V1
{
	public partial class FormMain : Form
	{
		Camera camera = new Camera();
		Algorithm algorithm = new Algorithm();


		public FormMain()
		{
			InitializeComponent();
			GlobalData.DataChanged += GlobalData_DataChanged;
		}

		#region Button
		private void cameraToolStripMenuItem_Click(object sender, EventArgs e)
		{
			camera.ShowDialog();
			camera.Hide();
		}

		private void algorithmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			algorithm.ShowDialog();
			algorithm.Hide();
		}
		#endregion

		#region Event
		private void FormMain_Load(object sender, EventArgs e)
		{
			this.WindowState = FormWindowState.Maximized;
			ini_Status.ini();
			Load_Job.LoadVPP(Global.Model_JobID, "1");
		}

		private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			Global.Camera_Tool_Cam1.Dispose();
		}
		#endregion

		#region Update UI
		private void GlobalData_DataChanged(object sender, DataChangedEventArgs e)
		{
			if (this.InvokeRequired)
			{
				this.Invoke(new Action(() => UpdateUI(e.PropertyName)));
			}
			else
			{
				UpdateUI(e.PropertyName);
			}
		}
		private void UpdateUI(string propertyName)
		{
			switch (propertyName)
			{
				case nameof(GlobalData.statusJobID):
					toolStripStatusLabel1.Text = "JobID:" + GlobalData.statusJobID;
					break;

				case nameof(GlobalData.statusPosition):
					toolStripStatusLabel2.Text = "Position:" + GlobalData.statusPosition;
					break;
			}
		}
		#endregion
	}
}

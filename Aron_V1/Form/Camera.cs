using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V1
{
	public partial class Camera : Form
	{
		public Camera()
		{
			InitializeComponent();			
		}

		

		private void Camera_FormClosing(object sender, FormClosingEventArgs e)
		{
		}

		private void Camera_Load(object sender, EventArgs e)
		{
			this.cogAcqFifoEditV21.Subject = Global.Camera_Tool_Cam1;
		}
	}
}

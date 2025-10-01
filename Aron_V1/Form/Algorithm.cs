using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V1
{
	public partial class Algorithm : Form
	{
		public Algorithm()
		{
			InitializeComponent();
		}

		private void Algorithm_Load(object sender, EventArgs e)
		{
			this.cogToolBlockEditV21.Subject = Global.Vpp_Tool_Cam1;
		}
	}
}

using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Aron_V2.Load_Job;

namespace Aron_V2
{
	public partial class Algorithm : Form
	{
		string Path;
		private AppConfig _config;

		public Algorithm(AppConfig config)
		{
			InitializeComponent();
			_config = config;
		}

		private void Algorithm_Load(object sender, EventArgs e)
		{
			FillJobs();
			if (Cbo_JobID.Items.Count > 0)
				Cbo_JobID.SelectedIndex = 0;  // 触发联动
		}

		private void Btn_Save_Click(object sender, EventArgs e)
		{
			try
			{
				Path = Global.Vpp_Root + "\\" + this.Cbo_JobID.SelectedItem.ToString() + "\\" + this.Cbo_Camera.SelectedItem.ToString() + "\\" + this.Cbo_Position.SelectedItem.ToString() + "\\Inspection.vpp";
				CogSerializer.SaveObjectToFile(this.cogToolBlockEditV21.Subject, Path);
				LogChangeEventArgs.Set("Log", "Save Successed , " + this.Cbo_Camera.SelectedItem.ToString() + " " + this.Cbo_JobID.SelectedItem.ToString(), Color.Green);
			}
			catch (Exception m)
			{
				LogChangeEventArgs.Set("Log", "SaveJob Failed:" + m.Message, Color.Red);
			}
		}


		private void Cbo_JobID_SelectedIndexChanged(object sender, EventArgs e)
		{
			FillChannel();
			FillCams();			
			FillPositions();
		}

		private void Cbo_Camera_SelectedValueChanged(object sender, EventArgs e)
		{			
			FillPositions();
			RefreshTool();
		}

		private void Cbo_Position_SelectedIndexChanged(object sender, EventArgs e)
		{			
			RefreshTool();
		}

		private void Cbo_Channel_SelectedIndexChanged(object sender, EventArgs e)
		{
			RefreshTool();
		}

		private void FillJobs()
		{
			Cbo_JobID.Items.Clear();
			if (_config != null && _config.Models != null)
			{
				foreach (var m in _config.Models)
					Cbo_JobID.Items.Add(m.Name);
			}
		}

		private void FillCams()
		{
			Cbo_Camera.Items.Clear();
			var jobName = Cbo_JobID.SelectedItem as string;
			if (string.IsNullOrEmpty(jobName) || _config == null || _config.Models == null) return;

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (model == null || model.Cameras == null) return;

			foreach (var cam in model.Cameras)
				Cbo_Camera.Items.Add(cam.Name);
			if (Cbo_Camera.Items.Count > 0)
				Cbo_Camera.SelectedIndex = 0;
		}


		private void FillPositions()
		{
			Cbo_Position.Items.Clear();
			var jobName = Cbo_JobID.SelectedItem as string;
			var camName = Cbo_Camera.SelectedItem as string;
			if (string.IsNullOrEmpty(jobName) || string.IsNullOrEmpty(camName)) return;

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (model == null || model.Cameras == null) return;

			var cam = model.Cameras.FirstOrDefault(c =>
				string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));
			if (cam == null || cam.Positions == null) return;

			foreach (var pos in cam.Positions)
				Cbo_Position.Items.Add(pos.Name);

			if (Cbo_Position.Items.Count > 0)
				Cbo_Position.SelectedIndex = 0;
		}

		private void FillChannel()
		{
			Cbo_Channel.Items.Clear();

			var jobName = Cbo_JobID.SelectedItem as string;
			if (_config == null || _config.Models == null || string.IsNullOrEmpty(jobName))
				return;

			var model = _config.Models
				.FirstOrDefault(m => string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));
			if (model == null || model.Cameras == null)
				return;

			var used = new HashSet<int>();
			foreach (var cam in model.Cameras)
			{
				if (cam == null || cam.Positions == null) continue;

				foreach (var pos in cam.Positions)
				{
					if (pos == null || pos.General == null) continue;

					int mainUsed, mainCh, secondUsed, secondCh;
					int.TryParse(pos.General.MainUsed, out mainUsed);
					int.TryParse(pos.General.MainChannel, out mainCh);
					int.TryParse(pos.General.SecondUsed, out secondUsed);
					int.TryParse(pos.General.SecondChannel, out secondCh);

					if (mainUsed == 1 && mainCh >= 0 && mainCh < 4) used.Add(mainCh);
					if (secondUsed == 1 && secondCh >= 0 && secondCh < 4) used.Add(secondCh);
				}
			}

			if (used.Count > 0)
			{
				foreach (var ch in used.OrderBy(x => x))
					Cbo_Channel.Items.Add(ch.ToString());
			}
			else
			{
				// 没配置到任何通道 → 回退展示 0~3
				for (int i = 0; i < 4; i++)
					Cbo_Channel.Items.Add(i.ToString());
			}

			if (Cbo_Channel.Items.Count > 0)
				Cbo_Channel.SelectedIndex = 0;
		}

		private void RefreshTool()
		{
			Load_Job.VppCache.TryGetTb(Cbo_JobID.SelectedItem as string, int.Parse(Cbo_Channel.SelectedItem as string), Cbo_Camera.SelectedItem as string, Cbo_Position.SelectedItem as string, out var VPP);
			this.cogToolBlockEditV21.Subject = VPP;
		}

		private void Btn_Load_Click(object sender, EventArgs e)
		{
			try
			{				
				Load_Job.LoadVPP_ForChannel_AllPos(int.Parse(Cbo_Channel.SelectedItem.ToString()), Cbo_JobID.SelectedItem.ToString());
				this.Close();
			}
			catch (Exception m)
			{
				LogChangeEventArgs.Set("Log", "ChangeJob Failed:" + m.Message, Color.Red);
			}


		}

		
	}
}

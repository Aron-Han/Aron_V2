using Aron_V2.UI_Update;
using Cognex.VisionPro;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
	public partial class Camera : Form
	{
		private AppConfig _config;
		private bool _loading = false;

		public Camera(AppConfig config)
		{
			InitializeComponent();
			_config = config;

			// 如果 Designer 里面已经绑定过这个事件，可以保留；最多只是多刷新一次。
			Cbo_Channel.SelectedIndexChanged += Cbo_Channel_SelectedIndexChanged;
		}

		private void Camera_Load(object sender, EventArgs e)
		{
			_loading = true;
			try
			{
				FillJobs();

				if (Cbo_JobID.Items.Count > 0)
					Cbo_JobID.SelectedIndex = 0;
			}
			finally
			{
				_loading = false;
			}

			FillCams();
		}

		private void Btn_Save_Click(object sender, EventArgs e)
		{
			try
			{
				string jobName = Cbo_JobID.SelectedItem as string;
				string camName = Cbo_Camera.SelectedItem as string;
				string posName = Cbo_Position.SelectedItem as string;
				string chText = Cbo_Channel.SelectedItem as string;

				if (string.IsNullOrWhiteSpace(jobName) ||
					string.IsNullOrWhiteSpace(camName) ||
					string.IsNullOrWhiteSpace(posName) ||
					string.IsNullOrWhiteSpace(chText))
				{
					MessageBox.Show("Job / Camera / Position / Channel should not empty。", "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				int channel;
				if (!int.TryParse(chText, out channel))
				{
					MessageBox.Show("Channel invalid：" + chText, "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (this.cogAcqFifoEditV21.Subject == null)
				{
					MessageBox.Show("Currently not loaded to the camera VPP, cannot be saved. Please check if the Job / Camera / Position / Channel match.",
						"Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (string.IsNullOrWhiteSpace(Global.Vpp_Root))
				{
					MessageBox.Show("Global.Vpp_Root is empty, unable to determine the save path。", "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				string dir = System.IO.Path.Combine(Global.Vpp_Root, jobName, camName, posName);
				System.IO.Directory.CreateDirectory(dir);

				string savePath = System.IO.Path.Combine(dir, "Camera.vpp");
				string tempPath = savePath + ".tmp";
				string bakPath = savePath + ".bak";

				CogSerializer.SaveObjectToFile(this.cogAcqFifoEditV21.Subject, tempPath);

				if (System.IO.File.Exists(savePath))
					System.IO.File.Copy(savePath, bakPath, true);

				System.IO.File.Copy(tempPath, savePath, true);
				System.IO.File.Delete(tempPath);

				LogChangeEventArgs.Set("Log",
					"Save Camera VPP Successed, Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel +
					" Path:" + savePath,
					Color.Green);
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Save Camera VPP Failed: " + ex.Message, Color.Red);
				MessageBox.Show("Save Camera VPP Failed:\r\n" + ex.Message, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void Cbo_JobID_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loading) return;

			_loading = true;
			try
			{
				FillCams();
			}
			finally
			{
				_loading = false;
			}

			FillPositions();
		}

		private void Cbo_Cam_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loading) return;

			_loading = true;
			try
			{
				FillPositions();
			}
			finally
			{
				_loading = false;
			}

			FillChannelForSelectedPosition();
			RefreshTool();
		}

		private void Cbo_Position_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loading) return;

			FillChannelForSelectedPosition();
			RefreshTool();
		}

		private void Cbo_Channel_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loading) return;
			RefreshTool();
		}

		private void FillJobs()
		{
			Cbo_JobID.Items.Clear();

			if (_config != null && _config.Models != null)
			{
				foreach (var m in _config.Models)
				{
					if (m == null || string.IsNullOrWhiteSpace(m.Name)) continue;
					Cbo_JobID.Items.Add(m.Name);
				}
			}
		}

		private void FillCams()
		{
			Cbo_Camera.Items.Clear();
			Cbo_Position.Items.Clear();
			Cbo_Channel.Items.Clear();
			cogAcqFifoEditV21.Subject = null;

			var jobName = Cbo_JobID.SelectedItem as string;
			if (string.IsNullOrEmpty(jobName) || _config == null || _config.Models == null) return;

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));

			if (model == null || model.Cameras == null) return;

			foreach (var cam in model.Cameras)
			{
				if (cam == null || string.IsNullOrWhiteSpace(cam.Name)) continue;
				Cbo_Camera.Items.Add(cam.Name);
			}

			if (Cbo_Camera.Items.Count > 0)
				Cbo_Camera.SelectedIndex = 0;
		}

		private void FillPositions()
		{
			Cbo_Position.Items.Clear();
			Cbo_Channel.Items.Clear();
			cogAcqFifoEditV21.Subject = null;

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
			{
				if (pos == null || string.IsNullOrWhiteSpace(pos.Name)) continue;
				Cbo_Position.Items.Add(pos.Name);
			}

			if (Cbo_Position.Items.Count > 0)
				Cbo_Position.SelectedIndex = 0;
		}

		private void FillChannelForSelectedPosition()
		{
			Cbo_Channel.Items.Clear();

			var jobName = Cbo_JobID.SelectedItem as string;
			var camName = Cbo_Camera.SelectedItem as string;
			var posName = Cbo_Position.SelectedItem as string;

			if (string.IsNullOrEmpty(jobName) ||
				string.IsNullOrEmpty(camName) ||
				string.IsNullOrEmpty(posName) ||
				_config == null ||
				_config.Models == null)
			{
				return;
			}

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));

			if (model == null || model.Cameras == null) return;

			var cam = model.Cameras.FirstOrDefault(c =>
				string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));

			if (cam == null || cam.Positions == null) return;

			var pos = cam.Positions.FirstOrDefault(p =>
				string.Equals(p.Name, posName, StringComparison.OrdinalIgnoreCase));

			if (pos == null || pos.General == null) return;

			var used = new HashSet<int>();

			int mainUsed, mainCh, secondUsed, secondCh;

			int.TryParse(pos.General.MainUsed, out mainUsed);
			int.TryParse(pos.General.MainChannel, out mainCh);
			int.TryParse(pos.General.SecondUsed, out secondUsed);
			int.TryParse(pos.General.SecondChannel, out secondCh);

			if (mainUsed == 1 && mainCh >= 0 && mainCh < 4)
				used.Add(mainCh);

			if (secondUsed == 1 && secondCh >= 0 && secondCh < 4)
				used.Add(secondCh);

			if (used.Count > 0)
			{
				foreach (var ch in used.OrderBy(x => x))
					Cbo_Channel.Items.Add(ch.ToString());
			}
			else
			{
				for (int i = 0; i < 4; i++)
					Cbo_Channel.Items.Add(i.ToString());
			}

			if (Cbo_Channel.Items.Count > 0)
				Cbo_Channel.SelectedIndex = 0;
		}

		private void RefreshTool()
		{
			string jobName = Cbo_JobID.SelectedItem as string;
			string camName = Cbo_Camera.SelectedItem as string;
			string posName = Cbo_Position.SelectedItem as string;
			string chText = Cbo_Channel.SelectedItem as string;

			cogAcqFifoEditV21.Subject = null;

			if (string.IsNullOrWhiteSpace(jobName) ||
				string.IsNullOrWhiteSpace(camName) ||
				string.IsNullOrWhiteSpace(posName) ||
				string.IsNullOrWhiteSpace(chText))
			{
				return;
			}

			int channel;
			if (!int.TryParse(chText, out channel))
			{
				LogChangeEventArgs.Set("Log", "Load Camera VPP Failed: Channel invalid: " + chText, Color.Red);
				return;
			}

			CogAcqFifoTool acq;
			bool ok = Load_Job.VppCache.TryGetAcq(jobName, channel, camName, posName, out acq);

			if (!ok || acq == null)
			{
				LogChangeEventArgs.Set("Log",
					"Load Camera VPP Failed: cache not found. Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel,
					Color.Red);
				return;
			}

			this.cogAcqFifoEditV21.Subject = acq;

			LogChangeEventArgs.Set("Log",
				"Load Camera VPP OK. Job:" + jobName +
				" Cam:" + camName +
				" Pos:" + posName +
				" Ch:" + channel,
				Color.Black);
		}
	}
}

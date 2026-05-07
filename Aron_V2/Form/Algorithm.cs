using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static Aron_V2.Load_Job;

namespace Aron_V2
{
	public partial class Algorithm : Form
	{
		private AppConfig _config;

		// 下拉框联动时置 true，避免 SelectedIndexChanged 连续触发导致 VPP 控件被重复刷新
		private bool _loading = false;

		public Algorithm(AppConfig config)
		{
			InitializeComponent();
			_config = config;

			// 如果 Designer 里面已经绑定过事件，先 -= 再 +=，避免重复绑定
			Cbo_Channel.SelectedIndexChanged -= Cbo_Channel_SelectedIndexChanged;
			Cbo_Channel.SelectedIndexChanged += Cbo_Channel_SelectedIndexChanged;
		}

		private void Algorithm_Load(object sender, EventArgs e)
		{
			_loading = true;
			try
			{
				FillJobs();

				if (Cbo_JobID.Items.Count > 0)
					Cbo_JobID.SelectedIndex = 0;

				FillCams();
				FillPositions();
				FillChannelForCurrentPosition();
			}
			finally
			{
				_loading = false;
			}

			// 所有下拉框联动完成后，只刷新一次 VPP 控件
			RefreshTool();
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
					MessageBox.Show("Job / Camera / Position / Channel cannot be empty.", "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				int channel;
				if (!int.TryParse(chText, out channel))
				{
					MessageBox.Show("Invalid channel: " + chText, "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (this.cogToolBlockEditV21.Subject == null)
				{
					MessageBox.Show("No inspection VPP is currently loaded, so it cannot be saved.\r\nPlease check whether Job / Camera / Position / Channel match.",
						"Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (string.IsNullOrWhiteSpace(Global.Vpp_Root))
				{
					MessageBox.Show("Global.Vpp_Root is empty. The save path cannot be determined.", "Save Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				string dir = System.IO.Path.Combine(Global.Vpp_Root, jobName, camName, posName);
				System.IO.Directory.CreateDirectory(dir);

				string savePath = System.IO.Path.Combine(dir, "Inspection.vpp");
				string tempPath = savePath + ".tmp";
				string bakPath = savePath + ".bak";

				// 先保存临时文件，成功后再覆盖正式文件，避免保存中断导致原文件损坏
				CogSerializer.SaveObjectToFile(this.cogToolBlockEditV21.Subject, tempPath);

				if (System.IO.File.Exists(savePath))
					System.IO.File.Copy(savePath, bakPath, true);

				System.IO.File.Copy(tempPath, savePath, true);
				System.IO.File.Delete(tempPath);

				LogChangeEventArgs.Set("Log",
					"Save Inspection VPP succeeded, Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel +
					" Path:" + savePath,
					Color.Green);
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Save Inspection VPP failed: " + ex.Message, Color.Red);
				MessageBox.Show("Save Inspection VPP failed:\r\n" + ex.Message, "Error",
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
				FillPositions();
				FillChannelForCurrentPosition();
			}
			finally
			{
				_loading = false;
			}

			RefreshTool();
		}

		private void Cbo_Camera_SelectedValueChanged(object sender, EventArgs e)
		{
			if (_loading) return;

			_loading = true;
			try
			{
				FillPositions();
				FillChannelForCurrentPosition();
			}
			finally
			{
				_loading = false;
			}

			RefreshTool();
		}

		private void Cbo_Position_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (_loading) return;

			_loading = true;
			try
			{
				FillChannelForCurrentPosition();
			}
			finally
			{
				_loading = false;
			}

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

			var jobName = Cbo_JobID.SelectedItem as string;
			if (string.IsNullOrEmpty(jobName) || _config == null || _config.Models == null)
				return;

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));

			if (model == null || model.Cameras == null)
				return;

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

			var jobName = Cbo_JobID.SelectedItem as string;
			var camName = Cbo_Camera.SelectedItem as string;

			if (string.IsNullOrEmpty(jobName) || string.IsNullOrEmpty(camName))
				return;

			if (_config == null || _config.Models == null)
				return;

			var model = _config.Models.FirstOrDefault(m =>
				string.Equals(m.Name, jobName, StringComparison.OrdinalIgnoreCase));

			if (model == null || model.Cameras == null)
				return;

			var cam = model.Cameras.FirstOrDefault(c =>
				string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));

			if (cam == null || cam.Positions == null)
				return;

			foreach (var pos in cam.Positions)
			{
				if (pos == null || string.IsNullOrWhiteSpace(pos.Name)) continue;
				Cbo_Position.Items.Add(pos.Name);
			}

			if (Cbo_Position.Items.Count > 0)
				Cbo_Position.SelectedIndex = 0;
		}

		// 关键：Channel 只根据当前 Job + Camera + Position 的 General 来填充
		// 不再把整个 Job 下所有 Channel 都放进去，避免选到错误通道导致 VPP 控件被清空。
		private void FillChannelForCurrentPosition()
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

			if (model == null || model.Cameras == null)
				return;

			var cam = model.Cameras.FirstOrDefault(c =>
				string.Equals(c.Name, camName, StringComparison.OrdinalIgnoreCase));

			if (cam == null || cam.Positions == null)
				return;

			var pos = cam.Positions.FirstOrDefault(p =>
				string.Equals(p.Name, posName, StringComparison.OrdinalIgnoreCase));

			if (pos == null || pos.General == null)
				return;

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
				// 如果当前 Pos 没配置通道，回退显示 0~3，方便手动检查
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

			// 不要在这里一开始就 Subject = null；
			// 否则下拉框联动时会导致 Cognex 编辑器信息一闪后被清空。
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
				LogChangeEventArgs.Set("Log", "Load Inspection VPP failed: invalid channel: " + chText, Color.Red);
				return;
			}

			CogToolBlock vpp;
			bool ok = Load_Job.VppCache.TryGetTb(jobName, channel, camName, posName, out vpp);

			if (!ok || vpp == null)
			{
				// 只有确认加载失败时才清空 Subject
				this.cogToolBlockEditV21.Subject = null;

				LogChangeEventArgs.Set("Log",
					"Load Inspection VPP failed: cache not found. Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel,
					Color.Red);
				return;
			}

			this.cogToolBlockEditV21.Subject = vpp;
			this.cogToolBlockEditV21.Refresh();
		}

		private void Btn_Load_Click(object sender, EventArgs e)
		{
			ReloadInspectionVppFromDisk();
		}

		private void ReloadInspectionVppFromDisk()
		{
			try
			{
				string jobName = Cbo_JobID.SelectedItem as string;
				string camName = Cbo_Camera.SelectedItem as string;
				string posName = Cbo_Position.SelectedItem as string;
				string chText = Cbo_Channel.SelectedItem as string;

				LogChangeEventArgs.Set("Log", "Algorithm Load button clicked.", Color.Black);

				if (string.IsNullOrWhiteSpace(jobName) ||
					string.IsNullOrWhiteSpace(camName) ||
					string.IsNullOrWhiteSpace(posName) ||
					string.IsNullOrWhiteSpace(chText))
				{
					MessageBox.Show("Job / Camera / Position / Channel cannot be empty.", "Load Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				int channel;
				if (!int.TryParse(chText, out channel))
				{
					MessageBox.Show("Invalid channel: " + chText, "Load Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (string.IsNullOrWhiteSpace(Global.Vpp_Root))
				{
					MessageBox.Show("Global.Vpp_Root is empty. The VPP path cannot be determined.", "Load Failed",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				string vppPath = System.IO.Path.Combine(
					Global.Vpp_Root,
					jobName,
					camName,
					posName,
					"Inspection.vpp");

				if (!System.IO.File.Exists(vppPath))
				{
					MessageBox.Show("Local Inspection.vpp was not found:\r\n" + vppPath,
						"Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				LogChangeEventArgs.Set("Log",
					"Local Inspection.vpp path: " + vppPath +
					"  LastWriteTime: " + System.IO.File.GetLastWriteTime(vppPath).ToString("yyyy-MM-dd HH:mm:ss"),
					Color.Black);

				DialogResult dr = MessageBox.Show(
					"Load will discard any unsaved algorithm changes and reload the local Inspection.vpp file.\r\n\r\nDo you want to continue?",
					"Reload Inspection VPP",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question,
					MessageBoxDefaultButton.Button2);

				if (dr != DialogResult.Yes)
					return;

				// 1) 直接从本地文件读取，不先走 VppCache，用于校验本地文件类型
				object obj = CogSerializer.LoadObjectFromFile(vppPath);
				CogToolBlock diskVpp = obj as CogToolBlock;

				if (diskVpp == null)
				{
					MessageBox.Show("The local file is not a CogToolBlock:\r\n" + vppPath,
						"Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				// 2) 先把编辑器绑定成本地文件直接读出来的对象，确保用户能看到磁盘文件内容
				this.cogToolBlockEditV21.Subject = null;
				Application.DoEvents();

				this.cogToolBlockEditV21.Subject = diskVpp;
				this.cogToolBlockEditV21.Refresh();

				// 3) 刷新主程序运行缓存
				// 注意：ReleaseAllVPP 会释放所有通道的对象，所以释放后必须把所有当前使用通道重新加载，
				// 否则其他通道触发时会拿到已 Dispose 的 CogAcqFifoTool / CogToolBlock。
				Load_Job.ReleaseAllVPP();
				ReloadRuntimeVppForAllActiveChannels(jobName, channel);

				// 4) 再从新的缓存里重新取当前算法对象
				CogToolBlock cacheVpp;
				bool ok = Load_Job.VppCache.TryGetTb(jobName, channel, camName, posName, out cacheVpp);

				if (!ok || cacheVpp == null)
				{
					LogChangeEventArgs.Set("Log",
						"Reload runtime cache failed, use disk Inspection VPP only. Job:" + jobName +
						" Cam:" + camName +
						" Pos:" + posName +
						" Ch:" + channel,
						Color.Orange);

					MessageBox.Show(
						"The local Inspection.vpp was loaded, but the runtime cache could not find the current algorithm VPP.\r\n\r\n" +
						"Job: " + jobName + "\r\n" +
						"Camera: " + camName + "\r\n" +
						"Position: " + posName + "\r\n" +
						"Channel: " + channel,
						"Load Warning",
						MessageBoxButtons.OK,
						MessageBoxIcon.Warning);

					return;
				}

				// 5) 最后窗口显示运行缓存对象，保证界面对象和主流程对象一致
				this.cogToolBlockEditV21.Subject = null;
				Application.DoEvents();

				this.cogToolBlockEditV21.Subject = cacheVpp;
				this.cogToolBlockEditV21.Refresh();

				LogChangeEventArgs.Set("Log",
					"Reload Inspection VPP From Disk complete, Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel +
					" Path:" + vppPath,
					Color.Green);
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Reload Inspection VPP failed: " + ex.Message, Color.Red);
				MessageBox.Show("Reload Inspection VPP failed:\r\n" + ex.Message,
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		private void ReloadRuntimeVppForAllActiveChannels(string selectedJobName, int selectedChannel)
		{
			try
			{
				for (int ch = 0; ch < 4; ch++)
				{
					string reloadJobName = null;

					try
					{
						if (Global.Model_JobID != null &&
							ch >= 0 &&
							ch < Global.Model_JobID.Length)
						{
							reloadJobName = Global.Model_JobID[ch];
						}
					}
					catch
					{
					}

					// 当前 Load 的通道，强制使用当前窗口选择的 Job
					if (ch == selectedChannel || string.IsNullOrWhiteSpace(reloadJobName))
						reloadJobName = selectedJobName;

					if (string.IsNullOrWhiteSpace(reloadJobName))
						continue;

					Load_Job.LoadVPP_ForChannel_AllPos(ch, reloadJobName);

					LogChangeEventArgs.Set("Log",
						"Reload runtime VPP OK, Ch:" + ch + " Job:" + reloadJobName,
						Color.Black);
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Reload runtime VPP for all active channels failed: " + ex.Message, Color.Red);
				throw;
			}
		}

	}
}

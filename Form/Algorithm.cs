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
        private readonly Panel _editorHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        private CogToolBlock _draft;
        private string[] _originalInputNames;

		public Algorithm(AppConfig config)
		{
			InitializeComponent();
            tableLayoutPanel1.Controls.Add(_editorHost, 0, 1);
            _editorHost.Controls.Add(new Label { Text = "Select Job / Camera / Position to load Inspection.vpp.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
			DesktopTheme.RemoveEditorChannelFromToolbar(tableLayoutPanel2, label4, Cbo_Channel);
			DesktopTheme.ApplySettings(this);
			_config = config;
            var shortcuts = new CheckedAlgorithmSaveShortcuts(this);
            Application.AddMessageFilter(shortcuts);
            Disposed += (s, e) => Application.RemoveMessageFilter(shortcuts);
            Disposed += (s, e) => { _draft?.Dispose(); _draft = null; };
			Load_Job.VppCache.LatestInspectionImageUpdated += LatestInspectionImageUpdated;
			Disposed += (s, e) => Load_Job.VppCache.LatestInspectionImageUpdated -= LatestInspectionImageUpdated;

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
            try { SaveInspectionVpp(); }
            catch (Exception ex) { ShowAlgorithmEditorFailure(ex); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void SaveInspectionVpp()
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

                if (this.cogToolBlockEditV21 == null || this.cogToolBlockEditV21.Subject == null)
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

                var draft = cogToolBlockEditV21.Subject;
                var required = RequiredOutputsForSave(jobName, camName, posName, draft);
                var errors = InspectionVppContract.Validate(draft, _originalInputNames, required);
                if (errors.Count > 0) throw new InvalidOperationException(InspectionVppContract.FailureMessage(errors));
                var channels = Load_Job.InspectionEditChannels(jobName, camName, posName,
                    Cbo_Channel.Items.Cast<object>().Select(item => int.Parse(item.ToString())).Concat(new[] { channel }));
                string savePath = System.IO.Path.Combine(Global.Vpp_Root, jobName, camName, posName, "Inspection.vpp");
                using (var gates = (Owner as FormMain)?.AcquireAlgorithmSaveGates(channels))
                using (var update = new Load_Job.InspectionEdit(draft, jobName, camName, posName, channels))
                {
                    // Prepare independent channel copies first. Validation or
                    // serialization failure never replaces the live tools.
                    InspectionVppContract.SaveChecked(draft, savePath, _originalInputNames, required);
                    update.Commit();
                }

				LogChangeEventArgs.Set("Log",
					"Save Inspection VPP succeeded, Job:" + jobName +
					" Cam:" + camName +
					" Pos:" + posName +
					" Ch:" + channel +
					" Path:" + savePath,
					Color.Green);
				MessageBox.Show(this, UiLanguage.Current == "zh-CN" ? "保存成功。" : "Save OK.",
					UiLanguage.Current == "zh-CN" ? "保存" : "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

			int configuredChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());

			if (mainUsed == 1 && mainCh >= 0 && mainCh < configuredChannelCount)
				used.Add(mainCh);

			if (secondUsed == 1 && secondCh >= 0 && secondCh < configuredChannelCount)
				used.Add(secondCh);

			if (used.Count > 0)
			{
				foreach (var ch in used.OrderBy(x => x))
					Cbo_Channel.Items.Add(ch.ToString());
			}
			else
			{
				// 当前 Pos 没配置通道时，显示当前协议允许的全部通道。
				for (int i = 0; i < configuredChannelCount; i++)
					Cbo_Channel.Items.Add(i.ToString());
			}

			if (Cbo_Channel.Items.Count > 0)
				Cbo_Channel.SelectedIndex = 0;
		}

		private void RefreshTool()
        {
            try { RefreshAlgorithmTool(); }
            catch (Exception ex) { ShowAlgorithmEditorFailure(ex); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void RefreshAlgorithmTool()
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

            if (!EnsureAlgorithmEditor()) return;

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
				try { vpp = Load_Job.GetOrLoadInspection(jobName, channel, camName, posName); }
				catch (Exception ex)
				{
                    ShowAlgorithmEditorFailure(ex);
					return;
				}
			}

            try { BindAlgorithmSubject(vpp); }
            catch (Exception ex) { ShowAlgorithmEditorFailure(ex); }
		}

		private void Btn_Load_Click(object sender, EventArgs e)
		{
            try { if (EnsureAlgorithmEditor()) ReloadInspectionVppFromDisk(); }
            catch (Exception ex) { ShowAlgorithmEditorFailure(ex); }
		}

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
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

                using (diskVpp) BindAlgorithmSubject(diskVpp);

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

                BindAlgorithmSubject(cacheVpp);

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
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool EnsureAlgorithmEditor()
        {
            if (cogToolBlockEditV21 != null) return true;
            CogToolBlockEditV2 editor = null;
            try
            {
                Load_Job.EnsureCognexInteropLoaded();
                editor = new CogToolBlockEditV2();
                ((System.ComponentModel.ISupportInitialize)editor).BeginInit();
                editor.Name = "cogToolBlockEditV21"; editor.Dock = DockStyle.Fill;
                editor.Margin = new Padding(4); editor.AllowDrop = true;
                editor.ShowNodeToolTips = true; editor.SuspendElectricRuns = false;
                ((System.ComponentModel.ISupportInitialize)editor).EndInit();
                _editorHost.Controls.Clear(); _editorHost.Controls.Add(editor);
                cogToolBlockEditV21 = editor;
                HideUncheckedSaveButtons(editor);
                return true;
            }
            catch (Exception ex)
            {
                try { editor?.Dispose(); } catch { }
                ShowAlgorithmEditorFailure(ex); return false;
            }
        }

        private void ShowAlgorithmEditorFailure(Exception ex)
        {
            try { ResetAlgorithmEditor(); } catch { }
            _editorHost.Controls.Clear();
            _editorHost.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, Dock = DockStyle.Fill,
                Text = "Algorithm editor could not be loaded. The application remains running; select a valid VPP to retry.\r\n" + ex.GetBaseException().Message });
            LogChangeEventArgs.Set("Log", "Algorithm editor failed: " + ex, Color.Red);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void ResetAlgorithmEditor()
        {
            var previous = cogToolBlockEditV21; cogToolBlockEditV21 = null;
            previous?.Dispose();
            _draft?.Dispose(); _draft = null;
        }

        private void BindAlgorithmSubject(CogToolBlock subject)
        {
            var copy = (CogToolBlock)CogSerializer.DeepCopyObject(subject);
            BlankCameraVpp.InitializeInspectionInterface(copy);
            InspectionVppContract.EnsureContextInputs(copy);
            var oldDraft = _draft;
            var oldSubject = cogToolBlockEditV21.Subject;
            bool suspended = cogToolBlockEditV21.SuspendElectricRuns;
            try
            {
				DateTime capturedAt;
				bool hasLatestImage = AttachLatestImage(copy, out capturedAt);
                cogToolBlockEditV21.SuspendElectricRuns = true;
                cogToolBlockEditV21.Subject = copy;
                cogToolBlockEditV21.Refresh();
                HideUncheckedSaveButtons(cogToolBlockEditV21);
                _draft = copy;
                _originalInputNames = copy.Inputs.Cast<CogToolBlockTerminal>().Take(2).Select(t => t.Name).ToArray();
                oldDraft?.Dispose();
				if (hasLatestImage)
					LogChangeEventArgs.Set("Log", "Algorithm editor attached latest cached image: " + capturedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"), Color.Black);
            }
            catch { cogToolBlockEditV21.Subject = oldSubject; copy.Dispose(); throw; }
            finally { cogToolBlockEditV21.SuspendElectricRuns = suspended; }
        }

		private bool AttachLatestImage(CogToolBlock draft, out DateTime capturedAt)
		{
			capturedAt = DateTime.MinValue;
			if (draft == null || draft.Inputs.Count == 0) return false;
			string job = Cbo_JobID.SelectedItem as string;
			string cam = Cbo_Camera.SelectedItem as string;
			string pos = Cbo_Position.SelectedItem as string;
			InspectionVppContract.ApplyContext(draft, job, pos, null);
			Load_Job.VppCache.LatestInspectionImage latest;
			if (!Load_Job.VppCache.TryGetLatestInspectionImage(job, cam, pos, out latest)) return false;
			draft.Inputs[0].Value = latest.Image;
			if (draft.Inputs.Count > 1) draft.Inputs[1].Value = latest.InputData;
			InspectionVppContract.ApplyContext(draft, job, pos, latest.Serial);
			capturedAt = latest.CapturedAt;
			return true;
		}

		private void LatestInspectionImageUpdated(string job, string cam, string pos)
		{
			if (IsDisposed || Disposing || !string.Equals(job, Cbo_JobID.SelectedItem as string, StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(cam, Cbo_Camera.SelectedItem as string, StringComparison.OrdinalIgnoreCase) ||
				!string.Equals(pos, Cbo_Position.SelectedItem as string, StringComparison.OrdinalIgnoreCase)) return;
			try
			{
				BeginInvoke(new Action(() =>
				{
					if (IsDisposed || _draft == null || cogToolBlockEditV21 == null) return;
					DateTime capturedAt;
					if (!AttachLatestImage(_draft, out capturedAt)) return;
					cogToolBlockEditV21.Refresh();
					LogChangeEventArgs.Set("Log", "Algorithm editor refreshed with latest captured image: " + capturedAt.ToString("yyyy-MM-dd HH:mm:ss.fff"), Color.Black);
				}));
			}
			catch (InvalidOperationException) { /* form was closing */ }
		}

        private List<string> RequiredOutputsForSave(string job, string camera, string position, CogToolBlock toolForSave = null)
        {
            var required = new List<string>();
            var config = XmlConfigHelper.Load(Global.ParameterCogfig);
            var cam = CameraIntegration.CameraAcquisition.Find(config, job, camera);
            var general = cam?.Positions?.FirstOrDefault(p => p.Name == position)?.General;
            int mainChannel;
            if (general == null || general.MainUsed != "1" || !int.TryParse(general.MainChannel, out mainChannel)) return required;
            if (System.IO.File.Exists(Global.VppOutputCfgPath))
                required.AddRange(XmlConfigHelper.GetVppItemsForCam(XmlConfigHelper.LoadVppOutput(Global.VppOutputCfgPath), job, camera)
                    .Where(item => item.Channel == mainChannel && string.Equals(item.Required, "true", StringComparison.OrdinalIgnoreCase)).Select(item => item.Source));
            var protocol = CommunicationConfigHelper.Load().Protocol;
            if (protocol == CommunicationProtocol.S7)
            {
                string path = ProjectPaths.Config("Communication/S7/S7OutputTableConfig.xml");
                if (System.IO.File.Exists(path))
                {
                    var rows = System.Xml.Linq.XDocument.Load(path).Descendants("S7OutputSignalTableRow");
                    foreach (var row in rows)
                    {
                        if ((string)row.Element("Channel") != mainChannel.ToString()) continue;
                        string pin = (string)row.Element("VppOutput");
                        if (!string.IsNullOrWhiteSpace(pin))
                        {
                            required.Add(pin);
                            if (toolForSave != null && toolForSave.Outputs.Contains(pin)) S7OutputData.ValidateDeclaredType(toolForSave.Outputs[pin].ValueType, (string)row.Element("Format"));
                        }
                    }
                }
            }
            if (protocol == CommunicationProtocol.Profinet && System.IO.File.Exists(Profinet.ProfinetCommConfigHelper.DefaultPath))
                required.AddRange(Profinet.ProfinetCommConfigHelper.Load().OutputPoints.Where(p => p.Channel == mainChannel && !p.IsFixed).Select(p => p.VppOutput));
            return required.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void HideUncheckedSaveButtons(Control control)
        {
            var toolbar = control as ToolBar;
            if (toolbar != null)
                foreach (ToolBarButton button in toolbar.Buttons)
                    if (string.Equals(button.Tag as string, "Save", StringComparison.Ordinal) || string.Equals(button.Tag as string, "SaveAs", StringComparison.Ordinal))
                        button.Visible = false;
            foreach (Control child in control.Controls) HideUncheckedSaveButtons(child);
        }
        private sealed class CheckedAlgorithmSaveShortcuts : IMessageFilter
        {
            private readonly Algorithm _form;
            internal CheckedAlgorithmSaveShortcuts(Algorithm form) { _form = form; }
            public bool PreFilterMessage(ref Message message)
            {
                if (message.Msg != 0x100 || message.WParam.ToInt32() != (int)Keys.S || !_form.ContainsFocus || (Control.ModifierKeys & Keys.Control) == 0) return false;
                if ((Control.ModifierKeys & Keys.Shift) != 0)
                    MessageBox.Show(_form, UiLanguage.Text("Use the top Save button to validate and save this algorithm."), UiLanguage.Text("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                else _form.Btn_Save_Click(_form, EventArgs.Empty);
                return true;
            }
        }

        private void ReloadRuntimeVppForAllActiveChannels(string selectedJobName, int selectedChannel)
		{
			try
			{
				int configuredChannelCount = CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load());
				for (int ch = 0; ch < configuredChannelCount; ch++)
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

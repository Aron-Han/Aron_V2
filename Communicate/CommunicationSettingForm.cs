using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
	public class CommunicationSettingForm : Form
	{
		private readonly RadioButton _s7 = new RadioButton();
		private readonly RadioButton _profinet = new RadioButton();
		private readonly RadioButton _tcpIp = new RadioButton();
		private readonly NumericUpDown _channelCount = new NumericUpDown();
		private readonly Label _channelHint = new Label();
		private readonly Label _description = new Label();
		private readonly Button _basic = new Button();
		private readonly Button _input = new Button();
		private readonly Button _output = new Button();
		private CommunicationConfig _config;

		public event EventHandler CommunicationSettingSaved;

		public CommunicationSettingForm()
		{
			SuspendLayout();
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			Text = "Communication";
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(630, 360);
			// Keep the protocol selector stable: protocol-specific descriptions must not
			// resize the dialog and move controls while an operator is using it.
			AutoSize = false;
			Font = new Font("Segoe UI", 9F);
			var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, ColumnCount = 1, Padding = new Padding(18) };
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			layout.RowCount = 5;
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

			GroupBox protocols = new GroupBox { Text = "Enable one protocol", Left = 20, Top = 18, Width = 590, Height = 82 };
			SetupProtocolRadio(_s7, "S7", 30);
			SetupProtocolRadio(_profinet, "Profinet", 205);
			SetupProtocolRadio(_tcpIp, "TCP/IP", 400);
			protocols.Controls.AddRange(new Control[] { _s7, _profinet, _tcpIp });
			protocols.AutoSize = false;
			protocols.Dock = DockStyle.Fill;
			protocols.Padding = new Padding(10);
			var choices = new FlowLayoutPanel { AutoSize = false, Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(6) };
			choices.Controls.AddRange(new Control[] { _s7, _profinet, _tcpIp });
			foreach (RadioButton radio in new[] { _s7, _profinet, _tcpIp }) radio.Margin = new Padding(12, 8, 30, 8);
			protocols.Controls.Add(choices);
			layout.Controls.Add(protocols, 0, 0);

			Label countLabel = new Label { Text = "Active channel count", AutoSize = true, Left = 28, Top = 127 };
			_channelCount.Left = 185;
			_channelCount.Top = 122;
			_channelCount.Width = 90;
			_channelCount.Minimum = 1;
			_channelCount.Maximum = CommunicationConfigHelper.MaxScalableChannelCount;
			_channelHint.Left = 295;
			_channelHint.Top = 127;
			_channelHint.Width = 300;
			_channelHint.AutoSize = true;
			var channels = new TableLayoutPanel { AutoSize = false, Dock = DockStyle.Fill, ColumnCount = 3, Margin = new Padding(6, 18, 6, 18) };
			channels.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			channels.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
			channels.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			channels.Controls.Add(countLabel, 0, 0); channels.Controls.Add(_channelCount, 1, 0); channels.Controls.Add(_channelHint, 2, 0);
			foreach (Control control in channels.Controls) { control.Anchor = AnchorStyles.Left; control.Margin = new Padding(6); }
			layout.Controls.Add(channels, 0, 1);

			_description.Left = 28;
			_description.Top = 165;
			_description.Width = 565;
			_description.Height = 48;
			_description.ForeColor = Color.DimGray;
			_description.AutoSize = false;
			_description.Dock = DockStyle.Fill;
			_description.MaximumSize = new Size(580, 0);
			_description.Margin = new Padding(6, 6, 6, 18);
			layout.Controls.Add(_description, 0, 2);

			_basic.Text = "Connection Setting";
			_input.Text = "Input Setting";
			_output.Text = "Output Setting";
			_basic.SetBounds(28, 230, 170, 38);
			_input.SetBounds(218, 230, 170, 38);
			_output.SetBounds(408, 230, 170, 38);
			_basic.Click += Basic_Click;
			_input.Click += Input_Click;
			_output.Click += Output_Click;
			var settings = new TableLayoutPanel { AutoSize = false, Dock = DockStyle.Fill, ColumnCount = 3 };
			int column = 0;
			foreach (Button button in new[] { _basic, _input, _output })
			{
				settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 3));
				button.AutoSize = true; button.MinimumSize = new Size(165, 38); button.Dock = DockStyle.Fill; button.Margin = new Padding(6);
				settings.Controls.Add(button, column++, 0);
			}
			layout.Controls.Add(settings, 0, 3);

			Button save = new Button { Text = "Save && Apply", Left = 373, Top = 305, Width = 120, Height = 32 };
			Button close = new Button { Text = "Close", Left = 508, Top = 305, Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
			save.Click += Save_Click;

			var actions = new FlowLayoutPanel { AutoSize = false, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = new Padding(6, 18, 6, 0) };
			foreach (Button button in new[] { close, save }) { button.AutoSize = true; button.MinimumSize = new Size(100, 32); button.Margin = new Padding(6); actions.Controls.Add(button); }
			layout.Controls.Add(actions, 0, 4);
			Controls.Add(layout);
			CancelButton = close;
			LoadConfig();
			DesktopTheme.ApplySettings(this);
			// Theme minimum button sizes must fit the explicitly reserved command rows.
			foreach (Button button in new[] { _basic, _input, _output })
			{
				button.AutoSize = false;
				button.MinimumSize = Size.Empty;
			}
			foreach (Button button in new[] { save, close })
			{
				button.AutoSize = false;
				button.MinimumSize = Size.Empty;
				button.Size = new Size(120, 32);
			}
			ResumeLayout(true);
			// Use one coordinate surface for this compact fixed dialog. Nested tables
			// with implicit rows retained preferred heights at 150% DPI and clipped.
			var surface = new Panel { Dock = DockStyle.Fill };
			Controls.Add(surface);
			surface.BringToFront();
			foreach (Control control in new Control[] { protocols, countLabel, _channelCount, _channelHint, _description, _basic, _input, _output, save, close })
			{
				control.Dock = DockStyle.None;
				control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
				surface.Controls.Add(control);
			}
			Action arrange = () =>
			{
				float sx = surface.ClientSize.Width / 630F, sy = surface.ClientSize.Height / 360F;
				Action<Control, int, int, int, int> place = (c, x, y, w, h) => {
					c.AutoSize = false; c.MinimumSize = Size.Empty; c.MaximumSize = Size.Empty;
					c.SetBounds((int)(x * sx), (int)(y * sy), (int)(w * sx), (int)(h * sy));
				};
				place(protocols, 20, 18, 590, 82);
				place(countLabel, 28, 127, 145, 26);
				place(_channelCount, 180, 125, 65, 26);
				place(_channelHint, 270, 127, 330, 26);
				place(_description, 28, 172, 570, 42);
				place(_basic, 28, 230, 182, 44);
				place(_input, 222, 230, 182, 44);
				place(_output, 416, 230, 182, 44);
				place(save, 350, 305, 120, 32);
				place(close, 480, 305, 118, 32);
			};
			surface.Layout += (s, e) => arrange();
			Shown += (s, e) => arrange();
			arrange();
			UiLanguage.ApplyTo(this);
		}

		private void SetupProtocolRadio(RadioButton radio, string text, int left)
		{
			radio.Text = text;
			radio.AutoSize = true;
			radio.Left = left;
			radio.Top = 34;
			radio.CheckedChanged += Protocol_CheckedChanged;
		}

		private void LoadConfig()
		{
			_config = CommunicationConfigHelper.Load();
			_channelCount.Value = Math.Max(_channelCount.Minimum, Math.Min(_channelCount.Maximum, _config.ChannelCount));
			_s7.Checked = _config.Protocol == CommunicationProtocol.S7;
			_profinet.Checked = _config.Protocol == CommunicationProtocol.Profinet;
			_tcpIp.Checked = _config.Protocol == CommunicationProtocol.TcpIp;
			UpdateProtocolUi();
		}

		private CommunicationProtocol SelectedProtocol
		{
			get
			{
				if (_profinet.Checked) return CommunicationProtocol.Profinet;
				if (_tcpIp.Checked) return CommunicationProtocol.TcpIp;
				return CommunicationProtocol.S7;
			}
		}

		private void Protocol_CheckedChanged(object sender, EventArgs e)
		{
			if (((RadioButton)sender).Checked) UpdateProtocolUi();
		}

		private void UpdateProtocolUi()
		{
			bool fixedFour = SelectedProtocol == CommunicationProtocol.Profinet;
			_channelCount.Enabled = !fixedFour;
			if (fixedFour) _channelCount.Value = CommunicationConfigHelper.ProfinetChannelCount;
			_channelHint.Text = fixedFour ? "Profinet is fixed at 4 channels." : "Configurable: 1 to 64 channels.";

			if (SelectedProtocol == CommunicationProtocol.S7)
				_description.Text = "Uses the standard S7 handshake, address tables, heartbeat and reconnect logic.";
			else if (SelectedProtocol == CommunicationProtocol.Profinet)
				_description.Text = "Uses the Cognex CC24 Profinet card. Input/output buffers are limited to four channels.";
			else
				_description.Text = "Uses the same logical handshake as S7. Socket frame encoding is configured by the project protocol.";

			_input.Enabled = SelectedProtocol != CommunicationProtocol.TcpIp;
			_output.Enabled = SelectedProtocol != CommunicationProtocol.TcpIp;
			// CC24/FFP network identity is configured outside this application.  Do not
			// open a modal placeholder for Profinet; it has led operators to a blocked UI.
			_basic.Enabled = SelectedProtocol != CommunicationProtocol.Profinet;
		}

		private void Save_Click(object sender, EventArgs e)
		{
			_config.Protocol = SelectedProtocol;
			_config.ChannelCount = SelectedProtocol == CommunicationProtocol.Profinet
				? CommunicationConfigHelper.ProfinetChannelCount
				: (int)_channelCount.Value;
			CommunicationConfigHelper.Save(_config);

			EventHandler handler = CommunicationSettingSaved;
			if (handler != null) handler(this, EventArgs.Empty);
			MessageBox.Show(this, UiLanguage.Text("Communication selection saved."), UiLanguage.Text("Communication"), MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void Basic_Click(object sender, EventArgs e)
		{
			if (SelectedProtocol == CommunicationProtocol.S7)
			{
				using (S7SettingForm form = new S7SettingForm()) form.ShowDialog(this);
			}
			else if (SelectedProtocol == CommunicationProtocol.Profinet)
			{
				MessageBox.Show("The Profinet adapter and network identity are configured by the CC24/FFP card. Use Input Setting to edit UserData addresses and Output Setting to edit Result addresses.", "Profinet", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			else
			{
				using (TcpIpSettingForm form = new TcpIpSettingForm()) form.ShowDialog(this);
			}
		}

		private void Input_Click(object sender, EventArgs e)
		{
			if (SelectedProtocol == CommunicationProtocol.S7)
				using (S7InputSettingForm form = new S7InputSettingForm((int)_channelCount.Value)) form.ShowDialog(this);
			else if (SelectedProtocol == CommunicationProtocol.Profinet)
				using (ProfinetInputSettingForm form = new ProfinetInputSettingForm()) form.ShowDialog(this);
		}

		private void Output_Click(object sender, EventArgs e)
		{
			if (SelectedProtocol == CommunicationProtocol.S7)
				using (S7OutputSettingForm form = new S7OutputSettingForm((int)_channelCount.Value)) form.ShowDialog(this);
			else if (SelectedProtocol == CommunicationProtocol.Profinet)
				using (ProfinetOutputSettingForm form = new ProfinetOutputSettingForm()) form.ShowDialog(this);
		}
	}
}

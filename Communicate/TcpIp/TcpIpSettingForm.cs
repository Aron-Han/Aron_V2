using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace Aron_V2
{
	public class TcpIpSettingForm : Form
	{
		private readonly ComboBox _mode = new ComboBox();
		private readonly TextBox _ip = new TextBox();
		private readonly NumericUpDown _port = new NumericUpDown();
		private readonly NumericUpDown _connectTimeout = new NumericUpDown();
		private readonly NumericUpDown _reconnectInterval = new NumericUpDown();

		public TcpIpSettingForm()
		{
			SuspendLayout();
			AutoScaleDimensions = new SizeF(96F, 96F);
			AutoScaleMode = AutoScaleMode.Dpi;
			Text = "TCP/IP Setting";
			StartPosition = FormStartPosition.CenterParent;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(460, 285);
			AutoSize = true;
			AutoSizeMode = AutoSizeMode.GrowAndShrink;
			Font = new Font("Segoe UI", 9F);

			TableLayoutPanel table = new TableLayoutPanel
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				MinimumSize = new Size(460, 0),
				Padding = new Padding(18),
				ColumnCount = 2,
				RowCount = 6
			};
			table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			_mode.DropDownStyle = ComboBoxStyle.DropDownList;
			_mode.Items.AddRange(new object[] { TcpIpWorkMode.Client, TcpIpWorkMode.Server });
			ConfigureNumber(_port, 1, 65535);
			ConfigureNumber(_connectTimeout, 100, 60000);
			ConfigureNumber(_reconnectInterval, 100, 60000);

			AddRow(table, 0, "Work mode", _mode);
			AddRow(table, 1, "Remote / Bind IP", _ip);
			AddRow(table, 2, "Port", _port);
			AddRow(table, 3, "Connect timeout (ms)", _connectTimeout);
			AddRow(table, 4, "Reconnect interval (ms)", _reconnectInterval);

			FlowLayoutPanel buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
			Button save = new Button { Text = "Save", Width = 90, Height = 30 };
			Button cancel = new Button { Text = "Cancel", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
			save.AutoSize = cancel.AutoSize = true;
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			save.Click += Save_Click;
			buttons.Controls.Add(save);
			buttons.Controls.Add(cancel);
			table.Controls.Add(buttons, 0, 5);
			table.SetColumnSpan(buttons, 2);
			Controls.Add(table);
			AcceptButton = save;
			CancelButton = cancel;

			TcpIpCommConfig config = TcpIpConfigXmlHelper.Load();
			_mode.SelectedItem = config.Mode;
			_ip.Text = config.IpAddress;
			_port.Value = config.Port;
			_connectTimeout.Value = Math.Max(_connectTimeout.Minimum, Math.Min(_connectTimeout.Maximum, config.ConnectTimeoutMs));
			_reconnectInterval.Value = Math.Max(_reconnectInterval.Minimum, Math.Min(_reconnectInterval.Maximum, config.ReconnectIntervalMs));
			DesktopTheme.ApplySettings(this);
			ResumeLayout(true);
		}

		private static void ConfigureNumber(NumericUpDown control, decimal minimum, decimal maximum)
		{
			control.Minimum = minimum;
			control.Maximum = maximum;
			control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		}

		private static void AddRow(TableLayoutPanel table, int row, string text, Control control)
		{
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			table.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 8, 12, 8) }, 0, row);
			control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			control.MinimumSize = new Size(160, 0);
			control.Margin = new Padding(6, 8, 6, 8);
			table.Controls.Add(control, 1, row);
		}

		private void Save_Click(object sender, EventArgs e)
		{
			IPAddress parsed;
			if (!IPAddress.TryParse(_ip.Text.Trim(), out parsed))
			{
				MessageBox.Show("Please enter a valid IP address.", "TCP/IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			TcpIpConfigXmlHelper.Save(new TcpIpCommConfig
			{
				Mode = (TcpIpWorkMode)_mode.SelectedItem,
				IpAddress = parsed.ToString(),
				Port = (int)_port.Value,
				ConnectTimeoutMs = (int)_connectTimeout.Value,
				ReconnectIntervalMs = (int)_reconnectInterval.Value
			});
			DialogResult = DialogResult.OK;
			Close();
		}
	}
}

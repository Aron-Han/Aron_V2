using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class ManualSimulationRequest
    {
        internal int Channel;
        internal bool PlcInteraction;
        internal byte ClearResult, JobId, PosId;
        internal string PartNo = "";
        internal CommunicationChannelInput ToInput()
        {
            return new CommunicationChannelInput { Channel = Channel, SetUserData = true,
                ClearResult = ClearResult, JobId = JobId, PosId = PosId, PartNo = PartNo };
        }
        internal string Describe(CommunicationProtocol protocol, bool receiveData)
        {
            string prefix = "Manual " + (receiveData ? "Receive Data" : "Trigger") + " [" + (protocol == CommunicationProtocol.TcpIp ? "TCP/IP" : protocol.ToString()) + "] CH=" + Channel +
                " PLC=" + (PlcInteraction ? "Yes" : "No (local only)");
            return receiveData ? prefix + " | logical input snapshot (not a wire frame): SetUserData=1|ClearResult=" + ClearResult +
                "|PosID=" + PosId + "|JobID=" + JobId + "|PartN=" + Newtonsoft.Json.JsonConvert.SerializeObject(PartNo) : prefix;
        }
    }

    internal sealed class ManualSimulationDialog : Form
    {
        private readonly ComboBox _channel, _interaction;
        private readonly NumericUpDown _clear, _job, _pos;
        private readonly TextBox _part;
        internal ManualSimulationRequest Request
        {
            get { return new ManualSimulationRequest { Channel = (int)_channel.SelectedItem, PlcInteraction = _interaction.SelectedIndex == 1,
                ClearResult = (byte)(_clear?.Value ?? 0), JobId = (byte)(_job?.Value ?? 1), PosId = (byte)(_pos?.Value ?? 1), PartNo = _part?.Text ?? "" }; }
        }

        internal ManualSimulationDialog(CommunicationConfig config, bool receiveData, int initialChannel)
        {
            float scale = DesktopTheme.DisplayScale;
            Func<int, int> px = n => (int)Math.Round(n * scale);
            Text = receiveData ? "Manual Receive Data" : "Manual Trigger";
            Name = receiveData ? "ManualReceiveDataDialog" : "ManualTriggerDialog";
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96 * scale, 96 * scale);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            ClientSize = new Size(px(540), px(receiveData ? 440 : 270));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = receiveData ? 9 : 5,
                Padding = new Padding(px(18)), Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(145)));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            var protocol = new Label { Text = "Protocol: " + (config.Protocol == CommunicationProtocol.TcpIp ? "TCP/IP" : config.Protocol.ToString()),
                AutoSize = true, ForeColor = DesktopTheme.Muted, Margin = new Padding(0, 0, 0, px(10)) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.Controls.Add(protocol, 0, row++); root.SetColumnSpan(protocol, 2);
            Action<string, Control> field = (caption, control) =>
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, px(36)));
                root.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = Padding.Empty }, 0, row);
                control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(0, px(4), 0, px(4)); root.Controls.Add(control, 1, row++);
            };
            _channel = new ComboBox { Name = "ManualChannel", DropDownStyle = ComboBoxStyle.DropDownList };
            int count = CommunicationConfigHelper.GetEffectiveChannelCount(config);
            for (int ch = 0; ch < count; ch++) _channel.Items.Add(ch);
            _channel.SelectedIndex = Math.Max(0, Math.Min(count - 1, initialChannel));
            field("Channel:", _channel);
            _interaction = new ComboBox { Name = "ManualPlcInteraction", DropDownStyle = ComboBoxStyle.DropDownList };
            _interaction.Items.AddRange(new object[] { UiLanguage.Text("No - local only"), UiLanguage.Text("Yes - PLC interaction") });
            _interaction.SelectedIndex = 0;
            field("PLC interaction:", _interaction);
            if (receiveData)
            {
                _clear = Number("ManualClearResult", 0, 1); field("Clear Result:", _clear);
                _pos = Number("ManualPosition", 1, 255); field("Position number:", _pos);
                _job = Number("ManualJob", 1, 255); field("Job number:", _job);
                _part = new TextBox { Name = "ManualPartNo" }; field("Product code:", _part);
                Action fill = () =>
                {
                    int ch = (int)_channel.SelectedItem, value;
                    _job.Value = int.TryParse((Global.Model_JobID[ch] ?? "").Replace("Job", ""), out value) ? Math.Max(1, Math.Min(255, value)) : 1;
                    _pos.Value = int.TryParse((Global.Position_ID[ch] ?? "").Replace("Pos", ""), out value) ? Math.Max(1, Math.Min(255, value)) : 1;
                    _part.Text = Global.PartCode[ch] ?? "";
                };
                _channel.SelectedIndexChanged += (s, e) => fill(); fill();
            }
            var note = new Label { Text = "No suppresses PLC output for this operation only; automatic polling continues. Yes uses the normal processing flow. Transport ACK follows actual PLC input edges.",
                Dock = DockStyle.Fill, ForeColor = DesktopTheme.Muted, Margin = new Padding(0, px(10), 0, px(10)) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.Controls.Add(note, 0, row++); root.SetColumnSpan(note, 2);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(115)));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(105)));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var run = new Button { Name = "ManualExecute", Text = receiveData ? "Apply" : "Trigger", DialogResult = DialogResult.OK,
                Size = new Size(px(105), px(32)), Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = run.Size,
                Margin = new Padding(0, 0, px(10), 0), Anchor = AnchorStyles.Left | AnchorStyles.Right };
            actions.Controls.Add(cancel, 1, 0); actions.Controls.Add(run, 2, 0);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, px(36))); root.Controls.Add(actions, 0, row); root.SetColumnSpan(actions, 2);
            Controls.Add(root); DesktopTheme.ApplySettings(this); DesktopTheme.StyleButton(run, true);
            AcceptButton = run; CancelButton = cancel; UiLanguage.ApplyTo(this);
        }

        private static NumericUpDown Number(string name, int minimum, int maximum)
        { return new NumericUpDown { Name = name, Minimum = minimum, Maximum = maximum, Value = minimum }; }
    }
}

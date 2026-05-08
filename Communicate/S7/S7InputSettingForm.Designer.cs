using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    partial class S7InputSettingForm
    {
        private System.ComponentModel.IContainer components = null;
        private DataGridView dgvInput;
        private DataGridViewTextBoxColumn colChannelNo;
        private DataGridViewTextBoxColumn colParameterName;
        private DataGridViewTextBoxColumn colDbAddress;
        private DataGridViewTextBoxColumn colLength;
        private DataGridViewTextBoxColumn colDataType;
        private DataGridViewTextBoxColumn colCurrentValue;
        private Button btnLoad;
        private Button btnSave;
        private Button btnReadSelected;
        private Button btnReadChannel;
        private Button btnResetDefault;
        private TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.dgvInput = new System.Windows.Forms.DataGridView();
            this.colChannelNo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colParameterName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDbAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDataType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCurrentValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnReadSelected = new System.Windows.Forms.Button();
            this.btnReadChannel = new System.Windows.Forms.Button();
            this.btnResetDefault = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            ((System.ComponentModel.ISupportInitialize)(this.dgvInput)).BeginInit();
            this.SuspendLayout();

            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1120, 660);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(1000, 620);
            this.Name = "S7InputSettingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "S7 Input Setting";
            this.Load += new System.EventHandler(this.S7InputSettingForm_Load);

            this.dgvInput.AllowUserToAddRows = false;
            this.dgvInput.AllowUserToDeleteRows = false;
            this.dgvInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvInput.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvInput.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvInput.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colChannelNo,
                this.colParameterName,
                this.colDbAddress,
                this.colLength,
                this.colDataType,
                this.colCurrentValue});
            this.dgvInput.Location = new System.Drawing.Point(12, 12);
            this.dgvInput.MultiSelect = false;
            this.dgvInput.Name = "dgvInput";
            this.dgvInput.RowHeadersVisible = false;
            this.dgvInput.RowTemplate.Height = 25;
            this.dgvInput.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvInput.Size = new System.Drawing.Size(1096, 430);

            this.colChannelNo.FillWeight = 55F;
            this.colChannelNo.HeaderText = "Channel";
            this.colChannelNo.Name = "ChannelNo";
            this.colChannelNo.ReadOnly = true;

            this.colParameterName.FillWeight = 140F;
            this.colParameterName.HeaderText = "Parameter";
            this.colParameterName.Name = "ParameterName";
            this.colParameterName.ReadOnly = true;

            this.colDbAddress.FillWeight = 170F;
            this.colDbAddress.HeaderText = "DB Address";
            this.colDbAddress.Name = "DbAddress";

            this.colLength.FillWeight = 80F;
            this.colLength.HeaderText = "Length";
            this.colLength.Name = "Length";

            this.colDataType.FillWeight = 110F;
            this.colDataType.HeaderText = "Format";
            this.colDataType.Name = "DataType";

            this.colCurrentValue.FillWeight = 180F;
            this.colCurrentValue.HeaderText = "Current Value";
            this.colCurrentValue.Name = "CurrentValue";

            this.btnLoad.Location = new System.Drawing.Point(12, 455);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(90, 30);
            this.btnLoad.Text = "Load";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);

            this.btnSave.Location = new System.Drawing.Point(115, 455);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 30);
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            this.btnReadSelected.Location = new System.Drawing.Point(220, 455);
            this.btnReadSelected.Name = "btnReadSelected";
            this.btnReadSelected.Size = new System.Drawing.Size(125, 30);
            this.btnReadSelected.Text = "Read Selected";
            this.btnReadSelected.UseVisualStyleBackColor = true;
            this.btnReadSelected.Click += new System.EventHandler(this.btnReadSelected_Click);

            this.btnReadChannel.Location = new System.Drawing.Point(360, 455);
            this.btnReadChannel.Name = "btnReadChannel";
            this.btnReadChannel.Size = new System.Drawing.Size(125, 30);
            this.btnReadChannel.Text = "Read Channel";
            this.btnReadChannel.UseVisualStyleBackColor = true;
            this.btnReadChannel.Click += new System.EventHandler(this.btnReadChannel_Click);

            this.btnResetDefault.Location = new System.Drawing.Point(500, 455);
            this.btnResetDefault.Name = "btnResetDefault";
            this.btnResetDefault.Size = new System.Drawing.Size(150, 30);
            this.btnResetDefault.Text = "Reset Default 4CH";
            this.btnResetDefault.UseVisualStyleBackColor = true;
            this.btnResetDefault.Click += new System.EventHandler(this.btnResetDefault_Click);

            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Location = new System.Drawing.Point(12, 500);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(1096, 148);

            this.Controls.Add(this.dgvInput);
            this.Controls.Add(this.btnLoad);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnReadSelected);
            this.Controls.Add(this.btnReadChannel);
            this.Controls.Add(this.btnResetDefault);
            this.Controls.Add(this.txtLog);

            ((System.ComponentModel.ISupportInitialize)(this.dgvInput)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}

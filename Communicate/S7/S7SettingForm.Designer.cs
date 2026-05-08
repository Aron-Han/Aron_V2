using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    partial class S7SettingForm
    {
        private System.ComponentModel.IContainer components = null;

        private GroupBox gbBasic;
        private DataGridView dgvBasic;
        private DataGridViewTextBoxColumn colBasicName;
        private DataGridViewTextBoxColumn colBasicValue;
        private DataGridViewTextBoxColumn colBasicDescription;
        private Button btnLoad;
        private Button btnSave;
        private Button btnConnectTest;

        private GroupBox gbHeartbeat;
        private DataGridView dgvHeartbeat;
        private DataGridViewTextBoxColumn colHeartbeatName;
        private DataGridViewTextBoxColumn colHeartbeatValue;
        private DataGridViewTextBoxColumn colHeartbeatDescription;
        private Button btnHeartbeatOnce;
        private Button btnHeartbeatStartStop;

        private GroupBox gbManual;
        private DataGridView dgvManual;
        private DataGridViewTextBoxColumn colManualName;
        private DataGridViewTextBoxColumn colManualValue;
        private DataGridViewTextBoxColumn colManualDescription;
        private Button btnRead;
        private Button btnWrite;

        private TextBox txtLog;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopHeartbeat();

                if (components != null)
                    components.Dispose();

                if (_tester != null)
                    _tester.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.gbBasic = new System.Windows.Forms.GroupBox();
            this.dgvBasic = new System.Windows.Forms.DataGridView();
            this.colBasicName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBasicValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colBasicDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnLoad = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnConnectTest = new System.Windows.Forms.Button();
            this.gbHeartbeat = new System.Windows.Forms.GroupBox();
            this.dgvHeartbeat = new System.Windows.Forms.DataGridView();
            this.colHeartbeatName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHeartbeatValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHeartbeatDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnHeartbeatOnce = new System.Windows.Forms.Button();
            this.btnHeartbeatStartStop = new System.Windows.Forms.Button();
            this.gbManual = new System.Windows.Forms.GroupBox();
            this.dgvManual = new System.Windows.Forms.DataGridView();
            this.colManualName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colManualValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colManualDescription = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnRead = new System.Windows.Forms.Button();
            this.btnWrite = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();

            this.gbBasic.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvBasic)).BeginInit();
            this.gbHeartbeat.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvHeartbeat)).BeginInit();
            this.gbManual.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvManual)).BeginInit();
            this.SuspendLayout();

            // Form
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1080, 760);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(1080, 760);
            this.Name = "S7SettingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "S7 Setting";
            this.Load += new System.EventHandler(this.S7SettingForm_Load);

            // gbBasic
            this.gbBasic.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.gbBasic.Controls.Add(this.dgvBasic);
            this.gbBasic.Controls.Add(this.btnLoad);
            this.gbBasic.Controls.Add(this.btnSave);
            this.gbBasic.Controls.Add(this.btnConnectTest);
            this.gbBasic.Location = new System.Drawing.Point(12, 12);
            this.gbBasic.Name = "gbBasic";
            this.gbBasic.Size = new System.Drawing.Size(1056, 300);
            this.gbBasic.TabIndex = 0;
            this.gbBasic.TabStop = false;
            this.gbBasic.Text = "S7 Basic Parameters";

            // dgvBasic
            this.dgvBasic.AllowUserToAddRows = false;
            this.dgvBasic.AllowUserToDeleteRows = false;
            this.dgvBasic.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvBasic.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvBasic.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvBasic.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colBasicName,
                this.colBasicValue,
                this.colBasicDescription});
            this.dgvBasic.Location = new System.Drawing.Point(15, 25);
            this.dgvBasic.Name = "dgvBasic";
            this.dgvBasic.RowHeadersVisible = false;
            this.dgvBasic.RowTemplate.Height = 26;
            this.dgvBasic.Size = new System.Drawing.Size(1026, 230);
            this.dgvBasic.TabIndex = 0;

            this.colBasicName.FillWeight = 165F;
            this.colBasicName.HeaderText = "Parameter";
            this.colBasicName.Name = "colBasicName";
            this.colBasicName.ReadOnly = true;

            this.colBasicValue.FillWeight = 150F;
            this.colBasicValue.HeaderText = "Value";
            this.colBasicValue.Name = "colBasicValue";

            this.colBasicDescription.FillWeight = 420F;
            this.colBasicDescription.HeaderText = "Description";
            this.colBasicDescription.Name = "colBasicDescription";
            this.colBasicDescription.ReadOnly = true;

            // Basic buttons
            this.btnLoad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLoad.Location = new System.Drawing.Point(751, 263);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(90, 28);
            this.btnLoad.TabIndex = 1;
            this.btnLoad.Text = "Load";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);

            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(851, 263);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 28);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            this.btnConnectTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnConnectTest.Location = new System.Drawing.Point(951, 263);
            this.btnConnectTest.Name = "btnConnectTest";
            this.btnConnectTest.Size = new System.Drawing.Size(90, 28);
            this.btnConnectTest.TabIndex = 3;
            this.btnConnectTest.Text = "Connect Test";
            this.btnConnectTest.UseVisualStyleBackColor = true;
            this.btnConnectTest.Click += new System.EventHandler(this.btnConnectTest_Click);

            // gbHeartbeat
            this.gbHeartbeat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.gbHeartbeat.Controls.Add(this.dgvHeartbeat);
            this.gbHeartbeat.Controls.Add(this.btnHeartbeatOnce);
            this.gbHeartbeat.Controls.Add(this.btnHeartbeatStartStop);
            this.gbHeartbeat.Location = new System.Drawing.Point(12, 320);
            this.gbHeartbeat.Name = "gbHeartbeat";
            this.gbHeartbeat.Size = new System.Drawing.Size(1056, 170);
            this.gbHeartbeat.TabIndex = 1;
            this.gbHeartbeat.TabStop = false;
            this.gbHeartbeat.Text = "Heartbeat Parameters";

            // dgvHeartbeat
            this.dgvHeartbeat.AllowUserToAddRows = false;
            this.dgvHeartbeat.AllowUserToDeleteRows = false;
            this.dgvHeartbeat.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvHeartbeat.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvHeartbeat.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvHeartbeat.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colHeartbeatName,
                this.colHeartbeatValue,
                this.colHeartbeatDescription});
            this.dgvHeartbeat.Location = new System.Drawing.Point(15, 25);
            this.dgvHeartbeat.Name = "dgvHeartbeat";
            this.dgvHeartbeat.RowHeadersVisible = false;
            this.dgvHeartbeat.RowTemplate.Height = 26;
            this.dgvHeartbeat.Size = new System.Drawing.Size(1026, 100);
            this.dgvHeartbeat.TabIndex = 0;

            this.colHeartbeatName.FillWeight = 165F;
            this.colHeartbeatName.HeaderText = "Parameter";
            this.colHeartbeatName.Name = "colHeartbeatName";
            this.colHeartbeatName.ReadOnly = true;

            this.colHeartbeatValue.FillWeight = 150F;
            this.colHeartbeatValue.HeaderText = "Value";
            this.colHeartbeatValue.Name = "colHeartbeatValue";

            this.colHeartbeatDescription.FillWeight = 420F;
            this.colHeartbeatDescription.HeaderText = "Description";
            this.colHeartbeatDescription.Name = "colHeartbeatDescription";
            this.colHeartbeatDescription.ReadOnly = true;

            this.btnHeartbeatOnce.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHeartbeatOnce.Location = new System.Drawing.Point(821, 132);
            this.btnHeartbeatOnce.Name = "btnHeartbeatOnce";
            this.btnHeartbeatOnce.Size = new System.Drawing.Size(100, 28);
            this.btnHeartbeatOnce.TabIndex = 1;
            this.btnHeartbeatOnce.Text = "Beat Once";
            this.btnHeartbeatOnce.UseVisualStyleBackColor = true;
            this.btnHeartbeatOnce.Click += new System.EventHandler(this.btnHeartbeatOnce_Click);

            this.btnHeartbeatStartStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHeartbeatStartStop.Location = new System.Drawing.Point(931, 132);
            this.btnHeartbeatStartStop.Name = "btnHeartbeatStartStop";
            this.btnHeartbeatStartStop.Size = new System.Drawing.Size(110, 28);
            this.btnHeartbeatStartStop.TabIndex = 2;
            this.btnHeartbeatStartStop.Text = "Start Heartbeat";
            this.btnHeartbeatStartStop.UseVisualStyleBackColor = true;
            this.btnHeartbeatStartStop.Click += new System.EventHandler(this.btnHeartbeatStartStop_Click);

            // gbManual
            this.gbManual.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.gbManual.Controls.Add(this.dgvManual);
            this.gbManual.Controls.Add(this.btnRead);
            this.gbManual.Controls.Add(this.btnWrite);
            this.gbManual.Location = new System.Drawing.Point(12, 498);
            this.gbManual.Name = "gbManual";
            this.gbManual.Size = new System.Drawing.Size(1056, 155);
            this.gbManual.TabIndex = 2;
            this.gbManual.TabStop = false;
            this.gbManual.Text = "Manual Address Read / Write Test";

            // dgvManual
            this.dgvManual.AllowUserToAddRows = false;
            this.dgvManual.AllowUserToDeleteRows = false;
            this.dgvManual.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvManual.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvManual.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvManual.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colManualName,
                this.colManualValue,
                this.colManualDescription});
            this.dgvManual.Location = new System.Drawing.Point(15, 25);
            this.dgvManual.Name = "dgvManual";
            this.dgvManual.RowHeadersVisible = false;
            this.dgvManual.RowTemplate.Height = 26;
            this.dgvManual.Size = new System.Drawing.Size(806, 120);
            this.dgvManual.TabIndex = 0;

            this.colManualName.FillWeight = 145F;
            this.colManualName.HeaderText = "Parameter";
            this.colManualName.Name = "colManualName";
            this.colManualName.ReadOnly = true;

            this.colManualValue.FillWeight = 190F;
            this.colManualValue.HeaderText = "Value";
            this.colManualValue.Name = "colManualValue";

            this.colManualDescription.FillWeight = 360F;
            this.colManualDescription.HeaderText = "Description";
            this.colManualDescription.Name = "colManualDescription";
            this.colManualDescription.ReadOnly = true;

            this.btnRead.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRead.Location = new System.Drawing.Point(851, 45);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(90, 30);
            this.btnRead.TabIndex = 1;
            this.btnRead.Text = "Read";
            this.btnRead.UseVisualStyleBackColor = true;
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);

            this.btnWrite.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnWrite.Location = new System.Drawing.Point(951, 45);
            this.btnWrite.Name = "btnWrite";
            this.btnWrite.Size = new System.Drawing.Size(90, 30);
            this.btnWrite.TabIndex = 2;
            this.btnWrite.Text = "Write";
            this.btnWrite.UseVisualStyleBackColor = true;
            this.btnWrite.Click += new System.EventHandler(this.btnWrite_Click);

            // txtLog
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Location = new System.Drawing.Point(12, 662);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(1056, 86);
            this.txtLog.TabIndex = 3;

            this.Controls.Add(this.gbBasic);
            this.Controls.Add(this.gbHeartbeat);
            this.Controls.Add(this.gbManual);
            this.Controls.Add(this.txtLog);

            this.gbBasic.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvBasic)).EndInit();
            this.gbHeartbeat.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvHeartbeat)).EndInit();
            this.gbManual.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvManual)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
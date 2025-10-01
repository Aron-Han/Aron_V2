namespace Aron_V2
{
	partial class FormCamGeneralConfig
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.CboJob = new System.Windows.Forms.ComboBox();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.btnSave = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.btnAddRow = new System.Windows.Forms.Button();
			this.btnDeleteRow = new System.Windows.Forms.Button();
			this.lblCamCount = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.CboCam = new System.Windows.Forms.ComboBox();
			this.btnAddCam = new System.Windows.Forms.Button();
			this.btnDeleteCam = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
			this.SuspendLayout();
			// 
			// CboJob
			// 
			this.CboJob.FormattingEnabled = true;
			this.CboJob.Location = new System.Drawing.Point(65, 9);
			this.CboJob.Name = "CboJob";
			this.CboJob.Size = new System.Drawing.Size(66, 20);
			this.CboJob.TabIndex = 0;
			// 
			// dataGridView1
			// 
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Location = new System.Drawing.Point(2, 104);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.Size = new System.Drawing.Size(934, 250);
			this.dataGridView1.TabIndex = 3;
			// 
			// btnSave
			// 
			this.btnSave.Location = new System.Drawing.Point(861, 363);
			this.btnSave.Name = "btnSave";
			this.btnSave.Size = new System.Drawing.Size(75, 44);
			this.btnSave.TabIndex = 4;
			this.btnSave.Text = "Save";
			this.btnSave.UseVisualStyleBackColor = true;
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(13, 13);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(35, 12);
			this.label1.TabIndex = 5;
			this.label1.Text = "JobN:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(13, 49);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 12);
			this.label2.TabIndex = 6;
			this.label2.Text = "CamN:";
			// 
			// btnAddRow
			// 
			this.btnAddRow.Location = new System.Drawing.Point(2, 72);
			this.btnAddRow.Name = "btnAddRow";
			this.btnAddRow.Size = new System.Drawing.Size(75, 27);
			this.btnAddRow.TabIndex = 19;
			this.btnAddRow.Text = "Add row";
			this.btnAddRow.UseVisualStyleBackColor = true;
			// 
			// btnDeleteRow
			// 
			this.btnDeleteRow.Location = new System.Drawing.Point(82, 73);
			this.btnDeleteRow.Margin = new System.Windows.Forms.Padding(2);
			this.btnDeleteRow.Name = "btnDeleteRow";
			this.btnDeleteRow.Size = new System.Drawing.Size(77, 27);
			this.btnDeleteRow.TabIndex = 20;
			this.btnDeleteRow.Text = "Delete row";
			this.btnDeleteRow.UseVisualStyleBackColor = true;
			// 
			// lblCamCount
			// 
			this.lblCamCount.AutoSize = true;
			this.lblCamCount.Location = new System.Drawing.Point(65, 49);
			this.lblCamCount.Name = "lblCamCount";
			this.lblCamCount.Size = new System.Drawing.Size(41, 12);
			this.lblCamCount.TabIndex = 21;
			this.lblCamCount.Text = "label5";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(148, 13);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(29, 12);
			this.label3.TabIndex = 8;
			this.label3.Text = "Cam:";
			// 
			// CboCam
			// 
			this.CboCam.FormattingEnabled = true;
			this.CboCam.Location = new System.Drawing.Point(196, 9);
			this.CboCam.Name = "CboCam";
			this.CboCam.Size = new System.Drawing.Size(69, 20);
			this.CboCam.TabIndex = 1;
			// 
			// btnAddCam
			// 
			this.btnAddCam.Location = new System.Drawing.Point(295, 9);
			this.btnAddCam.Name = "btnAddCam";
			this.btnAddCam.Size = new System.Drawing.Size(75, 21);
			this.btnAddCam.TabIndex = 22;
			this.btnAddCam.Text = "Add Camera";
			this.btnAddCam.UseVisualStyleBackColor = true;
			// 
			// btnDeleteCam
			// 
			this.btnDeleteCam.Location = new System.Drawing.Point(376, 9);
			this.btnDeleteCam.Name = "btnDeleteCam";
			this.btnDeleteCam.Size = new System.Drawing.Size(101, 21);
			this.btnDeleteCam.TabIndex = 23;
			this.btnDeleteCam.Text = "Delete Camera";
			this.btnDeleteCam.UseVisualStyleBackColor = true;
			// 
			// FormCamGeneralConfig
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(948, 418);
			this.Controls.Add(this.btnDeleteCam);
			this.Controls.Add(this.btnAddCam);
			this.Controls.Add(this.lblCamCount);
			this.Controls.Add(this.btnDeleteRow);
			this.Controls.Add(this.btnAddRow);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.btnSave);
			this.Controls.Add(this.dataGridView1);
			this.Controls.Add(this.CboCam);
			this.Controls.Add(this.CboJob);
			this.Name = "FormCamGeneralConfig";
			this.Text = "FormCamGeneralConfig";
			((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

        #endregion

        private System.Windows.Forms.ComboBox CboJob;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnAddRow;
        private System.Windows.Forms.Button btnDeleteRow;
        private System.Windows.Forms.Label lblCamCount;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox CboCam;
        private System.Windows.Forms.Button btnAddCam;
        private System.Windows.Forms.Button btnDeleteCam;
    }
}
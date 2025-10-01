namespace Aron_V2
{
	partial class Parameters_Config
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
			this.comboJob = new System.Windows.Forms.ComboBox();
			this.btnLoad = new System.Windows.Forms.Button();
			this.dataGridView1 = new System.Windows.Forms.DataGridView();
			this.btnSave = new System.Windows.Forms.Button();
			this.btnDeleterow = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.comboCam = new System.Windows.Forms.ComboBox();
			this.label2 = new System.Windows.Forms.Label();
			this.btnAddJob = new System.Windows.Forms.Button();
			this.btnDeleteJob = new System.Windows.Forms.Button();
			this.btnAddrow = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
			this.SuspendLayout();
			// 
			// comboJob
			// 
			this.comboJob.FormattingEnabled = true;
			this.comboJob.Location = new System.Drawing.Point(56, 13);
			this.comboJob.Margin = new System.Windows.Forms.Padding(2);
			this.comboJob.Name = "comboJob";
			this.comboJob.Size = new System.Drawing.Size(49, 20);
			this.comboJob.TabIndex = 2;
			this.comboJob.SelectedIndexChanged += new System.EventHandler(this.comboJob_SelectedIndexChanged);
			// 
			// btnLoad
			// 
			this.btnLoad.Location = new System.Drawing.Point(221, 9);
			this.btnLoad.Margin = new System.Windows.Forms.Padding(2);
			this.btnLoad.Name = "btnLoad";
			this.btnLoad.Size = new System.Drawing.Size(44, 26);
			this.btnLoad.TabIndex = 3;
			this.btnLoad.Text = "Load ";
			this.btnLoad.UseVisualStyleBackColor = true;
			this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click_1);
			// 
			// dataGridView1
			// 
			this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView1.Location = new System.Drawing.Point(18, 81);
			this.dataGridView1.Margin = new System.Windows.Forms.Padding(2);
			this.dataGridView1.Name = "dataGridView1";
			this.dataGridView1.RowTemplate.Height = 30;
			this.dataGridView1.Size = new System.Drawing.Size(1071, 555);
			this.dataGridView1.TabIndex = 5;
			// 
			// btnSave
			// 
			this.btnSave.Location = new System.Drawing.Point(277, 9);
			this.btnSave.Margin = new System.Windows.Forms.Padding(2);
			this.btnSave.Name = "btnSave";
			this.btnSave.Size = new System.Drawing.Size(46, 26);
			this.btnSave.TabIndex = 6;
			this.btnSave.Text = "Save";
			this.btnSave.UseVisualStyleBackColor = true;
			this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
			// 
			// btnDeleterow
			// 
			this.btnDeleterow.Location = new System.Drawing.Point(101, 53);
			this.btnDeleterow.Margin = new System.Windows.Forms.Padding(2);
			this.btnDeleterow.Name = "btnDeleterow";
			this.btnDeleterow.Size = new System.Drawing.Size(77, 27);
			this.btnDeleterow.TabIndex = 7;
			this.btnDeleterow.Text = "Delete row";
			this.btnDeleterow.UseVisualStyleBackColor = true;
			this.btnDeleterow.Click += new System.EventHandler(this.btnDeleteRow_Click);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(19, 16);
			this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(35, 12);
			this.label1.TabIndex = 8;
			this.label1.Text = "JobN:";
			// 
			// comboCam
			// 
			this.comboCam.FormattingEnabled = true;
			this.comboCam.Location = new System.Drawing.Point(155, 13);
			this.comboCam.Margin = new System.Windows.Forms.Padding(2);
			this.comboCam.Name = "comboCam";
			this.comboCam.Size = new System.Drawing.Size(49, 20);
			this.comboCam.TabIndex = 9;
			this.comboCam.SelectedIndexChanged += new System.EventHandler(this.comboCam_SelectedIndexChanged);
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(115, 16);
			this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 12);
			this.label2.TabIndex = 10;
			this.label2.Text = "CamN:";
			// 
			// btnAddJob
			// 
			this.btnAddJob.Location = new System.Drawing.Point(920, 9);
			this.btnAddJob.Margin = new System.Windows.Forms.Padding(2);
			this.btnAddJob.Name = "btnAddJob";
			this.btnAddJob.Size = new System.Drawing.Size(84, 55);
			this.btnAddJob.TabIndex = 11;
			this.btnAddJob.Text = "Add new Job";
			this.btnAddJob.UseVisualStyleBackColor = true;
			this.btnAddJob.Click += new System.EventHandler(this.btnAddJob_Click);
			// 
			// btnDeleteJob
			// 
			this.btnDeleteJob.Location = new System.Drawing.Point(1008, 9);
			this.btnDeleteJob.Margin = new System.Windows.Forms.Padding(2);
			this.btnDeleteJob.Name = "btnDeleteJob";
			this.btnDeleteJob.Size = new System.Drawing.Size(78, 55);
			this.btnDeleteJob.TabIndex = 12;
			this.btnDeleteJob.Text = "Delete Job";
			this.btnDeleteJob.UseVisualStyleBackColor = true;
			this.btnDeleteJob.Click += new System.EventHandler(this.btnDeleteJob_Click);
			// 
			// btnAddrow
			// 
			this.btnAddrow.Location = new System.Drawing.Point(21, 53);
			this.btnAddrow.Name = "btnAddrow";
			this.btnAddrow.Size = new System.Drawing.Size(75, 27);
			this.btnAddrow.TabIndex = 13;
			this.btnAddrow.Text = "Add row";
			this.btnAddrow.UseVisualStyleBackColor = true;
			this.btnAddrow.Click += new System.EventHandler(this.btnAddRow_Click);
			// 
			// Parameters_Config
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1097, 644);
			this.Controls.Add(this.btnAddrow);
			this.Controls.Add(this.btnDeleteJob);
			this.Controls.Add(this.btnAddJob);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.comboCam);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.btnDeleterow);
			this.Controls.Add(this.btnSave);
			this.Controls.Add(this.dataGridView1);
			this.Controls.Add(this.btnLoad);
			this.Controls.Add(this.comboJob);
			this.Margin = new System.Windows.Forms.Padding(2);
			this.Name = "Parameters_Config";
			this.Text = "Parameters_Config";
			this.Load += new System.EventHandler(this.Parameters_Config_Load);
			((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion
		private System.Windows.Forms.ComboBox comboJob;
		private System.Windows.Forms.Button btnLoad;
		private System.Windows.Forms.DataGridView dataGridView1;
		private System.Windows.Forms.Button btnSave;
		private System.Windows.Forms.Button btnDeleterow;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.ComboBox comboCam;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button btnAddJob;
		private System.Windows.Forms.Button btnDeleteJob;
		private System.Windows.Forms.Button btnAddrow;
	}
}
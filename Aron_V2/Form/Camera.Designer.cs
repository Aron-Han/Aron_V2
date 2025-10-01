namespace Aron_V2
{
	partial class Camera
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
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
			this.Cbo_Channel = new System.Windows.Forms.ComboBox();
			this.label4 = new System.Windows.Forms.Label();
			this.Cbo_Camera = new System.Windows.Forms.ComboBox();
			this.Cbo_Position = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.Cbo_JobID = new System.Windows.Forms.ComboBox();
			this.Btn_Load = new System.Windows.Forms.Button();
			this.Btn_Save = new System.Windows.Forms.Button();
			this.cogAcqFifoEditV21 = new Cognex.VisionPro.CogAcqFifoEditV2();
			this.tableLayoutPanel1.SuspendLayout();
			this.tableLayoutPanel2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.cogAcqFifoEditV21)).BeginInit();
			this.SuspendLayout();
			// 
			// tableLayoutPanel1
			// 
			this.tableLayoutPanel1.ColumnCount = 1;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 30F));
			this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 0);
			this.tableLayoutPanel1.Controls.Add(this.cogAcqFifoEditV21, 0, 1);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
			this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(4);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 2;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(1623, 900);
			this.tableLayoutPanel1.TabIndex = 0;
			// 
			// tableLayoutPanel2
			// 
			this.tableLayoutPanel2.ColumnCount = 11;
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 655F));
			this.tableLayoutPanel2.Controls.Add(this.Cbo_Channel, 7, 0);
			this.tableLayoutPanel2.Controls.Add(this.label4, 6, 0);
			this.tableLayoutPanel2.Controls.Add(this.Cbo_Camera, 5, 0);
			this.tableLayoutPanel2.Controls.Add(this.Cbo_Position, 3, 0);
			this.tableLayoutPanel2.Controls.Add(this.label1, 0, 0);
			this.tableLayoutPanel2.Controls.Add(this.label2, 2, 0);
			this.tableLayoutPanel2.Controls.Add(this.label3, 4, 0);
			this.tableLayoutPanel2.Controls.Add(this.Cbo_JobID, 1, 0);
			this.tableLayoutPanel2.Controls.Add(this.Btn_Load, 8, 0);
			this.tableLayoutPanel2.Controls.Add(this.Btn_Save, 9, 0);
			this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel2.Location = new System.Drawing.Point(4, 4);
			this.tableLayoutPanel2.Margin = new System.Windows.Forms.Padding(4);
			this.tableLayoutPanel2.Name = "tableLayoutPanel2";
			this.tableLayoutPanel2.RowCount = 1;
			this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel2.Size = new System.Drawing.Size(1615, 52);
			this.tableLayoutPanel2.TabIndex = 0;
			// 
			// Cbo_Channel
			// 
			this.Cbo_Channel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.Cbo_Channel.FormattingEnabled = true;
			this.Cbo_Channel.Location = new System.Drawing.Point(844, 13);
			this.Cbo_Channel.Margin = new System.Windows.Forms.Padding(4);
			this.Cbo_Channel.Name = "Cbo_Channel";
			this.Cbo_Channel.Size = new System.Drawing.Size(112, 26);
			this.Cbo_Channel.TabIndex = 11;
			// 
			// label4
			// 
			this.label4.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(756, 17);
			this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(80, 18);
			this.label4.TabIndex = 10;
			this.label4.Text = "Channel:";
			// 
			// Cbo_Camera
			// 
			this.Cbo_Camera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.Cbo_Camera.FormattingEnabled = true;
			this.Cbo_Camera.Location = new System.Drawing.Point(604, 13);
			this.Cbo_Camera.Margin = new System.Windows.Forms.Padding(4);
			this.Cbo_Camera.Name = "Cbo_Camera";
			this.Cbo_Camera.Size = new System.Drawing.Size(112, 26);
			this.Cbo_Camera.TabIndex = 5;
			this.Cbo_Camera.SelectedIndexChanged += new System.EventHandler(this.Cbo_Cam_SelectedIndexChanged);
			// 
			// Cbo_Position
			// 
			this.Cbo_Position.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.Cbo_Position.FormattingEnabled = true;
			this.Cbo_Position.Location = new System.Drawing.Point(364, 13);
			this.Cbo_Position.Margin = new System.Windows.Forms.Padding(4);
			this.Cbo_Position.Name = "Cbo_Position";
			this.Cbo_Position.Size = new System.Drawing.Size(112, 26);
			this.Cbo_Position.TabIndex = 4;
			this.Cbo_Position.SelectedIndexChanged += new System.EventHandler(this.Cbo_Position_SelectedIndexChanged);
			// 
			// label1
			// 
			this.label1.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(54, 17);
			this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(62, 18);
			this.label1.TabIndex = 0;
			this.label1.Text = "JobID:";
			// 
			// label2
			// 
			this.label2.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(267, 17);
			this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(89, 18);
			this.label2.TabIndex = 1;
			this.label2.Text = "Position:";
			// 
			// label3
			// 
			this.label3.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(525, 17);
			this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(71, 18);
			this.label3.TabIndex = 2;
			this.label3.Text = "Camera:";
			// 
			// Cbo_JobID
			// 
			this.Cbo_JobID.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.Cbo_JobID.FormattingEnabled = true;
			this.Cbo_JobID.Location = new System.Drawing.Point(124, 13);
			this.Cbo_JobID.Margin = new System.Windows.Forms.Padding(4);
			this.Cbo_JobID.Name = "Cbo_JobID";
			this.Cbo_JobID.Size = new System.Drawing.Size(112, 26);
			this.Cbo_JobID.TabIndex = 3;
			this.Cbo_JobID.SelectedIndexChanged += new System.EventHandler(this.Cbo_JobID_SelectedIndexChanged);
			// 
			// Btn_Load
			// 
			this.Btn_Load.Dock = System.Windows.Forms.DockStyle.Fill;
			this.Btn_Load.Location = new System.Drawing.Point(964, 4);
			this.Btn_Load.Margin = new System.Windows.Forms.Padding(4);
			this.Btn_Load.Name = "Btn_Load";
			this.Btn_Load.Size = new System.Drawing.Size(112, 44);
			this.Btn_Load.TabIndex = 9;
			this.Btn_Load.Text = "Load";
			this.Btn_Load.UseVisualStyleBackColor = true;
			// 
			// Btn_Save
			// 
			this.Btn_Save.Location = new System.Drawing.Point(1084, 4);
			this.Btn_Save.Margin = new System.Windows.Forms.Padding(4);
			this.Btn_Save.Name = "Btn_Save";
			this.Btn_Save.Size = new System.Drawing.Size(112, 44);
			this.Btn_Save.TabIndex = 7;
			this.Btn_Save.Text = "Save";
			this.Btn_Save.UseVisualStyleBackColor = true;
			this.Btn_Save.Click += new System.EventHandler(this.Btn_Save_Click);
			// 
			// cogAcqFifoEditV21
			// 
			this.cogAcqFifoEditV21.Dock = System.Windows.Forms.DockStyle.Fill;
			this.cogAcqFifoEditV21.Location = new System.Drawing.Point(4, 64);
			this.cogAcqFifoEditV21.Margin = new System.Windows.Forms.Padding(4);
			this.cogAcqFifoEditV21.MinimumSize = new System.Drawing.Size(734, 0);
			this.cogAcqFifoEditV21.Name = "cogAcqFifoEditV21";
			this.cogAcqFifoEditV21.Size = new System.Drawing.Size(1615, 832);
			this.cogAcqFifoEditV21.SuspendElectricRuns = false;
			this.cogAcqFifoEditV21.TabIndex = 1;
			// 
			// Camera
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1623, 900);
			this.Controls.Add(this.tableLayoutPanel1);
			this.Margin = new System.Windows.Forms.Padding(4);
			this.Name = "Camera";
			this.Text = "Camera";
			this.Load += new System.EventHandler(this.Camera_Load);
			this.tableLayoutPanel1.ResumeLayout(false);
			this.tableLayoutPanel2.ResumeLayout(false);
			this.tableLayoutPanel2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.cogAcqFifoEditV21)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
		private System.Windows.Forms.ComboBox Cbo_Camera;
		private System.Windows.Forms.ComboBox Cbo_Position;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.ComboBox Cbo_JobID;
		private System.Windows.Forms.Button Btn_Save;
		private Cognex.VisionPro.CogAcqFifoEditV2 cogAcqFifoEditV21;
		private System.Windows.Forms.Button Btn_Load;
		private System.Windows.Forms.ComboBox Cbo_Channel;
		private System.Windows.Forms.Label label4;
	}
}
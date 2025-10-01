namespace Aron_V2
{
	partial class UserControl_ImageShow
	{
		/// <summary> 
		/// 必需的设计器变量。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 清理所有正在使用的资源。
		/// </summary>
		/// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region 组件设计器生成的代码

		/// <summary> 
		/// 设计器支持所需的方法 - 不要修改
		/// 使用代码编辑器修改此方法的内容。
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UserControl_ImageShow));
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.lbl_Passrate = new System.Windows.Forms.Label();
			this.lbl_Pass = new System.Windows.Forms.Label();
			this.cogRecordDisplay1 = new Cognex.VisionPro.CogRecordDisplay();
			this.button2 = new System.Windows.Forms.Button();
			this.button3 = new System.Windows.Forms.Button();
			this.button4 = new System.Windows.Forms.Button();
			this.btnReplay = new System.Windows.Forms.Button();
			this.btnTrigger = new System.Windows.Forms.Button();
			this.btnResult = new System.Windows.Forms.Button();
			this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
			this.button1 = new System.Windows.Forms.Button();
			this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
			this.lbl_Total = new System.Windows.Forms.Label();
			this.Reset = new System.Windows.Forms.Button();
			this.tableLayoutPanel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.cogRecordDisplay1)).BeginInit();
			this.tableLayoutPanel2.SuspendLayout();
			this.tableLayoutPanel3.SuspendLayout();
			this.SuspendLayout();
			// 
			// tableLayoutPanel1
			// 
			this.tableLayoutPanel1.ColumnCount = 3;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33332F));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
			this.tableLayoutPanel1.Controls.Add(this.cogRecordDisplay1, 0, 0);
			this.tableLayoutPanel1.Controls.Add(this.button2, 0, 3);
			this.tableLayoutPanel1.Controls.Add(this.button3, 1, 3);
			this.tableLayoutPanel1.Controls.Add(this.button4, 2, 3);
			this.tableLayoutPanel1.Controls.Add(this.btnReplay, 2, 2);
			this.tableLayoutPanel1.Controls.Add(this.btnTrigger, 1, 2);
			this.tableLayoutPanel1.Controls.Add(this.btnResult, 0, 2);
			this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 4);
			this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 0, 1);
			this.tableLayoutPanel1.Controls.Add(this.Reset, 2, 1);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 5;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(386, 404);
			this.tableLayoutPanel1.TabIndex = 0;
			// 
			// lbl_Passrate
			// 
			this.lbl_Passrate.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lbl_Passrate.AutoSize = true;
			this.lbl_Passrate.Location = new System.Drawing.Point(169, 7);
			this.lbl_Passrate.Name = "lbl_Passrate";
			this.lbl_Passrate.Size = new System.Drawing.Size(41, 12);
			this.lbl_Passrate.TabIndex = 11;
			this.lbl_Passrate.Text = "label1";
			// 
			// lbl_Pass
			// 
			this.lbl_Pass.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lbl_Pass.AutoSize = true;
			this.lbl_Pass.Location = new System.Drawing.Point(86, 7);
			this.lbl_Pass.Name = "lbl_Pass";
			this.lbl_Pass.Size = new System.Drawing.Size(41, 12);
			this.lbl_Pass.TabIndex = 10;
			this.lbl_Pass.Text = "label1";
			// 
			// cogRecordDisplay1
			// 
			this.cogRecordDisplay1.ColorMapLowerClipColor = System.Drawing.Color.Black;
			this.cogRecordDisplay1.ColorMapLowerRoiLimit = 0D;
			this.cogRecordDisplay1.ColorMapPredefined = Cognex.VisionPro.Display.CogDisplayColorMapPredefinedConstants.None;
			this.cogRecordDisplay1.ColorMapUpperClipColor = System.Drawing.Color.Black;
			this.cogRecordDisplay1.ColorMapUpperRoiLimit = 1D;
			this.tableLayoutPanel1.SetColumnSpan(this.cogRecordDisplay1, 3);
			this.cogRecordDisplay1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.cogRecordDisplay1.DoubleTapZoomCycleLength = 2;
			this.cogRecordDisplay1.DoubleTapZoomSensitivity = 2.5D;
			this.cogRecordDisplay1.Location = new System.Drawing.Point(3, 3);
			this.cogRecordDisplay1.MouseWheelMode = Cognex.VisionPro.Display.CogDisplayMouseWheelModeConstants.Zoom1;
			this.cogRecordDisplay1.MouseWheelSensitivity = 1D;
			this.cogRecordDisplay1.Name = "cogRecordDisplay1";
			this.cogRecordDisplay1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("cogRecordDisplay1.OcxState")));
			this.cogRecordDisplay1.Size = new System.Drawing.Size(380, 270);
			this.cogRecordDisplay1.TabIndex = 0;
			// 
			// button2
			// 
			this.button2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.button2.Location = new System.Drawing.Point(3, 343);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(122, 26);
			this.button2.TabIndex = 5;
			this.button2.Text = "JobN:null";
			this.button2.UseVisualStyleBackColor = true;
			// 
			// button3
			// 
			this.button3.BackColor = System.Drawing.Color.Gainsboro;
			this.button3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.button3.Location = new System.Drawing.Point(131, 343);
			this.button3.Name = "button3";
			this.button3.Size = new System.Drawing.Size(122, 26);
			this.button3.TabIndex = 6;
			this.button3.Text = "PosN:null";
			this.button3.UseVisualStyleBackColor = false;
			this.button3.Click += new System.EventHandler(this.button3_Click);
			// 
			// button4
			// 
			this.button4.Dock = System.Windows.Forms.DockStyle.Fill;
			this.button4.Location = new System.Drawing.Point(259, 343);
			this.button4.Name = "button4";
			this.button4.Size = new System.Drawing.Size(124, 26);
			this.button4.TabIndex = 7;
			this.button4.Text = "EngineN:null";
			this.button4.UseVisualStyleBackColor = true;
			// 
			// btnReplay
			// 
			this.btnReplay.Dock = System.Windows.Forms.DockStyle.Fill;
			this.btnReplay.Location = new System.Drawing.Point(259, 311);
			this.btnReplay.Name = "btnReplay";
			this.btnReplay.Size = new System.Drawing.Size(124, 26);
			this.btnReplay.TabIndex = 3;
			this.btnReplay.Text = "Replay";
			this.btnReplay.UseVisualStyleBackColor = true;
			this.btnReplay.Click += new System.EventHandler(this.btnReplay_Click);
			// 
			// btnTrigger
			// 
			this.btnTrigger.Dock = System.Windows.Forms.DockStyle.Fill;
			this.btnTrigger.Enabled = false;
			this.btnTrigger.Location = new System.Drawing.Point(131, 311);
			this.btnTrigger.Name = "btnTrigger";
			this.btnTrigger.Size = new System.Drawing.Size(122, 26);
			this.btnTrigger.TabIndex = 2;
			this.btnTrigger.Text = "&Trigger Manual";
			this.btnTrigger.UseVisualStyleBackColor = true;
			this.btnTrigger.Click += new System.EventHandler(this.btnTrigger_Click);
			// 
			// btnResult
			// 
			this.btnResult.BackColor = System.Drawing.Color.Yellow;
			this.btnResult.Dock = System.Windows.Forms.DockStyle.Fill;
			this.btnResult.Location = new System.Drawing.Point(3, 311);
			this.btnResult.Name = "btnResult";
			this.btnResult.Size = new System.Drawing.Size(122, 26);
			this.btnResult.TabIndex = 1;
			this.btnResult.Text = "Waiting";
			this.btnResult.UseVisualStyleBackColor = false;
			this.btnResult.Click += new System.EventHandler(this.btnResult_Click);
			// 
			// tableLayoutPanel2
			// 
			this.tableLayoutPanel2.ColumnCount = 1;
			this.tableLayoutPanel1.SetColumnSpan(this.tableLayoutPanel2, 3);
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tableLayoutPanel2.Controls.Add(this.button1, 0, 0);
			this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 375);
			this.tableLayoutPanel2.Name = "tableLayoutPanel2";
			this.tableLayoutPanel2.RowCount = 1;
			this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27F));
			this.tableLayoutPanel2.Size = new System.Drawing.Size(380, 26);
			this.tableLayoutPanel2.TabIndex = 8;
			// 
			// button1
			// 
			this.button1.BackColor = System.Drawing.Color.Yellow;
			this.button1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.button1.Location = new System.Drawing.Point(3, 3);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(374, 20);
			this.button1.TabIndex = 0;
			this.button1.Text = "CamN";
			this.button1.UseVisualStyleBackColor = false;
			// 
			// tableLayoutPanel3
			// 
			this.tableLayoutPanel3.ColumnCount = 3;
			this.tableLayoutPanel1.SetColumnSpan(this.tableLayoutPanel3, 2);
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
			this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
			this.tableLayoutPanel3.Controls.Add(this.lbl_Passrate, 2, 0);
			this.tableLayoutPanel3.Controls.Add(this.lbl_Total, 0, 0);
			this.tableLayoutPanel3.Controls.Add(this.lbl_Pass, 1, 0);
			this.tableLayoutPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 279);
			this.tableLayoutPanel3.Name = "tableLayoutPanel3";
			this.tableLayoutPanel3.RowCount = 1;
			this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tableLayoutPanel3.Size = new System.Drawing.Size(250, 26);
			this.tableLayoutPanel3.TabIndex = 12;
			// 
			// lbl_Total
			// 
			this.lbl_Total.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lbl_Total.AutoSize = true;
			this.lbl_Total.Location = new System.Drawing.Point(3, 7);
			this.lbl_Total.Name = "lbl_Total";
			this.lbl_Total.Size = new System.Drawing.Size(41, 12);
			this.lbl_Total.TabIndex = 0;
			this.lbl_Total.Text = "label1";
			// 
			// Reset
			// 
			this.Reset.Dock = System.Windows.Forms.DockStyle.Fill;
			this.Reset.Location = new System.Drawing.Point(259, 279);
			this.Reset.Name = "Reset";
			this.Reset.Size = new System.Drawing.Size(124, 26);
			this.Reset.TabIndex = 13;
			this.Reset.Text = "Reset Count";
			this.Reset.UseVisualStyleBackColor = true;
			this.Reset.Click += new System.EventHandler(this.Reset_Click);
			// 
			// UserControl_ImageShow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.tableLayoutPanel1);
			this.Name = "UserControl_ImageShow";
			this.Size = new System.Drawing.Size(386, 404);
			this.tableLayoutPanel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.cogRecordDisplay1)).EndInit();
			this.tableLayoutPanel2.ResumeLayout(false);
			this.tableLayoutPanel3.ResumeLayout(false);
			this.tableLayoutPanel3.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
		private Cognex.VisionPro.CogRecordDisplay cogRecordDisplay1;
		private System.Windows.Forms.Button btnResult;
		private System.Windows.Forms.Button btnTrigger;
		private System.Windows.Forms.Button btnReplay;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
		private System.Windows.Forms.Label lbl_Passrate;
		private System.Windows.Forms.Label lbl_Pass;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
		private System.Windows.Forms.Label lbl_Total;
		private System.Windows.Forms.Button Reset;
	}
}

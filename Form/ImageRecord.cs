using Aron_V2.UI_Update;
using Cognex.VisionPro;
using Cognex.VisionPro.Display;
using Cognex.VisionPro.ImageFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Path = System.IO.Path;

namespace Aron_V2
{
	public partial class ImageRecord : Form
	{
		public static readonly Settings Current = new Settings();
		public event EventHandler SettingsSaved;

		private void AlignIdentityFields()
		{
			if (_settingsCenterLayout) return;
			// Keep the original controls and events; only reserve space for labels.
			int left = Math.Max(label7.Right, label8.Right) + (int)(12 * DesktopTheme.DisplayScale);
			foreach (TextBox field in new[] { Lincese, Program_Name })
			{
				int right = field.Right;
				field.Left = left;
				field.Width = Math.Max(100, right - left);
			}
		}
		public ImageRecord()
		{
			InitializeComponent();
			DesktopTheme.ApplySettings(this);
			this.Shown += (s, e) => AlignIdentityFields();
			this.Load += (s, e) =>
			{
				txtRoot.Text = Current.Root;
				chkEnable.Checked = Current.EnableSave;
				cboFormat.Items.Clear();
				cboFormat.Items.Add("BMP");
				cboFormat.Items.Add("PNG");

				string fmt = (Current.FormatMode ?? "BMP").ToUpperInvariant();
				if (fmt != "BMP" && fmt != "PNG")
					fmt = "BMP";

				var idx = Math.Max(0, cboFormat.Items.IndexOf(fmt));
				cboFormat.SelectedIndex = idx;
				numRetention.Value = Math.Min(numRetention.Maximum, Current.GetRetentionMinutes());
				textBox1.Text = Current.FTP_Host;
				textBox2.Text = Current.FTP_Username;
				textBox3.Text = Current.FTP_Password;
				checkBox1.Checked = Current.FTP_Enable;
				textBox4.Text = Current.FTP_Root;
				checkBox2.Checked = Current.Show_Received_data;
				Lincese.Text = Current.Lincese_ID;
				Program_Name.Text = Current.Program_Name;
				Show_upload_info.Checked = Current.Show_upload_info;
				chkCleanupLog.Checked = Current.ShowImageCleanupLog;
			};

			btnBrowse.Click += (s, e) =>
			{
				using (var dlg = new FolderBrowserDialog())
				{
					dlg.Description = "Please Select the direction of saving Image";
					dlg.SelectedPath = string.IsNullOrEmpty(txtRoot.Text) ? Current.Root : txtRoot.Text;
					if (dlg.ShowDialog(this) == DialogResult.OK)
						txtRoot.Text = dlg.SelectedPath;
				}
			};

			btnApply.Click += (s, e) =>
			{
                try
                {
                var backup=ReadBackupSettings();
                _storageDraft?.Validate();
                if(_storageDraft!=null) Current.Storage=_storageDraft.Copy();
                if(_recoveryDraft!=null) Current.Recovery=_recoveryDraft.Copy();
                if(_recoveryEnabled!=null) Current.Recovery.Enabled=_recoveryEnabled.Checked;
				Current.EnableSave = chkEnable.Checked;
				Current.Root = txtRoot.Text.Trim();

				string fmt = ((cboFormat.SelectedItem as string) ?? "BMP").ToUpperInvariant();
				if (fmt != "BMP" && fmt != "PNG")
					fmt = "BMP";
				Current.FormatMode = fmt;

				Current.RetentionMinutes = (int)numRetention.Value;
				Current.FTP_Host = textBox1.Text.Trim();
				Current.FTP_Username = textBox2.Text.Trim();
				Current.FTP_Password = textBox3.Text.Trim();
				Current.FTP_Enable = checkBox1.Checked;
				Current.FTP_Root = textBox4.Text.Trim();
				Current.Show_Received_data = checkBox2.Checked;
				Current.Lincese_ID = Lincese.Text.Trim();
				Current.Show_upload_info = Show_upload_info.Checked;
				Current.ShowImageCleanupLog = chkCleanupLog.Checked;

				Current.Program_Name = string.IsNullOrWhiteSpace(Program_Name.Text)
					? "Aron Vision System"
					: Program_Name.Text.Trim();

				if (_languageSelector != null) Current.Language = _languageSelector.SelectedIndex == 1 ? "zh-CN" : "en";
				SaveSettings();
                if(backup!=null) ProjectBackupSettings.Save(backup);
                OperationAudit.Record("General", "Settings saved", "General settings / Project backup settings");
				UiLanguage.SetLanguage(Current.Language);

				if (SettingsSaved != null)
				{
					SettingsSaved(this, EventArgs.Empty);
				}

				MessageBox.Show(UiLanguage.Text("Save Successed！"));
                }
                catch(Exception ex)
                {
                    OperationAudit.Record("General", "Save failed", ex.GetType().Name);
                    MessageBox.Show(this,ex.Message,UiLanguage.Text("Error"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
                }
			};
		}

		public class Settings
		{
            public ImageStorageOptions Storage { get; set; }
            public CameraRecoveryOptions Recovery { get; set; } = new CameraRecoveryOptions();
			public string Language { get; set; } = "en";
			public bool ShowImageCleanupLog { get; set; } = false;
			public bool EnableSave = true;            // 是否启用保存
			public string Root = @"C:\image\";        // 根目录
													  // FormatMode: "BMP" / "PNG"
			public string FormatMode = "BMP";
			public int RetentionMinutes = -1;        // -1 表示从旧版天数配置迁移
			public int RetentionDays = 7;            // 仅用于读取旧版配置
			public bool ShouldSerializeRetentionDays() { return false; }
			public int GetRetentionMinutes()
			{
				return RetentionMinutes >= 0 ? RetentionMinutes : Math.Max(0, RetentionDays) * 1440;
			}
			public string FTP_Host { get; set; } = "";                 // FTP服务器地址
			public string FTP_Username { get; set; } = "";                // FTP用户名
			public string FTP_Password { get; set; } = "";                  // FTP密码
			public bool FTP_Enable { get; set; } = false;              // 是否启用FTP上传

			public string FTP_Root { get; set; } = "";
			public bool Show_Received_data { get; set; } = false; // 是否在界面上显示接收到的PLC数据

			public string Lincese_ID { get; set; } = ""; // 授权ID
			public string Program_Name { get; set; } = "Aron Vision System"; // 程序名称
			public bool Show_upload_info { get; set; } = false; // 是否在界面上显示上传信息
		}



        public static int CleanupOldMinutes()
        {
            int deleted=0;
            try
            {
                var policy=Current.Storage??new ImageStorageOptions();
                int minutes=Current.GetRetentionMinutes();
                bool ageCleanup=policy.RetentionEnabled && minutes>0;
                if(!ageCleanup && !policy.DiskCleanupEnabled)return 0;
                string root=SafeRoot();if(!Directory.Exists(root))return 0;
                DateTime cutoff=DateTime.Now.AddMinutes(-minutes);
                var diskCandidates=new System.Collections.Generic.List<System.IO.FileInfo>();
                var pending=new System.Collections.Generic.Stack<string>();pending.Push(root);
                while(pending.Count>0)
                {
                    string dir=pending.Pop();
                    if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;
                    foreach(string child in Directory.GetDirectories(dir))pending.Push(child);
                    string relative=Path.GetFullPath(dir).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                    var parts=relative.Split(Path.DirectorySeparatorChar);
                    bool managed=parts.Any(p=>p=="Raw"||p=="Deal") || (parts.Length==5 && (parts[4]=="OK"||parts[4]=="NG") && parts[3].Length==8 && parts[3].All(char.IsDigit));
                    if(!managed)continue;
                    foreach(string file in Directory.GetFiles(dir))
                    {
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                        if(!new[]{".bmp",".png",".cdb"}.Contains(Path.GetExtension(file).ToLowerInvariant()))continue;
                        try { if(ageCleanup && File.GetLastWriteTime(file)<cutoff){File.Delete(file);deleted++;}
                            else if(policy.DiskCleanupEnabled)diskCandidates.Add(new FileInfo(file)); }
                        catch(Exception ex){DiagnosticLogStore.Exception("Image retention "+file,ex);}
                    }
                }
                if(policy.DiskCleanupEnabled)
                {
                    var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root)));
                    foreach(var file in diskCandidates.OrderBy(f=>f.LastWriteTimeUtc))
                    {
                        if(!drive.IsReady || drive.TotalSize<=0 || 100.0*(drive.TotalSize-drive.AvailableFreeSpace)/drive.TotalSize<policy.DiskUsagePercent)break;
                        try { if((file.Attributes&FileAttributes.ReparsePoint)==0){file.Delete();deleted++;} }
                        catch(Exception ex){DiagnosticLogStore.Exception("Image disk cleanup "+file.FullName,ex);}
                    }
                }
            }
            catch(Exception ex){LogChangeEventArgs.Set("Log","Cleanup failed: "+ex.Message,Color.Red);}
            return deleted;
        }

		private static string SafeRoot()
		{
			return string.IsNullOrEmpty(Current.Root)
				? AppDomain.CurrentDomain.BaseDirectory
				: Current.Root.TrimEnd('\\', '/');
		}

		private static void TryDeleteIfEmpty(string dir)
		{
			try
			{
				if (!Directory.Exists(dir)) return;
				if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
					Directory.Delete(dir, false);
			}
			catch { }
		}

		public static void SaveSettings(string path = null)
		{
			var p = string.IsNullOrEmpty(path) ? Global.CogfigSaveImage : path;
			var dir = System.IO.Path.GetDirectoryName(p);
			if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
				System.IO.Directory.CreateDirectory(dir);

			using (var fs = System.IO.File.Create(p))
			{
				var ser = new XmlSerializer(typeof(Settings));
				ser.Serialize(fs, Current);
			}
		}

		public static void LoadSettings(string path = null)
		{
			var p = string.IsNullOrEmpty(path) ? Global.CogfigSaveImage : path;
			if (!System.IO.File.Exists(p)) return;

			try
			{
				using (var fs = System.IO.File.OpenRead(p))
				{
					var ser = new XmlSerializer(typeof(Settings));
					var cfg = ser.Deserialize(fs) as Settings;
					if (cfg != null)
					{
						Current.EnableSave = cfg.EnableSave;
						Current.Language = UiLanguage.Normalize(cfg.Language);
						Current.Root = cfg.Root;
						Current.FormatMode = cfg.FormatMode;
                        Current.Storage=cfg.Storage ?? new ImageStorageOptions { Format2D=cfg.FormatMode=="PNG"?"PNG":"BMP" };
                        Current.Recovery=cfg.Recovery ?? new CameraRecoveryOptions();
						Current.RetentionDays = cfg.RetentionDays;
						Current.RetentionMinutes = cfg.RetentionMinutes;
						Current.FTP_Host = cfg.FTP_Host;
						Current.FTP_Username = cfg.FTP_Username;
						Current.FTP_Password = cfg.FTP_Password;
						Current.FTP_Enable = cfg.FTP_Enable;
						Current.FTP_Root = cfg.FTP_Root;
						Current.Show_Received_data = cfg.Show_Received_data;
						Current.Lincese_ID = cfg.Lincese_ID;
						Current.Program_Name = string.IsNullOrWhiteSpace(cfg.Program_Name) ? "Aron Vision System" : cfg.Program_Name;
						Current.Show_upload_info = cfg.Show_upload_info;
						Current.ShowImageCleanupLog = cfg.ShowImageCleanupLog;
					}
				}
			}
			catch { /* 忽略损坏文件，保留默认 */ }
		}

		private void ImageRecord_Load(object sender, EventArgs e)
		{
			LoadSettings();

			txtRoot.Text = Current.Root;
			chkEnable.Checked = Current.EnableSave;

			cboFormat.Items.Clear();
			cboFormat.Items.Add("BMP");
			cboFormat.Items.Add("PNG");

			string fmt = (Current.FormatMode ?? "BMP").ToUpperInvariant();
			if (fmt != "BMP" && fmt != "PNG")
				fmt = "BMP";

			var idx = Math.Max(0, cboFormat.Items.IndexOf(fmt));
			cboFormat.SelectedIndex = idx;

		}
	}

}

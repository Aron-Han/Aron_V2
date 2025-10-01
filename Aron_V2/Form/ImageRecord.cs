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
		public ImageRecord()
		{
			InitializeComponent();
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
				numRetention.Value = Current.RetentionDays;
				textBox1.Text = Current.FTP_Host;
				textBox2.Text = Current.FTP_Username;
				textBox3.Text = Current.FTP_Password;
				checkBox1.Checked = Current.FTP_Enable;
				textBox4.Text = Current.FTP_Root;
				checkBox2.Checked = Current.Show_Received_data;
				Lincese.Text = Current.Lincese_ID;
				Program_Name.Text = Current.Program_Name;
				Show_upload_info.Checked = Current.Show_upload_info;
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
				Current.EnableSave = chkEnable.Checked;
				Current.Root = txtRoot.Text.Trim();

				string fmt = ((cboFormat.SelectedItem as string) ?? "BMP").ToUpperInvariant();
				if (fmt != "BMP" && fmt != "PNG")
					fmt = "BMP";
				Current.FormatMode = fmt;

				Current.RetentionDays = (int)numRetention.Value;
				Current.FTP_Host = textBox1.Text.Trim();
				Current.FTP_Username = textBox2.Text.Trim();
				Current.FTP_Password = textBox3.Text.Trim();
				Current.FTP_Enable = checkBox1.Checked;
				Current.FTP_Root = textBox4.Text.Trim();
				Current.Show_Received_data = checkBox2.Checked;
				Current.Lincese_ID = Lincese.Text.Trim();
				Current.Show_upload_info = Show_upload_info.Checked;

				Current.Program_Name = string.IsNullOrWhiteSpace(Program_Name.Text)
					? "Aron Vision System"
					: Program_Name.Text.Trim();

				SaveSettings();

				if (SettingsSaved != null)
				{
					SettingsSaved(this, EventArgs.Empty);
				}

				MessageBox.Show("Save Successed！");
			};
		}

		public class Settings
		{
			public bool EnableSave = true;            // 是否启用保存
			public string Root = @"C:\image\";        // 根目录
													  // FormatMode: "BMP" / "PNG"
			public string FormatMode = "BMP";
			public int RetentionDays = 7;             // 保留天数（按“日”整删）
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



		public static void CleanupOldDays()
		{
			try
			{
				if (Current.RetentionDays <= 0) return;

				string root = SafeRoot();
				if (!Directory.Exists(root)) return;

				DateTime cutoff = DateTime.Today.AddDays(-Current.RetentionDays);

				// 目录结构：root\job\cam\pos\yyyyMMdd\OK|NG
				foreach (var jobDir in Directory.GetDirectories(root))
				{
					foreach (var camDir in Directory.GetDirectories(jobDir))
					{
						foreach (var posDir in Directory.GetDirectories(camDir))
						{
							foreach (var dateDir in Directory.GetDirectories(posDir))
							{
								string folderName = Path.GetFileName(dateDir); // yyyyMMdd
								DateTime day;

								if (!DateTime.TryParseExact(
									folderName,
									"yyyyMMdd",
									System.Globalization.CultureInfo.InvariantCulture,
									System.Globalization.DateTimeStyles.None,
									out day))
								{
									continue;
								}

								if (day < cutoff)
								{
									TryDeleteDirectory(dateDir);
								}
							}

							TryDeleteIfEmpty(posDir);
						}

						TryDeleteIfEmpty(camDir);
					}

					TryDeleteIfEmpty(jobDir);
				}
			}
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Cleanup failed: " + ex.Message, System.Drawing.Color.Red);
			}
		}

		private static string SafeRoot()
		{
			return string.IsNullOrEmpty(Current.Root)
				? AppDomain.CurrentDomain.BaseDirectory
				: Current.Root.TrimEnd('\\', '/');
		}

		private static void TryDeleteDirectory(string dir)
		{
			try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
			catch (Exception ex)
			{
				LogChangeEventArgs.Set("Log", "Delete dir failed: " + dir + " => " + ex.Message, System.Drawing.Color.Red);
			}
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
						Current.Root = cfg.Root;
						Current.FormatMode = cfg.FormatMode;
						Current.RetentionDays = cfg.RetentionDays;
						Current.FTP_Host = cfg.FTP_Host;
						Current.FTP_Username = cfg.FTP_Username;
						Current.FTP_Password = cfg.FTP_Password;
						Current.FTP_Enable = cfg.FTP_Enable;
						Current.FTP_Root = cfg.FTP_Root;
						Current.Show_Received_data = cfg.Show_Received_data;
						Current.Lincese_ID = cfg.Lincese_ID;
						Current.Program_Name = string.IsNullOrWhiteSpace(cfg.Program_Name) ? "Aron Vision System" : cfg.Program_Name;
						Current.Show_upload_info = cfg.Show_upload_info;
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

			//numRetention.Value = Current.RetentionDays;
			
		}
	}

}

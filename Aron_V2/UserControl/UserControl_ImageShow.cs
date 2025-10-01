using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace Aron_V2
{
	public partial class UserControl_ImageShow : UserControl
	{

		public int CamIndex { get; set; } // 外部创建时赋值 0..N-1
		public event EventHandler ResetStatsRequested;

		public UserControl_ImageShow()
		{
			InitializeComponent();
			lbl_Pass.DoubleClick += OnResetDoubleClick;
			lbl_Total.DoubleClick += OnResetDoubleClick;
			lbl_Passrate.DoubleClick += OnResetDoubleClick;

            ResetStatsRequested += (s, e) => ResetCounters();
        }
		public Cognex.VisionPro.CogRecordDisplay CogDisplay => cogRecordDisplay1;
		public event EventHandler Trigger_Manual;
		public event EventHandler Reset_Count;
		public event EventHandler Replay;

		private void btnTrigger_Click(object sender, EventArgs e)
		{
			Trigger_Manual?.Invoke(this, EventArgs.Empty);
		}

		private void btnReplay_Click(object sender, EventArgs e)
		{
			Replay?.Invoke(this, EventArgs.Empty);
		}

		private void btnResult_Click(object sender, EventArgs e)
		{

		}
		public void SetButton_Parameter(Color color,string text)
		{
			btnResult.Invoke(new Action(() => btnResult.BackColor = color));
			btnResult.Invoke(new Action(() => btnResult.Text = text));
		}

        public void SetButton_JobID(Color color, string text)
        {
            btnResult.Invoke(new Action(() => button2.BackColor = color));
            btnResult.Invoke(new Action(() => button2.Text = text));
        }

        public void SetButton_PosID(Color color, string text)
        {
            btnResult.Invoke(new Action(() => button3.BackColor = color));
            btnResult.Invoke(new Action(() => button3.Text = text));
        }

        public void SetButton_EngineID(Color color, string text)
        {
            btnResult.Invoke(new Action(() => button4.BackColor = color));
            btnResult.Invoke(new Action(() => button4.Text = text));
        }

        public void SetButton_CamID(Color color, string text)
        {
            btnResult.Invoke(new Action(() => button1.BackColor = color));
            btnResult.Invoke(new Action(() => button1.Text = text));
        }
        public void SetButton_btntrigger(bool enable)
        {
            btnResult.Invoke(new Action(() => btnTrigger.Enabled = enable));
        }

        public void SetButton_CamStatus(Color color, string text)
        {
            btnResult.Invoke(new Action(() => button1.BackColor = color));
            btnResult.Invoke(new Action(() => button1.Text = text));
        }


		public string JobID
		{
			get { return button2.Text; }
		}
		public string PosID
		{
			get { return button3.Text; }
		}
		public string CamID
		{
			get { return button1.Text; }
		}
		public int Channel
		{
			get { return int.Parse(button4.Text.ToString().Substring(button4.Text.Length - 1)); }
		}

		private void button3_Click(object sender, EventArgs e)
		{
			var posList = GetPositions(button2.Text.Substring(0,4), button1.Text.Substring(0,4)).ToList();
			ShowPosPopupAbove(button3, posList,PosID);
		}

		private static IEnumerable<string> GetPositions(string job, string cam)
		{
			if (string.IsNullOrEmpty(job) || string.IsNullOrEmpty(cam) || Global.CamGeneral == null)
				return Enumerable.Empty<string>();

			string prefix = job + "." + cam + ".";
			return Global.CamGeneral.Keys
				.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.Select(k => k.Substring(prefix.Length)) // 取 "PosX"
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
		}

		void ShowPosPopupAbove(Button button3, IEnumerable<string> posList, string currentPos)
		{
			var items = posList?.ToArray() ?? Array.Empty<string>();
			if (items.Length == 0) { MessageBox.Show("无可选 Pos。"); return; }

			var pop = new PopupListForm();
			pop.Bind(items, currentPos, button3.Width);

			// 计算“上方”位置（屏幕坐标 ➜ 设置到弹窗）
			var screenAbove = button3.PointToScreen(new Point(0, -pop.Height));
			pop.Location = screenAbove;

			pop.ItemPicked += pos => {
				// 回写选择
				button3.Text = pos;
			};

			pop.Show();
			pop.Activate(); // 抢焦，保证显示在最前
		}

		public void BindStats(CameraStats stats)
		{
			// 解除旧绑定
			lbl_Pass.DataBindings.Clear();
			lbl_Total.DataBindings.Clear();
			lbl_Passrate.DataBindings.Clear();

			// 绑定（不回写到模型）
			lbl_Pass.DataBindings.Add(
				"Text", stats, "Pass", true, DataSourceUpdateMode.Never, null, "Pass：{0}");

			lbl_Total.DataBindings.Add(
				"Text", stats, "Total", true, DataSourceUpdateMode.Never, null, "Total：{0}");

			lbl_Passrate.DataBindings.Add(
				"Text", stats, "PassRateText", true, DataSourceUpdateMode.Never);

		}

		private void OnResetDoubleClick(object sender, EventArgs e)
		{
			var ret = MessageBox.Show(
				$"确定要清空 Cam{CamIndex + 1} 的统计数据吗？",
				"清空统计", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

			if (ret == DialogResult.Yes)
				ResetStatsRequested?.Invoke(this, EventArgs.Empty);
		}

        private void ResetCounters()
        {
            // 更新控件的显示
            lbl_Pass.Text = "Pass: 0";
            lbl_Total.Text = "Total: 0";
            lbl_Passrate.Text = "PassRate: 0%";
        }

		private void Reset_Click(object sender, EventArgs e)
		{
			Reset_Count?.Invoke(this, EventArgs.Empty);
		}
	}
}

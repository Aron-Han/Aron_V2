using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2  
{
	public sealed partial class SplashForm : Form   // ← 一定要继承 System.Windows.Forms.Form
	{
		private readonly Label lbl;
		private readonly ProgressBar bar;

		public SplashForm()
		{
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterScreen;
			ControlBox = false;
			TopMost = true;
			Width = 420; Height = 120;
			Text = "正在加载…";

			lbl = new Label
			{
				AutoSize = false,
				Dock = DockStyle.Top,
				Height = 40,
				TextAlign = ContentAlignment.MiddleCenter,
				Text = "启动中…"
			};
			bar = new ProgressBar { Dock = DockStyle.Bottom, Height = 22, Style = ProgressBarStyle.Continuous };

			Controls.Add(bar);
			Controls.Add(lbl);
		}

		// 线程安全的自定义进度接口
		public void Report(int percent, string message)
		{
			if (InvokeRequired) { BeginInvoke(new Action(() => Report(percent, message))); return; }
			if (percent < 0) { bar.Style = ProgressBarStyle.Marquee; }
			else
			{
				if (bar.Style != ProgressBarStyle.Continuous) bar.Style = ProgressBarStyle.Continuous;
				bar.Value = Math.Max(0, Math.Min(100, percent));
			}
			if (!string.IsNullOrEmpty(message)) lbl.Text = message;
		}
	}
}

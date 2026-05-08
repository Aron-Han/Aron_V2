using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
	public partial class StartUP : Form
	{
		public InitResult Result { get; private set; }
		private readonly TextBox _details = new TextBox
		{
			Multiline = true, ReadOnly = true, WordWrap = true,
			ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
			BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle
		};
		private readonly Label _elapsed = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
		private readonly System.Windows.Forms.Timer _elapsedTimer = new System.Windows.Forms.Timer { Interval = 500 };
		private readonly System.Diagnostics.Stopwatch _totalTime = new System.Diagnostics.Stopwatch();
		private readonly System.Diagnostics.Stopwatch _itemTime = new System.Diagnostics.Stopwatch();
		private string _currentMessage = "Preparing startup…";

		public StartUP()
		{
			InitializeComponent();
			this.ControlBox = false;
			this.StartPosition = FormStartPosition.CenterScreen;
			InitializeProgressLayout();
			this.Shown += StartUP_Shown;
			_elapsedTimer.Tick += (s, e) => _elapsed.Text = $"Total: {_totalTime.Elapsed.TotalSeconds:F1}s   |   Current operation: {_itemTime.Elapsed.TotalSeconds:F1}s";
			Disposed += (s, e) => _elapsedTimer.Dispose();
		}

		private void InitializeProgressLayout()
		{
			SuspendLayout();
			Font = new Font("Segoe UI", 9F);
			BackColor = Color.White;
			Text = "Startup - Initialization details";
			float scale = DesktopTheme.DisplayScale;
			ClientSize = new Size((int)(680 * scale), (int)(350 * scale));
			MinimumSize = new Size((int)(540 * scale), (int)(280 * scale));
			var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding((int)(12 * scale)) };
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52 * scale));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28 * scale));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28 * scale));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
			label1.AutoSize = false;
			label1.Dock = DockStyle.Fill;
			label1.TextAlign = ContentAlignment.MiddleLeft;
			label1.Text = _currentMessage;
			progressBar1.Dock = DockStyle.Fill;
			layout.Controls.Add(label1, 0, 0);
			layout.Controls.Add(progressBar1, 0, 1);
			layout.Controls.Add(_elapsed, 0, 2);
			layout.Controls.Add(_details, 0, 3);
			Controls.Add(layout);
			ResumeLayout(true);
		}

		private void DisplayProgress(ProgressInfo progress)
		{
			if (IsDisposed || Disposing) return;
			progressBar1.Value = Math.Max(progressBar1.Value, Math.Max(0, Math.Min(100, progress.Percent)));
			_currentMessage = progress.Message ?? string.Empty;
			_itemTime.Restart();
			label1.Text = _currentMessage;
			_details.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {_currentMessage}\r\n");
			// Bound UI memory only; the complete startup history remains in the log file.
			if (_details.TextLength > 120000) _details.Text = _details.Text.Substring(_details.TextLength - 90000);
			_details.SelectionStart = _details.TextLength;
			_details.ScrollToCaret();
		}

		private async void StartUP_Shown(object sender, EventArgs e)
		{
			var cts = new CancellationTokenSource();
			_totalTime.Start(); _itemTime.Start(); _elapsedTimer.Start();
			var progress = new Progress<ProgressInfo>(p =>
			{
				try
				{
					DisplayProgress(p);
				}
				catch { }
			});

			try
			{
				Result = await Task.Run<InitResult>(() =>
					Bootstrap.DoHeavyInit(progress, cts.Token));
				this.DialogResult = DialogResult.OK;
			}
			catch (OperationCanceledException)
			{
				this.DialogResult = DialogResult.Cancel;
			}
			catch (Exception ex)
			{
				string failure = "Startup failed while " + _currentMessage + "\r\n" + ex;
				try { LogManager.WriteLog("[Startup] " + failure); } catch { }
				MessageBox.Show("Load Failed：\r\n" + ex, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.DialogResult = DialogResult.Cancel;
			}
			finally
			{
				_elapsedTimer.Stop(); _totalTime.Stop(); _itemTime.Stop(); cts.Dispose();
			}
		}
	}
}

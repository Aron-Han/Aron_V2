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

		public StartUP()
		{
			InitializeComponent();
			this.ControlBox = false;
			this.StartPosition = FormStartPosition.CenterScreen;
			this.Shown += StartUP_Shown;
		}

		private async void StartUP_Shown(object sender, EventArgs e)
		{
			var cts = new CancellationTokenSource();
			var progress = new Progress<ProgressInfo>(p =>
			{
				try
				{
					progressBar1.Value = Math.Max(0, Math.Min(100, p.Percent));
					label1.Text = p.Message;
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
				MessageBox.Show("Load Failed：\r\n" + ex, "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				this.DialogResult = DialogResult.Cancel;
			}
		}
	}
}

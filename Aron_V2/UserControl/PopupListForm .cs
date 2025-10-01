using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using System.Drawing;

namespace Aron_V2
{
	sealed class PopupListForm : Form
	{
		public event Action<string> ItemPicked;

		private readonly ListBox _list;

		public PopupListForm()
		{
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			TopMost = true;              // ★ 保证在前
			BackColor = Color.White;

			_list = new ListBox
			{
				BorderStyle = BorderStyle.None,
				IntegralHeight = false,
				ItemHeight = 22,
				Dock = DockStyle.Fill
			};
			_list.Click += (s, e) => Pick();
			_list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Pick(); if (e.KeyCode == Keys.Escape) Close(); };
			Controls.Add(_list);

			Deactivate += (s, e) => Close();    // 失焦即关闭
		}

		void Pick()
		{
			if (_list.SelectedItem is string s) ItemPicked?.Invoke(s);
			Close();
		}

		public void Bind(string[] items, string current, int width, int maxHeight = 240)
		{
			_list.Items.Clear();
			_list.Items.AddRange(items);
			if (!string.IsNullOrEmpty(current))
			{
				int i = _list.Items.IndexOf(current);
				if (i >= 0) _list.SelectedIndex = i;
			}
			Width = width;                                           // 等宽按钮
			Height = Math.Min(maxHeight, Math.Max(1, _list.Items.Count) * _list.ItemHeight + 2);
		}
	}
}

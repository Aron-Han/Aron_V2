using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class PluginManagementForm : Form
    {
        private readonly PluginRuntime _runtime;
        private readonly Func<bool> _canEdit;
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, BackgroundColor = Color.White };
        internal event EventHandler SettingsSaved;
        internal PluginManagementForm(PluginRuntime runtime, Func<bool> canEdit)
        {
            _runtime = runtime; _canEdit = canEdit; Text = "External plugins"; BackColor = Color.White; Font = new Font("Segoe UI", 10);
            ClientSize = new Size(1000, 550);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.Controls.Add(new Label { Text = UiLanguage.Text("Enable optional plugins for this project. Save and restart to apply. Disabled plugins are not loaded.") + "\r\n" + runtime.ConfigPath + "\r\n" + System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"), Dock = DockStyle.Fill }, 0, 0);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = UiLanguage.Text("Enabled"), Width = 85 });
            foreach (string column in new[] { "Plugin", "Status" }) _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column, HeaderText = UiLanguage.Text(column), ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            foreach (var plugin in runtime.Available)
            {
                int index = _grid.Rows.Add(runtime.SelectedIds.Contains(plugin.Id), plugin.Name, plugin.Error ?? (plugin.Enabled ? "Loaded this session" : "Disabled this session"));
                _grid.Rows[index].Tag = plugin;
                if (plugin.Error != null) _grid.Rows[index].DefaultCellStyle.ForeColor = Color.Firebrick;
            }
            root.Controls.Add(_grid, 0, 1);
            var save = new Button { Name = "btnSave", Text = "Save", Width = 120, Height = 36, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            save.Click += (s, e) =>
            {
                try
                {
                    if (!_canEdit()) throw new InvalidOperationException(UiLanguage.Text("Switch Offline with configuration permission before saving."));
                    _grid.EndEdit();
                    var selected = _grid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells[0].Value)).Select(r => ((PluginDescriptor)r.Tag).Id);
                    _runtime.SaveEnabled(selected); SettingsSaved?.Invoke(this, EventArgs.Empty);
                    MessageBox.Show(this, UiLanguage.Text("Plugin selection saved. Restart the application to apply."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            root.Controls.Add(save, 0, 2); Controls.Add(root); DesktopTheme.ApplySettings(this);
        }
    }
}

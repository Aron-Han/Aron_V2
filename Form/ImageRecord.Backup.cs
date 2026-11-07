using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class ImageRecord
    {
        private CheckBox _backupEnabled;
        private NumericUpDown _backupInterval, _backupRetention;
        private TextBox _backupDirectory;
        private Button _backupBrowse;
        private void AddBackupSettings(Action<string> section, Action<Control,Control> field, Func<Control[],Control> line)
        {
            ProjectBackupSettings settings;
            try { settings=ProjectBackupSettings.Load(); }
            catch(Exception ex) { settings=new ProjectBackupSettings(); DiagnosticLogStore.Exception("Load backup settings",ex); }
            section("Project backup");
            _backupEnabled=new CheckBox { Text="Enable automatic Project backup", Name="EnableProjectBackup", AutoSize=true, Checked=settings.Enabled };
            _backupInterval=new NumericUpDown { Name="BackupIntervalDays", Minimum=1, Maximum=36500, DecimalPlaces=0, Increment=1, Value=settings.IntervalDays, Width=130 };
            _backupRetention=new NumericUpDown { Name="BackupRetentionDays", Minimum=1, Maximum=36500, DecimalPlaces=0, Increment=1, Value=settings.RetentionDays, Width=130 };
            _backupDirectory=new TextBox { Name="BackupDirectory", Text=settings.Directory, Width=(int)(320*DesktopTheme.DisplayScale) };
            var browse=new Button { Text="…", Name="BrowseBackupDirectory", AutoSize=false, MinimumSize=Size.Empty, Size=new Size((int)(42*DesktopTheme.DisplayScale),_backupDirectory.Height) };
            _backupBrowse=browse;
            browse.Click += (s,e)=> { using(var dialog=new FolderBrowserDialog { SelectedPath=_backupDirectory.Text }) if(dialog.ShowDialog(this)==DialogResult.OK) _backupDirectory.Text=dialog.SelectedPath; };
            field(new Label { Text="Automatic backup:" },_backupEnabled);
            field(new Label { Text="Backup interval (days):" },_backupInterval); _backupInterval.Anchor=AnchorStyles.Left;
            field(new Label { Text="Backup retention (days):" },_backupRetention); _backupRetention.Anchor=AnchorStyles.Left;
            field(new Label { Text="Backup directory:" },line(new Control[]{_backupDirectory,browse}));
            field(new Label { Text="Backup scope:" },new Label { Text="Project folder only. Takes effect after Save; the application must be running.", AutoSize=true });
            Action enable=()=> { _backupInterval.Enabled=_backupRetention.Enabled=_backupDirectory.Enabled=browse.Enabled=_backupEnabled.Checked; };
            _backupEnabled.CheckedChanged += (s,e)=>enable(); enable();
            textBox3.UseSystemPasswordChar=true;
        }
        private void AlignBackupBrowse()
        {
            if(_backupBrowse==null) return;
            _backupBrowse.AutoSize=false; _backupBrowse.MinimumSize=Size.Empty; _backupBrowse.MaximumSize=Size.Empty;
            _backupBrowse.Padding=Padding.Empty;
            _backupBrowse.Size=new Size((int)(42*DesktopTheme.DisplayScale),_backupDirectory.Height);
        }
        private ProjectBackupSettings ReadBackupSettings()
        {
            if(_backupEnabled==null) return null;
            foreach(var field in new[]{_backupInterval,_backupRetention})
            {
                decimal number;
                if(!decimal.TryParse(field.Text,out number)||number!=decimal.Truncate(number))
                    throw new ArgumentException(UiLanguage.Current=="zh-CN" ? "备份天数必须为整数。" : "Backup days must be whole numbers.");
            }
            var settings=new ProjectBackupSettings { Enabled=_backupEnabled.Checked, IntervalDays=(int)_backupInterval.Value,
                RetentionDays=(int)_backupRetention.Value, Directory=_backupDirectory.Text.Trim() };
            ProjectBackupSettings.Validate(settings,ProjectBackupSettings.ProjectDirectory); return settings;
        }
    }
}

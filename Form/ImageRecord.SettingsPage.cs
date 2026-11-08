using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class ImageRecord
    {
        private bool _settingsCenterLayout;
        private ComboBox _languageSelector;
        private bool _aligningCaptions;

        internal void PrepareForSettingsCenter()
        {
            if (_settingsCenterLayout) return;
            _settingsCenterLayout = true;
            SuspendLayout();
            float scale = DesktopTheme.DisplayScale;
            var layout = new TableLayoutPanel { Name = "GeneralSettingsLayout", Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, Padding = new Padding(10), Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145 * scale));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            System.Action<string> section = text =>
            {
                var label = new Label { Text = text, AutoSize = true, ForeColor = DesktopTheme.Accent, Margin = new Padding(0, row == 0 ? 0 : 16, 0, 8) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.Controls.Add(label, 0, row++); layout.SetColumnSpan(label, 2);
            };
            System.Action<Control, Control> field = (label, value) =>
            {
                label.AutoSize = true; label.Anchor = AnchorStyles.Left;
                label.Margin = new Padding(0, (int)(6 * scale), (int)(8 * scale), (int)(6 * scale));
                value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                if (value is TextBoxBase) value.MaximumSize = new Size((int)(560 * scale), 0);
                value.Margin = new Padding(0, (int)(6 * scale), 0, (int)(6 * scale));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize, 38 * scale));
                layout.Controls.Add(label, 0, row); layout.Controls.Add(value, 1, row++);
            };
            System.Func<Control[], Control> line = controls =>
            {
                if (controls.Length > 2)
                {
                    var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
                    foreach (Control item in controls) { item.Margin = new Padding(0, 0, (int)(10 * scale), 0); flow.Controls.Add(item); }
                    return flow;
                }
                var panel = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = controls.Length, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
                panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                panel.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
                int column = 0;
                foreach (Control item in controls)
                {
                    panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    item.Anchor = AnchorStyles.Left;
                    item.Margin = new Padding(0, 0, (int)(10 * scale), 0);
                    panel.Controls.Add(item, column++, 0);
                }
                return panel;
            };
            section("Image storage");
            field(label1, line(new Control[] { cboFormat, chkEnable }));
            field(label2, numRetention);
            numRetention.Anchor = AnchorStyles.Left;
            numRetention.Width = (int)(120 * scale);
            txtRoot.Width = (int)(320 * scale);
            field(new Label { Text = "Image directory:" }, line(new Control[] { txtRoot, btnBrowse }));
            section("FTP upload");
            field(new Label { Text = "Enable FTP:" }, checkBox1);
            field(label3, textBox1); field(label4, textBox2); field(label5, textBox3); field(label6, textBox4);
            section("Display and application");
            _languageSelector = new ComboBox { Name = "LanguageSelector", DropDownStyle = ComboBoxStyle.DropDownList, Width = (int)(180 * scale) };
            _languageSelector.Items.AddRange(new object[] { "English", "中文" });
            _languageSelector.SelectedIndex = UiLanguage.Normalize(Current.Language) == "zh-CN" ? 1 : 0;
            field(new Label { Text = "Language:" }, _languageSelector);
            _languageSelector.Anchor = AnchorStyles.Left;
            Load += (s, e) => _languageSelector.SelectedIndex = UiLanguage.Normalize(Current.Language) == "zh-CN" ? 1 : 0;
            field(new Label { Text = "Log display:" }, line(new Control[] { Show_upload_info, checkBox2, chkCleanupLog }));
            field(label7, Lincese); field(label8, Program_Name);
            AddBackupSettings(section, field, line);
            layout.RowCount = row;
            layout.Layout += (s, e) => AlignCaptionCenters(layout);
            // Save is hosted by the settings center's fixed footer, not in
            // this scrollable parameter layout. Its original handler is retained.
            // All original input/button instances were reparented, not copied.
            // Their Load, validation, browse and Save handlers are untouched.
            Controls.Add(layout);
            layout.BringToFront();
            AutoScroll = true;
            Shown += (s, e) => AlignSettingsCenterFields();
            // Embedded pages do not reliably raise Shown after their native
            // combo height settles. Align again after Load/handle creation.
            Load += (s, e) => QueueInlineAlignment();
            VisibleChanged += (s, e) => { if (Visible) QueueInlineAlignment(); };
            cboFormat.SizeChanged += (s, e) => QueueInlineAlignment();
            ResumeLayout(true);
            PrepareFeatureCards(layout);
        }

        internal event EventHandler DraftChanged;
        private void PrepareFeatureCards(Control originalLayout)
        {
            InitializeStorageDrafts();
            // Original controls remain the sole draft backing the existing Save handler.
            originalLayout.Visible=false;
            var scroll=new Panel { Name="GeneralFeatureScroll", Dock=DockStyle.Fill, AutoScroll=true,
                Padding=new Padding(10,8,SystemInformation.VerticalScrollBarWidth+10,8), BackColor=DesktopTheme.Surface };
            var cards=new TableLayoutPanel { Name="GeneralFeatureCards", Dock=DockStyle.Top, AutoSize=true,
                AutoSizeMode=AutoSizeMode.GrowAndShrink, ColumnCount=1, RowCount=0, Margin=Padding.Empty };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            scroll.Controls.Add(cards); Controls.Add(scroll); scroll.BringToFront();
            AddFeatureCard(cards,"Image storage","Image format, storage directory and retention policy.",chkEnable,"ImageStorage",()=>OpenFeatureSettings("ImageStorage"));
            AddFeatureCard(cards,"FTP upload","Server, account and upload directory.",checkBox1,"FtpUpload",()=>OpenFeatureSettings("FtpUpload"));
            AddFeatureCard(cards,"Display and application","Language, log visibility and application information.",
                new CheckBox { Name="DisplayAlwaysEnabled",Checked=true,Enabled=false },"DisplayApplication",()=>OpenFeatureSettings("DisplayApplication"));
            AddFeatureCard(cards,"Project backup","Project backup interval, retention and destination.",_backupEnabled,"ProjectBackup",()=>OpenFeatureSettings("ProjectBackup"));
            AddFeatureCard(cards,"Camera offline recovery","Automatic retry, local VPP reload and recovery scope.",_recoveryEnabled,"CameraRecovery",OpenRecoverySettings);
            UiLanguage.ApplyTo(this);
        }
        private void AddFeatureCard(TableLayoutPanel cards,string title,string description,CheckBox enabled,string key,Action configure)
        {
            float scale=DesktopTheme.DisplayScale;
            var card=new GeneralFeatureCard { Name="FeatureCard"+key, Dock=DockStyle.Fill,
                Margin=new Padding(0,0,0,(int)(12*scale)), Height=(int)(88*scale), Padding=new Padding((int)(16*scale)) };
            var row=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=3, RowCount=1, Margin=Padding.Empty, Padding=Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36*scale)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,52*scale));
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            enabled.AutoSize=false; enabled.Text=""; enabled.Size=new Size((int)(22*scale),(int)(22*scale));
            enabled.Margin=Padding.Empty; enabled.Anchor=AnchorStyles.Left;
            enabled.AccessibleName=title; enabled.CheckedChanged+=(s,e)=> { if(enabled.Focused) DraftChanged?.Invoke(this,EventArgs.Empty); };
            var text=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2, Margin=Padding.Empty };
            text.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); text.RowStyles.Add(new RowStyle(SizeType.Percent,48)); text.RowStyles.Add(new RowStyle(SizeType.Percent,52));
            var heading=new FlowLayoutPanel { Dock=DockStyle.Fill, WrapContents=false, Margin=Padding.Empty };
            var font=new Font(Font.FontFamily,10F,FontStyle.Bold);
            var name=new Label { Text=title,AutoSize=true,Font=font,ForeColor=DesktopTheme.Ink,Margin=new Padding(0,0,(int)(12*scale),0) };
            name.Disposed+=(s,e)=>font.Dispose(); heading.Controls.Add(name);
            if(!enabled.Enabled) heading.Controls.Add(new Label { Text="Always enabled",AutoSize=true,BackColor=Color.FromArgb(237,241,246),ForeColor=DesktopTheme.Muted,Padding=new Padding(5,1,5,1),Margin=Padding.Empty });
            text.Controls.Add(heading,0,0);
            text.Controls.Add(new Label { Text=description,Dock=DockStyle.Fill,ForeColor=DesktopTheme.Muted,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Margin=Padding.Empty },0,1);
            var gear=new Button { Name="Configure"+key,Text="",AccessibleName=title,Anchor=AnchorStyles.Right,
                AutoSize=false,Size=new Size((int)(38*scale),(int)(36*scale)),Margin=Padding.Empty,MinimumSize=Size.Empty };
            DesktopTheme.StyleButton(gear); gear.BackColor=Color.FromArgb(233,243,254); gear.FlatAppearance.BorderSize=0;
            gear.Image=DesktopTheme.CreateIcon(DesktopIcon.Settings,(int)(21*scale),DesktopTheme.Accent);
            gear.ImageAlign=ContentAlignment.MiddleCenter; gear.Disposed+=(s,e)=>gear.Image?.Dispose(); gear.Click+=(s,e)=>configure();
            row.Controls.Add(enabled,0,0); row.Controls.Add(text,1,0); row.Controls.Add(gear,2,0); card.Controls.Add(row);
            int index=cards.RowCount++; cards.RowStyles.Add(new RowStyle(SizeType.Absolute,100*scale)); cards.Controls.Add(card,0,index);
            Action appearance=()=> {
                if(card.IsDisposed) return;
                name.Font=font;
                gear.AutoSize=false; gear.MinimumSize=Size.Empty; gear.MaximumSize=Size.Empty; gear.Padding=Padding.Empty; gear.Margin=Padding.Empty;
                gear.Size=new Size((int)(38*scale),(int)(36*scale));
                gear.BackColor=Color.FromArgb(233,243,254); gear.FlatAppearance.BorderSize=0;
            };
            Shown+=(s,e)=>appearance();
            appearance();
        }
        private void OpenFeatureSettings(string key)
        {
            if(key=="ImageStorage") { OpenStorageRules(); return; }
            using(var dialog=CreateFeatureDialog(key))
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                dialog.CommitDraft();
                DraftChanged?.Invoke(this,EventArgs.Empty);
                OperationAudit.Record("General","Parameters staged",key);
            }
        }
        private GeneralFeatureDialog CreateFeatureDialog(string key)
        {
            string title=key=="ImageStorage"?"Image storage settings":key=="FtpUpload"?"FTP upload settings":key=="DisplayApplication"?"Display and application settings":"Project backup settings";
            var dialog=new GeneralFeatureDialog(title);
            if(key=="ImageStorage")
            {
                dialog.AddField("Save type:",cboFormat); dialog.AddField("Delete cycle (min):",numRetention);
                dialog.AddField("Image directory:",txtRoot,true);
            }
            else if(key=="FtpUpload")
            {
                dialog.AddField("IP Address:",textBox1); dialog.AddField("User:",textBox2);
                dialog.AddField("Password:",textBox3); dialog.AddField("FTP Root:",textBox4);
            }
            else if(key=="DisplayApplication")
            {
                dialog.AddField("Language:",_languageSelector);
                dialog.AddField("Show Upload image info",Show_upload_info);
                dialog.AddField("Show Data Received from PLC",checkBox2);
                dialog.AddField("Show image cleanup log",chkCleanupLog);
                dialog.AddField("Lincese:",Lincese); dialog.AddField("Program Name:",Program_Name);
            }
            else if(key=="ProjectBackup")
            {
                dialog.AddField("Backup interval (days):",_backupInterval); dialog.AddField("Backup retention (days):",_backupRetention);
                dialog.AddField("Backup directory:",_backupDirectory,true);
                dialog.ValidateDraft=()=>ProjectBackupSettings.Validate(new ProjectBackupSettings {
                    Enabled=_backupEnabled.Checked,IntervalDays=(int)((NumericUpDown)dialog.CloneFor(_backupInterval)).Value,
                    RetentionDays=(int)((NumericUpDown)dialog.CloneFor(_backupRetention)).Value,
                    Directory=dialog.CloneFor(_backupDirectory).Text.Trim() },ProjectBackupSettings.ProjectDirectory);
            }
            else { dialog.Dispose(); throw new ArgumentException("Unknown feature: "+key); }
            dialog.FinishLayout(); return dialog;
        }
        private sealed class GeneralFeatureCard : Panel
        {
            internal GeneralFeatureCard() { DoubleBuffered=true; BackColor=DesktopTheme.Surface; }
            protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); DesktopTheme.DrawInspectionSurface(e.Graphics,ClientSize,DesktopTheme.Surface,DesktopTheme.Border); }
        }
        private sealed class GeneralFeatureDialog : Form
        {
            private readonly List<Tuple<Control,Control>> _bindings=new List<Tuple<Control,Control>>();
            private readonly TableLayoutPanel _fields=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(18),Margin=Padding.Empty };
            internal Action ValidateDraft;
            internal GeneralFeatureDialog(string title)
            {
                Text=title; Name="GeneralFeatureDialog"; AutoScaleMode=AutoScaleMode.Dpi;
                StartPosition=FormStartPosition.CenterParent; FormBorderStyle=FormBorderStyle.FixedDialog;
                MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false; Width=(int)(630*DesktopTheme.DisplayScale);
                _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205*DesktopTheme.DisplayScale));
                _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                Controls.Add(_fields);
            }
            internal Control CloneFor(Control original) { return _bindings.Single(p=>ReferenceEquals(p.Item1,original)).Item2; }
            internal void AddField(string label,Control original,bool browse=false)
            {
                Control copy;
                var number=original as NumericUpDown; var combo=original as ComboBox; var check=original as CheckBox;
                if(number!=null) copy=new NumericUpDown { Minimum=number.Minimum,Maximum=number.Maximum,DecimalPlaces=number.DecimalPlaces,Increment=number.Increment,Value=number.Value,Width=140 };
                else if(combo!=null)
                {
                    var list=new ComboBox { DropDownStyle=combo.DropDownStyle };
                    list.Items.AddRange(combo.Items.Cast<object>().ToArray()); list.SelectedIndex=combo.SelectedIndex; copy=list;
                }
                else if(check!=null) copy=new CheckBox { Text="",Checked=check.Checked,AutoSize=true };
                else copy=new TextBox { Text=original.Text,UseSystemPasswordChar=(original as TextBox)?.UseSystemPasswordChar??false };
                copy.Name="Draft"+original.Name; copy.Anchor=AnchorStyles.Left|AnchorStyles.Right; copy.Margin=new Padding(0,8,0,8);
                _bindings.Add(Tuple.Create(original,copy));
                int row=_fields.RowCount++; _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _fields.Controls.Add(new Label { Text=label,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(0,8,12,8) },0,row);
                if(browse)
                {
                    var line=new TableLayoutPanel { ColumnCount=2,RowCount=1,Dock=DockStyle.Fill,AutoSize=true,Margin=Padding.Empty };
                    line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,46*DesktopTheme.DisplayScale));
                    var button=new Button { Text="…",Name="BrowseDraftDirectory",Anchor=AnchorStyles.Right,Size=new Size(36,copy.Height),Margin=new Padding(8,0,0,0) };
                    button.Click+=(s,e)=> { using(var picker=new FolderBrowserDialog { SelectedPath=copy.Text }) if(picker.ShowDialog(this)==DialogResult.OK) copy.Text=picker.SelectedPath; };
                    line.Controls.Add(copy,0,0); line.Controls.Add(button,1,0); _fields.Controls.Add(line,1,row);
                    Shown+=(s,e)=> { button.AutoSize=false; button.MinimumSize=Size.Empty; button.Padding=Padding.Empty; button.Size=new Size((int)(36*DesktopTheme.DisplayScale),copy.Height); };
                }
                else _fields.Controls.Add(copy,1,row);
            }
            internal void FinishLayout()
            {
                float scale=DesktopTheme.DisplayScale;
                var actions=new FlowLayoutPanel { Dock=DockStyle.Bottom,FlowDirection=FlowDirection.RightToLeft,Height=(int)(62*scale),Padding=new Padding(12),WrapContents=false };
                var ok=new Button { Text="OK",Name="ConfirmFeatureDraft" };
                var cancel=new Button { Text="Cancel",Name="CancelFeatureDraft",DialogResult=DialogResult.Cancel };
                ok.Click+=(s,e)=> { try {
                    foreach(var number in _bindings.Select(p=>p.Item2).OfType<NumericUpDown>())
                        if(number.DecimalPlaces==0 && number.Value!=decimal.Truncate(number.Value)) throw new ArgumentException(UiLanguage.Current=="zh-CN"?"天数及计数必须为整数。":"Days and counts must be whole numbers.");
                    ValidateDraft?.Invoke(); DialogResult=DialogResult.OK; Close();
                } catch(Exception ex) { MessageBox.Show(this,ex.Message,UiLanguage.Text("Error"),MessageBoxButtons.OK,MessageBoxIcon.Warning); } };
                actions.Controls.Add(ok); actions.Controls.Add(cancel); Controls.Add(actions);
                AcceptButton=ok; CancelButton=cancel;
                DesktopTheme.ApplySettings(this); DesktopTheme.StyleButton(ok,true); UiLanguage.ApplyTo(this);
                _fields.PerformLayout(); ClientSize=new Size((int)(630*scale),_fields.GetPreferredSize(new Size((int)(630*scale),0)).Height+actions.Height);
                foreach(var button in new[]{ok,cancel}) { button.AutoSize=false; button.MinimumSize=Size.Empty; button.Size=new Size((int)(90*scale),(int)(32*scale)); }
            }
            internal void CommitDraft()
            {
                foreach(var pair in _bindings)
                {
                    if(pair.Item1 is NumericUpDown) ((NumericUpDown)pair.Item1).Value=((NumericUpDown)pair.Item2).Value;
                    else if(pair.Item1 is ComboBox) ((ComboBox)pair.Item1).SelectedIndex=((ComboBox)pair.Item2).SelectedIndex;
                    else if(pair.Item1 is CheckBox) ((CheckBox)pair.Item1).Checked=((CheckBox)pair.Item2).Checked;
                    else pair.Item1.Text=pair.Item2.Text;
                }
            }
        }

        private bool _inlineAlignmentPending;
        private void QueueInlineAlignment()
        {
            if (_inlineAlignmentPending || !IsHandleCreated || IsDisposed) return;
            _inlineAlignmentPending = true;
            BeginInvoke(new System.Action(() =>
            {
                _inlineAlignmentPending = false;
                if (!IsDisposed) AlignSettingsCenterFields();
            }));
        }

        private void AlignSettingsCenterFields()
        {
            AlignBackupBrowse();
            float scale = DesktopTheme.DisplayScale;
            btnBrowse.AutoSize = false;
            btnBrowse.MinimumSize = Size.Empty;
            btnBrowse.MaximumSize = Size.Empty;
            btnBrowse.Padding = Padding.Empty;
            btnBrowse.Text = "…";
            btnBrowse.Size = new Size((int)(42 * scale), txtRoot.Height);
            btnBrowse.AccessibleName = "Browse image directory";
            btnBrowse.Parent.PerformLayout();
            // Native ComboBox height can become larger after handle creation
            // than TableLayoutPanel's cached preferred height. Reserve its real
            // height so the caption is centered on the combo, not a shorter row.
            foreach (Control value in new Control[] { cboFormat.Parent, txtRoot.Parent, _backupDirectory?.Parent })
            {
                var line = value as TableLayoutPanel;
                if (line == null) continue;
                int height = 0;
                foreach (Control child in line.Controls) height = System.Math.Max(height,
                    System.Math.Max(child.Height, child is ComboBox ? ((ComboBox)child).PreferredHeight : child.Height) + child.Margin.Vertical);
                height += (int)(4 * scale);
                line.AutoSize = false;
                line.MinimumSize = new Size(0, height);
                line.MaximumSize = new Size(0, height);
                line.Height = height;
                line.RowStyles[0].SizeType = SizeType.Absolute;
                line.RowStyles[0].Height = height;
                line.PerformLayout();
            }
            cboFormat.Parent.Parent.PerformLayout();
        }

        private void AlignCaptionCenters(TableLayoutPanel layout)
        {
            if (_aligningCaptions || !layout.IsHandleCreated) return;
            _aligningCaptions = true;
            layout.SuspendLayout();
            try
            {
                for (int row = 0; row < layout.RowCount; row++)
                {
                    var caption = layout.GetControlFromPosition(0, row) as Label;
                    Control field = layout.GetControlFromPosition(1, row);
                    if (caption == null || field == null || ReferenceEquals(caption, field)) continue;
                    var inline = field as TableLayoutPanel;
                    Control reference = inline != null && inline.Controls.Count != 0 ? inline.Controls[0] : field;
                    int center = layout.PointToClient(reference.PointToScreen(new Point(0, reference.Height / 2))).Y;
                    int delta = center - caption.Top - caption.Height / 2;
                    if (delta == 0) continue;
                    Padding margin = caption.Margin;
                    // Keep total row spacing unchanged while compensating for
                    // native controls whose actual height differs from the
                    // preferred height used by the table's centering engine.
                    delta = System.Math.Max(-margin.Top, System.Math.Min(margin.Bottom, delta));
                    if (delta != 0) caption.Margin = new Padding(margin.Left, margin.Top + delta, margin.Right, margin.Bottom - delta);
                }
            }
            finally { layout.ResumeLayout(true); _aligningCaptions = false; }
        }
    }
}

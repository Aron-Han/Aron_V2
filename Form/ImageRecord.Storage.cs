using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class ImageRecord
    {
        private ImageStorageOptions _storageDraft;
        private CameraRecoveryOptions _recoveryDraft;
        private CheckBox _recoveryEnabled;
        private void InitializeStorageDrafts()
        {
            _storageDraft=(Current.Storage??new ImageStorageOptions { Format2D=Current.FormatMode=="PNG"?"PNG":"BMP" }).Copy();
            _recoveryDraft=(Current.Recovery??new CameraRecoveryOptions()).Copy();
            _recoveryEnabled=new CheckBox { Name="EnableCameraRecovery",Checked=_recoveryDraft.Enabled };
        }
        private void OpenStorageRules()
        {
            using(var dialog=new StorageRulesDialog(_storageDraft,txtRoot.Text,(int)numRetention.Value))
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                _storageDraft=dialog.Options; txtRoot.Text=dialog.Root; numRetention.Value=dialog.Retention;
                cboFormat.SelectedItem=_storageDraft.Format2D;
                DraftChanged?.Invoke(this,EventArgs.Empty);
            }
        }
        private void OpenRecoverySettings()
        {
            using(var dialog=new Form { Text=UiLanguage.Text("Camera offline recovery"),StartPosition=FormStartPosition.CenterParent,
                AutoScaleMode=AutoScaleMode.Dpi,ClientSize=new Size(620,340),MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false })
            {
                var table=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,RowCount=5 };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
                var reload=new CheckBox { Checked=_recoveryDraft.ReloadVpp,AutoSize=true };
                var only=new CheckBox { Checked=_recoveryDraft.OnlyOffline,AutoSize=true };
                var seconds=new NumericUpDown { Minimum=5,Maximum=3600,Value=Math.Max(5,Math.Min(3600,_recoveryDraft.IntervalSeconds)),DecimalPlaces=0,Dock=DockStyle.Fill };
                Control[] fields={reload,only,seconds}; string[] names={"Reload local VPP on recovery","Only recover offline cameras","Retry interval (seconds)"};
                for(int i=0;i<3;i++) { table.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); table.Controls.Add(new Label { Text=UiLanguage.Text(names[i]),AutoSize=true,Anchor=AnchorStyles.Left },0,i); fields[i].Anchor=AnchorStyles.Left; table.Controls.Add(fields[i],1,i); }
                var note=new Label { Text=UiLanguage.Text("Disabled: no automatic retry. Manual recovery remains available. Repeated retries are merged in the log."),Dock=DockStyle.Fill };
                table.Controls.Add(note,0,3);table.SetColumnSpan(note,2); table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                var buttons=new FlowLayoutPanel { FlowDirection=FlowDirection.RightToLeft,Dock=DockStyle.Fill };
                var ok=new Button { Text="OK",DialogResult=DialogResult.OK,Width=110,Height=34 }; var cancel=new Button { Text="Cancel",DialogResult=DialogResult.Cancel,Width=110,Height=34 };
                buttons.Controls.AddRange(new Control[]{ok,cancel}); table.Controls.Add(buttons,0,4); table.SetColumnSpan(buttons,2);table.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
                dialog.Controls.Add(table); dialog.AcceptButton=ok;dialog.CancelButton=cancel;DesktopTheme.ApplySettings(dialog);UiLanguage.ApplyTo(dialog);
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                _recoveryDraft.ReloadVpp=reload.Checked;_recoveryDraft.OnlyOffline=only.Checked;_recoveryDraft.IntervalSeconds=(int)seconds.Value;
                DraftChanged?.Invoke(this,EventArgs.Empty);
            }
        }
    }

    internal sealed class StorageRulesDialog : Form
    {
        internal ImageStorageOptions Options;
        internal string Root => _root.Text.Trim();
        internal int Retention => (int)_retention.Value;
        private readonly TextBox _root=new TextBox { Name="ImageStorageRoot" };
        private readonly NumericUpDown _retention=new NumericUpDown { Minimum=0,Maximum=52560000,DecimalPlaces=0 };
        private readonly TextBox _preview=new TextBox { ReadOnly=true,Multiline=true,Height=80,Dock=DockStyle.Fill };
        private readonly TextBox _dealPreview=new TextBox { ReadOnly=true,Multiline=true,Dock=DockStyle.Fill };
        private readonly ListBox _directories=new ListBox { Dock=DockStyle.Fill },_filename=new ListBox { Dock=DockStyle.Fill };
        private static string T(string text)=>UiLanguage.Text(text);
        private sealed class Field
        {
            internal string Key; internal Field(string key) { Key=key; }
            public override string ToString()=>T(Key==""?"Select field":Key=="Date"?"Date":Key=="Result"?"OK/NG":Key=="Time"?"Time":Key=="Cam"?"Camera":Key=="Pos"?"Position":Key=="Job"?"Job":Key);
        }
        internal StorageRulesDialog(ImageStorageOptions options,string root,int retention)
        {
            Options=options.Copy();Text=T("Image storage settings");Name="StorageRulesDialog";StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
            Font=new Font("Segoe UI",9F);BackColor=Color.White;ForeColor=Color.FromArgb(21,30,50);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(960,630);MinimumSize=new Size(880,640);
            var footer=new FlowLayoutPanel { Dock=DockStyle.Fill,Height=64,WrapContents=false,Padding=new Padding(16,14,16,14),FlowDirection=FlowDirection.RightToLeft,BackColor=Color.FromArgb(246,248,252) };
            footer.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(215,224,237)))e.Graphics.DrawLine(pen,0,0,footer.Width,0);};
            var ok=new StorageActionButton { Name="ConfirmStorageRules",Text="OK",Width=112,Height=34,Primary=true }; var cancel=new StorageActionButton { Name="CancelStorageRules",Text="Cancel",Width=112,Height=34,DialogResult=DialogResult.Cancel };
            footer.Controls.AddRange(new Control[]{ok,cancel});Controls.Add(footer);AcceptButton=ok;CancelButton=cancel;
            var layout=new Panel { Dock=DockStyle.Fill };
            Controls.Remove(footer);Controls.Add(layout);
            var split=new Panel { Dock=DockStyle.Fill };
            layout.Controls.Add(split);
            footer.Dock=DockStyle.Bottom;layout.Controls.Add(footer);
            var nav=new Panel { Dock=DockStyle.Left,Width=176,Padding=new Padding(7,16,0,0) };
            nav.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(212,223,237)))e.Graphics.DrawLine(pen,nav.Width-1,0,nav.Width-1,nav.Height);};
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true };
            // Keep the preview out of the scrolling table: its full border and
            // text area must remain visible independently of scroll rounding/DPI.
            var right=new Panel { Dock=DockStyle.Fill,Padding=new Padding(18,20,14,18) };
            var previewHost=new Panel { Dock=DockStyle.Bottom,Height=160,Padding=new Padding(1),Visible=false };
            previewHost.Paint+=(s,e)=>DrawCardBorder(e.Graphics,previewHost.ClientRectangle);
            var previewHeading=new StorageSectionHeader { Text=T("Live preview"),Dock=DockStyle.Top,Height=32 };
            var tabs=new FlowLayoutPanel { Dock=DockStyle.Top,Height=36,WrapContents=false,Padding=new Padding(12,4,0,0) };
            var rawTab=new StoragePreviewTab { Text="Raw",Checked=true,Width=76,Height=32 };
            var dealTab=new StoragePreviewTab { Text="Deal",Width=76,Height=32 };
            tabs.Controls.AddRange(new Control[]{rawTab,dealTab});
            _preview.Dock=DockStyle.Fill;_dealPreview.Visible=false;
            var previewInset=new Panel { Dock=DockStyle.Fill,Padding=new Padding(12,0,12,16) };
            var textFrame=new Panel { Dock=DockStyle.Fill,Padding=new Padding(10) };
            textFrame.Paint+=(s,e)=>DrawCardBorder(e.Graphics,textFrame.ClientRectangle);
            _preview.BorderStyle=_dealPreview.BorderStyle=BorderStyle.None;
            textFrame.Controls.Add(_preview);textFrame.Controls.Add(_dealPreview);previewInset.Controls.Add(textFrame);
            previewHost.Controls.Add(previewInset);previewHost.Controls.Add(tabs);previewHost.Controls.Add(previewHeading);
            rawTab.CheckedChanged+=(s,e)=>{_preview.Visible=rawTab.Checked;_dealPreview.Visible=!rawTab.Checked;rawTab.BackColor=rawTab.Checked?Color.FromArgb(232,243,255):Color.White;dealTab.BackColor=dealTab.Checked?Color.FromArgb(232,243,255):Color.White;};
            right.Controls.Add(scroll);right.Controls.Add(previewHost);
            var general=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1 };
            var directories=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=1 };
            general.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));directories.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var body=general;scroll.Controls.Add(general);scroll.Controls.Add(directories);directories.Visible=false;
            foreach(string page in new[]{"General","Directories and names"})
            {
                var button=new StorageNavButton { Name=page=="General"?"StorageGeneralNav":"StorageDirectoriesNav",Text=T(page=="Directories and names"?"Directories && naming":page),Selected=page=="General",Height=38,Dock=DockStyle.Top };
                button.Click+=(s,e)=>{scroll.SuspendLayout();general.Visible=page=="General";directories.Visible=page!="General";previewHost.Visible=page!="General";scroll.AutoScrollPosition=Point.Empty;scroll.ResumeLayout(true);foreach(StorageNavButton b in nav.Controls){b.Selected=ReferenceEquals(b,button);b.Invalidate();}};nav.Controls.Add(button);button.BringToFront();
            }
            nav.Layout+=(s,e)=>{int width=Math.Max((int)(176*Font.Height/15F),nav.Controls.Cast<Control>().Max(c=>TextRenderer.MeasureText(c.Text.Replace("&&","&"),c.Font,Size.Empty,TextFormatFlags.NoPrefix).Width)+20);if(nav.Width!=width)nav.Width=width;};
            split.Controls.Add(right);split.Controls.Add(nav);
            Action<string,Control> add=(title,content)=> {
                var heading=new Label { Text=T(title),AutoSize=true,ForeColor=DesktopTheme.Accent,Margin=new Padding(0,12,0,8) };
                if(title=="Directories and names")Shown+=(s,e)=>{heading.Font=new Font(Font.FontFamily,12F,FontStyle.Bold);heading.ForeColor=DesktopTheme.Ink;};
                body.Controls.Add(heading);content.Dock=DockStyle.Top;body.Controls.Add(content);
            };
            var formats=Table();
            Add(formats,"2D raw format",Select(new[]{"BMP","PNG"},Options.Format2D,v=>Options.Format2D=v));
            Add(formats,"3D raw format",Select(new[]{"CDB"},"CDB",v=>Options.Format3D=v));
            Add(formats,"Save 2D effect image",Check(Options.SaveDeal2D,v=>Options.SaveDeal2D=v));
            Add(formats,"Save 3D effect image",Check(Options.SaveDeal3D,v=>Options.SaveDeal3D=v));
            Add(formats,"Effect image format",Select(new[]{"BMP","PNG"},Options.DealFormat,v=>Options.DealFormat=v));
            add("Save formats",formats);
            add("CDB preserves 3D range data. Effect image: CogIPOneImageTool.OutputImage.",new Label { Height=2 });
            var retentionTable=Table();_retention.Value=Math.Min(_retention.Maximum,Math.Max(0,retention));
            Add(retentionTable,"Enable scheduled deletion",Check(Options.RetentionEnabled,v=>{Options.RetentionEnabled=v;_retention.Enabled=v;}));
            Add(retentionTable,"Delete cycle (min):",_retention);_retention.Enabled=Options.RetentionEnabled;
            var threshold=new NumericUpDown { Minimum=1,Maximum=99,Value=Math.Max(1,Math.Min(99,Options.DiskUsagePercent)),DecimalPlaces=0,Enabled=Options.DiskCleanupEnabled };
            threshold.ValueChanged+=(s,e)=>Options.DiskUsagePercent=(int)threshold.Value;
            Add(retentionTable,"Enable disk usage cleanup",Check(Options.DiskCleanupEnabled,v=>{Options.DiskCleanupEnabled=v;threshold.Enabled=v;}));
            Add(retentionTable,"Disk usage threshold (%)",threshold);add("Retention policy",retentionTable);
            add("Deletes oldest stored images in the image directory only, until disk usage is below the threshold.",new Label { Height=2 });
            body=directories;
            _root.Text=root;_root.Dock=DockStyle.Fill;
            var location=Table();var line=new TableLayoutPanel { ColumnCount=2,Dock=DockStyle.Fill,AutoSize=true };line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));line.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,50));
            line.ColumnStyles[1].Width=104;
            var browse=new StorageActionButton { Text=T("Browse..."),FolderIcon=true,Dock=DockStyle.Fill };browse.Click+=(s,e)=>{ using(var d=new FolderBrowserDialog { SelectedPath=Root }) if(d.ShowDialog(this)==DialogResult.OK) _root.Text=d.SelectedPath; };
            line.Controls.Add(_root,0,0);line.Controls.Add(browse,1,0);Add(location,"Root directory:",line);
            location.GetControlFromPosition(0,0).Font=new Font(Font,FontStyle.Bold);
            location.Margin=new Padding(0,0,0,12);location.Dock=DockStyle.Top;directories.Controls.Add(location);
            foreach(var f in Options.Directories) _directories.Items.Add(new Field(f));foreach(var f in Options.FileFields) _filename.Items.Add(new Field(f));
            var cards=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Name="StorageRuleCards" };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            cards.Controls.Add(RuleEditor(_directories,"Folder hierarchy"),0,0);
            cards.Controls.Add(RuleEditor(_filename,"Filename fields"),1,0);
            cards.Controls[0].Margin=new Padding(0,0,6,0);cards.Controls[1].Margin=new Padding(6,0,0,0);
            directories.Controls.Add(cards);
            var hint=new Label { Text=T("Camera folders contain separate Raw and Deal subfolders."),AutoSize=true,ForeColor=Color.FromArgb(65,65,65),Margin=new Padding(0,8,0,4) };directories.Controls.Add(hint);
            _root.TextChanged+=(s,e)=>Preview();
            ok.Click+=(s,e)=> {
                try { Sync();Options.Validate();if(string.IsNullOrWhiteSpace(Root)||!System.IO.Path.IsPathRooted(Root)) throw new ArgumentException(T("Select an absolute image directory."));DialogResult=DialogResult.OK; }
                catch(Exception ex) { MessageBox.Show(this,ex.Message,T("Error")); }
            };
            UiLanguage.ApplyTo(this);
            // Lists are the draft model only; the visible editor is a compact
            // two-column card with per-row actions, rather than oversized buttons.
            Disposed+=(s,e)=>{_directories.Dispose();_filename.Dispose();};
            _preview.ScrollBars=ScrollBars.Vertical;_dealPreview.ScrollBars=ScrollBars.Vertical;
            _preview.BackColor=_dealPreview.BackColor=Color.White;
            Preview();
        }
        private Control RuleEditor(ListBox list,string title)
        {
            var card=new TableLayoutPanel { Dock=DockStyle.Top,ColumnCount=1,RowCount=3,AutoSize=true,Padding=new Padding(1),Margin=new Padding(0,0,12,0) };
            card.Paint+=(s,e)=>DrawCardBorder(e.Graphics,card.ClientRectangle);
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<3;i++)card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var heading=new StorageSectionHeader { Text=T(title),Dock=DockStyle.Fill,Height=34,Margin=Padding.Empty };
            card.Controls.Add(heading);
            var rows=new ListView { Name=list==_directories?"FolderRuleRows":"FilenameRuleRows",View=View.Details,HeaderStyle=ColumnHeaderStyle.None,FullRowSelect=true,MultiSelect=false,HideSelection=false,Dock=DockStyle.Fill,Height=160,Margin=new Padding(12,12,12,4),ShowItemToolTips=true,BorderStyle=BorderStyle.FixedSingle,OwnerDraw=true,Font=Font };
            rows.DrawColumnHeader+=(s,e)=>e.DrawDefault=true;
            rows.DrawSubItem+=(s,e)=>{
                using(var brush=new SolidBrush(e.Item.Selected?Color.FromArgb(232,243,255):Color.White))e.Graphics.FillRectangle(brush,e.Bounds);
                using(var pen=new Pen(Color.FromArgb(191,204,221)))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
                if(e.ColumnIndex==0){var textBounds=e.Bounds;textBounds.X+=30;textBounds.Width-=30;TextRenderer.DrawText(e.Graphics,e.SubItem.Text,rows.Font,textBounds,DesktopTheme.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                    using(var brush=new SolidBrush(DesktopTheme.Ink))for(int y=-1;y<=1;y++)for(int x=0;x<2;x++)e.Graphics.FillEllipse(brush,e.Bounds.Left+7+x*4,e.Bounds.Top+e.Bounds.Height/2+y*4-1,2,2);return;}
                float cx=e.Bounds.Left+e.Bounds.Width/2F,cy=e.Bounds.Top+e.Bounds.Height/2F,u=Math.Max(3,rows.Font.Height/4F);
                using(var pen=new Pen(DesktopTheme.Ink,1.4F)){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if(e.ColumnIndex<3){float direction=e.ColumnIndex==1?-1:1;e.Graphics.DrawLine(pen,cx,cy-u,cx,cy+u);e.Graphics.DrawLine(pen,cx,cy+direction*u,cx-u,cy);e.Graphics.DrawLine(pen,cx,cy+direction*u,cx+u,cy);}
                    else{e.Graphics.DrawRectangle(pen,cx-u,cy-u,u*2,u*2.2F);e.Graphics.DrawLine(pen,cx-u-2,cy-u-2,cx+u+2,cy-u-2);e.Graphics.DrawLine(pen,cx-2,cy-u-4,cx+2,cy-u-4);}}
            };
            rows.Columns.Add("Field");rows.Columns.Add("Up");rows.Columns.Add("Down");rows.Columns.Add("Delete");
            FontChanged+=(s,e)=>rows.Font=Font;
            var images=new ImageList { ImageSize=new Size(1,30),ColorDepth=ColorDepth.Depth32Bit };images.Images.Add(new Bitmap(1,30));rows.SmallImageList=images;rows.Disposed+=(s,e)=>images.Dispose();
            Action refresh=()=>{rows.BeginUpdate();rows.Items.Clear();for(int i=0;i<list.Items.Count;i++){var row=new ListViewItem((i+1)+".   "+list.Items[i],0);row.SubItems.Add("↑");row.SubItems.Add("↓");row.SubItems.Add("×");row.ToolTipText=list.Items[i]+" — "+T("Move up")+" / "+T("Move down")+" / "+T("Delete Selected");rows.Items.Add(row);}rows.EndUpdate();};
            Action<int,int> edit=(index,action)=>{if(index<0||index>=list.Items.Count)return;if(action==3)list.Items.RemoveAt(index);else{int next=index+(action==1?-1:1);if(next<0||next>=list.Items.Count)return;object item=list.Items[index];list.Items.RemoveAt(index);list.Items.Insert(next,item);}refresh();Preview();};
            rows.MouseClick+=(s,e)=>{var hit=rows.HitTest(e.Location);if(hit.Item==null||hit.SubItem==null)return;int column=hit.Item.SubItems.IndexOf(hit.SubItem);if(column>0)edit(hit.Item.Index,column);};
            rows.KeyDown+=(s,e)=>{if(rows.SelectedIndices.Count==0)return;int index=rows.SelectedIndices[0];if(e.KeyCode==Keys.Delete){edit(index,3);e.Handled=true;}else if(e.Alt&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){edit(index,e.KeyCode==Keys.Up?1:2);e.Handled=true;}};
            rows.Resize+=(s,e)=>{int action=Math.Max(28,rows.Font.Height+10);rows.Columns[0].Width=Math.Max(80,rows.ClientSize.Width-3*action-SystemInformation.VerticalScrollBarWidth-4);for(int i=1;i<4;i++)rows.Columns[i].Width=action;};
            card.Controls.Add(rows);
            var panel=new Panel { Dock=DockStyle.Top,Height=38,Margin=new Padding(12,8,12,12) };
            var picker=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill };picker.Items.Add(new Field(""));picker.Items.AddRange(ImageStorageOptions.Fields.Select(f=>(object)new Field(f)).ToArray());picker.SelectedIndex=0;
            var add=new StorageActionButton { Text=T("+ Add"),AccessibleName=T("Add field"),Dock=DockStyle.Right,Width=82 };add.Click+=(s,e)=> { var f=(Field)picker.SelectedItem;if(f.Key.Length==0)return;if(!list.Items.Cast<Field>().Any(x=>x.Key==f.Key)) list.Items.Add(new Field(f.Key));refresh();Preview(); };
            panel.Layout+=(s,e)=>{string text=panel.ClientSize.Width>Font.Height*12?T("+ Add"):"+";if(add.Text!=text)add.Text=text;};
            panel.Controls.Add(picker);panel.Controls.Add(add);card.Controls.Add(panel);refresh();return card;
        }
        private class StorageActionButton : Button
        {
            internal bool Primary;
            internal bool FolderIcon;
            private bool _hover;
            internal StorageActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;Margin=new Padding(5,0,0,0);}
            protected override void OnMouseEnter(EventArgs e){_hover=true;Invalidate();base.OnMouseEnter(e);}
            protected override void OnMouseLeave(EventArgs e){_hover=false;Invalidate();base.OnMouseLeave(e);}
            protected override void OnPaint(PaintEventArgs e)
            {
                var box=ClientRectangle;box.Inflate(-1,-1);
                using(var path=CardPath(box,6))
                using(var brush=new System.Drawing.Drawing2D.LinearGradientBrush(box,Primary?Color.FromArgb(53,89,220):Color.White,Primary?Color.FromArgb(43,76,204):Color.FromArgb(_hover?232:243, _hover?240:246,252),90F))
                using(var pen=new Pen(Primary?Color.FromArgb(47,80,207):Color.FromArgb(166,183,205)))
                {e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
                var textBox=ClientRectangle;
                if(FolderIcon){float y=Height/2F;using(var pen=new Pen(ForeColor,1.2F)){e.Graphics.DrawRectangle(pen,12,y-4,14,10);e.Graphics.DrawLines(pen,new[]{new PointF(12,y-4),new PointF(12,y-7),new PointF(18,y-7),new PointF(21,y-4)});}textBox.X+=25;textBox.Width-=25;}
                TextRenderer.DrawText(e.Graphics,Text,Font,textBox,Primary?Color.White:ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
        }
        private sealed class StorageNavButton : StorageActionButton
        {
            internal bool Selected;
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Selected?Color.FromArgb(218,232,254):Color.White);
                if(Selected)using(var brush=new SolidBrush(Color.FromArgb(50,99,234)))e.Graphics.FillRectangle(brush,0,3,4,Height-6);
                using(var font=new Font(Font,Selected?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(e.Graphics,Text.Replace("&&","&"),font,new Rectangle(14,0,Width-18,Height),Color.FromArgb(20,31,59),TextFormatFlags.NoPrefix|TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
        }
        private sealed class StorageSectionHeader : Label
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                using(var brush=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,Color.FromArgb(224,236,249),Color.FromArgb(204,222,240),90F))e.Graphics.FillRectangle(brush,ClientRectangle);
                using(var font=new Font(Font.FontFamily,Font.Size*1.15F,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,Text,font,new Rectangle(12,0,Width-20,Height),Color.FromArgb(20,36,69),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            }
        }
        private sealed class StoragePreviewTab : RadioButton
        {
            internal StoragePreviewTab(){Appearance=Appearance.Button;FlatStyle=FlatStyle.Flat;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);Margin=new Padding(0);Cursor=Cursors.Hand;CheckedChanged+=(s,e)=>Invalidate();}
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);
                if(Checked){using(var pen=new Pen(Color.FromArgb(202,215,232)))e.Graphics.DrawRectangle(pen,1,1,Width-3,Height);using(var pen=new Pen(Color.FromArgb(55,100,235),3))e.Graphics.DrawLine(pen,1,Height-2,Width-2,Height-2);}
                using(var font=new Font(Font,Checked?FontStyle.Bold:FontStyle.Regular))TextRenderer.DrawText(e.Graphics,Text,font,ClientRectangle,Checked?Color.FromArgb(42,87,222):Color.FromArgb(28,37,55),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
        }
        private static System.Drawing.Drawing2D.GraphicsPath CardPath(Rectangle box,int radius)
        {
            var path=new System.Drawing.Drawing2D.GraphicsPath();int r=Math.Max(2,Math.Min(radius,Math.Min(box.Width,box.Height)/2));
            path.AddArc(box.Left,box.Top,r,r,180,90);path.AddArc(box.Right-r,box.Top,r,r,270,90);path.AddArc(box.Right-r,box.Bottom-r,r,r,0,90);path.AddArc(box.Left,box.Bottom-r,r,r,90,90);path.CloseFigure();return path;
        }
        private void Sync() { Options.Directories=_directories.Items.Cast<Field>().Select(f=>f.Key).ToArray();Options.FileFields=_filename.Items.Cast<Field>().Select(f=>f.Key).ToArray(); }
        private static void DrawCardBorder(Graphics graphics,Rectangle bounds)
        {
            if(bounds.Width<12||bounds.Height<12)return;
            using(var path=new System.Drawing.Drawing2D.GraphicsPath())
            using(var pen=new Pen(Color.FromArgb(211,224,238)))
            {
                int right=bounds.Right-1,bottom=bounds.Bottom-1,r=10;
                path.AddArc(0,0,r,r,180,90);path.AddArc(right-r,0,r,r,270,90);path.AddArc(right-r,bottom-r,r,r,0,90);path.AddArc(0,bottom-r,r,r,90,90);path.CloseFigure();
                graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;graphics.DrawPath(pen,path);
            }
        }
        private void Preview()
        {
            try { Sync();_preview.Text=Options.BuildPath(Root,"Job1","Cam1","Pos1","PN001",true,"PartA",new DateTime(2026,11,8,14,25,30,123),false,Options.Format2D);_dealPreview.Text=Options.BuildPath(Root,"Job1","Cam1","Pos1","PN001",true,"PartA",new DateTime(2026,11,8,14,25,30,123),true,Options.DealFormat); }
            catch(Exception ex) { _preview.Text=ex.Message; }
        }
        private static TableLayoutPanel Table() {var t=new TableLayoutPanel { AutoSize=true,ColumnCount=2 };t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return t;}
        private static void Add(TableLayoutPanel table,string caption,Control control) {int r=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));control.Margin=new Padding(0,6,0,6);control.Anchor=AnchorStyles.Left|AnchorStyles.Right;table.Controls.Add(new Label { Text=T(caption),AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,6,24,6) },0,r);table.Controls.Add(control,1,r);}
        private ComboBox Select(string[] items,string value,Action<string> change) {var c=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };c.Items.AddRange(items);c.SelectedItem=value;c.SelectedIndexChanged+=(s,e)=>{change((string)c.SelectedItem);Preview();};return c;}
        private static CheckBox Check(bool value,Action<bool> change) {var c=new CheckBox { Checked=value,AutoSize=true };c.CheckedChanged+=(s,e)=>change(c.Checked);return c;}
    }
}

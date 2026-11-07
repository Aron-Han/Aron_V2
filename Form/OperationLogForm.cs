using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class OperationLogForm : Form
    {
        private readonly DateTimePicker _start = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 135 };
        private readonly DateTimePicker _end = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 135 };
        private readonly TextBox _search = new TextBox { Name = "operationSearch", Width = 180 };
        private readonly Button _refresh = new Button { Text = "Filter", Name = "filterOperations" };
        private readonly Button _export = new Button { Text = "Export", Name = "exportOperations" };
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false };
        private readonly Label _status = new Label { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
        private bool _busy;
        internal OperationLogForm()
        {
            Text = "Operation Log"; Name = "OperationLogForm"; StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(1060,650); MinimumSize = new Size(800,480);
            _start.Value = _end.Value = DateTime.Today;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            var toolbar = new TableLayoutPanel { Name="OperationFilterBar", Dock=DockStyle.Fill, AutoSize=true,
                ColumnCount=8, RowCount=1, Margin=Padding.Empty, Padding=new Padding(0,0,0,12), GrowStyle=TableLayoutPanelGrowStyle.FixedSize };
            toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            foreach(var style in new[]{SizeType.AutoSize,SizeType.Absolute,SizeType.AutoSize,SizeType.Absolute,SizeType.AutoSize,SizeType.Percent,SizeType.Absolute,SizeType.Absolute})
                toolbar.ColumnStyles.Add(new ColumnStyle(style,style==SizeType.Percent?100:0));
            Control[] filters={Caption("Start:"),_start,Caption("End:"),_end,Caption("Search:"),_search,_refresh,_export};
            for(int i=0;i<filters.Length;i++)
            {
                filters[i].Anchor=i==5?AnchorStyles.Left|AnchorStyles.Right:AnchorStyles.Left;
                filters[i].Margin=new Padding(0,6,10,6);
                toolbar.Controls.Add(filters[i],i,0);
            }
            _start.Format=_end.Format=DateTimePickerFormat.Custom;
            _start.CustomFormat=_end.CustomFormat="yyyy-MM-dd";
            layout.Controls.Add(toolbar,0,0); layout.Controls.Add(_grid,0,1); layout.Controls.Add(_status,0,2); Controls.Add(layout);
            string[] names = { "Time", "User", "Window", "Action", "Content" };
            int[] widths = { 195,100,210,145,380 };
            for (int i=0;i<names.Length;i++) _grid.Columns.Add(new DataGridViewTextBoxColumn { Name=names[i], HeaderText=names[i],
                DataPropertyName=names[i], Width=widths[i], MinimumWidth=70,
                AutoSizeMode=i==4 ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
                SortMode=DataGridViewColumnSortMode.NotSortable });
            _grid.Columns["Time"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss.fff";
            _grid.CellFormatting += (s,e)=> { if(e.RowIndex>=0 && e.ColumnIndex==3) { e.Value=UiLanguage.Text(Convert.ToString(e.Value)); e.FormattingApplied=true; } };
            _grid.CellDoubleClick += (s,e) => {
                if(e.RowIndex<0) return;
                var record = _grid.Rows[e.RowIndex].DataBoundItem as OperationRecord;
                if(record==null) return;
                using(var details=new Form { Text=UiLanguage.Text("Operation Log"), Size=new Size(760,420), StartPosition=FormStartPosition.CenterParent })
                {
                    details.Controls.Add(new TextBox { Multiline=true, ReadOnly=true, WordWrap=true, ScrollBars=ScrollBars.Vertical, Dock=DockStyle.Fill,
                        Text=record.Time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")+"\r\n"+record.User+"\r\n"+record.Window+"\r\n"+record.Action+"\r\n"+record.Content+"\r\n"+record.Version+" / "+record.Build });
                    DesktopTheme.ApplySettings(details); details.ShowDialog(this);
                }
            };
            _refresh.Click += async (s,e) => await Reload();
            _export.Click += async (s,e) => await Export();
            Shown += async (s,e) => await Reload();
            DesktopTheme.ApplySettings(this); UiLanguage.ApplyTo(this);
            bool aligningToolbar=false;
            Action alignToolbar=()=> {
                if(aligningToolbar) return;
                aligningToolbar=true;
                float scale=DeviceDpi/96F;
                toolbar.SuspendLayout();
                try
                {
                    toolbar.ColumnStyles[1].Width=toolbar.ColumnStyles[3].Width=140*scale;
                    toolbar.ColumnStyles[6].Width=toolbar.ColumnStyles[7].Width=98*scale;
                    foreach(var button in new[]{_refresh,_export})
                    { button.AutoSize=false; button.MinimumSize=Size.Empty; button.MaximumSize=Size.Empty; button.Size=new Size((int)(88*scale),Math.Max(_search.Height,(int)(30*scale))); button.Padding=Padding.Empty; }
                    _start.Width=_end.Width=(int)(130*scale);
                    // On narrow windows put the actions on a second, right-aligned row.
                    bool compact=toolbar.ClientSize.Width<(int)(850*scale);
                    toolbar.RowCount=compact?2:1;
                    if(toolbar.RowStyles.Count<2) toolbar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    toolbar.SetCellPosition(_refresh,new TableLayoutPanelCellPosition(6,compact?1:0));
                    toolbar.SetCellPosition(_export,new TableLayoutPanelCellPosition(7,compact?1:0));
                    toolbar.SetColumnSpan(_search,compact?3:1);
                }
                finally { toolbar.ResumeLayout(true); aligningToolbar=false; }
            };
            toolbar.SizeChanged+=(s,e)=>alignToolbar();
            Shown+=(s,e)=>alignToolbar();
            alignToolbar();
        }
        private static Label Caption(string text) { return new Label { Text=text, AutoSize=true, TextAlign=ContentAlignment.MiddleLeft }; }
        private async Task Reload()
        {
            if (_busy) return;
            if (_start.Value.Date>_end.Value.Date) { MessageBox.Show(this,UiLanguage.Text("Start date must not be after end date."),Text); return; }
            _busy=true; _refresh.Enabled=_export.Enabled=false;
            DateTime start=_start.Value.Date, end=_end.Value.Date.AddDays(1).AddTicks(-1); string search=_search.Text;
            try
            {
                var rows=await Task.Run(()=>OperationAudit.Read(start,end,search));
                if(IsDisposed) return;
                _grid.DataSource=rows;
                _status.Text=UiLanguage.Current=="zh-CN" ? $"显示最新 {rows.Count} 条（最多 5000 条），全部原始记录保存在：{OperationAudit.Root}" :
                    $"Latest {rows.Count} records (up to 5000). All records retained in: {OperationAudit.Root}";
                if(OperationAudit.LastFailure!=null) { _status.Text=UiLanguage.Text("Operation log write failed:")+" "+OperationAudit.LastFailure; _status.ForeColor=Color.Firebrick; }
                else _status.ForeColor=DesktopTheme.Ink;
            }
            catch(Exception ex) { if(!IsDisposed) MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning); }
            finally { _busy=false; if(!IsDisposed) _refresh.Enabled=_export.Enabled=true; }
        }
        private async Task Export()
        {
            if(_busy) return;
            using(var dialog=new SaveFileDialog { Filter="CSV (*.csv)|*.csv", FileName="Operations-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv" })
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                _busy=true; _export.Enabled=_refresh.Enabled=false;
                try
                {
                    var records=(_grid.DataSource as System.Collections.Generic.List<OperationRecord> ?? new System.Collections.Generic.List<OperationRecord>()).ToArray();
                    await Task.Run(()=> {
                        using(var writer=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true)))
                        {
                            writer.WriteLine("Time,User,Window,Action,Content,Version,Build");
                            foreach(var r in records) writer.WriteLine(string.Join(",",new[]{r.Time.ToString("o"),r.User,r.Window,r.Action,r.Content,r.Version,r.Build}.Select(Csv)));
                        }
                    });
                    OperationAudit.Record(Name,"Export succeeded", "Operation log / rows="+records.Length);
                    if(!IsDisposed) MessageBox.Show(this,UiLanguage.Text("Export succeeded."),Text);
                }
                catch(Exception ex) { OperationAudit.Record(Name,"Export failed",ex.GetType().Name); if(!IsDisposed) MessageBox.Show(this,ex.Message,Text); }
                finally { _busy=false; if(!IsDisposed) _export.Enabled=_refresh.Enabled=true; }
            }
        }
        private static string Csv(string value)
        {
            value=value??"";
            if(value.Length>0 && "=+-@\t\r".Contains(value[0])) value="'"+value;
            return "\""+value.Replace("\"","\"\"")+"\"";
        }
    }
}

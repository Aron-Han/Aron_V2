using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class GeneralFeatureCardsTests
{
    const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Field(object o,string name) { return o.GetType().GetField(name,Flags).GetValue(o); }
    static object Call(object o,string name,params object[] args) { return o.GetType().GetMethod(name,Flags).Invoke(o,args); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static void Pump() { for(int i=0;i<8;i++) Application.DoEvents(); }
    static System.Collections.Generic.IEnumerable<Control> All(Control c) { foreach(Control child in c.Controls) { yield return child; foreach(var d in All(child)) yield return d; } }
    static void Capture(Form form,string path) { using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(path); } }
    [STAThread] static int Main(string[] args)
    {
        try { Run(args); return 0; } catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
    static void Run(string[] args)
    {
        Application.EnableVisualStyles(); var app=Assembly.LoadFrom(args[0]);
        var language=app.GetType("Aron_V2.UiLanguage");
        string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Screenshots"); Directory.CreateDirectory(folder);
        using(var general=(Form)Activator.CreateInstance(app.GetType("Aron_V2.ImageRecord")))
        {
            Call(general,"PrepareForSettingsCenter");
            var center=app.GetType("Aron_V2.SettingsCenterForm"); var pageType=center.GetNestedType("Page",Flags); var page=Activator.CreateInstance(pageType,true);
            pageType.GetField("Title",Flags).SetValue(page,"General"); pageType.GetField("Create",Flags).SetValue(page,new Func<Form>(()=>general));
            pageType.GetField("Allowed",Flags).SetValue(page,new Func<bool>(()=>true)); pageType.GetField("SaveButtonName",Flags).SetValue(page,"btnApply");
            var pages=Array.CreateInstance(pageType,1); pages.SetValue(page,0);
            using(var host=(Form)Activator.CreateInstance(center,Flags,null,new object[]{pages,"General"},null))
            {
                host.Show(); Pump();
                var cards=All(general).OfType<TableLayoutPanel>().Single(c=>c.Name=="GeneralFeatureCards");
                var scroll=All(general).OfType<Panel>().Single(c=>c.Name=="GeneralFeatureScroll");
                Check(cards.Controls.Count==4,"Incorrect card count");
                var fixedCheck=All(general).OfType<CheckBox>().Single(c=>c.Name=="DisplayAlwaysEnabled");
                Check(fixedCheck.Checked&&!fixedCheck.Enabled,"Display feature is not fixed on");
                Check(All(general).OfType<Button>().Where(b=>b.Name.StartsWith("Configure")).All(b=>b.Enabled),"Inactive feature settings must remain accessible");
                foreach(string code in new[]{"en","zh-CN","en"})
                {
                    language.GetMethod("SetLanguage",Flags).Invoke(null,new object[]{code}); Pump();
                    Check(All(cards).OfType<Label>().Any(l=>l.Text==(code=="en"?"Always enabled":"默认启用")),"Fixed checkbox label not localized");
                    Capture(host,Path.Combine(folder,"GeneralCards-"+code+".png"));
                    foreach(string key in new[]{"ImageStorage","FtpUpload","DisplayApplication","ProjectBackup"})
                    using(var dialog=(Form)Call(general,"CreateFeatureDialog",key))
                    {
                        dialog.Show(host); Pump();
                        string[] titles=code=="en"?new[]{"Image storage settings","FTP upload settings","Display and application settings","Project backup settings"}:new[]{"图像存储设置","FTP 上传设置","显示与程序设置","自动备份设置"};
                        Check(titles.Contains(dialog.Text),"Dialog title not localized: "+dialog.Text);
                        var buttons=All(dialog).OfType<Button>().Where(b=>b.Name=="ConfirmFeatureDraft"||b.Name=="CancelFeatureDraft").ToArray();
                        Check(buttons.Length==2&&buttons[0].Height==buttons[1].Height,"Dialog action sizes differ");
                        if(key=="FtpUpload") Check(All(dialog).OfType<TextBox>().Any(b=>b.UseSystemPasswordChar),"Password not masked");
                        Capture(dialog,Path.Combine(folder,key+"-"+code+".png")); dialog.Close();
                    }
                }
                var source=(TextBox)Field(general,"txtRoot"); string previous=source.Text;
                var settings=app.GetType("Aron_V2.ImageRecord").GetField("Current").GetValue(null);
                string runtimeRoot=(string)settings.GetType().GetField("Root").GetValue(settings);
                foreach(bool accept in new[]{false,true})
                using(var timer=new Timer { Interval=30 })
                {
                    timer.Tick+=(s,e)=> {
                        var modal=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="GeneralFeatureDialog"); if(modal==null) return;
                        timer.Stop(); ((Control)Call(modal,"CloneFor",source)).Text="D:\\DraftOnly";
                        modal.DialogResult=accept?DialogResult.OK:DialogResult.Cancel; modal.Close();
                    };
                    timer.Start(); Call(general,"OpenFeatureSettings","ImageStorage");
                    Check(source.Text==(accept?"D:\\DraftOnly":previous),"Cancel/OK draft semantics failed");
                    Check((string)settings.GetType().GetField("Root").GetValue(settings)==runtimeRoot,"Dialog changed active runtime settings before main Save");
                }
                Check((bool)Field(host,"_dirty"),"Confirmed dialog edits did not mark page dirty");
                // Simulate future options: a scrollbar must appear, with no horizontal overflow.
                for(int i=0;i<12;i++) { int row=cards.RowCount++; cards.RowStyles.Add(new RowStyle(SizeType.Absolute,100)); cards.Controls.Add(new Panel { Dock=DockStyle.Fill },0,row); }
                Pump(); Check(scroll.VerticalScroll.Visible,"Future feature list does not scroll vertically");
                Check(!scroll.HorizontalScroll.Visible,"Feature list unexpectedly scrolls horizontally");
                Call(host,"MarkSaved"); host.Close();
            }
        }
        app.GetType("Aron_V2.OperationAudit").GetMethod("Shutdown",Flags).Invoke(null,null);
        Console.WriteLine("PASS four cards, fixed/default checkbox, all gears, bilingual dialogs, masked password, Cancel/OK staging, runtime isolation, dirty state and future vertical scrolling.");
        Console.WriteLine("Screenshots: "+folder);
    }
}

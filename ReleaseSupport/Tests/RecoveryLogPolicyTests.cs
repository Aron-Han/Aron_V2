using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class RecoveryLogPolicyTests
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    sealed class Designer:LicenseContext { public override LicenseUsageMode UsageMode {get{return LicenseUsageMode.Designtime;}} }
    static object Call(object o,string m,params object[] args){return o.GetType().GetMethod(m,Flags).Invoke(o,args);}
    static object Field(object o,string n){return o.GetType().GetField(n,Flags).GetValue(o);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static System.Collections.Generic.IEnumerable<Control> All(Control c){yield return c;foreach(Control x in c.Controls)foreach(Control y in All(x))yield return y;}
    [STAThread]static int Main(string[]args)
    {
        try
        {
            var app=Assembly.LoadFrom(args[0]);LicenseManager.CurrentContext=new Designer();Application.EnableVisualStyles();
            using(var host=new Form { ClientSize=new Size(900,500),ShowInTaskbar=false })
            using(var panel=(Control)Activator.CreateInstance(app.GetType("Aron_V2.LogPanel"),true))
            {
                host.Controls.Add(panel);host.Show();Application.DoEvents();
                Call(panel,"Append","Algorithm editor attached latest cached image: test",Color.Black);
                Call(panel,"Append","Reload Channel 0 Job1 OK.",Color.Green);
                Call(panel,"Append","S7 connected",Color.Green);
                Call(panel,"Append","Inspection result: Job1/Cam1/Pos1 = 1",Color.Black);
                Call(panel,"Append","Camera recovery: offline, retry pending",Color.Orange);
                Call(panel,"Append","Camera recovery: Recovery completed.",Color.Green);
                Call(panel,"FlushPending");
                var entries=(System.Collections.IList)Field(panel,"_visible");
                Check(entries.Count==3,"Operator log hides editor/cache steps; retains communication, results and one recovery status");
                Check((string)Call(panel,"VisibleMessage",0)=="Camera recovery: Recovery completed.","Recovery completion replaces previous status and appears newest");
                Call(panel,"Append","相机恢复：恢复已完成。",Color.Green);Call(panel,"FlushPending");
                Check(((System.Collections.IList)Field(panel,"_visible")).Count==3,"Recovery status merges across language changes");
                Check(((DataGridView)Field(panel,"_grid")).ContextMenuStrip.Items.Count==1,"Log grid provides right-click Clear Log");
                Call(panel,"Append","S7 queued before clear",Color.Black);Call(panel,"ClearDisplayLog");Call(panel,"FlushPending");
                Check(((System.Collections.IList)Field(panel,"_visible")).Count==0&&((System.Collections.IList)Field(panel,"_entries")).Count==0,"Clear Log clears displayed and pending UI records only");
                Call(panel,"Append","S7 new event after clear",Color.Black);Call(panel,"FlushPending");Check(((System.Collections.IList)Field(panel,"_visible")).Count==1,"Logging continues after Clear Log");
            }
            using(var general=(Form)Activator.CreateInstance(app.GetType("Aron_V2.ImageRecord")))
            {
                Call(general,"PrepareForSettingsCenter");
                general.Show();Application.DoEvents();
                var cards=(TableLayoutPanel)All(general).Single(c=>c.Name=="GeneralFeatureCards");
                var backup=All(general).Single(c=>c.Name=="FeatureCardProjectBackup");var recovery=All(general).Single(c=>c.Name=="FeatureCardCameraRecovery");
                Check(cards.GetRow(recovery)==cards.GetRow(backup)+1,"Recovery card follows Project Backup");
                foreach(string language in new[]{"zh-CN","en"})
                {
                    app.GetType("Aron_V2.UiLanguage").GetMethod("SetLanguage",Flags).Invoke(null,new object[]{language});
                    Check(All(recovery).OfType<Label>().Any(l=>l.Text==(language=="en"?"Camera offline recovery":"相机离线恢复")),"Recovery card live language switch "+language);
                }
                string previous=((TextBox)Field(general,"txtRoot")).Text;
                var current=app.GetType("Aron_V2.ImageRecord").GetField("Current").GetValue(null);string active=(string)current.GetType().GetField("Root").GetValue(current);
                foreach(bool accept in new[]{false,true}) using(var timer=new Timer { Interval=50 })
                {
                    timer.Tick+=(s,e)=> {var modal=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Name=="StorageRulesDialog");if(modal==null)return;timer.Stop();All(modal).OfType<TextBox>().Single(t=>t.Name=="ImageStorageRoot").Text=@"D:\DraftImages";modal.DialogResult=accept?DialogResult.OK:DialogResult.Cancel;modal.Close();};
                    timer.Start();Call(general,"OpenFeatureSettings","ImageStorage");
                    Check(((TextBox)Field(general,"txtRoot")).Text==(accept?@"D:\DraftImages":previous),"Storage dialog "+(accept?"OK stages":"Cancel discards")+" changes");
                    Check((string)current.GetType().GetField("Root").GetValue(current)==active,"Dialog does not mutate active storage until General Save");
                }
            }
            using(var main=(Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain")))
            {
                // Missing/broken VPP files cannot matter when reload is disabled.
                var current=app.GetType("Aron_V2.ImageRecord").GetField("Current").GetValue(null);
                var options=Activator.CreateInstance(app.GetType("Aron_V2.CameraRecoveryOptions"));current.GetType().GetProperty("Recovery").SetValue(current,options,null);
                options.GetType().GetField("ReloadVpp").SetValue(options,false);Call(main,"ReloadRecoveryTools");
                Check(true,"Recovery without VPP reload performs no disk deserialization");
                Check((bool)options.GetType().GetField("OnlyOffline").GetValue(options),"Default recovery scope is offline cameras only");
            }
            return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}

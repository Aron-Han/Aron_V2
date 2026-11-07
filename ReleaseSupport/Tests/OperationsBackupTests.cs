using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
class OperationsBackupTests
{
    static BindingFlags All=BindingFlags.Static|BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static object Call(Type type,string name,params object[] args) { return type.GetMethod(name,All).Invoke(null,args); }
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch(TargetInvocationException ex) { if(ex.InnerException is ArgumentException) return; throw; } throw new Exception("Expected validation rejection"); }
    [STAThread] static int Main(string[] args)
    {
        try { return Run(args); }
        catch(Exception ex) { try { Console.WriteLine("FAIL "+ex.GetType().FullName+" : "+ex.Message); if(ex.InnerException!=null) Console.WriteLine("INNER "+ex.InnerException.GetType().FullName+" : "+ex.InnerException.Message); } catch { Console.WriteLine("FAIL exception formatting"); } return 1; }
    }
    static int Run(string[] args)
    {
        Application.EnableVisualStyles();
        var app=Assembly.LoadFrom(args[0]);
        var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestData",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var audit=app.GetType("Aron_V2.OperationAudit");
        Call(audit,"Initialize",Path.Combine(root,"Operations")); Call(audit,"SetUser","Tester");
        var observerType=app.GetType("Aron_V2.OperationAuditUi"); var observer=Activator.CreateInstance(observerType,true);
        using(var form=new Form { Name="AuditFixture",Text="Audit fixture" })
        {
            var button=new Button { Name="saveFixture",Text="Save" }; form.Controls.Add(button);
            var password=new TextBox { Name="password",UseSystemPasswordChar=true,Top=40 }; form.Controls.Add(password);
            observerType.GetMethod("Attach",All).Invoke(observer,new object[]{form});
            typeof(Button).GetMethod("OnClick",All).Invoke(button,new object[]{EventArgs.Empty});
            typeof(Control).GetMethod("OnEnter",All).Invoke(password,new object[]{EventArgs.Empty});
            password.Text="Secret-Do-Not-Log";
            typeof(Control).GetMethod("OnLeave",All).Invoke(password,new object[]{EventArgs.Empty});
        }
        Parallel.For(0,40,i=>Call(audit,"Record","Fixture","Table edited","Parameter limit="+i));
        Call(audit,"Shutdown");
        string[] lines=File.ReadAllLines(Directory.GetFiles(Path.Combine(root,"Operations"),"*.jsonl").Single());
        Check(lines.Length>=43,"Missing operation records"); Check(!string.Join("\n",lines).Contains("Secret-Do-Not-Log"),"Password leaked");
        Check(lines.Any(l=>l.Contains("Action requested")&&l.Contains("saveFixture")),"Button activation not recorded");
        var found=(System.Collections.ICollection)Call(audit,"Read",DateTime.Today,DateTime.Today.AddDays(1),"Parameter limit=",5000);
        Check(found.Count==40,"Persisted query/filter failed");
        Console.WriteLine("PASS durable concurrent audit, querying and password exclusion.");
        var settingsType=app.GetType("Aron_V2.ProjectBackupSettings"); var settings=Activator.CreateInstance(settingsType,true);
        string project=Path.Combine(root,"Project"), destination=Path.Combine(root,"Backups");
        Directory.CreateDirectory(Path.Combine(project,"Config")); Directory.CreateDirectory(Path.Combine(project,"VPP"));
        File.WriteAllText(Path.Combine(project,"Config","test.xml"),"<settings>1</settings>");
        File.WriteAllText(Path.Combine(project,"VPP","Inspection.vpp"),"fixture-no-camera");
        settingsType.GetField("Directory").SetValue(settings,destination); settingsType.GetField("RetentionDays").SetValue(settings,7);
        var service=app.GetType("Aron_V2.ProjectBackupService"); DateTime now=DateTime.UtcNow;
        string old=(string)Call(service,"CreateBackup",project,settings,now.AddDays(-20));
        string latest=(string)Call(service,"CreateBackup",project,settings,now);
        using(var zip=ZipFile.OpenRead(latest))
        {
            Check(zip.GetEntry("Project/Config/test.xml")!=null && zip.GetEntry("Project/VPP/Inspection.vpp")!=null,"Incomplete Project archive");
        }
        string other=Path.Combine(destination,"other-project.zip"); File.WriteAllText(other,"keep");
        Call(service,"Cleanup",project,settings,now,latest);
        Check(!File.Exists(old)&&File.Exists(latest)&&File.Exists(other),"Unsafe retention cleanup");
        settingsType.GetField("Directory").SetValue(settings,Path.Combine(project,"BadBackup")); Reject(()=>Call(service,"CreateBackup",project,settings,now));
        settingsType.GetField("Directory").SetValue(settings,destination); settingsType.GetField("IntervalDays").SetValue(settings,0);
        Reject(()=>Call(settingsType,"Validate",settings,project));
        Check(!(bool)Call(service,"IsDue",now.AddHours(-23),now,1)&&(bool)Call(service,"IsDue",now.AddDays(-1),now,1),"Schedule interval failed");
        Console.WriteLine("PASS Project ZIP contents, scoped retention, recursive path rejection, integer-day range and due-time boundary.");
        using(var viewer=(Form)Activator.CreateInstance(app.GetType("Aron_V2.OperationLogForm"),true))
        {
            viewer.Show(); Application.DoEvents();
            using(var bitmap=new Bitmap(viewer.Width,viewer.Height)) { viewer.DrawToBitmap(bitmap,new Rectangle(Point.Empty,viewer.Size)); bitmap.Save(Path.Combine(root,"OperationLog.png")); }
            viewer.Close();
        }
        var general=(Form)Activator.CreateInstance(app.GetType("Aron_V2.ImageRecord"));
        using(general)
        {
            general.GetType().GetMethod("PrepareForSettingsCenter",All).Invoke(general,null);
            var numbers=Descendants(general).OfType<NumericUpDown>().Where(n=>n.Name.StartsWith("Backup")).ToArray();
            Check(numbers.Length==2&&numbers.All(n=>n.DecimalPlaces==0&&n.Minimum==1),"Backup day fields not integer controls");
            var center=app.GetType("Aron_V2.SettingsCenterForm"); var pageType=center.GetNestedType("Page",All);
            var page=Activator.CreateInstance(pageType,true);
            pageType.GetField("Title",All).SetValue(page,"General");
            pageType.GetField("Create",All).SetValue(page,new Func<Form>(()=>general));
            pageType.GetField("Allowed",All).SetValue(page,new Func<bool>(()=>true));
            pageType.GetField("SaveButtonName",All).SetValue(page,"btnApply");
            var pages=Array.CreateInstance(pageType,1); pages.SetValue(page,0);
            using(var host=(Form)Activator.CreateInstance(center,All,null,new object[]{pages,"General"},null))
            {
                host.Size=new Size(1200,1000); host.Show(); Application.DoEvents();
                using(var bitmap=new Bitmap(host.Width,host.Height)) { host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,host.Size)); bitmap.Save(Path.Combine(root,"GeneralBackup.png")); }
                var language=app.GetType("Aron_V2.UiLanguage"); Call(language,"SetLanguage","zh-CN");
                Application.DoEvents();
                using(var bitmap=new Bitmap(host.Width,host.Height)) { host.DrawToBitmap(bitmap,new Rectangle(Point.Empty,host.Size)); bitmap.Save(Path.Combine(root,"GeneralBackup-Chinese.png")); }
                Call(language,"SetLanguage","en");
                host.Close();
            }
        }
        Console.WriteLine("PASS operation viewer and General backup controls render without camera/PLC startup. Artifacts: "+root);
        return 0;
    }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent) { foreach(Control child in parent.Controls) { yield return child; foreach(var descendant in Descendants(child)) yield return descendant; } }
}

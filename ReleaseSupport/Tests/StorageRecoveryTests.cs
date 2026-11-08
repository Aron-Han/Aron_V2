using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ImageProcessing;
using Cognex.VisionPro.ToolBlock;
using Cognex.VisionPro3D;
class StorageRecoveryTests
{
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
    const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Assembly App;
    static System.Collections.Generic.IEnumerable<Control> All(Control c) {foreach(Control child in c.Controls){yield return child;foreach(var next in All(child))yield return next;}}
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message); }
    [STAThread] static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=> {string p=Path.Combine(Path.GetDirectoryName(args[0]),new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        try { App=Assembly.LoadFrom(args[0]);Run(args[1]);return 0; }catch(Exception ex) {Console.WriteLine(ex);return 1;}
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void Run(string root)
    {
        Directory.CreateDirectory(root);object[] report={null};Check((bool)App.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start",Static).Invoke(null,report),"Pinned VisionPro runtime");
        App.GetType("Aron_V2.Load_Job").GetMethod("EnsureCognexInteropLoaded").Invoke(null,null);
        Type optionsType=App.GetType("Aron_V2.ImageStorageOptions");dynamic options=Activator.CreateInstance(optionsType);
        var method=optionsType.GetMethod("BuildPath",Instance);
        options.Directories=new[]{"Year","Month","Day","Cam"};
        string dated=(string)method.Invoke(options,new object[]{root,"Job1","Cam1","Pos1",null,true,null,new DateTime(2026,11,8),false,"PNG"});
        Check(dated.Contains(@"2026\11\08\Cam1\Raw\"),"Year/month/day hierarchy does not require Job first");
        options.Directories=new[]{"Date","Cam"};options.FileFields=new[]{"Cam","ImageFileName"};
        string path=(string)method.Invoke(options,new object[]{root,"Job1","Cam1","Pos1",null,true,"../A:B",new DateTime(2026,11,8),false,"PNG"});
        Check(path.Contains(@"Cam1\Raw\")&&!Path.GetFileName(path).Contains(":"),"Raw immediately below Cam; unsafe name sanitized; time added");
        string deal=(string)method.Invoke(options,new object[]{root,"Job1","Cam1","Pos1",null,true,null,DateTime.Now,true,"PNG"});Check(deal.Contains(@"Cam1\Deal\"),"Deal branch");
        options.Directories=new string[0];Check(((string)method.Invoke(options,new object[]{root,null,"Cam2",null,null,false,null,DateTime.Now,false,"CDB"})).Contains(@"Cam2\Raw\"),"Optional hierarchy removed; Cam/Raw invariant retained");
        Type settings=App.GetType("Aron_V2.ImageRecord+Settings");var ser=new XmlSerializer(settings);dynamic legacy=ser.Deserialize(new StringReader("<Settings><FormatMode>PNG</FormatMode></Settings>"));
        Check(legacy.FormatMode=="PNG" && legacy.Storage==null && legacy.Recovery.Enabled && !legacy.Recovery.ReloadVpp,"Legacy XML defaults compatible; no mandatory reload");
        var blank=App.GetType("Aron_V2.BlankCameraVpp");var contract=App.GetType("Aron_V2.InspectionVppContract");
        foreach(bool range in new[]{false,true}) using(var tool=new CogToolBlock())
        {
            blank.GetMethod("InitializeInspectionInterface",Static).Invoke(null,new object[]{tool});blank.GetMethod("InitializeImageTools",Static).Invoke(null,new object[]{tool,range});
            Check(tool.Outputs.Contains("ImageFileName") && tool.Tools[range?1:0] is CogIPOneImageTool,"Default "+(range?"3D splitter then":"2D")+" image tool and naming pin");
            if(range) { var splitter=(CogToolBlock)tool.Tools[0];Console.WriteLine("Splitter outputs: "+string.Join(",",splitter.Outputs.Cast<CogToolBlockTerminal>().Select(t=>t.Name))); }
            var errors=(System.Collections.IList)contract.GetMethod("Validate",Static).Invoke(null,new object[]{tool,null,null});Check(errors.Count==0,"Default VPP save contract valid");
            using(var image=new CogImage8Grey(20,12))
            {
                if(!range) {tool.Inputs[0].Value=image;tool.Run();var ip=(CogIPOneImageTool)tool.Tools[0];Check(ip.OutputImage!=null,"New 2D data binding produces effect image");}
            }
            tool.Tools.Clear();errors=(System.Collections.IList)contract.GetMethod("Validate",Static).Invoke(null,new object[]{tool,null,null});Check(errors.Count>0,"Missing image tool blocks save");
        }
        dynamic camera=Activator.CreateInstance(App.GetType("Aron_V2.CameraConfig"));camera.Name="Cam4";camera.ConnectionMode="LmiSdk";camera.SdkCategory="SDK-3D";
        var ensure=blank.GetMethod("EnsureForCamera",Static);string vppRoot=Path.Combine(root,"Provisioned");ensure.Invoke(null,new object[]{vppRoot,"Job1",camera,null});
        string vpp=Path.Combine(vppRoot,"Job1","Cam4","Pos1","Inspection.vpp");
        using(var provisioned=(CogToolBlock)CogSerializer.LoadObjectFromFile(vpp)) Check(provisioned.Tools.Count==2&&provisioned.Outputs.Contains("ImageFileName"),"New SDK camera provisions a serialized splitter/image-tool VPP");
        File.WriteAllText(vpp,"retained VPP sentinel");ensure.Invoke(null,new object[]{vppRoot,"Job1",camera,null});Check(File.ReadAllText(vpp)=="retained VPP sentinel","Existing retained VPP is never overwritten during provisioning");
        using(var raw=new CogImage8Grey(20,12)) using(var file=new CogImageFile())
        {string name=Path.Combine(root,"roundtrip.png");file.Open(name,CogImageFileModeConstants.Write);file.Append(raw);file.Close();file.Open(name,CogImageFileModeConstants.Read);Check(file[0].Width==20,"Native 2D raw round trip");file.Close();}
        using(var height=new CogImage16Grey(20,12))
        using(var range=new CogImage16Range(height,0,new Cog3DTransformLinear(new Cog3DMatrix3x3(2,0,0,0,3,0,0,0,4),new Cog3DVect3(5,6,7))))
        using(var copied=(CogImage16Range)range.CopyBase(CogImageCopyModeConstants.CopyPixels))
        using(var file=new CogImageFile())
        {
            string name=Path.Combine(root,"roundtrip.cdb");file.Open(name,CogImageFileModeConstants.Write);file.Append(copied);file.Close();
            file.Open(name,CogImageFileModeConstants.Read);var loaded=file[0];Check(loaded is CogImage16Range && loaded.Width==20 && loaded.Height==12,"3D native CDB round trip retains range type and dimensions");
            var expected=((Cog3DTransformLinear)range.PixelFromRootTransform3D).MapPoint(new Cog3DVect3(1,2,3));var actual=((Cog3DTransformLinear)((CogImage16Range)loaded).PixelFromRootTransform3D).MapPoint(new Cog3DVect3(1,2,3));
            Check(Math.Abs(actual.X-expected.X)<1e-8&&Math.Abs(actual.Y-expected.Y)<1e-8&&Math.Abs(actual.Z-expected.Z)<1e-8,"CDB snapshot preserves 3D calibration transform");file.Close();
            using(var tool=new CogToolBlock())
            {
                blank.GetMethod("InitializeInspectionInterface",Static).Invoke(null,new object[]{tool});blank.GetMethod("InitializeImageTools",Static).Invoke(null,new object[]{tool,true});tool.Inputs[0].Value=range;tool.Run();
                Check(tool.RunStatus.Result==CogToolResultConstants.Accept && ((CogIPOneImageTool)tool.Tools[1]).OutputImage!=null,"Default 3D splitter/image chain runs with synthetic range");
                var current=App.GetType("Aron_V2.ImageRecord").GetField("Current").GetValue(null);
                dynamic storage=Activator.CreateInstance(optionsType);storage.SaveDeal3D=true;storage.Directories=new[]{"Cam"};
                current.GetType().GetProperty("Storage").SetValue(current,storage,null);current.GetType().GetField("Root").SetValue(current,root);
                var workers=App.GetType("IoWorkers");workers.GetMethod("Start").Invoke(null,null);
                string rawPath=(string)App.GetType("Aron_V2.InspectionImageStorage").GetMethod("Queue",Static).Invoke(null,new object[]{tool,range,"Job1","Cam9","Pos1",null,true});
                for(int attempt=0;attempt<100&&(!File.Exists(rawPath)||!Directory.Exists(Path.Combine(root,"Cam9","Deal"))||Directory.GetFiles(Path.Combine(root,"Cam9","Deal")).Length==0);attempt++)Thread.Sleep(50);
                Check(File.Exists(rawPath),"Async 3D Raw CDB saved");
                Check(Directory.GetFiles(Path.Combine(root,"Cam9","Deal"),"*.png").Length>0,"Async 3D Deal PNG saved from exact tool output");
                workers.GetMethod("Stop").Invoke(null,null);
                tool.Outputs.Remove("ImageFileName");var errors=(System.Collections.IList)contract.GetMethod("Validate",Static).Invoke(null,new object[]{tool,null,null});Check(errors.Cast<string>().Any(e=>e.Contains("ImageFileName")),"Deleting naming pin blocks save");
            }
        }
        var currentSettings=App.GetType("Aron_V2.ImageRecord").GetField("Current").GetValue(null);currentSettings.GetType().GetField("Root").SetValue(currentSettings,root);currentSettings.GetType().GetField("RetentionMinutes").SetValue(currentSettings,1);
        string expired=Path.Combine(root,"Cam8","Raw","expired.cdb"),unrelated=Path.Combine(root,"unrelated.cdb");Directory.CreateDirectory(Path.GetDirectoryName(expired));File.WriteAllText(expired,"old frame");File.WriteAllText(unrelated,"unrelated");File.SetLastWriteTime(expired,DateTime.Now.AddMinutes(-10));File.SetLastWriteTime(unrelated,DateTime.Now.AddMinutes(-10));
        App.GetType("Aron_V2.ImageRecord").GetMethod("CleanupOldMinutes").Invoke(null,null);Check(!File.Exists(expired)&&File.Exists(unrelated),"Retention handles CDB under Raw while preserving unrelated files outside managed image hierarchy");
        dynamic disabled=Activator.CreateInstance(optionsType);disabled.RetentionEnabled=false;disabled.DiskCleanupEnabled=false;
        currentSettings.GetType().GetProperty("Storage").SetValue(currentSettings,disabled,null);
        File.WriteAllText(expired,"keep");File.SetLastWriteTime(expired,DateTime.Now.AddMinutes(-10));
        App.GetType("Aron_V2.ImageRecord").GetMethod("CleanupOldMinutes").Invoke(null,null);
        Check(File.Exists(expired),"Disabling both cleanup options preserves expired images");
        // Inspect UI on both languages, never construct FormMain or initialize transport.
        var lang=App.GetType("Aron_V2.UiLanguage");
        foreach(string language in new[]{"en","zh-CN"})
        foreach(float scale in new[]{1F,1.5F,2F})
        {
            lang.GetMethod("SetLanguage",Static).Invoke(null,new object[]{language});
            using(var dialog=(Form)Activator.CreateInstance(App.GetType("Aron_V2.StorageRulesDialog"),Instance,null,new object[]{Activator.CreateInstance(optionsType),@"C:\image",10080},null))
            {dialog.Show();Application.DoEvents();
                if(scale!=1F){dialog.Scale(new SizeF(scale,scale));dialog.Font=new Font("Segoe UI",9F*scale);dialog.MinimumSize=new Size(800,600);dialog.ClientSize=new Size(1040,760);Application.DoEvents();}
                foreach(var button in All(dialog).OfType<Button>().Where(b=>b.Visible))
                    Check(button.Parent.ClientRectangle.Contains(button.Bounds),"Button fits parent at "+scale+" / "+language+" / "+button.Text);
                using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(root,"Storage-"+language+"-"+scale+".png"));}Check(dialog.Controls.Count>0,"Storage rules UI "+language);
                All(dialog).OfType<Button>().First(b=>b.Name=="StorageDirectoriesNav").PerformClick();Application.DoEvents();
                var ruleRows=All(dialog).OfType<ListView>().ToArray();
                Check(ruleRows.Length==2&&ruleRows.All(c=>c.Visible),"Directory navigation shows both rule cards "+language);
                Check(ruleRows[0].RectangleToScreen(ruleRows[0].ClientRectangle).Right<=ruleRows[1].RectangleToScreen(ruleRows[1].ClientRectangle).Left,"B cards remain side by side at "+scale);
                if(language=="en"&&scale==1F)using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(root,"Storage-B-preview.png"));}
                if(language=="en"&&scale==1F)
                {
                    var rows=ruleRows.First(c=>c.Name=="FolderRuleRows");
                    var click=typeof(Control).GetMethod("OnMouseClick",Instance);
                    int x=rows.Columns[0].Width+rows.Columns[1].Width+rows.Columns[2].Width/2;
                    click.Invoke(rows,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,rows.Items[0].Bounds.Top+5,0)});
                    dynamic draft=dialog.GetType().GetField("Options",Instance).GetValue(dialog);
                    Check(draft.Directories[0]=="Cam","Per-row down action updates directory order and preview");
                    x=rows.Columns[0].Width+rows.Columns[1].Width+rows.Columns[2].Width+rows.Columns[3].Width/2;
                    click.Invoke(rows,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,rows.Items[0].Bounds.Top+5,0)});
                    Check(draft.Directories.Length==4,"Per-row delete removes only the chosen field");
                    var picker=All(rows.Parent).OfType<ComboBox>().First();picker.SelectedIndex=2;
                    All(rows.Parent).OfType<Button>().First(b=>b.AccessibleName=="Add field").PerformClick();
                    Check(draft.Directories.Length==5&&draft.Directories[4]=="Year","Add selector inserts Year into the draft");
                }
                Check(!All(dialog).OfType<NumericUpDown>().Any(c=>c.Visible),"General controls are hidden on directory page "+language);
                foreach(var button in All(dialog).OfType<Button>().Where(b=>b.Visible))
                    Check(button.Parent.ClientRectangle.Contains(button.Bounds),"Directory button fits parent at "+scale+" / "+language+" / "+button.Text);
                var preview=All(dialog).OfType<TextBox>().First(c=>c.Multiline);
                using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(root,"Directories-top-"+language+"-"+scale+".png"));}
                var scroll=All(dialog).OfType<Panel>().First(c=>c.AutoScroll);scroll.AutoScrollPosition=new Point(0,scroll.VerticalScroll.Maximum);Application.DoEvents();
                using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(root,"Directories-"+language+"-"+scale+".png"));}
                Check(preview.Parent.ClientRectangle.Contains(preview.Bounds),"Entire preview border fits its own viewport at "+scale);
                Check(preview.Parent.Parent.ClientRectangle.Contains(preview.Parent.Bounds),"Preview viewport fits above footer at "+scale);
                Check(scroll.RectangleToScreen(scroll.ClientRectangle).Bottom<=preview.Parent.RectangleToScreen(preview.Parent.ClientRectangle).Top,"Scroll area cannot overlap fixed preview at "+scale);
                dialog.Close();}
        }
    }
}

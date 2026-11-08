using Cognex.VisionPro;
using Cognex.VisionPro.ImageProcessing;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Aron_V2
{
    public sealed class ImageStorageOptions
    {
        public string Format2D = "BMP", Format3D = "CDB", DealFormat = "PNG";
        public bool SaveDeal2D = false, SaveDeal3D = false;
        public bool RetentionEnabled = true, DiskCleanupEnabled = false;
        public int DiskUsagePercent = 90;
        public string[] Directories = { "Job", "Cam", "Pos", "Date", "Result" };
        public string[] FileFields = { "Cam", "Pos", "Time", "PN", "Result" };
        public static readonly string[] Fields = { "Date", "Year", "Month", "Day", "Time", "PN", "Pos", "Job", "Result", "Cam", "ImageFileName" };
        public ImageStorageOptions Copy() => new ImageStorageOptions { Format2D=Format2D,Format3D=Format3D,DealFormat=DealFormat,
            SaveDeal2D=SaveDeal2D,SaveDeal3D=SaveDeal3D,RetentionEnabled=RetentionEnabled,DiskCleanupEnabled=DiskCleanupEnabled,DiskUsagePercent=DiskUsagePercent,Directories=(string[])Directories.Clone(),FileFields=(string[])FileFields.Clone() };
        public void Validate()
        {
            if(DiskUsagePercent<1 || DiskUsagePercent>99) throw new ArgumentException("Disk usage threshold must be 1–99%.");
            if (!new[]{"BMP","PNG"}.Contains(Format2D) || Format3D!="CDB" || !new[]{"BMP","PNG"}.Contains(DealFormat))
                throw new ArgumentException(UiLanguage.Text("Invalid image format."));
            if (Directories==null || FileFields==null || FileFields.Length==0 || Directories.Concat(FileFields).Any(f=>!Fields.Contains(f)) ||
                Directories.Distinct().Count()!=Directories.Length || FileFields.Distinct().Count()!=FileFields.Length)
                throw new ArgumentException(UiLanguage.Text("Invalid image naming fields."));
        }
        internal string BuildPath(string root,string job,string cam,string pos,string pn,bool ok,string custom,DateTime time,bool deal,string format)
        {
            Validate();
            var values=new Dictionary<string,string> { ["Job"]=job,["Cam"]=cam,["Pos"]=pos,["PN"]=pn,["Result"]=ok?"OK":"NG",
                ["ImageFileName"]=custom,["Date"]=time.ToString("yyyyMMdd",CultureInfo.InvariantCulture),["Year"]=time.ToString("yyyy",CultureInfo.InvariantCulture),["Month"]=time.ToString("MM",CultureInfo.InvariantCulture),["Day"]=time.ToString("dd",CultureInfo.InvariantCulture),["Time"]=time.ToString("HHmmss_fff",CultureInfo.InvariantCulture) };
            string path=Path.GetFullPath(root);
            bool camera=false;
            foreach(string field in Directories)
            {
                var value=Safe(values[field]); if(value.Length==0) continue;
                path=Path.Combine(path,value);
                if(field=="Cam") { path=Path.Combine(path,deal?"Deal":"Raw"); camera=true; }
            }
            // Cam/Raw|Deal is structural and cannot be removed by naming customization.
            if(!camera) path=Path.Combine(path,Safe(cam).Length==0?"Camera":Safe(cam),deal?"Deal":"Raw");
            string name=string.Join("_",FileFields.Select(f=>Safe(values[f])).Where(v=>v.Length>0));
            if(name.Length==0) name="Image";
            // Millisecond timestamp prevents a missing optional PN/output from overwriting frames.
            if(!FileFields.Contains("Time")) name+="_"+time.ToString("yyyyMMdd_HHmmss_fff");
            if(name.Length>160) name=name.Substring(0,120)+"_"+Guid.NewGuid().ToString("N");
            return Path.Combine(path,name+"."+format.ToLowerInvariant());
        }
        internal static string Safe(string value)
        {
            string text=new string((value??"").Select(c=>Path.GetInvalidFileNameChars().Contains(c)||char.IsControl(c)?'_':c).ToArray()).Trim().TrimEnd('.',' ');
            if(text=="." || text=="..") return "";
            if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(text.Split('.')[0].ToUpperInvariant())) text="_"+text;
            return text.Length>80?text.Substring(0,80):text;
        }
    }

    internal static class InspectionImageStorage
    {
        internal static CogIPOneImageTool DealTool(CogToolBlock tool) => tool.Tools.Cast<ICogTool>().OfType<CogIPOneImageTool>().FirstOrDefault();
        // Called under the existing inspection lock: snapshot before another run can change outputs.
        internal static string Queue(CogToolBlock tool,ICogImage raw,string job,string cam,string pos,string pn,bool ok)
        {
            var settings=ImageRecord.Current;
            var options=settings.Storage ?? new ImageStorageOptions { Format2D=settings.FormatMode??"BMP" };
            bool range=raw is CogImage16Range;
            string custom=tool.Outputs.Contains("ImageFileName")?Convert.ToString(tool.Outputs["ImageFileName"].Value,CultureInfo.InvariantCulture):null;
            var time=DateTime.Now;
            string root=string.IsNullOrWhiteSpace(settings.Root)?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Images"):settings.Root;
            string path=options.BuildPath(root,job,cam,pos,pn,ok,custom,time,false,range?options.Format3D:options.Format2D);
            path=Enqueue(raw,path,job,cam,pos,pn,ok,false);
            if(range?options.SaveDeal3D:options.SaveDeal2D)
            {
                var effect=DealTool(tool);
                if(effect==null || effect.OutputImage==null || effect.RunStatus.Result!=CogToolResultConstants.Accept)
                    throw new InvalidOperationException(UiLanguage.Text("Effect image unavailable: check CogIPOneImageTool.OutputImage."));
                Enqueue(effect.OutputImage,options.BuildPath(root,job,cam,pos,pn,ok,custom,time,true,options.DealFormat),job,cam,pos,pn,ok,true);
            }
            return path;
        }
        private static string Enqueue(ICogImage image,string path,string job,string cam,string pos,string pn,bool ok,bool deal)
        {
            ICogImage copy=image.CopyBase(CogImageCopyModeConstants.CopyPixels);
            try { var task=new IoWorkers.SaveImageTask { NativeImage=copy,FullPath=path,Ext=Path.GetExtension(path),JobId=job,CamName=cam,PosName=pos,Serial=pn,OkFlag=ok,ImageKind=deal?"Deal":"Raw" };IoWorkers.EnqueueImage(task);return task.FullPath; }
            catch { (copy as IDisposable)?.Dispose(); throw; }
        }
    }
}

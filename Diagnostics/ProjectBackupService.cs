using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Aron_V2.UI_Update;
using System.Drawing;

namespace Aron_V2
{
    internal sealed class ProjectBackupSettings
    {
        public bool Enabled;
        public int IntervalDays = 7, RetentionDays = 30;
        public string Directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        internal static string ProjectDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Project");
        internal static ProjectBackupSettings Load()
        {
            string path=ProjectPaths.Config("General/ProjectBackup.xml");
            if(!File.Exists(path)) return new ProjectBackupSettings();
            var root=XDocument.Load(path).Root;
            var result=new ProjectBackupSettings { Enabled=(bool?)root.Element("Enabled")??false,
                IntervalDays=(int?)root.Element("IntervalDays")??7, RetentionDays=(int?)root.Element("RetentionDays")??30,
                Directory=(string)root.Element("Directory")??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Backups") };
            Validate(result, ProjectDirectory); return result;
        }
        internal static void Validate(ProjectBackupSettings settings, string project)
        {
            if(settings.IntervalDays<1 || settings.IntervalDays>36500 || settings.RetentionDays<1 || settings.RetentionDays>36500)
                throw new ArgumentException(UiLanguage.Current=="zh-CN" ? "备份间隔和保留天数必须为 1～36500 的整数。" : "Backup interval and retention must be whole days from 1 to 36500.");
            if(string.IsNullOrWhiteSpace(settings.Directory) || !Path.IsPathRooted(settings.Directory))
                throw new ArgumentException(UiLanguage.Current=="zh-CN" ? "请选择备份文件夹的完整路径。" : "Select an absolute backup directory.");
            string destination=Path.GetFullPath(settings.Directory).TrimEnd('\\','/');
            string source=Path.GetFullPath(project).TrimEnd('\\','/');
            if(string.Equals(destination,source,StringComparison.OrdinalIgnoreCase) || destination.StartsWith(source+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(UiLanguage.Current=="zh-CN" ? "备份路径不能位于 Project 文件夹内。" : "Backup directory must be outside Project.");
            // Reject junctions/symlinks that could put an apparently external destination inside Project.
            for(var parent=new DirectoryInfo(destination); parent!=null; parent=parent.Parent)
                if(parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint)!=0) throw new ArgumentException("Backup path cannot contain directory links.");
        }
        internal static void Save(ProjectBackupSettings settings)
        {
            Validate(settings,ProjectDirectory);
            var document=new XDocument(new XElement("ProjectBackup",new XElement("Enabled",settings.Enabled),new XElement("IntervalDays",settings.IntervalDays),
                new XElement("RetentionDays",settings.RetentionDays),new XElement("Directory",Path.GetFullPath(settings.Directory))));
            AtomicSave(document,ProjectPaths.Config("General/ProjectBackup.xml"));
        }
        internal static void AtomicSave(XDocument document,string path)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { document.Save(temp); if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
    }
    internal sealed class ProjectBackupService : IDisposable
    {
        private readonly Timer _timer;
        private int _busy;
        private volatile bool _stopping;
        private DateTime _lastAttempt=DateTime.MinValue;
        internal ProjectBackupService() { _timer=new Timer(Check,null,TimeSpan.FromSeconds(30),TimeSpan.FromMinutes(1)); }
        private void Check(object state)
        {
            if(_stopping || Interlocked.Exchange(ref _busy,1)!=0) return;
            try
            {
                var settings=ProjectBackupSettings.Load(); if(!settings.Enabled) return;
                DateTime now=DateTime.UtcNow;
                if(now-_lastAttempt<TimeSpan.FromMinutes(5)) return;
                string stateFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data","Backups","ProjectBackupState.xml");
                DateTime last=DateTime.MinValue;
                if(File.Exists(stateFile))
                {
                    var saved=XDocument.Load(stateFile).Root;
                    if(string.Equals((string)saved.Element("Directory"),Path.GetFullPath(settings.Directory),StringComparison.OrdinalIgnoreCase))
                        DateTime.TryParse((string)saved.Element("LastSuccessUtc"),null,System.Globalization.DateTimeStyles.RoundtripKind,out last);
                }
                if(!IsDue(last,now,settings.IntervalDays)) return;
                _lastAttempt=now;
                string path=CreateBackup(ProjectBackupSettings.ProjectDirectory,settings,now);
                ProjectBackupSettings.AtomicSave(new XDocument(new XElement("ProjectBackupState",new XElement("Directory",Path.GetFullPath(settings.Directory)),
                    new XElement("LastSuccessUtc",now.ToString("o")),new XElement("LastArchive",path))),stateFile);
                LogChangeEventArgs.Set("Log","Project backup completed: "+path,Color.Green);
                OperationAudit.Record("Project backup","Automatic backup succeeded",path);
                try { Cleanup(ProjectBackupSettings.ProjectDirectory,settings,now,path); }
                catch(Exception ex) { DiagnosticLogStore.Exception("Backup retention cleanup",ex); LogChangeEventArgs.Set("Log","Project backup succeeded; retention cleanup failed: "+ex.Message,Color.Orange); }
            }
            catch(Exception ex)
            {
                _lastAttempt=DateTime.UtcNow;
                DiagnosticLogStore.Exception("Project auto backup",ex);
                LogChangeEventArgs.Set("Log","Project backup failed: "+ex.Message,Color.Orange);
                OperationAudit.Record("Project backup","Automatic backup failed",ex.GetType().Name);
            }
            finally { Interlocked.Exchange(ref _busy,0); }
        }
        internal static bool IsDue(DateTime last,DateTime now,int days) { return last==DateTime.MinValue || last>now || now-last>=TimeSpan.FromDays(days); }
        private static string Prefix(string project)
        {
            using(var hash=SHA256.Create()) return "AronProjectBackup_"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(project).TrimEnd('\\').ToUpperInvariant()))).Replace("-","").Substring(0,16)+"_";
        }
        private static List<FileInfo> Files(string path)
        {
            var directory=new DirectoryInfo(path);
            if((directory.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Project directory links are not supported: "+path);
            var files=directory.GetFiles().OrderBy(f=>f.Name).ToList();
            if(files.Any(f=>(f.Attributes&FileAttributes.ReparsePoint)!=0)) throw new IOException("Project file links are not supported.");
            foreach(var child in directory.GetDirectories().OrderBy(d=>d.Name)) files.AddRange(Files(child.FullName));
            return files;
        }
        internal static string CreateBackup(string project,ProjectBackupSettings settings,DateTime utc)
        {
            ProjectBackupSettings.Validate(settings,project);
            if(!System.IO.Directory.Exists(project)) throw new DirectoryNotFoundException(project);
            System.IO.Directory.CreateDirectory(settings.Directory);
            string path=Path.Combine(settings.Directory,Prefix(project)+utc.ToString("yyyyMMddTHHmmssfffZ")+"_"+Guid.NewGuid().ToString("N")+".zip");
            string temp=path+".partial";
            try
            {
                var before=Files(project);
                var signature=before.ToDictionary(f=>f.FullName,f=>f.Length+"/"+f.LastWriteTimeUtc.Ticks,StringComparer.OrdinalIgnoreCase);
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
                {
                    foreach(var file in before)
                    {
                        string relative=file.FullName.Substring(Path.GetFullPath(project).TrimEnd('\\').Length).TrimStart('\\','/').Replace('\\','/');
                        var entry=zip.CreateEntry("Project/"+relative,CompressionLevel.Fastest);
                        // Never block an engineer saving settings/VPPs. The post-copy
                        // signature check rejects a backup if the Project changed.
                        using(var input=new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                        using(var output=entry.Open()) input.CopyTo(output);
                    }
                    var metadata=zip.CreateEntry("backup-info.txt");
                    using(var writer=new StreamWriter(metadata.Open(),Encoding.UTF8)) writer.WriteLine(BuildInfo.SoftwareVersion+" / "+BuildInfo.BuildDate+" / "+utc.ToString("o"));
                }
                var after=Files(project);
                if(after.Count!=signature.Count || after.Any(f=>!signature.ContainsKey(f.FullName) || signature[f.FullName]!=f.Length+"/"+f.LastWriteTimeUtc.Ticks))
                    throw new IOException("Project changed during backup; will retry later.");
                File.Move(temp,path); return path;
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        internal static void Cleanup(string project,ProjectBackupSettings settings,DateTime now,string keep)
        {
            string prefix=Prefix(project);
            foreach(string path in System.IO.Directory.EnumerateFiles(settings.Directory,prefix+"*.zip"))
            {
                if(string.Equals(path,keep,StringComparison.OrdinalIgnoreCase)) continue;
                string name=Path.GetFileNameWithoutExtension(path).Substring(prefix.Length);
                if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^\d{8}T\d{9}Z_[0-9a-f]{32}$")) continue;
                DateTime created;
                if(DateTime.TryParseExact(name.Substring(0,19),"yyyyMMddTHHmmssfffZ",null,
                    System.Globalization.DateTimeStyles.AssumeUniversal|System.Globalization.DateTimeStyles.AdjustToUniversal,out created) && created<now.AddDays(-settings.RetentionDays)) File.Delete(path);
            }
        }
        public void Dispose() { _stopping=true; _timer.Dispose(); }
    }
}

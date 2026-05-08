using Aron.PluginContracts;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class FormMain
    {
        private PluginRuntime _plugins;
        private readonly ToolStripMenuItem _pluginMenu = new ToolStripMenuItem("External plugins") { Name = "externalPluginsToolStripMenuItem" };
        private void InitializePluginMenu()
        {
            _pluginMenu.Visible = false;
            toolToolStripMenuItem.DropDownItems.Add(_pluginMenu);
        }
        private bool CanEditPluginSettings()
        { return settingToolStripMenuItem.Enabled && generalToolStripMenuItem.Enabled && JobControlBlockReason() == null && (_plugins == null || _plugins.IsIdle); }
        private void InitializePlugins()
        {
            try
            {
                string runtimeVersion = typeof(FormMain).Assembly.GetReferencedAssemblies().Single(a => a.Name == "Cognex.VisionPro.Core").Version.ToString();
                _plugins = new PluginRuntime(AppDomain.CurrentDomain.BaseDirectory, id => new PluginHost(this, id), PluginLog, runtimeVersion);
                foreach (var slot in _plugins.Loaded)
                {
                    try
                    {
                    foreach (var page in slot.Plugin.Pages)
                    {
                        var item = new ToolStripMenuItem(page.Title) { Name = "plugin_" + slot.Descriptor.Id + "_" + _pluginMenu.DropDownItems.Count };
                        item.Click += (s, e) =>
                        {
                            if (slot.Failed) { MessageBox.Show(this, slot.Descriptor.Error, "External plugins"); return; }
                            try
                            {
                                using (var window = page.Create()) { DesktopTheme.ApplySettings(window); UiLanguage.ApplyTo(window); window.ShowDialog(this); }
                            }
                            catch (Exception ex) { PluginLog("ERROR", "Plugin page: " + ex); MessageBox.Show(this, ex.GetBaseException().Message, "External plugins"); }
                        };
                        _pluginMenu.DropDownItems.Add(item);
                    }
                    }
                    catch (Exception ex) { slot.Fail("Page registration failed: " + ex.Message); }
                }
                _pluginMenu.Visible = _pluginMenu.DropDownItems.Count > 0;
                DesktopTheme.StylePluginMenu(_pluginMenu, menuStrip1);
                Shown += (s, e) =>
                {
                    foreach (var plugin in _plugins.Available.Where(p => p.Enabled))
                        PluginLog(plugin.Error == null ? "INFO" : "ERROR", plugin.Name + " | " + (plugin.Error ?? "Loaded; optional observer only"));
                };
                FormClosed += (s, e) => _plugins?.Dispose();
            }
            catch (Exception ex) { PluginLog("ERROR", "Plugin framework initialization failed: " + ex); }
        }
        private static void PluginLog(string level, string message)
        {
            try
            {
                DiagnosticLogStore.Record(level, "Plugins", message);
                UI_Update.LogChangeEventArgs.Set("Log", "[Plugins] " + message, level == "ERROR" ? Color.Red : level == "WARN" ? Color.Orange : Color.Black);
            }
            catch { }
        }
        private InspectionSnapshot CreatePluginSnapshot(CogToolBlock tool, string job, string camera, string position, int channel, string result, DateTime captured)
        {
            try
            {
                if (_plugins == null || !_plugins.Observing || _manualLocalOnly.Value || Global.Manual_Trigger_Lock[channel]) return null;
                var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
                long size = 0;
                foreach (CogToolBlockTerminal terminal in tool.Outputs)
                {
                    if (!(terminal.Value is string)) continue;
                    var value = (string)terminal.Value; size += value.Length * 2L;
                    if (size > 8L * 1024 * 1024) throw new InvalidDataException("Plugin String output snapshot exceeds 8 MiB");
                    outputs[terminal.Name] = value;
                }
                bool accepted = result == "1" && tool.RunStatus != null && tool.RunStatus.Result.ToString() == "Accept";
                return new InspectionSnapshot(job, camera, position, Global.PartCode[channel] ?? "", channel, accepted, false, captured, outputs);
            }
            catch (Exception ex)
            {
                foreach (var slot in _plugins?.Loaded ?? Enumerable.Empty<PluginRuntime.Slot>()) slot.Fail("Snapshot failed: " + ex.Message);
                return null;
            }
        }
        private void PublishPluginFailure(string job, string camera, string position, int channel)
        {
            try
            {
                if (_plugins == null || !_plugins.Observing || _manualLocalOnly.Value || Global.Manual_Trigger_Lock[channel]) return;
                _plugins.Publish(new InspectionSnapshot(job, camera, position, Global.PartCode[channel] ?? "", channel, false, false, DateTime.Now, null));
            }
            catch (Exception ex) { PluginLog("ERROR", "Failed observation could not be delivered: " + ex.Message); }
        }
        private sealed class PluginHost : IPluginHost
        {
            private readonly FormMain _main;
            private readonly object _logLock = new object();
            private readonly string _id;
            public string ConfigDirectory { get; private set; }
            public string DataDirectory { get; private set; }
            public string LogDirectory { get; private set; }
            public string Language { get { return UiLanguage.Current; } }
            public bool CanEdit { get { return !_main.IsDisposed && _main.CanEditPluginSettings(); } }
            internal PluginHost(FormMain main, string id)
            {
                _main = main; _id = id; string root = AppDomain.CurrentDomain.BaseDirectory;
                ConfigDirectory = Path.Combine(root, "Project", "Config", "Plugins", id);
                DataDirectory = Path.Combine(root, "Data", "Plugins", id);
                LogDirectory = Path.Combine(root, "Logs", "Plugins", id);
                Directory.CreateDirectory(ConfigDirectory); Directory.CreateDirectory(DataDirectory); Directory.CreateDirectory(LogDirectory);
            }
            public IList<CameraPosition> GetCatalog()
            {
                var result = new List<CameraPosition>();
                var config = XmlConfigHelper.Load(Global.ParameterCogfig);
                foreach (var job in config.Models ?? new List<ModelConfig>())
                    foreach (var camera in job.Cameras ?? new List<CameraConfig>())
                        foreach (var pos in camera.Positions ?? new List<PositionConfig>())
                            result.Add(new CameraPosition { Job = job.Name, Camera = camera.Name, Position = pos.Name, MainUsed = pos.General?.MainUsed == "1", SecondUsed = pos.General?.SecondUsed == "1" });
                return result;
            }
            public void Log(string level, string message)
            {
                try
                {
                    lock (_logLock)
                    {
                        string path = Path.Combine(LogDirectory, "Plugin_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                        int part = 0;
                        while (File.Exists(path) && new FileInfo(path).Length > 20L * 1024 * 1024)
                            path = Path.Combine(LogDirectory, "Plugin_" + DateTime.Now.ToString("yyyyMMdd") + "_" + (++part).ToString("000") + ".log");
                        if (!File.Exists(path))
                        {
                            long total = 0;
                            foreach (var file in new DirectoryInfo(LogDirectory).GetFiles("Plugin_*.log").OrderByDescending(f => f.LastWriteTimeUtc))
                            {
                                total += file.Length;
                                if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30) || total > 512L * 1024 * 1024) try { file.Delete(); } catch { }
                            }
                        }
                        File.AppendAllText(path, DateTimeOffset.Now.ToString("o") + " [" + level + "] " + message + Environment.NewLine);
                    }
                }
                catch (Exception ex) { PluginLog("ERROR", _id + " log write failed: " + ex.Message); }
                PluginLog(level, _id + " | " + message);
            }
        }
    }
}

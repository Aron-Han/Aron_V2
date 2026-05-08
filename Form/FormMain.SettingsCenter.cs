using System;
using System.Windows.Forms;
using System.Linq;
using Aron_V2.UI_Update;

namespace Aron_V2
{
    public partial class FormMain
    {
        private void InitializeSettingsCenterMenu()
        {
            // Retain the original menu objects as permission targets. Their
            // pages are now available under Setting instead of duplicate windows.
            parametersConfigToolStripMenuItem.Visible = false;
            generalToolStripMenuItem.Visible = false;
        }

        private void OpenSettingsCenter(string initialPage)
        {
            SettingsCenterForm center = null;
            center = new SettingsCenterForm(new[]
            {
                new SettingsCenterForm.Page { Title = "Job Control", Icon = DesktopIcon.Job,
                    Revision = () => SettingsFileRevision(Global.ParameterCogfig),
                    Allowed = () => settingToolStripMenuItem.Enabled && parametersConfigToolStripMenuItem.Enabled && generalToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        var page = new JobControlForm(new JobCatalog(Global.ParameterCogfig, Global.Vpp_Root, outputs: Global.VppOutputCfgPath),
                            () => JobControlBlockReason() == null,
                            job => Global.Model_JobID.Take(CommunicationConfigHelper.GetEffectiveChannelCount(CommunicationConfigHelper.Load())).Contains(job))
                            { BlockReason = JobControlBlockReason };
                        page.ConfigSaved += (s, e) => { ApplyCatalogConfiguration(); center.MarkSaved(); };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "General", Icon = DesktopIcon.Settings,
                    SaveButtonName = "btnApply", Revision = () => SettingsFileRevision(Global.CogfigSaveImage),
                    Allowed = () => settingToolStripMenuItem.Enabled && saveImageToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        var page = new ImageRecord();
                        page.PrepareForSettingsCenter();
                        page.SettingsSaved += (s, e) => { ApplyGeneralSettingsSaved(); center.MarkSaved(); };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "Limit Setting", Icon = DesktopIcon.Ruler,
                    SaveButtonName = "btnSave", Revision = () => SettingsFileRevision(Global.ParameterCogfig),
                    Allowed = () => settingToolStripMenuItem.Enabled && parametersConfigToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        var page = new Parameters_Config();
                        page.ConfigSaved += (s, e) => { ApplyCatalogConfiguration(); center.MarkSaved(); };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "Display Layout", Icon = DesktopIcon.Sliders,
                    SaveButtonName = "btnSave", Revision = () => SettingsFileRevision(DisplayLayoutSettings.FilePath),
                    Allowed = () => settingToolStripMenuItem.Enabled && saveImageToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        var page = new DisplayLayoutSettingsForm(_catalogCameraIds?.Count ?? camViews.Count);
                        page.SettingsSaved += (s, e) => { ApplyCameraViewLayout(); center.MarkSaved(); };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "General Camera", Icon = DesktopIcon.Camera,
                    SaveButtonName = "btnSave", Revision = () => SettingsFileRevision(Global.ParameterCogfig),
                    Allowed = () => settingToolStripMenuItem.Enabled && generalToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        Global.CurrentConfig = XmlConfigHelper.Load(Global.ParameterCogfig);
                        XmlConfigHelper.Load_Job(Global.CurrentConfig, Global.Model_JobID[0]);
                        var page = new FormCamGeneralConfig(Global.CurrentConfig, Global.ParameterCogfig);
                        page.ConfigSaved += (s, e) =>
                        {
                            ApplyCatalogConfiguration();
                            center.MarkSaved();
                        };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "Logo", Icon = DesktopIcon.Palette,
                    SaveButtonName = "btnSave", Revision = () => SettingsFileRevision(BrandingSettings.FilePath),
                    Allowed = () => settingToolStripMenuItem.Enabled && generalToolStripMenuItem.Enabled,
                    Create = () => {
                        var page = new LogoSettingsForm();
                        page.SettingsSaved += (s, e) => { BrandingSettings.Apply(this); center.MarkSaved(); };
                        return page;
                    } },
                new SettingsCenterForm.Page { Title = "External plugins", Icon = DesktopIcon.Tools,
                    SaveButtonName = "btnSave", Revision = () => _plugins == null ? "none" : SettingsFileRevision(_plugins.ConfigPath),
                    Allowed = () => settingToolStripMenuItem.Enabled && generalToolStripMenuItem.Enabled,
                    Create = () =>
                    {
                        if (_plugins == null) throw new InvalidOperationException("Plugin runtime unavailable.");
                        var page = new PluginManagementForm(_plugins, CanEditPluginSettings);
                        page.SettingsSaved += (s, e) => center.MarkSaved();
                        return page;
                    } }
            }, initialPage);
            using (center) center.ShowDialog(this);
        }

        private void ApplyGeneralSettingsSaved()
        {
            // Exactly the former ImageRecord.SettingsSaved callback.
            ImageRecord.LoadSettings();
            Global.Save_Image_Root = ImageRecord.Current.Root;
            StartImageCleanupScheduler();
            ApplySoftwareTitle();
            StartLicenseMonitor();
            LogChangeEventArgs.Set("Log", "Settings saved, software title updated.", System.Drawing.Color.Green);
        }

        private static string SettingsFileRevision(string path)
        {
            var file = new System.IO.FileInfo(path);
            return file.Exists ? file.Length + ":" + file.LastWriteTimeUtc.Ticks : "missing";
        }
    }
}

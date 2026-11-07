using Aron.PluginContracts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class PluginHostUiTests
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    sealed class Host : IPluginHost
    {
        public string ConfigDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Project", "Config", "Plugins", "PreWeld");
        public string DataDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Plugins", "PreWeld");
        public string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Plugins", "PreWeld");
        public bool CanEdit => false; public string Language => "en";
        public IList<CameraPosition> GetCatalog() => new[] { new CameraPosition { Job = "Job1", Camera = "Cam1", Position = "Pos1", MainUsed = true } };
        public void Log(string level, string message) { }
    }
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var theme = app.GetType("Aron_V2.DesktopTheme").GetMethod("ApplySettings", All);
            var module = Assembly.LoadFrom(args[1]);
            using (var plugin = (IAronPlugin)Activator.CreateInstance(module.GetType("Aron_V2.PreWeldPlugin")))
            {
                plugin.Initialize(new Host());
                foreach (var page in plugin.Pages)
                {
                    using (var form = page.Create())
                    {
                        theme.Invoke(null, new object[] { form }); form.Show(); Application.DoEvents();
                        foreach (Button button in form.Controls.Cast<Control>().SelectMany(Descendants).OfType<Button>().Where(b => b.Visible))
                            Check(button.Parent.ClientRectangle.Contains(button.Bounds), "Themed plugin button fits parent: " + button.Text);
                        Directory.CreateDirectory("Tests/UIValidation");
                        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/Plugin-Themed-" + (page.Title.StartsWith("Pre-weld") ? "Setup" : "Replay") + ".png"); }
                        form.Close();
                    }
                }
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static IEnumerable<Control> Descendants(Control control) { yield return control; foreach (Control child in control.Controls) foreach (Control nested in Descendants(child)) yield return nested; }
}

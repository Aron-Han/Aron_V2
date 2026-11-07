using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static class HelpTests
{
    const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    sealed class Designer : LicenseContext { public override LicenseUsageMode UsageMode => LicenseUsageMode.Designtime; }
    static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, All).Invoke(instance, args);
    static object Field(object instance, string field) => instance.GetType().GetField(field, All).GetValue(instance);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    static void Pump() { for (int i = 0; i < 16; i++) { Application.DoEvents(); Thread.Sleep(20); } }
    static TreeNode[] Leaves(TreeView tree) => tree.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).ToArray();
    static void Capture(Form help, string name)
    {
        Directory.CreateDirectory("Tests/UIValidation");
        using (var image = new Bitmap(help.Width, help.Height)) { help.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save("Tests/UIValidation/" + name + ".png"); }
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var content = app.GetType("Aron_V2.HelpContent");
            var topics = ((IEnumerable)content.GetMethod("Load", All).Invoke(null, null)).Cast<object>().ToArray();
            Check(topics.Length >= 25 && topics.Select(t => Field(t, "Id")).Distinct().Count() == topics.Length, "Two embedded documents contain distinct navigable topics");
            Check(topics.Select(t => Field(t, "Module")).Distinct().Count() == 2, "Help contains exactly Software Operation and Communication modules");
            foreach (string query in new[] { "克隆", "Result_Send", "resultSend", "0x8007045A", "Ready", "SetUserData Ack", "TCP/IP", "Camera startup check is not complete; manual trigger is unavailable." })
                Check(topics.Any(t => (bool)Call(t, "Matches", query)), "Search finds current software/error terminology: " + query);
            Check(!topics.Any(t => (bool)Call(t, "Matches", "unfindableHelpToken")), "Unknown terms do not produce unrelated search results");
            var type = app.GetType("Aron_V2.HelpForm");
            using (var help = (Form)Activator.CreateInstance(type, All, null, new object[] { "Communication" }, null))
            {
                help.Show(); Pump();
                var tree = (TreeView)Field(help, "_tree"); var reader = (RichTextBox)Field(help, "_reader"); var search = (TextBox)Field(help, "_search");
                Check(reader.ReadOnly && reader.WordWrap && !reader.DetectUrls, "Reader is read-only, wraps locally, and does not open network links");
                Check((string)Field(tree.SelectedNode.Tag, "Module") == "Communication" && reader.Text.Contains("TCP/IP"), "Requested module opens immediately with current support boundaries");
                search.Text = "Result_Send"; Pump();
                Check(Leaves(tree).Length > 0 && Leaves(tree).All(n => (bool)Call(n.Tag, "Matches", "Result_Send")), "Debounced search filters both document modules by content");
                Check(reader.Text.Contains("Result_Send") && !reader.Text.Contains("**"), "Reader renders document text and preserves exact pin identifiers");
                search.Text = "unfindableHelpToken"; Pump();
                Check(tree.Nodes.Count == 0 && ((Label)Field(help, "_heading")).Text == "No matching help topics", "No-match state explains how to refine the query");
                ((Button)help.Controls.Find("HelpClearSearch", true).Single()).PerformClick(); Pump();
                Check(Leaves(tree).Length == topics.Length, "Clear search restores the complete topic directory");
                foreach (var size in new[] { new Size(1250, 760), new Size(1600, 950) })
                {
                    help.ClientSize = size; Pump();
                    var split = (SplitContainer)help.Controls.Find("HelpSplit", true).Single();
                    Check(split.Panel1.Width >= split.Panel1MinSize && split.Panel2.Width >= split.Panel2MinSize && reader.Width > 250, "Navigation and readable text remain usable after resize: " + size);
                }
                search.Text = "Ready"; Pump(); Capture(help, "Help-Communication");
                var language = app.GetType("Aron_V2.UiLanguage"); language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "zh-CN" }); Call(help, "SelectModule", "SoftwareOperation"); Pump();
                Check(help.Text == "帮助" && tree.Nodes[0].Text == "软件操作", "Help UI follows application language; Chinese content remains intact");
                Capture(help, "Help-Software");
                language.GetMethod("SetLanguage", All).Invoke(null, new object[] { "en" }); help.Close();
            }
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Cognex.VisionPro", StringComparison.Ordinal)), "Standalone help needs no VisionPro SDK initialization or camera/PLC startup");
            LicenseManager.CurrentContext = new Designer();
            using (var main = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain")))
            {
                main.Show(); Pump();
                var menu = (MenuStrip)Field(main, "menuStrip1");
                var help = menu.Items.Cast<ToolStripMenuItem>().Single(m => m.Name == "helpToolStripMenuItem");
                int index = menu.Items.IndexOf(help);
                Check(index == 5 && menu.Items[index - 1].Name == "manualTestToolStripMenuItem" && help.Image != null, "Help appears after Manual with a matching toolbar icon");
                Check(help.DropDownItems.Count == 4 && help.DropDownItems[3].Name == "helpExportDiagnosticsMenuItem", "Help exposes both modules and diagnostic export");
                Call(main, "InitPermissionTargets");
                var user = Activator.CreateInstance(app.GetType("Aron_V2.Security.UserRecord")); user.GetType().GetProperty("Role").SetValue(user, "Guest");
                Call(main, "ApplyPermissions", user);
                Check(help.Enabled && ((ToolStripMenuItem)help.DropDownItems[0]).Enabled, "Read-only help stays available without production-operation permissions");
                ((ToolStripMenuItem)help.DropDownItems[0]).PerformClick(); Pump();
                var first = (Form)Field(main, "_helpWindow");
                ((ToolStripMenuItem)help.DropDownItems[1]).PerformClick(); Pump();
                Check(ReferenceEquals(first, Field(main, "_helpWindow")) && !first.Modal, "Help reuses a modeless window and switches modules without blocking the main UI");
                Check((string)Field(((TreeView)Field(first, "_tree")).SelectedNode.Tag, "Module") == "Communication", "Actual menu click navigates to Communication help");
                Check(Field(main, "_communicationService") == null, "Opening Help never creates a communication service");
                first.Close(); main.Close();
            }
            Console.WriteLine("Topics: " + topics.Length); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

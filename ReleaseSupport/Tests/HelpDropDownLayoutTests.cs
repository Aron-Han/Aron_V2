using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
internal static class HelpDropDownLayoutTests
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            using (var form = new Form { ClientSize = new Size(900, 600), ShowInTaskbar = false })
            using (var menu = new MenuStrip())
            {
                var help = new ToolStripMenuItem("Help");
                help.DropDownItems.Add(new ToolStripMenuItem("Software Operation") { Name = "helpSoftwareMenuItem" });
                help.DropDownItems.Add(new ToolStripMenuItem("Communication Help") { Name = "helpCommunicationMenuItem" });
                help.DropDownItems.Add(new ToolStripSeparator());
                help.DropDownItems.Add(new ToolStripMenuItem("Export diagnostics") { Name = "helpExportDiagnosticsMenuItem" });
                menu.Items.Add(help); form.Controls.Add(menu);
                app.GetType("Aron_V2.DesktopTheme").GetMethod("StylePluginMenu", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { help, menu });
                form.Show(); Application.DoEvents();
                foreach (float fontSize in new[] { 10F, 15F, 20F })
                {
                    using (var font = new Font("Segoe UI", fontSize))
                    {
                        foreach (ToolStripItem child in help.DropDownItems) child.Font = font;
                        help.ShowDropDown(); Application.DoEvents();
                        foreach (ToolStripMenuItem child in help.DropDownItems.OfType<ToolStripMenuItem>())
                        {
                            int gap = help.DropDown.ClientSize.Width - child.Bounds.Right;
                            if (gap > help.DropDown.Padding.Right + 1) throw new Exception("Unused right strip: " + gap + " at font " + fontSize);
                        }
                        help.DropDownItems[0].Select(); Application.DoEvents();
                        using (var image = new Bitmap(help.DropDown.Width, help.DropDown.Height))
                        { help.DropDown.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(args[1] + fontSize + ".png"); }
                        help.HideDropDown();
                    }
                }
            }
            Console.WriteLine("PASS Help rows fill popup width with separators at font sizes 10/15/20; no main application/device startup."); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); return 1; }
    }
}

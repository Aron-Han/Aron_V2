using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
internal static class BrandingLayoutTests
{
    [STAThread] static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Aron_V2", "bin", "x64", "Debug", new AssemblyName(e.Name).Name + ".dll"));
            return System.IO.File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        var assembly = Assembly.LoadFrom(args[0]);
        using (var page = (Form)Activator.CreateInstance(assembly.GetType(args.Length > 2 ? args[2] : "Aron_V2.LogoSettingsForm"), true))
        {
            if (args.Length <= 2) page.Size = new Size(1180, 700);
            page.Show(); Application.DoEvents();
            CheckBounds(page);
            using (var bitmap = new Bitmap(page.Width, page.Height))
            { page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, page.Size)); bitmap.Save(args[1]); }
            page.Close();
        }
        Console.WriteLine("Logo settings standalone layout: PASS (no camera/PLC startup)");
    }
    static void CheckBounds(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.Visible && !parent.ClientRectangle.Contains(child.Bounds))
                throw new Exception("Control clipped: " + child.Name + " / " + child.Text);
            CheckBounds(child);
        }
    }
}

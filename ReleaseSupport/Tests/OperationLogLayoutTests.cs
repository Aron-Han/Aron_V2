using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class OperationLogLayoutTests
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); var assembly=Assembly.LoadFrom(args[0]);
            foreach(int width in new[]{1100,800,1200})
            using(var form=(Form)Activator.CreateInstance(assembly.GetType("Aron_V2.OperationLogForm"),true))
            {
                form.Width=width; form.Show();
                for(int i=0;i<5;i++) Application.DoEvents();
                var table=Find(form).OfType<TableLayoutPanel>().Single(c=>c.Name=="OperationFilterBar");
                var inputs=table.Controls.Cast<Control>().Where(c=>!(c is Button)).ToArray();
                int[] centers=inputs.Select(c=>c.PointToScreen(new Point(0,c.Height/2)).Y).ToArray();
                if(centers.Max()-centers.Min()>2) throw new Exception("Filter centers are not aligned: "+width+" / "+string.Join(",",centers));
                var buttons=table.Controls.OfType<Button>().ToArray();
                if(buttons[0].Top!=buttons[1].Top||buttons[0].Height!=buttons[1].Height) throw new Exception("Action bounds differ");
                if(width>900 && Math.Abs(buttons[0].PointToScreen(new Point(0,buttons[0].Height/2)).Y-centers[0])>2) throw new Exception("Button center differs");
                if(table.Controls.Cast<Control>().Any(c=>c.Right>table.ClientSize.Width||c.Left<0||c.Bottom>table.ClientSize.Height)) throw new Exception("Toolbar clipped");
                if(table.Controls.OfType<DateTimePicker>().Any(c=>c.CustomFormat!="yyyy-MM-dd")) throw new Exception("Date format differs");
                Directory.CreateDirectory(args[1]);
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(Path.Combine(args[1],"OperationLog-"+width+".png")); }
                Console.WriteLine("PASS alignment, spacing, responsive rows and date format: "+width);
                form.Close();
            }
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex.Message); return 1; }
    }
    static System.Collections.Generic.IEnumerable<Control> Find(Control parent) { foreach(Control c in parent.Controls) { yield return c; foreach(var d in Find(c)) yield return d; } }
}

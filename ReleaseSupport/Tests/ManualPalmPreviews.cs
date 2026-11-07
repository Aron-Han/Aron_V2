using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
class ManualPalmPreviews
{
    static void Palm(Graphics graphics,RectangleF bounds,int variant,Color ink)
    {
        var saved=graphics.Save(); graphics.TranslateTransform(bounds.X,bounds.Y); graphics.ScaleTransform(bounds.Width/32,bounds.Height/32);
        if(variant==1) { graphics.TranslateTransform(16,16); graphics.RotateTransform(-13); graphics.TranslateTransform(-16,-16); graphics.ScaleTransform(.94f,.94f); }
        using(var path=new GraphicsPath())
        using(var pen=new Pen(ink,1.65f) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round })
        {
            path.StartFigure(); path.AddLine(11,29,11,26);
            path.AddBezier(11,26,8,24,6,21,3.7f,17);
            path.AddBezier(3.7f,17,2.1f,14,4.6f,12.8f,6.3f,14.8f);
            path.AddLine(6.3f,14.8f,9,18); path.AddLine(9,18,9,7);
            path.AddBezier(9,7,9,3.5f,13,3.5f,13,7); path.AddLine(13,7,13,14);
            path.AddLine(13,14,13,4.5f); path.AddBezier(13,4.5f,13,1,17,1,17,4.5f); path.AddLine(17,4.5f,17,14);
            path.AddLine(17,14,17,6); path.AddBezier(17,6,17,2.5f,21,2.5f,21,6); path.AddLine(21,6,21,14);
            path.AddLine(21,14,21,9); path.AddBezier(21,9,21,5.5f,25,5.5f,25,9); path.AddLine(25,9,25,19);
            path.AddBezier(25,19,25,23,24,25,22,27); path.AddLine(22,27,22,29);
            graphics.DrawPath(pen,path);
            if(variant==1) graphics.DrawArc(pen,11.5f,17,8,6,205,130);
            if(variant==2)
            {
                graphics.DrawEllipse(pen,12,17,8,8);
                graphics.DrawLine(pen,28,7,30,5); graphics.DrawLine(pen,28,12,31,12);
            }
        }
        graphics.Restore(saved);
    }
    static int Main(string[] args)
    {
        var assembly=Assembly.LoadFrom(args[0]); var theme=assembly.GetType("Aron_V2.DesktopTheme"); var icon=assembly.GetType("Aron_V2.DesktopIcon");
        var create=theme.GetMethod("CreateIcon",BindingFlags.NonPublic|BindingFlags.Static);
        string[] names={"Login","Setting","Database","Tool","Manual","Help","About"};
        string[] kinds={"User","Settings","Database","Tools","Manual","Help","About"};
        string[] captions={"A · 标准手掌","B · 轻斜手掌","C · 手掌操作"};
        Directory.CreateDirectory(args[1]);
        for(int variant=0;variant<3;variant++)
        using(var bitmap=new Bitmap(1400,420))
        using(var g=Graphics.FromImage(bitmap))
        using(var text=new Font("Segoe UI",15))
        using(var title=new Font("Microsoft YaHei UI",18,FontStyle.Bold))
        using(var small=new Font("Microsoft YaHei UI",11))
        using(var ink=new SolidBrush(Color.FromArgb(38,51,68)))
        using(var border=new Pen(Color.FromArgb(211,220,230)))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.White);
            g.DrawString(captions[variant],title,ink,28,18);
            g.DrawString("菜单实际效果",small,Brushes.SlateGray,28,65);
            int start=28,top=104,width=150,height=112;
            using(var highlight=new SolidBrush(Color.FromArgb(233,243,254))) g.FillRectangle(highlight,start+4*width,top,width,height);
            for(int i=0;i<7;i++)
            {
                int left=start+i*width;
                if(i==4) Palm(g,new RectangleF(left+56,top+14,38,38),variant,Color.FromArgb(38,51,68));
                else using(var image=(Bitmap)create.Invoke(null,new object[]{Enum.Parse(icon,kinds[i]),38,Color.FromArgb(38,51,68)})) g.DrawImage(image,left+56,top+14);
                using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center })
                    g.DrawString(names[i],text,ink,new RectangleF(left,top+64,width,32),format);
                if(i==1||i==2||i==3||i==4||i==5) g.FillPolygon(ink,new[]{new Point(left+126,top+35),new Point(left+132,top+35),new Point(left+129,top+39)});
                g.DrawLine(border,left,top,left,top+height);
            }
            g.DrawLine(border,start,top+height,start+7*width,top+height);
            using(var accent=new Pen(Color.FromArgb(26,119,207),2)) g.DrawLine(accent,start+4*width,top+height,start+5*width,top+height);
            g.DrawString("图标放大预览",small,Brushes.SlateGray,28,260);
            Palm(g,new RectangleF(40,300,82,82),variant,Color.FromArgb(38,51,68));
            Palm(g,new RectangleF(164,300,82,82),variant,Color.FromArgb(26,119,207));
            g.DrawString("深灰默认态     /     蓝色强调态",small,Brushes.SlateGray,285,331);
            bitmap.Save(Path.Combine(args[1],"Manual-Palm-"+(char)('A'+variant)+".png"),ImageFormat.Png);
        }
        Console.WriteLine(args[1]); return 0;
    }
}

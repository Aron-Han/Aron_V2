using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Aron_V2
{
    internal static class BrandingSettings
    {
        internal static string FilePath => ProjectPaths.Config("General/Branding.xml");
        internal static readonly string[] Names = { "A · Vision Eye", "B · Precision Target", "C · Betterway B", "D · Aperture", "E · 3D Layers" };
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

        internal static Bitmap CreateMark(int index, int size = 256)
        {
            var image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size / 256F, size / 256F);
                var colors = new[] { Color.FromArgb(27, 113, 218), Color.FromArgb(12, 144, 158), Color.FromArgb(43, 83, 162), Color.FromArgb(91, 80, 190), Color.FromArgb(23, 134, 123) };
                using (var background = new LinearGradientBrush(new Rectangle(16, 16, 224, 224), colors[index], Color.FromArgb(20, 40, 75), 65F))
                    g.FillEllipse(background, 12, 12, 232, 232);
                using (var pen = new Pen(Color.White, 11) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var white = new SolidBrush(Color.White))
                {
                    if (index == 0)
                    {
                        using (var path = new GraphicsPath()) { path.AddBezier(45, 128, 90, 61, 166, 61, 211, 128); path.AddBezier(211, 128, 166, 195, 90, 195, 45, 128); g.DrawPath(pen, path); }
                        g.FillEllipse(white, 105, 105, 46, 46);
                    }
                    else if (index == 1)
                    {
                        g.DrawEllipse(pen, 76, 76, 104, 104);
                        g.DrawLine(pen, 128, 48, 128, 91); g.DrawLine(pen, 128, 165, 128, 208);
                        g.DrawLine(pen, 48, 128, 91, 128); g.DrawLine(pen, 165, 128, 208, 128);
                        g.FillEllipse(white, 116, 116, 24, 24);
                    }
                    else if (index == 2)
                    {
                        using (var path = new GraphicsPath()) { path.AddLines(new[] { new PointF(91, 190), new PointF(91, 66), new PointF(132, 66) }); path.AddBezier(132, 66, 187, 66, 187, 125, 132, 125); path.AddBezier(132, 125, 194, 125, 194, 190, 132, 190); path.AddLine(132, 190, 91, 190); g.DrawPath(pen, path); }
                    }
                    else if (index == 3)
                    {
                        for (int n = 0; n < 6; n++) { g.TranslateTransform(128, 128); g.RotateTransform(60); g.TranslateTransform(-128, -128); g.DrawLines(pen, new[] { new PointF(128, 49), new PointF(188, 83), new PointF(155, 137) }); }
                    }
                    else
                    {
                        g.DrawPolygon(pen, new[] { new PointF(128, 65), new PointF(200, 106), new PointF(128, 147), new PointF(56, 106) });
                        g.DrawLines(pen, new[] { new PointF(56, 139), new PointF(128, 180), new PointF(200, 139) });
                        g.DrawLines(pen, new[] { new PointF(56, 171), new PointF(128, 212), new PointF(200, 171) });
                    }
                }
            }
            return image;
        }

        internal static Icon ToIcon(Bitmap bitmap)
        {
            IntPtr handle = bitmap.GetHicon();
            try { using (var icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
            finally { DestroyIcon(handle); }
        }
        internal static Bitmap Load(out int selected)
        {
            selected = 3;
            if (!File.Exists(FilePath)) return CreateMark(3);
            var root = XDocument.Load(FilePath).Root;
            selected = (int?)root.Element("Preset") ?? -1;
            if (selected >= 0 && selected < Names.Length) return CreateMark(selected);
            var encoded = (string)root.Element("Image");
            if (string.IsNullOrEmpty(encoded)) return null;
            using (var stream = new MemoryStream(Convert.FromBase64String(encoded)))
            using (var image = Image.FromStream(stream)) return Fit(image);
        }
        internal static Bitmap Fit(Image image)
        {
            var result = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float ratio = Math.Min(256F / image.Width, 256F / image.Height);
                float w = image.Width * ratio, h = image.Height * ratio;
                g.DrawImage(image, (256 - w) / 2, (256 - h) / 2, w, h);
            }
            return result;
        }
        internal static void Save(int selected, Bitmap image)
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);
                var doc = new XDocument(new XElement("Branding", new XElement("Preset", selected), new XElement("Image", Convert.ToBase64String(stream.ToArray()))));
                string temporary = FilePath + ".tmp";
                doc.Save(temporary);
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
            }
            using (var icon = ToIcon(image))
            using (var file = File.Create(Path.Combine(Path.GetDirectoryName(FilePath), "Branding.ico"))) icon.Save(file);
        }
        internal static void Apply(Form form)
        {
            try
            {
                int selected;
                using (var image = Load(out selected))
                {
                    if (image == null) return;
                    form.Icon = ToIcon(image);
                }
            }
            catch (Exception ex) { DiagnosticLogStore.Exception("Load logo", ex); }
        }
    }

    internal sealed class LogoSettingsForm : Form
    {
        internal event EventHandler SettingsSaved;
        private int _selected;
        private Bitmap _draft;
        private readonly PictureBox _preview = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(104, 104), Margin = new Padding(12) };
        internal LogoSettingsForm()
        {
            Text = "Logo";
            AutoScaleMode = AutoScaleMode.Dpi;
            var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
            body.Controls.Add(new Label { Text = UiLanguage.Text("Choose a logo or import a local image."), AutoSize = true, Margin = new Padding(8, 8, 8, 20) });
            var choices = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(900, 0) };
            for (int n = 0; n < BrandingSettings.Names.Length; n++)
            {
                int index = n;
                var button = new Button { Text = BrandingSettings.Names[n], Image = BrandingSettings.CreateMark(n, 64), Size = new Size(160, 120), Margin = new Padding(8), TextImageRelation = TextImageRelation.ImageAboveText };
                button.Click += (s, e) => Choose(index, BrandingSettings.CreateMark(index));
                button.Disposed += (s, e) => button.Image.Dispose();
                choices.Controls.Add(button);
            }
            body.Controls.Add(choices);
            var import = new Button { Text = UiLanguage.Text("Import image"), AutoSize = true, Margin = new Padding(8, 20, 8, 8) };
            import.Click += (s, e) => {
                using (var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.ico" })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        try { using (var image = Image.FromFile(dialog.FileName)) Choose(-1, BrandingSettings.Fit(image)); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            body.Controls.Add(import); body.Controls.Add(_preview);
            body.Controls.Add(new Label { Text = UiLanguage.Text("Save updates the window and taskbar icon. The EXE file icon updates on the next build."), AutoSize = true, MaximumSize = new Size(700, 0), Margin = new Padding(8) });
            var save = new Button { Name = "btnSave", Text = "Save", Size = new Size(120, 34) };
            save.Click += (s, e) => {
                if (_draft == null) { MessageBox.Show(this, UiLanguage.Text("Choose a logo or import a local image.")); return; }
                try { BrandingSettings.Save(_selected, _draft); SettingsSaved?.Invoke(this, EventArgs.Empty); MessageBox.Show(this, UiLanguage.Current == "zh-CN" ? "保存成功。" : "Save OK.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            body.Controls.Add(save); Controls.Add(body);
            DesktopTheme.ApplySettings(this);
            int preset; var current = BrandingSettings.Load(out preset);
            if (current != null) Choose(preset, current);
            Disposed += (s, e) => { _preview.Image = null; _draft?.Dispose(); };
        }
        private void Choose(int selected, Bitmap image)
        {
            _selected = selected; _preview.Image = image;
            var previous = _draft; _draft = image; previous?.Dispose();
        }
    }

    internal sealed class AboutForm : Form
    {
        internal AboutForm()
        {
            Text = UiLanguage.Text("About");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            float scale = DesktopTheme.DisplayScale;
            AutoScaleDimensions = new SizeF(96 * scale, 96 * scale);
            var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, Padding = new Padding((int)(24 * scale)), Location = Point.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var title = new Label { Text = "Aron Vision System", AutoSize = true, Margin = new Padding(0, 0, 0, (int)(8 * scale)) };
            layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
            var version = new Label { Text = BuildInfo.SoftwareVersion, AutoSize = true, ForeColor = DesktopTheme.Accent, Margin = new Padding(0, 0, 0, (int)(22 * scale)) };
            layout.Controls.Add(version, 0, 1); layout.SetColumnSpan(version, 2);
            DateTime date;
            string updated = DateTime.TryParseExact(BuildInfo.BuildDate, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date.ToString("yyyy-MM-dd HH:mm:ss") : BuildInfo.BuildDate;
            string[] captions = UiLanguage.Current == "zh-CN" ? new[] { "更新日期", "作者", "公司", "邮箱" } : new[] { "Updated", "Author", "Company", "Email" };
            string[] values = { updated, "HanJunJie", "Betterway", "jjhan@shbetterway.com" };
            for (int i = 0; i < values.Length; i++)
            {
                layout.Controls.Add(new Label { Text = captions[i] + ":", AutoSize = true, ForeColor = DesktopTheme.Muted,
                    Margin = new Padding(0, (int)(5 * scale), (int)(22 * scale), (int)(9 * scale)) }, 0, i + 2);
                layout.Controls.Add(new Label { Name = "AboutValue" + i, Text = values[i], AutoSize = true,
                    Margin = new Padding(0, (int)(5 * scale), 0, (int)(9 * scale)) }, 1, i + 2);
            }
            var close = new Button { Text = UiLanguage.Text("Close"), AutoSize = true, Anchor = AnchorStyles.Right,
                Margin = new Padding(0, (int)(18 * scale), 0, 0), MinimumSize = new Size((int)(104 * scale), (int)(34 * scale)), DialogResult = DialogResult.OK };
            layout.Controls.Add(close, 0, 6); layout.SetColumnSpan(close, 2);
            Controls.Add(layout); AcceptButton = CancelButton = close;
            DesktopTheme.ApplySettings(this);
            var headingFont = new Font(Font.FontFamily, 14F, FontStyle.Bold);
            title.Font = headingFont;
            Disposed += (s, e) => headingFont.Dispose();
            BrandingSettings.Apply(this);
        }
    }

    public partial class FormMain
    {
        private void InitializeAboutMenu()
        {
            var about = new ToolStripMenuItem("About") { Name = "aboutToolStripMenuItem" };
            menuStrip1.Items.Add(about);
            Load += (s, e) => BrandingSettings.Apply(this);
            about.Click += (s, e) => {
                using (var form = new AboutForm()) form.ShowDialog(this);
            };
        }
    }
}

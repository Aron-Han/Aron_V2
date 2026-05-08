using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal enum DesktopIcon { User, Settings, Database, Tools, Manual, Camera, Algorithm, Sliders, Ruler, Communication, Rotation, Trigger, Send, Help, About, Job, Palette, Export }

    // Native Button behavior (Click, mnemonics, tab order, Enabled and accessibility)
    // is retained; only rendering is replaced for the three camera actions.
    internal sealed class InspectionActionButton : Button
    {
        internal int ActionKind { get; set; }
        private bool _hover, _pressed;

        internal InspectionActionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        internal Rectangle[] ContentBounds(Graphics graphics)
        {
            float scale = graphics.DpiX / 96F;
            int inset = Math.Max(2, (int)(6 * scale));
            int verticalInset = Math.Max(2, (int)(4 * scale));
            int icon = Math.Max(1, Math.Min((int)(18 * scale), Height - verticalInset * 2));
            int gap = Math.Max(2, (int)(7 * scale));
            int available = Math.Max(0, Width - inset * 2 - icon - gap);
            int measured = TextRenderer.MeasureText(graphics, Text, Font,
                Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            // Narrow columns retain readable captions without reducing font size.
            int stackedIcon = Math.Max(1, (int)(14 * scale)), stackedGap = Math.Max(1, (int)(2 * scale));
            if (measured > available && Height >= stackedIcon + stackedGap + Font.Height * 2)
            {
                int top = (Height - stackedIcon - stackedGap - Font.Height * 2) / 2;
                return new[] { new Rectangle((Width - stackedIcon) / 2, top, stackedIcon, stackedIcon),
                    new Rectangle(inset, top + stackedIcon + stackedGap, Math.Max(0, Width - inset * 2), Font.Height * 2) };
            }
            int text = Math.Min(available, measured);
            int left = (Width - icon - gap - text) / 2;
            return new[] { new Rectangle(left, (Height - icon) / 2, icon, icon),
                new Rectangle(left + icon + gap, 0, text, Height) };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            float scale = g.DpiX / 96F;
            g.Clear(Parent == null ? DesktopTheme.Surface : Parent.BackColor);
            if (Width < 4 || Height < 4) return;
            Color fill = !Enabled ? Color.FromArgb(246, 248, 251) :
                _pressed ? Color.FromArgb(216, 233, 252) :
                _hover ? Color.FromArgb(225, 239, 255) : BackColor;
            Color edge = Enabled && (_hover || Focused || ActionKind == 1)
                ? Color.FromArgb(158, 197, 239) : DesktopTheme.Border;
            DesktopTheme.DrawInspectionSurface(g, ClientSize, fill, edge);
            Rectangle[] bounds = ContentBounds(g);
            Color ink = Enabled ? ForeColor : Color.FromArgb(151, 162, 175);
            var state = g.Save();
            g.TranslateTransform(bounds[0].X, bounds[0].Y);
            g.ScaleTransform(bounds[0].Width / 24F, bounds[0].Height / 24F);
            using (var pen = new Pen(Enabled ? DesktopTheme.Accent : ink, 1.7F)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (ActionKind == 0)
                {
                    g.DrawArc(pen, 3, 3, 18, 18, 40, 290);
                    g.DrawLines(pen, new[] { new Point(21, 3), new Point(21, 9), new Point(15, 9) });
                }
                else if (ActionKind == 1)
                    g.DrawPolygon(pen, new[] { new Point(6, 3), new Point(21, 12), new Point(6, 21) });
                else
                {
                    g.DrawRectangle(pen, 2, 3, 20, 18);
                    g.DrawPolygon(pen, new[] { new Point(10, 8), new Point(16, 12), new Point(10, 16) });
                }
            }
            g.Restore(state);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (bounds[1].Height != Height)
                flags = (flags & ~TextFormatFlags.SingleLine) | TextFormatFlags.WordBreak;
            if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(g, Text, Font, bounds[1], ink, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -(int)(4 * scale), -(int)(4 * scale)), ink, fill);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) _pressed = true; base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; base.OnMouseUp(e); Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { _pressed = false; base.OnMouseCaptureChanged(e); Invalidate(); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) _pressed = true; base.OnKeyDown(e); Invalidate(); }
        protected override void OnKeyUp(KeyEventArgs e) { _pressed = false; base.OnKeyUp(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { _pressed = false; base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { _pressed = false; base.OnLostFocus(e); Invalidate(); }
    }

    // Status fills remain owned by SetButton_JobID/PosID/EngineID/CamStatus.
    // Neither focus, hover, pressing nor disabling may replace a status color.
    internal sealed class InspectionReadoutButton : Button
    {
        internal InspectionReadoutButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? DesktopTheme.Surface : Parent.BackColor);
            if (Width < 4 || Height < 4) return;
            DesktopTheme.DrawInspectionSurface(g, ClientSize, BackColor, DesktopTheme.Border);
            float scale = g.DpiX / 96F;
            Rectangle textBounds = Rectangle.Inflate(ClientRectangle, -(int)(6 * scale), 0);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter |
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            else if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText, flags);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -(int)(4 * scale), -(int)(4 * scale)), ForeColor, BackColor);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Keep the state BackColor untouched: existing logic owns its meaning.
    // Only the displayed palette and frame differ from the native button.
    internal sealed class OnlineStateButton : Button
    {
        internal OnlineStateButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? DesktopTheme.Surface : Parent.BackColor);
            if (Width < 4 || Height < 4) return;
            Color fill = BackColor.ToArgb() == Color.Red.ToArgb() ? Color.FromArgb(210, 65, 65) :
                BackColor.ToArgb() == Color.Green.ToArgb() ? Color.FromArgb(40, 143, 94) :
                BackColor.ToArgb() == Color.Yellow.ToArgb() ? Color.FromArgb(246, 202, 76) :
                BackColor.ToArgb() == Color.Orange.ToArgb() ? Color.FromArgb(174, 109, 18) : BackColor;
            if (Enabled && MouseButtons == MouseButtons.Left && Capture)
                fill = ControlPaint.Dark(fill, .08F);
            Color edge = ControlPaint.Dark(fill, .08F);
            DesktopTheme.DrawInspectionSurface(g, ClientSize, fill, edge);
            float scale = g.DpiX / 96F;
            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -(int)(10 * scale), 0);
            Color ink = BackColor.ToArgb() == Color.Yellow.ToArgb() ? DesktopTheme.Ink : Color.White;
            TextRenderer.DrawText(g, Text, Font, bounds, ink,
                TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle,
                    -(int)(5 * scale), -(int)(5 * scale)), ink, fill);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    // Small GDI vector-like icons: generated once at the current DPI, not on
    // every paint and not dependent on emoji fonts, external assets or Cognex.
    internal static partial class DesktopTheme
    {
        internal static readonly Color Surface = Color.White;
        internal static readonly Color Ink = Color.FromArgb(38, 51, 68);
        internal static readonly Color Muted = Color.FromArgb(101, 116, 134);
        internal static readonly Color Border = Color.FromArgb(211, 220, 230);
        internal static readonly Color Accent = Color.FromArgb(26, 119, 207);
        internal static readonly Color Hover = Color.FromArgb(233, 243, 254);

        internal static void DrawInspectionSurface(Graphics g, Size size, Color fill, Color edge)
        {
            float scale = g.DpiX / 96F;
            float x = scale / 2, y = scale / 2;
            float w = size.Width - scale - 1, h = size.Height - scale - 1;
            if (w <= 0 || h <= 0) return;
            float diameter = Math.Min(10 * scale, Math.Min(w, h));
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(edge, Math.Max(1, scale)))
            {
                path.AddArc(x, y, diameter, diameter, 180, 90);
                path.AddArc(x + w - diameter, y, diameter, diameter, 270, 90);
                path.AddArc(x + w - diameter, y + h - diameter, diameter, diameter, 0, 90);
                path.AddArc(x, y + h - diameter, diameter, diameter, 90, 90);
                path.CloseFigure(); g.FillPath(brush, path); g.DrawPath(pen, path);
            }
            g.SmoothingMode = smoothing;
        }

        // .NET Framework's DeviceDpi can stay at 96 without the newer opt-in
        // settings, even in a system-DPI-aware process. Use the actual display
        // DC so icons match the text, without changing the application's DPI mode.
        internal static float DisplayScale
        {
            get { using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) return graphics.DpiX / 96F; }
        }

        internal static void StyleButton(Button button, bool primary = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(20, 103, 184) : Hover;
            button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(18, 88, 158) : Color.FromArgb(218, 235, 252);
            button.BackColor = primary ? Accent : Color.White;
            button.ForeColor = primary ? Color.White : Ink;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        internal static void StyleInspectionActions(Control owner, params Button[] buttons)
        {
            // Explicit action-only list: do not restyle Waiting/Job/Pos/Engine/
            // camera/Online indicators, whose colors are driven by runtime state.
            var font = new Font("Segoe UI", 9F);
            foreach (Button button in buttons)
            {
                StyleButton(button);
                button.Font = font;
                var action = button as InspectionActionButton;
                if (action != null)
                {
                    action.ActionKind = button.Name == "Reset" ? 0 : button.Name == "btnTrigger" ? 1 : 2;
                    action.BackColor = action.ActionKind == 1 ? Color.FromArgb(235, 244, 255) : Surface;
                    action.ForeColor = action.ActionKind == 1 ? Accent : Ink;
                    action.Invalidate();
                }
            }
            owner.Disposed += (s, e) => font.Dispose();
        }

        internal static void StyleInspectionReadouts(Control owner, params Button[] buttons)
        {
            // Frame and type only. Runtime retains ownership of both colors,
            // text, enabled state and the existing Job/Pos selection handlers.
            var font = new Font("Segoe UI", 9F);
            foreach (Button button in buttons)
            {
                button.Font = font;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Border;
                button.UseVisualStyleBackColor = false;
                EventHandler syncHoverColor = (s, e) =>
                {
                    button.FlatAppearance.MouseOverBackColor = button.BackColor;
                    button.FlatAppearance.MouseDownBackColor = button.BackColor;
                };
                syncHoverColor(button, EventArgs.Empty);
                button.BackColorChanged += syncHoverColor;
            }
            owner.Disposed += (s, e) => font.Dispose();
        }

        internal static void ApplyMainMenu(MenuStrip menu, TableLayoutPanel layout)
        {
            menu.SuspendLayout();
            float scale = DisplayScale;
            var font = new Font("Segoe UI", 10F);
            menu.Font = font;
            menu.BackColor = Color.White;
            menu.ForeColor = Ink;
            menu.Padding = Padding.Empty;
            menu.Margin = Padding.Empty;
            menu.AutoSize = true;
            menu.ShowItemToolTips = false;
            menu.ImageScalingSize = new Size((int)(28 * scale), (int)(28 * scale));
            menu.Renderer = new DesktopMenuRenderer(scale);
            var kinds = new[] { DesktopIcon.User, DesktopIcon.Settings, DesktopIcon.Database, DesktopIcon.Tools, DesktopIcon.Manual, DesktopIcon.Help, DesktopIcon.About };
            var images = new Image[Math.Min(menu.Items.Count, kinds.Length)];
            for (int i = 0; i < images.Length; i++)
            {
                ToolStripItem item = menu.Items[i];
                images[i] = CreateIcon(kinds[i], menu.ImageScalingSize.Width, Ink);
                item.Image = images[i];
                item.ImageScaling = ToolStripItemImageScaling.None;
                item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                item.TextImageRelation = TextImageRelation.ImageAboveText;
                item.ImageAlign = ContentAlignment.MiddleCenter;
                item.TextAlign = ContentAlignment.MiddleCenter;
                item.AutoSize = false;
                item.Padding = new Padding((int)(10 * scale), (int)(8 * scale), (int)(10 * scale), (int)(8 * scale));
                item.Margin = Padding.Empty;
                item.ToolTipText = item.Text.Replace("&", "");
                item.AccessibleName = item.ToolTipText;
            }
            Action sizeItems = () =>
            {
                int width = menu.Items.Cast<ToolStripItem>().Select(i => TextRenderer.MeasureText(i.Text.Replace("&", ""), menu.Font).Width).DefaultIfEmpty(0).Max();
                width = Math.Max((int)(96 * scale), width + (int)(28 * scale));
                int height = Math.Max((int)(64 * scale), menu.ImageScalingSize.Height + menu.Font.Height + (int)(12 * scale));
                foreach (ToolStripItem item in menu.Items)
                {
                    item.Size = new Size(width, height);
                    item.Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale));
                }
                menu.PerformLayout();
            };
            menu.FontChanged += (s, e) => sizeItems();
            Form owner = layout.FindForm();
            if (owner != null) owner.Load += (s, e) => sizeItems();
            layout.RowStyles[0].SizeType = SizeType.AutoSize;
            ApplyDropDownMenus(menu);
            sizeItems();
            menu.Disposed += (s, e) => { foreach (var image in images) image?.Dispose(); font.Dispose(); };
            menu.ResumeLayout(true);
        }

        internal static Bitmap CreateIcon(DesktopIcon kind, int pixels, Color color)
        {
            var bitmap = new Bitmap(pixels, pixels);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(color, 1.7F) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(pixels / 32F, pixels / 32F);
                switch (kind)
                {
                    case DesktopIcon.Camera:
                        g.DrawRectangle(pen, 3, 8, 26, 20); g.DrawEllipse(pen, 11, 12, 10, 10);
                        g.DrawLines(pen, new[] { new Point(9, 8), new Point(12, 4), new Point(20, 4), new Point(23, 8) });
                        break;
                    case DesktopIcon.Algorithm:
                        g.DrawLine(pen, 16, 8, 7, 24); g.DrawLine(pen, 16, 8, 25, 24); g.DrawLine(pen, 7, 24, 25, 24);
                        foreach (var point in new[] { new Point(16, 6), new Point(6, 26), new Point(26, 26) })
                        { using (var fill = new SolidBrush(Surface)) g.FillEllipse(fill, point.X - 3, point.Y - 3, 6, 6); g.DrawEllipse(pen, point.X - 3, point.Y - 3, 6, 6); }
                        break;
                    case DesktopIcon.Communication:
                        g.DrawRectangle(pen, 10, 3, 12, 8);
                        g.DrawLine(pen, 16, 11, 16, 17);
                        g.DrawLine(pen, 6, 17, 26, 17);
                        g.DrawLine(pen, 6, 17, 6, 22); g.DrawLine(pen, 26, 17, 26, 22);
                        g.DrawRectangle(pen, 2, 22, 8, 7); g.DrawRectangle(pen, 22, 22, 8, 7);
                        break;
                    case DesktopIcon.About:
                        g.DrawEllipse(pen, 3, 3, 26, 26);
                        g.DrawLine(pen, 16, 14, 16, 23); g.DrawLine(pen, 13, 23, 19, 23);
                        g.DrawEllipse(pen, 15.3F, 8, 1.4F, 1.4F);
                        break;
                    case DesktopIcon.Job:
                        g.DrawPolygon(pen, new[] { new Point(3, 7), new Point(12, 7), new Point(15, 11), new Point(29, 11), new Point(29, 27), new Point(3, 27) });
                        g.DrawLine(pen, 9, 17, 23, 17); g.DrawLine(pen, 9, 22, 19, 22);
                        break;
                    case DesktopIcon.Palette:
                        using (var palette = new GraphicsPath())
                        {
                            palette.AddBezier(28, 15, 28, 1, 6, 0, 3, 14);
                            palette.AddBezier(3, 14, 0, 27, 17, 33, 19, 25);
                            palette.AddBezier(19, 25, 17, 19, 27, 23, 28, 15);
                            palette.CloseFigure(); g.DrawPath(pen, palette);
                        }
                        foreach (var spot in new[] { new Point(9, 11), new Point(16, 7), new Point(23, 12), new Point(8, 19) }) g.DrawEllipse(pen, spot.X - 1, spot.Y - 1, 2, 2);
                        break;
                    case DesktopIcon.Export:
                        g.DrawLines(pen, new[] { new Point(8, 13), new Point(8, 3), new Point(24, 3), new Point(24, 13) });
                        g.DrawLine(pen, 16, 10, 16, 23);
                        g.DrawLines(pen, new[] { new Point(11, 18), new Point(16, 23), new Point(21, 18) });
                        g.DrawLines(pen, new[] { new Point(4, 23), new Point(4, 29), new Point(28, 29), new Point(28, 23) });
                        break;
                    case DesktopIcon.Sliders:
                        for (int row = 0; row < 3; row++) { int y = 7 + row * 9, x = row == 1 ? 21 : 11; g.DrawLine(pen, 3, y, 29, y); using (var fill = new SolidBrush(Surface)) g.FillEllipse(fill, x - 3, y - 3, 6, 6); g.DrawEllipse(pen, x - 3, y - 3, 6, 6); }
                        break;
                    case DesktopIcon.Ruler:
                        g.DrawRectangle(pen, 3, 9, 26, 14);
                        for (int x = 7; x < 28; x += 5) g.DrawLine(pen, x, 9, x, x % 2 == 0 ? 17 : 14);
                        break;
                    case DesktopIcon.Rotation:
                        g.DrawArc(pen, 5, 5, 22, 22, 40, 290); g.DrawLines(pen, new[] { new Point(21, 3), new Point(27, 7), new Point(22, 12) });
                        g.DrawLine(pen, 12, 16, 20, 16); g.DrawLine(pen, 16, 12, 16, 20);
                        break;
                    case DesktopIcon.Trigger:
                        g.DrawPolygon(pen, new[] { new Point(18, 3), new Point(7, 18), new Point(15, 18), new Point(12, 29), new Point(26, 12), new Point(18, 12) });
                        break;
                    case DesktopIcon.Send:
                        g.DrawLine(pen, 3, 16, 27, 16); g.DrawLines(pen, new[] { new Point(18, 7), new Point(27, 16), new Point(18, 25) });
                        break;
                    case DesktopIcon.User:
                        g.DrawEllipse(pen, 11, 3, 10, 10);
                        using (var body = new GraphicsPath())
                        {
                            body.AddBezier(5, 28, 5, 12, 27, 12, 27, 28);
                            body.CloseFigure(); g.DrawPath(pen, body);
                        }
                        break;
                    case DesktopIcon.Settings:
                        var points = new PointF[64];
                        for (int i = 0; i < points.Length; i++)
                        {
                            double a = i * Math.PI * 2 / points.Length;
                            float radius = i % 8 < 4 ? 13F : 10.5F;
                            points[i] = new PointF(16 + radius * (float)Math.Cos(a), 16 + radius * (float)Math.Sin(a));
                        }
                        g.DrawPolygon(pen, points); g.DrawEllipse(pen, 11.5F, 11.5F, 9, 9);
                        break;
                    case DesktopIcon.Database:
                        g.DrawEllipse(pen, 6, 3, 20, 7);
                        g.DrawLine(pen, 6, 6.5F, 6, 25);
                        g.DrawLine(pen, 26, 6.5F, 26, 25);
                        g.DrawArc(pen, 6, 12, 20, 7, 0, 180);
                        g.DrawArc(pen, 6, 21.5F, 20, 7, 0, 180);
                        break;
                    case DesktopIcon.Tools:
                        using (var tool = new GraphicsPath())
                        {
                            tool.AddLines(new[] { new PointF(20, 3), new PointF(17, 8), new PointF(20, 12), new PointF(25, 11), new PointF(28, 6) });
                            tool.AddBezier(28, 6, 32, 14, 23, 20, 18, 16);
                            tool.AddLine(18, 16, 7, 28);
                            tool.AddBezier(7, 28, 3, 31, 0, 27, 4, 23);
                            tool.AddLine(4, 23, 15, 12);
                            tool.AddBezier(15, 12, 11, 7, 15, 2, 20, 3);
                            tool.CloseFigure(); g.DrawPath(pen, tool);
                        }
                        break;
                    case DesktopIcon.Help:
                        g.DrawEllipse(pen, 3, 3, 26, 26);
                        g.DrawArc(pen, 11, 8, 10, 9, 190, 245);
                        g.DrawLine(pen, 16, 17, 16, 21);
                        g.DrawEllipse(pen, 15.6F, 25, 0.8F, 0.8F);
                        break;
                    case DesktopIcon.Manual:
                        using (var book = new GraphicsPath())
                        {
                            book.AddLines(new[] { new PointF(16, 7), new PointF(12, 4), new PointF(3, 4), new PointF(3, 26), new PointF(12, 26), new PointF(16, 29), new PointF(20, 26), new PointF(29, 26), new PointF(29, 4), new PointF(20, 4), new PointF(16, 7), new PointF(16, 29) });
                            g.DrawPath(pen, book);
                        }
                        break;
                }
            }
            return bitmap;
        }

        private sealed class DesktopMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly float _scale;
            private readonly DropDownRenderer _popup;
            internal DesktopMenuRenderer(float scale) { _scale = scale; _popup = new DropDownRenderer(scale); RoundedEdges = false; }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                if (e.ToolStrip is MenuStrip) e.Graphics.Clear(Color.White);
                else _popup.DrawToolStripBackground(e);
            }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!(e.Item.Owner is MenuStrip)) { _popup.DrawMenuItemBackground(e); return; }
                bool active = e.Item.Enabled && (e.Item.Selected || e.Item.Pressed);
                using (var brush = new SolidBrush(active ? Hover : Color.White)) e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, e.Item.Width - 1, 0, e.Item.Width - 1, e.Item.Height);
                var menuItem = e.Item as ToolStripMenuItem;
                if (menuItem != null && menuItem.HasDropDownItems)
                {
                    float scale = _scale;
                    float x = e.Item.Width - 15 * scale;
                    float block = e.Item.Image.Height + e.Item.Font.Height + 4 * scale;
                    float y = (e.Item.Height - block) / 2 + e.Item.Image.Height / 2 - 2 * scale;
                    using (var brush = new SolidBrush(e.Item.Enabled ? Ink : SystemColors.GrayText))
                        e.Graphics.FillPolygon(brush, new[] { new PointF(x - 3 * scale, y), new PointF(x + 3 * scale, y), new PointF(x, y + 4 * scale) });
                }
                if (active)
                    using (var brush = new SolidBrush(Accent)) e.Graphics.FillRectangle(brush, 0, e.Item.Height - 3, e.Item.Width, 3);
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!(e.Item.Owner is MenuStrip)) { _popup.DrawItemText(e); return; }
                e.TextColor = e.Item.Enabled ? Ink : SystemColors.GrayText;
                int gap = (int)(4 * _scale), imageHeight = e.Item.Image == null ? 0 : e.Item.Image.Height;
                int blockHeight = imageHeight + gap + e.TextFont.Height;
                int top = (e.Item.Height - blockHeight) / 2;
                var rect = new Rectangle(0, top + imageHeight + gap, e.Item.Width, e.TextFont.Height);
                TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, rect, e.TextColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix | TextFormatFlags.NoPadding);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (!(e.ToolStrip is MenuStrip)) { _popup.DrawToolStripBorder(e); return; }
                using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
            }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                if (!(e.Item.Owner is MenuStrip)) { _popup.DrawItemImage(e); return; }
                if (e.Image == null) return;
                int blockHeight = e.Image.Height + (int)(4 * _scale) + e.Item.Font.Height;
                var point = new Point((e.Item.Width - e.Image.Width) / 2, (e.Item.Height - blockHeight) / 2);
                if (e.Item.Enabled) e.Graphics.DrawImage(e.Image, point);
                else ControlPaint.DrawImageDisabled(e.Graphics, e.Image, point.X, point.Y, Color.White);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { _popup.DrawSeparator(e); }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { _popup.DrawArrow(e); }
        }
    }
}

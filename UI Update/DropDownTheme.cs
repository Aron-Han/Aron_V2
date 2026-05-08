using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal static partial class DesktopTheme
    {
        internal static void StylePluginMenu(ToolStripMenuItem parent, MenuStrip menu) { StyleDropDown(parent, menu); }
        private static void ApplyDropDownMenus(MenuStrip menu)
        {
            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
                StyleDropDown(item, menu);
        }

        private static bool StartsMenuGroup(ToolStripItem item)
        {
            return item.Name == "saveImageToolStripMenuItem" || item.Name == "communicateToolStripMenuItem" || item.Name == "sendDataToolStripMenuItem";
        }

        private static DesktopIcon MenuIcon(ToolStripItem item)
        {
            string key = item.Name.ToLowerInvariant();
            if (key.Contains("exportdiagnostics")) return DesktopIcon.Export;
            if (key.Contains("helpcommunication")) return DesktopIcon.Communication;
            if (key.Contains("help")) return DesktopIcon.Help;
            if (key.Contains("camera") || key.Contains("general")) return DesktopIcon.Camera;
            if (key.Contains("algorithm")) return DesktopIcon.Algorithm;
            if (key.Contains("parametersconfig")) return DesktopIcon.Ruler;
            if (key.Contains("communicate") || key.Contains("profinet") || key.Contains("s7")) return DesktopIcon.Communication;
            if (key.Contains("rotate")) return DesktopIcon.Rotation;
            if (key.Contains("trigger")) return DesktopIcon.Trigger;
            if (key.Contains("send") || key.Contains("output")) return DesktopIcon.Send;
            return DesktopIcon.Sliders;
        }

        private static void StyleDropDown(ToolStripMenuItem parent, MenuStrip root)
        {
            if (!parent.HasDropDownItems) return; // Login/Database keep their direct actions.
            ToolStripDropDown drop = parent.DropDown;
            float scale = DisplayScale;
            var font = new Font("Segoe UI", 10F);
            drop.Font = font;
            drop.BackColor = Surface;
            drop.ForeColor = Ink;
            drop.ShowItemToolTips = false;
            drop.Renderer = new DropDownRenderer(scale);
            drop.Padding = new Padding((int)(5 * scale));
            drop.ImageScalingSize = new Size((int)(20 * scale), (int)(20 * scale));
            var popup = drop as ToolStripDropDownMenu;
            if (popup != null) { popup.ShowImageMargin = true; popup.ShowCheckMargin = false; }
            foreach (ToolStripMenuItem child in drop.Items.OfType<ToolStripMenuItem>())
            {
                // Keep the exact original item, name, text, permissions, checked
                // state, shortcut keys, handler list and submenu structure.
                child.Font = font;
                child.TextImageRelation = TextImageRelation.ImageBeforeText;
                child.ImageAlign = ContentAlignment.MiddleLeft;
                child.TextAlign = ContentAlignment.MiddleLeft;
                child.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                if (child.Image == null)
                {
                    Image image = CreateIcon(MenuIcon(child), drop.ImageScalingSize.Width, Ink);
                    child.Image = image;
                    child.ImageScaling = ToolStripItemImageScaling.SizeToFit;
                    child.Disposed += (s, e) => image.Dispose();
                }
                StyleDropDown(child, root);
            }
            Action arrange = () =>
            {
                drop.ImageScalingSize = new Size((int)(20 * scale), (int)(20 * scale));
                int textWidth = drop.Items.OfType<ToolStripMenuItem>().Where(i => i.Available).Select(i => TextRenderer.MeasureText(i.Text, i.Font).Width + (i.ShowShortcutKeys && i.ShortcutKeys != Keys.None ? TextRenderer.MeasureText(i.ShortcutKeyDisplayString ?? i.ShortcutKeys.ToString(), i.Font).Width : 0)).DefaultIfEmpty(0).Max();
                int width = Math.Max((int)(250 * scale), textWidth + (int)(86 * scale));
                int height = drop.Padding.Vertical;
                drop.SuspendLayout();
                foreach (ToolStripItem child in drop.Items)
                {
                    child.AutoSize = false;
                    child.Size = new Size(width - drop.Padding.Horizontal, (int)((child is ToolStripSeparator ? 10 : StartsMenuGroup(child) ? 50 : 44) * scale));
                    child.Padding = new Padding((int)(10 * scale), StartsMenuGroup(child) ? (int)(10 * scale) : 0, (int)(14 * scale), 0);
                    // Available is independent of the closed popup's Visible
                    // state. Hidden legacy/permission items must occupy no space.
                    if (child.Available) height += child.Height + child.Margin.Vertical;
                }
                drop.AutoSize = false;
                drop.Size = new Size(width, height);
                drop.ResumeLayout(true);
                // Native DropDownMenu layout determines the common item width
                // independently of a fixed popup width. Trim the outer window
                // to those actual rows, avoiding a dead strip beside selection.
                // Native separators stretch to the old popup width. Including
                // them here preserves an otherwise empty strip beside each row.
                int right = drop.Items.OfType<ToolStripMenuItem>().Where(i => i.Available).Select(i => i.Bounds.Right).DefaultIfEmpty(0).Max();
                if (right > 0) drop.Width = right + drop.Padding.Right;
                int bottom = drop.Items.Cast<ToolStripItem>().Where(i => i.Available).Select(i => i.Bounds.Bottom + i.Margin.Bottom).DefaultIfEmpty(drop.Padding.Top).Max();
                drop.Height = bottom + drop.Padding.Bottom;
            };
            parent.DropDownOpening += (s, e) => arrange();
            if (ReferenceEquals(parent.Owner, root)) parent.DropDownDirection = ToolStripDropDownDirection.BelowRight;
            parent.DropDownOpened += (s, e) =>
            {
                arrange();
                if (!ReferenceEquals(parent.Owner, root)) return;
                // Align actual popup window, not its image/text margin. Clamp
                // only at monitor edges, where exact left alignment cannot fit.
                Point anchor = root.PointToScreen(new Point(parent.Bounds.Left, parent.Bounds.Bottom));
                Rectangle work = Screen.FromControl(root).WorkingArea;
                anchor.X = Math.Max(work.Left, Math.Min(anchor.X, work.Right - drop.Width));
                // Keep the framework's vertical border join and automatic flip
                // above the owner when the monitor has no room below.
                drop.Location = new Point(anchor.X, drop.Top);
            };
            drop.Disposed += (s, e) => font.Dispose();
            arrange();
        }

        private sealed class DropDownRenderer : ToolStripProfessionalRenderer
        {
            private readonly float _scale;
            internal DropDownRenderer(float scale) { _scale = scale; RoundedEdges = false; }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(Surface); }
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
            protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            {
                if (e.Image == null) return;
                // Framework inherits the toolbar's larger image metrics into
                // popups at high DPI. Draw the submenu icon at its own size.
                int size = (int)(20 * _scale);
                var rect = new Rectangle(e.ImageRectangle.X + (e.ImageRectangle.Width - size) / 2,
                    (e.Item.Height - size) / 2 + (StartsMenuGroup(e.Item) ? (int)(3 * _scale) : 0), size, size);
                using (var attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    var matrix = new System.Drawing.Imaging.ColorMatrix(); matrix.Matrix33 = e.Item.Enabled ? 1F : 0.35F;
                    attributes.SetColorMatrix(matrix);
                    e.Graphics.DrawImage(e.Image, rect, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed))
                    using (var brush = new SolidBrush(Hover)) e.Graphics.FillRectangle(brush, new Rectangle((int)(3 * _scale), (int)(3 * _scale), e.Item.Width - (int)(6 * _scale), e.Item.Height - (int)(6 * _scale)));
                if (StartsMenuGroup(e.Item))
                    using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, (int)(10 * _scale), 0, e.Item.Width - (int)(10 * _scale), 0);
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Ink : SystemColors.GrayText;
                int inset = StartsMenuGroup(e.Item) ? (int)(6 * _scale) : 0;
                e.TextRectangle = new Rectangle(e.TextRectangle.X, inset,
                    e.TextRectangle.Width, e.Item.Height - inset);
                e.TextFormat = (e.TextFormat & ~(TextFormatFlags.Top | TextFormatFlags.Bottom)) | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
                base.OnRenderItemText(e);
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = e.Item.Enabled ? Muted : SystemColors.GrayText;
                base.OnRenderArrow(e);
            }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, 10, e.Item.Height / 2, e.Item.Width - 10, e.Item.Height / 2);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }
    }
}

using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Aron_V2
{
    internal static partial class DesktopTheme
    {
        private static readonly ConditionalWeakTable<Control, object> SurfaceObservers = new ConditionalWeakTable<Control, object>();

        // Backgrounds only: do not theme fonts, sizes, buttons, data cells,
        // status text or the private children of third-party image editors.
        internal static void ApplySurfaceColors(Control control)
        {
            string ns = control.GetType().Namespace ?? string.Empty;
            if (!ns.StartsWith("System.Windows.Forms", StringComparison.Ordinal) &&
                !ns.StartsWith("Aron_V2", StringComparison.Ordinal)) return;

            bool container = control is Form || control is Panel || control is UserControl ||
                control is GroupBox || control is TabPage || control is TabControl ||
                control is SplitContainer || control is ToolStrip;
            if (!container) return;

            Color color = control.BackColor;
            bool neutral = color.A == 0 || (color.GetSaturation() < 0.15F && color.GetBrightness() > 0.85F);
            // Keep explicitly drawn hairline separators and colored status panels.
            bool separator = control is Panel && !(control is TableLayoutPanel) &&
                !(control is FlowLayoutPanel) && (control.Height <= 2 || control.Width <= 2);
            if (neutral && !separator) control.BackColor = Surface;

            SurfaceObservers.GetValue(control, owner =>
            {
                if (owner is StatusStrip) ((StatusStrip)owner).Renderer = new SurfaceStatusRenderer();
                // Covers camera/channel views inserted after startup; no paint
                // polling and no recursive access inside Cognex controls.
                owner.ControlAdded += SurfaceChildAdded;
                return new object();
            });
            foreach (Control child in control.Controls) ApplySurfaceColors(child);
        }

        private static void SurfaceChildAdded(object sender, ControlEventArgs e)
        {
            ApplySurfaceColors(e.Control);
        }

        // The exact existing status items remain runtime-owned. Only their
        // column geometry/type is changed; no protocol or login data is parsed.
        internal static void StyleMainStatus(StatusStrip strip, ToolStripStatusLabel user, ToolStripStatusLabel communication)
        {
            float scale = DisplayScale;
            var font = new Font("Segoe UI", 9F);
            strip.Font = font;
            strip.MinimumSize = new Size(0, (int)(28 * scale));
            strip.Padding = new Padding((int)(4 * scale), (int)(2 * scale), strip.Padding.Right, (int)(2 * scale));
            foreach (var item in new[] { user, communication })
            {
                item.Font = font;
                item.AutoSize = false;
                item.Spring = false;
                item.TextAlign = ContentAlignment.MiddleLeft;
                item.Padding = new Padding((int)(12 * scale), 0, (int)(12 * scale), 0);
                item.Margin = new Padding(0, (int)(2 * scale), (int)(8 * scale), (int)(2 * scale));
                Action sizeColumn = () =>
                {
                    int textWidth = TextRenderer.MeasureText(item.Text, item.Font).Width;
                    item.Size = new Size(Math.Max((int)((item == user ? 160 : 220) * scale), textWidth + item.Padding.Horizontal),
                        Math.Max((int)(22 * scale), item.Font.Height + (int)(6 * scale)));
                };
                item.TextChanged += (s, e) => sizeColumn();
                sizeColumn();
            }
            strip.Paint += (s, e) =>
            {
                if (user.Owner != strip || communication.Owner != strip || !user.Available || !communication.Available) return;
                int x = user.Bounds.Right + (int)(4 * scale);
                using (var pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, x, (int)(6 * scale), x, strip.Height - (int)(6 * scale));
            };
            strip.Disposed += (s, e) => font.Dispose();
        }

        private sealed class SurfaceStatusRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(Surface);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(Border)) e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
            }
        }
    }
}

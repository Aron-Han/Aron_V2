using System;
using System.Drawing;
using System.Windows.Forms;

namespace Aron_V2
{
    internal static partial class UiLanguage
    {
        internal static DialogResult ConfirmCameraDeletion(IWin32Window owner, string cameraName)
        {
            using (Form dialog = CreateCameraDeleteConfirmation(cameraName))
                return dialog.ShowDialog(owner);
        }

        // Native MessageBox buttons use the OS language rather than the chosen
        // application language. This UI-only dialog localizes both text and
        // buttons without changing thread culture or the deletion operation.
        internal static Form CreateCameraDeleteConfirmation(string cameraName)
        {
            float scale = DesktopTheme.DisplayScale;
            Func<int, int> px = n => (int)Math.Round(n * scale);
            var font = new Font("Segoe UI", 9F);
            var dialog = new Form
            {
                Name = "CameraDeleteConfirmation", Text = Text("Confirm"), Font = font,
                AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96 * scale, 96 * scale),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = DesktopTheme.Surface, ForeColor = DesktopTheme.Ink, Padding = new Padding(px(20))
            };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, px(48)));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var icon = SystemIcons.Question.ToBitmap();
            layout.Controls.Add(new PictureBox { Image = icon, SizeMode = PictureBoxSizeMode.CenterImage,
                Size = new Size(px(36), px(40)), Margin = Padding.Empty }, 0, 0);
            const string message = "Delete camera {0} and all of its Pos configurations?\r\nVPP files on disk will be kept. Adding a camera with the same name later will not overwrite existing VPP files.";
            layout.Controls.Add(new Label { Name = "ConfirmationMessage", Text = string.Format(Text(message), cameraName),
                AutoSize = true, MaximumSize = new Size(px(490), 0), MinimumSize = new Size(px(400), px(52)),
                Margin = new Padding(0, px(4), 0, px(16)) }, 1, 0);
            var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            var no = new Button { Name = "ConfirmationNo", Text = Text("No"), DialogResult = DialogResult.No,
                Size = new Size(px(90), px(32)), Margin = Padding.Empty, TabIndex = 1 };
            var yes = new Button { Name = "ConfirmationYes", Text = Text("Yes"), DialogResult = DialogResult.Yes,
                Size = no.Size, Margin = new Padding(0, 0, px(10), 0), TabIndex = 0 };
            DesktopTheme.StyleButton(no); DesktopTheme.StyleButton(yes, true);
            actions.Controls.Add(no); actions.Controls.Add(yes);
            layout.Controls.Add(actions, 0, 1); layout.SetColumnSpan(actions, 2);
            dialog.Controls.Add(layout);
            // Preserve the former Yes/No decision contract and default Yes.
            // Escape and window-close remain non-Yes and cannot delete anything.
            dialog.AcceptButton = yes; dialog.CancelButton = no;
            dialog.Disposed += (s, e) => { icon.Dispose(); font.Dispose(); };
            return dialog;
        }
    }
}

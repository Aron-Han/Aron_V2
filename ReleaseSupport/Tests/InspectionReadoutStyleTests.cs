using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class InspectionReadoutStyleTests
{
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); Console.WriteLine("PASS " + text); }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            var app = Assembly.LoadFrom(args[0]);
            var method = app.GetType("Aron_V2.DesktopTheme", true).GetMethod("StyleInspectionReadouts", BindingFlags.Static | BindingFlags.NonPublic);
            var readoutType = app.GetType("Aron_V2.InspectionReadoutButton", true);
            using (var form = new Form { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size(900, 220), BackColor = Color.White })
            {
                var buttons = new[] { "Job1", "Pos1", "Engine:0", "Cam1" }.Select((text, i) =>
                {
                    var button = (Button)Activator.CreateInstance(readoutType, true);
                    button.Text = text; button.Bounds = new Rectangle(10 + i * 210, 80, 200, 40);
                    button.BackColor = i == 0 ? Color.Green : i == 1 ? Color.Gainsboro : i == 2 ? Color.Yellow : Color.Red;
                    button.ForeColor = Color.Black; button.Enabled = i != 2; button.TabIndex = i;
                    return button;
                }).ToArray();
                form.Controls.AddRange(buttons);
                var colors = buttons.Select(b => b.BackColor).ToArray(); var bounds = buttons.Select(b => b.Bounds).ToArray(); var texts = buttons.Select(b => b.Text).ToArray();
                int clicks = 0; buttons[1].Click += (s, e) => clicks++;
                method.Invoke(null, new object[] { form, buttons });
                Check(colors.SequenceEqual(buttons.Select(b => b.BackColor)) && buttons.All(b => b.ForeColor == Color.Black), "All original background/foreground status colors retained");
                Check(buttons.All(b => b.Font.Name == "Segoe UI" && b.FlatStyle == FlatStyle.Flat && b.FlatAppearance.BorderSize == 1 && b.FlatAppearance.BorderColor == Color.FromArgb(211, 220, 230)), "Job/Pos/Engine/Cam share the action-button font and border");
                Check(bounds.SequenceEqual(buttons.Select(b => b.Bounds)) && texts.SequenceEqual(buttons.Select(b => b.Text)) && !buttons[2].Enabled, "Geometry, labels and disabled state unchanged");
                foreach (var color in new[] { Color.Green, Color.Red, Color.Yellow, Color.Gainsboro })
                {
                    foreach (var button in buttons) button.BackColor = color;
                    Check(buttons.All(b => b.BackColor == color && b.FlatAppearance.MouseOverBackColor == color && b.FlatAppearance.MouseDownBackColor == color), "Runtime color and hover stay synchronized: " + color.Name);
                    foreach (var button in buttons)
                    using (var bitmap = new Bitmap(button.Width, button.Height))
                    {
                        button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        Check(bitmap.GetPixel(12, bitmap.Height / 2).ToArgb() == color.ToArgb(), "Paint preserves status fill, including disabled: " + button.Text + " / " + color.Name);
                        Check(bitmap.GetPixel(0, 0).ToArgb() == form.BackColor.ToArgb(), "Rounded corner exposes parent surface: " + button.Text);
                    }
                }
                form.Show(); Application.DoEvents(); buttons[1].PerformClick();
                Check(clicks == 1, "Original Pos click action remains attached and fires exactly once");
                int[] clickCounts = new int[4];
                for (int i = 0; i < buttons.Length; i++)
                {
                    int index = i;
                    buttons[i].Click += (s, e) => clickCounts[index]++;
                    buttons[i].PerformClick();
                }
                Check(clickCounts.SequenceEqual(new[] { 1, 1, 0, 1 }), "All readout click handlers and disabled behavior retained");
                var actions = new[] { "Reset Count", "&Trigger Manual", "Replay" }.Select((caption, i) =>
                {
                    var button = (Button)Activator.CreateInstance(app.GetType("Aron_V2.InspectionActionButton", true), true);
                    button.Name = i == 0 ? "Reset" : i == 1 ? "btnTrigger" : "btnReplay";
                    button.Text = caption; button.Bounds = new Rectangle(10 + i * 295, 20, 280, 40);
                    return button;
                }).ToArray();
                form.Controls.AddRange(actions);
                app.GetType("Aron_V2.DesktopTheme", true).GetMethod("StyleInspectionActions", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { form, actions });
                for (int i = 0; i < 3; i++)
                {
                    buttons[i].Bounds = new Rectangle(10 + i * 295, 80, 280, 40);
                    buttons[i].BackColor = i == 0 ? Color.Green : Color.Gainsboro;
                    buttons[i].Enabled = true;
                }
                buttons[3].Bounds = new Rectangle(10, 140, 870, 40); buttons[3].BackColor = Color.Gainsboro;
                Directory.CreateDirectory("Tests/UIValidation");
                using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/InspectionReadoutsRounded.png"); }
            }
            Environment.Exit(0);
        }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
}

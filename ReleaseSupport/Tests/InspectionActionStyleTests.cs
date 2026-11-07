using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class InspectionActionStyleTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    [STAThread] static void Main(string[] args)
    {
        try { Run(Assembly.LoadFrom(args[0])); Environment.Exit(0); }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
    static void Run(Assembly app)
    {
        Application.EnableVisualStyles();
        var style = app.GetType("Aron_V2.DesktopTheme", true).GetMethod("StyleInspectionActions", BindingFlags.Static | BindingFlags.NonPublic);
        using (var host = new Form { ShowInTaskbar = false, Opacity = 0, ClientSize = new Size(680, 160), BackColor = Color.White })
        {
            var actionType = app.GetType("Aron_V2.InspectionActionButton", true);
            var reset = (Button)Activator.CreateInstance(actionType, true);
            var trigger = (Button)Activator.CreateInstance(actionType, true);
            var replay = (Button)Activator.CreateInstance(actionType, true);
            reset.Name = "Reset"; reset.Text = "Reset Count"; reset.Bounds = new Rectangle(20, 20, 200, 40); reset.TabIndex = 13;
            trigger.Name = "btnTrigger"; trigger.Text = "&Trigger Manual"; trigger.Bounds = new Rectangle(235, 20, 200, 40); trigger.TabIndex = 2; trigger.Enabled = false;
            replay.Name = "btnReplay"; replay.Text = "Replay"; replay.Bounds = new Rectangle(450, 20, 200, 40); replay.TabIndex = 3;
            var buttons = new[] { reset, trigger, replay };
            var colors = new[] { Color.Red, Color.Yellow, Color.Green };
            var indicators = colors.Select((color, index) => new Button { Text = index == 0 ? "Offline" : index == 1 ? "Waiting" : "Job1", BackColor = color, Bounds = new Rectangle(20 + index * 215, 85, 200, 40), UseVisualStyleBackColor = false }).ToArray();
            host.Controls.AddRange(buttons); host.Controls.AddRange(indicators);
            var bounds = buttons.Select(b => b.Bounds).ToArray();
            var tabs = buttons.Select(b => b.TabIndex).ToArray();
            int resetCount = 0, triggerCount = 0, replayCount = 0;
            reset.Click += (s, e) => resetCount++; trigger.Click += (s, e) => triggerCount++; replay.Click += (s, e) => replayCount++;
            style.Invoke(null, new object[] { host, buttons });
            Check(buttons.All(b => b.FlatStyle == FlatStyle.Flat && !b.UseVisualStyleBackColor) && reset.BackColor == Color.White && replay.BackColor == Color.White && trigger.BackColor == Color.FromArgb(235, 244, 255), "White secondary actions and pale-blue manual trigger");
            Check(buttons.All(b => b.FlatAppearance.MouseOverBackColor == Color.FromArgb(233, 243, 254) && b.FlatAppearance.BorderColor == Color.FromArgb(211, 220, 230)), "Actions share menu-blue hover and neutral borders");
            Check(buttons.All(b => b.Font.Name == "Segoe UI"), "Unified Segoe UI font");
            Check(bounds.SequenceEqual(buttons.Select(b => b.Bounds)) && tabs.SequenceEqual(buttons.Select(b => b.TabIndex)), "Button bounds and keyboard order unchanged");
            Check(!trigger.Enabled && trigger.Text == "&Trigger Manual", "Trigger disabled state and mnemonic retained");
            Check(indicators.Select(b => b.BackColor).SequenceEqual(colors), "Red/yellow/green indicators untouched");
            host.Show(); Application.DoEvents();
            reset.PerformClick(); trigger.PerformClick(); replay.PerformClick();
            Check(resetCount == 1 && triggerCount == 0 && replayCount == 1, "Existing action handlers fire once; disabled trigger cannot fire");
            trigger.Enabled = true; trigger.PerformClick();
            Check(triggerCount == 1, "Existing runtime enable/disable behavior remains available");
            var content = actionType.GetMethod("ContentBounds", BindingFlags.Instance | BindingFlags.NonPublic);
            using (Graphics graphics = host.CreateGraphics())
            {
                foreach (int width in new[] { 120, 160, 220 })
                foreach (string caption in new[] { "Reset Count", "&Trigger Manual", "Replay", "计数清零", "手动触发", "回放" })
                {
                    reset.Width = width; reset.Text = caption;
                    var rectangles = (Rectangle[])content.Invoke(reset, new object[] { graphics });
                    Check(Math.Abs((rectangles[0].Left + rectangles[1].Right) / 2F - width / 2F) <= 0.5F &&
                        Math.Abs(rectangles[0].Top + rectangles[0].Height / 2F - reset.Height / 2F) <= 0.5F &&
                        rectangles[1].Top + rectangles[1].Height / 2F == reset.Height / 2F &&
                        rectangles[0].Right < rectangles[1].Left && rectangles[1].Right <= width,
                        "Centered non-overlapping icon/text: " + width + " / " + caption);
                }
            }
            reset.Bounds = bounds[0]; reset.Text = "Reset Count";
            foreach (Button indicator in indicators) indicator.BackColor = Color.Orange;
            Check(indicators.All(b => b.BackColor == Color.Orange), "Runtime status updates are not overwritten by styling");
            // Render a safe action-only preview; no Cognex editor/device is created.
            for (int i = 0; i < indicators.Length; i++) indicators[i].BackColor = colors[i];
            trigger.Enabled = true;
            Directory.CreateDirectory("Tests/UIValidation");
            using (var bitmap = new Bitmap(host.ClientSize.Width, host.ClientSize.Height))
            { host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/InspectionActions.png"); }
        }
    }
}

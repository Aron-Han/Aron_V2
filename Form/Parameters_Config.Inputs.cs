using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class LimitInputChoice
    {
        internal string Name, TypeName, DefaultValue;
        public override string ToString() => Name + "  (" + TypeName + ")";
    }
    public partial class Parameters_Config
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static List<LimitInputChoice> ReadLimitInputChoices(string root, string job, string camera, string position, IEnumerable<string> existing)
        {
            string path = Path.Combine(root, job, camera, position, "Inspection.vpp");
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            Load_Job.EnsureCognexInteropLoaded();
            object loaded = CogSerializer.LoadObjectFromFile(path);
            using (var tool = loaded as CogToolBlock)
            {
                if (tool == null) { (loaded as IDisposable)?.Dispose(); throw new InvalidOperationException("Expected Inspection.vpp / CogToolBlock: " + path); }
                return GetLimitInputChoices(tool, existing);
            }
        }
        internal static List<LimitInputChoice> GetLimitInputChoices(CogToolBlock tool, IEnumerable<string> existing)
        {
            var exclude = new HashSet<string>(existing ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (string name in new[] { "Input1", "Send_Data", "JobID", "PosID", "SN" }) exclude.Add(name);
            // Legacy VPPs can use different image/data names. The first two slots
            // remain runtime-owned regardless of their names.
            foreach (var terminal in tool.Inputs.Cast<CogToolBlockTerminal>().Take(2)) exclude.Add(terminal.Name);
            return tool.Inputs.Cast<CogToolBlockTerminal>().Where(input => !exclude.Contains(input.Name)).Select(input => {
                string value = string.Empty;
                if (input.Value is string || input.Value is bool || input.Value is IFormattable)
                    value = Convert.ToString(input.Value, CultureInfo.InvariantCulture);
                else if (input.Value == null && input.ValueType.IsValueType)
                    value = input.ValueType == typeof(bool) ? "False" : "0";
                return new LimitInputChoice { Name = input.Name, TypeName = input.ValueType.Name, DefaultValue = value };
            }).ToList();
        }
    }
    internal sealed class LimitInputChoiceForm : Form
    {
        internal readonly ComboBox Selector = new ComboBox { Name = "inputSelector", DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
        internal LimitInputChoice SelectedInput => Selector.SelectedItem as LimitInputChoice;
        internal LimitInputChoiceForm(IEnumerable<LimitInputChoice> choices, string scope)
        {
            Text = "Add parameter"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(22) };
            var source = new Label { Text = scope, AutoSize = true, Margin = new Padding(4, 4, 4, 18) };
            layout.Controls.Add(source, 0, 0); layout.SetColumnSpan(source, 2);
            layout.Controls.Add(new Label { Text = "VPP input:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 4, 16, 4) }, 0, 1);
            layout.Controls.Add(Selector, 1, 1);
            foreach (var choice in choices) Selector.Items.Add(choice);
            var ok = new Button { Text = "OK", AutoSize = true, Enabled = false, Anchor = AnchorStyles.Right, Margin = new Padding(4, 20, 4, 4) };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(4, 20, 4, 4) };
            Selector.SelectedIndexChanged += (s, e) => ok.Enabled = SelectedInput != null;
            ok.Click += (s, e) => { if (SelectedInput != null) { DialogResult = DialogResult.OK; Close(); } };
            layout.Controls.Add(cancel, 0, 2); layout.Controls.Add(ok, 1, 2);
            Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
            DesktopTheme.ApplySettings(this); UiLanguage.ApplyTo(this);
        }
    }
}

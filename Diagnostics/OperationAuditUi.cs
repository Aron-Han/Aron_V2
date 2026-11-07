using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Aron_V2
{
    // Observe only UI activity; never invoke a business handler or vendor method.
    internal sealed class OperationAuditUi : IMessageFilter
    {
        private sealed class State { internal string Before; internal bool Editing; }
        private readonly ConditionalWeakTable<Control, State> _attached = new ConditionalWeakTable<Control, State>();
        private readonly ConditionalWeakTable<ToolStripItem, object> _items = new ConditionalWeakTable<ToolStripItem, object>();
        internal static void Install()
        {
            var instance = new OperationAuditUi();
            Application.AddMessageFilter(instance);
            Application.Idle += instance.OnIdle;
            Application.ApplicationExit += (s,e) => { Application.Idle -= instance.OnIdle; Application.RemoveMessageFilter(instance); };
        }
        private void OnIdle(object sender, EventArgs e)
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray()) Attach(form);
        }
        private static string Window(Control control) { var form = control.FindForm(); return form == null ? "" : form.GetType().Name + " / " + form.Text; }
        private static string Label(Control control)
        {
            string name = string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;
            if (control is ButtonBase || control is Label) name += " / " + control.Text;
            var camera = control.Parent;
            while (camera != null) { if (camera is UserControl_ImageShow) { name = "Camera " + camera.Name + " / " + name; break; } camera = camera.Parent; }
            return name;
        }
        private static string Context(Control control)
        {
            var form=control.FindForm(); if(form==null) return "";
            return string.Join(" / ",Controls(form).OfType<ComboBox>().Where(c=> {
                string name=(c.Name??"").ToLowerInvariant();
                return name.Contains("job")||name.Contains("camera")||name.Contains("position")||name.Contains("channel")||name.Contains("camn")||name.Contains("posid");
            }).Select(c=>c.Name+"="+c.Text));
        }
        private static System.Collections.Generic.IEnumerable<Control> Controls(Control parent)
        { foreach(Control child in parent.Controls) { yield return child; if(!(child is DataGridView)) foreach(var descendant in Controls(child)) yield return descendant; } }
        internal static bool Sensitive(Control control)
        {
            var box = control as TextBox;
            if (box != null && (box.UseSystemPasswordChar || box.PasswordChar != '\0')) return true;
            // Account/authentication windows are intentionally value-free.
            string key = (Window(control) + " " + control.Name).ToLowerInvariant();
            return key.Contains("login") || key.Contains("password") || key.Contains("pwd") || key.Contains("密码") || key.Contains("register") || key.Contains("certification");
        }
        private static string Value(Control c)
        {
            if (Sensitive(c)) return "[protected]";
            if (c is TextBoxBase) return "[text edited]"; // never retain free text or credentials
            if (c is ComboBox || c is NumericUpDown) return c.Text;
            return "";
        }
        internal void Attach(Control control)
        {
            if (control.IsDisposed || _attached.TryGetValue(control, out _)) return;
            var state = new State(); _attached.Add(control, state);
            var form = control as Form;
            if (form != null)
            {
                OperationAudit.Record(Window(form), "Window opened", form.GetType().Name);
                form.FormClosed += (s,e) => OperationAudit.Record(Window(form), "Window closed", e.CloseReason.ToString());
                foreach(string name in new[]{"ConfigSaved","SettingsSaved","CommunicationSettingSaved","S7SettingSaved"})
                {
                    var saved=form.GetType().GetEvent(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                    if(saved!=null && saved.EventHandlerType==typeof(EventHandler))
                        saved.GetAddMethod(true).Invoke(form,new object[]{new EventHandler((s,e)=>OperationAudit.Record(Window(form),"Settings saved",form.GetType().Name))});
                }
            }
            var button=control as ButtonBase;
            if(button!=null) button.Click += (s,e)=>OperationAudit.Record(Window(button),"Action requested",Label(button)+" / "+Context(button)+
                (button is CheckBox ? " / checked="+((CheckBox)button).Checked : button is RadioButton ? " / checked="+((RadioButton)button).Checked : ""));
            control.ControlAdded += (s,e) => Attach(e.Control);
            var combo = control as ComboBox;
            if (combo != null) combo.SelectionChangeCommitted += (s,e) => OperationAudit.Record(Window(combo), "Selection changed", Label(combo) + " = " + Value(combo));
            if (control is TextBoxBase || control is NumericUpDown)
            {
                control.Enter += (s,e) => { state.Before = control.Text; state.Editing = true; };
                control.Leave += (s,e) => {
                    if (state.Editing && state.Before != control.Text)
                        OperationAudit.Record(Window(control), "Field edited", Label(control) + " = " + Value(control));
                    state.Before = null; state.Editing = false;
                };
            }
            var grid = control as DataGridView;
            if (grid != null)
            {
                string before = null;
                grid.CellBeginEdit += (s,e) => before = Convert.ToString(grid[e.ColumnIndex,e.RowIndex].Value);
                grid.CellEndEdit += (s,e) => {
                    string after = Convert.ToString(grid[e.ColumnIndex,e.RowIndex].Value);
                    if (before != after)
                    {
                        bool protectedRow=grid.Rows[e.RowIndex].Cells.Cast<DataGridViewCell>().Any(c=> {
                            string value=Convert.ToString(c.Value).ToLowerInvariant(); return value.Contains("password")||value.Contains("密码")||value.Contains("secret")||value=="pwd";
                        });
                        decimal oldNumber,newNumber;
                        string change=!protectedRow && decimal.TryParse(before,out oldNumber) && decimal.TryParse(after,out newNumber)
                            ? " / "+before+" -> "+after : " [value omitted]";
                        OperationAudit.Record(Window(grid), "Table edited", Label(grid)+" / "+Context(grid)+" / row "+(e.RowIndex+1)+" / "+grid.Columns[e.ColumnIndex].HeaderText+change);
                    }
                    before = null;
                };
                // Do not attach editable grid cells: CellEndEdit is the authoritative event.
                return;
            }
            var strip = control as ToolStrip;
            if (strip != null) { foreach (ToolStripItem item in strip.Items) AttachItem(item); strip.ItemAdded += (s,e) => AttachItem(e.Item); }
            foreach (Control child in control.Controls) Attach(child);
        }
        private void AttachItem(ToolStripItem item)
        {
            if (_items.TryGetValue(item, out _)) return;
            _items.Add(item, new object());
            item.Click += (s,e) => OperationAudit.Record("Menu", "Action requested", item.Name + " / " + item.Text);
            var menu = item as ToolStripMenuItem;
            if (menu != null) { foreach (ToolStripItem child in menu.DropDownItems) AttachItem(child); menu.DropDown.ItemAdded += (s,e) => AttachItem(e.Item); }
        }
        public bool PreFilterMessage(ref Message message)
        {
            try
            {
                // Mouse/key events before dispatch survive modal dialogs and failed commands.
                bool click = message.Msg == 0x201 || message.Msg == 0x203 || message.Msg == 0x204;
                bool key = message.Msg == 0x100;
                if (!click && !key) return false;
                Control control = Control.FromHandle(message.HWnd) ?? Control.FromChildHandle(message.HWnd);
                if (control == null) return false;
                Attach(control.FindForm() ?? control);
                if (control is ToolStrip) return false; // menu Click records keyboard and mouse once
                if (key)
                {
                    Keys code = (Keys)message.WParam.ToInt32();
                    if (Sensitive(control)) return false;
                    bool command = (Control.ModifierKeys & Keys.Control) != 0 && code == Keys.S;
                    bool activate = (control is ButtonBase) && (code == Keys.Space || code == Keys.Enter);
                    if (!command && !activate) return false;
                    OperationAudit.Record(Window(control), "Keyboard command", Label(control) + " / " + (command ? "Ctrl+S" : code.ToString()));
                }
                else OperationAudit.Record(Window(control), "Mouse action", Label(control) + " / " + Context(control) +
                    (message.Msg == 0x204 ? " / right click" : message.Msg == 0x203 ? " / double click" : " / click"));
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Audit observer: " + ex.GetType().Name); }
            return false;
        }
    }
}

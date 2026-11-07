using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class DesktopThemeTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    sealed class DesignerContext : LicenseContext { public override LicenseUsageMode UsageMode { get { return LicenseUsageMode.Designtime; } } }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, Flags).GetValue(obj); }
    static void Expect(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Snapshot(Control control, string name)
    {
        string folder = Path.GetFullPath("Tests/UIValidation"); Directory.CreateDirectory(folder);
        using (var bmp = new Bitmap(control.Width, control.Height))
        { control.DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size)); bmp.Save(Path.Combine(folder, name + ".png")); }
    }
    static void CheckClick(ToolStripItem item, string method)
    {
        object key = typeof(ToolStripItem).GetField("EventClick", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var events = (EventHandlerList)typeof(Component).GetProperty("Events", Flags).GetValue(item, null);
        var handler = events[key];
        Expect(handler != null && handler.GetInvocationList().Any(d => d.Method.Name == method), "Original click handler retained: " + method);
    }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Assembly app = Assembly.LoadFrom(args[0]);
            Type storeType = app.GetType("Aron_V2.Security.UserStore", true);
            var storePath = storeType.GetProperty("FilePath");
            string originalStorePath = (string)storePath.GetValue(null, null);
            string isolatedFile = Path.Combine(Path.GetTempPath(), "Aron-theme-auth-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                storePath.SetValue(null, isolatedFile, null);
                object store = Activator.CreateInstance(storeType);
                object[] add = { store, "theme-test", "test-only-password", "Operator", null };
                Expect((bool)storeType.GetMethod("AddUser").Invoke(null, add), "Isolated test user created without touching site credentials");
                storeType.GetMethod("Save").Invoke(null, new[] { store });
                using (var loginForm = (Form)Activator.CreateInstance(app.GetType("Aron_V2.LoginSimple", true)))
                {
                    var password = (TextBox)Field(loginForm, "txtPwd");
                    password.Text = "wrong-password";
                    loginForm.GetType().GetMethod("DoLogin", Flags).Invoke(loginForm, null);
                    Expect(loginForm.GetType().GetProperty("LoggedInUser").GetValue(loginForm, null) == null, "Wrong password still rejects login");
                    password.Text = "test-only-password";
                    loginForm.GetType().GetMethod("DoLogin", Flags).Invoke(loginForm, null);
                    object user = loginForm.GetType().GetProperty("LoggedInUser").GetValue(loginForm, null);
                    Expect(user != null && loginForm.DialogResult == DialogResult.OK && (string)user.GetType().GetProperty("Role").GetValue(user, null) == "Operator", "Valid login retains original user role");
                }
            }
            finally { storePath.SetValue(null, originalStorePath, null); if (File.Exists(isolatedFile)) File.Delete(isolatedFile); }
            using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2.LoginSimple", true)))
            {
                form.Opacity = 0; form.ShowInTaskbar = false;
                var users = (ComboBox)Field(form, "cboUsers"); users.Items.Clear(); users.Items.Add("admin"); users.SelectedIndex = 0;
                ((TextBox)Field(form, "txtPwd")).Text = "preview-only";
                form.Show(); Application.DoEvents();
                var login = (Button)Field(form, "btnLogin"); var cancel = (Button)Field(form, "btnCancel");
                var change = (Button)Field(form, "btnChangePwd"); var register = (Button)Field(form, "btnRegister"); var delete = (Button)Field(form, "btnDelete");
                Expect(ReferenceEquals(form.AcceptButton, login) && ReferenceEquals(form.CancelButton, cancel), "Enter/Escape default actions preserved");
                Expect(change.Parent == register.Parent && change.Parent == delete.Parent && login.Parent != change.Parent, "Account management actions are separated from Login/Cancel");
                Expect(login.Left > cancel.Left && login.BackColor != cancel.BackColor, "Primary Login is right aligned and visually distinct");
                Expect(((TextBox)Field(form, "txtPwd")).UseSystemPasswordChar, "Password remains masked");
                Snapshot(form, "Login-Actual");
                var message = (Label)Field(form, "lblInfo"); message.Text = "Account or password error"; Application.DoEvents();
                Expect(message.Visible && message.Bottom < change.Parent.Top, "Validation error appears without covering actions");
                Snapshot(form, "Login-Validation-Actual");
            }
            LicenseContext previous = LicenseManager.CurrentContext;
            LicenseManager.CurrentContext = new DesignerContext();
            using (var form = (Form)Activator.CreateInstance(app.GetType("Aron_V2.FormMain", true)))
            {
                form.Opacity = 0; form.ShowInTaskbar = false; form.ClientSize = new Size(1280,720); form.Show(); Application.DoEvents();
                var menu = (MenuStrip)Field(form, "menuStrip1");
                Console.WriteLine("INFO DPI=" + form.DeviceDpi + ", menu=" + menu.Size + ", item=" + menu.Items[0].Size + ", icon=" + menu.Items[0].Image.Size);
                Expect(menu.Items.Count == 5, "All five original top-level menus retained");
                Expect(menu.Items.Cast<ToolStripItem>().All(i => i.Image != null && i.TextImageRelation == TextImageRelation.ImageAboveText), "Menu B uses icons above readable text");
                Expect(menu.Items.Cast<ToolStripItem>().Select(i => i.Width).Distinct().Count() == 1, "Menu buttons have equal widths");
                Expect(menu.Items.Cast<ToolStripItem>().All(i => !i.IsOnOverflow && i.Bounds.Bottom <= menu.Height), "Menu icons/text fit without clipping or overflow at 1280 width");
                var setting = (ToolStripMenuItem)Field(form, "settingToolStripMenuItem");
                Expect(setting.DropDownItems.Count == 6, "Setting submenus are unchanged");
                CheckClick(menu.Items[0], "loginToolStripMenuItem_Click");
                CheckClick(menu.Items[2], "databaseToolStripMenuItem_Click");
                CheckClick(((ToolStripMenuItem)Field(form, "cameraToolStripMenuItem")).DropDownItems[0], "cameraToolStripMenuItem_Click");
                CheckClick((ToolStripItem)Field(form, "communicateToolStripMenuItem"), "communicateToolStripMenuItem_Click");
                setting.Select(); Snapshot(menu, "Menu-B-Actual");
                setting.Enabled = false; Application.DoEvents(); Snapshot(menu, "Menu-B-Disabled-Actual");
                Expect(!setting.Enabled && setting.Image != null, "Permission-disabled menu remains disabled with its icon");
                Expect(!((Timer)Field(form, "_communicationStatusTimer")).Enabled && !(bool)Field(form, "_eventsHooked"), "UI rendering tests do not start communication or runtime events");
                using (var enlarged = new Font("Segoe UI", 16F))
                {
                    Font original = menu.Font;
                    menu.Font = enlarged; Application.DoEvents();
                    Expect(menu.Items.Cast<ToolStripItem>().All(i => i.Height >= i.Image.Height + menu.Font.Height), "Menu accommodates enlarged text without losing icons");
                    menu.Font = original;
                }
            }
            LicenseManager.CurrentContext = previous;
            Console.WriteLine("Desktop theme tests passed; no camera, PLC or Cognex editor was opened.");
            Environment.Exit(0);
        }
        catch (Exception ex) { Console.WriteLine(ex); Environment.Exit(1); }
    }
}

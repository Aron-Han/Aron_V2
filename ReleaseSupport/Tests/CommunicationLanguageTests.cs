using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
class CommunicationLanguageTests
{
    static System.Collections.Generic.IEnumerable<Control> All(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var d in All(child)) yield return d; }
    [STAThread] static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        var language = assembly.GetType("Aron_V2.UiLanguage");
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        Action<string> set = code => language.GetMethod("SetLanguage", flags).Invoke(null, new object[] { code });
        Func<string,string> text = value => (string)language.GetMethod("Text", flags).Invoke(null, new object[] { value });
        using (var form = (Form)Activator.CreateInstance(assembly.GetType("Aron_V2.CommunicationSettingForm")))
        {
            var size = form.ClientSize;
            foreach (var code in new[] { "zh-CN", "en", "zh-CN", "en" })
            {
                set(code);
                language.GetMethod("ApplyTo", flags).Invoke(null, new object[] { form });
                bool zh = code == "zh-CN";
                foreach (var protocol in new[] { "S7", "Profinet", "TCP/IP" })
                {
                    All(form).OfType<RadioButton>().Single(r => r.Text == protocol).Checked = true;
                    if (!All(form).OfType<Button>().Any(b => b.Text == (zh ? "保存并应用" : "Save && Apply"))) throw new Exception("Save caption " + code);
                    if (!All(form).OfType<Button>().Any(b => b.Text == (zh ? "连接设置" : "Connection Setting") && b.Enabled == (protocol != "Profinet"))) throw new Exception("Connection caption/state");
                    if (All(form).OfType<Label>().Any(l => zh && (l.Text.StartsWith("Uses ") || l.Text.StartsWith("Configurable:") || l.Text.StartsWith("Profinet is fixed")))) throw new Exception("Dynamic protocol label untranslated");
                    if (form.ClientSize != size) throw new Exception("Protocol switch resized dialog");
                }
                if (text("Load ") != (zh ? "加载" : "Load ") || text("Pos:") != (zh ? "位置：" : "Pos:")) throw new Exception("Designer caption normalization");
                Console.WriteLine("PASS three protocols, dynamic captions, size and language roundtrip: " + code);
            }
        }
        return 0;
    }
}

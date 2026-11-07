using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
internal static class ParameterPromptLanguageTests
{
    static System.Collections.Generic.IEnumerable<Control> Children(Control parent)
    { foreach (Control child in parent.Controls) { yield return child; foreach (var descendant in Children(child)) yield return descendant; } }
    [STAThread] static int Main(string[] args)
    {
        var app = Assembly.LoadFrom(args[0]);
        var language = app.GetType("Aron_V2.UiLanguage");
        var prompt = app.GetType("Aron_V2.Prompt");
        foreach (string code in new[] { "en", "zh-CN", "en" })
        {
            language.GetMethod("SetLanguage", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { code });
            Exception failure = null;
            using (var timer = new Timer { Interval = 50 })
            {
                timer.Tick += (s, e) => {
                    var dialog = Application.OpenForms.Cast<Form>().FirstOrDefault(); if (dialog == null) return;
                    timer.Stop();
                    try
                    {
                        var controls = Children(dialog).ToArray();
                        if (dialog.Text != (code == "en" ? "Add parameter" : "新增参数") ||
                            !controls.Any(c => c is Label && c.Text == (code == "en" ? "Parameter name:" : "参数名：")) ||
                            !controls.Any(c => c is Button && c.Text == (code == "en" ? "OK" : "确定")) ||
                            !controls.Any(c => c is Button && c.Text == (code == "en" ? "Cancel" : "取消"))) throw new Exception("Incorrect prompt localization: " + code);
                        if (controls.OfType<TextBox>().Single().Text != "Limit_Width") throw new Exception("Parameter value was translated");
                    }
                    catch (Exception error) { failure = error; }
                    finally { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); }
                };
                timer.Start(); prompt.GetMethod("Show").Invoke(null, new object[] { "Parameter name:", "Add parameter", "Limit_Width" });
            }
            if (failure != null) { Console.WriteLine(failure.Message); return 1; }
            Console.WriteLine("PASS Add parameter prompt title/label/OK/Cancel: " + code);
        }
        return 0;
    }
}

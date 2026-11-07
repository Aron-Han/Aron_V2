using System;
using System.Collections;
using System.Linq;
using System.Reflection;
internal static class InspectionPortProbe
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var app = Assembly.LoadFrom(args[0]); object[] report = { null };
            if (!(bool)app.GetType("Aron_V2.VisionProRuntimeAudit").GetMethod("Start", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, report)) throw new Exception((string)report[0]);
            var method = app.GetType("Aron_V2.Load_Job").GetMethod("ReadVpp", BindingFlags.Static | BindingFlags.NonPublic);
            var type = app.GetType("Aron_V2.Load_Job").GetMethod("GetOrLoadInspection").ReturnType;
            Console.WriteLine("CONSTRUCTORS: " + string.Join("; ", type.GetConstructors().Select(c => c.ToString())));
            if (args[1] == "--editor-surface")
            {
                var formType = app.GetType("Aron_V2.Algorithm");
                using (var form = (IDisposable)Activator.CreateInstance(formType, Activator.CreateInstance(app.GetType("Aron_V2.AppConfig"))))
                {
                    formType.GetMethod("EnsureAlgorithmEditor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                    var editor = formType.GetField("cogToolBlockEditV21", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    foreach (var member in editor.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public).Where(m => m.Name.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("File", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Toolbar", StringComparison.OrdinalIgnoreCase) >= 0)) Console.WriteLine(member);
                    InspectControls(editor);
                }
                return 0;
            }
            using (var tool = (IDisposable)method.MakeGenericMethod(type).Invoke(null, new object[] { args[1] }))
                foreach (string direction in new[] { "Inputs", "Outputs" })
                {
                    int index = 0;
                    foreach (var terminal in (IEnumerable)type.GetProperty(direction).GetValue(tool))
                    {
                        var t = terminal.GetType();
                        Console.WriteLine(direction + "[" + index++ + "] " + t.GetProperty("Name").GetValue(terminal) + " : " + t.GetProperty("ValueType").GetValue(terminal));
                    }
                }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void InspectControls(object control)
    {
        var type = control.GetType();
        if (type.Name.IndexOf("ToolBar", StringComparison.OrdinalIgnoreCase) >= 0 || type.Name.IndexOf("ToolStrip", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            Console.WriteLine("BAR: " + type.FullName);
            var buttons = type.GetProperty("Buttons") ?? type.GetProperty("Items");
            if (buttons != null) foreach (var button in (IEnumerable)buttons.GetValue(control))
                Console.WriteLine("BUTTON: " + string.Join(" | ", new[] { "Name", "Text", "ToolTipText", "Tag" }.Select(n => n + "=" + button.GetType().GetProperty(n)?.GetValue(button))));
        }
        var children = type.GetProperty("Controls");
        if (children != null) foreach (var child in (IEnumerable)children.GetValue(control)) InspectControls(child);
    }
}

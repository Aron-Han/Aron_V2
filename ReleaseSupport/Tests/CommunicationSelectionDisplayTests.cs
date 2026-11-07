using Aron_V2;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class CommunicationSelectionDisplayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            foreach (CommunicationProtocol protocol in Enum.GetValues(typeof(CommunicationProtocol)))
            {
                // Executable lives in isolated EditorValidation, not the user's
                // normal Debug folder. Only fixture XML files are written.
                CommunicationConfigHelper.Save(new CommunicationConfig { Protocol = protocol, ChannelCount = 4 });
                using (var form = new FormMain())
                {
                    // Never Show: FormMain.Shown performs real camera validation.
                    var status = (ToolStripStatusLabel)typeof(FormMain).GetField("_communicationStatus", Private).GetValue(form);
                    string expected = "Communicate:" + (protocol == CommunicationProtocol.TcpIp ? "TCP/IP" : protocol.ToString());
                    if (status.Text != expected || status.ForeColor != Color.Red) throw new Exception("Wrong disconnected display: " + status.Text);
                    if (typeof(FormMain).GetField("_communicationService", Private).GetValue(form) != null || (bool)typeof(FormMain).GetField("_cameraValidationComplete", Private).GetValue(form)) throw new Exception("Display initialized devices or communication.");
                    ((Timer)typeof(FormMain).GetField("_communicationStatusTimer", Private).GetValue(form)).Dispose();
                    Console.WriteLine("PASS saved " + protocol + " displays before camera validation; red/disconnected; no service created");
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
}

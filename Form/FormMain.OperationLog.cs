using System;
using System.Windows.Forms;

namespace Aron_V2
{
    public partial class FormMain
    {
        private void InitializeDatabaseMenu()
        {
            var warningTimer=new Timer { Interval=3000 };
            string lastWarning=null;
            warningTimer.Tick += (s,e)=> {
                if(OperationAudit.LastFailure!=null && OperationAudit.LastFailure!=lastWarning)
                    Aron_V2.UI_Update.LogChangeEventArgs.Set("Log",UiLanguage.Text("Operation log write failed:")+" "+OperationAudit.LastFailure,System.Drawing.Color.Orange);
                lastWarning=OperationAudit.LastFailure;
            };
            if(System.ComponentModel.LicenseManager.UsageMode!=System.ComponentModel.LicenseUsageMode.Designtime) warningTimer.Start();
            Disposed += (s,e)=>warningTimer.Dispose();
            databaseToolStripMenuItem.Click -= databaseToolStripMenuItem_Click;
            var database=new ToolStripMenuItem("Database") { Name="inspectionDatabaseMenuItem" };
            var operations=new ToolStripMenuItem("Operation Log") { Name="operationLogMenuItem" };
            database.Click += databaseToolStripMenuItem_Click;
            operations.Click += (s,e)=> { using(var window=new OperationLogForm()) window.ShowDialog(this); };
            databaseToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]{database,operations});
        }
    }
}

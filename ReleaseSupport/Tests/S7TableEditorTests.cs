using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;

internal static class S7TableEditorTests
{
	[STAThread]
	private static int Main(string[] args)
	{
		Assembly app = Assembly.LoadFrom(args[0]);
		Type type = app.GetType("Aron_V2.S7TableEditor", true);
		using (var grid = new DataGridView { AllowUserToAddRows = false })
		{
			foreach (string name in new[] { "ChannelNo", "ParameterName", "DbAddress", "Length", "DataType", "CurrentValue" }) grid.Columns.Add(name, name);
			int count = 4;
			object editor = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { grid, new Func<int>(() => count) }, null);
			Action refresh = () => type.GetMethod("EndLoad").Invoke(editor, null);
			Func<int> errors = () => ((IDictionary)type.GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor)).Count;
			Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); };
			grid.Rows.Add(0, "Trigger Camera", "DB100.16.0", "", "Bool", "");
			grid.Rows.Add(1, "Trigger Camera", "DB100.16.1", "", "Bool", "");
			refresh(); check(errors() == 0, "Distinct Bool bits in the same byte are valid");
			check(grid.Rows[0].Cells["DataType"].ReadOnly, "Fixed handshake signal format is locked");
			grid.Rows[1].Cells["DbAddress"].Value = "DB100.16.0";
			check(errors() == 2, "Duplicate Bool bits mark both address cells");
			grid.Rows.Clear();
			grid.Rows.Add(0, "Result X", "DB101.0", "99", "Float", "");
			grid.Rows.Add(1, "JobID", "DB101.3", "1", "Byte", "");
			refresh(); check(errors() == 2, "Float detects cross-channel overlap at its last byte");
			check(Convert.ToString(grid.Rows[0].Cells["Length"].Value) == "4" && grid.Rows[0].Cells["Length"].ReadOnly, "Float length is fixed and locked");
			grid.Rows[1].Cells["DbAddress"].Value = "DB101.4";
			check(errors() == 0, "Adjacent fields are valid and red state clears");
			grid.Rows[1].Cells["DbAddress"].Value = "DB102.0";
			check(errors() == 0, "Equal offsets in different DBs do not conflict");
			count = 1; refresh(); check(errors() == 1, "Reducing active count flags an existing CH1 row");
			count = 4; refresh(); check(errors() == 0, "Restoring channel count clears the channel warning");
			grid.Rows.Add(-1, "Online", "DB102.8.7", "", "Bool", ""); refresh();
			check(errors() == 0, "System signals accept the global channel -1");
			grid.Rows[0].Cells["DataType"].Value = "String";
			check(!grid.Rows[0].Cells["Length"].ReadOnly, "String length remains editable");
			grid.Rows[0].Cells["Length"].Value = "0";
			check(errors() == 1, "Invalid String length is flagged");
			grid.Rows[1].Cells["DbAddress"].Value = "DB102.1.9";
			check(errors() == 2, "Invalid bit address is flagged");
			check(grid.Columns["DataType"] is DataGridViewComboBoxColumn, "Formats use a dropdown");
			grid.Rows[1].Cells["DbAddress"].Value = "";
			check(errors() == 2, "Blank new-channel address is invalid until configured");
		}
		return 0;
	}
}

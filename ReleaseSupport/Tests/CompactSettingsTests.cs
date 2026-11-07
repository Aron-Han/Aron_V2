using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Aron_V2;

internal static class CompactSettingsTests
{
	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
	private static object Field(object value, string name) { return value.GetType().GetField(name, Flags).GetValue(value); }
	private static void Call(object value, string name, params object[] args) { value.GetType().GetMethod(name, Flags).Invoke(value, args); }
	private static void Expect(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
	private static AppConfig Config()
	{
		var config = new AppConfig { Models = new List<ModelConfig>() };
		for (int j = 1; j <= 2; j++)
		{
			var job = new ModelConfig { Name = "Job" + j, General = new GeneralConfig(), Cameras = new List<CameraConfig>() };
			config.Models.Add(job);
			for (int c = 1; c <= 2; c++)
			{
				var cam = new CameraConfig { Name = "Cam" + c, Positions = new List<PositionConfig>() };
				job.Cameras.Add(cam);
				for (int p = 1; p <= 3; p++)
				{
					var pos = new PositionConfig { Name = "Pos" + p, General = new PositionGeneralConfig { Exposure = "27", MainUsed = "1", MainChannel = "0", SecondUsed = "0", SecondChannel = "0" }, Parameters = new List<ParameterConfig>() };
					cam.Positions.Add(pos);
					for (int n = 1; n <= 8; n++) pos.Parameters.Add(new ParameterConfig { Name = "Limit_Offset_X_Max_" + n, Description = "Inspection tolerance", Value = (n * 0.1).ToString() });
				}
			}
		}
		return config;
	}
	private static void CheckGrid(Form form, DataGridView grid, string name)
	{
		form.ShowInTaskbar = false; form.Opacity = 0; form.Show(); Application.DoEvents(); form.PerformLayout();
		Expect((bool)typeof(DataGridView).GetProperty("DoubleBuffered", Flags).GetValue(grid, null), name + " uses double-buffered painting");
		int width = grid.Columns.Cast<DataGridViewColumn>().Where(x => x.Visible).Sum(x => x.Width);
		int scrollbarWidth = grid.Controls.OfType<VScrollBar>().Where(x => x.Visible).Sum(x => x.Width);
		Expect(grid.ClientSize.Width - grid.RowHeadersWidth - width - scrollbarWidth < 25, name + " columns fill the grid without a large right-hand gap (excluding the DPI-scaled scrollbar)");
		Expect(form.ClientSize.Width <= 1220, name + " opens at a compact width at 150% system DPI");
		Expect(grid.Right <= grid.Parent.ClientSize.Width && grid.Bottom <= grid.Parent.ClientSize.Height, name + " grid stays within its layout cell: " + grid.Bounds + " / " + grid.Parent.ClientSize);
		Directory.CreateDirectory("Tests/UIValidation");
		using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save("Tests/UIValidation/" + name + ".png"); }
	}
	private static void CheckToolbar(Form form)
	{
		var toolbar = (FlowLayoutPanel)form.Controls.Find("AlignedSelectorToolbar", true).Single();
		foreach (float fontSize in new[] { 9F, 12F, 15F })
		using (var font = new Font("Segoe UI", fontSize))
		{
			Font original = form.Font;
			form.Font = font; Application.DoEvents();
			var combo = toolbar.Controls.OfType<ComboBox>().First();
			foreach (Button button in toolbar.Controls.OfType<Button>())
				Expect(button.Height == combo.Height && button.Top >= combo.Top,
					form.Name + " selector/button heights match (wrapping permitted) at font " + fontSize + ": " + button.Text);
			foreach (ComboBox selector in toolbar.Controls.OfType<ComboBox>())
				Expect(selector.Width >= TextRenderer.MeasureText("Cam1", selector.Font).Width + SystemInformation.VerticalScrollBarWidth,
					form.Name + " selector text fits at font " + fontSize);
			form.Font = original; Application.DoEvents();
		}
	}
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static void Run()
	{
		AppConfig config = Config();
		using (var form = new Parameters_Config())
		{
			form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, form.GetType().GetMethod("Parameters_Config_Load", Flags));
			form.GetType().GetField("_config", Flags).SetValue(form, config);
			Call(form, "FillJobCombo", new object[] { null });
			var grid = (DataGridView)Field(form, "dataGridView1");
			CheckGrid(form, grid, "LimitSetting");
			CheckToolbar(form);
			object source = grid.DataSource; DataGridViewColumn column = grid.Columns["Parameter"];
			int resets = 0; ((IBindingList)source).ListChanged += delegate(object sender, ListChangedEventArgs e) { if (e.ListChangedType == ListChangedType.Reset) resets++; };
			((ComboBox)Field(form, "comboCam")).SelectedIndex = 1;
			Expect(resets == 1 && ReferenceEquals(source, grid.DataSource) && ReferenceEquals(column, grid.Columns["Parameter"]), "Limit camera change refreshes once without recreating source or columns");
			resets = 0; ((ComboBox)Field(form, "comboJob")).SelectedIndex = 1;
			Expect(resets == 1, "Limit job change refreshes once rather than cascading multiple reloads");
			Expect(grid.Columns.Count == 6 && ((IBindingList)source).Count == 24, "Limit retains all parameter rows and six editable data fields");
			form.Close();
		}
		using (var form = new FormCamGeneralConfig(config, Path.Combine(Path.GetTempPath(), "unused-ui-test.xml")))
		{
			var grid = (DataGridView)Field(form, "dataGridView1");
			CheckGrid(form, grid, "GeneralConfig");
			CheckToolbar(form);
			Expect(grid.Columns.Count == 7 && !grid.Columns.Cast<DataGridViewColumn>().Any(x => x.DataPropertyName == "Exposure"), "GeneralConfig omits Exposure and keeps seven position/channel fields");
            var count = (Label)Field(form, "lblCamCount");
            var countCaption = (Label)Field(form, "label3");
            var delete = (Button)Field(form, "btnDeleteCam");
            Expect(count.Parent == delete.Parent && count.Parent.Controls.GetChildIndex(countCaption) == count.Parent.Controls.GetChildIndex(delete) + 1, "Camera count follows Delete Camera in the first toolbar");
            Expect(count.Font.Equals(form.Font) && countCaption.Font.Equals(form.Font), "Camera count and caption use the standard control font");
            Expect(Math.Abs(count.Top + count.Height / 2.0 - delete.Top - delete.Height / 2.0) <= 1.5, "Camera count is vertically centered with the adjacent Delete Camera button");
			int resets = 0; ((IBindingList)grid.DataSource).ListChanged += delegate(object sender, ListChangedEventArgs e) { if (e.ListChangedType == ListChangedType.Reset) resets++; };
			((ComboBox)Field(form, "CboCam")).SelectedIndex = 1;
			Expect(resets == 1, "General camera change sends one batched binding reset");
			resets = 0; ((ComboBox)Field(form, "CboJob")).SelectedIndex = 1;
			Expect(resets == 1, "General job change avoids a second camera-triggered refresh");
			Expect(config.Models[0].Cameras[0].Positions[0].General.Exposure == "27", "Opening GeneralConfig preserves legacy exposure metadata");
			form.Close();
		}
	}
	[STAThread]
	private static void Main(string[] args)
	{
		try { Application.EnableVisualStyles(); Assembly.LoadFrom(args[0]); Run(); Environment.Exit(0); }
		catch (Exception ex) { Console.WriteLine("FAIL " + (ex.InnerException ?? ex).Message); Environment.Exit(1); }
	}
}

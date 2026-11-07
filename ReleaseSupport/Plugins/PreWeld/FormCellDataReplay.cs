using Aron_V2.CellInspection;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace Aron_V2
{
	public sealed class FormCellDataReplay : Form
	{
		private readonly CellMapControl _map = new CellMapControl();
		private readonly DataGridView _grid = new DoubleBufferedDataGridView();
		private readonly DataGridView _markGrid = new DoubleBufferedDataGridView();
		private readonly TextBox _details = new TextBox();
		private readonly Label _fileLabel = new Label();
		private readonly Label _summaryLabel = new Label();
		private readonly TextBox _searchBox = new TextBox();
		private readonly NumericUpDown _duplicateDistance = new NumericUpDown();
		private readonly NumericUpDown _rowTolerance = new NumericUpDown();
		private readonly NumericUpDown _heightDuplicateDistance = new NumericUpDown();
		private readonly NumericUpDown _heightRowTolerance = new NumericUpDown();
		private readonly ComboBox _selectionMode = new ComboBox();
		private readonly CheckBox _bottomMinY = new CheckBox();
		private readonly CheckBox _rightMaxX = new CheckBox();
		private readonly CheckBox _requireHeightData = new CheckBox();
		private Button _saveOffsetsButton;
		private readonly ToolTip _toolTip = new ToolTip();
		private readonly SplitContainer _split = new SplitContainer();
		private GroupBox _settingsGroup;
		private readonly BindingList<FinalCellRecord> _rows = new BindingList<FinalCellRecord>();
		private readonly BindingList<FinalMarkRecord> _markRows = new BindingList<FinalMarkRecord>();
		private CellInspectionDocument _document;
		private int _lastSearchRow = -1;
		private bool _selectionSync;

		public FormCellDataReplay()
		{
			Text = "Cell Data Replay";
			StartPosition = FormStartPosition.CenterParent;
			WindowState = FormWindowState.Normal;
			Size = new Size(1250, 780);
			MinimumSize = new Size(1100, 700);
			BuildLayout();
			LoadSettingsToControls();
			Shown += delegate { SetDefaultSplitterDistance(); };
		}

		private void BuildLayout()
		{
			var filePanel = new Panel
			{
				Dock = DockStyle.Top,
				Height = 50,
				Padding = new Padding(10, 9, 10, 7),
				BackColor = Color.WhiteSmoke
			};
			var openButton = NewButton("Open Cell XML...", 175);
			openButton.Dock = DockStyle.Left;
			openButton.Click += delegate { OpenDocument(); };
			_fileLabel.Dock = DockStyle.Fill;
			_fileLabel.Padding = new Padding(12, 5, 4, 0);
			_fileLabel.AutoEllipsis = true;
			_fileLabel.Text = "No complete XML selected.";
			_saveOffsetsButton = NewButton("Save Offsets", 140);
			_saveOffsetsButton.Dock = DockStyle.Right;
			_saveOffsetsButton.Enabled = false;
			_saveOffsetsButton.Click += delegate { SaveCompensationTable(); };
			_toolTip.SetToolTip(_saveOffsetsButton,
				"Save Offset_X/Y/Z by Job and final cell sequence. Automatic summaries use the same table.");
			filePanel.Controls.Add(_fileLabel);
			filePanel.Controls.Add(_saveOffsetsButton);
			filePanel.Controls.Add(openButton);

			_settingsGroup = new GroupBox
			{
				Text = "Automatic Summary and Sorting Rules",
				Dock = DockStyle.Top,
				Height = 118,
				Padding = new Padding(8)
			};
			var settingsFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				WrapContents = true,
				Padding = new Padding(4, 6, 4, 2)
			};
			var saveSettingsButton = NewButton("Save Rules", 105);
			saveSettingsButton.Click += delegate { SaveSettingsFromControls(); };

			_duplicateDistance.DecimalPlaces = 3;
			_duplicateDistance.Maximum = 1000;
			_duplicateDistance.Increment = 0.1M;
			_duplicateDistance.Width = 75;
			_rowTolerance.DecimalPlaces = 3;
			_rowTolerance.Maximum = 1000;
			_rowTolerance.Increment = 0.1M;
			_rowTolerance.Width = 75;
			_heightDuplicateDistance.DecimalPlaces = 3;
			_heightDuplicateDistance.Maximum = 1000;
			_heightDuplicateDistance.Increment = 0.1M;
			_heightDuplicateDistance.Width = 75;
			_heightRowTolerance.DecimalPlaces = 3;
			_heightRowTolerance.Maximum = 1000;
			_heightRowTolerance.Increment = 0.1M;
			_heightRowTolerance.Width = 75;
			_selectionMode.DropDownStyle = ComboBoxStyle.DropDownList;
			_selectionMode.Items.AddRange(new object[] { "Cam1 priority", "Earlier capture priority" });
			_selectionMode.Width = 165;
			_bottomMinY.Text = "Bottom row uses smaller Y";
			_bottomMinY.AutoSize = true;
			_bottomMinY.Margin = new Padding(12, 8, 4, 0);
			_rightMaxX.Text = "Right side uses larger X";
			_rightMaxX.AutoSize = true;
			_rightMaxX.Margin = new Padding(12, 8, 4, 0);
			_requireHeightData.Text = "Require 3D height";
			_requireHeightData.AutoSize = true;
			_requireHeightData.Margin = new Padding(12, 8, 4, 0);

			settingsFlow.Controls.Add(NewSettingPair("2D duplicate distance (mm):", _duplicateDistance));
			settingsFlow.Controls.Add(NewSettingPair("Duplicate priority:", _selectionMode));
			settingsFlow.Controls.Add(NewSettingPair("2D row tolerance (mm):", _rowTolerance));
			settingsFlow.Controls.Add(NewSettingPair("3D duplicate distance (mm):", _heightDuplicateDistance));
			settingsFlow.Controls.Add(NewSettingPair("3D row tolerance (mm):", _heightRowTolerance));
			settingsFlow.Controls.Add(NewSettingControlHost(_bottomMinY));
			settingsFlow.Controls.Add(NewSettingControlHost(_rightMaxX));
			settingsFlow.Controls.Add(NewSettingControlHost(_requireHeightData));
			settingsFlow.Controls.Add(NewSettingControlHost(saveSettingsButton));
			_settingsGroup.Controls.Add(settingsFlow);

			_toolTip.SetToolTip(openButton, "Open a final complete XML and reconstruct every sorted cell and its source.");
			_toolTip.SetToolTip(_duplicateDistance, "Points whose XY distance is within this value are treated as the same cell.");
			_toolTip.SetToolTip(_selectionMode, "Choose which measurement is kept when duplicate points are found.");
			_toolTip.SetToolTip(_rowTolerance, "Maximum Y difference used to place measured points into the same CAD row.");
			_toolTip.SetToolTip(_heightDuplicateDistance, "3D points within this XY distance are treated as the same height Cell before 3D sorting.");
			_toolTip.SetToolTip(_heightRowTolerance, "Maximum 3D Y difference used to place deduplicated height points into the same row.");
			_toolTip.SetToolTip(_bottomMinY, "Checked: the bottom CAD row has smaller Y values. Unchecked: it has larger Y values.");
			_toolTip.SetToolTip(_rightMaxX, "Checked: the right side has larger X values. Unchecked: it has smaller X values.");
			_toolTip.SetToolTip(_requireHeightData, "When checked, automatic summary fails instead of silently generating XML if 3D data is missing or its sorted structure differs from 2D.");
			_toolTip.SetToolTip(saveSettingsButton, "Save these rules for subsequent automatic complete-XML generation. It does not rewrite the currently opened XML.");

			var header = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 5, 10, 3), BackColor = Color.AliceBlue };
			_summaryLabel.Dock = DockStyle.Fill;
			_summaryLabel.Text = "Open a complete XML to view module data.";
			_summaryLabel.Font = new Font(Font, FontStyle.Bold);
			header.Controls.Add(_summaryLabel);

			_split.Dock = DockStyle.Fill;
			_map.Dock = DockStyle.Fill;
			_map.CellSelected += Map_CellSelected;
			_map.MarkSelected += Map_MarkSelected;
			_split.Panel1.Controls.Add(_map);

			_grid.Dock = DockStyle.Fill;
			_grid.AutoGenerateColumns = false;
			_grid.ReadOnly = false;
			_grid.AllowUserToAddRows = false;
			_grid.AllowUserToDeleteRows = false;
			_grid.MultiSelect = false;
			_grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			_grid.RowHeadersVisible = false;
			_grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
			BuildGridColumns();
			_grid.DataSource = _rows;
			_grid.SelectionChanged += Grid_SelectionChanged;
			_grid.CellEndEdit += Grid_CellEndEdit;
			_grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
			{
				e.ThrowException = false;
				MessageBox.Show(this, "Offset must be a valid number.", "Cell Compensation",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
			};

			_markGrid.Dock = DockStyle.Fill;
			_markGrid.AutoGenerateColumns = false;
			_markGrid.ReadOnly = true;
			_markGrid.AllowUserToAddRows = false;
			_markGrid.AllowUserToDeleteRows = false;
			_markGrid.MultiSelect = false;
			_markGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			_markGrid.RowHeadersVisible = false;
			_markGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
			BuildMarkGridColumns();
			_markGrid.DataSource = _markRows;
			_markGrid.SelectionChanged += MarkGrid_SelectionChanged;

			var detailsPanel = new GroupBox { Text = "Selected Point Source Details", Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(6) };
			_details.Dock = DockStyle.Fill;
			_details.Multiline = true;
			_details.ReadOnly = true;
			_details.ScrollBars = ScrollBars.Both;
			_details.WordWrap = false;
			_details.Font = new Font("Consolas", 9F);
			_details.Text = "Select a Mark or Cell in the map or table to see its camera and PLC position source.";
			detailsPanel.Controls.Add(_details);
			var dataGroup = new GroupBox { Text = "Replay Data", Dock = DockStyle.Fill, Padding = new Padding(5) };
			var searchPanel = new TableLayoutPanel
			{
				Dock = DockStyle.Top,
				Height = 42,
				ColumnCount = 3,
				RowCount = 1,
				Padding = new Padding(8, 3, 4, 3),
				Margin = new Padding(0)
			};
			searchPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
			searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
			var searchLabel = new Label
			{
				Text = "Find:",
				AutoSize = true,
				Anchor = AnchorStyles.Right,
				Margin = new Padding(0, 0, 7, 0)
			};
			_searchBox.Width = 130;
			_searchBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
			_searchBox.Margin = new Padding(0);
			var findNext = NewButton("Find Next", 110);
			findNext.Anchor = AnchorStyles.Left;
			findNext.Margin = new Padding(6, 0, 0, 0);
			findNext.Click += delegate { FindNextCell(); };
			_searchBox.KeyDown += delegate(object sender, KeyEventArgs e)
			{
				if (e.KeyCode != Keys.Enter) return;
				e.SuppressKeyPress = true;
				FindNextCell();
			};
			_searchBox.TextChanged += delegate { _lastSearchRow = -1; };
			searchPanel.Controls.Add(searchLabel, 0, 0);
			searchPanel.Controls.Add(_searchBox, 1, 0);
			searchPanel.Controls.Add(findNext, 2, 0);
			_toolTip.SetToolTip(_searchBox, "Search Mark name, final Cell sequence, original 2D/3D VPP sequence, camera, PLC position or capture ID. Use Find Next to cycle through matches.");
			var markGroup = new GroupBox { Text = "Mark Data", Dock = DockStyle.Top, Height = 150, Padding = new Padding(5) };
			markGroup.Controls.Add(_markGrid);
			var cellLabel = new Label
			{
				Text = "Cell Data",
				Dock = DockStyle.Top,
				Height = 24,
				TextAlign = ContentAlignment.MiddleLeft,
				Font = new Font(Font, FontStyle.Bold),
				Padding = new Padding(3, 0, 0, 0)
			};
			dataGroup.Controls.Add(_grid);
			dataGroup.Controls.Add(cellLabel);
			dataGroup.Controls.Add(markGroup);
			dataGroup.Controls.Add(searchPanel);
			_split.Panel2.Controls.Add(dataGroup);
			_split.Panel2.Controls.Add(detailsPanel);

			Controls.Add(_split);
			Controls.Add(header);
			Controls.Add(_settingsGroup);
			Controls.Add(filePanel);
		}

		private void SetDefaultSplitterDistance()
		{
			if (_split.Width <= 0) return;
			_split.Panel1MinSize = 0;
			_split.Panel2MinSize = 0;
			int panel2Minimum = Math.Min(430, Math.Max(320, _split.Width / 3));
			int panel1Minimum = Math.Min(520, Math.Max(420, _split.Width - panel2Minimum - _split.SplitterWidth));
			int desired = _split.Width - Math.Max(panel2Minimum, (int)(_split.Width * 0.40));
			int maximum = _split.Width - panel2Minimum - _split.SplitterWidth;
			_split.SplitterDistance = Math.Max(panel1Minimum, Math.Min(desired, maximum));
			_split.Panel1MinSize = panel1Minimum;
			_split.Panel2MinSize = panel2Minimum;
		}

		private void BuildGridColumns()
		{
			_grid.Columns.Add(TextColumn("Sequence", "Seq", 55));
			_grid.Columns.Add(TextColumn("X", "X", 90));
			_grid.Columns.Add(TextColumn("Y", "Y", 90));
			_grid.Columns.Add(TextColumn("Height", "Height", 90));
			_grid.Columns.Add(OffsetColumn("OffsetX", "Offset_X"));
			_grid.Columns.Add(OffsetColumn("OffsetY", "Offset_Y"));
			_grid.Columns.Add(OffsetColumn("OffsetZ", "Offset_Z"));
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Camera", HeaderText = "2D Camera", Width = 75 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position", HeaderText = "2D Position", Width = 90 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "VppSequence", HeaderText = "2D VPP Seq", Width = 85 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HeightCamera", HeaderText = "3D Camera", Width = 80 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HeightPosition", HeaderText = "3D Position", Width = 90 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HeightVppSequence", HeaderText = "3D VPP Seq", Width = 85 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sources", HeaderText = "All 2D Sources", Width = 190 });
			_grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "HeightSources", HeaderText = "All 3D Sources", Width = 190 });
			foreach (DataGridViewColumn column in _grid.Columns)
			{
				column.ReadOnly = !column.Name.StartsWith("Offset", StringComparison.Ordinal);
				column.SortMode = DataGridViewColumnSortMode.NotSortable;
			}
			_grid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
			{
				if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
				FinalCellRecord cell = _rows[e.RowIndex];
				string name = _grid.Columns[e.ColumnIndex].Name;

				if (name == "Camera") e.Value = cell.SelectedSource == null ? "" : cell.SelectedSource.Camera;
				else if (name == "Position") e.Value = cell.SelectedSource == null ? "" : cell.SelectedSource.PlcPosition;
				else if (name == "VppSequence") e.Value = cell.SelectedSource == null ? "" : cell.SelectedSource.VppSequence.ToString();
				else if (name == "HeightCamera") e.Value = cell.HeightSource == null ? "" : cell.HeightSource.Camera;
				else if (name == "HeightPosition") e.Value = cell.HeightSource == null ? "" : cell.HeightSource.PlcPosition;
				else if (name == "HeightVppSequence") e.Value = cell.HeightSource == null ? "" : cell.HeightSource.VppSequence.ToString();
				else if (name == "Sources")
				{
					var sources = new List<CellPointSource>();
					if (cell.SelectedSource != null) sources.Add(cell.SelectedSource);
					sources.AddRange(cell.DuplicateSources ?? new List<CellPointSource>());
					e.Value = string.Join("; ", sources.Select(source => source.Camera + "/" + source.PlcPosition));
				}
				else if (name == "HeightSources")
				{
					var sources = new List<CellPointSource>();
					if (cell.HeightSource != null) sources.Add(cell.HeightSource);
					sources.AddRange(cell.DuplicateHeightSources ?? new List<CellPointSource>());
					e.Value = string.Join("; ", sources.Select(source => source.Camera + "/" + source.PlcPosition));
				}
			};
		}

		private void BuildMarkGridColumns()
		{
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", Name = "Name", HeaderText = "Mark", FillWeight = 70 });
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "X", Name = "X", HeaderText = "X", FillWeight = 75 });
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Y", Name = "Y", HeaderText = "Y", FillWeight = 75 });
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Z", Name = "Z", HeaderText = "Z", FillWeight = 75 });
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Camera", HeaderText = "Camera", FillWeight = 75 });
			_markGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Position", HeaderText = "Position", FillWeight = 80 });
			foreach (DataGridViewColumn column in _markGrid.Columns)
				column.SortMode = DataGridViewColumnSortMode.NotSortable;
			_markGrid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
			{
				if (e.RowIndex < 0 || e.RowIndex >= _markRows.Count) return;
				FinalMarkRecord mark = _markRows[e.RowIndex];
				string name = _markGrid.Columns[e.ColumnIndex].Name;
				if (name == "Camera") e.Value = mark.Source == null ? "" : mark.Source.Camera;
				else if (name == "Position") e.Value = mark.Source == null ? "" : mark.Source.PlcPosition;

			};
		}

		private static DataGridViewTextBoxColumn OffsetColumn(string property, string header)
		{
			return new DataGridViewTextBoxColumn
			{
				DataPropertyName = property,
				Name = property,
				HeaderText = header,
				Width = 80,
				ReadOnly = false,
				DefaultCellStyle = new DataGridViewCellStyle
				{
					Format = "0.###",
					BackColor = Color.LightYellow
				}
			};
		}

		private static DataGridViewTextBoxColumn TextColumn(string property, string header, int width)
		{
			return new DataGridViewTextBoxColumn { DataPropertyName = property, Name = property, HeaderText = header, Width = width };
		}

		private static Button NewButton(string text, int width)
		{
			return new Button { Text = text, Width = width, Height = 28, Margin = new Padding(4) };
		}

		private static Label NewLabel(string text)
		{
			return new Label { Text = text, AutoSize = true, Margin = new Padding(8, 9, 2, 0) };
		}

		private static Control NewSettingPair(string text, Control editor)
		{
			var label = new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
			editor.Anchor = AnchorStyles.Left;
			editor.Margin = new Padding(6, 0, 0, 0);
			var host = new TableLayoutPanel
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				ColumnCount = 2,
				RowCount = 1,
				Margin = new Padding(3, 1, 3, 1),
				Padding = new Padding(0),
				Height = 34
			};
			host.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
			host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			host.Controls.Add(label, 0, 0);
			host.Controls.Add(editor, 1, 0);
			return host;
		}

		private static Control NewSettingControlHost(Control control)
		{
			control.Anchor = AnchorStyles.None;
			control.Margin = new Padding(3, 0, 3, 0);
			var host = new TableLayoutPanel
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				ColumnCount = 1,
				RowCount = 1,
				Margin = new Padding(3, 1, 3, 1),
				Padding = new Padding(0),
				Height = 34
			};
			host.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
			host.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			host.Controls.Add(control, 0, 0);
			return host;
		}

		private void LoadSettingsToControls()
		{
			CellInspectionSettings settings = CellInspectionService.LoadSettings();
			_duplicateDistance.Value = ClampDecimal(settings.DuplicateDistanceMm, _duplicateDistance.Maximum);
			_rowTolerance.Value = ClampDecimal(settings.RowToleranceMm, _rowTolerance.Maximum);
			_heightDuplicateDistance.Value = ClampDecimal(settings.HeightDuplicateDistanceMm, _heightDuplicateDistance.Maximum);
			_heightRowTolerance.Value = ClampDecimal(settings.HeightRowToleranceMm, _heightRowTolerance.Maximum);
			_selectionMode.SelectedItem = string.Equals(settings.DuplicateSelectionMode, "PreferEarlierCapture", StringComparison.OrdinalIgnoreCase)
				? "Earlier capture priority" : "Cam1 priority";
			_bottomMinY.Checked = settings.BottomIsMinimumY;
			_rightMaxX.Checked = settings.RightIsMaximumX;
			_requireHeightData.Checked = settings.RequireHeightData;
		}

		private static decimal ClampDecimal(double value, decimal maximum)
		{
			if (value < 0) return 0;
			if ((decimal)value > maximum) return maximum;
			return (decimal)value;
		}

		private void SaveSettingsFromControls()
		{
            try { SaveSettingsFromControlsCore(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cell Summary Rules", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void SaveSettingsFromControlsCore()
		{
            if (!PreWeldEnvironment.Host.CanEdit) { MessageBox.Show(this, "Switch Offline with configuration permission before saving."); return; }
			var settings = new CellInspectionSettings
			{
				DuplicateDistanceMm = (double)_duplicateDistance.Value,
				RowToleranceMm = (double)_rowTolerance.Value,
				HeightDuplicateDistanceMm = (double)_heightDuplicateDistance.Value,
				HeightRowToleranceMm = (double)_heightRowTolerance.Value,
				RequireHeightData = _requireHeightData.Checked,
				ExpectedCellCount = 0,
				DuplicateSelectionMode = string.Equals(Convert.ToString(_selectionMode.SelectedItem),
					"Earlier capture priority", StringComparison.OrdinalIgnoreCase)
					? "PreferEarlierCapture" : "PreferCam1",
				BottomIsMinimumY = _bottomMinY.Checked,
				RightIsMaximumX = _rightMaxX.Checked
			};
			CellInspectionService.SaveSettings(settings);
			MessageBox.Show(this,
				"Rules saved. They will be used when the next complete XML is generated.\r\nThe currently opened XML is not changed.",
				"Cell Summary Rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void OpenDocument()
		{
			using (var dialog = new OpenFileDialog())
			{
				dialog.Filter = "Cell XML (*.xml)|*.xml|All files (*.*)|*.*";
				string summaryRoot = Path.Combine(CellInspectionService.RootDirectory, "Summary");
				if (Directory.Exists(summaryRoot)) dialog.InitialDirectory = summaryRoot;
				if (dialog.ShowDialog(this) != DialogResult.OK) return;
				try
				{
					string rootName;
					using (XmlReader reader = XmlReader.Create(dialog.FileName))
					{
						reader.MoveToContent();
						rootName = reader.LocalName;
					}
                    _document = CellInspectionService.LoadDocument(dialog.FileName);
					_lastSearchRow = -1;
					_saveOffsetsButton.Enabled = _document != null && !string.IsNullOrWhiteSpace(_document.Job);
					_fileLabel.Text = dialog.FileName;
					_rows.RaiseListChangedEvents = false;
					_rows.Clear();
					foreach (FinalCellRecord cell in (_document.Cells ?? new List<FinalCellRecord>()).OrderBy(x => x.Sequence)) _rows.Add(cell);
					_rows.RaiseListChangedEvents = true;
					_rows.ResetBindings();
					_markRows.RaiseListChangedEvents = false;
					_markRows.Clear();
					foreach (FinalMarkRecord mark in (_document.Marks ?? new List<FinalMarkRecord>())) _markRows.Add(mark);
					_markRows.RaiseListChangedEvents = true;
					_markRows.ResetBindings();
					_map.SetData(_rows.ToList(), _markRows.ToList(), _document.BottomIsMinimumY, _document.RightIsMaximumX);
                    _summaryLabel.Text = "Cell addressing    Part: " + _document.PartNumber + "    Job: " + _document.Job + "    Cells: " + _document.FinalCellCount + "    Marks: " + _document.MarkPointCount + "    Heights: " + _document.MatchedHeightCount;
					if (_markRows.Count > 0) _markGrid.Rows[0].Selected = true;
					else if (_rows.Count > 0) _grid.Rows[0].Selected = true;
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "The selected file is not a valid Cell XML.\r\n\r\n" + ex.Message,
						"Open XML", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0 || e.RowIndex >= _rows.Count || e.ColumnIndex < 0) return;
			if (!_grid.Columns[e.ColumnIndex].Name.StartsWith("Offset", StringComparison.Ordinal)) return;
			FinalCellRecord cell = _rows[e.RowIndex];
			if (double.IsNaN(cell.OffsetX) || double.IsInfinity(cell.OffsetX) ||
				double.IsNaN(cell.OffsetY) || double.IsInfinity(cell.OffsetY) ||
				double.IsNaN(cell.OffsetZ) || double.IsInfinity(cell.OffsetZ))
			{
				MessageBox.Show(this, "Offset must be a finite number.", "Cell Compensation",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			CellInspectionService.RefreshFinalValues(cell);
			_rows.ResetItem(e.RowIndex);
			_map.Invalidate();
			ShowDetails(cell);
		}



		private void SaveCompensationTable()
		{
			if (_document == null || string.IsNullOrWhiteSpace(_document.Job)) return;
			try
			{
				_grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
				_grid.EndEdit();
                if (!PreWeldEnvironment.Host.CanEdit) throw new InvalidOperationException("Switch Offline with configuration permission before saving.");
                CellInspectionService.SaveCompensations(_document.Job, _rows.Select(cell => new CellCompensationEntry { Sequence = cell.Sequence, OffsetX = cell.OffsetX, OffsetY = cell.OffsetY, OffsetZ = cell.OffsetZ }));
				MessageBox.Show(this,
					"Compensation saved for Job " + _document.Job + ".\r\n" +
                    "Offsets will be applied to future summaries and replay previews; the historical XML is not overwritten.",
					"Cell Compensation", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Save compensation failed:\r\n" + ex.Message,
					"Cell Compensation", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void Grid_SelectionChanged(object sender, EventArgs e)
		{
			if (_selectionSync) return;
			if (_grid.SelectedRows.Count == 0) return;
			FinalCellRecord cell = _grid.SelectedRows[0].DataBoundItem as FinalCellRecord;
			if (cell == null) return;
			_selectionSync = true;
			try
			{
				_markGrid.ClearSelection();
				_map.SelectedMark = null;
				_map.SelectedSequence = cell.Sequence;
				ShowDetails(cell);
			}
			finally { _selectionSync = false; }
		}

		private void MarkGrid_SelectionChanged(object sender, EventArgs e)
		{
			if (_selectionSync || _markGrid.SelectedRows.Count == 0) return;
			FinalMarkRecord mark = _markGrid.SelectedRows[0].DataBoundItem as FinalMarkRecord;
			if (mark == null) return;
			_selectionSync = true;
			try
			{
				_grid.ClearSelection();
				_map.SelectedSequence = 0;
				_map.SelectedMark = mark.Name;
				ShowMarkDetails(mark);
			}
			finally { _selectionSync = false; }
		}

		private void Map_CellSelected(object sender, int sequence)
		{
			_selectionSync = true;
			try
			{
				_markGrid.ClearSelection();
				foreach (DataGridViewRow row in _grid.Rows)
				{
					FinalCellRecord cell = row.DataBoundItem as FinalCellRecord;
					if (cell == null || cell.Sequence != sequence) continue;
					row.Selected = true;
					_grid.CurrentCell = row.Cells[0];
					_map.SelectedMark = null;
					ShowDetails(cell);
					return;
				}
			}
			finally { _selectionSync = false; }
		}

		private void Map_MarkSelected(object sender, string name)
		{
			_selectionSync = true;
			try
			{
				_grid.ClearSelection();
				foreach (DataGridViewRow row in _markGrid.Rows)
				{
					FinalMarkRecord mark = row.DataBoundItem as FinalMarkRecord;
					if (mark == null || !string.Equals(mark.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
					row.Selected = true;
					_markGrid.CurrentCell = row.Cells[0];
					ShowMarkDetails(mark);
					return;
				}
			}
			finally { _selectionSync = false; }
		}

		private void FindNextCell()
		{
			string query = (_searchBox.Text ?? string.Empty).Trim();
			int total = _markRows.Count + _rows.Count;
			if (query.Length == 0 || total == 0) return;

			for (int offset = 1; offset <= total; offset++)
			{
				int combinedIndex = (_lastSearchRow + offset) % total;
				if (combinedIndex < _markRows.Count)
				{
					int markIndex = combinedIndex;
					if (!MarkMatchesSearch(_markRows[markIndex], query)) continue;
					_selectionSync = true;
					try
					{
						_grid.ClearSelection();
						_markGrid.ClearSelection();
						_markGrid.Rows[markIndex].Selected = true;
						_markGrid.CurrentCell = _markGrid.Rows[markIndex].Cells[0];
						_map.SelectedSequence = 0;
						_map.SelectedMark = _markRows[markIndex].Name;
						ShowMarkDetails(_markRows[markIndex]);
						try { _markGrid.FirstDisplayedScrollingRowIndex = markIndex; }
						catch (InvalidOperationException) { }
					}
					finally { _selectionSync = false; }
					_lastSearchRow = combinedIndex;
					return;
				}

				int cellIndex = combinedIndex - _markRows.Count;
				if (!CellMatchesSearch(_rows[cellIndex], query)) continue;
				_selectionSync = true;
				try
				{
					_markGrid.ClearSelection();
					_grid.ClearSelection();
					_grid.Rows[cellIndex].Selected = true;
					_grid.CurrentCell = _grid.Rows[cellIndex].Cells[0];
					_map.SelectedMark = null;
					_map.SelectedSequence = _rows[cellIndex].Sequence;
					ShowDetails(_rows[cellIndex]);
					try { _grid.FirstDisplayedScrollingRowIndex = cellIndex; }
					catch (InvalidOperationException) { }
				}
				finally { _selectionSync = false; }
				_lastSearchRow = combinedIndex;
				return;
			}

			MessageBox.Show(this,
				"No Mark or Cell source matches: " + query,
				"Find Point", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private static bool MarkMatchesSearch(FinalMarkRecord mark, string query)
		{
			if (mark == null) return false;
			if (ContainsIgnoreCase(mark.Name, query)) return true;
			MarkPointSource source = mark.Source;
			return source != null &&
				(ContainsIgnoreCase(source.Camera, query) ||
				 ContainsIgnoreCase(source.PlcPosition, query) ||
				 ContainsIgnoreCase(source.CaptureId, query));
		}

		private static bool CellMatchesSearch(FinalCellRecord cell, string query)
		{
			int sequence;
			bool numericQuery = int.TryParse(query, out sequence);
			if (numericQuery && cell.Sequence == sequence) return true;

			var sources = new List<CellPointSource>();
			if (cell.SelectedSource != null) sources.Add(cell.SelectedSource);
			sources.AddRange(cell.DuplicateSources ?? new List<CellPointSource>());
			if (cell.HeightSource != null) sources.Add(cell.HeightSource);
			sources.AddRange(cell.DuplicateHeightSources ?? new List<CellPointSource>());

			foreach (CellPointSource source in sources)
			{
				if (source == null) continue;
				if (numericQuery && source.VppSequence == sequence) return true;
				if (ContainsIgnoreCase(source.Camera, query) ||
					ContainsIgnoreCase(source.PlcPosition, query) ||
					ContainsIgnoreCase(source.CaptureId, query)) return true;
			}
			return false;
		}

		private static bool ContainsIgnoreCase(string value, string query)
		{
			return !string.IsNullOrEmpty(value) &&
				value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private void ShowDetails(FinalCellRecord cell)
		{

			CellPointSource source = cell.SelectedSource;
			if (source == null) { _details.Clear(); return; }
			var lines = new List<string>
			{
				"Type: Cell",
				"Final sequence: " + cell.Sequence,
				"Final coordinate: X=" + cell.X + ", Y=" + cell.Y,
				"Final height: " + (cell.HeightSource == null ? "NOT MATCHED" : cell.Height),
				"Compensation: Offset_X=" + cell.OffsetX + ", Offset_Y=" + cell.OffsetY + ", Offset_Z=" + cell.OffsetZ,
				"2D measured value: X=" + source.RawX + ", Y=" + source.RawY,
				"Row=" + cell.Row + ", RowPosition=" + cell.RowPosition + ", TriangleBand=" + cell.TriangleBand,
				"Selected: " + source.Camera + " / " + source.PlcPosition + " / Channel " + source.Channel,
				"VPP temporary sequence: " + source.VppSequence,
				"Capture order/time: " + source.CaptureOrder + " / " + source.CaptureTime,
				"Raw XML: " + source.RawXmlPath,
				"Duplicate sources: " + cell.DuplicateCount
			};
			foreach (CellPointSource duplicate in cell.DuplicateSources ?? new List<CellPointSource>())
				lines.Add("  - " + duplicate.Camera + "/" + duplicate.PlcPosition +
					" VPP#" + duplicate.VppSequence + " X=" + duplicate.RawX + " Y=" + duplicate.RawY);
			if (cell.HeightSource != null)
			{
				lines.Add("3D source: " + cell.HeightSource.Camera + "/" + cell.HeightSource.PlcPosition +
					" / Channel " + cell.HeightSource.Channel + " VPP#" + cell.HeightSource.VppSequence);
				lines.Add("3D coordinate: X=" + cell.HeightSource.RawX + ", Y=" + cell.HeightSource.RawY +
					", Z=" + cell.HeightSource.RawZ);
				lines.Add("3D Raw XML: " + cell.HeightSource.RawXmlPath);
				lines.Add("Duplicate 3D sources: " + cell.HeightDuplicateCount);
			}
			_details.Lines = lines.ToArray();
		}

		private void ShowMarkDetails(FinalMarkRecord mark)
		{
			if (mark == null) { _details.Clear(); return; }

			MarkPointSource source = mark.Source;
			var lines = new List<string>
			{
				"Type: Mark",
				"Name: " + mark.Name,
				"Coordinate: X=" + mark.X + ", Y=" + mark.Y + ", Z=" + mark.Z
			};
			if (source != null)
			{
				lines.Add("Source: " + source.Camera + " / " + source.PlcPosition + " / Channel " + source.Channel);
				lines.Add("Capture order/time: " + source.CaptureOrder + " / " + source.CaptureTime);
				lines.Add("Capture ID: " + source.CaptureId);
				lines.Add("Raw XML: " + source.RawXmlPath);
			}
			_details.Lines = lines.ToArray();
		}

	}

	internal sealed class DoubleBufferedDataGridView : DataGridView
	{
		public DoubleBufferedDataGridView()
		{
			DoubleBuffered = true;
			ResizeRedraw = true;
			SetStyle(ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.AllPaintingInWmPaint |
				ControlStyles.UserPaint, true);
			UpdateStyles();
		}

		protected override void OnScroll(ScrollEventArgs e)
		{
			SuspendLayout();
			try { base.OnScroll(e); }
			finally { ResumeLayout(false); }
		}
	}

	internal sealed class CellMapControl : Control
	{
		private List<FinalCellRecord> _cells = new List<FinalCellRecord>();
		private List<FinalMarkRecord> _marks = new List<FinalMarkRecord>();
		private readonly Dictionary<int, PointF> _screenPoints = new Dictionary<int, PointF>();
		private readonly Dictionary<string, PointF> _markScreenPoints =
			new Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase);
		private int _selectedSequence;
		private string _selectedMark;
		private bool _bottomIsMinimumY = true;
		private bool _rightIsMaximumX = true;
		private float _zoom = 1F;
		private float _panX;
		private float _panY;
		private bool _isPanning;
		private Point _lastPanPoint;
		public event EventHandler<int> CellSelected;
		public event EventHandler<string> MarkSelected;

		public CellMapControl()
		{
			DoubleBuffered = true;
			BackColor = Color.Black;
			ForeColor = Color.Lime;
			ResizeRedraw = true;
			TabStop = true;
			SetStyle(ControlStyles.Selectable, true);
		}

		public int SelectedSequence
		{
			get { return _selectedSequence; }
			set
			{
				_selectedSequence = value;
				if (value > 0) _selectedMark = null;
				Invalidate();
			}
		}

		public string SelectedMark
		{
			get { return _selectedMark; }
			set
			{
				_selectedMark = value;
				if (!string.IsNullOrEmpty(value)) _selectedSequence = 0;
				Invalidate();
			}
		}

		public void SetCells(List<FinalCellRecord> cells, bool bottomIsMinimumY, bool rightIsMaximumX)
		{
			SetData(cells, new List<FinalMarkRecord>(), bottomIsMinimumY, rightIsMaximumX);
		}

		public void SetData(List<FinalCellRecord> cells, List<FinalMarkRecord> marks,
			bool bottomIsMinimumY, bool rightIsMaximumX)
		{
			_cells = cells ?? new List<FinalCellRecord>();
			_marks = marks ?? new List<FinalMarkRecord>();
			_bottomIsMinimumY = bottomIsMinimumY;
			_rightIsMaximumX = rightIsMaximumX;
			_selectedMark = _marks.Count == 0 ? null : _marks[0].Name;
			_selectedSequence = _marks.Count > 0 || _cells.Count == 0 ? 0 : _cells[0].Sequence;
			ResetView();
			Invalidate();
		}

		private void ResetView()
		{
			_zoom = 1F;
			_panX = 0F;
			_panY = 0F;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			_screenPoints.Clear();
			_markScreenPoints.Clear();
			if (_cells.Count == 0 && _marks.Count == 0)
			{
				TextRenderer.DrawText(e.Graphics, "Open a complete XML file to reconstruct the Mark and Cell layout.", Font,
					ClientRectangle, Color.LightGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
				return;
			}

			List<double> allX = _cells.Select(GetDisplayX).Concat(_marks.Select(GetDisplayX)).ToList();
			List<double> allY = _cells.Select(GetDisplayY).Concat(_marks.Select(GetDisplayY)).ToList();
			double minX = allX.Min(), maxX = allX.Max();
			double minY = allY.Min(), maxY = allY.Max();
			double rangeX = Math.Max(0.001, maxX - minX), rangeY = Math.Max(0.001, maxY - minY);
			float margin = 28F;
			float scaleX = (ClientSize.Width - margin * 2) / (float)rangeX;
			float scaleY = (ClientSize.Height - margin * 2) / (float)rangeY;
			float scale = Math.Max(0.01F, Math.Min(scaleX, scaleY));
			float usedW = (float)rangeX * scale, usedH = (float)rangeY * scale;
			float offsetX = (ClientSize.Width - usedW) / 2F;
			float offsetY = (ClientSize.Height - usedH) / 2F;
			float nearest = EstimateNearestDistance();
			float radius = Math.Max(3F, Math.Min(18F, nearest * scale * _zoom * 0.40F));
			float centerX = ClientSize.Width / 2F;
			float centerY = ClientSize.Height / 2F;

			using (var normalPen = new Pen(Color.WhiteSmoke, 1F))
			using (var duplicatePen = new Pen(Color.Orange, 1.5F))
			using (var selectedPen = new Pen(Color.Red, 2.5F))
			using (var markPen = new Pen(Color.DeepSkyBlue, 2.5F))
			using (var labelBrush = new SolidBrush(Color.Lime))
			using (var markBrush = new SolidBrush(Color.DeepSkyBlue))
			using (var selectedBrush = new SolidBrush(Color.Yellow))
			{
				foreach (FinalCellRecord cell in _cells)
				{
					float normalizedX = (float)(GetDisplayX(cell) - minX) * scale;
					float normalizedY = (float)(GetDisplayY(cell) - minY) * scale;
					float baseX = offsetX + (_rightIsMaximumX ? normalizedX : usedW - normalizedX);
					float baseY = offsetY + (_bottomIsMinimumY ? usedH - normalizedY : normalizedY);
					float x = centerX + (baseX - centerX) * _zoom + _panX;
					float y = centerY + (baseY - centerY) * _zoom + _panY;
					_screenPoints[cell.Sequence] = new PointF(x, y);
					RectangleF circle = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
					Pen pen = cell.Sequence == _selectedSequence ? selectedPen : (cell.DuplicateCount > 0 ? duplicatePen : normalPen);
					e.Graphics.DrawEllipse(pen, circle);
					if (radius >= 7 || cell.Sequence == _selectedSequence)
					{
						string text = cell.Sequence.ToString();
						SizeF size = e.Graphics.MeasureString(text, Font);
						e.Graphics.DrawString(text, Font, cell.Sequence == _selectedSequence ? selectedBrush : labelBrush,
							x - size.Width / 2F, y - size.Height / 2F);
					}
				}

				float markRadius = Math.Max(7F, Math.Min(22F, radius * 1.55F));
				foreach (FinalMarkRecord mark in _marks)
				{
					float normalizedX = (float)(GetDisplayX(mark) - minX) * scale;
					float normalizedY = (float)(GetDisplayY(mark) - minY) * scale;
					float baseX = offsetX + (_rightIsMaximumX ? normalizedX : usedW - normalizedX);
					float baseY = offsetY + (_bottomIsMinimumY ? usedH - normalizedY : normalizedY);
					float x = centerX + (baseX - centerX) * _zoom + _panX;
					float y = centerY + (baseY - centerY) * _zoom + _panY;
					_markScreenPoints[mark.Name ?? string.Empty] = new PointF(x, y);
					bool selected = string.Equals(mark.Name, _selectedMark, StringComparison.OrdinalIgnoreCase);
					Pen pen = selected ? selectedPen : markPen;
					e.Graphics.DrawEllipse(pen,
						new RectangleF(x - markRadius, y - markRadius, markRadius * 2F, markRadius * 2F));
					string text = mark.Name ?? "Mark";
					SizeF size = e.Graphics.MeasureString(text, Font);
					e.Graphics.DrawString(text, Font, selected ? selectedBrush : markBrush,
						x - size.Width / 2F, y - markRadius - size.Height - 2F);
				}
			}

			string viewHint = "Wheel: zoom   Right drag: pan   Double-click: fit   " +
				Math.Round(_zoom * 100F).ToString() + "%   White: Cell   Orange: duplicate   Blue: Mark   Red: selected";
			TextRenderer.DrawText(e.Graphics, viewHint, Font, new Point(8, 8), Color.LightGray,
				BackColor, TextFormatFlags.NoPadding);
		}

		private float EstimateNearestDistance()
		{
			if (_cells.Count < 2) return 20F;
			double best = double.MaxValue;
			FinalCellRecord first = _cells[0];
			foreach (FinalCellRecord cell in _cells.Skip(1).Take(200))
			{
				double dx = GetDisplayX(first) - GetDisplayX(cell);
				double dy = GetDisplayY(first) - GetDisplayY(cell);
				double distance = Math.Sqrt(dx * dx + dy * dy);
				if (distance > 0.0001 && distance < best) best = distance;
			}
			return best == double.MaxValue ? 20F : (float)best;
		}

		private static double GetDisplayX(FinalCellRecord cell)
		{
			double value;
			return TryParseDisplayNumber(cell == null ? null : cell.X, out value)
				? value : (cell == null || cell.SelectedSource == null ? 0 : cell.SelectedSource.X);
		}

		private static double GetDisplayY(FinalCellRecord cell)
		{
			double value;
			return TryParseDisplayNumber(cell == null ? null : cell.Y, out value)
				? value : (cell == null || cell.SelectedSource == null ? 0 : cell.SelectedSource.Y);
		}

		private static double GetDisplayX(FinalMarkRecord mark)
		{
			double value;
			return TryParseDisplayNumber(mark == null ? null : mark.X, out value) ? value : 0;
		}

		private static double GetDisplayY(FinalMarkRecord mark)
		{
			double value;
			return TryParseDisplayNumber(mark == null ? null : mark.Y, out value) ? value : 0;
		}

		private static bool TryParseDisplayNumber(string text, out double value)
		{
			return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
				double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			Focus();
			if (e.Button == MouseButtons.Right)
			{
				_isPanning = true;
				_lastPanPoint = e.Location;
				Capture = true;
				Cursor = Cursors.Hand;
				return;
			}
			if (e.Button != MouseButtons.Left) return;
			if (_screenPoints.Count == 0 && _markScreenPoints.Count == 0) return;
			var nearestCell = _screenPoints.Select(pair => new
			{
				pair.Key,
				Distance = Math.Pow(pair.Value.X - e.X, 2) + Math.Pow(pair.Value.Y - e.Y, 2)
			}).OrderBy(x => x.Distance).FirstOrDefault();
			var nearestMark = _markScreenPoints.Select(pair => new
			{
				pair.Key,
				Distance = Math.Pow(pair.Value.X - e.X, 2) + Math.Pow(pair.Value.Y - e.Y, 2)
			}).OrderBy(x => x.Distance).FirstOrDefault();
			double cellDistance = nearestCell == null ? double.MaxValue : nearestCell.Distance;
			double markDistance = nearestMark == null ? double.MaxValue : nearestMark.Distance;
			if (Math.Min(cellDistance, markDistance) > 900) return;
			if (markDistance < cellDistance)
			{
				SelectedMark = nearestMark.Key;
				EventHandler<string> markHandler = MarkSelected;
				if (markHandler != null) markHandler(this, nearestMark.Key);
				return;
			}
			SelectedSequence = nearestCell.Key;
			EventHandler<int> handler = CellSelected;
			if (handler != null) handler(this, nearestCell.Key);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (!_isPanning) return;
			_panX += e.X - _lastPanPoint.X;
			_panY += e.Y - _lastPanPoint.Y;
			_lastPanPoint = e.Location;
			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if (e.Button != MouseButtons.Right || !_isPanning) return;
			_isPanning = false;
			Capture = false;
			Cursor = Cursors.Default;
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			base.OnMouseWheel(e);
			if ((_cells.Count == 0 && _marks.Count == 0) || e.Delta == 0) return;
			float oldZoom = _zoom;
			float factor = e.Delta > 0 ? 1.20F : 1F / 1.20F;
			float newZoom = Math.Max(1F, Math.Min(20F, oldZoom * factor));
			if (Math.Abs(newZoom - oldZoom) < 0.0001F) return;

			float centerX = ClientSize.Width / 2F;
			float centerY = ClientSize.Height / 2F;
			float baseRelativeX = (e.X - centerX - _panX) / oldZoom;
			float baseRelativeY = (e.Y - centerY - _panY) / oldZoom;
			_panX = e.X - centerX - baseRelativeX * newZoom;
			_panY = e.Y - centerY - baseRelativeY * newZoom;
			_zoom = newZoom;
			Invalidate();
		}

		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			base.OnMouseDoubleClick(e);
			ResetView();
			Invalidate();
		}
	}
}

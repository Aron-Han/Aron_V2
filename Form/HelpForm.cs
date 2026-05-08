using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Aron_V2
{
    internal sealed class HelpForm : Form
    {
        private readonly List<HelpTopic> _topics = HelpContent.Load();
        private readonly TextBox _search = new TextBox { Name = "HelpSearch", Dock = DockStyle.Fill };
        private readonly TreeView _tree = new TreeView { Name = "HelpTopics", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            HideSelection = false, ShowLines = false, FullRowSelect = true };
        private readonly RichTextBox _reader = new RichTextBox { Name = "HelpReader", Dock = DockStyle.Fill, ReadOnly = true,
            WordWrap = true, DetectUrls = false, BorderStyle = BorderStyle.None, BackColor = Color.White, ScrollBars = RichTextBoxScrollBars.Vertical };
        private readonly Label _heading = new Label { Name = "HelpHeading", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Label _status = new Label { Name = "HelpStatus", Dock = DockStyle.Fill, ForeColor = DesktopTheme.Muted, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Timer _searchDelay = new Timer { Interval = 180 };
        private readonly Font _bodyFont = new Font("Segoe UI", 10F), _headingFont = new Font("Segoe UI", 14F, FontStyle.Bold),
            _sectionFont = new Font("Segoe UI", 11F, FontStyle.Bold), _boldFont = new Font("Segoe UI", 10F, FontStyle.Bold), _codeFont = new Font("Consolas", 9.5F);
        private string _language;

        internal HelpForm(string module)
        {
            float scale = DesktopTheme.DisplayScale;
            Text = "Help"; Name = "HelpWindow"; Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96 * scale, 96 * scale);
            StartPosition = FormStartPosition.CenterParent; BackColor = Color.White; ForeColor = DesktopTheme.Ink;
            ClientSize = new Size((int)(1080 * scale), (int)(700 * scale));
            MinimumSize = new Size((int)(760 * scale), (int)(480 * scale));
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding((int)(14 * scale)) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34 * scale));
            var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, Padding = new Padding(0, 0, 0, (int)(10 * scale)) };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 3; i++) toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var clear = new Button { Name = "HelpClearSearch", Text = "Clear search", AutoSize = true };
            var copy = new Button { Name = "HelpCopyTopic", Text = "Copy topic", AutoSize = true };
            toolbar.Controls.Add(new Label { Text = "Search help:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _search.Anchor = AnchorStyles.Left | AnchorStyles.Right; toolbar.Controls.Add(_search, 1, 0);
            toolbar.Controls.Add(clear, 2, 0); toolbar.Controls.Add(copy, 3, 0);
            toolbar.Controls.Add(new Label { Text = "Build " + BuildInfo.BuildDate, ForeColor = DesktopTheme.Muted, AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
            var split = new SplitContainer { Name = "HelpSplit", Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = Math.Max(5, (int)(5 * scale)),
                Size = new Size((int)(900 * scale), (int)(500 * scale)),
                BackColor = DesktopTheme.Border, Panel1MinSize = (int)(150 * scale), Panel2MinSize = (int)(280 * scale) };
            split.Panel1.BackColor = split.Panel2.BackColor = Color.White; split.Panel1.Controls.Add(_tree);
            _tree.Font = Font; _tree.ItemHeight = (int)(30 * scale); _tree.Indent = (int)(16 * scale);
            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding((int)(16 * scale), 0, 0, 0), BackColor = Color.White };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48 * scale)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _heading.Font = _headingFont; _reader.Font = _bodyFont; right.Controls.Add(_heading, 0, 0); right.Controls.Add(_reader, 0, 1); split.Panel2.Controls.Add(right);
            root.Controls.Add(toolbar, 0, 0); root.Controls.Add(split, 0, 1); root.Controls.Add(_status, 0, 2); Controls.Add(root);
            DesktopTheme.StyleButton(clear); DesktopTheme.StyleButton(copy);
            _search.TextChanged += (s, e) => { _searchDelay.Stop(); _searchDelay.Start(); };
            _searchDelay.Tick += (s, e) => { _searchDelay.Stop(); PopulateTree(); };
            clear.Click += (s, e) => { _search.Text = ""; _searchDelay.Stop(); PopulateTree(); _search.Focus(); };
            copy.Click += (s, e) => { try { if (_reader.TextLength > 0) Clipboard.SetText(_heading.Text + "\r\n\r\n" + _reader.Text); } catch (System.Runtime.InteropServices.ExternalException) { _status.Text = UiLanguage.Text("Clipboard is busy. Please try again."); } };
            _tree.AfterSelect += (s, e) => { if (e.Node.Tag is HelpTopic) Display((HelpTopic)e.Node.Tag); };
            Shown += (s, e) =>
            {
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
                split.SplitterDistance = Math.Min((int)(260 * scale), split.Width - split.Panel2MinSize - split.SplitterWidth);
                SelectModule(module);
            };
            Activated += (s, e) => { if (_language != UiLanguage.Current) PopulateTree(); };
            FormClosed += (s, e) => _searchDelay.Stop();
            Disposed += (s, e) => { _searchDelay.Dispose(); _bodyFont.Dispose(); _headingFont.Dispose(); _sectionFont.Dispose(); _boldFont.Dispose(); _codeFont.Dispose(); Font.Dispose(); };
            PopulateTree(); UiLanguage.ApplyTo(this);
        }
        internal void SelectModule(string module)
        {
            _searchDelay.Stop(); _search.Text = ""; _searchDelay.Stop(); PopulateTree();
            var node = _tree.Nodes.Cast<TreeNode>().FirstOrDefault(n => (string)n.Tag == module);
            if (node != null && node.Nodes.Count > 0) { _tree.SelectedNode = node.Nodes[0]; node.EnsureVisible(); }
        }
        private void PopulateTree()
        {
            string selected = (_tree.SelectedNode?.Tag as HelpTopic)?.Id;
            _language = UiLanguage.Current;
            var matches = _topics.Where(t => t.Matches(_search.Text)).ToList();
            TreeNode preferred = null, first = null;
            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();
                foreach (string module in new[] { "SoftwareOperation", "Communication" })
                {
                    var list = matches.Where(t => t.Module == module).ToList(); if (list.Count == 0) continue;
                    var category = new TreeNode(UiLanguage.Text(module == "SoftwareOperation" ? "Software Operation" : "Communication Help")) { Tag = module };
                    foreach (var topic in list)
                    {
                        var node = new TreeNode(topic.Title) { Tag = topic }; category.Nodes.Add(node);
                        if (first == null) first = node; if (topic.Id == selected) preferred = node;
                    }
                    _tree.Nodes.Add(category); category.Expand();
                }
                _tree.SelectedNode = preferred ?? first;
                if (first == null) { _heading.Text = UiLanguage.Text("No matching help topics"); _reader.Text = UiLanguage.Text("Try a shorter keyword, an error code, a signal name, or clear the search."); }
            }
            finally { _tree.EndUpdate(); }
            _status.Text = (_language == "zh-CN" ? "只读离线帮助 · 中文正文 · 主题数：" : "Read-only offline help · Chinese documentation · Topics: ") + matches.Count;
        }
        private void Display(HelpTopic topic)
        {
            _heading.Text = topic.Title; _reader.Clear();
            foreach (string original in topic.Body.Replace("\r\n", "\n").Split('\n'))
            {
                string line = original;
                _reader.SelectionStart = _reader.TextLength; _reader.SelectionColor = DesktopTheme.Ink;
                _reader.SelectionBackColor = Color.White; _reader.SelectionFont = _bodyFont; _reader.SelectionBullet = false;
                if (line.StartsWith("### ", StringComparison.Ordinal)) { line = line.Substring(4); _reader.SelectionFont = _sectionFont; _reader.SelectionColor = DesktopTheme.Accent; }
                else if (line.StartsWith("- ", StringComparison.Ordinal)) { line = "• " + line.Substring(2); }
                foreach (string piece in Regex.Split(line, "(`[^`]+`|\\*\\*[^*]+\\*\\*)"))
                {
                    Font previous = _reader.SelectionFont;
                    if (piece.StartsWith("`", StringComparison.Ordinal) && piece.EndsWith("`", StringComparison.Ordinal) && piece.Length > 1)
                    { _reader.SelectionFont = _codeFont; _reader.AppendText(piece.Substring(1, piece.Length - 2)); }
                    else if (piece.StartsWith("**", StringComparison.Ordinal) && piece.EndsWith("**", StringComparison.Ordinal) && piece.Length > 3)
                    { _reader.SelectionFont = _boldFont; _reader.AppendText(piece.Substring(2, piece.Length - 4)); }
                    else _reader.AppendText(piece);
                    _reader.SelectionFont = previous;
                }
                _reader.AppendText("\n\n");
            }
            foreach (string word in _search.Text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int start = 0, count = 0;
                while (start < _reader.TextLength && count++ < 100)
                {
                    int index = _reader.Find(word, start, RichTextBoxFinds.None); if (index < 0) break;
                    _reader.Select(index, word.Length); _reader.SelectionBackColor = Color.FromArgb(255, 242, 192); start = index + word.Length;
                }
            }
            _reader.Select(0, 0); _reader.ScrollToCaret();
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { _search.Focus(); _search.SelectAll(); return true; }
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }
    }
}

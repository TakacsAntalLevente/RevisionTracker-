using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RevisionTracker.Core;

namespace RevisionTracker.App
{
    public class ProjectDialog : EditDialog
    {
        private readonly Project _project;
        private readonly TextBox _name, _notes;

        public ProjectDialog(Project project, bool isNew) : base(isNew ? "New project" : "Edit project")
        {
            _project = project;
            _name = AddRow("Project name", new TextBox { Text = project.Name });
            _notes = AddRow("Changes / notes", MultiLine(project.Notes));
            AddNote("Example: \"VW_Series-04_VS\" or \"DTNA MCO1\". One project per release or change.");
        }

        protected override bool ValidateAndApply()
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) { Warn("Please enter a project name."); return false; }
            _project.Name = _name.Text.Trim();
            _project.Notes = _notes.Text;
            return true;
        }
    }

    public class PartDialog : EditDialog
    {
        private readonly Part _part;
        private readonly ComboBox _board, _kind;
        private readonly TextBox _ttn, _variant, _notes;
        private readonly NumericUpDown _rev;
        private readonly CheckBox _addStandard;

        public bool AddStandardDocuments { get { return _addStandard != null && _addStandard.Checked; } }

        public PartDialog(Part part, Categories cats, bool isNew) : base(isNew ? "New board" : "Edit board")
        {
            _part = part;
            _board = AddRow("Board", Combo(cats.Boards, part.Board));
            _kind = AddRow("Type", Combo(cats.PartKinds, part.Kind));
            _ttn = AddRow("TTN", new TextBox { Text = part.Ttn });
            _rev = AddRow("REV", Number(part.Rev));
            _variant = AddRow("Variant", new TextBox { Text = part.Variant });
            _notes = AddRow("Notes", MultiLine(part.Notes));
            AddNote("Variant is used in file names, e.g. C2_VAR04 → Schematic_LogicPCBA_C2_VAR04_…");
            if (isNew)
            {
                _addStandard = new CheckBox { Text = "Add the standard documents for this type (Schematic, eBOM, Gerber…)", Checked = true, AutoSize = true };
                Grid.Controls.Add(_addStandard);
                Grid.SetColumnSpan(_addStandard, 2);
            }
        }

        protected override bool ValidateAndApply()
        {
            if (string.IsNullOrWhiteSpace(_board.Text) || string.IsNullOrWhiteSpace(_kind.Text)) { Warn("Board and type are required."); return false; }
            _part.Board = _board.Text.Trim();
            _part.Kind = _kind.Text.Trim();
            _part.Ttn = _ttn.Text.Trim();
            _part.Rev = (int)_rev.Value;
            _part.Variant = _variant.Text.Trim();
            _part.Notes = _notes.Text;
            return true;
        }
    }

    public class DocumentDialog : EditDialog
    {
        private readonly TrackedDocument _doc;
        private readonly Part _part;
        private readonly Categories _cats;
        private readonly ComboBox _type, _wcType;
        private readonly TextBox _ttn, _tag, _wcName, _suffix, _notes, _file;
        private readonly NumericUpDown _rev;
        private readonly CheckBox _uploaded;
        private readonly Label _preview;
        private readonly bool _isNew;

        /// <summary>For new documents: a file to save as version 1 (may be empty).</summary>
        public string InitialFile { get { return _file == null ? "" : _file.Text.Trim(); } }

        public DocumentDialog(TrackedDocument doc, Part part, Categories cats, bool isNew, string initialFile = null)
            : base(isNew ? "New document" : "Edit document", 520)
        {
            _doc = doc; _part = part; _cats = cats; _isNew = isNew;
            var types = cats.DocTypes.Where(d => string.IsNullOrEmpty(d.AppliesTo) || string.Equals(d.AppliesTo, part.Kind, StringComparison.OrdinalIgnoreCase)).Select(d => d.Name).ToList();
            if (types.Count == 0) types = cats.DocTypes.Select(d => d.Name).ToList();

            _type = AddRow("Document type", Combo(types, isNew && string.IsNullOrEmpty(doc.DocType) ? types.FirstOrDefault() : doc.DocType));
            _ttn = AddRow("TTN", new TextBox { Text = doc.Ttn });
            _rev = AddRow("REV", Number(doc.Rev));
            _tag = AddRow("TAG", new TextBox { Text = doc.Tag });
            _wcType = AddRow("WC type", Combo(cats.WcTypes, doc.WcType));
            _wcName = AddRow("WC name", new TextBox { Text = doc.WcName });
            _suffix = AddRow("Name suffix", new TextBox { Text = doc.Suffix });
            _uploaded = AddRow("Windchill", new CheckBox { Text = "Uploaded to Windchill", Checked = doc.UploadedToWc });
            _notes = AddRow("Notes", MultiLine(doc.Notes));

            if (isNew)
            {
                var filePanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
                filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                _file = new TextBox { Text = initialFile ?? "", Dock = DockStyle.Fill };
                var browse = new Button { Text = "Browse…", AutoSize = true };
                browse.Click += (s, e) => { var f = PickFile(this); if (f != null) _file.Text = f; };
                filePanel.Controls.Add(_file); filePanel.Controls.Add(browse);
                AddRow("First version", filePanel);
            }

            _preview = AddNote("", Theme.Accent);
            _preview.Font = Theme.Bold;

            _type.TextChanged += (s, e) => ApplyTypeDefaults();
            foreach (Control c in new Control[] { _ttn, _rev, _tag, _suffix }) c.TextChanged += (s, e) => UpdatePreview();
            _rev.ValueChanged += (s, e) => UpdatePreview();
            if (isNew) ApplyTypeDefaults();
            UpdatePreview();
        }

        public static string PickFile(IWin32Window owner)
        {
            using (var d = new OpenFileDialog { Title = "Choose file", CheckFileExists = true })
                return d.ShowDialog(owner) == DialogResult.OK ? d.FileName : null;
        }

        private void ApplyTypeDefaults()
        {
            var def = _cats.Find(_type.Text);
            _wcName.Text = FileNaming.WcName(_part, _type.Text);
            if (def == null) { UpdatePreview(); return; }
            _wcType.Text = def.DefaultWcType;
            _suffix.Text = def.DefaultSuffix;
            if (def.UsesPartTtn && (string.IsNullOrWhiteSpace(_ttn.Text) || _isNew))
            {
                _ttn.Text = _part.Ttn;
                _rev.Value = Math.Max(0, Math.Min(999, _part.Rev));
            }
            UpdatePreview();
        }

        private TrackedDocument Snapshot()
        {
            return new TrackedDocument { DocType = _type.Text, Ttn = _ttn.Text, Rev = (int)_rev.Value, Tag = _tag.Text, Suffix = _suffix.Text };
        }

        private void UpdatePreview()
        {
            _preview.Text = "File name: " + FileNaming.StandardName(_part, Snapshot(), _cats);
        }

        protected override bool ValidateAndApply()
        {
            if (string.IsNullOrWhiteSpace(_type.Text)) { Warn("Please choose a document type."); return false; }
            if (_isNew && InitialFile.Length > 0 && !File.Exists(InitialFile)) { Warn("The chosen file does not exist."); return false; }
            _doc.DocType = _type.Text.Trim();
            _doc.Ttn = _ttn.Text.Trim();
            _doc.Rev = (int)_rev.Value;
            _doc.Tag = _tag.Text.Trim();
            _doc.WcType = _wcType.Text.Trim();
            _doc.WcName = _wcName.Text.Trim();
            _doc.Suffix = _suffix.Text.Trim();
            _doc.UploadedToWc = _uploaded.Checked;
            _doc.Notes = _notes.Text;
            return true;
        }
    }

    public class SaveVersionDialog : EditDialog
    {
        private readonly Repository _repo;
        private readonly TrackedDocument _doc;
        private readonly TextBox _file, _tag, _note;
        private readonly NumericUpDown _rev;
        private readonly ComboBox _label;
        private readonly Label _status;

        public string FilePath { get { return _file.Text.Trim(); } }
        public int Rev { get { return (int)_rev.Value; } }
        public string VersionTag { get { return _tag.Text.Trim(); } }
        public string VersionLabel { get { return _label.Text.Trim(); } }
        public string Note { get { return _note.Text; } }

        public SaveVersionDialog(Repository repo, TrackedDocument doc, string file) : base("Save new version — " + doc.DocType, 520)
        {
            _repo = repo; _doc = doc;
            OkButton.Text = "Save version";
            OkButton.Width = 110;

            var filePanel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var last = doc.Latest;
            _file = new TextBox { Dock = DockStyle.Fill, Text = file ?? (last != null && File.Exists(last.OriginalPath) ? last.OriginalPath : "") };
            var browse = new Button { Text = "Browse…", AutoSize = true };
            browse.Click += (s, e) => { var f = DocumentDialog.PickFile(this); if (f != null) _file.Text = f; };
            filePanel.Controls.Add(_file); filePanel.Controls.Add(browse);
            AddRow("File", filePanel);

            var revPanel = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
            _rev = Number(doc.Rev); _rev.Width = 70;
            var bump = new Button { Text = "REV +1", AutoSize = true };
            bump.Click += (s, e) => { if (_rev.Value < _rev.Maximum) _rev.Value++; };
            revPanel.Controls.Add(_rev); revPanel.Controls.Add(bump);
            AddRow("REV", revPanel);
            _tag = AddRow("TAG", new TextBox { Text = doc.Tag });
            _label = AddRow("Label", Combo(repo.Data.Categories.Labels, ""));
            _note = AddRow("What changed?", MultiLine(""));
            _status = AddNote("");
            _file.TextChanged += (s, e) => CheckFile();
            CheckFile();
        }

        private void CheckFile()
        {
            _status.ForeColor = Theme.Muted;
            if (FilePath.Length == 0) { _status.Text = "Choose the file to save, or drag it onto the document list."; return; }
            if (!File.Exists(FilePath)) { _status.Text = "File not found."; _status.ForeColor = Color.Firebrick; return; }
            try
            {
                var same = _repo.FindIdenticalVersion(_doc, FilePath);
                if (same != null)
                {
                    _status.Text = "This file is identical to version " + same.Number + ". Saving again only adds a new entry; it takes no extra space.";
                    _status.ForeColor = Color.DarkOrange;
                }
                else _status.Text = Format.Size(new FileInfo(FilePath).Length) + " — changed since the last saved version.";
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        protected override bool ValidateAndApply()
        {
            if (!File.Exists(FilePath)) { Warn("Please choose an existing file."); return false; }
            return true;
        }
    }

    public class VersionLabelDialog : EditDialog
    {
        private readonly DocVersion _v;
        private readonly ComboBox _label;
        private readonly TextBox _note;

        public VersionLabelDialog(DocVersion v, Categories cats) : base("Label version " + v.Number)
        {
            _v = v;
            _label = AddRow("Label", Combo(cats.Labels, v.Label));
            _note = AddRow("Note", MultiLine(v.Note));
        }

        protected override bool ValidateAndApply()
        {
            _v.Label = _label.Text.Trim();
            _v.Note = _note.Text;
            return true;
        }
    }

    /// <summary>Edit the drop-down lists and document type templates.</summary>
    public class CategoriesDialog : Form
    {
        private readonly Categories _cats;
        private readonly DataGridView _grid;
        private readonly TextBox _wc, _boards, _kinds, _labels;

        public CategoriesDialog(Categories cats)
        {
            _cats = cats;
            Text = "Categories";
            Font = Theme.Base;
            Size = new Size(860, 520);
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var docTab = new TabPage("Document types");
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 28,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None
            };
            _grid.Columns.Add("Name", "Name");
            _grid.Columns.Add("Prefix", "File name prefix");
            var wcCol = new DataGridViewComboBoxColumn { Name = "Wc", HeaderText = "Default WC type", FlatStyle = FlatStyle.Flat };
            wcCol.Items.Add("");
            foreach (var w in cats.WcTypes) wcCol.Items.Add(w);
            foreach (var d in cats.DocTypes) if (!wcCol.Items.Contains(d.DefaultWcType ?? "")) wcCol.Items.Add(d.DefaultWcType);
            _grid.Columns.Add(wcCol);
            _grid.Columns.Add("Suffix", "Default suffix");
            _grid.Columns.Add("Applies", "For type (PCBA/PCB, empty = all)");
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "PartTtn", HeaderText = "Uses board TTN", FillWeight = 60 });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Default", HeaderText = "Add to new boards", FillWeight = 60 });
            foreach (var d in cats.DocTypes)
                _grid.Rows.Add(d.Name, d.FilePrefix, d.DefaultWcType ?? "", d.DefaultSuffix, d.AppliesTo, d.UsesPartTtn, d.AddByDefault);
            docTab.Controls.Add(_grid);
            tabs.TabPages.Add(docTab);

            var listsTab = new TabPage("Lists") { Padding = new Padding(8) };
            var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            for (int i = 0; i < 4; i++) lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            lists.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            lists.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _wc = ListBox(lists, "WC types", cats.WcTypes, 0);
            _boards = ListBox(lists, "Boards", cats.Boards, 1);
            _kinds = ListBox(lists, "Board types", cats.PartKinds, 2);
            _labels = ListBox(lists, "Version labels", cats.Labels, 3);
            listsTab.Controls.Add(lists);
            tabs.TabPages.Add(listsTab);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(8) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            var ok = new Button { Text = "Save", Width = 90 };
            ok.Click += (s, e) => { Apply(); DialogResult = DialogResult.OK; Close(); };
            var reset = new Button { Text = "Reset to defaults", AutoSize = true };
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Replace all categories with the defaults?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var def = Categories.CreateDefault();
                CopyInto(def, _cats);
                DialogResult = DialogResult.OK; Close();
            };
            bottom.Controls.Add(cancel); bottom.Controls.Add(ok); bottom.Controls.Add(reset);
            CancelButton = cancel;

            Controls.Add(tabs);
            Controls.Add(bottom);
        }

        private static TextBox ListBox(TableLayoutPanel host, string title, List<string> items, int col)
        {
            host.Controls.Add(new Label { Text = title + " (one per line)", AutoSize = true, Font = Theme.Bold }, col, 0);
            var t = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = string.Join(Environment.NewLine, items.ToArray()) };
            host.Controls.Add(t, col, 1);
            return t;
        }

        private static List<string> Lines(TextBox t)
        {
            return t.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct().ToList();
        }

        private static void CopyInto(Categories src, Categories dst)
        {
            dst.DocTypes = src.DocTypes; dst.WcTypes = src.WcTypes; dst.Boards = src.Boards; dst.PartKinds = src.PartKinds; dst.Labels = src.Labels;
        }

        private void Apply()
        {
            _grid.EndEdit();
            var types = new List<DocTypeDef>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (r.IsNewRow) continue;
                var name = Convert.ToString(r.Cells["Name"].Value).Trim();
                if (name.Length == 0) continue;
                types.Add(new DocTypeDef
                {
                    Name = name,
                    FilePrefix = Convert.ToString(r.Cells["Prefix"].Value).Trim(),
                    DefaultWcType = Convert.ToString(r.Cells["Wc"].Value).Trim(),
                    DefaultSuffix = Convert.ToString(r.Cells["Suffix"].Value).Trim(),
                    AppliesTo = Convert.ToString(r.Cells["Applies"].Value).Trim(),
                    UsesPartTtn = r.Cells["PartTtn"].Value is bool && (bool)r.Cells["PartTtn"].Value,
                    AddByDefault = r.Cells["Default"].Value is bool && (bool)r.Cells["Default"].Value
                });
            }
            _cats.DocTypes = types;
            _cats.WcTypes = Lines(_wc);
            _cats.Boards = Lines(_boards);
            _cats.PartKinds = Lines(_kinds);
            _cats.Labels = Lines(_labels);
        }
    }
}

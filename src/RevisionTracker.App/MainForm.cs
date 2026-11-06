using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RevisionTracker.Core;

namespace RevisionTracker.App
{
    public class MainForm : Form
    {
        private readonly Repository _repo;

        private TreeView _tree;
        private ListView _docs, _versions;
        private Label _treeHeader, _treeSub, _docsHeader, _docsSub, _timelineHeader, _timelineSub, _emptyHint;
        private ToolStripStatusLabel _statusLeft, _statusRight;
        private ToolStripButton _tbNewPart, _tbNewDoc, _tbSaveVersion;
        private Button _btnSave, _btnOpen, _btnCopy, _btnRestore, _btnLabel, _btnDelete;

        public MainForm(Repository repo)
        {
            _repo = repo;
            Text = Edition.ProductName;
            Font = Theme.Base;
            Size = new Size(1280, 800);
            MinimumSize = new Size(900, 560);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildLayout();
            RefreshTree();
            UpdateState();
        }

        // ================= layout =================

        private void BuildLayout()
        {
            // Menu
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add("Export to Excel (CSV)…", null, (s, e) => ExportCsv());
            file.DropDownItems.Add("Open data folder", null, (s, e) => Process.Start("explorer.exe", _repo.Root));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("E&xit", null, (s, e) => Close());
            var edit = new ToolStripMenuItem("&Edit");
            edit.DropDownItems.Add("New project…", null, (s, e) => NewProject());
            edit.DropDownItems.Add("New board…", null, (s, e) => NewPart());
            edit.DropDownItems.Add("New document…", null, (s, e) => NewDocument(null));
            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add("Edit selected…", null, (s, e) => EditSelected());
            edit.DropDownItems.Add("Delete selected…", null, (s, e) => DeleteSelected());
            var tools = new ToolStripMenuItem("&Tools");
            tools.DropDownItems.Add("Categories…", null, (s, e) => EditCategories());
            var help = new ToolStripMenuItem("&Help");
            help.DropDownItems.Add("About", null, (s, e) => About());
            menu.Items.AddRange(new ToolStripItem[] { file, edit, tools, help });

            // Toolbar
            var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 2, 6, 2), RenderMode = ToolStripRenderMode.System };
            bar.Items.Add(Btn("＋ Project", NewProject));
            _tbNewPart = Btn("＋ Board", NewPart); bar.Items.Add(_tbNewPart);
            _tbNewDoc = Btn("＋ Document", () => NewDocument(null)); bar.Items.Add(_tbNewDoc);
            bar.Items.Add(new ToolStripSeparator());
            _tbSaveVersion = Btn("Save new version…", () => SaveVersion(null)); _tbSaveVersion.Font = Theme.Bold; bar.Items.Add(_tbSaveVersion);
            bar.Items.Add(new ToolStripSeparator());
            bar.Items.Add(Btn("Export to Excel", ExportCsv));
            bar.Items.Add(Btn("Categories", EditCategories));

            // Status bar
            var status = new StatusStrip();
            _statusLeft = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusRight = new ToolStripStatusLabel { ForeColor = Theme.Muted };
            status.Items.Add(_statusLeft); status.Items.Add(_statusRight);

            // Left: project tree
            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, BorderStyle = BorderStyle.None, ShowLines = false, FullRowSelect = true, ItemHeight = 24, BackColor = Theme.Panel };
            _tree.AfterSelect += (s, e) => { RefreshDocs(); UpdateState(); };
            _tree.NodeMouseDoubleClick += (s, e) => EditSelected();
            _tree.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };
            _tree.NodeMouseClick += (s, e) => { if (e.Button == MouseButtons.Right) _tree.SelectedNode = e.Node; };
            var treeMenu = new ContextMenuStrip();
            treeMenu.Items.Add("New board…", null, (s, e) => NewPart());
            treeMenu.Items.Add("New document…", null, (s, e) => NewDocument(null));
            treeMenu.Items.Add("Edit…", null, (s, e) => EditSelected());
            treeMenu.Items.Add("Delete…", null, (s, e) => DeleteSelected());
            _tree.ContextMenuStrip = treeMenu;
            var leftPanel = Section("Projects", null, _tree, out _treeHeader, out _treeSub);

            // Right top: documents
            _docs = MakeList();
            _docs.Columns.Add("File name", 360);
            _docs.Columns.Add("Type", 110);
            _docs.Columns.Add("TTN", 100);
            _docs.Columns.Add("REV", 45);
            _docs.Columns.Add("TAG", 80);
            _docs.Columns.Add("WC type", 85);
            _docs.Columns.Add("WC name", 170);
            _docs.Columns.Add("WC", 35);
            _docs.Columns.Add("Versions", 65);
            _docs.Columns.Add("Last saved", 120);
            _docs.SelectedIndexChanged += (s, e) => { RefreshVersions(); UpdateState(); };
            _docs.DoubleClick += (s, e) => EditDocument();
            _docs.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteDocument(); if (e.KeyCode == Keys.Enter) EditDocument(); };
            _docs.AllowDrop = true;
            _docs.DragEnter += OnDragEnter;
            _docs.DragOver += OnDragOverDocs;
            _docs.DragDrop += OnDropOnDocs;
            var docMenu = new ContextMenuStrip();
            docMenu.Items.Add("Save new version…", null, (s, e) => SaveVersion(null));
            docMenu.Items.Add("Copy file name", null, (s, e) => CopyStandardName());
            docMenu.Items.Add("Edit…", null, (s, e) => EditDocument());
            docMenu.Items.Add("Delete…", null, (s, e) => DeleteDocument());
            _docs.ContextMenuStrip = docMenu;
            _emptyHint = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, BackColor = Color.White, Visible = false };
            var docsHost = new Panel { Dock = DockStyle.Fill };
            docsHost.Controls.Add(_docs); docsHost.Controls.Add(_emptyHint);
            var docsPanel = Section("Documents", null, docsHost, out _docsHeader, out _docsSub);

            // Right bottom: timeline
            _versions = MakeList();
            _versions.Columns.Add("Version", 60);
            _versions.Columns.Add("Saved", 130);
            _versions.Columns.Add("REV", 45);
            _versions.Columns.Add("TAG", 80);
            _versions.Columns.Add("Label", 150);
            _versions.Columns.Add("Original file", 260);
            _versions.Columns.Add("Size", 70);
            _versions.Columns.Add("What changed", 300);
            _versions.SelectedIndexChanged += (s, e) => UpdateState();
            _versions.DoubleClick += (s, e) => OpenVersion();
            _versions.AllowDrop = true;
            _versions.DragEnter += OnDragEnter;
            _versions.DragDrop += (s, e) => { var f = DroppedFile(e); if (f != null && SelectedDoc != null) SaveVersion(f); };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(0, 6, 0, 0), BackColor = Color.White };
            _btnSave = ActionButton("Save new version…", () => SaveVersion(null), true);
            _btnOpen = ActionButton("Open", OpenVersion);
            _btnCopy = ActionButton("Save a copy…", SaveCopy);
            _btnRestore = ActionButton("Restore this version", Restore);
            _btnLabel = ActionButton("Label…", LabelVersion);
            _btnDelete = ActionButton("Delete version", DeleteVersion);
            actions.Controls.AddRange(new Control[] { _btnSave, _btnOpen, _btnCopy, _btnRestore, _btnLabel, _btnDelete });
            var timelineHost = new Panel { Dock = DockStyle.Fill };
            timelineHost.Controls.Add(_versions); timelineHost.Controls.Add(actions);
            var timelinePanel = Section("Timeline", null, timelineHost, out _timelineHeader, out _timelineSub);

            var rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6, BackColor = Color.FromArgb(226, 232, 240) };
            rightSplit.Panel1.Controls.Add(docsPanel);
            rightSplit.Panel2.Controls.Add(timelinePanel);

            var mainSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6, FixedPanel = FixedPanel.Panel1, BackColor = Color.FromArgb(226, 232, 240) };
            mainSplit.Panel1.Controls.Add(leftPanel);
            mainSplit.Panel2.Controls.Add(rightSplit);

            Controls.Add(mainSplit);
            Controls.Add(bar);
            Controls.Add(menu);
            Controls.Add(status);
            MainMenuStrip = menu;

            Load += (s, e) =>
            {
                mainSplit.SplitterDistance = 260;
                rightSplit.SplitterDistance = (int)(rightSplit.Height * 0.55);
            };
        }

        private static ToolStripButton Btn(string text, Action a)
        {
            var b = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, Padding = new Padding(4, 2, 4, 2) };
            b.Click += (s, e) => a();
            return b;
        }

        private static Button ActionButton(string text, Action a, bool primary = false)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.System };
            if (primary) b.Font = Theme.Bold;
            b.Click += (s, e) => a();
            return b;
        }

        private static ListView MakeList()
        {
            return new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false,
                MultiSelect = false, BorderStyle = BorderStyle.None, ShowGroups = true
            };
        }

        private static Panel Section(string title, string sub, Control body, out Label header, out Label subLabel)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10, 8, 10, 6) };
            var top = new Panel { Dock = DockStyle.Top, Height = 46 };
            header = new Label { Text = title, Font = Theme.Header, AutoSize = true, Location = new Point(0, 0) };
            subLabel = new Label { Text = sub ?? "", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(1, 24) };
            top.Controls.Add(header); top.Controls.Add(subLabel);
            panel.Controls.Add(body);
            panel.Controls.Add(top);
            return panel;
        }

        // ================= selection helpers =================

        private Project SelectedProject
        {
            get
            {
                var n = _tree.SelectedNode;
                if (n == null) return null;
                if (n.Tag is Project) return (Project)n.Tag;
                return n.Parent != null ? n.Parent.Tag as Project : null;
            }
        }

        private Part SelectedPart { get { return _tree.SelectedNode != null ? _tree.SelectedNode.Tag as Part : null; } }

        private TrackedDocument SelectedDoc { get { return _docs.SelectedItems.Count == 1 ? _docs.SelectedItems[0].Tag as TrackedDocument : null; } }

        private DocVersion SelectedVersion { get { return _versions.SelectedItems.Count == 1 ? _versions.SelectedItems[0].Tag as DocVersion : null; } }

        /// <summary>Board for new documents: the selected one, or the board of the selected document.</summary>
        private Part TargetPart
        {
            get
            {
                if (SelectedPart != null) return SelectedPart;
                if (SelectedDoc != null) return _repo.FindPartOf(SelectedDoc);
                var p = SelectedProject;
                return p != null && p.Parts.Count == 1 ? p.Parts[0] : null;
            }
        }

        // ================= refresh =================

        private void RefreshTree(object select = null)
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            TreeNode toSelect = null;
            foreach (var p in _repo.Data.Projects.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var pn = new TreeNode(p.Name) { Tag = p, NodeFont = Theme.Bold };
                foreach (var part in p.Parts)
                {
                    var n = new TreeNode(part.DisplayName) { Tag = part };
                    pn.Nodes.Add(n);
                    if (part == select) toSelect = n;
                }
                _tree.Nodes.Add(pn);
                if (p == select) toSelect = pn;
            }
            _tree.ExpandAll();
            _tree.EndUpdate();
            if (toSelect != null) _tree.SelectedNode = toSelect;
            else if (_tree.Nodes.Count > 0 && _tree.SelectedNode == null) _tree.SelectedNode = _tree.Nodes[0];
            RefreshDocs();
        }

        private void RefreshDocs(TrackedDocument select = null)
        {
            if (select == null) select = SelectedDoc;
            _docs.BeginUpdate();
            _docs.Items.Clear();
            _docs.Groups.Clear();
            var project = SelectedProject;
            var parts = SelectedPart != null ? new[] { SelectedPart } : project != null ? project.Parts.ToArray() : new Part[0];
            ListViewItem toSelect = null;
            int count = 0;
            foreach (var part in parts)
            {
                var g = new ListViewGroup(part.DisplayName + (string.IsNullOrEmpty(part.Variant) ? "" : "   ·   " + part.Variant));
                _docs.Groups.Add(g);
                foreach (var d in part.Documents)
                {
                    var latest = d.Latest;
                    var item = new ListViewItem(new[]
                    {
                        FileNaming.StandardName(part, d, _repo.Data.Categories), d.DocType, d.Ttn, d.Rev.ToString("00"), d.Tag,
                        d.WcType, d.WcName, d.UploadedToWc ? "✓" : "", d.Versions.Count == 0 ? "—" : d.Versions.Count.ToString(),
                        latest == null ? "not saved yet" : Format.When(latest.SavedAt)
                    }) { Tag = d, Group = g };
                    if (latest == null) item.ForeColor = Theme.Muted;
                    _docs.Items.Add(item);
                    if (d == select) toSelect = item;
                    count++;
                }
            }
            _docs.EndUpdate();

            if (project == null)
            {
                _docsHeader.Text = "Documents";
                _docsSub.Text = "";
                ShowHint(_repo.Data.Projects.Count == 0
                    ? "No projects yet.\n\nClick \"＋ Project\" to create your first one,\nthen add a board and its documents."
                    : "Select a project on the left.");
            }
            else
            {
                _docsHeader.Text = SelectedPart != null ? project.Name + "  ›  " + SelectedPart.DisplayName : project.Name;
                _docsSub.Text = count + " document" + (count == 1 ? "" : "s") + (string.IsNullOrWhiteSpace(project.Notes) ? "" : "   ·   " + project.Notes.Replace("\r", " ").Replace("\n", " "));
                if (parts.Length == 0) ShowHint("This project has no boards yet.\n\nClick \"＋ Board\" to add a PCBA or PCB.");
                else if (count == 0) ShowHint("No documents yet.\n\nClick \"＋ Document\", or drag a file here.");
                else ShowHint(null);
            }

            if (toSelect != null) { toSelect.Selected = true; toSelect.EnsureVisible(); }
            RefreshVersions();
        }

        private void ShowHint(string text)
        {
            _emptyHint.Text = text ?? "";
            _emptyHint.Visible = text != null;
            if (text != null) _emptyHint.BringToFront();
        }

        private void RefreshVersions(DocVersion select = null)
        {
            _versions.BeginUpdate();
            _versions.Items.Clear();
            var doc = SelectedDoc;
            if (doc != null)
            {
                var latest = doc.Latest;
                foreach (var v in doc.Versions.OrderByDescending(v => v.Number))
                {
                    var item = new ListViewItem(new[]
                    {
                        "v" + v.Number, Format.When(v.SavedAt), v.Rev.ToString("00"), v.Tag, v.Label, v.OriginalFileName,
                        Format.Size(v.Size), (v.Note ?? "").Replace("\r", " ").Replace("\n", " ")
                    }) { Tag = v };
                    if (v == latest) item.Font = Theme.Bold;
                    _versions.Items.Add(item);
                    if (v == select) item.Selected = true;
                }
                if (_versions.SelectedItems.Count == 0 && _versions.Items.Count > 0) _versions.Items[0].Selected = true;
                _timelineHeader.Text = "Timeline — " + doc.DocType;
                _timelineSub.Text = doc.Versions.Count == 0
                    ? "No versions saved yet. Click \"Save new version\" or drag the file here."
                    : doc.Versions.Count + " version" + (doc.Versions.Count == 1 ? "" : "s") + "   ·   latest saved " + Format.When(latest.SavedAt);
            }
            else
            {
                _timelineHeader.Text = "Timeline";
                _timelineSub.Text = "Select a document to see its versions.";
            }
            _versions.EndUpdate();
        }

        private void UpdateState()
        {
            var hasProject = SelectedProject != null;
            var doc = SelectedDoc;
            var v = SelectedVersion;
            _tbNewPart.Enabled = hasProject;
            _tbNewDoc.Enabled = TargetPart != null;
            _tbSaveVersion.Enabled = _btnSave.Enabled = doc != null;
            _btnOpen.Enabled = _btnCopy.Enabled = _btnLabel.Enabled = _btnDelete.Enabled = v != null;
            _btnRestore.Enabled = v != null && !string.IsNullOrEmpty(v.OriginalPath);

            int docs = 0, versions = 0;
            foreach (var d in _repo.Data.Projects.SelectMany(p => p.Parts).SelectMany(p => p.Documents)) { docs++; versions += d.Versions.Count; }
            _statusLeft.Text = string.Format("{0} projects · {1} documents · {2} versions", _repo.Data.Projects.Count, docs, versions);
            _statusRight.Text = Edition.Name + " edition — up to " + Edition.MaxVersionsPerDocument + " versions per document";
        }

        // ================= commands: structure =================

        private void NewProject()
        {
            var p = new Project();
            using (var d = new ProjectDialog(p, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
            }
            _repo.Data.Projects.Add(p);
            Persist();
            RefreshTree(p);
            UpdateState();
        }

        private void NewPart()
        {
            var project = SelectedProject;
            if (project == null) { Info("Select or create a project first."); return; }
            var cats = _repo.Data.Categories;
            var part = new Part
            {
                Board = cats.Boards.FirstOrDefault() ?? "Logic",
                Kind = project.Parts.Count > 0 && project.Parts.Last().Kind == "PCBA" && cats.PartKinds.Contains("PCB") ? "PCB" : cats.PartKinds.FirstOrDefault() ?? "PCBA",
                Variant = project.Parts.Select(x => x.Variant).LastOrDefault(x => !string.IsNullOrEmpty(x)) ?? ""
            };
            using (var d = new PartDialog(part, cats, true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (d.AddStandardDocuments)
                {
                    foreach (var def in cats.DocTypes.Where(t => t.AddByDefault && (string.IsNullOrEmpty(t.AppliesTo) || string.Equals(t.AppliesTo, part.Kind, StringComparison.OrdinalIgnoreCase))))
                    {
                        part.Documents.Add(new TrackedDocument
                        {
                            DocType = def.Name, WcType = def.DefaultWcType, Suffix = def.DefaultSuffix,
                            WcName = FileNaming.WcName(part, def.Name),
                            Ttn = def.UsesPartTtn ? part.Ttn : "", Rev = def.UsesPartTtn ? part.Rev : 1
                        });
                    }
                }
            }
            project.Parts.Add(part);
            Persist();
            RefreshTree(part);
            UpdateState();
        }

        private void NewDocument(string file)
        {
            var part = TargetPart;
            if (part == null) { Info("Select a board (PCBA or PCB) on the left first."); return; }
            var doc = new TrackedDocument();
            using (var d = new DocumentDialog(doc, part, _repo.Data.Categories, true, file))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                part.Documents.Add(doc);
                Persist();
                if (d.InitialFile.Length > 0)
                    Try(() => _repo.AddVersion(doc, d.InitialFile, doc.Rev, doc.Tag, "", "First version"));
            }
            RefreshDocs(doc);
            UpdateState();
        }

        private void EditSelected()
        {
            if (_docs.Focused && SelectedDoc != null) { EditDocument(); return; }
            var part = SelectedPart;
            if (part != null)
            {
                using (var d = new PartDialog(part, _repo.Data.Categories, false))
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                Persist(); RefreshTree(part); return;
            }
            var project = SelectedProject;
            if (project != null)
            {
                using (var d = new ProjectDialog(project, false))
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                Persist(); RefreshTree(project);
            }
        }

        private void EditDocument()
        {
            var doc = SelectedDoc;
            if (doc == null) return;
            using (var d = new DocumentDialog(doc, _repo.FindPartOf(doc), _repo.Data.Categories, false))
                if (d.ShowDialog(this) != DialogResult.OK) return;
            Persist();
            RefreshDocs(doc);
        }

        private void DeleteSelected()
        {
            if (_docs.Focused && SelectedDoc != null) { DeleteDocument(); return; }
            var part = SelectedPart;
            if (part != null)
            {
                var n = part.Documents.Sum(d => d.Versions.Count);
                if (!Confirm("Delete board \"" + part.DisplayName + "\" with " + part.Documents.Count + " documents and " + n + " saved versions?\n\nThis cannot be undone.")) return;
                var proj = _repo.FindProjectOf(part);
                Try(() => _repo.DeletePart(part));
                RefreshTree(proj); UpdateState(); return;
            }
            var project = SelectedProject;
            if (project != null)
            {
                var n = project.Parts.SelectMany(p => p.Documents).Sum(d => d.Versions.Count);
                if (!Confirm("Delete project \"" + project.Name + "\" and all " + n + " saved versions?\n\nThis cannot be undone.")) return;
                Try(() => _repo.DeleteProject(project));
                RefreshTree(); UpdateState();
            }
        }

        private void DeleteDocument()
        {
            var doc = SelectedDoc;
            if (doc == null) return;
            if (!Confirm("Delete document \"" + doc.DocType + "\" and its " + doc.Versions.Count + " saved versions?\n\nThis cannot be undone.")) return;
            Try(() => _repo.DeleteDocument(doc));
            RefreshDocs(); UpdateState();
        }

        private void CopyStandardName()
        {
            var doc = SelectedDoc;
            if (doc == null) return;
            Clipboard.SetText(FileNaming.StandardName(_repo.FindPartOf(doc), doc, _repo.Data.Categories));
            _statusLeft.Text = "File name copied to clipboard.";
        }

        // ================= commands: versions =================

        private void SaveVersion(string file)
        {
            var doc = SelectedDoc;
            if (doc == null) { Info("Select a document first."); return; }
            if (_repo.IsAtVersionLimit(doc))
            {
                Info(string.Format("The {0} edition keeps up to {1} versions per document.\nDelete an old version to save a new one.", Edition.Name, Edition.MaxVersionsPerDocument));
                return;
            }
            using (var d = new SaveVersionDialog(_repo, doc, file))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                DocVersion v = null;
                if (!Try(() => { v = _repo.AddVersion(doc, d.FilePath, d.Rev, d.VersionTag, d.VersionLabel, d.Note); })) return;
                RefreshDocs(doc);
                RefreshVersions(v);
                _statusLeft.Text = "Saved version " + v.Number + " of " + doc.DocType + ".";
            }
        }

        private void OpenVersion()
        {
            var v = SelectedVersion;
            if (v == null) return;
            Try(() =>
            {
                var path = _repo.ExtractForViewing(v);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            });
        }

        private void SaveCopy()
        {
            var v = SelectedVersion;
            var doc = SelectedDoc;
            if (v == null || doc == null) return;
            var ext = Path.GetExtension(v.OriginalFileName);
            using (var d = new SaveFileDialog
            {
                Title = "Save a copy of version " + v.Number,
                FileName = FileNaming.StandardName(_repo.FindPartOf(doc), new TrackedDocument { DocType = doc.DocType, Ttn = doc.Ttn, Rev = v.Rev, Tag = v.Tag, Suffix = doc.Suffix }, _repo.Data.Categories) + ext,
                Filter = string.IsNullOrEmpty(ext) ? "All files|*.*" : ext.TrimStart('.').ToUpperInvariant() + " file|*" + ext + "|All files|*.*"
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (Try(() => _repo.ExportVersion(v, d.FileName))) _statusLeft.Text = "Copy saved to " + d.FileName;
            }
        }

        private void Restore()
        {
            var v = SelectedVersion;
            var doc = SelectedDoc;
            if (v == null || doc == null) return;
            if (!Confirm("Put version " + v.Number + " back at its original location?\n\n" + v.OriginalPath +
                         "\n\nIf the current file has unsaved changes, they are saved as a new version first, so nothing is lost.")) return;
            DocVersion safety = null;
            if (!Try(() => { safety = _repo.RestoreToOriginal(doc, v); })) return;
            RefreshDocs(doc);
            RefreshVersions(v);
            _statusLeft.Text = "Restored version " + v.Number + (safety != null ? " (previous file kept as version " + safety.Number + ")" : "") + ".";
        }

        private void LabelVersion()
        {
            var v = SelectedVersion;
            if (v == null) return;
            using (var d = new VersionLabelDialog(v, _repo.Data.Categories))
                if (d.ShowDialog(this) != DialogResult.OK) return;
            Persist();
            RefreshVersions(v);
        }

        private void DeleteVersion()
        {
            var v = SelectedVersion;
            var doc = SelectedDoc;
            if (v == null || doc == null) return;
            if (!Confirm("Delete version " + v.Number + " (" + Format.When(v.SavedAt) + ")?\n\nThis cannot be undone.")) return;
            Try(() => _repo.DeleteVersion(doc, v));
            RefreshDocs(doc);
            UpdateState();
        }

        // ================= drag & drop =================

        private static string DroppedFile(DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            return files != null && files.Length > 0 && File.Exists(files[0]) ? files[0] : null;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragOverDocs(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Copy;
            var pt = _docs.PointToClient(new Point(e.X, e.Y));
            var item = _docs.GetItemAt(pt.X, pt.Y);
            if (item != null && !item.Selected) item.Selected = true;
        }

        private void OnDropOnDocs(object sender, DragEventArgs e)
        {
            var file = DroppedFile(e);
            if (file == null) return;
            var pt = _docs.PointToClient(new Point(e.X, e.Y));
            var item = _docs.GetItemAt(pt.X, pt.Y);
            if (item != null) { item.Selected = true; SaveVersion(file); }
            else NewDocument(file);
        }

        // ================= misc =================

        private void ExportCsv()
        {
            var projects = SelectedProject != null && Confirm("Export only \"" + SelectedProject.Name + "\"?\n\nChoose No to export all projects.", MessageBoxIcon.Question)
                ? new[] { SelectedProject } : _repo.Data.Projects.ToArray();
            using (var d = new SaveFileDialog { Title = "Export to Excel", Filter = "CSV (opens in Excel)|*.csv", FileName = (projects.Length == 1 ? FileNaming.Sanitize(projects[0].Name) : "RevisionTracker") + ".csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (Try(() => CsvExporter.Export(projects, _repo.Data.Categories, d.FileName)))
                    _statusLeft.Text = "Exported to " + d.FileName;
            }
        }

        private void EditCategories()
        {
            using (var d = new CategoriesDialog(_repo.Data.Categories))
                if (d.ShowDialog(this) != DialogResult.OK) return;
            Persist();
            RefreshDocs();
        }

        private void About()
        {
            MessageBox.Show(this,
                Edition.ProductName + " " + Application.ProductVersion + " — " + Edition.Name + " edition\n\n" +
                "Keeps every version of your documents, so you never need final_v3_REALLY_final again.\n\n" +
                "Data folder:\n" + _repo.Root + "\n\nStored: " + Format.Size(_repo.Objects.TotalStoredBytes()) + " (compressed, duplicates stored once)",
                "About " + Edition.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Persist() { Try(_repo.Save); }

        private bool Try(Action a)
        {
            try { a(); return true; }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Edition.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void Info(string message) { MessageBox.Show(this, message, Edition.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information); }

        private bool Confirm(string message, MessageBoxIcon icon = MessageBoxIcon.Warning)
        {
            return MessageBox.Show(this, message, Edition.ProductName, MessageBoxButtons.YesNo, icon) == DialogResult.Yes;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ItgManiaManager
{
    public partial class MainWindow : Form
    {
        // --- UI ---
        private MenuStrip menuStrip1;
        private ToolStrip toolStrip1;
        private StatusStrip statusStrip1;

        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel statusPath;

        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem apriCartellaToolStripMenuItem;
        private ToolStripMenuItem recentiToolStripMenuItem;
        private ToolStripMenuItem esciToolStripMenuItem;

        private ToolStripButton btnApri;
        private ToolStripButton btnRefresh;
        private ToolStripButton btnRimuovi;

        private SplitContainer splitContainer1;
        private TreeView Pacchi_tree;

        private Panel detailsPanel;
        private Label lblTitolo;
        private TextBox txtDettagli;

        // --- State ---
        private const int MaxRecenti = 10;
        private TreeNode _placeHolder;
        private string? _currentRootPath;

        public MainWindow()
        {
            InitializeComponent();

            AddPlaceholder();
            BuildRecentiMenu();
        }

        // -----------------------------
        // Placeholder
        // -----------------------------
        private void AddPlaceholder()
        {
            Pacchi_tree.Nodes.Clear();
            _placeHolder = new TreeNode("⬇ Trascina qui un pacco (cartella) oppure File → Apri cartella…")
            {
                ForeColor = Color.Gray,
                Tag = null
            };
            Pacchi_tree.Nodes.Add(_placeHolder);
            Pacchi_tree.ExpandAll();
        }

        private bool IsPlaceholderOnly()
            => Pacchi_tree.Nodes.Count == 1 && ReferenceEquals(Pacchi_tree.Nodes[0], _placeHolder);

        // -----------------------------
        // Drag & Drop (da Explorer)
        // -----------------------------
        private void Pacchi_tree_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                if (paths.Any(Directory.Exists))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void Pacchi_tree_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null) return;

            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var folder = dropped.FirstOrDefault(Directory.Exists);
            if (folder == null) return;

            LoadFolder(folder, addToRecenti: true);
        }

        // -----------------------------
        // Tree selection -> Details
        // -----------------------------
        private void Pacchi_tree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            var fullPath = e.Node.Tag as string;
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                txtDettagli.Text = "";
                statusPath.Text = "";
                return;
            }

            statusPath.Text = fullPath;

            if (File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                txtDettagli.Text =
                    $"FILE\n" +
                    $"Nome: {fi.Name}\n" +
                    $"Path: {fi.FullName}\n" +
                    $"Size: {fi.Length:n0} bytes\n" +
                    $"Modificato: {fi.LastWriteTime}\n";
            }
            else if (Directory.Exists(fullPath))
            {
                var di = new DirectoryInfo(fullPath);
                txtDettagli.Text =
                    $"CARTELLA\n" +
                    $"Nome: {di.Name}\n" +
                    $"Path: {di.FullName}\n" +
                    $"Modificato: {di.LastWriteTime}\n";
            }
        }

        // -----------------------------
        // Load folder into TreeView
        // -----------------------------
        private void LoadFolder(string rootPath, bool addToRecenti)
        {
            _currentRootPath = rootPath;

            Pacchi_tree.BeginUpdate();
            try
            {
                Pacchi_tree.Nodes.Clear();

                var rootNode = new TreeNode(Path.GetFileName(rootPath))
                {
                    Tag = rootPath
                };

                Pacchi_tree.Nodes.Add(rootNode);
                AddDirectoryToNode(rootNode, rootPath);

                rootNode.Expand();
                statusLabel.Text = "Caricato";
                statusPath.Text = rootPath;
                lblTitolo.Text = $"Dettagli — {Path.GetFileName(rootPath)}";
            }
            finally
            {
                Pacchi_tree.EndUpdate();
            }

            if (addToRecenti)
                AddToRecenti(rootPath);
        }

        private void AddDirectoryToNode(TreeNode parent, string dirPath)
        {
            // Cartelle
            foreach (var dir in Directory.GetDirectories(dirPath))
            {
                var dirNode = new TreeNode(Path.GetFileName(dir))
                {
                    Tag = dir
                };
                parent.Nodes.Add(dirNode);
                AddDirectoryToNode(dirNode, dir);
            }

            // File
            foreach (var file in Directory.GetFiles(dirPath))
            {
                var fileNode = new TreeNode(Path.GetFileName(file))
                {
                    Tag = file
                };
                parent.Nodes.Add(fileNode);
            }
        }

        // -----------------------------
        // Menu / Toolbar actions
        // -----------------------------
        private void ApriCartella()
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                LoadFolder(dialog.SelectedPath, addToRecenti: true);
        }

        private void RefreshCorrente()
        {
            if (!string.IsNullOrWhiteSpace(_currentRootPath) && Directory.Exists(_currentRootPath))
                LoadFolder(_currentRootPath, addToRecenti: false);
            else
                AddPlaceholder();
        }

        private void RimuoviSelezione()
        {
            var node = Pacchi_tree.SelectedNode;
            if (node == null) return;
            if (node.Parent == null)
            {
                // root
                AddPlaceholder();
                _currentRootPath = null;
                txtDettagli.Text = "";
                statusLabel.Text = "Vuoto";
                statusPath.Text = "";
                return;
            }

            node.Remove();
        }

        // -----------------------------
        // Recenti (persistenti)
        // -----------------------------
        private List<string> GetRecenti()
        {
            // Usa Settings se esiste, altrimenti fallback in memoria.
            // Per usarlo davvero: Project -> Properties -> Settings:
            // Nome: RecentFolders  Tipo: System.Collections.Specialized.StringCollection  Scope: User
            var sc = Properties.Settings.Default.RecentFolders;
            if (sc == null) return new List<string>();
            return sc.Cast<string>().Where(Directory.Exists).ToList();
        }

        private void SaveRecenti(IEnumerable<string> paths)
        {
            var sc = Properties.Settings.Default.RecentFolders;
            if (sc == null)
            {
                sc = new System.Collections.Specialized.StringCollection();
                Properties.Settings.Default.RecentFolders = sc;
            }

            sc.Clear();
            foreach (var p in paths) sc.Add(p);

            Properties.Settings.Default.Save();
        }

        private void AddToRecenti(string path)
        {
            var list = GetRecenti();
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            list = list.Take(MaxRecenti).ToList();

            SaveRecenti(list);
            BuildRecentiMenu();
        }

        private void BuildRecentiMenu()
        {
            recentiToolStripMenuItem.DropDownItems.Clear();

            var rec = GetRecenti();
            if (rec.Count == 0)
            {
                var empty = new ToolStripMenuItem("(nessun recente)") { Enabled = false };
                recentiToolStripMenuItem.DropDownItems.Add(empty);
                return;
            }

            foreach (var path in rec)
            {
                var item = new ToolStripMenuItem(path);
                item.Click += (_, __) => LoadFolder(path, addToRecenti: false);
                recentiToolStripMenuItem.DropDownItems.Add(item);
            }

            recentiToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());

            var clear = new ToolStripMenuItem("Svuota recenti");
            clear.Click += (_, __) =>
            {
                SaveRecenti(Array.Empty<string>());
                BuildRecentiMenu();
            };
            recentiToolStripMenuItem.DropDownItems.Add(clear);
        }

        // -----------------------------
        // InitializeComponent (Layout completo)
        // -----------------------------
        private void InitializeComponent()
        {
            // Form
            this.Text = "ItgManiaManager";
            this.ClientSize = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // MenuStrip
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem("File");
            apriCartellaToolStripMenuItem = new ToolStripMenuItem("Apri cartella…");
            recentiToolStripMenuItem = new ToolStripMenuItem("Recenti");
            esciToolStripMenuItem = new ToolStripMenuItem("Esci");

            apriCartellaToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            esciToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;

            apriCartellaToolStripMenuItem.Click += (_, __) => ApriCartella();
            esciToolStripMenuItem.Click += (_, __) => this.Close();

            fileToolStripMenuItem.DropDownItems.Add(apriCartellaToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(recentiToolStripMenuItem);
            fileToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            fileToolStripMenuItem.DropDownItems.Add(esciToolStripMenuItem);

            menuStrip1.Items.Add(fileToolStripMenuItem);
            menuStrip1.Dock = DockStyle.Top;

            // ToolStrip
            toolStrip1 = new ToolStrip();
            btnApri = new ToolStripButton("Apri");
            btnRefresh = new ToolStripButton("Refresh");
            btnRimuovi = new ToolStripButton("Rimuovi");

            btnApri.Click += (_, __) => ApriCartella();
            btnRefresh.Click += (_, __) => RefreshCorrente();
            btnRimuovi.Click += (_, __) => RimuoviSelezione();

            toolStrip1.Items.Add(btnApri);
            toolStrip1.Items.Add(btnRefresh);
            toolStrip1.Items.Add(new ToolStripSeparator());
            toolStrip1.Items.Add(btnRimuovi);
            toolStrip1.Dock = DockStyle.Top;

            // StatusStrip
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Pronto");
            statusPath = new ToolStripStatusLabel("") { Spring = true };
            statusStrip1.Items.Add(statusLabel);
            statusStrip1.Items.Add(statusPath);
            statusStrip1.Dock = DockStyle.Bottom;

            // SplitContainer
            splitContainer1 = new SplitContainer();
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Orientation = Orientation.Vertical;
            splitContainer1.SplitterDistance = 420;

            // Left: TreeView
            Pacchi_tree = new TreeView();
            Pacchi_tree.Dock = DockStyle.Fill;
            Pacchi_tree.AllowDrop = true;
            Pacchi_tree.HideSelection = false;
            Pacchi_tree.AfterSelect += Pacchi_tree_AfterSelect;
            Pacchi_tree.DragEnter += Pacchi_tree_DragEnter;
            Pacchi_tree.DragDrop += Pacchi_tree_DragDrop;

            splitContainer1.Panel1.Controls.Add(Pacchi_tree);

            // Right: Details panel
            detailsPanel = new Panel();
            detailsPanel.Dock = DockStyle.Fill;
            detailsPanel.Padding = new Padding(10);

            lblTitolo = new Label();
            lblTitolo.Dock = DockStyle.Top;
            lblTitolo.Text = "Dettagli";
            lblTitolo.Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold);
            lblTitolo.Height = 32;

            txtDettagli = new TextBox();
            txtDettagli.Dock = DockStyle.Fill;
            txtDettagli.Multiline = true;
            txtDettagli.ReadOnly = true;
            txtDettagli.ScrollBars = ScrollBars.Vertical;

            detailsPanel.Controls.Add(txtDettagli);
            detailsPanel.Controls.Add(lblTitolo);

            splitContainer1.Panel2.Controls.Add(detailsPanel);

            // Add controls to form (ordine conta!)
            this.Controls.Add(splitContainer1);
            this.Controls.Add(toolStrip1);
            this.Controls.Add(menuStrip1);
            this.Controls.Add(statusStrip1);

            this.MainMenuStrip = menuStrip1;
        }
    }
}
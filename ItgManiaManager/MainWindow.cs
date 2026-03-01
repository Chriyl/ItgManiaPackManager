using ItgManiaManager.Service;
using ItgManiaManager.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
#nullable disable

namespace ItgManiaManager
{
    public partial class MainWindow : Form
    {
        // --- UI ---
        private MenuStrip menuStrip1;
        private ToolStrip toolStrip1;
        private StatusStrip statusStrip1;
        IPackService _packService;

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

        // ---- NEW layout helpers ----
        private TableLayoutPanel detailsLayout;
        private FlowLayoutPanel opsPanel;
        private Panel optionsPanel;

        private Label lblTitolo;
        private TextBox txtDettagli;

        private Label lblUniformDiff;
        private ComboBox uniformDiffComboBox;

        // ---- Example operation buttons ----
        private Button btnOperazione1;
        private Button btnOperazione2;
        private Button btnOperazione3;

        // --- State ---
        private const int MaxRecenti = 10;
        private TreeNode _placeHolder;
        private string _currentRootPath;
        private List<string> _bannerList = new List<string>();
        private ComboBox ComboBannerSong;
        private ComboBox ComboBannerPack;
        private string _pathSelectedBannerPack;
        private string _pathSelectedBannerSong;
        private EnumDifficulties _selectedDiff;

        public MainWindow() : this(new PackService())
        {
        }

        public MainWindow(IPackService packservice)
        {
            InitializeComponent();
            _packService = packservice;
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
        private void Pacchi_tree_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths.Any(Directory.Exists))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void Pacchi_tree_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null) return;

            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            var folder = dropped.FirstOrDefault(Directory.Exists);
            if (folder == null) return;

            LoadFolder(folder, addToRecenti: true);
        }

        // -----------------------------
        // Tree selection -> Details
        // -----------------------------
        private void Pacchi_tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var fullPath = e.Node.Tag as string;
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                txtDettagli.Text = "";
                statusPath.Text = "";
                UpdateOpsEnabled(false);
                return;
            }

            statusPath.Text = fullPath;
            UpdateOpsEnabled(true);

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
                lblTitolo.Text = "Dettagli";
                UpdateOpsEnabled(false);
                return;
            }

            node.Remove();
        }

        // -----------------------------
        // Recenti (persistenti)
        // -----------------------------
        private List<string> GetRecenti()
        {
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

        private void ApriCartellaToolStripMenuItem_Click(object sender, EventArgs e) => ApriCartella();
        private void EsciToolStripMenuItem_Click(object sender, EventArgs e) => Close();
        private void BtnApri_Click(object sender, EventArgs e) => ApriCartella();
        private void BtnRefresh_Click(object sender, EventArgs e) => RefreshCorrente();
        private void BtnRimuovi_Click(object sender, EventArgs e) => RimuoviSelezione();

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
        // Operation buttons (example)
        // -----------------------------
        private void UpdateOpsEnabled(bool enabled)
        {
            btnOperazione1.Enabled = enabled;
            btnOperazione2.Enabled = enabled;
            btnOperazione3.Enabled = enabled;
            uniformDiffComboBox.Enabled = enabled && !IsPlaceholderOnly();
        }

        private void BtnOperazione1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("TODO: Operazione 1", "Operazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnOperazione2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("TODO: Operazione 2", "Operazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnOperazione3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("TODO: Operazione 3", "Operazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // -----------------------------
        // InitializeComponent (Layout completo)
        // -----------------------------
        private void InitializeComponent()
        {
            _bannerList = Directory.GetFiles("Banner").ToList();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            apriCartellaToolStripMenuItem = new ToolStripMenuItem();
            recentiToolStripMenuItem = new ToolStripMenuItem();
            esciToolStripMenuItem = new ToolStripMenuItem();
            toolStrip1 = new ToolStrip();
            btnApri = new ToolStripButton();
            btnRefresh = new ToolStripButton();
            btnRimuovi = new ToolStripButton();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            statusPath = new ToolStripStatusLabel();
            splitContainer1 = new SplitContainer();
            Pacchi_tree = new TreeView();
            detailsPanel = new Panel();
            detailsLayout = new TableLayoutPanel();
            lblTitolo = new Label();
            opsPanel = new FlowLayoutPanel();
            btnOperazione1 = new Button();
            btnOperazione2 = new Button();
            btnOperazione3 = new Button();
            ComboBannerSong = new ComboBox();
            ComboBannerPack = new ComboBox();
            optionsPanel = new Panel();
            lblUniformDiff = new Label();
            uniformDiffComboBox = new ComboBox();
            txtDettagli = new TextBox();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            ((ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            detailsPanel.SuspendLayout();
            detailsLayout.SuspendLayout();
            opsPanel.SuspendLayout();
            optionsPanel.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1529, 24);
            menuStrip1.TabIndex = 2;
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { apriCartellaToolStripMenuItem, recentiToolStripMenuItem, esciToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "File";
            // 
            // apriCartellaToolStripMenuItem
            // 
            apriCartellaToolStripMenuItem.Name = "apriCartellaToolStripMenuItem";
            apriCartellaToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            apriCartellaToolStripMenuItem.Size = new Size(189, 22);
            apriCartellaToolStripMenuItem.Text = "Apri cartella…";
            apriCartellaToolStripMenuItem.Click += ApriCartellaToolStripMenuItem_Click;
            // 
            // recentiToolStripMenuItem
            // 
            recentiToolStripMenuItem.Name = "recentiToolStripMenuItem";
            recentiToolStripMenuItem.Size = new Size(189, 22);
            recentiToolStripMenuItem.Text = "Recenti";
            // 
            // esciToolStripMenuItem
            // 
            esciToolStripMenuItem.Name = "esciToolStripMenuItem";
            esciToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
            esciToolStripMenuItem.Size = new Size(189, 22);
            esciToolStripMenuItem.Text = "Esci";
            esciToolStripMenuItem.Click += EsciToolStripMenuItem_Click;
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnApri, btnRefresh, btnRimuovi });
            toolStrip1.Location = new Point(0, 24);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(1529, 25);
            toolStrip1.TabIndex = 1;
            // 
            // btnApri
            // 
            btnApri.Name = "btnApri";
            btnApri.Size = new Size(33, 22);
            btnApri.Text = "Apri";
            btnApri.Click += BtnApri_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(50, 22);
            btnRefresh.Text = "Refresh";
            btnRefresh.Click += BtnRefresh_Click;
            // 
            // btnRimuovi
            // 
            btnRimuovi.Name = "btnRimuovi";
            btnRimuovi.Size = new Size(55, 22);
            btnRimuovi.Text = "Rimuovi";
            btnRimuovi.Click += BtnRimuovi_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, statusPath });
            statusStrip1.Location = new Point(0, 678);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1529, 22);
            statusStrip1.TabIndex = 3;
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(43, 17);
            statusLabel.Text = "Pronto";
            // 
            // statusPath
            // 
            statusPath.Name = "statusPath";
            statusPath.Size = new Size(0, 17);
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 49);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(Pacchi_tree);
            splitContainer1.Panel2.Controls.Add(detailsPanel);
            splitContainer1.Panel2MinSize = 260;
            splitContainer1.Size = new Size(1529, 629);
            splitContainer1.SplitterDistance = 36;
            splitContainer1.SplitterWidth = 6;
            splitContainer1.TabIndex = 0;
            // 
            // Pacchi_tree
            // 
            Pacchi_tree.AllowDrop = true;
            Pacchi_tree.Anchor = AnchorStyles.Top;
            Pacchi_tree.HideSelection = false;
            Pacchi_tree.Location = new Point(-3, 0);
            Pacchi_tree.Name = "Pacchi_tree";
            Pacchi_tree.Size = new Size(765, 629);
            Pacchi_tree.TabIndex = 0;
            Pacchi_tree.AfterSelect += Pacchi_tree_AfterSelect;
            Pacchi_tree.DragDrop += Pacchi_tree_DragDrop;
            Pacchi_tree.DragEnter += Pacchi_tree_DragEnter;
            // 
            // detailsPanel
            // 
            detailsPanel.Anchor = AnchorStyles.Top;
            detailsPanel.Controls.Add(detailsLayout);
            detailsPanel.Location = new Point(768, 3);
            detailsPanel.Name = "detailsPanel";
            detailsPanel.Padding = new Padding(10);
            detailsPanel.Size = new Size(683, 626);
            detailsPanel.TabIndex = 0;
            // 
            // detailsLayout
            // 
            detailsLayout.ColumnCount = 1;
            detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            detailsLayout.Controls.Add(lblTitolo, 0, 0);
            detailsLayout.Controls.Add(opsPanel, 0, 1);
            detailsLayout.Controls.Add(optionsPanel, 0, 2);
            detailsLayout.Controls.Add(txtDettagli, 0, 3);
            detailsLayout.Dock = DockStyle.Fill;
            detailsLayout.Location = new Point(10, 10);
            detailsLayout.Name = "detailsLayout";
            detailsLayout.RowCount = 4;
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            detailsLayout.Size = new Size(663, 606);
            detailsLayout.TabIndex = 0;
            // 
            // lblTitolo
            // 
            lblTitolo.Dock = DockStyle.Fill;
            lblTitolo.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblTitolo.Location = new Point(3, 0);
            lblTitolo.Name = "lblTitolo";
            lblTitolo.Size = new Size(657, 36);
            lblTitolo.TabIndex = 0;
            lblTitolo.Text = "Dettagli";
            lblTitolo.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // opsPanel
            // 
            opsPanel.AutoScroll = true;
            opsPanel.Controls.Add(btnOperazione1);
            opsPanel.Controls.Add(btnOperazione2);
            opsPanel.Controls.Add(btnOperazione3);
            opsPanel.Controls.Add(ComboBannerSong);
            opsPanel.Controls.Add(ComboBannerPack);
            opsPanel.Dock = DockStyle.Fill;
            opsPanel.Location = new Point(0, 36);
            opsPanel.Margin = new Padding(0);
            opsPanel.Name = "opsPanel";
            opsPanel.Size = new Size(663, 40);
            opsPanel.TabIndex = 1;
            opsPanel.WrapContents = false;
            // 
            // btnOperazione1
            // 
            btnOperazione1.AutoSize = true;
            btnOperazione1.Location = new Point(3, 3);
            btnOperazione1.Name = "btnOperazione1";
            btnOperazione1.Size = new Size(86, 25);
            btnOperazione1.TabIndex = 0;
            btnOperazione1.Text = "Operazione 1";
            btnOperazione1.Click += BtnOperazione1_Click;
            // 
            // btnOperazione2
            // 
            btnOperazione2.AutoSize = true;
            btnOperazione2.Location = new Point(95, 3);
            btnOperazione2.Name = "btnOperazione2";
            btnOperazione2.Size = new Size(86, 25);
            btnOperazione2.TabIndex = 1;
            btnOperazione2.Text = "Operazione 2";
            btnOperazione2.Click += BtnOperazione2_Click;
            // 
            // btnOperazione3
            // 
            btnOperazione3.AutoSize = true;
            btnOperazione3.Location = new Point(187, 3);
            btnOperazione3.Name = "btnOperazione3";
            btnOperazione3.Size = new Size(86, 25);
            btnOperazione3.TabIndex = 2;
            btnOperazione3.Text = "Operazione 3";
            btnOperazione3.Click += BtnOperazione3_Click;
            // 
            // comboBox1
            // 
            ComboBannerSong.DropDownStyle = ComboBoxStyle.DropDownList;
            ComboBannerSong.Items.AddRange(new object[] { "Aggiungi il banner della canzone" });
            ComboBannerSong.Items.AddRange( _bannerList.ToArray());
            ComboBannerSong.Location = new Point(279, 3);
            ComboBannerSong.Name = "comboBox1";
            ComboBannerSong.Size = new Size(182, 23);
            ComboBannerSong.TabIndex = 2;
            ComboBannerSong.SelectedIndexChanged += ComboBannerSong_SelectedIndexChanged;
            // 
            // comboBox2
            // 
            ComboBannerPack.DropDownStyle = ComboBoxStyle.DropDownList;
            ComboBannerPack.Items.AddRange(new object[] { "Aggiungi il banner del pack" });
            ComboBannerPack.Items.AddRange(_bannerList.ToArray());
            ComboBannerPack.Location = new Point(467, 3);
            ComboBannerPack.Name = "comboBox2";
            ComboBannerPack.Size = new Size(190, 23);
            ComboBannerPack.TabIndex = 3;
            ComboBannerPack.SelectedIndexChanged += ComboBannerPack_SelectedIndexChanged;

            // 
            // optionsPanel
            // 
            optionsPanel.Controls.Add(lblUniformDiff);
            optionsPanel.Controls.Add(uniformDiffComboBox);
            optionsPanel.Dock = DockStyle.Fill;
            optionsPanel.Location = new Point(3, 79);
            optionsPanel.Name = "optionsPanel";
            optionsPanel.Size = new Size(657, 34);
            optionsPanel.TabIndex = 2;
            // 
            // lblUniformDiff
            // 
            lblUniformDiff.AutoSize = true;
            lblUniformDiff.BorderStyle = BorderStyle.FixedSingle;
            lblUniformDiff.Cursor = Cursors.Hand;
            lblUniformDiff.Location = new Point(24, 9);
            lblUniformDiff.Name = "lblUniformDiff";
            lblUniformDiff.Size = new Size(112, 17);
            lblUniformDiff.TabIndex = 0;
            lblUniformDiff.Text = "Uniforma difficoltà:";
            lblUniformDiff.Click += lblUniformDiff_Click;
            // 
            // uniformDiffComboBox
            // 
            uniformDiffComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            uniformDiffComboBox.Items.AddRange(new object[] { EnumDifficulties.Beginner, EnumDifficulties.Easy, EnumDifficulties.Medium, EnumDifficulties.Hard, EnumDifficulties.Challenge, EnumDifficulties.Edit });
            uniformDiffComboBox.Location = new Point(140, 6);
            uniformDiffComboBox.Name = "uniformDiffComboBox";
            uniformDiffComboBox.Size = new Size(160, 23);
            uniformDiffComboBox.TabIndex = 1;
            uniformDiffComboBox.SelectionChangeCommitted += UniformDiffComboBox_SelectionChangeCommitted;
            // 
            // txtDettagli
            // 
            txtDettagli.BorderStyle = BorderStyle.None;
            txtDettagli.Dock = DockStyle.Fill;
            txtDettagli.Location = new Point(3, 119);
            txtDettagli.Multiline = true;
            txtDettagli.Name = "txtDettagli";
            txtDettagli.ReadOnly = true;
            txtDettagli.ScrollBars = ScrollBars.Vertical;
            txtDettagli.Size = new Size(657, 484);
            txtDettagli.TabIndex = 3;
            // 
            // MainWindow
            // 
            ClientSize = new Size(1529, 700);
            Controls.Add(splitContainer1);
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainWindow";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ItgManiaManager";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            detailsPanel.ResumeLayout(false);
            detailsLayout.ResumeLayout(false);
            detailsLayout.PerformLayout();
            opsPanel.ResumeLayout(false);
            opsPanel.PerformLayout();
            optionsPanel.ResumeLayout(false);
            optionsPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        private void UniformDiffComboBox_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if (uniformDiffComboBox.SelectedItem == null)
                return;

            var selectedDifficulty = (EnumDifficulties)uniformDiffComboBox.SelectedItem;
            _selectedDiff = selectedDifficulty;
            MessageBox.Show($"Selezionata difficoltà: {selectedDifficulty}");
        }

        private void lblUniformDiff_Click(object sender, EventArgs e)
        {
            try
            {
                // Mostra la rotellina
                Cursor.Current = Cursors.WaitCursor;

                // Esegui l’operazione
                _packService.UniformDifficulty(_currentRootPath, _selectedDiff);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Errore", MessageBoxButtons.OK);
            }
            finally
            {
                // Torna al cursore normale
                Cursor.Current = Cursors.Default;
                MessageBox.Show($"Difficolta impostat a {_selectedDiff}", "Successo", MessageBoxButtons.OK);

            }
        }

        private void ComboBannerSong_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ComboBannerSong.SelectedItem == null)
                return;

            var songBanner = ComboBannerSong.SelectedItem;
            _pathSelectedBannerSong= (string)songBanner;
            MessageBox.Show($"Banner della song selezionato:  {songBanner}");
        }

        private void ComboBannerPack_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ComboBannerPack.SelectedItem == null)
                return;

            var songBanner = ComboBannerSong.SelectedItem;
            _pathSelectedBannerPack = (string)songBanner;
            MessageBox.Show($"Banner del pack selezionato:  {songBanner}");
        }
    }
}
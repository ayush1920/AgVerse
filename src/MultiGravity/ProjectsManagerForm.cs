using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MultiGravity
{
    public class ProjectsManagerForm : Form
    {
        private enum FilterMode { All, Shared, Excluded, Archived }

        private string _currentProfileName;
        private string _currentProfilePath = "";
        private readonly List<ProfileInfo> _profiles = new();

        private Dictionary<string, ProjectInfo> _activeProjects = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ProjectInfo> _archivedProjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _profileExclusionsMap = new(StringComparer.OrdinalIgnoreCase);

        private FilterMode _currentFilter = FilterMode.All;
        private string _searchFilter = "";

        // UI Controls
        private ComboBox _cbProfile = null!;
        private TextBox _txtSearch = null!;
        private Button _btnRefresh = null!;

        private Button _btnPillAll = null!;
        private Button _btnPillShared = null!;
        private Button _btnPillExcluded = null!;
        private Button _btnPillArchived = null!;

        private ListView _lvProjects = null!;
        private Button _btnToggle = null!;
        private Button _btnArchiveRestore = null!;
        private Button _btnOpenFolder = null!;
        private Button _btnSave = null!;
        private ContextMenuStrip _contextMenu = null!;

        public ProjectsManagerForm(string? initialProfileName = null)
        {
            _profiles = AntigravityCore.GetProfiles();
            _currentProfileName = initialProfileName ?? _profiles.FirstOrDefault()?.Name ?? "Default";
            UpdateProfilePath();

            LoadAllExclusions();
            ReloadProjectsData();

            InitializeComponent();
            ApplyFilterAndRender();
        }

        private void UpdateProfilePath()
        {
            _currentProfilePath = Path.Combine(AntigravityCore.ProfilesDir, _currentProfileName);
        }

        private void LoadAllExclusions()
        {
            _profileExclusionsMap.Clear();
            foreach (var p in _profiles)
            {
                _profileExclusionsMap[p.Name] = AntigravityCore.GetExclusions(p.Path);
            }
            if (!_profileExclusionsMap.ContainsKey(_currentProfileName) && Directory.Exists(_currentProfilePath))
            {
                _profileExclusionsMap[_currentProfileName] = AntigravityCore.GetExclusions(_currentProfilePath);
            }
        }

        private void ReloadProjectsData()
        {
            _activeProjects = AntigravityCore.GetAllProjects();
            _archivedProjects = AntigravityCore.GetArchivedProjects();
        }

        private HashSet<string> GetCurrentProfileExclusions()
        {
            if (!_profileExclusionsMap.TryGetValue(_currentProfileName, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _profileExclusionsMap[_currentProfileName] = set;
            }
            return set;
        }

        private void InitializeComponent()
        {
            Text = "Manage Projects - MultiGravity";
            Size = new Size(820, 640);
            MinimumSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            string iconPath = Path.Combine(AntigravityCore.BaseDir, "icon.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 4,
                ColumnCount = 1
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Top bar (Profile + Search + Refresh)
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // Filter pills bar
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Projects Table
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Action Toolbar

            // 1. Top Bar
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                Margin = new Padding(0, 0, 0, 4)
            };

            var leftTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var lblProfile = new Label
            {
                Text = "Target Profile:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0)
            };

            _cbProfile = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200,
                Margin = new Padding(0, 2, 0, 0)
            };
            foreach (var p in _profiles)
            {
                _cbProfile.Items.Add(p.Name);
            }
            if (_cbProfile.Items.Contains(_currentProfileName))
            {
                _cbProfile.SelectedItem = _currentProfileName;
            }
            else if (_cbProfile.Items.Count > 0)
            {
                _cbProfile.SelectedIndex = 0;
                _currentProfileName = _cbProfile.SelectedItem?.ToString() ?? "Default";
                UpdateProfilePath();
            }
            _cbProfile.SelectedIndexChanged += OnProfileChanged;

            leftTop.Controls.Add(lblProfile);
            leftTop.Controls.Add(_cbProfile);

            var rightTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _txtSearch = new TextBox
            {
                Width = 220,
                PlaceholderText = "🔍 Search project or folder...",
                Margin = new Padding(0, 3, 6, 0)
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtSearch.Text.Trim();
                ApplyFilterAndRender();
            };

            _btnRefresh = new Button
            {
                Text = "🔄 " + Localization.T("btn_refresh"),
                Height = 27,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 1, 0, 0),
                BackColor = Color.FromArgb(245, 245, 248)
            };
            _btnRefresh.Click += OnRefreshClick;

            rightTop.Controls.Add(_txtSearch);
            rightTop.Controls.Add(_btnRefresh);

            topPanel.Controls.Add(rightTop);
            topPanel.Controls.Add(leftTop);
            rootLayout.Controls.Add(topPanel, 0, 0);

            // 2. Filter Pills Bar
            var pillsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _btnPillAll = CreatePillButton("All (0)", FilterMode.All);
            _btnPillShared = CreatePillButton("🟢 Shared (0)", FilterMode.Shared);
            _btnPillExcluded = CreatePillButton("🟡 Excluded (0)", FilterMode.Excluded);
            _btnPillArchived = CreatePillButton("⚪ Archived (0)", FilterMode.Archived);

            pillsPanel.Controls.Add(_btnPillAll);
            pillsPanel.Controls.Add(_btnPillShared);
            pillsPanel.Controls.Add(_btnPillExcluded);
            pillsPanel.Controls.Add(_btnPillArchived);
            rootLayout.Controls.Add(pillsPanel, 0, 1);

            // 3. Projects ListView Table
            _lvProjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point)
            };
            EnableDoubleBuffer(_lvProjects);

            _lvProjects.Columns.Add("Status", 110, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Project Name", 190, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Workspace Folder", 380, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("ID", 90, HorizontalAlignment.Left);

            _lvProjects.SelectedIndexChanged += OnSelectedProjectChanged;
            _lvProjects.DoubleClick += (s, e) => OnOpenFolderClick(s, e);
            _lvProjects.Resize += (s, e) => AdjustColumnWidths();
            _lvProjects.SizeChanged += (s, e) => AdjustColumnWidths();
            _lvProjects.ColumnWidthChanged += (s, e) =>
            {
                if (e.ColumnIndex != 2) AdjustColumnWidths();
            };
            Shown += (s, e) => AdjustColumnWidths();

            // Context Menu
            _contextMenu = new ContextMenuStrip();
            var mnuShare = new ToolStripMenuItem("🟢 Set as Shared (Include)", null, (s, e) => SetSelectedProjectExclusion(false));
            var mnuExclude = new ToolStripMenuItem("🟡 Exclude from this Profile", null, (s, e) => SetSelectedProjectExclusion(true));
            var mnuArchive = new ToolStripMenuItem("📦 Archive Globally (Hide from All)", null, (s, e) => OnArchiveRestoreClick(s, e));
            var mnuOpen = new ToolStripMenuItem("📂 Open Folder in Explorer", null, (s, e) => OnOpenFolderClick(s, e));

            _contextMenu.Items.Add(mnuShare);
            _contextMenu.Items.Add(mnuExclude);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(mnuArchive);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(mnuOpen);
            _lvProjects.ContextMenuStrip = _contextMenu;

            rootLayout.Controls.Add(_lvProjects, 0, 2);

            // 4. Action Toolbar
            var actionPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(0, 6, 0, 0)
            };

            var leftActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _btnToggle = new Button
            {
                Text = "🟡 Exclude from Profile",
                Height = 32,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            _btnToggle.Click += OnToggleExcludeClick;

            _btnArchiveRestore = new Button
            {
                Text = "📦 Archive Globally",
                Height = 32,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            _btnArchiveRestore.Click += OnArchiveRestoreClick;

            _btnOpenFolder = new Button
            {
                Text = "📂 Open Folder",
                Height = 32,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            _btnOpenFolder.Click += OnOpenFolderClick;

            leftActions.Controls.Add(_btnToggle);
            leftActions.Controls.Add(_btnArchiveRestore);
            leftActions.Controls.Add(_btnOpenFolder);

            var rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            _btnSave = new Button
            {
                Text = "💾 " + Localization.T("btn_salvar"),
                Height = 32,
                AutoSize = true,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(0, 0, 8, 0),
                BackColor = Color.FromArgb(225, 238, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnSave.Click += OnSaveClick;

            var btnClose = new Button
            {
                Text = Localization.T("btn_cancelar"),
                Width = 85,
                Height = 32,
                Margin = new Padding(0),
                DialogResult = DialogResult.OK
            };

            rightActions.Controls.Add(_btnSave);
            rightActions.Controls.Add(btnClose);

            actionPanel.Controls.Add(rightActions);
            actionPanel.Controls.Add(leftActions);

            rootLayout.Controls.Add(actionPanel, 0, 3);

            Controls.Add(rootLayout);
        }

        private Button CreatePillButton(string text, FilterMode mode)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                Padding = new Padding(10, 0, 10, 0),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Standard,
                BackColor = Color.FromArgb(245, 245, 245),
                Tag = mode
            };
            btn.Click += (s, e) =>
            {
                _currentFilter = mode;
                UpdatePillStyles();
                ApplyFilterAndRender();
            };
            return btn;
        }

        private void UpdatePillStyles()
        {
            UpdatePillStyle(_btnPillAll, FilterMode.All);
            UpdatePillStyle(_btnPillShared, FilterMode.Shared);
            UpdatePillStyle(_btnPillExcluded, FilterMode.Excluded);
            UpdatePillStyle(_btnPillArchived, FilterMode.Archived);
        }

        private void UpdatePillStyle(Button btn, FilterMode mode)
        {
            if (_currentFilter == mode)
            {
                btn.BackColor = Color.FromArgb(220, 235, 252);
                btn.Font = new Font(Font, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(245, 245, 245);
                btn.Font = new Font(Font, FontStyle.Regular);
            }
        }

        private void OnProfileChanged(object? sender, EventArgs e)
        {
            string? sel = _cbProfile.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(sel) && sel != _currentProfileName)
            {
                _currentProfileName = sel;
                UpdateProfilePath();
                ApplyFilterAndRender();
            }
        }

        private void OnRefreshClick(object? sender, EventArgs e)
        {
            ReloadProjectsData();
            LoadAllExclusions();
            ApplyFilterAndRender();
            MessageBox.Show(this, "Projects reloaded successfully from disk.", "Projects Refreshed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyFilterAndRender()
        {
            var exclusions = GetCurrentProfileExclusions();

            // Calculate counts
            int totalActive = _activeProjects.Count;
            int totalArchived = _archivedProjects.Count;
            int excludedCount = _activeProjects.Keys.Count(k => exclusions.Contains(k));
            int sharedCount = totalActive - excludedCount;

            _btnPillAll.Text = $"All ({totalActive + totalArchived})";
            _btnPillShared.Text = $"🟢 Shared ({sharedCount})";
            _btnPillExcluded.Text = $"🟡 Excluded ({excludedCount})";
            _btnPillArchived.Text = $"⚪ Archived ({totalArchived})";
            UpdatePillStyles();

            // Filter projects
            var itemsToDisplay = new List<(ProjectInfo proj, string statusText, Color statusColor)>();

            // Active projects
            foreach (var p in _activeProjects.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                bool isExcluded = exclusions.Contains(p.Id);
                string status = isExcluded ? "🟡 Excluded" : "🟢 Shared";
                Color color = isExcluded ? Color.FromArgb(190, 95, 0) : Color.FromArgb(34, 139, 34);

                if (_currentFilter == FilterMode.Shared && isExcluded) continue;
                if (_currentFilter == FilterMode.Excluded && !isExcluded) continue;
                if (_currentFilter == FilterMode.Archived) continue;

                if (!MatchesSearch(p)) continue;

                itemsToDisplay.Add((p, status, color));
            }

            // Archived projects
            if (_currentFilter == FilterMode.All || _currentFilter == FilterMode.Archived)
            {
                foreach (var p in _archivedProjects.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!MatchesSearch(p)) continue;
                    itemsToDisplay.Add((p, "⚪ Archived", Color.Gray));
                }
            }

            _lvProjects.BeginUpdate();
            _lvProjects.Items.Clear();

            for (int i = 0; i < itemsToDisplay.Count; i++)
            {
                var (p, statusText, statusColor) = itemsToDisplay[i];
                var item = new ListViewItem(statusText)
                {
                    UseItemStyleForSubItems = false,
                    Tag = p
                };
                item.SubItems[0].ForeColor = statusColor;
                item.SubItems[0].Font = new Font(_lvProjects.Font, FontStyle.Bold);

                var subName = item.SubItems.Add(p.Name);
                if (p.IsArchived)
                {
                    subName.ForeColor = Color.DimGray;
                }
                else
                {
                    subName.Font = new Font(_lvProjects.Font, FontStyle.Bold);
                }

                item.SubItems.Add(string.IsNullOrEmpty(p.FolderPath) ? "(Unknown path)" : p.FolderPath);
                item.SubItems.Add(p.Id.Length > 8 ? p.Id.Substring(0, 8) + "..." : p.Id);

                // Alternating row background
                if (i % 2 == 1)
                {
                    item.BackColor = Color.FromArgb(248, 250, 252);
                }

                _lvProjects.Items.Add(item);
            }

            _lvProjects.EndUpdate();
            AdjustColumnWidths();
            UpdateActionButtons();
        }

        private bool _isAdjustingColumns = false;

        private void AdjustColumnWidths()
        {
            if (_isAdjustingColumns || _lvProjects == null || _lvProjects.Columns.Count < 4) return;
            try
            {
                _isAdjustingColumns = true;
                int clientWidth = _lvProjects.ClientSize.Width;
                int statusWidth = _lvProjects.Columns[0].Width;
                int nameWidth = _lvProjects.Columns[1].Width;
                int idWidth = _lvProjects.Columns[3].Width;

                int remaining = clientWidth - (statusWidth + nameWidth + idWidth) - 4;
                if (remaining > 150)
                {
                    _lvProjects.Columns[2].Width = remaining;
                }
            }
            catch { }
            finally
            {
                _isAdjustingColumns = false;
            }
        }

        private bool MatchesSearch(ProjectInfo p)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return p.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   p.FolderPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   p.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
        }

        private void OnSelectedProjectChanged(object? sender, EventArgs e)
        {
            UpdateActionButtons();
        }

        private ProjectInfo? GetSelectedProject()
        {
            if (_lvProjects.SelectedItems.Count == 0) return null;
            return _lvProjects.SelectedItems[0].Tag as ProjectInfo;
        }

        private void UpdateActionButtons()
        {
            var p = GetSelectedProject();
            if (p == null)
            {
                _btnToggle.Enabled = false;
                _btnToggle.Text = "🟡 Exclude from Profile";
                _btnArchiveRestore.Enabled = false;
                _btnArchiveRestore.Text = "📦 Archive Globally";
                _btnOpenFolder.Enabled = false;
                return;
            }

            _btnOpenFolder.Enabled = !string.IsNullOrEmpty(p.FolderPath) && Directory.Exists(p.FolderPath);

            if (p.IsArchived)
            {
                _btnToggle.Enabled = false;
                _btnToggle.Text = "🟡 Exclude from Profile";

                _btnArchiveRestore.Enabled = true;
                _btnArchiveRestore.Text = "🔄 Restore from Archive";
                _btnArchiveRestore.BackColor = Color.FromArgb(235, 245, 235);
            }
            else
            {
                var exclusions = GetCurrentProfileExclusions();
                bool isExcluded = exclusions.Contains(p.Id);

                _btnToggle.Enabled = true;
                if (isExcluded)
                {
                    _btnToggle.Text = "🟢 Share with Profile";
                    _btnToggle.BackColor = Color.FromArgb(235, 245, 235);
                }
                else
                {
                    _btnToggle.Text = "🟡 Exclude from Profile";
                    _btnToggle.BackColor = Color.FromArgb(255, 245, 230);
                }

                _btnArchiveRestore.Enabled = true;
                _btnArchiveRestore.Text = "📦 Archive Globally";
                _btnArchiveRestore.BackColor = Color.FromArgb(250, 240, 235);
            }
        }

        private void OnToggleExcludeClick(object? sender, EventArgs e)
        {
            var p = GetSelectedProject();
            if (p == null || p.IsArchived) return;

            var exclusions = GetCurrentProfileExclusions();
            if (exclusions.Contains(p.Id))
            {
                exclusions.Remove(p.Id);
            }
            else
            {
                exclusions.Add(p.Id);
            }

            ApplyFilterAndRender();
        }

        private void SetSelectedProjectExclusion(bool exclude)
        {
            var p = GetSelectedProject();
            if (p == null || p.IsArchived) return;

            var exclusions = GetCurrentProfileExclusions();
            if (exclude) exclusions.Add(p.Id);
            else exclusions.Remove(p.Id);

            ApplyFilterAndRender();
        }

        private void OnArchiveRestoreClick(object? sender, EventArgs e)
        {
            var p = GetSelectedProject();
            if (p == null) return;

            if (p.IsArchived)
            {
                // Unarchive / Restore
                var confirm = MessageBox.Show(
                    this,
                    Localization.T("confirma_unarchive", p.Name),
                    Localization.T("t_confirma"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    if (AntigravityCore.UnarchiveProject(p.Id))
                    {
                        ReloadProjectsData();
                        ApplyFilterAndRender();
                        MessageBox.Show(this, Localization.T("sucesso_unarchived", p.Name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            else
            {
                // Archive Globally
                var confirm = MessageBox.Show(
                    this,
                    Localization.T("confirma_archive", p.Name),
                    Localization.T("t_confirma"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    if (AntigravityCore.ArchiveProject(p.Id))
                    {
                        ReloadProjectsData();
                        ApplyFilterAndRender();
                        MessageBox.Show(this, Localization.T("sucesso_archived", p.Name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void OnOpenFolderClick(object? sender, EventArgs e)
        {
            var p = GetSelectedProject();
            if (p == null || string.IsNullOrEmpty(p.FolderPath)) return;

            if (Directory.Exists(p.FolderPath))
            {
                try
                {
                    Process.Start("explorer.exe", p.FolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not open folder: " + ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show(this, "Workspace directory does not exist on disk:\n" + p.FolderPath, Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            // Save exclusions for all modified profiles
            foreach (var (profName, exSet) in _profileExclusionsMap)
            {
                string profPath = Path.Combine(AntigravityCore.ProfilesDir, profName);
                if (Directory.Exists(profPath))
                {
                    AntigravityCore.SaveExclusions(profPath, exSet);
                    AntigravityCore.SyncProfileProjects(profPath);
                }
            }

            MessageBox.Show(
                this,
                "All project exclusions and visibility settings applied successfully!",
                Localization.T("t_sucesso"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        private static void EnableDoubleBuffer(ListView listView)
        {
            try
            {
                typeof(Control).InvokeMember(
                    "DoubleBuffered",
                    BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    listView,
                    new object[] { true });
            }
            catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace AgVerse
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
        private readonly HashSet<string> _checkedProjectIds = new(StringComparer.OrdinalIgnoreCase);
        private bool _isPopulating = false;

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
            Text = "Manage Projects - AgVerse";
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

            var btnRefreshToken = new Button
            {
                Text = "🔑 Refresh Token",
                Height = 27,
                AutoSize = true,
                Padding = new Padding(6, 0, 6, 0),
                Margin = new Padding(8, 1, 0, 0),
                BackColor = Color.FromArgb(245, 248, 255)
            };
            btnRefreshToken.Click += OnRefreshProfileTokenClick;

            leftTop.Controls.Add(lblProfile);
            leftTop.Controls.Add(_cbProfile);
            leftTop.Controls.Add(btnRefreshToken);

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

            var btnCheckAll = new Button
            {
                Text = "☑️ Check All",
                Height = 28,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(14, 0, 4, 0),
                BackColor = Color.FromArgb(245, 245, 245)
            };
            btnCheckAll.Click += (s, e) => SetAllVisibleChecked(true);

            var btnUncheckAll = new Button
            {
                Text = "⬜ Clear",
                Height = 28,
                AutoSize = true,
                Padding = new Padding(8, 0, 8, 0),
                Margin = new Padding(0, 0, 6, 0),
                BackColor = Color.FromArgb(245, 245, 245)
            };
            btnUncheckAll.Click += (s, e) => SetAllVisibleChecked(false);

            pillsPanel.Controls.Add(_btnPillAll);
            pillsPanel.Controls.Add(_btnPillShared);
            pillsPanel.Controls.Add(_btnPillExcluded);
            pillsPanel.Controls.Add(_btnPillArchived);
            pillsPanel.Controls.Add(btnCheckAll);
            pillsPanel.Controls.Add(btnUncheckAll);
            rootLayout.Controls.Add(pillsPanel, 0, 1);

            // 3. Projects ListView Table
            _lvProjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                CheckBoxes = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point)
            };
            EnableDoubleBuffer(_lvProjects);

            _lvProjects.Columns.Add("Status", 135, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Project Name", 190, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Workspace Folder", 380, HorizontalAlignment.Left);
            _lvProjects.Columns.Add("ID", 90, HorizontalAlignment.Left);

            _lvProjects.SelectedIndexChanged += OnSelectedProjectChanged;
            _lvProjects.ItemChecked += OnItemChecked;
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
            var mnuShare = new ToolStripMenuItem("🟢 Set as Shared (Include)", null, (s, e) => SetSelectedProjectsExclusion(false));
            var mnuExclude = new ToolStripMenuItem("🟡 Exclude from this Profile", null, (s, e) => SetSelectedProjectsExclusion(true));
            var mnuArchive = new ToolStripMenuItem("📦 Archive Globally (Hide from All)", null, (s, e) => OnArchiveRestoreClick(s, e));
            var mnuCheckAll = new ToolStripMenuItem("☑️ Check All Visible", null, (s, e) => SetAllVisibleChecked(true));
            var mnuClearChecks = new ToolStripMenuItem("⬜ Uncheck All", null, (s, e) => SetAllVisibleChecked(false));
            var mnuOpen = new ToolStripMenuItem("📂 Open Folder in Explorer", null, (s, e) => OnOpenFolderClick(s, e));

            _contextMenu.Items.Add(mnuShare);
            _contextMenu.Items.Add(mnuExclude);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(mnuArchive);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(mnuCheckAll);
            _contextMenu.Items.Add(mnuClearChecks);
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
                _checkedProjectIds.Clear();
                UpdateProfilePath();
                ApplyFilterAndRender();
            }
        }

        private void OnRefreshClick(object? sender, EventArgs e)
        {
            _checkedProjectIds.Clear();
            ReloadProjectsData();
            LoadAllExclusions();
            ApplyFilterAndRender();
            MessageBox.Show(this, "Projects reloaded successfully from disk.", "Projects Refreshed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void OnRefreshProfileTokenClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProfilePath) || !Directory.Exists(_currentProfilePath))
            {
                MessageBox.Show(this, "Profile directory not found.", Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var (success, message) = await TokenService.RefreshTokenForProfileAsync(_currentProfilePath, _currentProfileName);
            if (success)
            {
                MessageBox.Show(this, message, "Token Refreshed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var ask = MessageBox.Show(this,
                    $"{message}\n\nWould you like to sign in with Google in your browser to generate a fresh token?",
                    "Refresh Token - AgVerse",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ask == DialogResult.Yes)
                {
                    var (authSuccess, authMsg) = await TokenService.AuthenticateInBrowserAsync(_currentProfilePath, _currentProfileName);
                    if (authSuccess)
                    {
                        MessageBox.Show(this, authMsg, Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(this, authMsg, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
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

            _isPopulating = true;
            try
            {
                _lvProjects.BeginUpdate();
                _lvProjects.Items.Clear();

                for (int i = 0; i < itemsToDisplay.Count; i++)
                {
                    var (p, statusText, statusColor) = itemsToDisplay[i];
                    var item = new ListViewItem(statusText)
                    {
                        UseItemStyleForSubItems = false,
                        Tag = p,
                        Checked = _checkedProjectIds.Contains(p.Id)
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
            }
            finally
            {
                _isPopulating = false;
            }

            AdjustColumnWidths();
            UpdateActionButtons();
        }

        private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (_isPopulating) return;
            if (e.Item?.Tag is ProjectInfo p)
            {
                if (e.Item.Checked)
                    _checkedProjectIds.Add(p.Id);
                else
                    _checkedProjectIds.Remove(p.Id);
            }
            UpdateActionButtons();
        }

        private void SetAllVisibleChecked(bool check)
        {
            _isPopulating = true;
            _lvProjects.BeginUpdate();
            try
            {
                foreach (ListViewItem item in _lvProjects.Items)
                {
                    item.Checked = check;
                    if (item.Tag is ProjectInfo p)
                    {
                        if (check) _checkedProjectIds.Add(p.Id);
                        else _checkedProjectIds.Remove(p.Id);
                    }
                }
            }
            finally
            {
                _lvProjects.EndUpdate();
                _isPopulating = false;
            }
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

        private List<ProjectInfo> GetTargetProjects()
        {
            var list = new List<ProjectInfo>();

            if (_checkedProjectIds.Count > 0)
            {
                foreach (var id in _checkedProjectIds)
                {
                    if (_activeProjects.TryGetValue(id, out var p)) list.Add(p);
                    else if (_archivedProjects.TryGetValue(id, out var ap)) list.Add(ap);
                }
            }

            if (list.Count == 0 && _lvProjects.SelectedItems.Count > 0)
            {
                foreach (ListViewItem item in _lvProjects.SelectedItems)
                {
                    if (item.Tag is ProjectInfo p) list.Add(p);
                }
            }

            return list;
        }

        private ProjectInfo? GetSelectedProject()
        {
            var list = GetTargetProjects();
            return list.Count > 0 ? list[0] : null;
        }

        private void UpdateActionButtons()
        {
            var targets = GetTargetProjects();
            if (targets.Count == 0)
            {
                _btnToggle.Enabled = false;
                _btnToggle.Text = "🟡 Exclude from Profile";
                _btnToggle.BackColor = SystemColors.Control;
                _btnArchiveRestore.Enabled = false;
                _btnArchiveRestore.Text = "📦 Archive Globally";
                _btnArchiveRestore.BackColor = SystemColors.Control;
                _btnOpenFolder.Enabled = false;
                return;
            }

            _btnOpenFolder.Enabled = (targets.Count == 1) && !string.IsNullOrEmpty(targets[0].FolderPath) && Directory.Exists(targets[0].FolderPath);

            var exclusions = GetCurrentProfileExclusions();
            int activeCount = targets.Count(t => !t.IsArchived);
            int archivedCount = targets.Count(t => t.IsArchived);
            int excludedCount = targets.Count(t => !t.IsArchived && exclusions.Contains(t.Id));
            int sharedCount = targets.Count(t => !t.IsArchived && !exclusions.Contains(t.Id));

            if (activeCount > 0)
            {
                _btnToggle.Enabled = true;
                if (targets.Count == 1)
                {
                    var p = targets[0];
                    if (exclusions.Contains(p.Id))
                    {
                        _btnToggle.Text = "🟢 Share with Profile";
                        _btnToggle.BackColor = Color.FromArgb(235, 245, 235);
                    }
                    else
                    {
                        _btnToggle.Text = "🟡 Exclude from Profile";
                        _btnToggle.BackColor = Color.FromArgb(255, 245, 230);
                    }
                }
                else
                {
                    if (sharedCount > 0)
                    {
                        _btnToggle.Text = $"🟡 Exclude Checked ({sharedCount})";
                        _btnToggle.BackColor = Color.FromArgb(255, 245, 230);
                    }
                    else
                    {
                        _btnToggle.Text = $"🟢 Share Checked ({excludedCount})";
                        _btnToggle.BackColor = Color.FromArgb(235, 245, 235);
                    }
                }
            }
            else
            {
                _btnToggle.Enabled = false;
                _btnToggle.Text = "🟡 Exclude from Profile";
                _btnToggle.BackColor = SystemColors.Control;
            }

            if (targets.Count == 1)
            {
                var p = targets[0];
                _btnArchiveRestore.Enabled = true;
                if (p.IsArchived)
                {
                    _btnArchiveRestore.Text = "🔄 Restore from Archive";
                    _btnArchiveRestore.BackColor = Color.FromArgb(235, 245, 235);
                }
                else
                {
                    _btnArchiveRestore.Text = "📦 Archive Globally";
                    _btnArchiveRestore.BackColor = Color.FromArgb(250, 240, 235);
                }
            }
            else
            {
                if (activeCount > 0 && archivedCount == 0)
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"📦 Archive Checked ({activeCount})";
                    _btnArchiveRestore.BackColor = Color.FromArgb(250, 240, 235);
                }
                else if (archivedCount > 0 && activeCount == 0)
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"🔄 Restore Checked ({archivedCount})";
                    _btnArchiveRestore.BackColor = Color.FromArgb(235, 245, 235);
                }
                else
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"📦 Archive/Restore ({targets.Count})";
                    _btnArchiveRestore.BackColor = Color.FromArgb(245, 245, 245);
                }
            }
        }

        private void OnToggleExcludeClick(object? sender, EventArgs e)
        {
            var targets = GetTargetProjects().Where(t => !t.IsArchived).ToList();
            if (targets.Count == 0) return;

            var exclusions = GetCurrentProfileExclusions();
            if (targets.Count == 1)
            {
                var p = targets[0];
                if (exclusions.Contains(p.Id)) exclusions.Remove(p.Id);
                else exclusions.Add(p.Id);
            }
            else
            {
                bool anyShared = targets.Any(t => !exclusions.Contains(t.Id));
                foreach (var t in targets)
                {
                    if (anyShared) exclusions.Add(t.Id);
                    else exclusions.Remove(t.Id);
                }
                _checkedProjectIds.Clear();
            }

            ApplyFilterAndRender();
        }

        private void SetSelectedProjectsExclusion(bool exclude)
        {
            var targets = GetTargetProjects().Where(t => !t.IsArchived).ToList();
            if (targets.Count == 0) return;

            var exclusions = GetCurrentProfileExclusions();
            foreach (var t in targets)
            {
                if (exclude) exclusions.Add(t.Id);
                else exclusions.Remove(t.Id);
            }
            _checkedProjectIds.Clear();

            ApplyFilterAndRender();
        }

        private void OnArchiveRestoreClick(object? sender, EventArgs e)
        {
            var targets = GetTargetProjects();
            if (targets.Count == 0) return;

            var activeTargets = targets.Where(t => !t.IsArchived).ToList();
            var archivedTargets = targets.Where(t => t.IsArchived).ToList();

            if (activeTargets.Count > 0 && archivedTargets.Count == 0)
            {
                string msg = activeTargets.Count == 1
                    ? Localization.T("confirma_archive", activeTargets[0].Name)
                    : $"Are you sure you want to archive {activeTargets.Count} project(s) globally?\nThey will be removed from Antigravity across ALL profiles.";

                var confirm = MessageBox.Show(this, msg, Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    int count = 0;
                    foreach (var t in activeTargets)
                    {
                        if (AntigravityCore.ArchiveProject(t.Id)) count++;
                    }
                    _checkedProjectIds.Clear();
                    ReloadProjectsData();
                    ApplyFilterAndRender();
                    MessageBox.Show(this, $"{count} project(s) archived successfully.", Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (archivedTargets.Count > 0 && activeTargets.Count == 0)
            {
                string msg = archivedTargets.Count == 1
                    ? Localization.T("confirma_unarchive", archivedTargets[0].Name)
                    : $"Restore {archivedTargets.Count} project(s) to active status?\nThey will be available across all profiles again.";

                var confirm = MessageBox.Show(this, msg, Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    int count = 0;
                    foreach (var t in archivedTargets)
                    {
                        if (AntigravityCore.UnarchiveProject(t.Id)) count++;
                    }
                    _checkedProjectIds.Clear();
                    ReloadProjectsData();
                    ApplyFilterAndRender();
                    MessageBox.Show(this, $"{count} project(s) restored successfully.", Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                var confirm = MessageBox.Show(this,
                    $"Process {targets.Count} project(s)?\n- {activeTargets.Count} will be archived\n- {archivedTargets.Count} will be restored",
                    Localization.T("t_confirma"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Yes)
                {
                    foreach (var t in activeTargets) AntigravityCore.ArchiveProject(t.Id);
                    foreach (var t in archivedTargets) AntigravityCore.UnarchiveProject(t.Id);
                    _checkedProjectIds.Clear();
                    ReloadProjectsData();
                    ApplyFilterAndRender();
                    MessageBox.Show(this, "Batch archive/restore completed successfully.", Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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

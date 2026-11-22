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
        private enum FilterMode { All, Shared, Unsynced, Excluded, Archived }

        private sealed class DisplayRow
        {
            public required ProjectInfo Project { get; init; }
            public required string StatusText { get; init; }
            public required Color StatusColor { get; init; }
            public required string PresenceText { get; init; }
            public required string FolderText { get; init; }
            public required bool FolderMissing { get; init; }
            public required bool IsUnsynced { get; init; }
        }

        private string _currentProfileName;
        private string _currentProfilePath = "";
        private List<ProfileInfo> _profiles = new();

        private Dictionary<string, ProjectInfo> _activeProjects = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ProjectInfo> _archivedProjects = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> _projectPresenceMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _profileExclusionsMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _checkedProjectIds = new(StringComparer.OrdinalIgnoreCase);

        private bool _isPopulating = false;
        private bool _isUpdatingProfileCombo = false;
        private bool _hasUnsavedChanges = false;
        private FilterMode _currentFilter = FilterMode.All;
        private string _searchFilter = "";
        private int _sortColumn = 1;
        private SortOrder _sortOrder = SortOrder.Ascending;

        // UI Controls
        private ComboBox _cbProfile = null!;
        private TextBox _txtSearch = null!;
        private Button _btnSyncAll = null!;
        private Button _btnRefresh = null!;
        private readonly ToolTip _toolTip = new();

        private Button _btnPillAll = null!;
        private Button _btnPillShared = null!;
        private Button _btnPillUnsynced = null!;
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
            _profiles = AntigravityCore.GetProfiles(syncVault: false);
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
            _hasUnsavedChanges = false;
        }

        private void ReloadProjectsData()
        {
            _activeProjects = AntigravityCore.GetAllProjects();
            _archivedProjects = AntigravityCore.GetArchivedProjects();
            _projectPresenceMap = AntigravityCore.GetProjectPresenceMap();
        }

        private void ReloadAllFromDisk()
        {
            _profiles = AntigravityCore.GetProfiles(syncVault: false);
            ReloadProjectsData();
            LoadAllExclusions();
            RefreshProfileComboBox();
        }

        private void RefreshProfileComboBox()
        {
            if (_cbProfile == null) return;
            _isUpdatingProfileCombo = true;
            try
            {
                _cbProfile.Items.Clear();
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
                }
                UpdateProfilePath();
            }
            finally
            {
                _isUpdatingProfileCombo = false;
            }
        }

        private HashSet<string> GetProfileExclusions(string profileName)
        {
            if (!_profileExclusionsMap.TryGetValue(profileName, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _profileExclusionsMap[profileName] = set;
            }
            return set;
        }

        private HashSet<string> GetCurrentProfileExclusions() => GetProfileExclusions(_currentProfileName);

        private int Dpi(int px) => (int)Math.Round(px * (DeviceDpi / 96.0f));

        private void InitializeComponent()
        {
            Text = "Manage Projects - AgVerse";
            Size = new Size(Dpi(920), Dpi(610));
            MinimumSize = new Size(Dpi(800), Dpi(500));
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            string iconPath = Path.Combine(AntigravityCore.BaseDir, "icon.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            FormClosing += OnFormClosingPromptUnsaved;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(Dpi(12)),
                RowCount = 4,
                ColumnCount = 1
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(38))); // Top bar
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(38))); // Filter pills bar
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // Projects Table
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(44))); // Action Toolbar

            // 1. Top Bar
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = Dpi(34),
                Margin = new Padding(0, 0, 0, Dpi(4))
            };

            var leftTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblProfile = new Label
            {
                Text = "Target Profile:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, Dpi(6), Dpi(8), 0)
            };

            _cbProfile = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = Dpi(155),
                Margin = new Padding(0, Dpi(2), 0, 0)
            };
            RefreshProfileComboBox();
            _cbProfile.SelectedIndexChanged += OnProfileChanged;

            var btnRefreshToken = new Button
            {
                Text = "↻ " + Localization.T("btn_refresh_token"),
                Height = Dpi(27),
                AutoSize = true,
                Padding = new Padding(Dpi(6), 0, Dpi(6), 0),
                Margin = new Padding(Dpi(8), Dpi(1), 0, 0),
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
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _txtSearch = new TextBox
            {
                Width = Dpi(155),
                PlaceholderText = "Search project or folder...",
                Margin = new Padding(0, Dpi(3), Dpi(6), 0)
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchFilter = _txtSearch.Text.Trim();
                ApplyFilterAndRender();
            };

            _btnSyncAll = new Button
            {
                Text = "⚡ " + Localization.T("btn_sync_all"),
                Height = Dpi(27),
                AutoSize = true,
                MinimumSize = new Size(Dpi(96), Dpi(27)),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, Dpi(1), Dpi(6), 0),
                BackColor = Color.FromArgb(240, 246, 255)
            };
            _btnSyncAll.Click += OnSyncAllClick;
            _toolTip.SetToolTip(_btnSyncAll, "Sync all shared projects across Master and every non-excluded profile");

            _btnRefresh = new Button
            {
                Text = "↻ " + Localization.T("btn_refresh"),
                Height = Dpi(27),
                AutoSize = true,
                MinimumSize = new Size(Dpi(86), Dpi(27)),
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, Dpi(1), 0, 0),
                BackColor = Color.FromArgb(245, 245, 248)
            };
            _btnRefresh.Click += OnRefreshClick;
            _toolTip.SetToolTip(_btnRefresh, "Reload profiles, project files, and sync status from disk");

            rightTop.Controls.Add(_txtSearch);
            rightTop.Controls.Add(_btnSyncAll);
            rightTop.Controls.Add(_btnRefresh);

            topPanel.Controls.Add(rightTop);
            topPanel.Controls.Add(leftTop);
            rootLayout.Controls.Add(topPanel, 0, 0);

            // 2. Filter Pills Bar
            var pillsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = Dpi(34),
                WrapContents = false,
                Margin = new Padding(0, 0, 0, Dpi(4))
            };

            _btnPillAll = CreatePillButton("All (0)", FilterMode.All);
            _btnPillShared = CreatePillButton("● Shared (0)", FilterMode.Shared);
            _btnPillUnsynced = CreatePillButton("⚡ Unsynced (0)", FilterMode.Unsynced);
            _btnPillExcluded = CreatePillButton("○ Excluded (0)", FilterMode.Excluded);
            _btnPillArchived = CreatePillButton("○ Archived (0)", FilterMode.Archived);

            _toolTip.SetToolTip(_btnPillUnsynced, "Projects whose disk presence across profiles does not match sharing/exclusion rules");

            var btnCheckAll = new Button
            {
                Text = "☑ Check All",
                Height = Dpi(28),
                AutoSize = true,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(Dpi(12), 0, Dpi(4), 0),
                BackColor = Color.FromArgb(245, 245, 245)
            };
            btnCheckAll.Click += (s, e) => SetAllVisibleChecked(true);

            var btnUncheckAll = new Button
            {
                Text = "☐ Clear",
                Height = Dpi(28),
                AutoSize = true,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, 0, Dpi(4), 0),
                BackColor = Color.FromArgb(245, 245, 245)
            };
            btnUncheckAll.Click += (s, e) => SetAllVisibleChecked(false);

            pillsPanel.Controls.Add(_btnPillAll);
            pillsPanel.Controls.Add(_btnPillShared);
            pillsPanel.Controls.Add(_btnPillUnsynced);
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

            _lvProjects.Columns.Add("Status", Dpi(130), HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Project Name ▲", Dpi(170), HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Profiles", Dpi(145), HorizontalAlignment.Left);
            _lvProjects.Columns.Add("Workspace Folder", Dpi(340), HorizontalAlignment.Left);
            _lvProjects.Columns.Add("ID", Dpi(82), HorizontalAlignment.Left);

            _lvProjects.SelectedIndexChanged += OnSelectedProjectChanged;
            _lvProjects.ItemChecked += OnItemChecked;
            _lvProjects.DoubleClick += (s, e) => OnOpenFolderClick(s, e);
            _lvProjects.ColumnClick += OnColumnClick;
            _lvProjects.Resize += (s, e) => AdjustColumnWidths();
            _lvProjects.SizeChanged += (s, e) => AdjustColumnWidths();
            _lvProjects.ColumnWidthChanged += (s, e) =>
            {
                if (e.ColumnIndex != 3) AdjustColumnWidths();
            };
            Shown += (s, e) => { AdjustColumnWidths(); AdjustSearchBoxWidth(); };
            Resize += (s, e) => AdjustSearchBoxWidth();

            // Context Menu
            _contextMenu = new ContextMenuStrip();
            var mnuShare = new ToolStripMenuItem("● Share in Selected Profile", null, (s, e) => SetSelectedProjectsExclusion(false));
            var mnuExclude = new ToolStripMenuItem("○ Exclude from Selected Profile", null, (s, e) => SetSelectedProjectsExclusion(true));
            var mnuShareAll = new ToolStripMenuItem("⚡ Share Across ALL Profiles && Sync", null, (s, e) => ApplyGlobalScopeForSelected(keepOnlyHere: false));
            var mnuKeepHereOnly = new ToolStripMenuItem("★ Keep ONLY in Selected Profile", null, (s, e) => ApplyGlobalScopeForSelected(keepOnlyHere: true));
            var mnuArchive = new ToolStripMenuItem("Archive Globally (Hide from All)", null, (s, e) => OnArchiveRestoreClick(s, e));
            var mnuCheckAll = new ToolStripMenuItem("☑ Check All Visible", null, (s, e) => SetAllVisibleChecked(true));
            var mnuClearChecks = new ToolStripMenuItem("☐ Uncheck All", null, (s, e) => SetAllVisibleChecked(false));
            var mnuOpen = new ToolStripMenuItem("Open Workspace Folder", null, (s, e) => OnOpenFolderClick(s, e));

            _contextMenu.Items.AddRange(new ToolStripItem[]
            {
                mnuShare,
                mnuExclude,
                new ToolStripSeparator(),
                mnuShareAll,
                mnuKeepHereOnly,
                new ToolStripSeparator(),
                mnuArchive,
                new ToolStripSeparator(),
                mnuCheckAll,
                mnuClearChecks,
                new ToolStripSeparator(),
                mnuOpen
            });

            _contextMenu.Opening += (s, e) =>
            {
                var targets = GetTargetProjects();
                bool hasTargets = targets.Count > 0;
                bool hasActive = targets.Any(t => !t.IsArchived);
                bool onlyArchived = hasTargets && targets.All(t => t.IsArchived);

                mnuShare.Enabled = hasActive;
                mnuExclude.Enabled = hasActive;
                mnuShareAll.Enabled = hasActive;
                mnuKeepHereOnly.Enabled = hasActive;
                mnuKeepHereOnly.Text = $"★ Keep ONLY in '{_currentProfileName}' (Exclude Others)";

                mnuArchive.Enabled = hasTargets;
                mnuArchive.Text = onlyArchived ? "↻ Restore from Archive" : "Archive Globally";

                mnuOpen.Enabled = targets.Count == 1 &&
                                  !string.IsNullOrEmpty(targets[0].FolderPath) &&
                                  Directory.Exists(targets[0].FolderPath);
            };
            _lvProjects.ContextMenuStrip = _contextMenu;

            rootLayout.Controls.Add(_lvProjects, 0, 2);

            // 4. Action Toolbar
            var actionPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = Dpi(36),
                Margin = new Padding(0, Dpi(6), 0, 0)
            };

            var leftActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _btnToggle = new Button
            {
                Text = "○ Exclude from Profile",
                Height = Dpi(30),
                AutoSize = true,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, 0, Dpi(8), 0),
                Enabled = false
            };
            _btnToggle.Click += OnToggleExcludeClick;

            _btnArchiveRestore = new Button
            {
                Text = "Archive Globally",
                Height = Dpi(30),
                AutoSize = true,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, 0, Dpi(8), 0),
                Enabled = false
            };
            _btnArchiveRestore.Click += OnArchiveRestoreClick;

            _btnOpenFolder = new Button
            {
                Text = "Open Folder",
                Height = Dpi(30),
                AutoSize = true,
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, 0, Dpi(8), 0),
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
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _btnSave = new Button
            {
                Text = "✓ Save && Sync All",
                Height = Dpi(30),
                AutoSize = true,
                Padding = new Padding(Dpi(12), 0, Dpi(12), 0),
                Margin = new Padding(0, 0, Dpi(8), 0),
                BackColor = Color.FromArgb(225, 238, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnSave.Click += OnSaveClick;

            var btnClose = new Button
            {
                Text = "Close",
                Width = Dpi(82),
                Height = Dpi(30),
                Margin = Padding.Empty,
                DialogResult = DialogResult.Cancel
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
                Height = Dpi(28),
                Padding = new Padding(Dpi(8), 0, Dpi(8), 0),
                Margin = new Padding(0, 0, Dpi(5), 0),
                FlatStyle = FlatStyle.Standard,
                BackColor = Color.FromArgb(245, 245, 245),
                Tag = mode
            };
            btn.Click += (s, e) =>
            {
                _currentFilter = mode;
                UpdatePillStyles(0);
                ApplyFilterAndRender();
            };
            return btn;
        }

        private void UpdatePillStyles(int unsyncedCount)
        {
            UpdatePillStyle(_btnPillAll, FilterMode.All, highlightAlert: false);
            UpdatePillStyle(_btnPillShared, FilterMode.Shared, highlightAlert: false);
            UpdatePillStyle(_btnPillUnsynced, FilterMode.Unsynced, highlightAlert: unsyncedCount > 0);
            UpdatePillStyle(_btnPillExcluded, FilterMode.Excluded, highlightAlert: false);
            UpdatePillStyle(_btnPillArchived, FilterMode.Archived, highlightAlert: false);
        }

        private void UpdatePillStyle(Button btn, FilterMode mode, bool highlightAlert)
        {
            if (_currentFilter == mode)
            {
                btn.BackColor = Color.FromArgb(220, 235, 252);
                btn.ForeColor = Color.FromArgb(9, 105, 218);
                btn.Font = new Font(Font, FontStyle.Bold);
            }
            else if (highlightAlert)
            {
                btn.BackColor = Color.FromArgb(255, 244, 224);
                btn.ForeColor = Color.FromArgb(180, 83, 9);
                btn.Font = new Font(Font, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(245, 245, 245);
                btn.ForeColor = SystemColors.ControlText;
                btn.Font = new Font(Font, FontStyle.Regular);
            }
        }

        private void OnProfileChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingProfileCombo) return;
            string? sel = _cbProfile.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(sel) && sel != _currentProfileName)
            {
                _currentProfileName = sel;
                _checkedProjectIds.Clear();
                UpdateProfilePath();
                ApplyFilterAndRender();
            }
        }

        private void ExecuteSyncAll(bool showSuccessMessage)
        {
            SaveAllExclusionsToDisk();
            var res = AntigravityCore.SyncAllProfiles();
            _checkedProjectIds.Clear();
            ReloadAllFromDisk();
            ApplyFilterAndRender();

            if (showSuccessMessage)
            {
                string msg = (res.FilesCopied > 0 || res.FilesRemoved > 0)
                    ? $"Synchronized projects across {res.ProfilesTouched} profile(s):\n• Copied/Updated: {res.FilesCopied}\n• Removed (excluded/archived): {res.FilesRemoved}" +
                      (res.SyncedProjectNames.Count > 0 ? $"\n\nProjects synced: {string.Join(", ", res.SyncedProjectNames)}" : "")
                    : $"All {_profiles.Count} profiles are already synchronized ({_activeProjects.Count} active projects).";

                MessageBox.Show(this, msg, "Sync All Profiles - AgVerse", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnSyncAllClick(object? sender, EventArgs e)
        {
            ExecuteSyncAll(showSuccessMessage: true);
        }

        private void OnRefreshClick(object? sender, EventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var saveAsk = MessageBox.Show(
                    this,
                    "You have unsaved exclusion edits in this window.\n\nSave them before reloading from disk?",
                    "Unsaved Changes - AgVerse",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (saveAsk == DialogResult.Cancel) return;
                if (saveAsk == DialogResult.Yes) SaveAllExclusionsToDisk();
            }

            _checkedProjectIds.Clear();
            ReloadAllFromDisk();
            ApplyFilterAndRender();

            int unsyncedCount = CountUnsyncedActiveProjects();
            if (unsyncedCount > 0)
            {
                var syncAsk = MessageBox.Show(
                    this,
                    $"Projects reloaded from disk.\n\nDetected {unsyncedCount} unsynced project(s) across profiles (see the '⚡ Unsynced' tab & 'Profiles' column).\n\nWould you like to sync all profiles right now?",
                    "Projects Refreshed - AgVerse",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (syncAsk == DialogResult.Yes)
                {
                    ExecuteSyncAll(showSuccessMessage: true);
                }
            }
            else
            {
                MessageBox.Show(
                    this,
                    $"Projects reloaded from disk.\nAll {_profiles.Count} profiles are in sync ({_activeProjects.Count} active project(s)).",
                    "Projects Refreshed - AgVerse",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
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

        private (string presenceText, bool isUnsyncedGlobally) EvaluateProjectPresence(ProjectInfo p)
        {
            if (p.IsArchived)
            {
                return ("Archived (Global)", false);
            }

            int total = _profiles.Count;
            var presentSet = _projectPresenceMap.TryGetValue(p.Id, out var s) ? s : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var excludedProfiles = new List<string>();
            var missingExpected = new List<string>();
            bool presentWhileExcluded = false;

            foreach (var prof in _profiles)
            {
                bool isExcl = GetProfileExclusions(prof.Name).Contains(p.Id);
                bool isOnDisk = presentSet.Contains(prof.Name);

                if (isExcl)
                {
                    excludedProfiles.Add(prof.Name);
                    if (isOnDisk) presentWhileExcluded = true;
                }
                else if (!isOnDisk)
                {
                    missingExpected.Add(prof.Name);
                }
            }

            bool isUnsynced = missingExpected.Count > 0 || presentWhileExcluded;

            if (presentSet.Count == total && excludedProfiles.Count == 0 && !isUnsynced)
            {
                return ($"All ({total}/{total})", false);
            }

            if (presentSet.Count == 1 && missingExpected.Count > 0)
            {
                string onlyProf = presentSet.First();
                return ($"1/{total} (Only: {onlyProf})", true);
            }

            if (excludedProfiles.Count > 0 && !isUnsynced)
            {
                return ($"{presentSet.Count}/{total} ({excludedProfiles.Count} excl.)", false);
            }

            if (isUnsynced)
            {
                return ($"{presentSet.Count}/{total} ({missingExpected.Count} missing)", true);
            }

            return ($"{presentSet.Count}/{total}", false);
        }

        private int CountUnsyncedActiveProjects()
        {
            int count = 0;
            foreach (var p in _activeProjects.Values)
            {
                if (EvaluateProjectPresence(p).isUnsyncedGlobally) count++;
            }
            return count;
        }

        private (string statusText, Color statusColor) EvaluateTargetProfileStatus(
            ProjectInfo p,
            HashSet<string> targetExclusions,
            bool isUnsyncedGlobally)
        {
            if (p.IsArchived)
            {
                return ("○ Archived", Color.Gray);
            }

            bool isExcl = targetExclusions.Contains(p.Id);
            bool presentHere = _projectPresenceMap.TryGetValue(p.Id, out var set) && set.Contains(_currentProfileName);

            if (isExcl)
            {
                return presentHere
                    ? ("○ Excl. (On Disk)", Color.FromArgb(190, 95, 0))
                    : ("○ Excluded", Color.FromArgb(190, 95, 0));
            }

            if (!presentHere)
            {
                return ("○ Pending Sync", Color.FromArgb(9, 105, 218));
            }

            if (isUnsyncedGlobally)
            {
                return ("◐ Partial Sync", Color.FromArgb(9, 105, 218));
            }

            return ("● Shared", Color.FromArgb(34, 139, 34));
        }

        private void ApplyFilterAndRender()
        {
            var exclusions = GetCurrentProfileExclusions();

            int totalActive = _activeProjects.Count;
            int totalArchived = _archivedProjects.Count;
            int excludedCount = _activeProjects.Keys.Count(k => exclusions.Contains(k));
            int sharedCount = totalActive - excludedCount;
            int unsyncedCount = CountUnsyncedActiveProjects();

            int onDiskInCurrent = _activeProjects.Values.Count(p =>
                _projectPresenceMap.TryGetValue(p.Id, out var set) && set.Contains(_currentProfileName));
            Text = $"Manage Projects - AgVerse  [{_currentProfileName}: {onDiskInCurrent} on disk]";

            _btnPillAll.Text = $"All ({totalActive + totalArchived})";
            _btnPillShared.Text = $"● Shared ({sharedCount})";
            _btnPillUnsynced.Text = $"⚡ Unsynced ({unsyncedCount})";
            _btnPillExcluded.Text = $"○ Excluded ({excludedCount})";
            _btnPillArchived.Text = $"○ Archived ({totalArchived})";
            UpdatePillStyles(unsyncedCount);

            if (unsyncedCount > 0)
            {
                _btnSyncAll.Text = $"⚡ {Localization.T("btn_sync_all")} ({unsyncedCount})";
                _btnSyncAll.BackColor = Color.FromArgb(220, 235, 252);
                _btnSyncAll.ForeColor = Color.FromArgb(9, 105, 218);
                _btnSyncAll.Font = new Font(Font, FontStyle.Bold);
            }
            else
            {
                _btnSyncAll.Text = "⚡ " + Localization.T("btn_sync_all");
                _btnSyncAll.BackColor = Color.FromArgb(245, 247, 250);
                _btnSyncAll.ForeColor = SystemColors.ControlText;
                _btnSyncAll.Font = new Font(Font, FontStyle.Regular);
            }

            var rows = new List<DisplayRow>();

            foreach (var p in _activeProjects.Values)
            {
                bool isExcluded = exclusions.Contains(p.Id);
                var (presenceText, isUnsyncedGlobally) = EvaluateProjectPresence(p);

                if (_currentFilter == FilterMode.Shared && isExcluded) continue;
                if (_currentFilter == FilterMode.Excluded && !isExcluded) continue;
                if (_currentFilter == FilterMode.Unsynced && !isUnsyncedGlobally) continue;
                if (_currentFilter == FilterMode.Archived) continue;

                if (!MatchesSearch(p, presenceText)) continue;

                var (statusText, statusColor) = EvaluateTargetProfileStatus(p, exclusions, isUnsyncedGlobally);
                bool missingFolder = !string.IsNullOrEmpty(p.FolderPath) && !Directory.Exists(p.FolderPath);
                string folderDisplay = string.IsNullOrEmpty(p.FolderPath)
                    ? "(Unknown path)"
                    : (missingFolder ? "⚠ [Missing] " + p.FolderPath : p.FolderPath);

                rows.Add(new DisplayRow
                {
                    Project = p,
                    StatusText = statusText,
                    StatusColor = statusColor,
                    PresenceText = presenceText,
                    FolderText = folderDisplay,
                    FolderMissing = missingFolder,
                    IsUnsynced = isUnsyncedGlobally
                });
            }

            if (_currentFilter == FilterMode.All || _currentFilter == FilterMode.Archived)
            {
                foreach (var p in _archivedProjects.Values)
                {
                    if (!MatchesSearch(p, "Archived")) continue;
                    bool missingFolder = !string.IsNullOrEmpty(p.FolderPath) && !Directory.Exists(p.FolderPath);
                    string folderDisplay = string.IsNullOrEmpty(p.FolderPath)
                        ? "(Unknown path)"
                        : (missingFolder ? "⚠ [Missing] " + p.FolderPath : p.FolderPath);

                    rows.Add(new DisplayRow
                    {
                        Project = p,
                        StatusText = "○ Archived",
                        StatusColor = Color.Gray,
                        PresenceText = "Archived (Global)",
                        FolderText = folderDisplay,
                        FolderMissing = missingFolder,
                        IsUnsynced = false
                    });
                }
            }

            SortRows(rows);
            UpdateColumnHeaders();

            _isPopulating = true;
            try
            {
                _lvProjects.BeginUpdate();
                _lvProjects.Items.Clear();

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var p = row.Project;

                    var item = new ListViewItem(row.StatusText)
                    {
                        UseItemStyleForSubItems = false,
                        Tag = p,
                        Checked = _checkedProjectIds.Contains(p.Id)
                    };
                    item.SubItems[0].ForeColor = row.StatusColor;
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

                    var subPresence = item.SubItems.Add(row.PresenceText);
                    if (row.IsUnsynced)
                    {
                        subPresence.ForeColor = Color.FromArgb(9, 105, 218);
                        subPresence.Font = new Font(_lvProjects.Font, FontStyle.Bold);
                    }

                    var subFolder = item.SubItems.Add(row.FolderText);
                    if (row.FolderMissing)
                    {
                        subFolder.ForeColor = Color.FromArgb(180, 83, 9);
                    }

                    item.SubItems.Add(p.Id.Length > 8 ? p.Id.Substring(0, 8) + "..." : p.Id);

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

        private void OnColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortOrder = SortOrder.Ascending;
            }
            ApplyFilterAndRender();
        }

        private void SortRows(List<DisplayRow> rows)
        {
            Comparison<DisplayRow> cmp = _sortColumn switch
            {
                0 => (a, b) => string.Compare(a.StatusText, b.StatusText, StringComparison.OrdinalIgnoreCase),
                2 => (a, b) => string.Compare(a.PresenceText, b.PresenceText, StringComparison.OrdinalIgnoreCase),
                3 => (a, b) => string.Compare(a.Project.FolderPath, b.Project.FolderPath, StringComparison.OrdinalIgnoreCase),
                4 => (a, b) => string.Compare(a.Project.Id, b.Project.Id, StringComparison.OrdinalIgnoreCase),
                _ => (a, b) => string.Compare(a.Project.Name, b.Project.Name, StringComparison.OrdinalIgnoreCase)
            };

            rows.Sort((a, b) =>
            {
                int res = cmp(a, b);
                if (res == 0)
                {
                    res = string.Compare(a.Project.Name, b.Project.Name, StringComparison.OrdinalIgnoreCase);
                }
                return _sortOrder == SortOrder.Descending ? -res : res;
            });
        }

        private void UpdateColumnHeaders()
        {
            if (_lvProjects.Columns.Count < 5) return;
            string[] baseTitles = { "Status", "Project Name", "Profiles", "Workspace Folder", "ID" };
            string arrow = _sortOrder == SortOrder.Descending ? " ▼" : " ▲";
            for (int i = 0; i < baseTitles.Length; i++)
            {
                _lvProjects.Columns[i].Text = i == _sortColumn ? baseTitles[i] + arrow : baseTitles[i];
            }
        }

        private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            if (_isPopulating) return;
            if (e.Item?.Tag is ProjectInfo p)
            {
                if (e.Item.Checked) _checkedProjectIds.Add(p.Id);
                else _checkedProjectIds.Remove(p.Id);
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
            if (_isAdjustingColumns || _lvProjects == null || _lvProjects.Columns.Count < 5) return;
            try
            {
                _isAdjustingColumns = true;
                int clientWidth = _lvProjects.ClientSize.Width;
                int fixedWidth = _lvProjects.Columns[0].Width + _lvProjects.Columns[1].Width +
                                 _lvProjects.Columns[2].Width + _lvProjects.Columns[4].Width;

                int remaining = clientWidth - fixedWidth - 4;
                if (remaining > 150)
                {
                    _lvProjects.Columns[3].Width = remaining;
                }
            }
            catch { }
            finally
            {
                _isAdjustingColumns = false;
            }
        }

        private void AdjustSearchBoxWidth()
        {
            if (_txtSearch == null) return;
            int extra = Math.Max(0, (ClientSize.Width - Dpi(820)) / 4);
            _txtSearch.Width = Math.Min(Dpi(210), Dpi(155) + extra);
        }

        private bool MatchesSearch(ProjectInfo p, string extraText)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return p.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   p.FolderPath.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   p.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   extraText.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
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
                _btnToggle.Text = "○ Exclude from Profile";
                _btnToggle.BackColor = SystemColors.Control;
                _btnArchiveRestore.Enabled = false;
                _btnArchiveRestore.Text = "Archive Globally";
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
                        _btnToggle.Text = "● Share with Profile";
                        _btnToggle.BackColor = Color.FromArgb(235, 245, 235);
                    }
                    else
                    {
                        _btnToggle.Text = "○ Exclude from Profile";
                        _btnToggle.BackColor = Color.FromArgb(255, 245, 230);
                    }
                }
                else
                {
                    if (sharedCount > 0)
                    {
                        _btnToggle.Text = $"○ Exclude Checked ({sharedCount})";
                        _btnToggle.BackColor = Color.FromArgb(255, 245, 230);
                    }
                    else
                    {
                        _btnToggle.Text = $"● Share Checked ({excludedCount})";
                        _btnToggle.BackColor = Color.FromArgb(235, 245, 235);
                    }
                }
            }
            else
            {
                _btnToggle.Enabled = false;
                _btnToggle.Text = "○ Exclude from Profile";
                _btnToggle.BackColor = SystemColors.Control;
            }

            if (targets.Count == 1)
            {
                var p = targets[0];
                _btnArchiveRestore.Enabled = true;
                if (p.IsArchived)
                {
                    _btnArchiveRestore.Text = "↻ Restore from Archive";
                    _btnArchiveRestore.BackColor = Color.FromArgb(235, 245, 235);
                }
                else
                {
                    _btnArchiveRestore.Text = "Archive Globally";
                    _btnArchiveRestore.BackColor = Color.FromArgb(250, 240, 235);
                }
            }
            else
            {
                if (activeCount > 0 && archivedCount == 0)
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"Archive Checked ({activeCount})";
                    _btnArchiveRestore.BackColor = Color.FromArgb(250, 240, 235);
                }
                else if (archivedCount > 0 && activeCount == 0)
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"↻ Restore Checked ({archivedCount})";
                    _btnArchiveRestore.BackColor = Color.FromArgb(235, 245, 235);
                }
                else
                {
                    _btnArchiveRestore.Enabled = true;
                    _btnArchiveRestore.Text = $"Archive/Restore ({targets.Count})";
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

            _hasUnsavedChanges = true;
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
            _hasUnsavedChanges = true;

            ApplyFilterAndRender();
        }

        private void ApplyGlobalScopeForSelected(bool keepOnlyHere)
        {
            var targets = GetTargetProjects().Where(t => !t.IsArchived).ToList();
            if (targets.Count == 0) return;

            string confirmMsg = keepOnlyHere
                ? $"Keep {targets.Count} selected project(s) ONLY in '{_currentProfileName}' and exclude/remove from all other profiles?"
                : $"Share {targets.Count} selected project(s) across ALL {_profiles.Count} profiles and sync immediately?";

            if (MessageBox.Show(this, confirmMsg, Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            foreach (var prof in _profiles)
            {
                var set = GetProfileExclusions(prof.Name);
                bool isTargetProfile = string.Equals(prof.Name, _currentProfileName, StringComparison.OrdinalIgnoreCase);

                foreach (var t in targets)
                {
                    if (keepOnlyHere && !isTargetProfile) set.Add(t.Id);
                    else set.Remove(t.Id);
                }
            }

            SaveAllExclusionsToDisk();
            AntigravityCore.SyncAllProfiles();
            _checkedProjectIds.Clear();
            ReloadAllFromDisk();
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

                if (MessageBox.Show(this, msg, Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    int count = 0;
                    foreach (var t in activeTargets)
                    {
                        if (AntigravityCore.ArchiveProject(t.Id)) count++;
                    }
                    _checkedProjectIds.Clear();
                    ReloadAllFromDisk();
                    ApplyFilterAndRender();
                    MessageBox.Show(this, $"{count} project(s) archived successfully.", Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (archivedTargets.Count > 0 && activeTargets.Count == 0)
            {
                string msg = archivedTargets.Count == 1
                    ? Localization.T("confirma_unarchive", archivedTargets[0].Name)
                    : $"Restore {archivedTargets.Count} project(s) to active status?\nThey will be available across all profiles again.";

                if (MessageBox.Show(this, msg, Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    int count = 0;
                    foreach (var t in archivedTargets)
                    {
                        if (AntigravityCore.UnarchiveProject(t.Id)) count++;
                    }
                    _checkedProjectIds.Clear();
                    ReloadAllFromDisk();
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
                    ReloadAllFromDisk();
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

        private void SaveAllExclusionsToDisk()
        {
            foreach (var (profName, exSet) in _profileExclusionsMap)
            {
                string profPath = Path.Combine(AntigravityCore.ProfilesDir, profName);
                if (Directory.Exists(profPath))
                {
                    AntigravityCore.SaveExclusions(profPath, exSet);
                }
            }
            _hasUnsavedChanges = false;
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            SaveAllExclusionsToDisk();
            var res = AntigravityCore.SyncAllProfiles();

            string details = (res.FilesCopied > 0 || res.FilesRemoved > 0)
                ? $"\n\nSynced {res.FilesCopied} project file(s) and removed {res.FilesRemoved} excluded/archived file(s) across {res.ProfilesTouched} profile(s)."
                : "";

            MessageBox.Show(
                this,
                "All project exclusions and sharing settings saved and synced!" + details,
                Localization.T("t_sucesso"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnFormClosingPromptUnsaved(object? sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK || !_hasUnsavedChanges) return;

            var ask = MessageBox.Show(
                this,
                "You have unsaved project exclusion changes.\n\nSave and sync across all profiles before closing?",
                "Unsaved Changes - AgVerse",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (ask == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
            else if (ask == DialogResult.Yes)
            {
                SaveAllExclusionsToDisk();
                AntigravityCore.SyncAllProfiles();
                DialogResult = DialogResult.OK;
            }
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

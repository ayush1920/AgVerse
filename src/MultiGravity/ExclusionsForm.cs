using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MultiGravity
{
    public class ExclusionsForm : Form
    {
        private readonly string _profileName;
        private readonly string _profilePath;
        private Dictionary<string, ProjectInfo> _allProjects = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, ProjectInfo> _archivedProjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _exclusionState = new(StringComparer.OrdinalIgnoreCase);

        private TabControl _tabControl = null!;
        private TabPage _tabExclusions = null!;
        private TabPage _tabArchived = null!;

        // Tab 1 controls (Exclusions)
        private TextBox _txtFilter = null!;
        private CheckedListBox _clbProjects = null!;
        private Button _btnArchiveSelected = null!;
        private List<ProjectInfo> _currentFilteredList = new();

        // Tab 2 controls (Archived)
        private TextBox _txtArchivedFilter = null!;
        private ListBox _lstArchived = null!;
        private Button _btnRestore = null!;
        private List<ProjectInfo> _currentArchivedFilteredList = new();

        public ExclusionsForm(string profileName, int initialTabIndex = 0)
        {
            _profileName = profileName;
            _profilePath = System.IO.Path.Combine(AntigravityCore.ProfilesDir, profileName);

            ReloadData();

            InitializeComponent();
            ApplyFilter();
            ApplyArchivedFilter();

            if (initialTabIndex > 0 && initialTabIndex < _tabControl.TabPages.Count)
            {
                _tabControl.SelectedIndex = initialTabIndex;
            }
        }

        private void ReloadData()
        {
            _allProjects = AntigravityCore.GetAllProjects();
            _archivedProjects = AntigravityCore.GetArchivedProjects();

            var currentExclusions = AntigravityCore.GetExclusions(_profilePath);
            _exclusionState.Clear();

            foreach (var pid in _allProjects.Keys)
            {
                _exclusionState[pid] = currentExclusions.Contains(pid);
            }
        }

        private void InitializeComponent()
        {
            Text = Localization.T("top_titulo", _profileName);
            Size = new Size(580, 640);
            MinimumSize = new Size(520, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _tabControl = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };

            // ==========================================
            // TAB 1: Profile Exclusions
            // ==========================================
            _tabExclusions = new TabPage(Localization.T("tab_exclusions"));
            _tabExclusions.Padding = new Padding(12);

            var exclusionsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1
            };
            exclusionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header labels
            exclusionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Search bar
            exclusionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Action buttons
            exclusionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List
            exclusionsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Save/Cancel buttons

            // 1. Header
            var headerPanel = new Panel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var lblTitle = new Label
            {
                Text = Localization.T("top_aviso", _profileName),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            var lblSub = new Label
            {
                Text = Localization.T("top_aviso_sub"),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Dock = DockStyle.Bottom,
                Padding = new Padding(0, 3, 0, 0)
            };
            headerPanel.Controls.Add(lblSub);
            headerPanel.Controls.Add(lblTitle);
            exclusionsLayout.Controls.Add(headerPanel, 0, 0);

            // 2. Filter
            var filterPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 0, 0, 6) };
            var lblFilter = new Label { Text = Localization.T("filtrar"), AutoSize = true, Location = new Point(0, 6) };
            _txtFilter = new TextBox { Location = new Point(50, 4), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Width = 480 };
            _txtFilter.TextChanged += (s, e) => ApplyFilter();
            filterPanel.Controls.Add(lblFilter);
            filterPanel.Controls.Add(_txtFilter);
            exclusionsLayout.Controls.Add(filterPanel, 0, 1);

            // 3. Action buttons (Exclude All, Share All, Archive Selected)
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            var btnExcludeAll = new Button { Text = Localization.T("marcar_todos"), AutoSize = true, Height = 28, Padding = new Padding(6, 0, 6, 0) };
            btnExcludeAll.Click += (s, e) =>
            {
                foreach (var p in _currentFilteredList) _exclusionState[p.Id] = true;
                UpdateCheckboxStates();
            };

            var btnShareAll = new Button { Text = Localization.T("desmarcar_todos"), AutoSize = true, Height = 28, Padding = new Padding(6, 0, 6, 0) };
            btnShareAll.Click += (s, e) =>
            {
                foreach (var p in _currentFilteredList) _exclusionState[p.Id] = false;
                UpdateCheckboxStates();
            };

            _btnArchiveSelected = new Button
            {
                Text = "📦 " + Localization.T("btn_archive"),
                AutoSize = true,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = Color.FromArgb(250, 240, 230)
            };
            _btnArchiveSelected.Click += OnArchiveSelectedClick;

            actionPanel.Controls.Add(btnExcludeAll);
            actionPanel.Controls.Add(btnShareAll);
            actionPanel.Controls.Add(_btnArchiveSelected);
            exclusionsLayout.Controls.Add(actionPanel, 0, 2);

            // 4. CheckedListBox
            _clbProjects = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
            _clbProjects.ItemCheck += (s, e) =>
            {
                if (e.Index >= 0 && e.Index < _currentFilteredList.Count)
                {
                    var proj = _currentFilteredList[e.Index];
                    _exclusionState[proj.Id] = (e.NewValue == CheckState.Checked);
                }
            };
            exclusionsLayout.Controls.Add(_clbProjects, 0, 3);

            // 5. Save and Cancel
            var footerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            var btnCancel = new Button { Text = Localization.T("btn_cancelar"), DialogResult = DialogResult.Cancel, Width = 90, Height = 32 };
            var btnSave = new Button { Text = Localization.T("btn_salvar"), Width = 140, Height = 32, BackColor = SystemColors.ButtonFace };
            btnSave.Click += OnSaveClick;

            footerPanel.Controls.Add(btnCancel);
            footerPanel.Controls.Add(btnSave);
            exclusionsLayout.Controls.Add(footerPanel, 0, 4);

            _tabExclusions.Controls.Add(exclusionsLayout);
            _tabControl.TabPages.Add(_tabExclusions);

            // ==========================================
            // TAB 2: Global Archived Projects
            // ==========================================
            _tabArchived = new TabPage(Localization.T("tab_archived", _archivedProjects.Count));
            _tabArchived.Padding = new Padding(12);

            var archivedLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            archivedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            archivedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filter
            archivedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List
            archivedLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Action button

            var archivedHeader = new Panel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            var lblArchivedTitle = new Label
            {
                Text = Localization.T("btn_global_archive"),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            var lblArchivedSub = new Label
            {
                Text = Localization.T("archived_sub"),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Dock = DockStyle.Bottom,
                Padding = new Padding(0, 3, 0, 0)
            };
            archivedHeader.Controls.Add(lblArchivedSub);
            archivedHeader.Controls.Add(lblArchivedTitle);
            archivedLayout.Controls.Add(archivedHeader, 0, 0);

            var archivedFilterPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 0, 0, 6) };
            var lblAFilter = new Label { Text = Localization.T("filtrar"), AutoSize = true, Location = new Point(0, 6) };
            _txtArchivedFilter = new TextBox { Location = new Point(50, 4), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Width = 480 };
            _txtArchivedFilter.TextChanged += (s, e) => ApplyArchivedFilter();
            archivedFilterPanel.Controls.Add(lblAFilter);
            archivedFilterPanel.Controls.Add(_txtArchivedFilter);
            archivedLayout.Controls.Add(archivedFilterPanel, 0, 1);

            _lstArchived = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false
            };
            _lstArchived.DoubleClick += (s, e) => OnRestoreClick(s, e);
            archivedLayout.Controls.Add(_lstArchived, 0, 2);

            var archivedFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            _btnRestore = new Button
            {
                Text = "🔄 " + Localization.T("btn_unarchive"),
                Width = 160,
                Height = 32,
                BackColor = Color.FromArgb(235, 245, 235),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnRestore.Click += OnRestoreClick;
            archivedFooter.Controls.Add(_btnRestore);
            archivedLayout.Controls.Add(archivedFooter, 0, 3);

            _tabArchived.Controls.Add(archivedLayout);
            _tabControl.TabPages.Add(_tabArchived);

            Controls.Add(_tabControl);
        }

        private void ApplyFilter()
        {
            string filter = _txtFilter.Text.Trim();
            _currentFilteredList = _allProjects.Values
                .Where(p => string.IsNullOrEmpty(filter) || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _clbProjects.BeginUpdate();
            _clbProjects.Items.Clear();

            for (int i = 0; i < _currentFilteredList.Count; i++)
            {
                var p = _currentFilteredList[i];
                bool isExcluded = _exclusionState.GetValueOrDefault(p.Id, false);
                _clbProjects.Items.Add($"[Exclude]  {p.Name}", isExcluded);
            }

            _clbProjects.EndUpdate();
        }

        private void ApplyArchivedFilter()
        {
            string filter = _txtArchivedFilter.Text.Trim();
            _currentArchivedFilteredList = _archivedProjects.Values
                .Where(p => string.IsNullOrEmpty(filter) || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _lstArchived.BeginUpdate();
            _lstArchived.Items.Clear();

            foreach (var p in _currentArchivedFilteredList)
            {
                _lstArchived.Items.Add($"📦  {p.Name}  ({p.Id})");
            }

            _lstArchived.EndUpdate();
            _tabArchived.Text = Localization.T("tab_archived", _archivedProjects.Count);
        }

        private void UpdateCheckboxStates()
        {
            _clbProjects.BeginUpdate();
            for (int i = 0; i < _currentFilteredList.Count; i++)
            {
                var p = _currentFilteredList[i];
                bool isExcluded = _exclusionState.GetValueOrDefault(p.Id, false);
                _clbProjects.SetItemChecked(i, isExcluded);
            }
            _clbProjects.EndUpdate();
        }

        private void OnArchiveSelectedClick(object? sender, EventArgs e)
        {
            int idx = _clbProjects.SelectedIndex;
            if (idx < 0 || idx >= _currentFilteredList.Count)
            {
                MessageBox.Show(this, "Please select a project from the list to archive.", Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var proj = _currentFilteredList[idx];
            var confirm = MessageBox.Show(
                this,
                Localization.T("confirma_archive", proj.Name),
                Localization.T("t_confirma"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                if (AntigravityCore.ArchiveProject(proj.Id))
                {
                    ReloadData();
                    ApplyFilter();
                    ApplyArchivedFilter();
                    MessageBox.Show(this, Localization.T("sucesso_archived", proj.Name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OnRestoreClick(object? sender, EventArgs e)
        {
            int idx = _lstArchived.SelectedIndex;
            if (idx < 0 || idx >= _currentArchivedFilteredList.Count)
            {
                MessageBox.Show(this, "Please select an archived project to restore.", Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var proj = _currentArchivedFilteredList[idx];
            var confirm = MessageBox.Show(
                this,
                Localization.T("confirma_unarchive", proj.Name),
                Localization.T("t_confirma"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm == DialogResult.Yes)
            {
                if (AntigravityCore.UnarchiveProject(proj.Id))
                {
                    ReloadData();
                    ApplyFilter();
                    ApplyArchivedFilter();
                    MessageBox.Show(this, Localization.T("sucesso_unarchived", proj.Name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            var excludedIds = _exclusionState.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            AntigravityCore.SaveExclusions(_profilePath, excludedIds);
            AntigravityCore.SyncProfileProjects(_profilePath);

            MessageBox.Show(
                this,
                Localization.T("sucesso_proj", _profileName),
                Localization.T("t_sucesso"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

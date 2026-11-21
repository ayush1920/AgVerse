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
        private readonly Dictionary<string, ProjectInfo> _allProjects;
        private readonly Dictionary<string, bool> _exclusionState;

        private TextBox _txtFilter = null!;
        private CheckedListBox _clbProjects = null!;
        private List<ProjectInfo> _currentFilteredList = new();

        public ExclusionsForm(string profileName)
        {
            _profileName = profileName;
            _profilePath = System.IO.Path.Combine(AntigravityCore.ProfilesDir, profileName);
            _allProjects = AntigravityCore.GetAllProjects();

            var currentExclusions = AntigravityCore.GetExclusions(_profilePath);
            _exclusionState = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var pid in _allProjects.Keys)
            {
                _exclusionState[pid] = currentExclusions.Contains(pid);
            }

            InitializeComponent();
            ApplyFilter();
        }

        private void InitializeComponent()
        {
            Text = Localization.T("top_titulo", _profileName);
            Size = new Size(540, 600);
            MinimumSize = new Size(480, 500);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 5,
                ColumnCount = 1
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header labels
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Search bar
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Action buttons
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Save/Cancel buttons

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
            mainPanel.Controls.Add(headerPanel, 0, 0);

            // 2. Filter
            var filterPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 0, 0, 6) };
            var lblFilter = new Label
            {
                Text = Localization.T("filtrar"),
                AutoSize = true,
                Location = new Point(0, 6)
            };
            _txtFilter = new TextBox
            {
                Location = new Point(50, 4),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Width = 450
            };
            _txtFilter.TextChanged += (s, e) => ApplyFilter();
            filterPanel.Controls.Add(lblFilter);
            filterPanel.Controls.Add(_txtFilter);
            mainPanel.Controls.Add(filterPanel, 0, 1);

            // 3. Action buttons (Exclude All / Share All)
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
            var btnExcludeAll = new Button
            {
                Text = Localization.T("marcar_todos"),
                AutoSize = true,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0)
            };
            btnExcludeAll.Click += (s, e) =>
            {
                foreach (var p in _currentFilteredList)
                {
                    _exclusionState[p.Id] = true;
                }
                UpdateCheckboxStates();
            };

            var btnShareAll = new Button
            {
                Text = Localization.T("desmarcar_todos"),
                AutoSize = true,
                Height = 28,
                Padding = new Padding(8, 0, 8, 0)
            };
            btnShareAll.Click += (s, e) =>
            {
                foreach (var p in _currentFilteredList)
                {
                    _exclusionState[p.Id] = false;
                }
                UpdateCheckboxStates();
            };

            actionPanel.Controls.Add(btnExcludeAll);
            actionPanel.Controls.Add(btnShareAll);
            mainPanel.Controls.Add(actionPanel, 0, 2);

            // 4. CheckedListBox
            _clbProjects = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };
            _clbProjects.ItemCheck += (s, e) =>
            {
                if (e.Index >= 0 && e.Index < _currentFilteredList.Count)
                {
                    var proj = _currentFilteredList[e.Index];
                    _exclusionState[proj.Id] = (e.NewValue == CheckState.Checked);
                }
            };
            mainPanel.Controls.Add(_clbProjects, 0, 3);

            // 5. Save and Cancel
            var footerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0)
            };
            var btnCancel = new Button
            {
                Text = Localization.T("btn_cancelar"),
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32
            };
            var btnSave = new Button
            {
                Text = Localization.T("btn_salvar"),
                Width = 140,
                Height = 32,
                BackColor = SystemColors.ButtonFace
            };
            btnSave.Click += OnSaveClick;

            footerPanel.Controls.Add(btnCancel);
            footerPanel.Controls.Add(btnSave);
            mainPanel.Controls.Add(footerPanel, 0, 4);

            Controls.Add(mainPanel);
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

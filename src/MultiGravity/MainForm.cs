using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MultiGravity
{
    public class MainForm : Form
    {
        private ComboBox _cbLanguage = null!;
        private GroupBox _grpCreate = null!;
        private Label _lblProfileName = null!;
        private TextBox _txtProfileName = null!;
        private Button _btnAddProfile = null!;
        private Label _lblInfoShared = null!;

        private GroupBox _grpProfiles = null!;
        private ListBox _lstProfiles = null!;
        private Label _lblProfilesPath = null!;

        private Button _btnLaunch = null!;
        private Button _btnExclusions = null!;
        private Button _btnGlobalArchive = null!;
        private Button _btnShortcut = null!;
        private Button _btnDelete = null!;

        public MainForm()
        {
            InitializeComponent();
            LoadProfiles();
            UpdateLocalizedTexts();
        }

        private void InitializeComponent()
        {
            Size = new Size(540, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            string iconPath = Path.Combine(AntigravityCore.BaseDir, "icon.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(AntigravityCore.BaseDir, "..", "..", "icon.ico");
            }
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            var rootPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                RowCount = 4,
                ColumnCount = 1
            };
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Language
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Create Profile
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Profiles List
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Action Buttons

            // 1. Language selector
            var langPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
            var lblLang = new Label { Text = "Language / Idioma:", AutoSize = true, Padding = new Padding(0, 5, 6, 0) };
            _cbLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            foreach (var kvp in Localization.LanguageOptions)
            {
                _cbLanguage.Items.Add(kvp.Key);
            }
            var currentKey = Localization.LanguageOptions.FirstOrDefault(x => x.Value == Localization.CurrentLanguage).Key ?? "English";
            _cbLanguage.SelectedItem = currentKey;
            _cbLanguage.SelectedIndexChanged += OnLanguageChanged;

            langPanel.Controls.Add(lblLang);
            langPanel.Controls.Add(_cbLanguage);
            rootPanel.Controls.Add(langPanel, 0, 0);

            // 2. Create Profile GroupBox
            _grpCreate = new GroupBox { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(12, 10, 14, 12), Margin = new Padding(0, 0, 0, 6) };
            var createLayout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2 };
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));

            _lblProfileName = new Label { Text = Localization.T("lbl_nome"), AutoSize = true, Anchor = AnchorStyles.Left };
            _txtProfileName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0) };
            _txtProfileName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { OnAddProfileClick(s, e); e.SuppressKeyPress = true; } };

            _btnAddProfile = new Button { Text = Localization.T("btn_add"), Dock = DockStyle.Fill, Height = 28, Margin = new Padding(4, 0, 8, 0) };
            _btnAddProfile.Click += OnAddProfileClick;

            _lblInfoShared = new Label
            {
                Text = Localization.T("info_default_shared"),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 32,
                Margin = new Padding(2, 6, 8, 0)
            };

            createLayout.Controls.Add(_lblProfileName, 0, 0);
            createLayout.Controls.Add(_txtProfileName, 1, 0);
            createLayout.Controls.Add(_btnAddProfile, 2, 0);
            createLayout.Controls.Add(_lblInfoShared, 0, 1);
            createLayout.SetColumnSpan(_lblInfoShared, 3);
            _grpCreate.Controls.Add(createLayout);
            rootPanel.Controls.Add(_grpCreate, 0, 1);

            // 3. Profiles List GroupBox
            _grpProfiles = new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(10, 10, 10, 8), Margin = new Padding(0, 4, 0, 8) };
            var profilesLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lstProfiles = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false
            };
            _lstProfiles.DoubleClick += (s, e) => OnLaunchProfileClick(s, e);
            profilesLayout.Controls.Add(_lstProfiles, 0, 0);

            // Profiles directory location bar
            var dirPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 4, 0, 0) };
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblDir = new Label { Text = "Path:", AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.Gray, Anchor = AnchorStyles.Left };
            _lblProfilesPath = new Label { Text = AntigravityCore.ProfilesDir, AutoEllipsis = true, AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8F), ForeColor = Color.DarkSlateGray, Anchor = AnchorStyles.Left };
            var lnkChange = new LinkLabel { Text = "Change...", AutoSize = true, Font = new Font("Segoe UI", 8F), Anchor = AnchorStyles.Right, Padding = new Padding(0, 0, 2, 0) };
            lnkChange.LinkClicked += OnChangeProfilesFolder;

            dirPanel.Controls.Add(lblDir, 0, 0);
            dirPanel.Controls.Add(_lblProfilesPath, 1, 0);
            dirPanel.Controls.Add(lnkChange, 2, 0);
            profilesLayout.Controls.Add(dirPanel, 0, 1);

            _grpProfiles.Controls.Add(profilesLayout);
            rootPanel.Controls.Add(_grpProfiles, 0, 2);

            // 4. Action Buttons
            var btnPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 5, ColumnCount = 1 };
            _btnLaunch = new Button
            {
                Text = Localization.T("btn_abrir"),
                Dock = DockStyle.Fill,
                Height = 38,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(230, 235, 245),
                Margin = new Padding(0, 2, 0, 4)
            };
            _btnLaunch.Click += OnLaunchProfileClick;

            _btnExclusions = new Button
            {
                Text = Localization.T("btn_proj"),
                Dock = DockStyle.Fill,
                Height = 32,
                Margin = new Padding(0, 2, 0, 4)
            };
            _btnExclusions.Click += OnManageExclusionsClick;

            _btnGlobalArchive = new Button
            {
                Text = Localization.T("btn_global_archive"),
                Dock = DockStyle.Fill,
                Height = 32,
                Margin = new Padding(0, 2, 0, 4)
            };
            _btnGlobalArchive.Click += OnGlobalArchiveClick;

            _btnShortcut = new Button
            {
                Text = Localization.T("btn_atalho"),
                Dock = DockStyle.Fill,
                Height = 30,
                Margin = new Padding(0, 2, 0, 4)
            };
            _btnShortcut.Click += OnCreateShortcutClick;

            _btnDelete = new Button
            {
                Text = Localization.T("btn_del"),
                Dock = DockStyle.Fill,
                Height = 30,
                ForeColor = Color.DarkRed,
                Margin = new Padding(0, 2, 0, 2)
            };
            _btnDelete.Click += OnDeleteProfileClick;

            btnPanel.Controls.Add(_btnLaunch, 0, 0);
            btnPanel.Controls.Add(_btnExclusions, 0, 1);
            btnPanel.Controls.Add(_btnGlobalArchive, 0, 2);
            btnPanel.Controls.Add(_btnShortcut, 0, 3);
            btnPanel.Controls.Add(_btnDelete, 0, 4);
            rootPanel.Controls.Add(btnPanel, 0, 3);

            Controls.Add(rootPanel);
        }

        private void OnChangeProfilesFolder(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select MultiGravity Profiles Folder",
                SelectedPath = AntigravityCore.ProfilesDir,
                UseDescriptionForTitle = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                AntigravityCore.SetProfilesDir(dlg.SelectedPath);
                _lblProfilesPath.Text = dlg.SelectedPath;
                LoadProfiles();
            }
        }

        private void LoadProfiles()
        {
            string? selectedName = GetSelectedProfileName();
            _lstProfiles.BeginUpdate();
            _lstProfiles.Items.Clear();

            var profiles = AntigravityCore.GetProfiles();
            int selectedIndex = -1;

            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                string display = $"{p.Name}  ({p.ProjectCount} {Localization.T("projetos_sufixo")})";
                _lstProfiles.Items.Add(display);

                if (selectedName != null && p.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (selectedIndex >= 0)
            {
                _lstProfiles.SelectedIndex = selectedIndex;
            }
            else if (_lstProfiles.Items.Count > 0)
            {
                _lstProfiles.SelectedIndex = 0;
            }

            _lstProfiles.EndUpdate();
        }

        private string? GetSelectedProfileName()
        {
            if (_lstProfiles.SelectedIndex < 0) return null;
            string item = _lstProfiles.SelectedItem?.ToString() ?? "";
            int idx = item.IndexOf("  (");
            return idx > 0 ? item.Substring(0, idx) : item;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (_cbLanguage.SelectedItem != null &&
                Localization.LanguageOptions.TryGetValue(_cbLanguage.SelectedItem.ToString()!, out string? code))
            {
                Localization.SetLanguage(code, AntigravityCore.ConfigFile);
                UpdateLocalizedTexts();
                LoadProfiles();
            }
        }

        private void UpdateLocalizedTexts()
        {
            Text = Localization.T("title");
            _grpCreate.Text = Localization.T("frame_create");
            _lblProfileName.Text = Localization.T("lbl_nome");
            _btnAddProfile.Text = Localization.T("btn_add");
            _lblInfoShared.Text = Localization.T("info_default_shared");
            _grpProfiles.Text = Localization.T("frame_perfis");
            _btnLaunch.Text = Localization.T("btn_abrir");
            _btnExclusions.Text = Localization.T("btn_proj");
            _btnGlobalArchive.Text = Localization.T("btn_global_archive");
            _btnShortcut.Text = Localization.T("btn_atalho");
            _btnDelete.Text = Localization.T("btn_del");
        }

        private void OnAddProfileClick(object? sender, EventArgs e)
        {
            string name = _txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_nome"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Path.GetInvalidFileNameChars().Any(name.Contains))
            {
                MessageBox.Show(this, "Profile name contains invalid characters.", Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                AntigravityCore.CreateProfile(name);
                _txtProfileName.Clear();
                LoadProfiles();
                MessageBox.Show(this, Localization.T("sucesso_criado", name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnLaunchProfileClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AntigravityCore.LaunchProfile(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to launch Antigravity: " + ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnManageExclusionsClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new ExclusionsForm(name);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                LoadProfiles();
            }
        }

        private void OnGlobalArchiveClick(object? sender, EventArgs e)
        {
            string profileName = GetSelectedProfileName() ?? "Default";
            using var form = new ExclusionsForm(profileName, initialTabIndex: 1);
            form.ShowDialog(this);
            LoadProfiles();
        }

        private void OnCreateShortcutClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AntigravityCore.CreateDesktopShortcut(name);
                MessageBox.Show(this, Localization.T("sucesso_atalho", name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to create shortcut: " + ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnDeleteProfileClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(this, Localization.T("confirma_del", name), Localization.T("t_confirma"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm == DialogResult.Yes)
            {
                try
                {
                    AntigravityCore.DeleteProfile(name);
                    LoadProfiles();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to delete profile: " + ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

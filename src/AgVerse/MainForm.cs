using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AgVerse
{
    public class MainForm : Form
    {
        private Button _btnGitHub = null!;
        private ComboBox _cbLanguage = null!;
        private GroupBox _grpCreate = null!;
        private Label _lblProfileName = null!;
        private TextBox _txtProfileName = null!;
        private Button _btnSaveProfile = null!;
        private Button _btnAddProfile = null!;
        private Label _lblInfoShared = null!;

        private GroupBox _grpProfiles = null!;
        private ListBox _lstProfiles = null!;
        private Label _lblProfilesPath = null!;
        private Label _lblTokenInfo = null!;

        private Button _btnLaunch = null!;
        private Button _btnSetDefault = null!;
        private Button _btnManageProjects = null!;
        private Button _btnRefreshToken = null!;
        private Button _btnShortcut = null!;
        private Button _btnDelete = null!;
        private CheckBox _chkVerbose = null!;
        private readonly ToolTip _toolTip = new();

        public MainForm()
        {
            InitializeComponent();
            LoadProfiles();
            UpdateLocalizedTexts();
            Activated += (s, e) => UpdateSelectedProfileTokenInfo();
        }

        private int Dpi(int px) => (int)Math.Round(px * (DeviceDpi / 96.0f));

        private static Button CreateUniformButton(
            string text,
            int height,
            Color backColor,
            Color foreColor,
            Color borderColor,
            Color hoverBackColor,
            bool bold = false,
            Padding? margin = null)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 9F, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = margin ?? Padding.Empty
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = borderColor;
            btn.FlatAppearance.MouseOverBackColor = hoverBackColor;
            return btn;
        }

        private void InitializeComponent()
        {
            Size = new Size(Dpi(580), Dpi(660));
            MinimumSize = new Size(Dpi(520), Dpi(520));
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(247, 248, 250);
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
                Padding = new Padding(Dpi(10), Dpi(6), Dpi(10), Dpi(10)),
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 0: Language + GitHub
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 1: Create Profile
            rootPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // 2: Profiles List
            rootPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 3: Options & Action Buttons

            // 1. Top Header: Language Selector + GitHub Link Button
            var topHeaderPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, Dpi(3)),
                Padding = Padding.Empty
            };
            topHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var langPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var lblLang = new Label
            {
                Text = "Language / Idioma:",
                AutoSize = true,
                Padding = new Padding(0, Dpi(4), Dpi(6), 0),
                Margin = Padding.Empty
            };
            _cbLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = Dpi(135),
                Margin = new Padding(0, Dpi(1), 0, 0)
            };
            foreach (var kvp in Localization.LanguageOptions)
            {
                _cbLanguage.Items.Add(kvp.Key);
            }
            var currentKey = Localization.LanguageOptions.FirstOrDefault(x => x.Value == Localization.CurrentLanguage).Key ?? "English";
            _cbLanguage.SelectedItem = currentKey;
            _cbLanguage.SelectedIndexChanged += OnLanguageChanged;

            langPanel.Controls.Add(lblLang);
            langPanel.Controls.Add(_cbLanguage);

            _btnGitHub = new Button
            {
                Text = "GitHub",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(36, 41, 47),
                Height = Dpi(26),
                Width = Dpi(90),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Margin = Padding.Empty,
                Padding = new Padding(Dpi(18), 0, 0, 0)
            };
            _btnGitHub.FlatAppearance.BorderColor = Color.FromArgb(208, 215, 222);
            _btnGitHub.FlatAppearance.BorderSize = 1;
            _btnGitHub.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
            _btnGitHub.Paint += (s, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float dpiScale = pe.Graphics.DpiX / 96.0f;
                int iconTargetSize = dpiScale > 1.25f ? (int)Math.Round(16.0f * dpiScale) : 16;
                var icon = LoadGitHubIcon(dpiScale > 1.25f);
                if (icon != null)
                {
                    int x = Math.Max(7, (int)Math.Round(8.0f * dpiScale));
                    int y = (_btnGitHub.ClientSize.Height - iconTargetSize) / 2;
                    pe.Graphics.DrawImage(icon, new Rectangle(x, y, iconTargetSize, iconTargetSize));
                }
            };
            _btnGitHub.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/ayush1920/AgVerse") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not open repository link: {ex.Message}", "AgVerse", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _toolTip.SetToolTip(_btnGitHub, "View AgVerse source repository on GitHub (ayush1920/AgVerse)");

            topHeaderPanel.Controls.Add(langPanel, 0, 0);
            topHeaderPanel.Controls.Add(_btnGitHub, 1, 0);
            rootPanel.Controls.Add(topHeaderPanel, 0, 0);

            // 2. Create Profile GroupBox (compact top padding)
            _grpCreate = new GroupBox
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(Dpi(10), Dpi(4), Dpi(10), Dpi(6)),
                Margin = new Padding(0, 0, 0, Dpi(4))
            };
            var createLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi(75)));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi(75)));

            _lblProfileName = new Label
            {
                Text = Localization.T("lbl_nome"),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, Dpi(6), 0)
            };
            _txtProfileName = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, Dpi(1), Dpi(6), Dpi(1))
            };
            _txtProfileName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    string? cur = GetSelectedProfileName();
                    string text = _txtProfileName.Text.Trim();
                    if (!string.IsNullOrEmpty(cur) && !string.Equals(cur, text, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(AntigravityCore.ProfilesDir, cur)))
                    {
                        OnSaveProfileClick(s, e);
                    }
                    else
                    {
                        OnAddProfileClick(s, e);
                    }
                    e.SuppressKeyPress = true;
                }
            };

            _btnSaveProfile = CreateUniformButton(
                Localization.T("btn_save"),
                Dpi(25),
                Color.White,
                Color.FromArgb(31, 35, 40),
                Color.FromArgb(208, 215, 222),
                Color.FromArgb(243, 244, 246),
                bold: false,
                margin: new Padding(0, 0, Dpi(6), 0));
            _btnSaveProfile.Click += OnSaveProfileClick;

            _btnAddProfile = CreateUniformButton(
                Localization.T("btn_add"),
                Dpi(25),
                Color.White,
                Color.FromArgb(31, 35, 40),
                Color.FromArgb(208, 215, 222),
                Color.FromArgb(243, 244, 246),
                bold: false,
                margin: Padding.Empty);
            _btnAddProfile.Click += OnAddProfileClick;

            _lblInfoShared = new Label
            {
                Text = Localization.T("info_default_shared"),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Margin = new Padding(0, Dpi(4), 0, 0)
            };

            createLayout.Controls.Add(_lblProfileName, 0, 0);
            createLayout.Controls.Add(_txtProfileName, 1, 0);
            createLayout.Controls.Add(_btnSaveProfile, 2, 0);
            createLayout.Controls.Add(_btnAddProfile, 3, 0);
            createLayout.Controls.Add(_lblInfoShared, 0, 1);
            createLayout.SetColumnSpan(_lblInfoShared, 4);
            _grpCreate.Controls.Add(createLayout);
            rootPanel.Controls.Add(_grpCreate, 0, 1);

            // 3. Profiles List GroupBox (expanded vertical space)
            _grpProfiles = new GroupBox
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(Dpi(10), Dpi(6), Dpi(10), Dpi(8)),
                Margin = new Padding(0, 0, 0, Dpi(6))
            };
            var profilesLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            profilesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lstProfiles = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = Dpi(26),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = Padding.Empty
            };
            _lstProfiles.DrawItem += OnDrawProfileItem;
            _lstProfiles.DoubleClick += (s, e) => OnLaunchProfileClick(s, e);
            _lstProfiles.SelectedIndexChanged += (s, e) =>
            {
                string? sel = GetSelectedProfileName();
                if (!string.IsNullOrEmpty(sel))
                {
                    _txtProfileName.Text = sel;
                }
                UpdateSelectedProfileTokenInfo();
            };
            profilesLayout.Controls.Add(_lstProfiles, 0, 0);

            // Token info status bar
            _lblTokenInfo = new Label
            {
                Text = "🔑 Account: Select a profile...",
                AutoSize = false,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Height = Dpi(20),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.DarkSlateGray,
                Margin = new Padding(0, Dpi(5), 0, Dpi(1))
            };
            profilesLayout.Controls.Add(_lblTokenInfo, 0, 1);

            // Profiles directory location bar
            var dirPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = Dpi(20),
                ColumnCount = 3,
                Margin = new Padding(0, Dpi(1), 0, 0),
                Padding = Padding.Empty
            };
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            dirPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblDir = new Label
            {
                Text = "Path:",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, Dpi(4), 0)
            };
            _lblProfilesPath = new Label
            {
                Text = AntigravityCore.ProfilesDir,
                AutoEllipsis = true,
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.DarkSlateGray,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
            var lnkChange = new LinkLabel
            {
                Text = "Change...",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(Dpi(6), 0, 0, 0)
            };
            lnkChange.LinkClicked += OnChangeProfilesFolder;

            _toolTip.SetToolTip(_lblProfilesPath, AntigravityCore.ProfilesDir);
            _toolTip.SetToolTip(lblDir, AntigravityCore.ProfilesDir);

            dirPanel.Controls.Add(lblDir, 0, 0);
            dirPanel.Controls.Add(_lblProfilesPath, 1, 0);
            dirPanel.Controls.Add(lnkChange, 2, 0);
            profilesLayout.Controls.Add(dirPanel, 0, 2);

            _grpProfiles.Controls.Add(profilesLayout);
            rootPanel.Controls.Add(_grpProfiles, 0, 2);

            // 4. Options & Uniform 2-Column Action Grid
            var bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                RowCount = 2,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _chkVerbose = new CheckBox
            {
                Text = Localization.T("chk_verbose"),
                AutoSize = true,
                Checked = AntigravityCore.GetVerboseModeConfig(),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Margin = new Padding(Dpi(2), 0, 0, Dpi(6))
            };
            _chkVerbose.CheckedChanged += OnVerboseModeChanged;

            var btnGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            btnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            btnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            btnGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            int btnHeight = Dpi(34);
            int colGap = Dpi(4);
            int rowGap = Dpi(6);

            _btnLaunch = CreateUniformButton(
                Localization.T("btn_abrir"),
                btnHeight,
                Color.FromArgb(9, 105, 218),
                Color.White,
                Color.FromArgb(8, 90, 190),
                Color.FromArgb(7, 88, 188),
                bold: true,
                margin: new Padding(0, 0, colGap, rowGap));
            _btnLaunch.Click += OnLaunchProfileClick;

            _btnSetDefault = CreateUniformButton(
                Localization.T("btn_set_default"),
                btnHeight,
                Color.White,
                Color.FromArgb(146, 94, 0),
                Color.FromArgb(224, 198, 124),
                Color.FromArgb(255, 250, 235),
                bold: true,
                margin: new Padding(colGap, 0, 0, rowGap));
            _btnSetDefault.Click += OnSetDefaultClick;

            _btnManageProjects = CreateUniformButton(
                "📂 " + Localization.T("btn_proj"),
                btnHeight,
                Color.White,
                Color.FromArgb(31, 35, 40),
                Color.FromArgb(208, 215, 222),
                Color.FromArgb(243, 244, 246),
                bold: false,
                margin: new Padding(0, 0, colGap, rowGap));
            _btnManageProjects.Click += OnManageProjectsClick;

            _btnRefreshToken = CreateUniformButton(
                "🔄 " + Localization.T("btn_refresh_token"),
                btnHeight,
                Color.White,
                Color.FromArgb(31, 35, 40),
                Color.FromArgb(208, 215, 222),
                Color.FromArgb(243, 244, 246),
                bold: false,
                margin: new Padding(colGap, 0, 0, rowGap));
            _btnRefreshToken.Click += OnRefreshTokenClick;

            _btnShortcut = CreateUniformButton(
                Localization.T("btn_atalho"),
                btnHeight,
                Color.White,
                Color.FromArgb(31, 35, 40),
                Color.FromArgb(208, 215, 222),
                Color.FromArgb(243, 244, 246),
                bold: false,
                margin: new Padding(0, 0, colGap, 0));
            _btnShortcut.Click += OnCreateShortcutClick;

            _btnDelete = CreateUniformButton(
                Localization.T("btn_del"),
                btnHeight,
                Color.White,
                Color.FromArgb(207, 34, 46),
                Color.FromArgb(238, 195, 200),
                Color.FromArgb(255, 240, 242),
                bold: false,
                margin: new Padding(colGap, 0, 0, 0));
            _btnDelete.Click += OnDeleteProfileClick;

            btnGrid.Controls.Add(_btnLaunch, 0, 0);
            btnGrid.Controls.Add(_btnSetDefault, 1, 0);
            btnGrid.Controls.Add(_btnManageProjects, 0, 1);
            btnGrid.Controls.Add(_btnRefreshToken, 1, 1);
            btnGrid.Controls.Add(_btnShortcut, 0, 2);
            btnGrid.Controls.Add(_btnDelete, 1, 2);

            bottomPanel.Controls.Add(_chkVerbose, 0, 0);
            bottomPanel.Controls.Add(btnGrid, 0, 1);
            rootPanel.Controls.Add(bottomPanel, 0, 3);

            Controls.Add(rootPanel);
        }

        private static Image? _githubIcon16;
        private static Image? _githubIcon32;

        private Image? LoadGitHubIcon(bool highDpi = false)
        {
            if (highDpi && _githubIcon32 != null) return _githubIcon32;
            if (!highDpi && _githubIcon16 != null) return _githubIcon16;

            string preferredName = highDpi ? "github_32.png" : "github_16.png";
            string fallbackName = highDpi ? "github_24.png" : "github_20.png";

            Image? img = LoadIconFromResource(preferredName) 
                      ?? LoadIconFromFile(preferredName) 
                      ?? LoadIconFromResource(fallbackName) 
                      ?? LoadIconFromFile(fallbackName);

            if (highDpi) _githubIcon32 = img;
            else _githubIcon16 = img;

            return img;
        }

        private static Image? LoadIconFromResource(string fileName)
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                var resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                if (resName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) return Image.FromStream(stream);
                }
            }
            catch { }
            return null;
        }

        private static Image? LoadIconFromFile(string fileName)
        {
            string[] candidates = [
                Path.Combine(AntigravityCore.BaseDir, fileName),
                Path.Combine(AntigravityCore.BaseDir, "..", "..", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)
            ];
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try { return Image.FromFile(path); } catch { }
                }
            }
            return null;
        }

        private void OnChangeProfilesFolder(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select AgVerse Profiles Folder",
                SelectedPath = AntigravityCore.ProfilesDir,
                UseDescriptionForTitle = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                AntigravityCore.SetProfilesDir(dlg.SelectedPath);
                _lblProfilesPath.Text = dlg.SelectedPath;
                _toolTip.SetToolTip(_lblProfilesPath, dlg.SelectedPath);
                LoadProfiles();
            }
        }

        private void LoadProfiles(string? selectName = null)
        {
            string? selectedName = selectName ?? GetSelectedProfileName();
            _lstProfiles.BeginUpdate();
            _lstProfiles.Items.Clear();

            var profiles = AntigravityCore.GetProfiles();
            int selectedIndex = -1;

            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                bool isActive = VaultBridge.IsProfileActiveInIde(p.Name, p.Path);
                string defaultBadge = isActive ? "  " + Localization.T("badge_default") : "";
                string display = $"{p.Name}{defaultBadge}  ({p.ProjectCount} {Localization.T("projetos_sufixo")})";
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
            UpdateSelectedProfileTokenInfo();
        }

        private void OnDrawProfileItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstProfiles.Items.Count) return;

            string text = _lstProfiles.Items[e.Index]?.ToString() ?? "";
            bool isDefault = text.Contains(Localization.T("badge_default")) || text.Contains("★");
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // Soft-tinted selection background or alternating rows
            Color bgColor;
            if (isSelected)
            {
                bgColor = isDefault ? Color.FromArgb(235, 248, 238) : Color.FromArgb(235, 245, 255);
            }
            else
            {
                bgColor = e.Index % 2 == 0 ? Color.White : Color.FromArgb(250, 252, 255);
            }

            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            // Selection border
            if (isSelected)
            {
                Color borderColor = isDefault ? Color.FromArgb(140, 210, 160) : Color.FromArgb(160, 200, 245);
                using var pen = new Pen(borderColor);
                e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            // Default profile in Green, rest of profiles in Blue
            Color textColor = isDefault ? Color.FromArgb(16, 124, 65) : Color.FromArgb(9, 105, 218);
            using var font = new Font(e.Font ?? _lstProfiles.Font, isDefault ? FontStyle.Bold : FontStyle.Regular);

            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, font, textRect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void UpdateSelectedProfileTokenInfo()
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                _lblTokenInfo.Text = "🔑 Account: No profile selected";
                _lblTokenInfo.ForeColor = Color.Gray;
                _btnRefreshToken.Enabled = false;
                _btnSetDefault.Enabled = false;
                return;
            }

            _btnRefreshToken.Enabled = true;
            _btnSetDefault.Enabled = true;
            string profilePath = Path.Combine(AntigravityCore.ProfilesDir, name);
            var (email, hasToken, isExpired, expiryText) = TokenService.GetTokenSummary(profilePath);

            bool isActive = VaultBridge.IsProfileActiveInIde(name, profilePath);
            string activeTag = isActive ? "  •  " + Localization.T("badge_default") : "";

            // For default show in Green, rest of profiles in Blue
            Color activeColor = isActive ? Color.FromArgb(16, 124, 65) : Color.FromArgb(9, 105, 218);

            if (!hasToken)
            {
                _lblTokenInfo.Text = $"🔑 Account: None linked (click Refresh Token to sign in){activeTag}";
                _lblTokenInfo.ForeColor = activeColor;
            }
            else if (isExpired)
            {
                _lblTokenInfo.Text = $"🔑 {email}  •  Status: Expired (click Refresh Token){activeTag}";
                _lblTokenInfo.ForeColor = Color.FromArgb(190, 70, 0);
            }
            else
            {
                _lblTokenInfo.Text = $"🔑 {email}  •  Status: {expiryText}{activeTag}";
                _lblTokenInfo.ForeColor = activeColor;
            }
        }

        private string? GetSelectedProfileName()
        {
            if (_lstProfiles.SelectedIndex < 0) return null;
            string item = _lstProfiles.SelectedItem?.ToString() ?? "";
            int idx = item.IndexOf("  (");
            string namePart = idx > 0 ? item.Substring(0, idx) : item;
            int badgeIdx = namePart.IndexOf("  ⭐");
            if (badgeIdx > 0) namePart = namePart.Substring(0, badgeIdx);
            return namePart.Trim();
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
            _btnSaveProfile.Text = Localization.T("btn_save");
            _btnAddProfile.Text = Localization.T("btn_add");
            _lblInfoShared.Text = Localization.T("info_default_shared");
            _grpProfiles.Text = Localization.T("frame_perfis");
            _chkVerbose.Text = Localization.T("chk_verbose");
            _btnLaunch.Text = Localization.T("btn_abrir");
            _btnSetDefault.Text = Localization.T("btn_set_default");
            _btnManageProjects.Text = "📂 " + Localization.T("btn_proj");
            _btnRefreshToken.Text = "🔄 " + Localization.T("btn_refresh_token");
            _btnShortcut.Text = Localization.T("btn_atalho");
            _btnDelete.Text = Localization.T("btn_del");
        }

        private void OnSetDefaultClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string profilePath = Path.Combine(AntigravityCore.ProfilesDir, name);
            var (email, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);

            bool success = VaultBridge.SetActiveAccount(hasToken && !string.IsNullOrEmpty(email) ? email : name);
            if (success)
            {
                LoadProfiles();
                UpdateSelectedProfileTokenInfo();
                MessageBox.Show(this, Localization.T("msg_default_set", name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Could not set as default profile. Ensure the profile has an account linked or token refreshed.", Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnVerboseModeChanged(object? sender, EventArgs e)
        {
            bool isVerbose = _chkVerbose.Checked;
            AntigravityCore.SetVerboseModeConfig(isVerbose);
            AntigravityCore.SetVerboseModeForAllProfiles(isVerbose);
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
                LoadProfiles(name);
                MessageBox.Show(this, Localization.T("sucesso_criado", name), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnSaveProfileClick(object? sender, EventArgs e)
        {
            string newName = _txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show(this, Localization.T("aviso_nome"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Path.GetInvalidFileNameChars().Any(newName.Contains))
            {
                MessageBox.Show(this, "Profile name contains invalid characters.", Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string? currentSelected = GetSelectedProfileName();
            if (string.IsNullOrEmpty(currentSelected))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(currentSelected, newName, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                AntigravityCore.RenameProfile(currentSelected, newName);
                LoadProfiles(newName);
                MessageBox.Show(this, Localization.T("sucesso_renomeado", currentSelected, newName), Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void OnManageProjectsClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            using var form = new ProjectsManagerForm(name);
            form.ShowDialog(this);
            LoadProfiles();
        }

        private async void OnRefreshTokenClick(object? sender, EventArgs e)
        {
            string? name = GetSelectedProfileName();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, Localization.T("aviso_selecao"), Localization.T("t_aviso"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string profilePath = Path.Combine(AntigravityCore.ProfilesDir, name);
            if (!Directory.Exists(profilePath))
            {
                MessageBox.Show(this, "Profile folder not found.", Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _btnRefreshToken.Enabled = false;
            string origText = _btnRefreshToken.Text;
            _btnRefreshToken.Text = "⏳ Refreshing token...";

            try
            {
                var (success, message) = await TokenService.RefreshTokenForProfileAsync(profilePath, name);
                if (success)
                {
                    UpdateSelectedProfileTokenInfo();
                    MessageBox.Show(this, message, Localization.T("t_sucesso"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        _btnRefreshToken.Text = "⏳ Waiting for browser sign-in...";
                        var (authSuccess, authMsg) = await TokenService.AuthenticateInBrowserAsync(profilePath, name);
                        UpdateSelectedProfileTokenInfo();
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
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error refreshing token: " + ex.Message, Localization.T("t_erro"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRefreshToken.Enabled = true;
                _btnRefreshToken.Text = origText;
                UpdateSelectedProfileTokenInfo();
            }
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
                    string profilePath = Path.Combine(AntigravityCore.ProfilesDir, name);
                    string? email = null;
                    if (Directory.Exists(profilePath))
                    {
                        var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);
                        if (hasToken) email = tEmail;
                    }

                    AntigravityCore.DeleteProfile(name);
                    VaultBridge.RemoveAccount(email, name);
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

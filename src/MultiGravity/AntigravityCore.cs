using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MultiGravity
{
    public record ProjectInfo(string Id, string Name, string Filename, string SourcePath, string FolderPath = "", bool IsArchived = false);
    public record ProfileInfo(string Name, int ProjectCount, string Path);

    public static class AntigravityCore
    {
        public static string BaseDir { get; } = DetermineBaseDir();
        public static string ConfigFile { get; } = Path.Combine(BaseDir, "config.json");
        public static string ProfilesDir { get; private set; } = DetermineProfilesDir();
        public static string ArchivedProjectsDir => Path.Combine(BaseDir, "archived_projects");

        private static string DetermineBaseDir()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(Path.Combine(appDir, "profiles")))
                return appDir;
            string? parent = Directory.GetParent(appDir)?.FullName;
            if (parent != null && Directory.Exists(Path.Combine(parent, "profiles")))
                return parent;
            return appDir;
        }

        private static string DetermineProfilesDir()
        {
            // 1. Check if configured in config.json
            try
            {
                if (File.Exists(ConfigFile))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile));
                    if (doc.RootElement.TryGetProperty("profiles_dir", out var p) && p.GetString() is { Length: > 0 } customPath)
                    {
                        if (Directory.Exists(customPath)) return customPath;
                    }
                }
            }
            catch { }

            // 2. Check local profiles folder in BaseDir (only if it has existing profile folders)
            string localProfiles = Path.Combine(BaseDir, "profiles");
            if (Directory.Exists(localProfiles) && Directory.GetDirectories(localProfiles).Length > 0)
            {
                return localProfiles;
            }

            // 3. Check if running inside an active profile (e.g. USERPROFILE contains \profiles\<name>\home)
            string envProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
            int idx = envProfile.IndexOf(@"\profiles\", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                string activeProfilesDir = envProfile.Substring(0, idx + @"\profiles".Length);
                if (Directory.Exists(activeProfilesDir) && Directory.GetDirectories(activeProfilesDir).Length > 0)
                {
                    SaveProfilesDirConfig(activeProfilesDir);
                    return activeProfilesDir;
                }
            }

            // 4. Auto-detect existing MultiGravity profiles on the system
            string userProfile = RealUserHome;
            string[] commonLocations = new[]
            {
                Path.Combine(userProfile, "Downloads", "Programs", "profiles"),
                Path.Combine(userProfile, "Downloads", "MultiGravity", "profiles"),
                Path.Combine(userProfile, "Desktop", "MultiGravity", "profiles"),
                Path.Combine(userProfile, "MultiGravity", "profiles")
            };

            foreach (string loc in commonLocations)
            {
                if (Directory.Exists(loc) && Directory.GetDirectories(loc).Length > 0)
                {
                    SaveProfilesDirConfig(loc);
                    return loc;
                }
            }

            // Default fallback to local folder
            Directory.CreateDirectory(localProfiles);
            return localProfiles;
        }

        public static void SetProfilesDir(string path)
        {
            if (Directory.Exists(path))
            {
                ProfilesDir = path;
                SaveProfilesDirConfig(path);
            }
        }

        private static void SaveProfilesDirConfig(string path)
        {
            try
            {
                var dict = new Dictionary<string, object>();
                if (File.Exists(ConfigFile))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile));
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.Clone();
                        }
                    }
                    catch { }
                }
                dict["profiles_dir"] = path;
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static bool GetVerboseModeConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile));
                    if (doc.RootElement.TryGetProperty("verbose_mode", out var v))
                    {
                        return v.GetBoolean();
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetVerboseModeConfig(bool verbose)
        {
            try
            {
                var dict = new Dictionary<string, object>();
                if (File.Exists(ConfigFile))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile));
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.Clone();
                        }
                    }
                    catch { }
                }
                dict["verbose_mode"] = verbose;
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static void SetVerboseMode(string profilePath, bool verbose)
        {
            try
            {
                string configDir = Path.Combine(profilePath, "home", ".gemini", "config");
                Directory.CreateDirectory(configDir);
                string cfgFile = Path.Combine(configDir, "config.json");

                JsonObject root;
                if (File.Exists(cfgFile))
                {
                    string raw = File.ReadAllText(cfgFile);
                    root = JsonNode.Parse(raw)?.AsObject() ?? new JsonObject();
                }
                else
                {
                    root = new JsonObject();
                }

                if (!root.ContainsKey("userSettings") || root["userSettings"] is not JsonObject)
                {
                    root["userSettings"] = new JsonObject();
                }

                var userSettings = root["userSettings"]!.AsObject();
                userSettings["verboseConversationSteps"] = verbose;

                File.WriteAllText(cfgFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static void SetVerboseModeForAllProfiles(bool verbose)
        {
            if (Directory.Exists(ProfilesDir))
            {
                foreach (string prof in Directory.GetDirectories(ProfilesDir))
                {
                    SetVerboseMode(prof, verbose);
                }
            }
        }
        public static string RealUserHome { get; } = GetRealUserHome();
        public static string MasterProjectsDir { get; } = Path.Combine(RealUserHome, ".gemini", "config", "projects");
        public static string MasterAntigravityDir { get; } = Path.Combine(RealUserHome, ".gemini", "antigravity");

        public static string GetRealUserHome()
        {
            string username = Environment.UserName;
            if (!string.IsNullOrEmpty(username))
            {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                string p = Path.Combine(sysDrive, "Users", username);
                if (Directory.Exists(p)) return p;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public static string AntigravityExePath { get; } = FindAntigravityExe();

        private static string FindAntigravityExe()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string p1 = Path.Combine(localAppData, "Programs", "antigravity", "Antigravity.exe");
            if (File.Exists(p1)) return p1;

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string p2 = Path.Combine(progFiles, "Antigravity", "Antigravity.exe");
            if (File.Exists(p2)) return p2;

            return p1;
        }

        public static void Initialize()
        {
            Directory.CreateDirectory(ProfilesDir);
            Localization.Initialize(ConfigFile);
            VaultBridge.RegisterProfilesDir(ProfilesDir);
            VaultBridge.SyncAllAccountsAndProfiles();
        }

        public static Dictionary<string, ProjectInfo> GetAllProjects()
        {
            var projects = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
            var searchDirs = new List<string> { MasterProjectsDir };

            if (Directory.Exists(ProfilesDir))
            {
                foreach (string p in Directory.GetDirectories(ProfilesDir))
                {
                    string d = Path.Combine(p, "home", ".gemini", "config", "projects");
                    if (Directory.Exists(d)) searchDirs.Add(d);
                }
            }

            foreach (string dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.GetFiles(dir, "*.json"))
                {
                    string filename = Path.GetFileName(file);
                    if (filename.Equals("outside-of-project.json", StringComparison.OrdinalIgnoreCase) ||
                        filename.Equals("default-cli-project.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        using var stream = File.OpenRead(file);
                        using var doc = JsonDocument.Parse(stream);
                        var root = doc.RootElement;

                        // Filter out sidecars and swarm boards
                        if (root.TryGetProperty("sidecar", out _)) continue;

                        if (root.TryGetProperty("name", out var nameProp) &&
                            nameProp.GetString() == "swarm/swarm_board")
                        {
                            continue;
                        }

                        // Filter out non-workspace entries without projectResources.resources
                        if (!root.TryGetProperty("projectResources", out var resProp) ||
                            !resProp.TryGetProperty("resources", out var arrProp) ||
                            arrProp.ValueKind != JsonValueKind.Array ||
                            arrProp.GetArrayLength() == 0)
                        {
                            continue;
                        }

                        string name = nameProp.GetString() ?? Path.GetFileNameWithoutExtension(filename);
                        string id = root.TryGetProperty("id", out var idProp) ? (idProp.GetString() ?? filename) : filename;
                        string folderPath = ExtractFolderPath(root);

                        if (!projects.ContainsKey(id))
                        {
                            projects[id] = new ProjectInfo(id, name, filename, file, folderPath, false);
                        }
                    }
                    catch { }
                }
            }

            return projects;
        }

        private static string ExtractFolderPath(JsonElement root)
        {
            if (root.TryGetProperty("projectResources", out var resProp) &&
                resProp.TryGetProperty("resources", out var arrProp) &&
                arrProp.ValueKind == JsonValueKind.Array &&
                arrProp.GetArrayLength() > 0)
            {
                var first = arrProp[0];
                string? uri = null;
                if (first.TryGetProperty("gitFolder", out var gf) && gf.TryGetProperty("folderUri", out var fu))
                {
                    uri = fu.GetString();
                }
                else if (first.TryGetProperty("folderUri", out var fu2))
                {
                    uri = fu2.GetString();
                }

                if (!string.IsNullOrEmpty(uri))
                {
                    try
                    {
                        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                        {
                            return parsedUri.LocalPath;
                        }
                        return Uri.UnescapeDataString(uri.Replace("file:///", "").Replace('/', Path.DirectorySeparatorChar));
                    }
                    catch { }
                }
            }
            return "";
        }

        public static Dictionary<string, ProjectInfo> GetArchivedProjects()
        {
            var archived = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(ArchivedProjectsDir)) return archived;

            foreach (string file in Directory.GetFiles(ArchivedProjectsDir, "*.json"))
            {
                string filename = Path.GetFileName(file);
                try
                {
                    using var stream = File.OpenRead(file);
                    using var doc = JsonDocument.Parse(stream);
                    var root = doc.RootElement;
                    string name = root.TryGetProperty("name", out var np) ? (np.GetString() ?? filename) : filename;
                    string id = root.TryGetProperty("id", out var ip) ? (ip.GetString() ?? filename) : filename;
                    string folderPath = ExtractFolderPath(root);
                    if (!archived.ContainsKey(id))
                    {
                        archived[id] = new ProjectInfo(id, name, filename, file, folderPath, true);
                    }
                }
                catch { }
            }
            return archived;
        }

        public static bool ArchiveProject(string projectId)
        {
            var all = GetAllProjects();
            if (!all.TryGetValue(projectId, out var pinfo)) return false;

            Directory.CreateDirectory(ArchivedProjectsDir);
            string dest = Path.Combine(ArchivedProjectsDir, pinfo.Filename);

            // 1. Copy to archived_projects directory
            try { File.Copy(pinfo.SourcePath, dest, true); } catch { return false; }

            // 2. Remove from MasterProjectsDir
            string masterFile = Path.Combine(MasterProjectsDir, pinfo.Filename);
            if (File.Exists(masterFile))
            {
                try { File.Delete(masterFile); } catch { }
            }

            // 3. Remove from all profiles
            if (Directory.Exists(ProfilesDir))
            {
                foreach (string prof in Directory.GetDirectories(ProfilesDir))
                {
                    string f = Path.Combine(prof, "home", ".gemini", "config", "projects", pinfo.Filename);
                    if (File.Exists(f))
                    {
                        try { File.Delete(f); } catch { }
                    }

                    // Also remove from profile exclusions if present
                    var ex = GetExclusions(prof);
                    if (ex.Remove(projectId))
                    {
                        SaveExclusions(prof, ex);
                    }
                }
            }

            // 4. Sync all profiles to ensure clean state
            SyncAllProfiles();
            return true;
        }

        public static bool UnarchiveProject(string projectId)
        {
            var archived = GetArchivedProjects();
            if (!archived.TryGetValue(projectId, out var pinfo)) return false;

            Directory.CreateDirectory(MasterProjectsDir);
            string dest = Path.Combine(MasterProjectsDir, pinfo.Filename);

            try
            {
                File.Copy(pinfo.SourcePath, dest, true);
                File.Delete(pinfo.SourcePath);
            }
            catch { return false; }

            // Restore across all profiles via sync
            SyncAllProfiles();
            return true;
        }

        public static void SyncAllProfiles()
        {
            if (Directory.Exists(ProfilesDir))
            {
                foreach (string prof in Directory.GetDirectories(ProfilesDir))
                {
                    try { SyncProfileProjects(prof); } catch { }
                }
            }
        }

        public static HashSet<string> GetExclusions(string profilePath)
        {
            string exFile = Path.Combine(profilePath, "exclusions.json");
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(exFile))
            {
                try
                {
                    string content = File.ReadAllText(exFile);
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            string? s = el.GetString();
                            if (!string.IsNullOrEmpty(s)) set.Add(s);
                        }
                    }
                }
                catch { }
            }

            return set;
        }

        public static void SaveExclusions(string profilePath, IEnumerable<string> exclusions)
        {
            string exFile = Path.Combine(profilePath, "exclusions.json");
            try
            {
                var list = exclusions.OrderBy(x => x).ToList();
                string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(exFile, json);
            }
            catch { }
        }

        public static void EnsureAntigravityJunction(string profilePath)
        {
            if (!Directory.Exists(MasterAntigravityDir)) return;

            string homeDir = Path.Combine(profilePath, "home");
            string geminiDir = Path.Combine(homeDir, ".gemini");
            string agDir = Path.Combine(geminiDir, "antigravity");

            Directory.CreateDirectory(geminiDir);

            if (Directory.Exists(agDir))
            {
                var dirInfo = new DirectoryInfo(agDir);
                if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return; // Already a junction
                }

                string summaries = Path.Combine(agDir, "conversation_summaries.db");
                if (File.Exists(summaries) && new FileInfo(summaries).Length > 30000)
                {
                    return;
                }

                try
                {
                    Directory.Delete(agDir, true);
                }
                catch { }
            }

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c mklink /J \"{agDir}\" \"{MasterAntigravityDir}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(3000);
            }
            catch { }

            // Fallback copy if mklink failed
            if (!Directory.Exists(agDir))
            {
                try
                {
                    Directory.CreateDirectory(agDir);
                    foreach (var f in new[] { "conversation_summaries.db", "antigravity_state.pbtxt" })
                    {
                        string src = Path.Combine(MasterAntigravityDir, f);
                        if (File.Exists(src)) File.Copy(src, Path.Combine(agDir, f), true);
                    }
                    foreach (var d in new[] { "conversations", "brain" })
                    {
                        string src = Path.Combine(MasterAntigravityDir, d);
                        string dst = Path.Combine(agDir, d);
                        if (Directory.Exists(src) && !Directory.Exists(dst))
                        {
                            CopyDirectory(src, dst);
                        }
                    }
                }
                catch { }
            }
        }

        public static void SyncProfileProjects(string profilePath)
        {
            string projDir = Path.Combine(profilePath, "home", ".gemini", "config", "projects");
            Directory.CreateDirectory(projDir);

            string outsideFile = Path.Combine(projDir, "outside-of-project.json");
            if (!File.Exists(outsideFile))
            {
                try
                {
                    File.WriteAllText(outsideFile, JsonSerializer.Serialize(new { id = "outside-of-project", name = "Outside of Project" }));
                }
                catch { }
            }

            // Clean out orphan sidecar files (e.g. swarm/swarm_board)
            if (Directory.Exists(projDir))
            {
                foreach (string file in Directory.GetFiles(projDir, "*.json"))
                {
                    string fname = Path.GetFileName(file);
                    if (fname.Equals("outside-of-project.json", StringComparison.OrdinalIgnoreCase) ||
                        fname.Equals("default-cli-project.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        using var s = File.OpenRead(file);
                        using var doc = JsonDocument.Parse(s);
                        if (doc.RootElement.TryGetProperty("sidecar", out _) ||
                            (doc.RootElement.TryGetProperty("name", out var n) && n.GetString() == "swarm/swarm_board"))
                        {
                            s.Close();
                            File.Delete(file);
                        }
                    }
                    catch { }
                }
            }

            // Clean out any archived projects that may still exist in profile
            var archivedProjects = GetArchivedProjects();
            foreach (var ap in archivedProjects.Values)
            {
                string af = Path.Combine(projDir, ap.Filename);
                if (File.Exists(af))
                {
                    try { File.Delete(af); } catch { }
                }
            }

            var allProjects = GetAllProjects();
            var exclusions = GetExclusions(profilePath);

            // Default-shared logic:
            // Every valid project is present UNLESS explicitly excluded
            foreach (var (pid, pinfo) in allProjects)
            {
                string destFile = Path.Combine(projDir, pinfo.Filename);
                if (exclusions.Contains(pid))
                {
                    if (File.Exists(destFile))
                    {
                        try { File.Delete(destFile); } catch { }
                    }
                }
                else
                {
                    if (!File.Exists(destFile))
                    {
                        try { File.Copy(pinfo.SourcePath, destFile, true); } catch { }
                    }
                }
            }
        }

        public static List<ProfileInfo> GetProfiles()
        {
            VaultBridge.SyncAllAccountsAndProfiles();
            var list = new List<ProfileInfo>();
            if (!Directory.Exists(ProfilesDir)) return list;

            foreach (string dir in Directory.GetDirectories(ProfilesDir).OrderBy(d => Path.GetFileName(d)))
            {
                string name = Path.GetFileName(dir);
                string projDir = Path.Combine(dir, "home", ".gemini", "config", "projects");
                int count = 0;

                if (Directory.Exists(projDir))
                {
                    foreach (string file in Directory.GetFiles(projDir, "*.json"))
                    {
                        string fname = Path.GetFileName(file);
                        if (fname.Equals("outside-of-project.json", StringComparison.OrdinalIgnoreCase) ||
                            fname.Equals("default-cli-project.json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        try
                        {
                            using var s = File.OpenRead(file);
                            using var doc = JsonDocument.Parse(s);
                            if (!doc.RootElement.TryGetProperty("sidecar", out _) &&
                                (!doc.RootElement.TryGetProperty("name", out var n) || n.GetString() != "swarm/swarm_board"))
                            {
                                count++;
                            }
                        }
                        catch { }
                    }
                }

                list.Add(new ProfileInfo(name, count, dir));
            }

            return list;
        }

        public static void CreateProfile(string name)
        {
            string profilePath = Path.Combine(ProfilesDir, name);
            if (Directory.Exists(profilePath))
            {
                throw new InvalidOperationException("Profile already exists");
            }

            Directory.CreateDirectory(Path.Combine(profilePath, "data"));
            Directory.CreateDirectory(Path.Combine(profilePath, "home", ".gemini", "config", "projects"));

            EnsureAntigravityJunction(profilePath);
            SaveExclusions(profilePath, Array.Empty<string>());
            SyncProfileProjects(profilePath);

            if (GetVerboseModeConfig())
            {
                SetVerboseMode(profilePath, true);
            }
        }

        public static void DeleteProfile(string name)
        {
            string profilePath = Path.Combine(ProfilesDir, name);
            if (Directory.Exists(profilePath))
            {
                Directory.Delete(profilePath, true);
            }
        }

        public static void LaunchProfile(string name)
        {
            string profilePath = Path.Combine(ProfilesDir, name);
            string dataDir = Path.Combine(profilePath, "data");
            string homeDir = Path.Combine(profilePath, "home");

            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(homeDir);

            // Sync all projects (accounting for exclusions) and ensure junctions before launch
            SyncProfileProjects(profilePath);
            EnsureAntigravityJunction(profilePath);
            SetVerboseMode(profilePath, GetVerboseModeConfig());
            VaultBridge.SyncProfileTokenFromVault(profilePath, name);

            var psi = new ProcessStartInfo
            {
                FileName = AntigravityExePath,
                Arguments = $"--user-data-dir=\"{dataDir}\"",
                WorkingDirectory = Path.GetDirectoryName(AntigravityExePath) ?? "",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.Environment["USERPROFILE"] = homeDir;
            psi.Environment["SSH_CONNECTION"] = "127.0.0.1 1234 127.0.0.1 22";
            psi.Environment["SSH_CLIENT"] = "127.0.0.1 1234 22";

            Process.Start(psi);
        }

        public static void CreateDesktopShortcut(string name)
        {
            string profilePath = Path.Combine(ProfilesDir, name);
            string dataDir = Path.Combine(profilePath, "data");
            string homeDir = Path.Combine(profilePath, "home");

            SyncProfileProjects(profilePath);
            EnsureAntigravityJunction(profilePath);
            SetVerboseMode(profilePath, GetVerboseModeConfig());
            VaultBridge.SyncProfileTokenFromVault(profilePath, name);

            // 1. Create launch.vbs runner script as fallback
            string runnerScript = Path.Combine(profilePath, "launch.vbs");
            string vbs = $@"Set WshShell = CreateObject(""WScript.Shell"")
Set objEnv = WshShell.Environment(""PROCESS"")
objEnv(""USERPROFILE"") = ""{homeDir}""
objEnv(""SSH_CONNECTION"") = ""127.0.0.1 1234 127.0.0.1 22""
objEnv(""SSH_CLIENT"") = ""127.0.0.1 1234 22""
WshShell.Run """"""{AntigravityExePath}"""" --user-data-dir=""""{dataDir}"""""", 1, False
";
            File.WriteAllText(runnerScript, vbs);

            // 2. Create Desktop Shortcut pointing directly to MultiGravity --launch (with auto-sync)
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(BaseDir, "MultiGravity.exe");
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string shortcutPath = Path.Combine(desktop, $"Antigravity - {name}.lnk");

            string psScript = $@"
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut('{shortcutPath.Replace("'", "''")}')
if (Test-Path '{currentExe.Replace("'", "''")}') {{
    $s.TargetPath = '{currentExe.Replace("'", "''")}'
    $s.Arguments = '--launch ""{name.Replace("'", "''")}""'
}} else {{
    $s.TargetPath = 'wscript.exe'
    $s.Arguments = '""{runnerScript.Replace("'", "''")}""'
}}
$s.WorkingDirectory = '{Path.GetDirectoryName(AntigravityExePath)?.Replace("'", "''")}'
$s.IconLocation = '{AntigravityExePath.Replace("'", "''")},0'
$s.Description = 'Antigravity - {name.Replace("'", "''")}'
$s.Save()
";
            using var ps = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{psScript}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            ps?.WaitForExit(5000);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
            }
        }
    }
}

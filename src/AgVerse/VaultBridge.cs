using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgVerse
{
    public static class VaultBridge
    {
        public static string RealLocalAppData
        {
            get
            {
                string realHome = AntigravityCore.RealUserHome;
                string realLocal = Path.Combine(realHome, "AppData", "Local");
                if (Directory.Exists(realLocal)) return realLocal;
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
        }

        public static string VaultDir => Path.Combine(RealLocalAppData, "AntigravityQuota");
        public static string VaultPath => Path.Combine(VaultDir, "vault.dat");
        public static string AgVerseProfilesPointerFile => Path.Combine(VaultDir, "agverse_profiles.txt");
        public static string LegacyProfilesPointerFile => Path.Combine(VaultDir, "multigravity_profiles.txt");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL cred, int flags);

        public static void RegisterProfilesDir(string profilesDir)
        {
            try
            {
                Directory.CreateDirectory(VaultDir);
                File.WriteAllText(AgVerseProfilesPointerFile, profilesDir, Encoding.UTF8);
                File.WriteAllText(LegacyProfilesPointerFile, profilesDir, Encoding.UTF8);
            }
            catch { }
        }

        public static JsonArray LoadVaultJson()
        {
            if (!File.Exists(VaultPath)) return new JsonArray();
            try
            {
                byte[] enc = File.ReadAllBytes(VaultPath);
                byte[] dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(dec);
                var node = JsonNode.Parse(json);
                if (node is JsonArray arr) return arr;
            }
            catch { }
            return new JsonArray();
        }

        public static void SaveVaultJson(JsonArray array)
        {
            try
            {
                Directory.CreateDirectory(VaultDir);
                string json = array.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                byte[] raw = Encoding.UTF8.GetBytes(json);
                byte[] enc = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(VaultPath, enc);
            }
            catch { }
        }

        public static JsonObject? FindAccount(JsonArray vault, string? email, string? profileName)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                foreach (var item in vault)
                {
                    if (item is JsonObject obj &&
                        string.Equals(obj["Email"]?.ToString(), email, StringComparison.OrdinalIgnoreCase))
                    {
                        return obj;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(profileName))
            {
                foreach (var item in vault)
                {
                    if (item is JsonObject obj)
                    {
                        string? label = obj["Label"]?.ToString();
                        string? itemEmail = obj["Email"]?.ToString();
                        if (string.Equals(label, profileName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(itemEmail, profileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return obj;
                        }
                    }
                }
            }

            return null;
        }

        public static void UpdateOrAddAccount(string email, string? label, string accessToken, string refreshToken, string idToken, long expiresAt, string rawBlob)
        {
            try
            {
                var vault = LoadVaultJson();
                var existing = FindAccount(vault, email, label);

                if (existing != null)
                {
                    existing["AccessToken"] = accessToken;
                    if (!string.IsNullOrEmpty(refreshToken)) existing["RefreshToken"] = refreshToken;
                    if (!string.IsNullOrEmpty(idToken)) existing["IdToken"] = idToken;
                    existing["ExpiresAt"] = expiresAt;
                    existing["RawBlob"] = rawBlob;
                    existing["AuthFailed"] = false;
                    if (!string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(existing["Label"]?.ToString()))
                    {
                        existing["Label"] = label;
                    }
                }
                else
                {
                    var newAcc = new JsonObject
                    {
                        ["Id"] = Guid.NewGuid().ToString("N").Substring(0, 8),
                        ["Email"] = email,
                        ["Label"] = !string.IsNullOrWhiteSpace(label) ? label : email.Split('@')[0],
                        ["ProjectId"] = "rising-fact-p41fc",
                        ["IsActive"] = (vault.Count == 0),
                        ["IsVisible"] = true,
                        ["AuthFailed"] = false,
                        ["AccessToken"] = accessToken,
                        ["RefreshToken"] = refreshToken,
                        ["IdToken"] = idToken,
                        ["ExpiresAt"] = expiresAt,
                        ["RawBlob"] = rawBlob,
                        ["HasGemini"] = true,
                        ["HasClaude"] = true,
                        ["GFiveH"] = 1.0,
                        ["GWeekly"] = 1.0,
                        ["GFiveHReset"] = "--",
                        ["GWeeklyReset"] = "--",
                        ["CFiveH"] = 1.0,
                        ["CWeekly"] = 1.0,
                        ["CFiveHReset"] = "--",
                        ["CWeeklyReset"] = "--",
                        ["HasArgon"] = false,
                        ["AWeekly"] = 1.0,
                        ["AWeeklyReset"] = "--"
                    };
                    vault.Add(newAcc);
                }

                SaveVaultJson(vault);
                RegisterProfilesDir(AntigravityCore.ProfilesDir);
                TriggerRainmeterSync();
            }
            catch { }
        }

        public static string? GetActiveAccountEmail()
        {
            var vault = LoadVaultJson();
            foreach (var item in vault)
            {
                if (item is JsonObject obj && obj["IsActive"]?.GetValue<bool>() == true)
                {
                    return obj["Email"]?.ToString();
                }
            }
            return null;
        }

        public static bool IsProfileActiveInIde(string profileName, string profilePath)
        {
            string? activeEmail = GetActiveAccountEmail();
            if (string.IsNullOrEmpty(activeEmail)) return false;

            // 1. Check profile's token email
            var (email, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);
            if (hasToken && string.Equals(email, activeEmail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Check vault account label
            var vault = LoadVaultJson();
            var acc = FindAccount(vault, null, profileName);
            if (acc != null && string.Equals(acc["Email"]?.ToString(), activeEmail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool SetActiveAccount(string emailOrProfileName)
        {
            try
            {
                var vault = LoadVaultJson();
                JsonObject? target = FindAccount(vault, emailOrProfileName, emailOrProfileName);
                if (target == null) return false;

                string targetEmail = target["Email"]?.ToString() ?? "";
                foreach (var item in vault)
                {
                    if (item is JsonObject obj)
                    {
                        obj["IsActive"] = string.Equals(obj["Email"]?.ToString(), targetEmail, StringComparison.OrdinalIgnoreCase);
                    }
                }

                SaveVaultJson(vault);

                string? rawBlob = target["RawBlob"]?.ToString();
                if (!string.IsNullOrEmpty(rawBlob))
                {
                    ActivateInIde(rawBlob);
                }

                TriggerRainmeterSync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ActivateInIde(string rawBlob)
        {
            if (string.IsNullOrEmpty(rawBlob)) return;
            try
            {
                string dir = Path.Combine(AntigravityCore.RealUserHome, ".gemini");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "jetski-standalone-oauth-token"), rawBlob, new UTF8Encoding(false));

                byte[] bytes = Encoding.UTF8.GetBytes(rawBlob);
                IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    CREDENTIAL c = new CREDENTIAL
                    {
                        Type = 1,
                        TargetName = "gemini:antigravity",
                        UserName = "antigravity",
                        CredentialBlob = ptr,
                        CredentialBlobSize = bytes.Length,
                        Persist = 2
                    };
                    CredWrite(ref c, 0);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch { }
        }

        public static void RemoveAccount(string? email, string? profileName)
        {
            try
            {
                // Safety: check if any OTHER profile directory on disk still uses this email
                if (!string.IsNullOrEmpty(email) && Directory.Exists(AntigravityCore.ProfilesDir))
                {
                    foreach (string profDir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
                    {
                        string dirName = Path.GetFileName(profDir);
                        if (string.Equals(dirName, profileName, StringComparison.OrdinalIgnoreCase)) continue;

                        var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(profDir);
                        if (hasToken && string.Equals(tEmail, email, StringComparison.OrdinalIgnoreCase))
                        {
                            // Another profile still uses this email; don't remove from vault
                            return;
                        }
                    }
                }

                var vault = LoadVaultJson();
                JsonObject? target = FindAccount(vault, email, profileName);
                if (target != null)
                {
                    vault.Remove(target);
                    SaveVaultJson(vault);
                    TriggerRainmeterSync();
                }
            }
            catch { }
        }

        public static void SyncProfileTokenFromVault(string profilePath, string profileName)
        {
            try
            {
                var vault = LoadVaultJson();
                string tokenFile = TokenService.GetTokenFilePath(profilePath);

                string? currentEmail = null;
                if (File.Exists(tokenFile))
                {
                    var (email, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);
                    if (hasToken) currentEmail = email;
                }

                JsonObject? acc = FindAccount(vault, currentEmail, profileName);
                if (acc == null)
                {
                    // Profile has a token locally that is not in vault -> import to vault so Rainmeter sees it
                    if (File.Exists(tokenFile) && !string.IsNullOrEmpty(currentEmail))
                    {
                        try
                        {
                            string localRaw = File.ReadAllText(tokenFile);
                            var localRoot = JsonNode.Parse(localRaw);
                            string accessToken = localRoot?["token"]?["access_token"]?.ToString() ?? "";
                            string refreshToken = localRoot?["token"]?["refresh_token"]?.ToString() ?? "";
                            string idToken = localRoot?["id_token"]?.ToString() ?? "";
                            string expiryStr = localRoot?["token"]?["expiry"]?.ToString() ?? "";
                            long expUnix = 0;
                            if (DateTimeOffset.TryParse(expiryStr, out var localOffset))
                            {
                                expUnix = localOffset.ToUnixTimeSeconds();
                            }
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                UpdateOrAddAccount(currentEmail, profileName, accessToken, refreshToken, idToken, expUnix, localRaw);
                            }
                        }
                        catch { }
                    }
                    return;
                }

                string? rawBlob = acc["RawBlob"]?.ToString();
                long vaultExpiresAt = acc["ExpiresAt"]?.GetValue<long>() ?? 0;

                bool shouldUpdateLocal = false;
                bool shouldUpdateVault = false;

                if (!File.Exists(tokenFile))
                {
                    if (!string.IsNullOrEmpty(rawBlob)) shouldUpdateLocal = true;
                }
                else
                {
                    try
                    {
                        string localRaw = File.ReadAllText(tokenFile);
                        var localRoot = JsonNode.Parse(localRaw);
                        string expiryStr = localRoot?["token"]?["expiry"]?.ToString() ?? "";
                        if (DateTimeOffset.TryParse(expiryStr, out var localOffset))
                        {
                            long localExpiresAt = localOffset.ToUnixTimeSeconds();
                            if (vaultExpiresAt > localExpiresAt + 60 && !string.IsNullOrEmpty(rawBlob))
                            {
                                shouldUpdateLocal = true;
                            }
                            else if (localExpiresAt > vaultExpiresAt + 60)
                            {
                                shouldUpdateVault = true;
                            }
                        }
                        else if (!string.IsNullOrEmpty(rawBlob))
                        {
                            shouldUpdateLocal = true;
                        }
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(rawBlob)) shouldUpdateLocal = true;
                    }
                }

                if (shouldUpdateLocal && !string.IsNullOrEmpty(rawBlob))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tokenFile)!);
                    File.WriteAllText(tokenFile, rawBlob, new UTF8Encoding(false));
                }
                else if (shouldUpdateVault)
                {
                    try
                    {
                        string localRaw = File.ReadAllText(tokenFile);
                        var localRoot = JsonNode.Parse(localRaw);
                        string accessToken = localRoot?["token"]?["access_token"]?.ToString() ?? "";
                        string refreshToken = localRoot?["token"]?["refresh_token"]?.ToString() ?? "";
                        string idToken = localRoot?["id_token"]?.ToString() ?? "";
                        string expiryStr = localRoot?["token"]?["expiry"]?.ToString() ?? "";
                        long expUnix = 0;
                        if (DateTimeOffset.TryParse(expiryStr, out var localOffset))
                        {
                            expUnix = localOffset.ToUnixTimeSeconds();
                        }
                        if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(currentEmail))
                        {
                            UpdateOrAddAccount(currentEmail, profileName, accessToken, refreshToken, idToken, expUnix, localRaw);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static void SyncAllAccountsAndProfiles()
        {
            try
            {
                RegisterProfilesDir(AntigravityCore.ProfilesDir);
                if (!Directory.Exists(AntigravityCore.ProfilesDir)) return;

                var vault = LoadVaultJson();

                // 1. Sync any existing profile tokens with vault
                foreach (string profDir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
                {
                    string profName = Path.GetFileName(profDir);
                    SyncProfileTokenFromVault(profDir, profName);
                }

                // 2. Check if vault has accounts that do not have a profile directory yet.
                // If so, provision the profile so both applications have the same account list!
                bool vaultChanged = false;
                var existingDirs = Directory.GetDirectories(AntigravityCore.ProfilesDir)
                    .Select(d => Path.GetFileName(d) ?? "")
                    .Where(n => n.Length > 0)
                    .ToList();

                foreach (var item in vault)
                {
                    if (item is not JsonObject acc) continue;
                    string email = acc["Email"]?.ToString() ?? "";
                    string label = acc["Label"]?.ToString() ?? "";
                    string? rawBlob = acc["RawBlob"]?.ToString();

                    if (string.IsNullOrEmpty(email)) continue;

                    // Does any directory match email or label?
                    bool found = false;
                    foreach (string dirName in existingDirs)
                    {
                        if (dirName.Equals(label, StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals(email, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }

                        // Also check token inside directory
                        string tf = Path.Combine(AntigravityCore.ProfilesDir, dirName, "home", ".gemini", "jetski-standalone-oauth-token");
                        if (File.Exists(tf))
                        {
                            var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(Path.Combine(AntigravityCore.ProfilesDir, dirName));
                            if (hasToken && string.Equals(tEmail, email, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        // Auto-provision profile directory
                        string chosenName = SanitizeProfileName(!string.IsNullOrWhiteSpace(label) ? label : email.Split('@')[0]);
                        if (existingDirs.Contains(chosenName, StringComparer.OrdinalIgnoreCase))
                        {
                            chosenName = SanitizeProfileName(email.Replace("@", "_at_"));
                        }

                        try
                        {
                            AntigravityCore.CreateProfile(chosenName);
                            existingDirs.Add(chosenName);
                            string newProfPath = Path.Combine(AntigravityCore.ProfilesDir, chosenName);
                            if (!string.IsNullOrEmpty(rawBlob))
                            {
                                string tf = TokenService.GetTokenFilePath(newProfPath);
                                Directory.CreateDirectory(Path.GetDirectoryName(tf)!);
                                File.WriteAllText(tf, rawBlob, new UTF8Encoding(false));
                            }
                        }
                        catch { }
                    }
                }

                if (vaultChanged)
                {
                    SaveVaultJson(vault);
                }
            }
            catch { }
        }

        private static string SanitizeProfileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            string res = sb.ToString().Trim();
            return string.IsNullOrEmpty(res) ? "profile" : res;
        }

        public static void TriggerRainmeterSync()
        {
            try
            {
                string? bridgePath = FindAntigravityBridgeExe();
                if (!string.IsNullOrEmpty(bridgePath) && File.Exists(bridgePath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = bridgePath,
                        Arguments = "sync",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
            }
            catch { }
        }

        public static string? FindAntigravityBridgeExe()
        {
            // 1. Check OneDrive / Documents Rainmeter skin path
            string oneDrivePath = Path.Combine(AntigravityCore.RealUserHome, "OneDrive", "Documents", "Rainmeter", "Skins", "AntigravityQuota", "@Resources", "AntigravityBridge.exe");
            if (File.Exists(oneDrivePath)) return oneDrivePath;

            // 2. Check standard Documents Rainmeter skin path
            string docPath = Path.Combine(AntigravityCore.RealUserHome, "Documents", "Rainmeter", "Skins", "AntigravityQuota", "@Resources", "AntigravityBridge.exe");
            if (File.Exists(docPath)) return docPath;

            // 3. Check Rainmeter.ini SkinPath
            try
            {
                string iniFile = Path.Combine(AntigravityCore.RealUserHome, "AppData", "Roaming", "Rainmeter", "Rainmeter.ini");
                if (File.Exists(iniFile))
                {
                    foreach (string line in File.ReadAllLines(iniFile))
                    {
                        if (line.StartsWith("SkinPath=", StringComparison.OrdinalIgnoreCase))
                        {
                            string skinPath = line.Substring("SkinPath=".Length).Trim();
                            string p = Path.Combine(skinPath, "AntigravityQuota", "@Resources", "AntigravityBridge.exe");
                            if (File.Exists(p)) return p;
                        }
                    }
                }
            }
            catch { }

            // 4. Check repo source build folder
            string repoPath = @"C:\Users\Ayush\Desktop\Projects\rainmeter-skins\antigravity-limits\skin\@Resources\AntigravityBridge.exe";
            if (File.Exists(repoPath)) return repoPath;

            return null;
        }
    }
}

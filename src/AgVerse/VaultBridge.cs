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

        private const string VaultMutexName = @"Local\AntigravityQuotaVaultMutex";

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            string t = email.Trim();
            if (t.Contains(' ') || t.Contains("Unknown", StringComparison.OrdinalIgnoreCase)) return false;
            int at = t.IndexOf('@');
            if (at <= 0 || at != t.LastIndexOf('@') || at >= t.Length - 1) return false;
            int dot = t.IndexOf('.', at + 1);
            return dot > at + 1 && dot < t.Length - 1;
        }

        private static string? TryExtractEmailFromVaultItem(JsonObject obj)
        {
            string? idTok = obj["IdToken"]?.ToString();
            string? fromIdTok = TokenService.ExtractEmailFromJwt(idTok);
            if (IsValidEmail(fromIdTok)) return fromIdTok;

            string? rawBlob = obj["RawBlob"]?.ToString();
            if (!string.IsNullOrEmpty(rawBlob))
            {
                try
                {
                    var node = JsonNode.Parse(rawBlob);
                    string? blobEmail = node?["id_token_email"]?.ToString();
                    if (IsValidEmail(blobEmail)) return blobEmail;

                    string? blobIdTok = node?["id_token"]?.ToString();
                    string? fromBlobJwt = TokenService.ExtractEmailFromJwt(blobIdTok);
                    if (IsValidEmail(fromBlobJwt)) return fromBlobJwt;
                }
                catch { }
            }

            string? label = obj["Label"]?.ToString();
            if (!string.IsNullOrWhiteSpace(label) && Directory.Exists(AntigravityCore.ProfilesDir))
            {
                string profPath = Path.Combine(AntigravityCore.ProfilesDir, label);
                if (Directory.Exists(profPath))
                {
                    var (profEmail, hasToken, _, _) = TokenService.GetTokenSummary(profPath);
                    if (hasToken && IsValidEmail(profEmail)) return profEmail;
                }
            }

            return null;
        }

        public static bool RepairPlaceholderEmails(JsonArray vault)
        {
            bool changed = false;
            foreach (var item in vault)
            {
                if (item is not JsonObject obj) continue;
                string? email = obj["Email"]?.ToString();
                if (!IsValidEmail(email))
                {
                    string? resolved = TryExtractEmailFromVaultItem(obj);
                    if (IsValidEmail(resolved))
                    {
                        obj["Email"] = resolved;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public static JsonArray LoadVaultJson()
        {
            if (!File.Exists(VaultPath)) return new JsonArray();
            try
            {
                using var mutex = new System.Threading.Mutex(false, VaultMutexName);
                bool locked = false;
                try
                {
                    try { locked = mutex.WaitOne(2500); }
                    catch (System.Threading.AbandonedMutexException) { locked = true; }

                    byte[] enc = File.ReadAllBytes(VaultPath);
                    byte[] dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(dec);
                    var node = JsonNode.Parse(json);
                    if (node is JsonArray arr) return arr;
                }
                finally
                {
                    if (locked) mutex.ReleaseMutex();
                }
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

                using var mutex = new System.Threading.Mutex(false, VaultMutexName);
                bool locked = false;
                try
                {
                    try { locked = mutex.WaitOne(2500); }
                    catch (System.Threading.AbandonedMutexException) { locked = true; }

                    string tmpPath = VaultPath + ".tmp";
                    File.WriteAllBytes(tmpPath, enc);
                    File.Move(tmpPath, VaultPath, overwrite: true);
                }
                finally
                {
                    if (locked) mutex.ReleaseMutex();
                }
            }
            catch { }
        }

        public static JsonObject? FindAccount(JsonArray vault, string? email, string? profileName)
        {
            if (IsValidEmail(email))
            {
                foreach (var item in vault)
                {
                    if (item is not JsonObject obj) continue;
                    string? itemEmail = obj["Email"]?.ToString();
                    if (string.Equals(itemEmail, email, StringComparison.OrdinalIgnoreCase))
                    {
                        return obj;
                    }

                    // Fallback: if stored Email was a placeholder (e.g. "Unknown email"), check JWT email inside item
                    if (!IsValidEmail(itemEmail))
                    {
                        string? resolved = TryExtractEmailFromVaultItem(obj);
                        if (string.Equals(resolved, email, StringComparison.OrdinalIgnoreCase))
                        {
                            return obj;
                        }
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
                            (IsValidEmail(itemEmail) && string.Equals(itemEmail, profileName, StringComparison.OrdinalIgnoreCase)))
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
                // Never persist placeholder strings like "Unknown email" as an account email
                if (!IsValidEmail(email))
                {
                    string? fromJwt = TokenService.ExtractEmailFromJwt(idToken);
                    if (IsValidEmail(fromJwt))
                    {
                        email = fromJwt!;
                    }
                    else if (!string.IsNullOrEmpty(rawBlob))
                    {
                        try
                        {
                            var node = JsonNode.Parse(rawBlob);
                            string? fromBlob = node?["id_token_email"]?.ToString();
                            if (!IsValidEmail(fromBlob)) fromBlob = TokenService.ExtractEmailFromJwt(node?["id_token"]?.ToString());
                            if (IsValidEmail(fromBlob)) email = fromBlob!;
                        }
                        catch { }
                    }
                }

                var vault = LoadVaultJson();
                RepairPlaceholderEmails(vault);

                // Match by valid email first; only fall back to label when repairing an account without a conflicting valid email
                JsonObject? existing = IsValidEmail(email) ? FindAccount(vault, email, null) : null;
                if (existing == null && !string.IsNullOrWhiteSpace(label))
                {
                    var byLabel = FindAccount(vault, null, label);
                    if (byLabel != null)
                    {
                        string? existingEmail = byLabel["Email"]?.ToString();
                        if (!IsValidEmail(existingEmail) || string.Equals(existingEmail, email, StringComparison.OrdinalIgnoreCase))
                        {
                            existing = byLabel;
                        }
                    }
                }

                if (existing != null)
                {
                    if (IsValidEmail(email) && !IsValidEmail(existing["Email"]?.ToString()))
                    {
                        existing["Email"] = email;
                    }
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
                    if (!IsValidEmail(email))
                    {
                        // Refuse to create a broken vault record with "Unknown email"
                        return;
                    }

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
                    string? email = obj["Email"]?.ToString();
                    if (IsValidEmail(email)) return email;
                    return TryExtractEmailFromVaultItem(obj) ?? email;
                }
            }
            return null;
        }

        public static bool IsProfileActiveInIde(string profileName, string profilePath)
        {
            var vault = LoadVaultJson();
            JsonObject? activeObj = null;
            foreach (var item in vault)
            {
                if (item is JsonObject obj && obj["IsActive"]?.GetValue<bool>() == true)
                {
                    activeObj = obj;
                    break;
                }
            }
            if (activeObj == null) return false;

            string? activeEmail = activeObj["Email"]?.ToString();
            if (!IsValidEmail(activeEmail)) activeEmail = TryExtractEmailFromVaultItem(activeObj);

            // 1. Match by valid email from local profile token
            var (email, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);
            if (hasToken && IsValidEmail(email) && IsValidEmail(activeEmail) &&
                string.Equals(email, activeEmail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 2. Match by profile label directly against the active vault record
            if (string.Equals(activeObj["Label"]?.ToString(), profileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool SetActiveProfile(string profileName, string profilePath)
        {
            try
            {
                SyncProfileTokenFromVault(profilePath, profileName);

                var (email, hasToken, _, _) = TokenService.GetTokenSummary(profilePath);
                string? validEmail = hasToken && IsValidEmail(email) ? email : null;

                var vault = LoadVaultJson();
                bool repaired = RepairPlaceholderEmails(vault);
                JsonObject? target = FindAccount(vault, validEmail, profileName);
                if (target == null)
                {
                    if (repaired) SaveVaultJson(vault);
                    return false;
                }

                ActivateVaultObject(vault, target, profilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool SetActiveAccount(string emailOrProfileName)
        {
            try
            {
                var vault = LoadVaultJson();
                bool repaired = RepairPlaceholderEmails(vault);
                JsonObject? target = FindAccount(vault, emailOrProfileName, emailOrProfileName);
                if (target == null)
                {
                    if (repaired) SaveVaultJson(vault);
                    return false;
                }

                string? label = target["Label"]?.ToString();
                string? profPath = !string.IsNullOrEmpty(label)
                    ? Path.Combine(AntigravityCore.ProfilesDir, label)
                    : null;
                ActivateVaultObject(vault, target, profPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ActivateVaultObject(JsonArray vault, JsonObject target, string? profilePath)
        {
            string? targetEmail = target["Email"]?.ToString();
            bool hasValidTargetEmail = IsValidEmail(targetEmail);
            foreach (var item in vault)
            {
                if (item is JsonObject obj)
                {
                    obj["IsActive"] = ReferenceEquals(obj, target) ||
                                      (hasValidTargetEmail && string.Equals(obj["Email"]?.ToString(), targetEmail, StringComparison.OrdinalIgnoreCase));
                }
            }

            SaveVaultJson(vault);

            string? rawBlob = target["RawBlob"]?.ToString();
            if (string.IsNullOrEmpty(rawBlob) && !string.IsNullOrEmpty(profilePath))
            {
                string tokenFile = TokenService.GetTokenFilePath(profilePath);
                if (File.Exists(tokenFile))
                {
                    try { rawBlob = File.ReadAllText(tokenFile); } catch { }
                }
            }
            if (!string.IsNullOrEmpty(rawBlob))
            {
                ActivateInIde(rawBlob);
            }

            TriggerRainmeterSync();
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
                if (IsValidEmail(email) && Directory.Exists(AntigravityCore.ProfilesDir))
                {
                    foreach (string profDir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
                    {
                        string dirName = Path.GetFileName(profDir);
                        if (string.Equals(dirName, profileName, StringComparison.OrdinalIgnoreCase)) continue;

                        var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(profDir);
                        if (hasToken && IsValidEmail(tEmail) && string.Equals(tEmail, email, StringComparison.OrdinalIgnoreCase))
                        {
                            // Another profile still uses this email; don't remove from vault
                            return;
                        }
                    }
                }

                var vault = LoadVaultJson();
                JsonObject? target = FindAccount(vault, IsValidEmail(email) ? email : null, profileName);
                if (target != null)
                {
                    vault.Remove(target);
                    SaveVaultJson(vault);
                    TriggerRainmeterSync();
                }
            }
            catch { }
        }

        public static void RenameAccountLabel(string oldProfileName, string newProfileName, string newProfilePath)
        {
            try
            {
                var vault = LoadVaultJson();
                string? email = null;
                if (Directory.Exists(newProfilePath))
                {
                    var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(newProfilePath);
                    if (hasToken && IsValidEmail(tEmail)) email = tEmail;
                }

                JsonObject? acc = FindAccount(vault, email, oldProfileName);
                if (acc != null)
                {
                    acc["Label"] = newProfileName;
                    SaveVaultJson(vault);
                    TriggerRainmeterSync();
                }
            }
            catch { }
        }

        private static void TrySyncLocalTokenToVault(string tokenFile, string profileName, string? emailHint)
        {
            if (!File.Exists(tokenFile)) return;

            try
            {
                string localRaw = File.ReadAllText(tokenFile);
                var localRoot = JsonNode.Parse(localRaw);
                string accessToken = localRoot?["token"]?["access_token"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(accessToken)) return;

                string refreshToken = localRoot?["token"]?["refresh_token"]?.ToString() ?? "";
                string idToken = localRoot?["id_token"]?.ToString() ?? "";
                string expiryStr = localRoot?["token"]?["expiry"]?.ToString() ?? "";

                string? resolvedEmail = IsValidEmail(emailHint) ? emailHint : null;
                if (!IsValidEmail(resolvedEmail))
                {
                    string? blobEmail = localRoot?["id_token_email"]?.ToString();
                    resolvedEmail = IsValidEmail(blobEmail)
                        ? blobEmail
                        : TokenService.ExtractEmailFromJwt(idToken);
                }

                if (!IsValidEmail(resolvedEmail)) return;

                long expUnix = 0;
                if (DateTimeOffset.TryParse(expiryStr, out var localOffset))
                {
                    expUnix = localOffset.ToUnixTimeSeconds();
                }

                UpdateOrAddAccount(resolvedEmail!, profileName, accessToken, refreshToken, idToken, expUnix, localRaw);
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
                    if (hasToken && IsValidEmail(email)) currentEmail = email;
                }

                JsonObject? acc = FindAccount(vault, currentEmail, profileName);
                if (acc == null)
                {
                    // Profile has a token locally that is not in vault -> import to vault so Rainmeter sees it
                    TrySyncLocalTokenToVault(tokenFile, profileName, currentEmail);
                    return;
                }

                if (!IsValidEmail(acc["Email"]?.ToString()))
                {
                    string? repairedEmail = IsValidEmail(currentEmail) ? currentEmail : TryExtractEmailFromVaultItem(acc);
                    if (IsValidEmail(repairedEmail))
                    {
                        acc["Email"] = repairedEmail;
                        SaveVaultJson(vault);
                        currentEmail ??= repairedEmail;
                    }
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
                    string? emailHint = IsValidEmail(currentEmail) ? currentEmail : TryExtractEmailFromVaultItem(acc);
                    TrySyncLocalTokenToVault(tokenFile, profileName, emailHint);
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

                var initialVault = LoadVaultJson();
                if (RepairPlaceholderEmails(initialVault))
                {
                    SaveVaultJson(initialVault);
                }

                // 1. Sync any existing profile tokens with vault
                foreach (string profDir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
                {
                    string profName = Path.GetFileName(profDir);
                    SyncProfileTokenFromVault(profDir, profName);
                }

                // 2. Reload vault after step 1 updates, then provision any vault accounts missing a profile directory.
                var vault = LoadVaultJson();
                var existingDirs = Directory.GetDirectories(AntigravityCore.ProfilesDir)
                    .Select(d => Path.GetFileName(d) ?? "")
                    .Where(n => n.Length > 0)
                    .ToList();

                foreach (var item in vault)
                {
                    if (item is not JsonObject acc) continue;
                    string? resolvedEmail = TryExtractEmailFromVaultItem(acc);
                    string email = IsValidEmail(resolvedEmail) ? resolvedEmail! : (acc["Email"]?.ToString() ?? "");
                    string label = acc["Label"]?.ToString() ?? "";
                    string? rawBlob = acc["RawBlob"]?.ToString();

                    if (!IsValidEmail(email)) continue;

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
                            if (hasToken && IsValidEmail(tEmail) && string.Equals(tEmail, email, StringComparison.OrdinalIgnoreCase))
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

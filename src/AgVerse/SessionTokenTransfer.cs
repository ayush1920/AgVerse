using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AgVerse
{
    public enum ImportedProfileAction
    {
        AddedNew,
        UpdatedExisting,
        AlreadyExists,
        SkippedInvalid
    }

    public record ImportedProfileOutcome(
        string ProfileName,
        string Email,
        ImportedProfileAction Action,
        bool Refreshed,
        string? Note = null);

    public record ImportTransferResult(
        bool Success,
        string Message,
        List<ImportedProfileOutcome> Outcomes,
        string? ActivatedDefaultProfile,
        string? SuggestedDefaultProfile);

    public static class SessionTokenTransfer
    {
        public const string FormatId = "agverse-session-tokens";
        public const int FormatVersion = 1;

        public static string DefaultIdeTokenPath =>
            Path.Combine(AntigravityCore.RealUserHome, ".gemini", "jetski-standalone-oauth-token");

        public static int CountExportableProfiles()
        {
            if (!Directory.Exists(AntigravityCore.ProfilesDir)) return 0;
            int count = 0;
            foreach (string dir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
            {
                var (_, hasToken, _, _) = TokenService.GetTokenSummary(dir);
                if (hasToken) count++;
            }
            return count;
        }

        public static bool HasDefaultIdeToken() => File.Exists(DefaultIdeTokenPath);

        public static (bool Success, string Message, int ExportedCount) ExportSingleProfile(string profileName, string outputPath)
        {
            try
            {
                string profilePath = Path.Combine(AntigravityCore.ProfilesDir, profileName);
                if (!Directory.Exists(profilePath))
                {
                    return (false, $"Profile folder '{profileName}' was not found.", 0);
                }

                VaultBridge.SyncProfileTokenFromVault(profilePath, profileName);
                string tokenFile = TokenService.GetTokenFilePath(profilePath);
                if (!File.Exists(tokenFile))
                {
                    return (false, $"Profile '{profileName}' does not have a saved session token yet. Sign in or refresh token first.", 0);
                }

                var entry = BuildExportEntry(profileName, profilePath, tokenFile, VaultBridge.IsProfileActiveInIde(profileName, profilePath));
                if (entry == null)
                {
                    return (false, $"Session token for '{profileName}' is empty or unreadable.", 0);
                }

                WriteExportFile(outputPath, new List<JsonObject> { entry });
                return (true, $"Exported session token for '{profileName}'.", 1);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to export session token: {ex.Message}", 0);
            }
        }

        public static (bool Success, string Message, int ExportedCount) ExportAllProfiles(string outputPath)
        {
            try
            {
                var entries = new List<JsonObject>();
                var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (Directory.Exists(AntigravityCore.ProfilesDir))
                {
                    foreach (string dir in Directory.GetDirectories(AntigravityCore.ProfilesDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = Path.GetFileName(dir);
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        VaultBridge.SyncProfileTokenFromVault(dir, name);
                        string tokenFile = TokenService.GetTokenFilePath(dir);
                        if (!File.Exists(tokenFile)) continue;

                        var entry = BuildExportEntry(name, dir, tokenFile, VaultBridge.IsProfileActiveInIde(name, dir));
                        if (entry != null)
                        {
                            entries.Add(entry);
                            string? email = entry["email"]?.ToString();
                            if (VaultBridge.IsValidEmail(email)) seenEmails.Add(email!);
                        }
                    }
                }

                // Also include Default IDE token if it belongs to an account not already in a profile folder
                if (File.Exists(DefaultIdeTokenPath))
                {
                    var defEntry = BuildExportEntry("Default", null, DefaultIdeTokenPath, isDefault: true);
                    if (defEntry != null)
                    {
                        string? defEmail = defEntry["email"]?.ToString();
                        if (!VaultBridge.IsValidEmail(defEmail) || !seenEmails.Contains(defEmail!))
                        {
                            entries.Add(defEntry);
                        }
                    }
                }

                if (entries.Count == 0)
                {
                    return (false, "No signed-in profiles with session tokens were found to export.", 0);
                }

                WriteExportFile(outputPath, entries);
                return (true, $"Exported {entries.Count} session token(s).", entries.Count);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to export session tokens: {ex.Message}", 0);
            }
        }

        public static (bool Success, string Message, int ExportedCount) ExportDefaultIdeToken(string outputPath)
        {
            try
            {
                if (!File.Exists(DefaultIdeTokenPath))
                {
                    return (false, "No default Antigravity IDE session token was found on this machine.", 0);
                }

                var entry = BuildExportEntry("Default", null, DefaultIdeTokenPath, isDefault: true);
                if (entry == null)
                {
                    return (false, "Default Antigravity IDE session token is empty or unreadable.", 0);
                }

                WriteExportFile(outputPath, new List<JsonObject> { entry });
                return (true, "Exported default IDE session token.", 1);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to export default IDE token: {ex.Message}", 0);
            }
        }

        public static async Task<ImportTransferResult> ImportFromFileAsync(
            string inputPath,
            bool autoRefreshExpired = true,
            bool activateDefaultIfNone = true)
        {
            var outcomes = new List<ImportedProfileOutcome>();
            if (!File.Exists(inputPath))
            {
                return new ImportTransferResult(false, "Selected token file does not exist.", outcomes, null, null);
            }

            JsonNode? root;
            try
            {
                string rawText = File.ReadAllText(inputPath);
                root = JsonNode.Parse(rawText);
            }
            catch (Exception ex)
            {
                return new ImportTransferResult(false, $"Invalid JSON file: {ex.Message}", outcomes, null, null);
            }

            var rawEntries = ParseImportEntries(root, Path.GetFileNameWithoutExtension(inputPath));
            if (rawEntries.Count == 0)
            {
                return new ImportTransferResult(
                    false,
                    "No valid Antigravity session tokens were found in this file.",
                    outcomes,
                    null,
                    null);
            }

            Directory.CreateDirectory(AntigravityCore.ProfilesDir);

            // Check whether a valid active default account already exists before importing
            string? existingActiveEmail = VaultBridge.GetActiveAccountEmail();
            bool hadActiveDefault = VaultBridge.IsValidEmail(existingActiveEmail);

            string? preferredDefaultName = null;
            string? firstValidImportedName = null;

            foreach (var entry in rawEntries)
            {
                var outcome = await ImportSingleEntryAsync(entry, autoRefreshExpired);
                outcomes.Add(outcome);

                if (outcome.Action != ImportedProfileAction.SkippedInvalid)
                {
                    firstValidImportedName ??= outcome.ProfileName;
                    if (entry.IsDefault)
                    {
                        preferredDefaultName = outcome.ProfileName;
                    }
                }
            }

            string? candidateDefault = preferredDefaultName ?? (rawEntries.Count == 1 ? firstValidImportedName : null);
            string? activatedDefault = null;
            string? suggestedDefault = null;

            if (!string.IsNullOrEmpty(candidateDefault))
            {
                string candidatePath = Path.Combine(AntigravityCore.ProfilesDir, candidateDefault);
                if (activateDefaultIfNone && !hadActiveDefault)
                {
                    if (VaultBridge.SetActiveProfile(candidateDefault, candidatePath))
                    {
                        activatedDefault = candidateDefault;
                    }
                }
                else if (!VaultBridge.IsProfileActiveInIde(candidateDefault, candidatePath))
                {
                    suggestedDefault = candidateDefault;
                }
            }

            int added = outcomes.Count(o => o.Action == ImportedProfileAction.AddedNew);
            int updated = outcomes.Count(o => o.Action == ImportedProfileAction.UpdatedExisting);
            int already = outcomes.Count(o => o.Action == ImportedProfileAction.AlreadyExists);
            int skipped = outcomes.Count(o => o.Action == ImportedProfileAction.SkippedInvalid);

            bool anyOk = (added + updated + already) > 0;
            var sb = new StringBuilder();
            sb.AppendLine($"Import completed: {added} added, {updated} updated, {already} already present" +
                          (skipped > 0 ? $", {skipped} skipped" : "") + ".");

            foreach (var o in outcomes)
            {
                string tag = o.Action switch
                {
                    ImportedProfileAction.AddedNew => "Added",
                    ImportedProfileAction.UpdatedExisting => "Updated",
                    ImportedProfileAction.AlreadyExists => "Already added",
                    _ => "Skipped"
                };
                string extra = o.Refreshed ? " (refreshed)" : "";
                if (!string.IsNullOrEmpty(o.Note)) extra += $" — {o.Note}";
                sb.AppendLine($"• {o.ProfileName} ({o.Email}): {tag}{extra}");
            }

            if (!string.IsNullOrEmpty(activatedDefault))
            {
                sb.AppendLine();
                sb.AppendLine($"⭐ '{activatedDefault}' was automatically set as the Default (IDE Active) profile.");
            }

            return new ImportTransferResult(anyOk, sb.ToString().TrimEnd(), outcomes, activatedDefault, suggestedDefault);
        }

        private record ParsedImportEntry(
            string? ProfileName,
            string? EmailHint,
            bool IsDefault,
            JsonObject TokenObject);

        private static List<ParsedImportEntry> ParseImportEntries(JsonNode? root, string fallbackFileName)
        {
            var result = new List<ParsedImportEntry>();
            if (root is not JsonObject obj) return result;

            // Format 1: AgVerse bundle { "format": "agverse-session-tokens", "profiles": [ ... ] }
            if (obj["profiles"] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is not JsonObject pObj) continue;
                    if (pObj["token"] is JsonObject tokObj && HasTokenCredentials(tokObj))
                    {
                        result.Add(new ParsedImportEntry(
                            pObj["profile_name"]?.ToString(),
                            pObj["email"]?.ToString(),
                            pObj["is_default"]?.GetValue<bool>() == true,
                            tokObj.DeepClone().AsObject()));
                    }
                }
                return result;
            }

            // Format 2: Single wrapped profile object { "profile_name": "...", "token": { "token": { ... } } }
            if (obj["token"] is JsonObject innerTok && innerTok["token"] is JsonObject)
            {
                if (HasTokenCredentials(innerTok))
                {
                    result.Add(new ParsedImportEntry(
                        obj["profile_name"]?.ToString(),
                        obj["email"]?.ToString(),
                        obj["is_default"]?.GetValue<bool>() == true,
                        innerTok.DeepClone().AsObject()));
                }
                return result;
            }

            // Format 3: Raw jetski-standalone-oauth-token file { "token": { "access_token": ... }, "auth_method": ... }
            if (HasTokenCredentials(obj))
            {
                string? nameHint = fallbackFileName.Equals("jetski-standalone-oauth-token", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : fallbackFileName;
                result.Add(new ParsedImportEntry(
                    nameHint,
                    obj["id_token_email"]?.ToString(),
                    false,
                    obj.DeepClone().AsObject()));
            }

            return result;
        }

        private static bool HasTokenCredentials(JsonObject tokenFileObj)
        {
            if (tokenFileObj["token"] is not JsonObject inner) return false;
            string access = inner["access_token"]?.ToString() ?? "";
            string refresh = inner["refresh_token"]?.ToString() ?? "";
            return !string.IsNullOrWhiteSpace(access) || !string.IsNullOrWhiteSpace(refresh);
        }

        private static async Task<ImportedProfileOutcome> ImportSingleEntryAsync(ParsedImportEntry entry, bool autoRefreshExpired)
        {
            var tokObj = entry.TokenObject;
            var innerTok = tokObj["token"] as JsonObject;
            string accessToken = innerTok?["access_token"]?.ToString() ?? "";
            string refreshToken = innerTok?["refresh_token"]?.ToString() ?? "";
            string idToken = tokObj["id_token"]?.ToString() ?? "";
            string expiryStr = innerTok?["expiry"]?.ToString() ?? "";

            long importedExpUnix = 0;
            bool isImportedExpired = true;
            if (DateTimeOffset.TryParse(expiryStr, out var expOffset))
            {
                importedExpUnix = expOffset.ToUnixTimeSeconds();
                isImportedExpired = expOffset.UtcDateTime <= DateTime.UtcNow.AddMinutes(1);
            }

            // Resolve email from hint -> id_token_email -> id_token JWT -> Google UserInfo
            string? email = VaultBridge.IsValidEmail(entry.EmailHint) ? entry.EmailHint!.Trim() : null;
            if (!VaultBridge.IsValidEmail(email))
            {
                string? blobEmail = tokObj["id_token_email"]?.ToString();
                if (VaultBridge.IsValidEmail(blobEmail)) email = blobEmail!.Trim();
            }
            if (!VaultBridge.IsValidEmail(email))
            {
                string? jwtEmail = TokenService.ExtractEmailFromJwt(idToken);
                if (VaultBridge.IsValidEmail(jwtEmail)) email = jwtEmail!.Trim();
            }
            if (!VaultBridge.IsValidEmail(email) && !string.IsNullOrWhiteSpace(accessToken))
            {
                string? fetched = await TokenService.FetchEmailFromUserInfoAsync(accessToken);
                if (VaultBridge.IsValidEmail(fetched)) email = fetched!.Trim();
            }

            if (VaultBridge.IsValidEmail(email))
            {
                tokObj["id_token_email"] = email;
            }

            // Check if an existing profile already owns this email
            string? existingProfileName = VaultBridge.IsValidEmail(email)
                ? FindExistingProfileByEmail(email!)
                : null;

            string rawBlob = tokObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

            if (!string.IsNullOrEmpty(existingProfileName))
            {
                string existingPath = Path.Combine(AntigravityCore.ProfilesDir, existingProfileName);
                string existingTokenFile = TokenService.GetTokenFilePath(existingPath);
                bool shouldOverwriteLocal = ShouldOverwriteExistingToken(existingTokenFile, importedExpUnix, !string.IsNullOrWhiteSpace(refreshToken));

                if (shouldOverwriteLocal)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(existingTokenFile)!);
                    File.WriteAllText(existingTokenFile, rawBlob, new UTF8Encoding(false));
                    if (VaultBridge.IsValidEmail(email))
                    {
                        VaultBridge.UpdateOrAddAccount(email!, existingProfileName, accessToken, refreshToken, idToken, importedExpUnix, rawBlob);
                    }

                    bool refreshed = false;
                    if (autoRefreshExpired && isImportedExpired && !string.IsNullOrWhiteSpace(refreshToken))
                    {
                        var (ok, _) = await TokenService.RefreshTokenForProfileAsync(existingPath, existingProfileName);
                        refreshed = ok;
                    }

                    return new ImportedProfileOutcome(existingProfileName, email ?? "Unknown email", ImportedProfileAction.UpdatedExisting, refreshed);
                }

                // Existing token is already valid & newer -> keep existing and ensure vault is synced
                VaultBridge.SyncProfileTokenFromVault(existingPath, existingProfileName);
                return new ImportedProfileOutcome(existingProfileName, email ?? "Unknown email", ImportedProfileAction.AlreadyExists, false, "kept newer local token");
            }

            // New account -> determine profile folder name
            string desiredName = ChooseTargetProfileName(entry.ProfileName, email);
            string targetPath = Path.Combine(AntigravityCore.ProfilesDir, desiredName);
            if (!Directory.Exists(targetPath))
            {
                AntigravityCore.CreateProfile(desiredName);
            }

            string destTokenFile = TokenService.GetTokenFilePath(targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destTokenFile)!);
            File.WriteAllText(destTokenFile, rawBlob, new UTF8Encoding(false));

            if (VaultBridge.IsValidEmail(email))
            {
                VaultBridge.UpdateOrAddAccount(email!, desiredName, accessToken, refreshToken, idToken, importedExpUnix, rawBlob);
            }

            bool refreshedNew = false;
            if (autoRefreshExpired && (isImportedExpired || !VaultBridge.IsValidEmail(email)) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                var (ok, _) = await TokenService.RefreshTokenForProfileAsync(targetPath, desiredName);
                refreshedNew = ok;
                if (ok)
                {
                    var (refEmail, hasTok, _, _) = TokenService.GetTokenSummary(targetPath);
                    if (hasTok && VaultBridge.IsValidEmail(refEmail)) email = refEmail;
                }
            }

            return new ImportedProfileOutcome(desiredName, email ?? "Unknown email", ImportedProfileAction.AddedNew, refreshedNew);
        }

        private static string? FindExistingProfileByEmail(string email)
        {
            if (!Directory.Exists(AntigravityCore.ProfilesDir)) return null;
            foreach (string dir in Directory.GetDirectories(AntigravityCore.ProfilesDir))
            {
                var (tEmail, hasToken, _, _) = TokenService.GetTokenSummary(dir);
                if (hasToken && VaultBridge.IsValidEmail(tEmail) &&
                    string.Equals(tEmail, email, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFileName(dir);
                }
            }
            return null;
        }

        private static bool ShouldOverwriteExistingToken(string existingTokenFile, long importedExpUnix, bool importedHasRefresh)
        {
            if (!File.Exists(existingTokenFile)) return true;
            try
            {
                var existingRoot = JsonNode.Parse(File.ReadAllText(existingTokenFile));
                string existingRefresh = existingRoot?["token"]?["refresh_token"]?.ToString() ?? "";
                string existingExpiryStr = existingRoot?["token"]?["expiry"]?.ToString() ?? "";
                if (!DateTimeOffset.TryParse(existingExpiryStr, out var existingOffset)) return true;

                long existingExpUnix = existingOffset.ToUnixTimeSeconds();
                bool existingExpired = existingOffset.UtcDateTime <= DateTime.UtcNow.AddMinutes(1);

                if (existingExpired && (importedExpUnix > existingExpUnix || importedHasRefresh)) return true;
                if (string.IsNullOrWhiteSpace(existingRefresh) && importedHasRefresh) return true;
                return importedExpUnix > existingExpUnix + 60;
            }
            catch
            {
                return true;
            }
        }

        private static string ChooseTargetProfileName(string? preferredName, string? email)
        {
            string rawBase = !string.IsNullOrWhiteSpace(preferredName)
                ? preferredName.Trim()
                : (VaultBridge.IsValidEmail(email) ? email!.Split('@')[0] : "ImportedProfile");

            string clean = SanitizeName(rawBase);
            string candidatePath = Path.Combine(AntigravityCore.ProfilesDir, clean);

            // If a directory with the exact name exists and has NO token linked yet, reuse it cleanly
            if (Directory.Exists(candidatePath))
            {
                var (_, hasToken, _, _) = TokenService.GetTokenSummary(candidatePath);
                if (!hasToken) return clean;
            }
            else
            {
                return clean;
            }

            // Otherwise generate a unique non-colliding name
            int suffix = 2;
            while (Directory.Exists(Path.Combine(AntigravityCore.ProfilesDir, $"{clean}_{suffix}")))
            {
                suffix++;
            }
            return $"{clean}_{suffix}";
        }

        private static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            string res = sb.ToString().Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(res) ? "ImportedProfile" : res;
        }

        private static JsonObject? BuildExportEntry(string profileName, string? profilePath, string tokenFile, bool isDefault)
        {
            try
            {
                string raw = File.ReadAllText(tokenFile);
                var tokNode = JsonNode.Parse(raw) as JsonObject;
                if (tokNode == null || !HasTokenCredentials(tokNode)) return null;

                string? email = tokNode["id_token_email"]?.ToString();
                if (!VaultBridge.IsValidEmail(email))
                {
                    email = TokenService.ExtractEmailFromJwt(tokNode["id_token"]?.ToString());
                }
                if (!VaultBridge.IsValidEmail(email) && !string.IsNullOrEmpty(profilePath))
                {
                    var (sumEmail, hasTok, _, _) = TokenService.GetTokenSummary(profilePath);
                    if (hasTok && VaultBridge.IsValidEmail(sumEmail)) email = sumEmail;
                }

                if (VaultBridge.IsValidEmail(email))
                {
                    tokNode["id_token_email"] = email;
                }

                return new JsonObject
                {
                    ["profile_name"] = profileName,
                    ["email"] = VaultBridge.IsValidEmail(email) ? email : "",
                    ["is_default"] = isDefault,
                    ["token"] = tokNode
                };
            }
            catch
            {
                return null;
            }
        }

        private static void WriteExportFile(string outputPath, List<JsonObject> entries)
        {
            var profilesArr = new JsonArray();
            foreach (var e in entries) profilesArr.Add(e);

            var bundle = new JsonObject
            {
                ["format"] = FormatId,
                ["version"] = FormatVersion,
                ["exported_at"] = DateTime.UtcNow.ToString("o"),
                ["profiles"] = profilesArr
            };

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, bundle.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
    }
}

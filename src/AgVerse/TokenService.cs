using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AgVerse
{
    public static class TokenService
    {
        private static readonly byte[] _cid = new byte[] { 107,106,109,107,106,106,108,106,108,106,111,99,107,119,46,55,50,41,41,51,52,104,50,104,107,54,57,40,63,104,105,111,44,46,53,54,53,48,50,110,61,110,106,105,63,42,116,59,42,42,41,116,61,53,53,61,54,63,47,41,63,40,57,53,52,46,63,52,46,116,57,53,55 };
        private static readonly byte[] _csec = new byte[] { 29,21,25,9,10,2,119,17,111,98,28,13,8,110,98,108,22,62,22,16,107,55,22,24,98,41,2,25,110,32,108,43,30,27,60 };

        public static string ClientId => Unmask(_cid);
        public static string ClientSecret => Unmask(_csec);

        private static string Unmask(byte[] data)
        {
            var res = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) res[i] = (byte)(data[i] ^ 0x5A);
            return Encoding.UTF8.GetString(res);
        }
        public const int RedirectPort = 51121;
        public static readonly string RedirectUri = $"http://localhost:{RedirectPort}/oauth-callback";

        public static readonly string[] Scopes = new[]
        {
            "https://www.googleapis.com/auth/cloud-platform",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/userinfo.profile",
            "https://www.googleapis.com/auth/cclog",
            "https://www.googleapis.com/auth/experimentsandconfigs"
        };

        /// <summary>
        /// Fallback email lookup when Google OAuth responses omit an id_token.
        /// </summary>
        internal static async Task<string?> FetchEmailFromUserInfoAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return null;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v1/userinfo?alt=json");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                using var resp = await client.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                string body = await resp.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(body);
                string? email = node?["email"]?.ToString();
                return VaultBridge.IsValidEmail(email) ? email : null;
            }
            catch
            {
                return null;
            }
        }

        public static string GetTokenFilePath(string profilePath)
        {
            return Path.Combine(profilePath, "home", ".gemini", "jetski-standalone-oauth-token");
        }

        public static (string email, bool hasToken, bool isExpired, string expiryText) GetTokenSummary(string profilePath)
        {
            string tokenFile = GetTokenFilePath(profilePath);
            if (!File.Exists(tokenFile))
            {
                return ("No account linked", false, true, "No token found");
            }

            try
            {
                string json = File.ReadAllText(tokenFile);
                var root = JsonNode.Parse(json);
                if (root == null) return ("Invalid token file", false, true, "Corrupted token");

                string? rawEmail = root["id_token_email"]?.ToString();
                string email = VaultBridge.IsValidEmail(rawEmail) ? rawEmail! : "";
                if (string.IsNullOrEmpty(email))
                {
                    string idToken = root["id_token"]?.ToString() ?? "";
                    string? jwtEmail = ExtractEmailFromJwt(idToken);
                    email = VaultBridge.IsValidEmail(jwtEmail) ? jwtEmail! : "Unknown email";
                }

                string expiryStr = root["token"]?["expiry"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(expiryStr) && DateTimeOffset.TryParse(expiryStr, out var expiryOffset))
                {
                    var now = DateTimeOffset.Now;
                    if (expiryOffset <= now)
                    {
                        return (email, true, true, "Expired");
                    }
                    else
                    {
                        var remaining = expiryOffset - now;
                        string remText = remaining.TotalMinutes < 60
                            ? $"Active ({(int)remaining.TotalMinutes}m left)"
                            : $"Active ({(int)remaining.TotalHours}h {remaining.Minutes}m left)";
                        return (email, true, false, remText);
                    }
                }

                return (email, true, false, "Active");
            }
            catch
            {
                return ("Error reading token", false, true, "Read error");
            }
        }

        public static async Task<(bool success, string message)> RefreshTokenForProfileAsync(string profilePath, string profileName)
        {
            string tokenFile = GetTokenFilePath(profilePath);
            if (!File.Exists(tokenFile))
            {
                return (false, $"No token file found for profile '{profileName}'.");
            }

            JsonNode? root;
            try
            {
                string rawJson = await File.ReadAllTextAsync(tokenFile);
                root = JsonNode.Parse(rawJson);
            }
            catch (Exception ex)
            {
                return (false, "Could not parse token file: " + ex.Message);
            }

            if (root == null) return (false, "Token file is empty.");

            string? refreshToken = root["token"]?["refresh_token"]?.ToString();
            string email = root["id_token_email"]?.ToString() ?? "";
            if (!VaultBridge.IsValidEmail(email))
            {
                email = ExtractEmailFromJwt(root["id_token"]?.ToString()) ?? "";
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return (false, $"No refresh token present in '{profileName}'.");
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "antigravity/hub/2.34.0");

            var formParams = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(formParams));
            }
            catch (Exception ex)
            {
                return (false, "Network error contacting Google OAuth endpoint: " + ex.Message);
            }

            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, $"Google rejected token refresh (HTTP {(int)response.StatusCode}): {responseBody}");
            }

            try
            {
                var respNode = JsonNode.Parse(responseBody);
                if (respNode == null) return (false, "Empty response from Google OAuth.");

                string? newAccessToken = respNode["access_token"]?.ToString();
                if (string.IsNullOrEmpty(newAccessToken))
                {
                    return (false, "No access_token returned by Google.");
                }

                int expiresIn = respNode["expires_in"]?.GetValue<int>() ?? 3599;
                DateTime expiryUtc = DateTime.UtcNow.AddSeconds(expiresIn);

                // Update token node
                var tokenNode = root["token"]?.AsObject();
                if (tokenNode == null)
                {
                    tokenNode = new JsonObject();
                    root["token"] = tokenNode;
                }

                tokenNode["access_token"] = newAccessToken;
                tokenNode["expires_in"] = expiresIn;
                tokenNode["expiry"] = expiryUtc.ToString("o");

                // Update refresh_token if Google rotated it
                string? newRefreshToken = respNode["refresh_token"]?.ToString();
                if (!string.IsNullOrEmpty(newRefreshToken))
                {
                    tokenNode["refresh_token"] = newRefreshToken;
                }

                // Update id_token if returned, preserving existing id_token if refresh omitted it
                string? idToken = respNode["id_token"]?.ToString();
                if (!string.IsNullOrEmpty(idToken))
                {
                    root["id_token"] = idToken;
                    string? extractedEmail = ExtractEmailFromJwt(idToken);
                    if (VaultBridge.IsValidEmail(extractedEmail))
                    {
                        root["id_token_email"] = extractedEmail;
                        email = extractedEmail!;
                    }
                }
                else
                {
                    idToken = root["id_token"]?.ToString();
                }

                if (!VaultBridge.IsValidEmail(email) && !string.IsNullOrEmpty(newAccessToken))
                {
                    string? fetchedEmail = await FetchEmailFromUserInfoAsync(newAccessToken);
                    if (VaultBridge.IsValidEmail(fetchedEmail))
                    {
                        email = fetchedEmail!;
                        root["id_token_email"] = email;
                    }
                }
                else if (VaultBridge.IsValidEmail(email))
                {
                    root["id_token_email"] = email;
                }

                // Write back atomically
                Directory.CreateDirectory(Path.GetDirectoryName(tokenFile)!);
                string rawBlob = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(tokenFile, rawBlob);

                // Bridge: sync to shared Rainmeter vault
                if (VaultBridge.IsValidEmail(email))
                {
                    try
                    {
                        long expUnix = new DateTimeOffset(expiryUtc).ToUnixTimeSeconds();
                        VaultBridge.UpdateOrAddAccount(email, profileName, newAccessToken, newRefreshToken ?? refreshToken, idToken ?? "", expUnix, rawBlob);
                    }
                    catch { }
                }

                return (true, $"Token refreshed successfully for '{profileName}'!\n\nAccount: {email}\nValid until: {expiryUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                return (false, "Error saving refreshed token: " + ex.Message);
            }
        }

        public static async Task<(bool success, string message)> AuthenticateInBrowserAsync(string profilePath, string profileName)
        {
            byte[] verifierBytes = RandomNumberGenerator.GetBytes(32);
            string codeVerifier = Base64UrlEncode(verifierBytes);

            using var sha256 = SHA256.Create();
            byte[] challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            string codeChallenge = Base64UrlEncode(challengeBytes);
            string state = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={Uri.EscapeDataString(ClientId)}&" +
                $"response_type=code&" +
                $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                $"scope={Uri.EscapeDataString(string.Join(" ", Scopes))}&" +
                $"code_challenge={Uri.EscapeDataString(codeChallenge)}&" +
                $"code_challenge_method=S256&" +
                $"state={Uri.EscapeDataString(state)}&" +
                $"access_type=offline&" +
                $"prompt=consent";

            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, RedirectPort);
                listener.Start();
            }
            catch (Exception ex)
            {
                return (false, $"Could not start OAuth listener on port {RedirectPort}: {ex.Message}");
            }

            try
            {
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return (false, $"Failed to open browser: {ex.Message}");
            }

            string? authCode = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                using var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                using var stream = tcpClient.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                string? requestLine = await reader.ReadLineAsync(cts.Token);
                if (!string.IsNullOrEmpty(requestLine))
                {
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length >= 2 && parts[1].Contains("?"))
                    {
                        string queryString = parts[1].Substring(parts[1].IndexOf('?') + 1);
                        foreach (string pair in queryString.Split('&'))
                        {
                            string[] kv = pair.Split('=');
                            if (kv.Length == 2 && kv[0] == "code")
                            {
                                authCode = Uri.UnescapeDataString(kv[1]);
                                break;
                            }
                        }
                    }
                }

                string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>AgVerse Authentication Successful</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
               background: #0d1117; color: #c9d1d9; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .card { background: #161b22; border: 1px solid #30363d; border-radius: 12px; padding: 40px; text-align: center; max-width: 480px; box-shadow: 0 8px 24px rgba(0,0,0,0.5); }
        h1 { color: #58a6ff; font-size: 24px; margin-bottom: 8px; }
        p { color: #8b949e; line-height: 1.5; font-size: 15px; }
        .check { font-size: 56px; color: #3fb950; margin-bottom: 12px; }
        .badge { display: inline-block; background: #238636; color: white; padding: 4px 12px; border-radius: 20px; font-weight: 600; font-size: 13px; margin-top: 10px; }
    </style>
</head>
<body>
    <div class='card'>
        <div class='check'>✓</div>
        <h1>Google Authentication Successful</h1>
        <p>Token captured and saved for profile: <b>" + WebUtility.HtmlEncode(profileName) + @"</b></p>
        <div class='badge'>AgVerse Token Linked</div>
        <p style='margin-top: 24px; font-size: 13px;'>You may now close this tab and return to AgVerse.</p>
    </div>
</body>
</html>";
                byte[] htmlBytes = Encoding.UTF8.GetBytes(html);
                await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {htmlBytes.Length}\r\nConnection: close\r\n\r\n{html}");
            }
            catch (OperationCanceledException)
            {
                return (false, "Authentication timed out (120 seconds).");
            }
            catch (Exception ex)
            {
                return (false, "Error during OAuth callback: " + ex.Message);
            }
            finally
            {
                listener.Stop();
            }

            if (string.IsNullOrEmpty(authCode))
            {
                return (false, "No authorization code was captured from Google.");
            }

            // Exchange authorization code for tokens
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "antigravity/hub/2.34.0");

            var tokenExchangeParams = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["code"] = authCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = codeVerifier
            };

            HttpResponseMessage resp;
            try
            {
                resp = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenExchangeParams));
            }
            catch (Exception ex)
            {
                return (false, "Failed to exchange authorization code: " + ex.Message);
            }

            string tokenRespStr = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return (false, "Token exchange failed: " + tokenRespStr);
            }

            try
            {
                var respNode = JsonNode.Parse(tokenRespStr);
                if (respNode == null) return (false, "Invalid token response from Google.");

                string accessToken = respNode["access_token"]?.ToString() ?? "";
                string refreshToken = respNode["refresh_token"]?.ToString() ?? "";
                string idToken = respNode["id_token"]?.ToString() ?? "";
                int expiresIn = respNode["expires_in"]?.GetValue<int>() ?? 3599;
                DateTime expiryUtc = DateTime.UtcNow.AddSeconds(expiresIn);

                string? resolvedEmail = ExtractEmailFromJwt(idToken);
                if (!VaultBridge.IsValidEmail(resolvedEmail) && !string.IsNullOrEmpty(accessToken))
                {
                    resolvedEmail = await FetchEmailFromUserInfoAsync(accessToken);
                }
                string email = VaultBridge.IsValidEmail(resolvedEmail) ? resolvedEmail! : "Unknown email";

                var tokenFile = GetTokenFilePath(profilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(tokenFile)!);

                var rootObj = new JsonObject
                {
                    ["token"] = new JsonObject
                    {
                        ["access_token"] = accessToken,
                        ["token_type"] = "Bearer",
                        ["refresh_token"] = refreshToken,
                        ["expiry"] = expiryUtc.ToString("o"),
                        ["expires_in"] = expiresIn
                    },
                    ["auth_method"] = "consumer",
                    ["writer_id"] = Guid.NewGuid().ToString("N")
                };
                if (!string.IsNullOrEmpty(idToken))
                {
                    rootObj["id_token"] = idToken;
                }
                if (VaultBridge.IsValidEmail(resolvedEmail))
                {
                    rootObj["id_token_email"] = resolvedEmail;
                }

                string rawBlob = rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(tokenFile, rawBlob);

                // Bridge: sync to shared Rainmeter vault
                if (VaultBridge.IsValidEmail(resolvedEmail))
                {
                    try
                    {
                        long expUnix = new DateTimeOffset(expiryUtc).ToUnixTimeSeconds();
                        VaultBridge.UpdateOrAddAccount(resolvedEmail!, profileName, accessToken, refreshToken, idToken, expUnix, rawBlob);
                    }
                    catch { }
                }

                return (true, $"Signed in successfully for '{profileName}'!\n\nAccount: {email}\nToken valid until: {expiryUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                return (false, "Error storing tokens: " + ex.Message);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        public static string? ExtractEmailFromJwt(string? jwt)
        {
            if (string.IsNullOrEmpty(jwt) || !jwt.Contains('.')) return null;
            try
            {
                string[] parts = jwt.Split('.');
                if (parts.Length < 2) return null;
                string payload = parts[1].Replace("-", "+").Replace("_", "/");
                int mod = payload.Length % 4;
                if (mod == 2) payload += "==";
                else if (mod == 3) payload += "=";

                byte[] bytes = Convert.FromBase64String(payload);
                using var doc = JsonDocument.Parse(bytes);
                if (doc.RootElement.TryGetProperty("email", out var emailProp))
                {
                    return emailProp.GetString();
                }
            }
            catch { }
            return null;
        }
    }
}

using System.Net.Http;
using System.Text.Json;
using Claudestrap;

namespace Claudestrap.Utility
{
    /// <summary>
    /// Roblox account network layer -- CSRF token, auth ticket, and identity
    /// lookup for a saved .ROBLOSECURITY cookie. Ported from MultiRoblox-RAM's
    /// roblox_api.rs (get_csrf_token / get_auth_ticket / fetch_user_info):
    /// same endpoints, same header shape, same 403/429 retry-with-fresh-token
    /// behaviour.
    /// </summary>
    public static class RobloxAuthApi
    {
        /// <summary>
        /// Account requests get their own cookie-less client instead of App.HttpClient.
        /// Roblox hands back a rotated .ROBLOSECURITY on the auth endpoints, and the
        /// shared handler has cookie handling on by default -- its container would then
        /// append that stored cookie alongside the per-account one we set here, and
        /// Roblox authenticates whichever it picks. That made every saved account fetch
        /// a ticket for whichever account happened to be in the container first, so the
        /// wrong account launched.
        /// </summary>
        private static readonly HttpClient Client = new(
            new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false
            })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

        public sealed record UserInfoResult(bool Ok, string? Username, string? UserId, string? Reason);

        public static async Task<UserInfoResult> FetchUserInfoAsync(string cookie)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated");
                req.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                req.Headers.Add("Accept", "application/json");
                req.Headers.Add("User-Agent", UA);

                using var res = await Client.SendAsync(req).ConfigureAwait(false);
                string body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    string? username = doc.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    string userId = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt64().ToString() : idProp.GetString() ?? "";
                    return new UserInfoResult(true, username, userId, null);
                }

                return new UserInfoResult(false, null, null, body.Length > 200 ? body[..200] : body);
            }
            catch (Exception ex)
            {
                return new UserInfoResult(false, null, null, ex.Message);
            }
        }

        private static async Task<string?> CsrfFromEndpointAsync(string cookie, string endpoint)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"https://auth.roblox.com{endpoint}");
                req.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                req.Headers.Add("User-Agent", UA);
                req.Headers.Add("Accept", "application/json");
                req.Content = new StringContent("");
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                using var res = await Client.SendAsync(req).ConfigureAwait(false);
                return res.Headers.TryGetValues("x-csrf-token", out var values) ? values.FirstOrDefault() : null;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string?> GetCsrfTokenAsync(string cookie)
        {
            foreach (string endpoint in new[] { "/v2/logout", "/v1/logout" })
            {
                string? token = await CsrfFromEndpointAsync(cookie, endpoint).ConfigureAwait(false);
                if (token is not null)
                    return token;
            }

            return null;
        }

        public sealed record TicketResult(bool Ok, string? Ticket, string? Error);

        public static async Task<TicketResult> GetAuthTicketAsync(string cookie, string? csrfToken)
        {
            const string LOG_IDENT = "RobloxAuthApi::GetAuthTicketAsync";

            string? token = csrfToken;
            int[] delaysMs = { 0, 2000, 5000 };
            int lastStatus = 0;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (delaysMs[attempt] > 0)
                    await Task.Delay(delaysMs[attempt]).ConfigureAwait(false);

                HttpResponseMessage res;
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket");
                    req.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
                    req.Headers.Add("Referer", "https://www.roblox.com");
                    req.Headers.Add("Origin", "https://www.roblox.com");
                    req.Headers.Add("User-Agent", UA);
                    req.Headers.Add("Accept", "application/json");
                    if (token is not null)
                        req.Headers.Add("X-CSRF-TOKEN", token);
                    req.Content = new StringContent("");
                    req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    res = await Client.SendAsync(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Request failed: {ex.Message}");
                    continue;
                }

                using (res)
                {
                    lastStatus = (int)res.StatusCode;

                    if (res.Headers.TryGetValues("rbx-authentication-ticket", out var ticketValues))
                    {
                        string? ticket = ticketValues.FirstOrDefault();
                        if (!string.IsNullOrEmpty(ticket))
                            return new TicketResult(true, ticket, null);
                    }

                    if (lastStatus == 429)
                    {
                        int retryAfter = 8;
                        if (res.Headers.TryGetValues("retry-after", out var raValues) && int.TryParse(raValues.FirstOrDefault(), out int ra))
                            retryAfter = ra;

                        await Task.Delay(TimeSpan.FromSeconds(retryAfter)).ConfigureAwait(false);
                        token = await GetCsrfTokenAsync(cookie).ConfigureAwait(false);
                        if (token is null)
                            return new TicketResult(false, null, "Rate limited and could not refresh token. Wait a moment and try again.");
                        continue;
                    }

                    if (lastStatus == 403)
                    {
                        token = await GetCsrfTokenAsync(cookie).ConfigureAwait(false);
                        if (token is null)
                            return new TicketResult(false, null, "Authentication failed (403). Cookie may be expired.");
                        continue;
                    }
                }
            }

            if (lastStatus != 0)
                return new TicketResult(false, null, $"Auth ticket request failed (HTTP {lastStatus}) after 3 attempts. Try again in a moment.");

            return new TicketResult(false, null, "Still rate limited after 3 attempts. Please wait 30 seconds and try again.");
        }

        /// <summary>
        /// Builds the roblox-player launch URI for a fresh auth ticket, home-launch
        /// shaped (no specific place target) -- matches MultiRoblox's do_launch
        /// "app" launchmode branch.
        /// </summary>
        public static string BuildLaunchUri(string ticket)
        {
            long launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ulong browserId = (ulong)Random.Shared.NextInt64(1_000_000_000_000, 9_999_999_999_999);
            return $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{launchTime}+browsertrackerid:{browserId}+robloxLocale:en_us+gameLocale:en_us";
        }
    }
}

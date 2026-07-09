using ExploitStrap.Utility.Accounts;

namespace ExploitStrap.Utility
{
    // Backs the Server Browser page.
    //
    // Two very different jobs:
    //   1. FetchServersAsync — the public, no-auth games API. Reliable. Returns players/ping/fps but
    //      NOT the server IP, so it can't tell you the region on its own.
    //   2. ResolveServerIpAsync — best-effort region lookup. Roblox only hands out a server's IP through
    //      the authenticated gamejoin API (the same call the website makes when you click "join"), so we
    //      borrow a saved account's cookie to ask. This is rate-limited and undocumented, so it's strictly
    //      opt-in (the page resolves regions one server at a time, on demand) and degrades to "unknown"
    //      whenever Roblox declines. We never fire it for a whole list at once — that would risk the
    //      account.
    public static class ServerBrowserClient
    {
        private const string LOG_IDENT = "ServerBrowserClient";

        // One page of public servers for a place (newest-first), players/ping/fps included. No auth.
        public static async Task<ServerListResponse?> FetchServersAsync(long placeId, string? cursor = null, int sortOrder = 2)
        {
            try
            {
                string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?limit=100&sortOrder={sortOrder}";
                if (!string.IsNullOrEmpty(cursor))
                    url += $"&cursor={Uri.EscapeDataString(cursor)}";

                return await Http.GetJson<ServerListResponse>(url);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::FetchServers", ex);
                return null;
            }
        }

        // The least-populated joinable public server. sortOrder=1 returns emptiest-first; we still pick
        // the least-full one client-side so it holds up even if that ordering ever changes. Returns null
        // when the game has no joinable public servers or the API declines. No auth needed.
        public static async Task<GameServer?> GetEmptiestServerAsync(long placeId)
        {
            var resp = await FetchServersAsync(placeId, null, sortOrder: 1);
            if (resp?.Data is null)
                return null;

            return resp.Data
                .Where(s => !string.IsNullOrEmpty(s.Id) && s.Playing < s.MaxPlayers)
                .OrderBy(s => s.Playing)
                .FirstOrDefault();
        }

        // Region lookups need a saved account to authenticate the gamejoin call. The server IP is the
        // same no matter who asks, so any one saved account works.
        public static bool CanResolveRegions => AccountManager.All.Count > 0;

        // Best-effort: resolve one server's IP via the gamejoin API, authenticated with a saved account's
        // cookie. Returns null when there's no account, the cookie's expired, Roblox rate-limits the call,
        // or the response carries no IP. The response shape is undocumented and changes over time, so we
        // don't model it — we just pull the server IP out by regex (a Roblox datacenter IP lives in
        // 128.116.0.0/16; otherwise we take whatever MachineAddress/UDMUX address is present).
        public static async Task<string?> ResolveServerIpAsync(long placeId, string jobId)
        {
            var account = AccountManager.All.FirstOrDefault();
            if (account is null)
                return null;

            string? cookie = SecureStore.Unprotect(account.EncryptedCookieB64);
            if (string.IsNullOrEmpty(cookie))
                return null;

            try
            {
                // gameJoinAttemptId just has to be a unique GUID per attempt; Roblox echoes it back.
                string body =
                    $"{{\"placeId\":{placeId},\"isTeleport\":false,\"gameId\":\"{jobId}\"," +
                    $"\"gameJoinAttemptId\":\"{Guid.NewGuid()}\"}}";

                string? raw = await PostGameJoinAsync(cookie, body, null);
                if (string.IsNullOrEmpty(raw))
                    return null;

                // Prefer a real Roblox datacenter IP (128.116.x.x) — that's what the datacenter map keys on.
                var dc = Regex.Match(raw, @"128\.116\.\d{1,3}\.\d{1,3}");
                if (dc.Success)
                    return dc.Value;

                // Otherwise fall back to any address field (UDMUX edge IP); ipinfo.io still gets a rough region.
                var any = Regex.Match(
                    raw,
                    "\"(?:MachineAddress|Address|ip)\"\\s*:\\s*\"(\\d{1,3}(?:\\.\\d{1,3}){3})\"",
                    RegexOptions.IgnoreCase);
                return any.Success ? any.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::ResolveServerIp", ex);
                return null;
            }
        }

        // POSTs to the gamejoin endpoint. Roblox answers the first (token-less) call with a 403 that
        // carries a fresh x-csrf-token; we read it and retry once. The cookie is set per-request because
        // App.HttpClient runs with UseCookies=false (see App.HttpClient). Cookies are never logged.
        private static async Task<string?> PostGameJoinAsync(string cookie, string body, string? csrf)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://gamejoin.roblox.com/v1/join-game-instance");
            req.Headers.TryAddWithoutValidation("Cookie", $".ROBLOSECURITY={cookie}");
            req.Headers.TryAddWithoutValidation("User-Agent", "Roblox/WinInet");
            req.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
            if (!string.IsNullOrEmpty(csrf))
                req.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrf);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await App.HttpClient.SendAsync(req);

            if (resp.StatusCode == HttpStatusCode.Forbidden && string.IsNullOrEmpty(csrf)
                && resp.Headers.TryGetValues("x-csrf-token", out var values))
            {
                string? token = values.FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    return await PostGameJoinAsync(cookie, body, token);
            }

            if (!resp.IsSuccessStatusCode)
            {
                App.Logger.WriteLine(LOG_IDENT, $"gamejoin returned {(int)resp.StatusCode} {resp.ReasonPhrase} — region lookup skipped (rate limit?).");
                return null;
            }

            return await resp.Content.ReadAsStringAsync();
        }
    }
}

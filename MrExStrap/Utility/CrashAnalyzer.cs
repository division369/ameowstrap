namespace ExploitStrap.Utility
{
    // After Roblox crashes, reads the newest Roblox client logs and tries to name a likely
    // third-party or environmental cause — a blocked overlay/anti-cheat, a firewall/VPN, a
    // graphics-driver device loss, or running out of memory. Returns a short user-facing
    // paragraph for the crash dialog, or null when nothing recognisable is found (the dialog
    // then falls back to its generic Roblox-side message).
    //
    // The whole point is to tell the user, when we actually can, that the crash was NOT
    // ExploitStrap. Everything here is best-effort: any failure returns null. Messages are
    // hedged ("looks like", "can crash it") because a matched signature is a strong hint, not
    // proof, and we never want to wrongly blame the user's setup.
    public static class CrashAnalyzer
    {
        private const string LOG_IDENT = "CrashAnalyzer";

        public static string? Analyze()
        {
            try
            {
                foreach (string content in RecentRobloxLogs())
                {
                    string? cause = Classify(content);
                    if (cause is not null)
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Attributed the crash to a recognised third-party/environmental cause.");
                        return cause;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            return null;
        }

        // Roblox writes one log per launch to %LocalAppData%\Roblox\logs. The crash we're
        // explaining is fresh, so only consider logs touched in the last 15 minutes, newest
        // first. Share read+write because Roblox may still hold the newest one open.
        private static IEnumerable<string> RecentRobloxLogs(int max = 3)
        {
            string logDir;
            try { logDir = Path.Combine(Paths.LocalAppData, "Roblox", "logs"); }
            catch { yield break; }

            if (!Directory.Exists(logDir))
                yield break;

            FileInfo[] files;
            try
            {
                var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(15);
                files = new DirectoryInfo(logDir).GetFiles("*.log")
                    .Where(f => f.LastWriteTimeUtc >= cutoff)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(max)
                    .ToArray();
            }
            catch { yield break; }

            foreach (var file in files)
            {
                string content;
                try
                {
                    content = ReadTail(file.FullName, MaxReadBytes);
                }
                catch { continue; }

                yield return content;
            }
        }

        // A crash-on-launch log is small (the crash fires under 60s), but an older long-session
        // log can be tens of MB. Read only the last MaxReadBytes so a big log can't hang the
        // dialog or spike memory. That still covers a small crashed log in full, and for a large
        // one the tail is where an end-of-session failure shows up.
        private const long MaxReadBytes = 1024 * 1024;

        private static string ReadTail(string path, long maxBytes)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length > maxBytes)
                fs.Seek(-maxBytes, SeekOrigin.End);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }

        // First match wins, ordered most-definitive cause first.
        private static string? Classify(string log)
        {
            // Graphics driver lost the device — a hard, unambiguous crash.
            if (Regex.IsMatch(log, @"DXGI_ERROR_DEVICE_REMOVED|DXGI_ERROR_DEVICE_HUNG|GfxCrash|graphics device (removed|lost)|D3D.{0,20}device (removed|lost)", RegexOptions.IgnoreCase))
                return "This looks like a **graphics driver** problem — Roblox lost contact with your GPU. "
                     + "Update your graphics driver (or roll it back if you just updated it), and try lowering "
                     + "Roblox's graphics quality. This is not an ExploitStrap issue.";

            // Ran out of memory.
            if (Regex.IsMatch(log, @"bad_alloc|out of memory|OutOfMemory|Not enough memory", RegexOptions.IgnoreCase))
                return "Roblox ran **out of memory**. Close other apps (browsers especially) and try again. "
                     + "This is not an ExploitStrap issue.";

            // Firewall / antivirus / VPN deliberately blocking the connection: Winsock 10013 is
            // ACCESS DENIED, a security product refusing the socket. Ranked above the overlay
            // block below because that block is usually benign, while access-denied is a real,
            // deliberate block that breaks connectivity.
            if (Regex.IsMatch(log, @"OS_ERRNO:\s*10013|WSAEACCES|errno[:=\s]+10013", RegexOptions.IgnoreCase))
                return "Your **firewall, antivirus, or VPN** looks like it is blocking Roblox from connecting "
                     + "(Windows error 10013, access denied). Allow Roblox through your firewall and antivirus, "
                     + "or turn off your VPN, then try again. This is not an ExploitStrap issue.";

            // Roblox's anti-cheat blocked a third-party overlay/capture tool from hooking the game.
            var blocked = Regex.Match(log, @"Blocked DLL:.*?([^\\/]+\.dll)", RegexOptions.IgnoreCase);
            if (blocked.Success)
            {
                string dll = blocked.Groups[1].Value.ToLowerInvariant();
                string tool =
                    dll.Contains("nvspcap") || dll.Contains("nvcamera") || dll.StartsWith("nvgx") || dll.Contains("nvidia") ? "the NVIDIA GeForce Experience / ShadowPlay overlay"
                    : dll.Contains("rtss") ? "the RivaTuner / MSI Afterburner overlay"
                    : dll.Contains("discord") ? "the Discord in-game overlay"
                    : dll.Contains("gameoverlayrenderer") ? "the Steam overlay"
                    : dll.Contains("graphics-hook") ? "OBS (game capture)"
                    : dll.Contains("fraps") ? "Fraps"
                    : "a third-party overlay or screen-capture tool";
                return $"Roblox's anti-cheat blocked **{tool}** from hooking into the game, which can crash it. "
                     + "Turn that overlay or capture tool off for Roblox and try again. This is not an ExploitStrap issue.";
            }

            // Weaker signal, last: a run of connection failures straight to Roblox with no explicit
            // access-denied. Generic connectivity trouble rather than a named blocker.
            if (Regex.Matches(log, @"Failed to connect to \S*roblox\.com", RegexOptions.IgnoreCase).Count >= 3)
                return "Roblox couldn't reach its servers — this looks like a **network or connection** problem "
                     + "on your side (Wi-Fi, VPN, or an unstable connection), not ExploitStrap. Check your "
                     + "connection and try again.";

            return null;
        }
    }
}

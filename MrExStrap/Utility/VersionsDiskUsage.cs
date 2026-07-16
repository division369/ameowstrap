namespace ExploitStrap.Utility
{
    // Recursively sums file sizes under each Paths.Versions\<guid> install so the
    // Versions Manager tab can show users how much disk space each profile uses.
    // All calls are best-effort and return 0 on permission errors or missing dirs
    // — never throw, since the UI binds these values straight to TextBlocks.
    public static class VersionsDiskUsage
    {
        private const string LOG_IDENT = "VersionsDiskUsage";

        public static long GetUsageBytes(string versionGuid)
        {
            if (string.IsNullOrEmpty(versionGuid) || string.IsNullOrEmpty(Paths.Versions))
                return 0;

            string dir = Path.Combine(Paths.Versions, versionGuid);
            return GetDirectorySize(dir);
        }

        private static long GetDirectorySize(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return 0;

            long size = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { size += new FileInfo(file).Length; }
                    catch { /* file gone or no access — skip */ }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::GetDirectorySize", ex);
            }
            return size;
        }

        // "1.4 GB" / "684 MB" / "0 B" — fits a one-line disk usage label in the UI.
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024.0:0.0} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.0} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.00} GB";
        }
    }
}

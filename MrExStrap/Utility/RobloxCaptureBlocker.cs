namespace ExploitStrap.Utility
{
    // Disables Roblox's built-in screenshot and video capture without touching the Roblox process
    // or any FastFlag. Roblox saves screenshots to %MyPictures%\Roblox and recordings to
    // %MyVideos%\Roblox; if we sit a read-only placeholder FILE where each of those folders would
    // go, Roblox can't create the folder or write a capture into it, so the in-game capture just
    // fails. Toggling off removes the placeholder and restores any folder we moved aside.
    //
    // Independent implementation: the save locations are Roblox's own behaviour and a read-only
    // block is the obvious approach, so no third-party code is used here.
    public static class RobloxCaptureBlocker
    {
        private const string LOG_IDENT = "RobloxCaptureBlocker";
        private const string BackupSuffix = " (captures backup)";

        private static IEnumerable<string> CapturePaths()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Roblox");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Roblox");
        }

        // True only when every capture path is currently blocked (read-only placeholder in place).
        public static bool IsBlocked => CapturePaths().All(IsPathBlocked);

        public static void SetBlocked(bool blocked)
        {
            foreach (string path in CapturePaths())
            {
                try
                {
                    if (blocked)
                        Block(path);
                    else
                        Unblock(path);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        private static bool IsPathBlocked(string path)
        {
            try
            {
                return File.Exists(path) && !Directory.Exists(path)
                    && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
            }
            catch { return false; }
        }

        private static void Block(string path)
        {
            if (IsPathBlocked(path))
                return;

            // Keep any captures the user already has: move a non-empty folder aside, drop an empty
            // one. Then leave a read-only placeholder file so Roblox can't write captures here.
            string backup = path + BackupSuffix;
            bool movedToBackup = false;

            if (Directory.Exists(path))
            {
                if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                    if (!Directory.Exists(backup))
                    {
                        Directory.Move(path, backup);
                        movedToBackup = true;
                    }
                    else
                    {
                        Directory.Delete(path, recursive: true); // a backup already holds the originals
                    }
                }
                else
                {
                    Directory.Delete(path);
                }
            }

            try
            {
                File.WriteAllBytes(path, Array.Empty<byte>());
                File.SetAttributes(path, FileAttributes.ReadOnly);
            }
            catch
            {
                // Roblox may have recreated the folder in the gap, or the write was denied.
                // Don't strand the user's captures in the backup folder — put them back.
                if (movedToBackup && Directory.Exists(backup) && !Directory.Exists(path))
                {
                    try { Directory.Move(backup, path); } catch { }
                }
                throw;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Blocked Roblox captures at {path}");
        }

        private static void Unblock(string path)
        {
            if (File.Exists(path) && !Directory.Exists(path))
            {
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                File.Delete(path);
            }

            string backup = path + BackupSuffix;
            if (!Directory.Exists(path) && Directory.Exists(backup))
                Directory.Move(backup, path);

            App.Logger.WriteLine(LOG_IDENT, $"Unblocked Roblox captures at {path}");
        }
    }
}

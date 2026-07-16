namespace ExploitStrap.Utility
{
    // Manages Windows directory junctions used to expose per-profile Roblox
    // install dirs under the standard "Versions\version-<hash>\" name. This is
    // the v420.24 fix for flippi's two reports on v420.23:
    //   1. Same-hash profiles shared one install folder, so an executor (syn z)
    //      installed under one profile would leak into another (wave). With
    //      junctions, each profile has its own real folder at
    //      Versions\profile-<id>\ and only the active profile's junction
    //      Versions\version-<active-hash>\ -> Versions\profile-<active-id>\
    //      exists at any moment.
    //   2. Profile switches re-extracted Roblox packages even when nothing had
    //      changed, because the InstalledVersionGuid empty-string fallback was
    //      broken. Fixed in Bootstrapper.cs; this helper enables the layout
    //      that makes that fix matter.
    //
    // Junctions (mklink /J) do not require admin/Developer-Mode like symbolic
    // links do, so this works for every user. Most executors that parse the
    // install-dir name see the junction path verbatim — they get the
    // version-<hash> name they expect.
    public static class VersionJunctionManager
    {
        private const string LOG_IDENT = "VersionJunctionManager";

        // True when the directory at `path` is a reparse point (junction or
        // symlink). Either is safe to remove via Directory.Delete(path, false).
        public static bool IsJunction(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return false;
                var attrs = File.GetAttributes(path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::IsJunction", ex);
                return false;
            }
        }

        // Create a directory junction at `junctionPath` that resolves to
        // `targetDir`. `junctionPath` must not exist already. `targetDir`
        // should exist (mklink /J accepts a missing target but the resulting
        // junction is broken).
        public static bool CreateJunction(string junctionPath, string targetDir)
        {
            try
            {
                Directory.CreateDirectory(targetDir);

                var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var proc = Process.Start(psi);
                if (proc is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to start cmd.exe for mklink /J {junctionPath} -> {targetDir}");
                    return false;
                }

                // Read both pipes concurrently BEFORE waiting. Reading them serially after
                // WaitForExit is the classic pipe-buffer deadlock shape (safe here only
                // because mklink's output is tiny, but no reason to keep the footgun).
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(true); } catch { }
                    App.Logger.WriteLine(LOG_IDENT, $"mklink /J timed out for {junctionPath} -> {targetDir}");
                    return false;
                }

                string stdout = stdoutTask.GetAwaiter().GetResult().Trim();
                string stderr = stderrTask.GetAwaiter().GetResult().Trim();

                if (proc.ExitCode != 0 || !Directory.Exists(junctionPath) || !IsJunction(junctionPath))
                {
                    string msg = !string.IsNullOrEmpty(stderr) ? stderr
                                 : !string.IsNullOrEmpty(stdout) ? stdout
                                 : $"exit code {proc.ExitCode}";
                    App.Logger.WriteLine(LOG_IDENT, $"mklink /J failed for {junctionPath} -> {targetDir}: {msg}");
                    return false;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Created junction {junctionPath} -> {targetDir}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::CreateJunction", ex);
                return false;
            }
        }

        // Remove a junction without affecting its target. Directory.Delete with
        // recursive=false treats reparse points as unlink-only, so the target
        // dir's contents are safe.
        public static bool DeleteJunction(string junctionPath)
        {
            try
            {
                if (!Directory.Exists(junctionPath))
                    return true;
                if (!IsJunction(junctionPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Refusing to DeleteJunction on non-junction {junctionPath} — caller must handle real dirs explicitly.");
                    return false;
                }
                Directory.Delete(junctionPath, recursive: false);
                App.Logger.WriteLine(LOG_IDENT, $"Removed junction {junctionPath}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::DeleteJunction", ex);
                return false;
            }
        }

        // Tear down whatever's at junctionPath (existing junction or a real dir
        // that got dropped there outside our control) and lay down a fresh
        // junction resolving to targetDir. A real dir is preserved by renaming
        // it to <name>.orphan-<utc> rather than deleted, so user data never
        // disappears silently. Returns true on success.
        //
        // Used by Bootstrapper at launch time and by the Versions Manager's
        // "Set as install target" button so both paths take the same code.
        public static bool RepointJunction(string junctionPath, string targetDir)
        {
            try
            {
                if (Directory.Exists(junctionPath))
                {
                    if (IsJunction(junctionPath))
                    {
                        // Already resolving to the target? Leave it untouched. Tearing down and
                        // recreating a junction that's already correct is pure churn, and during
                        // a multi-instance launch it's actively harmful: starting a second client
                        // of the same profile would briefly unlink the very junction the first
                        // client is still running out of. Skipping the rebuild keeps concurrent
                        // same-profile launches from disturbing each other.
                        if (JunctionTargetMatches(junctionPath, targetDir))
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Junction {junctionPath} already resolves to {targetDir}; leaving it in place.");
                            return true;
                        }

                        DeleteJunction(junctionPath);
                    }
                    else
                    {
                        // Preserve any real dir we didn't expect — could be the user's
                        // own work. Rename rather than delete.
                        string parent = Path.GetDirectoryName(junctionPath)
                                       ?? throw new InvalidOperationException("junctionPath has no parent directory");
                        string orphanName = $"{Path.GetFileName(junctionPath)}.orphan-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
                        string orphanPath = Path.Combine(parent, orphanName);
                        try
                        {
                            Directory.Move(junctionPath, orphanPath);
                            App.Logger.WriteLine(LOG_IDENT, $"Set aside real dir {junctionPath} -> {orphanPath} before creating junction.");
                        }
                        catch (Exception ex)
                        {
                            App.Logger.WriteException(LOG_IDENT + "::RepointJunction", ex);
                            return false;
                        }
                    }
                }

                return CreateJunction(junctionPath, targetDir);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::RepointJunction", ex);
                return false;
            }
        }

        // True when the existing junction at junctionPath already resolves to targetDir
        // (path-normalised, case-insensitive). Lets RepointJunction no-op when nothing needs
        // to change instead of churning the link out from under a running client.
        private static bool JunctionTargetMatches(string junctionPath, string targetDir)
        {
            string? current = GetJunctionTargetName(junctionPath);
            if (string.IsNullOrEmpty(current))
                return false;

            try
            {
                string a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
                string b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir));
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::JunctionTargetMatches", ex);
                return false;
            }
        }

        // Resolve the immediate junction target. Returns the absolute target
        // path, or null if junctionPath isn't a junction (or any error). Uses
        // .NET 6's Directory.ResolveLinkTarget for the actual work — wraps it
        // in a try/catch so callers can treat the null return as "no junction
        // here, move on" without having to handle exceptions themselves.
        public static string? GetJunctionTargetName(string junctionPath)
        {
            try
            {
                if (!IsJunction(junctionPath))
                    return null;
                var target = Directory.ResolveLinkTarget(junctionPath, returnFinalTarget: false);
                return target?.FullName;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::GetJunctionTargetName", ex);
                return null;
            }
        }
    }
}

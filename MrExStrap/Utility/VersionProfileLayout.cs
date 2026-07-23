namespace ExploitStrap.Utility
{
    // Replaces the v420.24 junction layout, which launched every client through an mklink /J reparse
    // point at Versions\version-<hash>\ pointing to Versions\profile-<id>\.
    //
    // Why it changed (2026-07-19): a user's diagnostics bundle contained a same-machine control —
    // one Fishstrap session and three ExploitStrap sessions, same build, same hour. Fishstrap's
    // client disconnected cleanly at 92s; all three of ours died 41-58s in while still connected to
    // the server, with the log truncating mid-gameplay and no shutdown sequence. That is Hyperion's
    // signature, and the reparse point in the launch path was the only structural difference.
    //
    // The layout now has no junction at all:
    //
    //     Versions\version-<hash>\   real directory — the ACTIVE profile's install
    //     Versions\profile-<id>\     real directories — every inactive profile's install, parked
    //
    // Switching profiles is two same-volume renames (park the outgoing, unpark the incoming), which
    // NTFS does atomically and instantly regardless of how many gigabytes are inside. Three things
    // fall out of that:
    //
    //   * nothing is launched through a reparse point, which is the crash fix;
    //   * both the PEB path and the NTFS-resolved path read ...\Versions\version-<hash>\, so tools
    //     that identify the build from the folder name work whichever API they use;
    //   * the path gets SHORTER. Roblox's deepest content files sit ~170 characters below the
    //     version folder, and a custom profile's real path (Versions\profile-<36-char-guid>\) was
    //     already 266 characters against Win32's 260 limit — those files have been failing to
    //     extract silently. Win32 enforces the limit on the string you pass, not the one it
    //     resolves to, which is how the junction hid it. Versions\version-<hash>\ is 246.
    //
    // Per-profile isolation is preserved: only one profile is ever unparked, so two profiles pinned
    // to the same hash still cannot share a folder.
    public static class VersionProfileLayout
    {
        private const string LOG_IDENT = "VersionProfileLayout";

        public static string ParkedPath(string profileId) => Path.Combine(Paths.Versions, "profile-" + profileId);

        public static string ActivePath(string versionGuid) => Path.Combine(Paths.Versions, versionGuid);

        /// <summary>
        /// Makes <paramref name="profile"/>'s install the one living at Versions\<paramref name="versionGuid"/>\,
        /// parking whichever profile was there before. Returns the directory the client should launch
        /// from — the active path on success, or the profile's parked path if the shuffle couldn't be
        /// completed, so a launch never hard-fails over this.
        /// </summary>
        public static string EnsureActive(VersionProfile profile, string versionGuid)
        {
            string active = ActivePath(versionGuid);
            string parked = ParkedPath(profile.Id);

            try
            {
                Directory.CreateDirectory(Paths.Versions);

                RemoveLegacyJunction(active);

                // Already unparked and ours? Nothing to do — the common case on repeat launches of
                // the same profile.
                if (App.State.Prop.ActiveInstallProfileId == profile.Id
                    && App.State.Prop.ActiveInstallVersionGuid == versionGuid
                    && Directory.Exists(active)
                    && !VersionJunctionManager.IsJunction(active))
                {
                    return active;
                }

                if (Directory.Exists(active))
                {
                    if (!ParkCurrentOccupant(active, versionGuid))
                        return Directory.Exists(parked) ? parked : active;
                }

                // Unpark ours if we have an install; otherwise leave the path free for the
                // downloader to populate.
                if (Directory.Exists(parked))
                {
                    Directory.Move(parked, active);
                    App.Logger.WriteLine(LOG_IDENT, $"Unparked '{profile.Name}' → {Path.GetFileName(active)}");
                }
                else
                {
                    Directory.CreateDirectory(active);
                }

                RecordOwner(profile.Id, versionGuid);
                return active;
            }
            catch (Exception ex)
            {
                // Never let a layout problem stop a launch. Falling back to the parked path costs us
                // the short-path and folder-name benefits but still runs.
                App.Logger.WriteException(LOG_IDENT + "::EnsureActive", ex);
                return Directory.Exists(parked) ? parked : active;
            }
        }

        /// <summary>
        /// Moves whatever currently occupies the active path back to its owner's parked folder.
        /// Returns false when it couldn't be moved — almost always because a client is still running
        /// out of it — so the caller can back off rather than clobber a live install.
        /// </summary>
        private static bool ParkCurrentOccupant(string active, string versionGuid)
        {
            string? ownerId = App.State.Prop.ActiveInstallProfileId;

            // No recorded owner means this is a pre-migration install, or State was lost. Treat it as
            // belonging to whichever profile is pinned to this hash; if none is, it's a legacy or
            // Studio directory and we leave it alone rather than risk moving something we don't own.
            if (string.IsNullOrEmpty(ownerId))
            {
                var claimant = App.Settings.Prop.VersionProfiles.FirstOrDefault(p =>
                    string.Equals(p.InstalledVersionGuid, versionGuid, StringComparison.OrdinalIgnoreCase));

                if (claimant is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{Path.GetFileName(active)} has no recorded owner and no profile claims it — adopting it in place.");
                    return true; // let the caller take it over as-is
                }

                ownerId = claimant.Id;
            }

            string ownerParked = ParkedPath(ownerId);

            try
            {
                if (Directory.Exists(ownerParked))
                {
                    // Both a parked and an unparked copy for the same profile. The unparked one is
                    // the live install, so keep it and set the stale copy aside rather than deleting
                    // anything — CleanupVersionsFolder skips dot-prefixed names, so it survives for
                    // the user to inspect.
                    string aside = Path.Combine(Paths.Versions, $".stale-{ownerId}-{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
                    Directory.Move(ownerParked, aside);
                    App.Logger.WriteLine(LOG_IDENT, $"Set aside a duplicate parked install as {Path.GetFileName(aside)}");
                }

                Directory.Move(active, ownerParked);
                App.Logger.WriteLine(LOG_IDENT, $"Parked {Path.GetFileName(active)} → {Path.GetFileName(ownerParked)}");
                return true;
            }
            catch (IOException ex)
            {
                // Locked, which in practice means a client is still running from it.
                App.Logger.WriteLine(LOG_IDENT, $"Couldn't park {Path.GetFileName(active)} — it's in use. Close Roblox and relaunch to switch profiles.");
                App.Logger.WriteException(LOG_IDENT + "::ParkCurrentOccupant", ex);
                return false;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::ParkCurrentOccupant", ex);
                return false;
            }
        }

        /// <summary>
        /// v420.24-era junctions are unlinked on sight. Directory.Delete on a reparse point removes
        /// only the link, so the profile directory it pointed at survives — already in exactly the
        /// parked shape this layout expects, which is what makes the migration free.
        /// </summary>
        private static void RemoveLegacyJunction(string path)
        {
            if (!VersionJunctionManager.IsJunction(path))
                return;

            if (VersionJunctionManager.DeleteJunction(path))
                App.Logger.WriteLine(LOG_IDENT, $"Removed legacy junction {Path.GetFileName(path)} — the client no longer launches through a reparse point.");
        }

        private static void RecordOwner(string profileId, string versionGuid)
        {
            if (App.State.Prop.ActiveInstallProfileId == profileId
                && App.State.Prop.ActiveInstallVersionGuid == versionGuid)
            {
                return;
            }

            App.State.Prop.ActiveInstallProfileId = profileId;
            App.State.Prop.ActiveInstallVersionGuid = versionGuid;
            App.State.Save();
        }

        /// <summary>
        /// True when this profile's install is the unparked one. Replaces the old junction-target
        /// check behind the Versions Manager's "install target" badge: with no junctions, being
        /// unparked is exactly what makes a profile the target an executor installer writes into.
        /// </summary>
        public static bool IsInstallTarget(string profileId) =>
            !string.IsNullOrEmpty(profileId) && App.State.Prop.ActiveInstallProfileId == profileId;
    }
}

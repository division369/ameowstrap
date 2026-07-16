using ExploitStrap.AppData;
using ExploitStrap.Integrations;
using ExploitStrap.Models;

namespace ExploitStrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;

        // If Roblox exits sooner than this after launch AND never reached gameplay, Run() treats it as
        // a probable crash. Covers the observed 7-40s crash-on-launch window with margin while staying
        // below where a normal player is reliably in-game. Single knob for tuning.
        private const int ProbableCrashWindowSeconds = 60;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);
            }

            _notifyIcon = new(this);
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Watcher::Run";

            if (!_lock.IsAcquired || _watcherData is null)
                return;

            // Multi-instance: the watcher is the longest-lived ExploitStrap process in a play
            // session, so it owns Roblox's single-instance lock while the client runs. While
            // we own it, no client can become the primary instance and close the others.
            // See Utility.MultiInstance for the full picture. The flag covers account launches
            // (Multi Instance tab) that force multi-instance without the global toggle.
            if (App.Settings.Prop.MultiInstanceEnabled || App.LaunchSettings.MultiInstanceFlag.Active)
                Utility.MultiInstance.HoldSingletonMutex();

            ActivityWatcher?.Start();

            // v420.28: when Stream Mode is on, keep Roblox's window title
            // rewritten to a generic "Roblox" so streamers don't leak game /
            // account info to viewers. Runs for the lifetime of the watcher.
            using var streamModeCts = new CancellationTokenSource();
            Task? streamModeTask = null;
            if (Utility.StreamMode.IsActive)
            {
                streamModeTask = Utility.StreamMode.RewriteWindowTitleLoopAsync(
                    _watcherData.ProcessId, streamModeCts.Token);
            }

            // v420.26: rolled back v420.23's MainWindowHandle-based force-kill. It
            // was killing live Roblox sessions when the main window briefly went to
            // IntPtr.Zero mid-game (fullscreen toggles, loading screens, in-game
            // transitions). flippi's 2026-05-24 report showed Roblox dying after
            // about a minute of normal gameplay. We're back to passive polling for
            // PID exit — same behaviour as pre-v420.23. The always-spawn change
            // from v420.23 stays (so AutoclosePids cleanup runs for everyone even
            // without EnableActivityTracking).
            bool closeCrashHandler = App.Settings.Prop.CloseRobloxCrashHandler;

            // Record when Roblox started so that when the PID poll below exits we can tell a
            // crash-on-launch from a normal session. The Roblox log file's creation time is the
            // closest "client actually started" signal we have; fall back to now if it can't be read.
            DateTime sessionStartUtc;
            try { sessionStartUtc = new FileInfo(_watcherData.LogFile!).CreationTimeUtc; }
            catch { sessionStartUtc = DateTime.UtcNow; }

            while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
            {
                // Froststrap-style memory saver: keep RobloxCrashHandler closed while Roblox runs.
                if (closeCrashHandler)
                    CloseRobloxCrashHandlers();

                await Task.Delay(1000);
            }

            streamModeCts.Cancel();
            if (streamModeTask is not null)
            {
                try { await streamModeTask; } catch { /* expected on cancel */ }
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");

            // The Roblox PID just disappeared. If it died quickly and never reached gameplay, treat it
            // as a probable crash and surface the (previously never-fired) crash dialog, whose one-click
            // export produces a bundle that includes the Roblox client logs. "Never reached gameplay"
            // (ActivityWatcher never saw 'Replicator created:') is the discriminator: a normal quit
            // almost always happens after joining, so it's excluded. If activity tracking is off we
            // can't tell, so we stay quiet. No process handle is held — this rides the PID poll above.
            TimeSpan sessionLength = DateTime.UtcNow - sessionStartUtc;
            bool everInGame = (ActivityWatcher?.InGame ?? false) || (ActivityWatcher?.History.Count > 0);
            bool probableCrash = ActivityWatcher is not null
                && !everInGame
                && sessionLength < TimeSpan.FromSeconds(ProbableCrashWindowSeconds)
                // Multi Instance spawns a throwaway Roblox starter that exits in ~1s while the real
                // client runs under a different PID. If any RobloxPlayerBeta is still alive, this
                // wasn't a crash — don't fire the dialog on every multi-instance launch.
                && !Utilities.GetProcessesSafe().Any(p => p.ProcessName == "RobloxPlayerBeta");

            if (probableCrash)
            {
                App.Logger.WriteLine(LOG_IDENT,
                    $"Roblox exited after {sessionLength.TotalSeconds:F0}s without reaching in-game — probable crash");
                try
                {
                    // Blocks (self-marshals to the UI dispatcher) until the user dismisses it, which
                    // also holds off the watcher's teardown/terminate until they're done exporting.
                    UI.Frontend.ShowPlayerErrorDialog(crash: true);
                }
                catch (Exception ex)
                {
                    // Never let a dialog failure stop normal watcher teardown.
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }

        // Froststrap-style memory saver: close RobloxCrashHandler.exe while Roblox runs. It's the
        // out-of-process crash reporter and isn't needed for the game to run, so closing it frees
        // the memory it holds. Best-effort and silent on failure (it may be mid-exit or briefly
        // protected). Called once per second from the watch loop so a respawn gets caught too.
        private static void CloseRobloxCrashHandlers()
        {
            const string LOG_IDENT = "Watcher::CloseRobloxCrashHandlers";

            foreach (var process in Process.GetProcessesByName("RobloxCrashHandler"))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        App.Logger.WriteLine(LOG_IDENT, $"Closed RobloxCrashHandler (pid={process.Id}) to free memory");
                    }
                }
                catch { /* best-effort: handler may be exiting or briefly protected */ }
                finally { process.Dispose(); }
            }
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}

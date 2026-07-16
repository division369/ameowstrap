using System.Windows;

using ExploitStrap.UI.Elements.Bootstrapper;
using ExploitStrap.UI.Elements.Dialogs;

namespace ExploitStrap.UI
{
    static class Frontend
    {
        public static MessageBoxResult ShowMessageBox(string message, MessageBoxImage icon = MessageBoxImage.None, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            App.Logger.WriteLine("Frontend::ShowMessageBox", message);

            if (App.LaunchSettings.QuietFlag.Active)
                return defaultResult;

            return ShowFluentMessageBox(message, icon, buttons);
        }

        // crash=false: Roblox never produced a log / failed to start (shown from the bootstrapper).
        // crash=true:  Roblox started fine and then died on its own (shown from the watcher).
        // Either way the fault is on Roblox's side, not the launcher — say so plainly, and if the
        // user launched through an executor profile, name it as the likely culprit.
        public static void ShowPlayerErrorDialog(bool crash = false)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            var info = new StringBuilder();

            if (crash)
            {
                info.Append("Roblox closed unexpectedly — it looks like the game crashed.");
                info.Append("\n\nExploitStrap only downloads and launches Roblox, so this is usually a "
                    + "Roblox-side issue — graphics drivers, an unstable build, or the machine itself. If it "
                    + "keeps happening, send us your logs so we can rule out anything on our end.");
            }
            else
            {
                info.Append(String.Format(Strings.Dialog_PlayerError_FailedLaunch, App.ProjectSupportLink));
                info.Append("\n\nThis happens on **Roblox's** side — ExploitStrap downloads and launches the "
                    + "game, but it's the Roblox client itself that failed to run.");
            }

            // Read the Roblox client logs and, when we recognise the cause (a blocked overlay,
            // firewall/VPN, graphics driver, out of memory), name it plainly so the user knows
            // it wasn't ExploitStrap and what to actually fix.
            string? diagnosis = ExploitStrap.Utility.CrashAnalyzer.Analyze();
            if (!string.IsNullOrEmpty(diagnosis))
                info.Append("\n\n" + diagnosis);

            // If they're on an executor profile, that's by far the most likely cause: injected /
            // external tools crash the Roblox client constantly. Point them at the clean profile so
            // they can tell the difference between an executor problem and a real Roblox problem.
            string? executor = App.GetActiveExecutorTitle();
            if (!string.IsNullOrEmpty(executor))
            {
                info.Append($"\n\nYou're launching with the **{executor}** executor. Crashes like this are very "
                    + "often caused by the executor or other external/injection tools, not by Roblox or "
                    + "ExploitStrap. Try switching to the **Latest LIVE** profile in the Versions Manager and "
                    + "launching clean — if it stops crashing, the executor was the cause.");
            }

            // Offer the same one-click log export the crash dialog has. FluentMessageBox renders
            // markdown, so the email / Discord links below are clickable.
            info.Append("\n\nWant us to take a look? Export your logs and send them over — "
                + $"email [{App.ProjectSupportEmail}](mailto:{App.ProjectSupportEmail}) "
                + $"or join our [Discord]({App.ProjectDiscordLink}).\n\nExport your logs now?");

            var result = ShowMessageBox(info.ToString(), MessageBoxImage.Error, MessageBoxButton.YesNo, MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
            {
                // We're on a background thread here, so blocking until the export finishes is safe
                // (and the export shows its own dispatcher-marshalled result dialog). The process
                // may exit right after this returns, so we wait.
                SupportActions.ExportLogsAsync().GetAwaiter().GetResult();
            }
        }

        public static void ShowExceptionDialog(Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                new ExceptionDialog(exception).ShowDialog();
            });
        }

        public static void ShowConnectivityDialog(string title, string description, MessageBoxImage image, Exception exception)
        {
            if (App.LaunchSettings.QuietFlag.Active)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                new ConnectivityDialog(title, description, image, exception).ShowDialog();
            });
        }

        private static IBootstrapperDialog GetCustomBootstrapper()
        {
            const string LOG_IDENT = "Frontend::GetCustomBootstrapper";

            Directory.CreateDirectory(Paths.CustomThemes);

            try
            {
                if (App.Settings.Prop.SelectedCustomTheme == null)
                    throw new CustomThemeException("CustomTheme.Errors.NoThemeSelected");

                CustomDialog dialog = new CustomDialog();
                dialog.ApplyCustomTheme(App.Settings.Prop.SelectedCustomTheme);
                return dialog;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);

                if (!App.LaunchSettings.QuietFlag.Active)
                    ShowMessageBox(string.Format(Strings.CustomTheme_Errors_SetupFailed, ex.Message, "ExploitStrap"), MessageBoxImage.Error); // NOTE: ExploitStrap is the theme name

                return GetBootstrapperDialog(BootstrapperStyle.FluentDialog);
            }
        }

        public static IBootstrapperDialog GetBootstrapperDialog(BootstrapperStyle style)
        {
            return style switch
            {
                BootstrapperStyle.VistaDialog => new VistaDialog(),
                BootstrapperStyle.LegacyDialog2008 => new LegacyDialog2008(),
                BootstrapperStyle.LegacyDialog2011 => new LegacyDialog2011(),
                BootstrapperStyle.ProgressDialog => new ProgressDialog(),
                BootstrapperStyle.ClassicFluentDialog => new ClassicFluentDialog(),
                BootstrapperStyle.ByfronDialog => new ByfronDialog(),
                BootstrapperStyle.FluentDialog => new FluentDialog(false),
                BootstrapperStyle.FluentAeroDialog => new FluentDialog(true),
                BootstrapperStyle.CustomDialog => GetCustomBootstrapper(),
                _ => new FluentDialog(false)
            };
        }

        private static MessageBoxResult ShowFluentMessageBox(string message, MessageBoxImage icon, MessageBoxButton buttons)
        {
            return Application.Current.Dispatcher.Invoke(new Func<MessageBoxResult>(() =>
            {
                var messagebox = new FluentMessageBox(message, icon, buttons);
                messagebox.ShowDialog();
                return messagebox.Result;
            }));
        }

        public static void ShowBalloonTip(string title, string message, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.None, int timeout = 5)
        {
            var notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = BootstrapperIconEx.GetBrandIcon(),
                Text = App.ProjectName,
                Visible = true
            };

            notifyIcon.ShowBalloonTip(timeout, title, message, icon);
        }
    }
}

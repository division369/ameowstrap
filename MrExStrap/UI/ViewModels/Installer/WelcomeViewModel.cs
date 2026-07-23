namespace ExploitStrap.UI.ViewModels.Installer
{
    public class WelcomeViewModel : NotifyPropertyChangedViewModel
    {
        // formatting is done here instead of in xaml, it's just a bit easier
        public string MainText => String.Format(
            Strings.Installer_Welcome_MainText,
            $"[{App.ProjectName}]({App.ProjectHost}/{App.ProjectRepository})"
        );

        public string VersionNotice { get; private set; } = "";

        public bool CanContinue { get; set; } = false;

        public event EventHandler? CanContinueEvent;

        // called by codebehind on page load
        public async void DoChecks()
        {
            var releaseInfo = await App.GetLatestRelease();

            if (releaseInfo is not null)
            {
                try
                {
                    if (Utilities.CompareVersions(App.Version, releaseInfo.TagName) == VersionComparison.LessThan)
                    {
                        VersionNotice = String.Format(Strings.Installer_Welcome_UpdateNotice, App.Version, releaseInfo.TagName.Replace("v", ""));
                        OnPropertyChanged(nameof(VersionNotice));
                    }
                }
                catch (Exception ex)
                {
                    // GitHub can return placeholder tags like "untagged-<sha>" when a release
                    // isn't attached to a real git tag. Don't let that crash the installer —
                    // just skip the "update available" notice and let setup proceed.
                    App.Logger.WriteException("WelcomeViewModel::DoChecks", ex);
                }
            }

            CanContinue = true;
            OnPropertyChanged(nameof(CanContinue));

            CanContinueEvent?.Invoke(this, new EventArgs());
        }
    }
}

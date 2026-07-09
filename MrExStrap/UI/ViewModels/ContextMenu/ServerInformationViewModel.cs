using System.Windows;
using System.Windows.Input;
using ExploitStrap.Integrations;
using ExploitStrap.Utility;
using CommunityToolkit.Mvvm.Input;

namespace ExploitStrap.UI.ViewModels.ContextMenu
{
    internal class ServerInformationViewModel : NotifyPropertyChangedViewModel
    {
        private readonly ActivityWatcher _activityWatcher;

        public string InstanceId => _activityWatcher.Data.JobId;

        public string ServerType => _activityWatcher.Data.ServerType.ToTranslatedString();

        public string ServerLocation { get; private set; } = Strings.Common_Loading;

        public string ServerRegion { get; private set; } = Strings.Common_Loading;

        public Visibility ServerLocationVisibility => App.Settings.Prop.ShowServerDetails ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CopyInstanceIdCommand => new RelayCommand(CopyInstanceId);

        public ServerInformationViewModel(Watcher watcher)
        {
            _activityWatcher = watcher.ActivityWatcher!;

            if (ServerLocationVisibility == Visibility.Visible)
            {
                QueryServerLocation();
                QueryServerRegion();
            }
        }

        public async void QueryServerLocation()
        {
            string? location = await _activityWatcher.Data.QueryServerLocation();

            if (String.IsNullOrEmpty(location))
                ServerLocation = Strings.Common_NotAvailable;
            else
                ServerLocation = location;

            OnPropertyChanged(nameof(ServerLocation));
        }

        public async void QueryServerRegion()
        {
            string? region = _activityWatcher.Data.MachineAddressValid
                ? await RobloxDatacenters.ResolveRegionAsync(_activityWatcher.Data.MachineAddress)
                : null;

            ServerRegion = String.IsNullOrEmpty(region) ? Strings.Common_NotAvailable : region;
            OnPropertyChanged(nameof(ServerRegion));
        }

        private void CopyInstanceId() => Clipboard.SetDataObject(InstanceId);
    }
}

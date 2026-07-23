using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class GlobalPage
    {
        public GlobalPage()
        {
            DataContext = new GlobalSettingsViewModel();
            InitializeComponent();
        }
    }
}

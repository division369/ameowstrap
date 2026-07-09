using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class LinkBypasserPage
    {
        public LinkBypasserPage()
        {
            DataContext = new LinkBypasserViewModel();
            InitializeComponent();
        }
    }
}

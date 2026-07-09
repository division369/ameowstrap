using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class ObfuscatorPage
    {
        public ObfuscatorPage()
        {
            DataContext = new ObfuscatorViewModel();
            InitializeComponent();
        }
    }
}

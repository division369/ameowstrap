using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class DeobfuscatorPage
    {
        public DeobfuscatorPage()
        {
            DataContext = new DeobfuscatorViewModel();
            InitializeComponent();
        }
    }
}

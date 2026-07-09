using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class NewsPage
    {
        public NewsPage()
        {
            DataContext = new NewsViewModel();
            InitializeComponent();
        }
    }
}

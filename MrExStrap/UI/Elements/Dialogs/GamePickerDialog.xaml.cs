using ExploitStrap.UI.ViewModels.Dialogs;

namespace ExploitStrap.UI.Elements.Dialogs
{
    public partial class GamePickerDialog
    {
        // null means "launch normally" — the user pressed Just launch Roblox, or closed the window.
        public long? PickedPlaceId { get; private set; }

        public GamePickerDialog()
        {
            var vm = new GamePickerViewModel();
            vm.CloseRequested += (_, placeId) =>
            {
                PickedPlaceId = placeId;
                Close();
            };
            DataContext = vm;
            InitializeComponent();
        }
    }
}

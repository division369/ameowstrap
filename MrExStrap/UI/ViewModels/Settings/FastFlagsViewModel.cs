using ExploitStrap.Enums.FlagPresets;

namespace ExploitStrap.UI.ViewModels.Settings
{
    public class FastFlagsViewModel : NotifyPropertyChangedViewModel
    {
        private Dictionary<string, object>? _preResetFlags;

        // Raised when a reset swap happens so the page can rebuild this VM (so the preset
        // controls re-read App.FastFlags) and refresh the raw editor grid.
        public event EventHandler? RequestPageReloadEvent;

        public bool UseFastFlagManager
        {
            get => App.Settings.Prop.UseFastFlagManager;
            set => App.Settings.Prop.UseFastFlagManager = value;
        }

        public IReadOnlyDictionary<MSAAMode, string?> MSAALevels => FastFlagManager.MSAAModes;

        public MSAAMode SelectedMSAALevel
        {
            get => MSAALevels.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.MSAA")).Key;
            set => App.FastFlags.SetPreset("Rendering.MSAA", MSAALevels[value]);
        }

        public IReadOnlyDictionary<RenderingMode, string> RenderingModes => FastFlagManager.RenderingModes;

        public RenderingMode SelectedRenderingMode
        {
            get => App.FastFlags.GetPresetEnum(RenderingModes, "Rendering.Mode", "True");
            set => App.FastFlags.SetPresetEnum("Rendering.Mode", value.ToString(), "True");
        }

        public bool GraySky
        {
            get => App.FastFlags.GetPreset("Rendering.GraySky") == "True";
            set => App.FastFlags.SetPreset("Rendering.GraySky", value ? "True" : null);
        }

        public bool DisableGrass
        {
            get => App.FastFlags.GetPreset("Rendering.Grass.Max") == "0";
            set
            {
                App.FastFlags.SetPreset("Rendering.Grass.Max", value ? "0" : null);
                App.FastFlags.SetPreset("Rendering.Grass.Min", value ? "0" : null);
            }
        }

        public bool FixDisplayScaling
        {
            get => App.FastFlags.GetPreset("Rendering.DisableScaling") == "True";
            set => App.FastFlags.SetPreset("Rendering.DisableScaling", value ? "True" : null);
        }

        public IReadOnlyDictionary<TextureQuality, string?> TextureQualities => FastFlagManager.TextureQualityLevels;

        public TextureQuality SelectedTextureQuality
        {
            get => TextureQualities.Where(x => x.Value == App.FastFlags.GetPreset("Rendering.TextureQuality.Level")).FirstOrDefault().Key;
            set
            {
                if (value == TextureQuality.Default)
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality", null);
                }
                else
                {
                    App.FastFlags.SetPreset("Rendering.TextureQuality.OverrideEnabled", "True");
                    App.FastFlags.SetPreset("Rendering.TextureQuality.Level", TextureQualities[value]);
                }
            }
        }

        public IReadOnlyDictionary<FramerateLimit, string?> FramerateLimits => FastFlagManager.FramerateLimits;

        public FramerateLimit SelectedFramerateLimit
        {
            get => FramerateLimits.FirstOrDefault(x => x.Value == App.FastFlags.GetPreset("Rendering.Framerate")).Key;
            set => App.FastFlags.SetPreset("Rendering.Framerate", FramerateLimits[value]);
        }

        public bool ResetConfiguration
        {
            get => _preResetFlags is not null;

            set
            {
                if (value)
                {
                    _preResetFlags = new(App.FastFlags.Prop);
                    App.FastFlags.Prop.Clear();
                }
                else
                {
                    App.FastFlags.Prop = _preResetFlags!;
                    _preResetFlags = null;
                }

                RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

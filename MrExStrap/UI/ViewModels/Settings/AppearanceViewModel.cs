using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;

using Microsoft.Win32;

using ExploitStrap.UI.Elements.Settings;
using ExploitStrap.UI.Elements.Editor;
using ExploitStrap.UI.Elements.Dialogs;

namespace ExploitStrap.UI.ViewModels.Settings
{
    public class AppearanceViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Page _page;

        public ICommand PreviewBootstrapperCommand => new RelayCommand(PreviewBootstrapper);
        public ICommand BrowseCustomIconLocationCommand => new RelayCommand(BrowseCustomIconLocation);

        public ICommand AddCustomThemeCommand => new RelayCommand(AddCustomTheme);
        public ICommand DeleteCustomThemeCommand => new RelayCommand(DeleteCustomTheme);
        public ICommand RenameCustomThemeCommand => new RelayCommand(RenameCustomTheme);
        public ICommand EditCustomThemeCommand => new RelayCommand(EditCustomTheme);
        public ICommand ExportCustomThemeCommand => new RelayCommand(ExportCustomTheme);

        private void PreviewBootstrapper()
        {
            IBootstrapperDialog dialog = App.Settings.Prop.BootstrapperStyle.GetNew();

            if (App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.ByfronDialog)
                dialog.Message = Strings.Bootstrapper_StylePreview_ImageCancel;
            else
                dialog.Message = Strings.Bootstrapper_StylePreview_TextCancel;

            dialog.CancelEnabled = true;
            dialog.ShowBootstrapper();
        }

        private void BrowseCustomIconLocation()
        {
            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_IconFiles}|*.ico"
            };

            if (dialog.ShowDialog() != true)
                return;

            CustomIconLocation = dialog.FileName;
            OnPropertyChanged(nameof(CustomIconLocation));
        }

        public AppearanceViewModel(Page page)
        {
            _page = page;

            foreach (var entry in BootstrapperIconEx.Selections)
                Icons.Add(new BootstrapperIconEntry { IconType = entry });

            PopulateCustomThemes();
        }

        public IEnumerable<Theme> Themes { get; } = Enum.GetValues(typeof(Theme)).Cast<Theme>();

        public Theme Theme
        {
            get => App.Settings.Prop.Theme;
            set
            {
                App.Settings.Prop.Theme = value;
                if (Window.GetWindow(_page) is MainWindow mw)
                    mw.ApplyTheme();
            }
        }

        public static List<string> Languages => Locale.GetLanguages();

        public string SelectedLanguage
        {
            get => Locale.SupportedLocales[App.Settings.Prop.Locale];
            set => App.Settings.Prop.Locale = Locale.GetIdentifierFromName(value);
        }

        // ===== App theming (ExploitStrap fork feature) — live-editable brand palette =====
        private ExploitStrap.Models.ThemePalette Pal => App.Settings.Prop.Palette;
        private void ApplyTheme() => ExploitStrap.Utility.ThemeManager.Apply(App.Settings.Prop.Palette);

        public ICommand ResetThemeCommand => new RelayCommand(ResetTheme);
        public ICommand BrowseAppIconCommand => new RelayCommand(BrowseAppIcon);
        public ICommand ClearAppIconCommand => new RelayCommand(() => AppIconLocation = "");

        public IEnumerable<string> ThemePresets => ExploitStrap.Utility.ThemeManager.Presets.Keys.Append("Custom");

        public string SelectedThemePreset
        {
            get => App.Settings.Prop.SelectedThemePreset;
            set
            {
                App.Settings.Prop.SelectedThemePreset = value;
                OnPropertyChanged(nameof(SelectedThemePreset));

                if (ExploitStrap.Utility.ThemeManager.Presets.TryGetValue(value, out var preset))
                {
                    App.Settings.Prop.Palette = preset.Clone();
                    ApplyTheme();
                    NotifyColours();
                }
            }
        }

        // Accent drives the gradient start + glow too, so the palette stays cohesive from one colour.
        public string AccentHex
        {
            get => Pal.Accent;
            set { Pal.Accent = value; Pal.GradientStart = value; Pal.Glow = value; MarkCustom(); ApplyTheme(); }
        }
        public string GradientEndHex
        {
            get => Pal.GradientEnd;
            set { Pal.GradientEnd = value; MarkCustom(); ApplyTheme(); }
        }
        public string PurpleHex
        {
            get => Pal.Purple;
            set { Pal.Purple = value; MarkCustom(); ApplyTheme(); }
        }
        public string BackgroundHex
        {
            get => Pal.Background;
            set { Pal.Background = value; MarkCustom(); ApplyTheme(); }
        }
        public string SurfaceHex
        {
            get => Pal.Surface;
            set { Pal.Surface = value; MarkCustom(); ApplyTheme(); }
        }

        public bool EnableAurora
        {
            get => App.Settings.Prop.EnableAurora;
            set { App.Settings.Prop.EnableAurora = value; ApplyTheme(); }
        }
        public bool EnableGlass
        {
            get => App.Settings.Prop.EnableGlass;
            set { App.Settings.Prop.EnableGlass = value; ApplyTheme(); }
        }
        public bool EnableGlow
        {
            get => App.Settings.Prop.EnableGlow;
            set { App.Settings.Prop.EnableGlow = value; ApplyTheme(); }
        }

        private void MarkCustom()
        {
            App.Settings.Prop.SelectedThemePreset = "Custom";
            OnPropertyChanged(nameof(SelectedThemePreset));
        }

        private void NotifyColours()
        {
            OnPropertyChanged(nameof(AccentHex));
            OnPropertyChanged(nameof(GradientEndHex));
            OnPropertyChanged(nameof(PurpleHex));
            OnPropertyChanged(nameof(BackgroundHex));
            OnPropertyChanged(nameof(SurfaceHex));
        }

        private void ResetTheme()
        {
            App.Settings.Prop.Palette = new ExploitStrap.Models.ThemePalette();
            App.Settings.Prop.SelectedThemePreset = "Default";
            App.Settings.Prop.EnableAurora = true;
            App.Settings.Prop.EnableGlass = true;
            App.Settings.Prop.EnableGlow = true;

            ApplyTheme();
            NotifyColours();
            OnPropertyChanged(nameof(SelectedThemePreset));
            OnPropertyChanged(nameof(EnableAurora));
            OnPropertyChanged(nameof(EnableGlass));
            OnPropertyChanged(nameof(EnableGlow));
        }

        // ===== Custom app icon =====
        public string AppIconLocation
        {
            get => App.Settings.Prop.CustomAppIconLocation;
            set { App.Settings.Prop.CustomAppIconLocation = value; OnPropertyChanged(nameof(AppIconLocation)); ApplyAppIcon(); }
        }

        private void BrowseAppIcon()
        {
            var dialog = new OpenFileDialog { Filter = "Icon and image files|*.ico;*.png;*.jpg;*.jpeg" };

            if (dialog.ShowDialog() != true)
                return;

            AppIconLocation = dialog.FileName;
        }

        private void ApplyAppIcon()
        {
            try
            {
                string path = App.Settings.Prop.CustomAppIconLocation;

                Uri uri = !string.IsNullOrEmpty(path) && File.Exists(path)
                    ? new Uri(path)
                    : new Uri("pack://application:,,,/ExploitStrap.ico");

                var src = new System.Windows.Media.Imaging.BitmapImage();
                src.BeginInit();
                src.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                src.UriSource = uri;
                src.EndInit();
                src.Freeze();

                foreach (Window window in Application.Current.Windows)
                    window.Icon = src;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::ApplyAppIcon", ex);
            }
        }

        public IEnumerable<BootstrapperStyle> Dialogs { get; } = BootstrapperStyleEx.Selections;

        public BootstrapperStyle Dialog
        {
            get => App.Settings.Prop.BootstrapperStyle;
            set
            {
                App.Settings.Prop.BootstrapperStyle = value;
                OnPropertyChanged(nameof(CustomThemesExpanded)); // TODO: only fire when needed
            }
        }

        public bool CustomThemesExpanded => App.Settings.Prop.BootstrapperStyle == BootstrapperStyle.CustomDialog;

        public ObservableCollection<BootstrapperIconEntry> Icons { get; set; } = new();

        public BootstrapperIcon Icon
        {
            get => App.Settings.Prop.BootstrapperIcon;
            set => App.Settings.Prop.BootstrapperIcon = value; 
        }

        public string Title
        {
            get => App.Settings.Prop.BootstrapperTitle;
            set => App.Settings.Prop.BootstrapperTitle = value;
        }

        public string CustomIconLocation
        {
            get => App.Settings.Prop.BootstrapperIconCustomLocation;
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    if (App.Settings.Prop.BootstrapperIcon == BootstrapperIcon.IconCustom)
                        App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconBloxstrap;
                }
                else
                {
                    App.Settings.Prop.BootstrapperIcon = BootstrapperIcon.IconCustom;
                }

                App.Settings.Prop.BootstrapperIconCustomLocation = value;

                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(Icons));
            }
        }

        private void DeleteCustomThemeStructure(string name)
        {
            string dir = Path.Combine(Paths.CustomThemes, name);
            Directory.Delete(dir, true);
        }

        private void RenameCustomThemeStructure(string oldName, string newName)
        {
            string oldDir = Path.Combine(Paths.CustomThemes, oldName);
            string newDir = Path.Combine(Paths.CustomThemes, newName);
            Directory.Move(oldDir, newDir);
        }

        private void AddCustomTheme()
        {
            var dialog = new AddCustomThemeDialog();
            dialog.ShowDialog();

            if (dialog.Created)
            {
                CustomThemes.Add(dialog.ThemeName);
                SelectedCustomThemeIndex = CustomThemes.Count - 1;

                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                OnPropertyChanged(nameof(IsCustomThemeSelected));

                if (dialog.OpenEditor)
                    EditCustomTheme();
            }
        }

        private void DeleteCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            try
            {
                DeleteCustomThemeStructure(SelectedCustomTheme);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::DeleteCustomTheme", ex);
                Frontend.ShowMessageBox(string.Format(Strings.Menu_Appearance_CustomThemes_DeleteFailed, SelectedCustomTheme, ex.Message), MessageBoxImage.Error);
                return;
            }

            CustomThemes.Remove(SelectedCustomTheme);

            if (CustomThemes.Any())
            {
                SelectedCustomThemeIndex = CustomThemes.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomThemeIndex));
            }

            OnPropertyChanged(nameof(IsCustomThemeSelected));
        }

        private void RenameCustomTheme()
        {
            const string LOG_IDENT = "AppearanceViewModel::RenameCustomTheme";

            if (SelectedCustomTheme is null || SelectedCustomTheme == SelectedCustomThemeName)
                return;

            if (string.IsNullOrEmpty(SelectedCustomThemeName))
            {
                Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameEmpty, MessageBoxImage.Error);
                return;
            }

            var validationResult = PathValidator.IsFileNameValid(SelectedCustomThemeName);

            if (validationResult != PathValidator.ValidationResult.Ok)
            {
                switch (validationResult)
                {
                    case PathValidator.ValidationResult.IllegalCharacter:
                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameIllegalCharacters, MessageBoxImage.Error);
                        break;
                    case PathValidator.ValidationResult.ReservedFileName:
                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameReserved, MessageBoxImage.Error);
                        break;
                    default:
                        App.Logger.WriteLine(LOG_IDENT, $"Got unhandled PathValidator::ValidationResult {validationResult}");
                        Debug.Assert(false);

                        Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_Unknown, MessageBoxImage.Error);
                        break;
                }

                return;
            }

            // better to check for the file instead of the directory so broken themes can be overwritten
            string path = Path.Combine(Paths.CustomThemes, SelectedCustomThemeName, "Theme.xml");
            if (File.Exists(path))
            {
                Frontend.ShowMessageBox(Strings.CustomTheme_Add_Errors_NameTaken, MessageBoxImage.Error);
                return;
            }

            try
            {
                RenameCustomThemeStructure(SelectedCustomTheme, SelectedCustomThemeName);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox(string.Format(Strings.Menu_Appearance_CustomThemes_RenameFailed, SelectedCustomTheme, ex.Message), MessageBoxImage.Error);
                return;
            }

            int idx = CustomThemes.IndexOf(SelectedCustomTheme);
            CustomThemes[idx] = SelectedCustomThemeName;

            SelectedCustomThemeIndex = idx;
            OnPropertyChanged(nameof(SelectedCustomThemeIndex));
        }

        private void EditCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            new BootstrapperEditorWindow(SelectedCustomTheme).ShowDialog();
        }

        private void ExportCustomTheme()
        {
            if (SelectedCustomTheme is null)
                return;

            var dialog = new SaveFileDialog
            {
                FileName = $"{SelectedCustomTheme}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string themeDir = Path.Combine(Paths.CustomThemes, SelectedCustomTheme);

                using (var memStream = new MemoryStream())
                {
                    using (var zipStream = new ZipOutputStream(memStream))
                    {
                        foreach (var filePath in Directory.EnumerateFiles(themeDir, "*.*", SearchOption.AllDirectories))
                        {
                            string relativePath = filePath[(themeDir.Length + 1)..];

                            var entry = new ZipEntry(relativePath);
                            entry.DateTime = DateTime.Now;

                            zipStream.PutNextEntry(entry);

                            using var fileStream = File.OpenRead(filePath);
                            fileStream.CopyTo(zipStream);
                        }

                        zipStream.CloseEntry();
                        zipStream.Finish();
                    }

                    // Buffer the whole archive in memory first, then write in one shot — a
                    // failure part-way through never leaves a truncated .zip at the target.
                    File.WriteAllBytes(dialog.FileName, memStream.ToArray());
                }

                Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AppearanceViewModel::ExportCustomTheme", ex);
                Frontend.ShowMessageBox($"Couldn't export the theme '{SelectedCustomTheme}': {ex.Message}", MessageBoxImage.Error);
            }
        }

        private void PopulateCustomThemes()
        {
            string? selected = App.Settings.Prop.SelectedCustomTheme;

            Directory.CreateDirectory(Paths.CustomThemes);

            foreach (string directory in Directory.GetDirectories(Paths.CustomThemes))
            {
                if (!File.Exists(Path.Combine(directory, "Theme.xml")))
                    continue; // missing the main theme file, ignore

                string name = Path.GetFileName(directory);
                CustomThemes.Add(name);
            }

            if (selected != null)
            {
                int idx = CustomThemes.IndexOf(selected);

                if (idx != -1)
                {
                    SelectedCustomThemeIndex = idx;
                    OnPropertyChanged(nameof(SelectedCustomThemeIndex));
                }
                else
                {
                    SelectedCustomTheme = null;
                }
            }
        }

        public string? SelectedCustomTheme
        {
            get => App.Settings.Prop.SelectedCustomTheme;
            set
            {
                App.Settings.Prop.SelectedCustomTheme = value;

                // The list binds straight to this, so it's the only place that knows the
                // selection moved. Seed the rename box from the current name and let the
                // action buttons re-evaluate IsEnabled — without this they stay greyed out
                // no matter what you click.
                SelectedCustomThemeName = value ?? "";

                OnPropertyChanged(nameof(SelectedCustomTheme));
                OnPropertyChanged(nameof(SelectedCustomThemeName));
                OnPropertyChanged(nameof(IsCustomThemeSelected));
            }
        }

        public string SelectedCustomThemeName { get; set; } = "";

        public int SelectedCustomThemeIndex { get; set; }

        public ObservableCollection<string> CustomThemes { get; set; } = new();
        public bool IsCustomThemeSelected => SelectedCustomTheme is not null;
    }
}

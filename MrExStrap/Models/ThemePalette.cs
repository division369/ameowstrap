namespace ExploitStrap.Models
{
    // A user-editable colour palette for the app. Stored as hex strings (System.Text.Json friendly,
    // no custom Color converter needed). ThemeManager turns these into the live brand brushes.
    public class ThemePalette
    {
        public string Accent { get; set; } = "#22D3EE";
        public string GradientStart { get; set; } = "#22D3EE";
        public string GradientEnd { get; set; } = "#4ADE80";
        public string Purple { get; set; } = "#A855F7";
        public string Background { get; set; } = "#0B0F14";
        public string Surface { get; set; } = "#121821";
        public string Hairline { get; set; } = "#1F2A37";
        public string Glow { get; set; } = "#22D3EE";

        public ThemePalette Clone() => (ThemePalette)MemberwiseClone();
    }
}

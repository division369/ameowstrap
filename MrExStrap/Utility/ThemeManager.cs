using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

using Wpf.Ui.Appearance;

using ExploitStrap.Models;

namespace ExploitStrap.Utility
{
    // Turns a ThemePalette into the app's live brand resources. Overwrites the keyed brushes /
    // gradients / effects in Application.Current.Resources (which sit above the merged dictionaries),
    // so anything binding them via DynamicResource updates instantly, and StaticResource consumers
    // pick up the palette on the next window that loads. Also drives the WPF-UI accent (which is
    // DynamicResource-backed everywhere) and honours the aurora / glass / glow on-off toggles.
    public static class ThemeManager
    {
        // Built-in presets. "Default" is the original cyan->green neon.
        public static readonly Dictionary<string, ThemePalette> Presets = new()
        {
            ["Default"] = new ThemePalette(),
            ["Purple Haze"] = new ThemePalette { Accent = "#A855F7", GradientStart = "#A855F7", GradientEnd = "#22D3EE", Purple = "#EC4899", Glow = "#A855F7" },
            ["Blood Red"] = new ThemePalette { Accent = "#FF4D4D", GradientStart = "#FF4D4D", GradientEnd = "#FF9838", Purple = "#FF4D4D", Glow = "#FF4D4D" },
            ["Ocean"] = new ThemePalette { Accent = "#38BDF8", GradientStart = "#38BDF8", GradientEnd = "#818CF8", Purple = "#818CF8", Glow = "#38BDF8" },
            ["Emerald"] = new ThemePalette { Accent = "#34D399", GradientStart = "#34D399", GradientEnd = "#A3E635", Purple = "#22D3EE", Glow = "#34D399" },
            ["Sunset"] = new ThemePalette { Accent = "#FB923C", GradientStart = "#FB923C", GradientEnd = "#F472B6", Purple = "#F472B6", Glow = "#FB923C" },
            ["Mono"] = new ThemePalette { Accent = "#E5E7EB", GradientStart = "#E5E7EB", GradientEnd = "#9CA3AF", Purple = "#9CA3AF", Glow = "#E5E7EB" },
        };

        public static Color Parse(string? hex, Color fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                    return (Color)ColorConverter.ConvertFromString(hex.Trim())!;
            }
            catch { }

            return fallback;
        }

        // ApplyTheme runs in every window's ctor, but the brand resources are app-level and shared, so
        // there's no need to rebuild them for each new window — skip when nothing actually changed.
        private static string _lastSignature = "";

        // Apply the saved palette + effect toggles.
        public static void ApplyFromSettings()
        {
            var palette = App.Settings?.Prop?.Palette ?? new ThemePalette();
            if (Signature(palette) == _lastSignature)
                return;
            Apply(palette);
        }

        public static void Apply(ThemePalette p)
        {
            var res = Application.Current.Resources;

            Color cyan = Parse(p.Accent, Color.FromRgb(0x22, 0xD3, 0xEE));
            Color gStart = Parse(p.GradientStart, cyan);
            Color gEnd = Parse(p.GradientEnd, Color.FromRgb(0x4A, 0xDE, 0x80));
            Color purple = Parse(p.Purple, Color.FromRgb(0xA8, 0x55, 0xF7));
            Color ink = Parse(p.Background, Color.FromRgb(0x0B, 0x0F, 0x14));
            Color surface = Parse(p.Surface, Color.FromRgb(0x12, 0x18, 0x21));
            Color hairline = Parse(p.Hairline, Color.FromRgb(0x1F, 0x2A, 0x37));
            Color glow = Parse(p.Glow, cyan);

            bool glass = App.Settings?.Prop?.EnableGlass ?? true;
            bool glowOn = App.Settings?.Prop?.EnableGlow ?? true;
            bool aurora = App.Settings?.Prop?.EnableAurora ?? true;

            // Colours
            res["BrandCyanColor"] = cyan;
            res["BrandGreenColor"] = gEnd;
            res["BrandPurpleColor"] = purple;
            res["BrandInkColor"] = ink;
            res["BrandSurfaceColor"] = surface;
            res["BrandHairlineColor"] = hairline;
            res["ApplicationBackgroundColor"] = ink;

            // Solid brushes
            res["BrandCyanBrush"] = Brush(cyan);
            res["BrandGreenBrush"] = Brush(gEnd);
            res["BrandPurpleBrush"] = Brush(purple);
            res["BrandInkBrush"] = Brush(ink);
            res["BrandSurfaceBrush"] = Brush(surface);
            res["BrandHairlineBrush"] = Brush(hairline);
            res["ApplicationBackgroundBrush"] = Brush(ink);

            // Glass surfaces. When glass is off, cards become the solid surface colour.
            res["GlassFillBrush"] = glass ? Brush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)) : Brush(surface);
            res["GlassFillHoverBrush"] = glass ? Brush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)) : Brush(Lighten(surface, 0.08));
            res["GlassBorderBrush"] = Brush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));

            // Gradients
            res["BrandGradientBrush"] = Gradient(gStart, gEnd);
            res["BrandGradientBrightBrush"] = Gradient(Lighten(gStart, 0.28), Lighten(gEnd, 0.28));
            res["BrandGradientTriBrush"] = TriGradient(purple, cyan, gEnd);

            // Glows (null when the glow toggle is off => no effect)
            res["BrandGlowEffect"] = glowOn ? Glow(glow, 18, 0.5) : null;
            res["BrandGlowSoft"] = glowOn ? Glow(glow, 14, 0.4) : null;
            res["BrandGlowStrong"] = glowOn ? Glow(glow, 30, 0.6) : null;

            // Aurora visibility (the AmbientBackground binds this)
            res["AuroraVisibility"] = aurora ? Visibility.Visible : Visibility.Collapsed;

            // Accent — DynamicResource-backed across every WPF-UI control, so this is fully live.
            Accent.Apply(cyan, ThemeType.Dark);

            _lastSignature = Signature(p);
        }

        private static string Signature(ThemePalette p)
        {
            var s = App.Settings?.Prop;
            return string.Join("|", p.Accent, p.GradientStart, p.GradientEnd, p.Purple, p.Background,
                p.Surface, p.Hairline, p.Glow, s?.EnableAurora, s?.EnableGlass, s?.EnableGlow);
        }

        private static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private static LinearGradientBrush Gradient(Color a, Color b)
        {
            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 1));
            g.Freeze();
            return g;
        }

        private static LinearGradientBrush TriGradient(Color a, Color b, Color c)
        {
            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            g.GradientStops.Add(new GradientStop(a, 0));
            g.GradientStops.Add(new GradientStop(b, 0.55));
            g.GradientStops.Add(new GradientStop(c, 1));
            g.Freeze();
            return g;
        }

        private static DropShadowEffect Glow(Color c, double blur, double opacity)
        {
            var e = new DropShadowEffect
            {
                Color = c,
                BlurRadius = blur,
                ShadowDepth = 0,
                Opacity = opacity
            };
            e.Freeze();
            return e;
        }

        private static Color Lighten(Color c, double amount)
        {
            byte L(byte v) => (byte)Math.Clamp(v + (255 - v) * amount, 0, 255);
            return Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B));
        }
    }
}

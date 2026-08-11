using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using RAWSelectionAssistant.Core.Models;
using AppThemeMode = RAWSelectionAssistant.Core.Models.ThemeMode;

namespace RAWSelectionAssistant.Services;

public interface IAppearanceService : IDisposable
{
    void Initialize(AppearanceSettings settings);
    void Apply(AppearanceSettings settings);
    string ResolveAccentHex(AppearanceSettings settings);
}

public sealed class AppearanceService : IAppearanceService
{
    private const string ThemeMarker = "DesignSystem/Theme.";
    private AppearanceSettings? _settings;

    public void Initialize(AppearanceSettings settings)
    {
        _settings = settings;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply(settings);
    }

    public void Apply(AppearanceSettings settings)
    {
        _settings = settings;
        var highContrast = SystemParameters.HighContrast;
        var effectiveTheme = highContrast ? "HighContrast" : ResolveTheme(settings.Theme);
        ReplaceThemeDictionary(effectiveTheme);
        ApplyAccent(settings, highContrast);
        ApplyMetrics(settings);
        NativeWindowTheme.ApplyAll(effectiveTheme == "Dark");
    }

    public string ResolveAccentHex(AppearanceSettings settings) => AccentColorService.ResolveHex(settings.Accent, settings.CustomAccentColor);

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_settings is not null && Application.Current is not null)
        {
            Application.Current.Dispatcher.BeginInvoke(() => Apply(_settings));
        }
    }

    private static string ResolveTheme(AppThemeMode mode) => mode switch
    {
        AppThemeMode.Light => "Light",
        AppThemeMode.Dark => "Dark",
        _ => IsWindowsLightTheme() ? "Light" : "Dark"
    };

    private static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    private static void ReplaceThemeDictionary(string themeName)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(dictionary => dictionary.Source?.OriginalString.Contains(ThemeMarker, StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary { Source = new Uri($"Resources/DesignSystem/Theme.{themeName}.xaml", UriKind.Relative) };
        if (current is null) dictionaries.Insert(0, replacement);
        else dictionaries[dictionaries.IndexOf(current)] = replacement;
    }

    private static void ApplyAccent(AppearanceSettings settings, bool highContrast)
    {
        // Pixel Tart A-v2 owns the runtime primary action color. User presets remain
        // readable for migration, but no longer repaint product semantics.
        var color = highContrast ? SystemColors.HighlightColor : Color.FromRgb(0x18, 0xA8, 0x8C);
        var resources = Application.Current.Resources;
        resources["AccentBrush"] = AccentColorService.Brush(color);
        resources["AccentHoverBrush"] = AccentColorService.Brush(AccentColorService.Adjust(color, -0.12));
        resources["AccentPressedBrush"] = AccentColorService.Brush(AccentColorService.Adjust(color, -0.22));
        resources["AccentSoftBrush"] = AccentColorService.BlendBrush(color, highContrast ? SystemColors.WindowColor : GetThemeColor("SurfacePrimaryColor"), 0.14);
        resources["AccentForegroundBrush"] = AccentColorService.Brush(AccentColorService.GetReadableForeground(color));
    }

    private static Color GetThemeColor(string key) => Application.Current.TryFindResource(key) is Color color ? color : Colors.White;

    private static void ApplyMetrics(AppearanceSettings settings)
    {
        var resources = Application.Current.Resources;
        var compact = settings.Density == InterfaceDensity.Compact;
        var largeFont = settings.FontScale == FontScale.Large;
        resources["ControlHeight"] = compact ? 32d : 38d;
        resources["ControlPadding"] = compact ? new Thickness(10, 5, 10, 5) : new Thickness(14, 8, 14, 8);
        resources["RowHeight"] = compact ? 34d : 42d;
        resources["BodyFontSize"] = largeFont ? 15d : 13d;
        resources["CaptionFontSize"] = largeFont ? 13d : 12d;
        resources["MinimumCaptionFontSize"] = largeFont ? 13d : 11d;
        resources["SidebarWidth"] = settings.SidebarCollapsed
            ? SidebarLayoutMetrics.CollapsedWidth
            : SidebarLayoutMetrics.ExpandedWidth;
    }
}

public static class AccentColorService
{
    private static readonly IReadOnlyDictionary<AccentPreset, string> Presets = new Dictionary<AccentPreset, string>
    {
        [AccentPreset.KitaoBlue] = "#C98220",
        [AccentPreset.MossGreen] = "#2D6A4F",
        [AccentPreset.WineRed] = "#9B3A4A",
        [AccentPreset.NightPurple] = "#6750A4",
        [AccentPreset.WarmAmber] = "#A15C00",
        [AccentPreset.Graphite] = "#4F5B66"
    };

    public static string ResolveHex(AccentPreset preset, string? customColor)
    {
        if (preset == AccentPreset.System) return ToHex(SystemParameters.WindowGlassColor);
        if (preset == AccentPreset.Custom && TryParse(customColor, out var custom)) return ToHex(custom);
        return Presets.TryGetValue(preset, out var value) ? value : Presets[AccentPreset.KitaoBlue];
    }

    public static Color ResolveColor(AccentPreset preset, string? customColor) =>
        TryParse(ResolveHex(preset, customColor), out var color) ? color : Color.FromRgb(15, 108, 189);

    public static bool TryParse(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim().TrimStart('#');
        if (text.Length != 6 || !byte.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out var red) ||
            !byte.TryParse(text.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green) ||
            !byte.TryParse(text.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue)) return false;
        color = Color.FromRgb(red, green, blue);
        return true;
    }

    public static Color Adjust(Color color, double amount)
    {
        byte Scale(byte channel) => (byte)Math.Clamp(channel + (amount < 0 ? channel : 255 - channel) * amount, 0, 255);
        return Color.FromRgb(Scale(color.R), Scale(color.G), Scale(color.B));
    }

    public static Color GetReadableForeground(Color background) => RelativeLuminance(background) > 0.42 ? Colors.Black : Colors.White;

    public static double ContrastRatio(Color first, Color second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    public static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush BlendBrush(Color foreground, Color background, double amount)
    {
        byte Blend(byte front, byte back) => (byte)Math.Round(front * amount + back * (1 - amount));
        return Brush(Color.FromRgb(Blend(foreground.R, background.R), Blend(foreground.G, background.G), Blend(foreground.B, background.B)));
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
}

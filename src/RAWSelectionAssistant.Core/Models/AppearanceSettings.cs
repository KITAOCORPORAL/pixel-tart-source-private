namespace RAWSelectionAssistant.Core.Models;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public enum AccentPreset
{
    System,
    KitaoBlue,
    MossGreen,
    WineRed,
    NightPurple,
    WarmAmber,
    Graphite,
    Custom
}

public enum InterfaceDensity
{
    Comfortable,
    Compact
}

public enum SidebarMode
{
    AlwaysExpanded,
    AutoCollapse,
    Remember
}

public enum MotionPreference
{
    Normal,
    Reduced
}

public enum FontScale
{
    Standard,
    Large
}

public sealed class AppearanceSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public AccentPreset Accent { get; set; } = AccentPreset.KitaoBlue;
    public string CustomAccentColor { get; set; } = "#C98220";
    public InterfaceDensity Density { get; set; } = InterfaceDensity.Comfortable;
    public SidebarMode Sidebar { get; set; } = SidebarMode.Remember;
    public bool SidebarCollapsed { get; set; }
    public MotionPreference Motion { get; set; } = MotionPreference.Normal;
    public FontScale FontScale { get; set; } = FontScale.Standard;
}

namespace RAWSelectionAssistant.Core.Utilities;

public static class AppDataPaths
{
    private static readonly string ProcessName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
    private static readonly bool IsAcceptanceBuild = ProcessName.EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase);
    private static readonly bool IsUiReviewBuild = ProcessName.EndsWith(".UiReview", StringComparison.OrdinalIgnoreCase);
    private static readonly bool IsExplicitIsolatedRuntime = string.Equals(
        Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME"), "1", StringComparison.Ordinal);
    private static readonly string? RootOverride = IsAcceptanceBuild
        ? Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")
        : IsExplicitIsolatedRuntime ? Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT") : null;

    public static string Root { get; } = ResolveRoot();

    public static string LegacyRoot { get; } = ResolveLegacyRoot();

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string IndexDirectory => Path.Combine(Root, "Indexes");
    public static string IndexFile => Path.Combine(IndexDirectory, "raw-index.json");
    public static string LogDirectory => Path.Combine(Root, "Logs");
    public static string CacheDirectory => Path.Combine(Root, "Cache");
    public static string WeatherCacheDirectory => Path.Combine(CacheDirectory, "Weather");
    public static string TetherProxyCacheDirectory => Path.Combine(CacheDirectory, "TetherProxies");
    public static string TetherFullResolutionCacheDirectory => Path.Combine(CacheDirectory, "TetherFullResolution");
    public static string TetherLutCacheDirectory => Path.Combine(CacheDirectory, "TetherLutPreviews");
    public static string TetherColorSettingsDirectory => Path.Combine(DataDirectory, "TetherColor");
    public static string TetherDisplaySettingsDirectory => Path.Combine(Root, "TetherDisplaySettings");
    public static string TutorialDirectory => Path.Combine(Root, "Tutorial");
    public static string LicenseDirectory => Path.Combine(Root, "License");
    public static string ProjectDirectory => Path.Combine(Root, "Projects");
    public static string DataDirectory => Path.Combine(Root, "Data");
    public static string DatabaseFile => Path.Combine(DataDirectory, "pixel-tart.db");
    public static string DatabaseBackupDirectory => Path.Combine(Root, "Backups", "Database");
    public static string MigrationBackupDirectory => Path.Combine(Root, "Backups", "Migration");

    private static string ResolveRoot()
    {
        if (!string.IsNullOrWhiteSpace(RootOverride) && Path.IsPathFullyQualified(RootOverride))
            return Path.GetFullPath(RootOverride);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            IsUiReviewBuild ? "KitaoPhotoSelector.UiReview" : IsAcceptanceBuild ? "KitaoPhotoSelector.Acceptance" : "KitaoPhotoSelector");
    }

    private static string ResolveLegacyRoot()
    {
        if (!string.IsNullOrWhiteSpace(RootOverride) && Path.IsPathFullyQualified(RootOverride))
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(RootOverride))!, IsAcceptanceBuild ? "RAWSelectionAssistant.Acceptance" : "RAWSelectionAssistant.IsolatedRuntime");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            IsUiReviewBuild ? "RAWSelectionAssistant.UiReview" : IsAcceptanceBuild ? "RAWSelectionAssistant.Acceptance" : "RAWSelectionAssistant");
    }

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IndexDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(TetherProxyCacheDirectory);
        Directory.CreateDirectory(TetherFullResolutionCacheDirectory);
        Directory.CreateDirectory(TetherLutCacheDirectory);
        Directory.CreateDirectory(TetherColorSettingsDirectory);
        Directory.CreateDirectory(TetherDisplaySettingsDirectory);
        Directory.CreateDirectory(LicenseDirectory);
        Directory.CreateDirectory(ProjectDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(DatabaseBackupDirectory);
        Directory.CreateDirectory(MigrationBackupDirectory);
    }
}

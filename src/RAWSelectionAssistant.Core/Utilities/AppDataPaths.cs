namespace RAWSelectionAssistant.Core.Utilities;

public static class AppDataPaths
{
    private static readonly string ProcessName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
    private static readonly bool IsAcceptanceBuild = ProcessName.EndsWith(".Acceptance", StringComparison.OrdinalIgnoreCase);
    private static readonly bool IsUiReviewBuild = ProcessName.EndsWith(".UiReview", StringComparison.OrdinalIgnoreCase);

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        IsUiReviewBuild ? "KitaoPhotoSelector.UiReview" : IsAcceptanceBuild ? "KitaoPhotoSelector.Acceptance" : "KitaoPhotoSelector");

    public static string LegacyRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        IsUiReviewBuild ? "RAWSelectionAssistant.UiReview" : IsAcceptanceBuild ? "RAWSelectionAssistant.Acceptance" : "RAWSelectionAssistant");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string IndexDirectory => Path.Combine(Root, "Indexes");
    public static string IndexFile => Path.Combine(IndexDirectory, "raw-index.json");
    public static string LogDirectory => Path.Combine(Root, "Logs");
    public static string CacheDirectory => Path.Combine(Root, "Cache");
    public static string TutorialDirectory => Path.Combine(Root, "Tutorial");
    public static string LicenseDirectory => Path.Combine(Root, "License");
    public static string ProjectDirectory => Path.Combine(Root, "Projects");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(IndexDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LicenseDirectory);
        Directory.CreateDirectory(ProjectDirectory);
    }
}

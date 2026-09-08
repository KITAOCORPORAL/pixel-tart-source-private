namespace RAWSelectionAssistant.Core.Models;

public sealed class AssetLibraryPortableSettings
{
    public const int CurrentVersion = 1;
    public const int MaximumRecentLibraries = 12;

    public int Version { get; set; } = CurrentVersion;
    public string CurrentContainerPath { get; set; } = string.Empty;
    public List<AssetLibraryRecentEntry> RecentLibraries { get; set; } = [];

    public void Normalize()
    {
        Version = CurrentVersion;
        CurrentContainerPath = NormalizePath(CurrentContainerPath);
        RecentLibraries = (RecentLibraries ?? [])
            .Where(entry => entry is not null)
            .Select(entry => entry.Normalize())
            .Where(entry => entry.LibraryId != Guid.Empty && entry.ContainerPath.Length > 0)
            .GroupBy(entry => entry.ContainerPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.LastOpenedAt).First())
            .OrderByDescending(entry => entry.LastOpenedAt)
            .Take(MaximumRecentLibraries)
            .ToList();
    }

    public void RecordOpened(Guid libraryId, string displayName, string containerPath, DateTimeOffset openedAt)
    {
        var normalizedPath = NormalizePath(containerPath);
        if (libraryId == Guid.Empty || normalizedPath.Length == 0) throw new ArgumentException("素材库身份和路径不能为空。");
        RecentLibraries.RemoveAll(entry => entry.LibraryId == libraryId ||
            string.Equals(NormalizePath(entry.ContainerPath), normalizedPath, StringComparison.OrdinalIgnoreCase));
        RecentLibraries.Insert(0, new AssetLibraryRecentEntry
        {
            LibraryId = libraryId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(normalizedPath) : displayName.Trim(),
            ContainerPath = normalizedPath,
            LastOpenedAt = openedAt
        });
        CurrentContainerPath = normalizedPath;
        Normalize();
    }

    internal static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return string.Empty; }
    }
}

public sealed class AssetLibraryRecentEntry
{
    public Guid LibraryId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ContainerPath { get; set; } = string.Empty;
    public DateTimeOffset LastOpenedAt { get; set; }

    internal AssetLibraryRecentEntry Normalize()
    {
        DisplayName = (DisplayName ?? string.Empty).Trim();
        ContainerPath = AssetLibraryPortableSettings.NormalizePath(ContainerPath);
        return this;
    }
}

using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetFolderTreeLayoutTests
{
    [TestMethod]
    public void FolderTreeUsesCompactRowsFolderIconsAndMutedCounts()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        foreach (var token in new[] { "AutomationProperties.AutomationId=\"AssetFolderTree\"", "MinHeight\" Value=\"30", "AssetAction.Folder", "Text=\"{Binding CountText}\"", "Brush.Text.Muted", "BorderThickness\" Value=\"0" })
            StringAssert.Contains(xaml, token);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

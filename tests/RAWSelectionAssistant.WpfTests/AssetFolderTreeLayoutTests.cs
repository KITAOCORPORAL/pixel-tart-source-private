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
    { return TestResources.Root; }
}

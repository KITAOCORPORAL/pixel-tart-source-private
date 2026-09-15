using System.IO;
using System.Windows.Media;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetCollectionCountTests
{
    [TestMethod]
    public void SystemCollectionRowsExposeLiveCountAndIcons()
    {
        var nodeSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryOrganizationNodes.cs"));
        var viewModelSource = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs"));
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        StringAssert.Contains(nodeSource, "public int Count");
        StringAssert.Contains(nodeSource, "public Geometry IconData");
        StringAssert.Contains(viewModelSource, "RefreshSystemCollectionCountsAsync");
        StringAssert.Contains(viewModelSource, "PageSize = 1");
        StringAssert.Contains(xaml, "Text=\"{Binding Count, StringFormat=N0}\"");
        StringAssert.Contains(xaml, "Data=\"{Binding IconData}\"");
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

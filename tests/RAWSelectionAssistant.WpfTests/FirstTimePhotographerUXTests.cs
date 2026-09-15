using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class FirstTimePhotographerUXTests
{
    [TestMethod]
    [DataRow("导入照片", "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml", "导入")]
    [DataRow("整理照片", "src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml", "开始整理")]
    [DataRow("RAW 转 JPG", "src/RAWSelectionAssistant/Views/RawToJpegModal.xaml", "开始转换")]
    [DataRow("筛选 4 星照片", "src/PixelTart.Modules.AssetLibrary/AssetLibraryP3Styles.xaml", "筛选字段")]
    [DataRow("建立智能文件夹", "src/PixelTart.Modules.AssetLibrary/AssetSmartFolderEditorView.xaml", "保存智能文件夹")]
    [DataRow("关联拍摄项目", "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml", "关联项目")]
    public void FirstTimeTaskHasDirectUserFacingAction(string task, string relative, string action)
    {
        var source = Text(relative);
        StringAssert.Contains(source, action, $"{task} lacks a direct photographer-facing action.");
    }

    [TestMethod]
    public void FirstTimeTaskSurfacesRequireNoArchitectureVocabulary()
    {
        var source = string.Join('\n', new[]
        {
            Text("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml"),
            Text("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml"),
            Text("src/PixelTart.Modules.AssetLibrary/AssetSmartFolderEditorView.xaml")
        });
        foreach (var term in new[] { "TaskId", "LibRaw", "SHA-256", "Database", "QueryOption" })
            Assert.DoesNotContain(term, source, $"First-time surfaces expose {term}.");
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

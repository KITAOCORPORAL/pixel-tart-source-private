using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UXSimplificationRegressionTests
{
    [TestMethod]
    public void Organize_DefaultSurfaceUsesPhotographerLanguageAndKeepsSafety()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml");
        ContainsAll(xaml, "整理哪些照片？", "怎么整理？", "保存到哪里？", "预览整理结果", "开始整理", "文件确认复制完整后才继续处理原文件");
        ContainsNone(xaml, "SHA-256", "TaskId", "操作清单", "ConflictPolicy");
    }

    [TestMethod]
    public void RawToJpeg_DefaultSurfaceIsSimpleAndTechnicalDecoderIsHidden()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml");
        var viewModel = Text("src/RAWSelectionAssistant/ViewModels/RawToJpegViewModel.cs");
        ContainsAll(xaml, "输出目录", "尺寸", "JPEG 质量", "高级设置", "开始转换");
        ContainsNone(xaml, "LibRaw", "TaskId");
        Assert.DoesNotContain("TaskId:", viewModel);
    }

    [TestMethod]
    public void SmartFolderUsesHumanConditionsAndLivePhotoCount()
    {
        var editor = Text("src/PixelTart.Modules.AssetLibrary/AssetSmartFolderEditorView.xaml");
        var styles = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryP3Styles.xaml");
        var viewModel = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3QueryComposer.cs");
        ContainsAll(editor + styles + viewModel, "满足：", "找到 {P2QueryTotalCount:N0} 张照片", "保存智能文件夹");
        ContainsNone(editor, "通用规则", "规则树");
        Assert.DoesNotContain("AutomationProperties.Name=\"P3Query", editor);
    }

    [TestMethod]
    public void GalleryHidesZeroRatingAndShowsOnlyOneSimilarityScore()
    {
        var xaml = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml");
        var viewModel = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.cs");
        ContainsAll(xaml, "Visibility=\"{Binding HasRating", "Asset.DisplayName");
        ContainsAll(viewModel, "public bool HasRating => Asset.Rating > 0", "相似度 {Scores.Overall:F0}%");
        ContainsNone(viewModel, "相似 {Scores.Overall:F0} · 色", "ΔE76 {ColorDeltaE:F1}");
    }

    [TestMethod]
    public void InspectorUsesHumanFileValuesAndHidesNormalMissingRow()
    {
        var xaml = Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml");
        ContainsAll(xaml, "InspectorFormat", "InspectorFileSize", "位置：{0}", "源文件不可访问", "Header=\"查看详细信息\"");
        ContainsNone(xaml, "文件缺失：{0}", "{0:N0} 字节", "SelectedAsset.MediaType, StringFormat=格式");
    }

    [TestMethod]
    public void TaskCenterHidesIdAndKeepsTechnicalDetailsCollapsed()
    {
        var xaml = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        ContainsAll(xaml, "查看详细信息", "Header=\"技术信息\"", "{Binding DiagnosticText}", "Header=\"停止\"");
        ContainsNone(xaml, "Text=\"{Binding TaskIdText}\"", "展开技术信息");
    }

    [TestMethod]
    public void AuxiliarySurfacesAreMutuallyExclusive()
    {
        var sources = string.Join('\n', new[]
        {
            Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs"),
            Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3QueryComposer.cs"),
            Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3SmartFolder.cs"),
            Text("src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3TagManager.cs")
        });
        ContainsAll(sources, "ClosePrimaryAuxiliarySurfacesExceptSmartFolder", "ClosePrimaryAuxiliarySurfacesExceptQuery", "ClosePrimaryAuxiliarySurfacesExceptTagManager", "ClosePrimaryAuxiliarySurfacesExceptInspirationTray");
    }

    private static void ContainsAll(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void ContainsNone(string text, params string[] values) { foreach (var value in values) Assert.DoesNotContain(value, text); }
    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

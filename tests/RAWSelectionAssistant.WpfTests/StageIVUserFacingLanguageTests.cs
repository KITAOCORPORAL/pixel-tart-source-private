using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UserFacingChineseLanguageTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] Forbidden = ["Look", "Match", "Reference", "Split", "Before", "After", "Shot Notes"];

    [TestMethod]
    public void StageIVRelatedXamlHasNoForbiddenVisibleEnglish()
    {
        foreach (var relative in new[]
        {
            "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml",
            "src/RAWSelectionAssistant/Views/PublishingExportView.xaml",
            "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml"
        })
        {
            var document = XDocument.Load(Path.Combine(Root(), relative));
            var visible = document.Descendants().SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName is "Content" or "Header" or "Text" or "ToolTip" ||
                    attribute.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal) || attribute.Name.LocalName.EndsWith(".HelpText", StringComparison.Ordinal))
                .Select(attribute => attribute.Value).Where(value => !value.StartsWith("{Binding", StringComparison.Ordinal)).ToArray();
            foreach (var text in visible)
                foreach (var forbidden in Forbidden)
                    Assert.IsFalse(Regex.IsMatch(text, $@"\b{Regex.Escape(forbidden)}\b", RegexOptions.IgnoreCase), $"{relative}: 用户可见文案泄漏 {forbidden}: {text}");
        }
    }

    [TestMethod]
    public void IndustryAbbreviationAllowlistIsExplicitAndNarrow()
    {
        CollectionAssert.AreEquivalent(new[] { "RAW", "JPEG", "PNG", "TIFF", "ICC", "LUT", "EXIF", "RGB", "HSL", "ISO" }, AllowedIndustryAbbreviations);
    }

    [TestMethod]
    public void TetherAndColorSchemeTermsUseChineseProductLanguage()
    {
        var tether = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml")) +
            File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/ViewModels/TetherReferenceModeViewModel.cs"));
        var library = File.ReadAllText(Path.Combine(Root(), "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.xaml"));
        foreach (var expected in new[] { "参考仿色", "仿色强度", "原片", "仿色", "左右对比", "并排对比", "应用到后续拍摄", "肤色保护", "高光保护", "拍摄备注" }) StringAssert.Contains(tether, expected);
        foreach (var expected in new[] { "项目色彩方案", "项目配色", "目标影调" }) StringAssert.Contains(library, expected);
    }

    [TestMethod]
    public void DesignSystemDefinesProductRadiusHierarchyAndThemedPopups()
    {
        var tokens = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml"));
        foreach (var token in new[] { "CompactCornerRadius", "ControlCornerRadius", "CardCornerRadius", "PopoverCornerRadius", "DialogCornerRadius", "LargePanelCornerRadius" }) StringAssert.Contains(tokens, token);
        var inputs = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Inputs.xaml"));
        StringAssert.Contains(inputs, "DynamicResource PopoverCornerRadius"); StringAssert.Contains(inputs, "DropdownBackgroundBrush");
    }

    private static readonly string[] AllowedIndustryAbbreviations = ["RAW", "JPEG", "PNG", "TIFF", "ICC", "LUT", "EXIF", "RGB", "HSL", "ISO"];
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

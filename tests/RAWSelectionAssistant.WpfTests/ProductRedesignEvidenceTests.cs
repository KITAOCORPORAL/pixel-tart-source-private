using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ProductRedesignEvidenceTests
{
    private static readonly string[] ProductViews =
    [
        "src/RAWSelectionAssistant/Views/RawToJpegModal.xaml",
        "src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml",
        "src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml"
    ];

    [TestMethod]
    public void ProductViews_UseAv2ResourcesWithoutLiteralColorsOrSizes()
    {
        var literalColor = new Regex("(?<![A-Za-z0-9_])#[0-9A-Fa-f]{6,8}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant);
        var literalFontSize = new Regex("FontSize=\"[0-9]+(?:\\.[0-9]+)?\"", RegexOptions.CultureInvariant);
        foreach (var relative in ProductViews)
        {
            var source = Read(relative);
            XDocument.Parse(source);
            Assert.IsFalse(literalColor.IsMatch(source), $"Literal color found in {relative}.");
            Assert.IsFalse(literalFontSize.IsMatch(source), $"Literal font size found in {relative}.");
        }
    }

    [TestMethod]
    public void ToolModals_UseLockedWidthAndSinglePrimaryAction()
    {
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.AreEqual(2, Count(main, "Style=\"{StaticResource ModalSurface}\" Width=\"960\""));
        foreach (var relative in ProductViews.Take(2))
        {
            var source = Read(relative);
            Assert.AreEqual(1, Count(source, "Style=\"{StaticResource Av2PrimaryButton}\""), relative);
            StringAssert.Contains(source, "<ColumnDefinition Width=\"320\" />");
        }
    }

    [TestMethod]
    public void UiReviewDriver_UsesOnlyExplicitIsolatedRuntime()
    {
        var driver = Read("tools/ProductRedesignReview/Invoke-ProductRedesignReview.ps1");
        StringAssert.Contains(driver, "$env:PIXEL_TART_ISOLATED_RUNTIME = '1'");
        StringAssert.Contains(driver, "$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT = $runtimeRoot");
        StringAssert.Contains(driver, "artifacts\\ui-review\\product-redesign");
        StringAssert.Contains(driver, "Evidence already exists; choose a new OutputRoot");
        Assert.IsFalse(driver.Contains("$env:LOCALAPPDATA", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(driver.Contains("LocalApplicationData", StringComparison.OrdinalIgnoreCase));

        foreach (var relative in new[]
                 {
                     "src/RAWSelectionAssistant/MainWindow.xaml.cs",
                     "src/RAWSelectionAssistant/MainWindow.AutomatedDpiAcceptance.cs",
                     "src/RAWSelectionAssistant/Views/FeedbackDialog.xaml.cs"
                 })
        {
            var source = Read(relative);
            Assert.IsFalse(source.Contains("SpecialFolder.LocalApplicationData", StringComparison.Ordinal), relative);
            Assert.IsFalse(source.Contains("\"KitaoPhotoSelector.UiReview\"", StringComparison.Ordinal), relative);
        }
    }

    [TestMethod]
    public void UiReviewMatrix_CoversThemesBreakpointsDpiAndProductMasters()
    {
        var source = Read("tools/ProductRedesignReview/Invoke-ProductRedesignReview.ps1");
        foreach (var token in new[]
                 {
                     "Theme='Dark'", "Theme='Light'", "Theme='HighContrast'",
                     "Width=1280; Height=720", "Width=1280; Height=768", "Width=1366; Height=768",
                     "Width=1440; Height=900", "Width=1600; Height=900", "Width=1920; Height=1080", "Width=2560; Height=1440",
                     "Scale=1.0", "Scale=1.25", "Scale=1.5", "Scale=1.75", "Scale=2.0",
                     "BookingQuickCreate", "BookingQuickEdit", "BookingFullPlanning", "RawToJpeg", "BatchCompression",
                     "OnlineSelectionHome", "OnlineSelectionProject", "OnlineSelectionCreate"
                 })
        {
            StringAssert.Contains(source, token);
        }
    }

    [TestMethod]
    public void RawRuntimeLicenses_AreShippedWithProduct()
    {
        var project = Read("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
        var notices = Read("src/RAWSelectionAssistant/THIRD-PARTY-NOTICES.md");
        StringAssert.Contains(project, "Content Include=\"THIRD-PARTY-NOTICES.md\"");
        StringAssert.Contains(project, "Content Include=\"Licenses\\*\"");
        StringAssert.Contains(project, "CopyToPublishDirectory");
        StringAssert.Contains(notices, "Sdcb.LibRaw 0.21.1.7");
        StringAssert.Contains(notices, "LGPL-2.1-only");
        StringAssert.Contains(notices, "CDDL-1.0");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/Sdcb.LibRaw-0.21.1.7-MIT.txt"),
            "Permission is hereby granted, free of charge");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/LibRaw-0.21.1-LICENSE.LGPL.txt"),
            "GNU LESSER GENERAL PUBLIC LICENSE");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/LibRaw-0.21.1-LICENSE.CDDL.txt"),
            "COMMON DEVELOPMENT AND DISTRIBUTION LICENSE");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/LibRaw-0.21.1-COPYRIGHT.txt"),
            "LibRaw is free software");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/libjpeg-turbo-2.1.3-LICENSE.md.txt"),
            "libjpeg-turbo Licenses");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/libjpeg-turbo-2.1.3-README.ijg.txt"),
            "Independent JPEG Group");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/Little-CMS-2.12-COPYING.txt"),
            "Little CMS");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Licenses/zlib-1.2.11-README-LICENSE.txt"),
            "Copyright notice:");
    }

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;
    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

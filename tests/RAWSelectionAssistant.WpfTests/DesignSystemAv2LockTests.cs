using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class DesignSystemAv2LockTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void DarkPalette_LocksAv2SemanticColors()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AppBackgroundColor"] = "#0D0F12",
            ["Surface01Color"] = "#121519",
            ["Surface02Color"] = "#171B20",
            ["Surface03Color"] = "#1D2228",
            ["SurfaceElevatedColor"] = "#222830",
            ["BorderSubtleColor"] = "#292F37",
            ["BorderStrongColor"] = "#3A424C",
            ["TextPrimaryColor"] = "#F2F4F6",
            ["TextSecondaryColor"] = "#B2B8C0",
            ["TextMutedColor"] = "#747C86",
            ["TextDisabledColor"] = "#555C65",
            ["PrimaryColor"] = "#18A88C",
            ["PrimaryHoverColor"] = "#20B89B",
            ["PrimaryPressedColor"] = "#128671",
            ["PhotographyGoldColor"] = "#D79A32",
            ["SuccessColor"] = "#42B883",
            ["WarningColor"] = "#E0AD43",
            ["DangerColor"] = "#D85A5A",
            ["InfoColor"] = "#4E8FD4",
            ["CalendarFreeColor"] = "#59616B",
            ["CalendarScheduledColor"] = "#E05252",
            ["CalendarShotColor"] = "#3DB879",
            ["CalendarPendingReturnColor"] = "#DDAF32",
            ["CalendarReturnedColor"] = "#3E8ED0"
        };

        AssertKeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/Colors.Dark.xaml", "Color", expected);
    }

    [TestMethod]
    public void Typography_LocksAv2FamiliesAndScale()
    {
        var typography = KeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/Typography.xaml", "FontFamily");
        Assert.AreEqual("Microsoft YaHei UI", typography["ChineseFontFamily"]);
        Assert.AreEqual("Segoe UI Variable, Segoe UI", typography["LatinNumericFontFamily"]);

        var expectedSizes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PageTitleFontSize"] = "22",
            ["HeroFontSize"] = "20",
            ["SectionFontSize"] = "16",
            ["CardFontSize"] = "14",
            ["BodyFontSize"] = "13",
            ["SecondaryFontSize"] = "12",
            ["MinimumCaptionFontSize"] = "11",
            ["NumericLargeFontSize"] = "24"
        };

        AssertKeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml", "Double", expectedSizes);
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Resources/DesignSystem/Typography.xaml"),
            "x:Key=\"CaptionText\" TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"{DynamicResource MinimumCaptionFontSize}\"");
    }

    [TestMethod]
    public void Spacing_LocksAv2Scale()
    {
        var expected = new[] { 4, 8, 12, 16, 24, 32, 48 };
        var spacing = KeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/Spacing.xaml", "Double")
            .Where(pair => Regex.IsMatch(pair.Key, "^Spacing[0-9]+$", RegexOptions.CultureInvariant))
            .OrderBy(pair => int.Parse(pair.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        CollectionAssert.AreEqual(expected.Select(value => $"Spacing{value}={value}").ToArray(),
            spacing.Select(pair => $"{pair.Key}={pair.Value}").ToArray());
    }

    [TestMethod]
    public void Radius_LocksAv2Scale()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RadiusSmall"] = "4",
            ["RadiusControl"] = "6",
            ["RadiusCard"] = "8",
            ["RadiusDrawer"] = "10",
            ["RadiusModal"] = "12"
        };

        AssertKeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/Radius.xaml", "CornerRadius", expected);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(),
            KeyedValues("src/RAWSelectionAssistant/Resources/DesignSystem/Radius.xaml", "CornerRadius").Keys.ToArray());
    }

    [TestMethod]
    public void App_MergesCompleteAv2ResourceSetInDependencyOrder()
    {
        var required = new[]
        {
            "Resources/DesignSystem/Theme.Dark.xaml",
            "Resources/DesignSystem/AccentColors.xaml",
            "Resources/DesignSystem/Spacing.xaml",
            "Resources/DesignSystem/Radius.xaml",
            "Resources/DesignSystem/DesignTokens.xaml",
            "Resources/DesignSystem/Typography.xaml",
            "Resources/DesignSystem/Buttons.xaml",
            "Resources/DesignSystem/Inputs.xaml",
            "Resources/DesignSystem/Cards.xaml",
            "Resources/DesignSystem/Navigation.xaml",
            "Resources/DesignSystem/Icons.xaml",
            "Resources/DesignSystem/Calendar.xaml",
            "Resources/DesignSystem/Modal.xaml",
            "Resources/DesignSystem/Drawer.xaml",
            "Resources/DesignSystem/Tooltip.xaml",
            "Resources/DesignSystem/ContextMenu.xaml",
            "Resources/DesignSystem/ScrollBars.xaml",
            "Resources/DesignSystem/EmptyState.xaml"
        };
        var sources = Load("src/RAWSelectionAssistant/App.xaml")
            .Descendants()
            .Attributes("Source")
            .Select(attribute => attribute.Value)
            .ToArray();

        CollectionAssert.AreEqual(required, sources.Where(required.Contains).ToArray());
        foreach (var source in required)
        {
            Assert.IsTrue(File.Exists(Path.Combine(Root(), "src", "RAWSelectionAssistant", source.Replace('/', Path.DirectorySeparatorChar))),
                $"Missing merged resource: {source}");
        }
    }

    [TestMethod]
    public void ComponentDictionaries_DoNotDeclareArbitraryHexColors()
    {
        var paletteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AccentColors.xaml",
            "Colors.Dark.xaml",
            "Colors.Light.xaml",
            "Colors.HighContrast.xaml",
            "Theme.Dark.xaml",
            "Theme.Light.xaml",
            "Theme.HighContrast.xaml"
        };
        var designSystem = Path.Combine(Root(), "src", "RAWSelectionAssistant", "Resources", "DesignSystem");
        var literalColor = new Regex("(?<![A-Za-z0-9_])#[0-9A-Fa-f]{3,8}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant);
        var violations = Directory.EnumerateFiles(designSystem, "*.xaml", SearchOption.TopDirectoryOnly)
            .Where(path => !paletteFiles.Contains(Path.GetFileName(path)))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { path, line, number = index + 1 }))
            .Where(item => literalColor.IsMatch(item.line))
            .Select(item => $"{Path.GetFileName(item.path)}:{item.number}")
            .ToArray();

        Assert.IsEmpty(violations,
            "Component resources must reference semantic brushes instead of literal colors: " + string.Join(", ", violations));
    }

    [TestMethod]
    public void FormalDesignDocumentsAndMasters_ExistAndAreNotEmpty()
    {
        var required = new[]
        {
            "docs/design/PixelTart_Visual_Design_System_v1.md",
            "docs/design/PixelTart_UX_Container_Rules_v1.md",
            "docs/design/PixelTart_Component_Catalog_v1.md",
            "docs/design/PixelTart_Visual_AntiPatterns_v1.md",
            "docs/design/PixelTart_Accessibility_Rules_v1.md",
            "docs/design/reference/Workbench_Av2_Master.md",
            "docs/design/reference/FullCalendar_Av2_Master.md",
            "docs/design/reference/ToolModal_Av2_Master.md",
            "docs/design/reference/OnlineSelectionHome_Av2_Master.md",
            "docs/design/reference/OnlineSelectionProject_Av2_Master.md"
        };

        foreach (var relative in required)
        {
            var path = Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"Missing design document: {relative}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(File.ReadAllText(path)), $"Empty design document: {relative}");
        }
    }

    private static void AssertKeyedValues(string relative, string localName, IReadOnlyDictionary<string, string> expected)
    {
        var actual = KeyedValues(relative, localName);
        foreach (var pair in expected)
        {
            Assert.IsTrue(actual.TryGetValue(pair.Key, out var value), $"Missing {pair.Key} in {relative}");
            Assert.AreEqual(pair.Value, value, $"Unexpected value for {pair.Key} in {relative}");
        }
    }

    private static Dictionary<string, string> KeyedValues(string relative, string localName) => Load(relative)
        .Descendants()
        .Where(element => element.Name.LocalName == localName)
        .Select(element => new { Key = (string?)element.Attribute(Xaml + "Key"), Value = element.Value.Trim() })
        .Where(item => item.Key is not null)
        .ToDictionary(item => item.Key!, item => item.Value, StringComparer.Ordinal);

    private static XDocument Load(string relative) => XDocument.Parse(Read(relative));

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}

using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryButtonReadabilityContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly IReadOnlyDictionary<string, int> ExpectedRoleCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["{StaticResource AssetLibraryPrimaryButton}"] = 5,
            ["{StaticResource AssetLibrarySecondaryButton}"] = 11,
            ["{StaticResource AssetLibraryCompactChipButton}"] = 9,
            ["{StaticResource AssetLibraryIconButton}"] = 1,
            ["{StaticResource AssetLibraryPaletteSwatchButton}"] = 1,
        };

    [TestMethod]
    public void EveryAssetLibraryButtonUsesOneExplicitLocalRole()
    {
        var document = LoadPage();
        var buttons = document.Descendants(Presentation + "Button").ToArray();

        Assert.HasCount(27, buttons, "Update the audited role map when an Asset Library button is added or removed.");
        foreach (var button in buttons)
        {
            var style = Attribute(button, "Style");
            Assert.IsTrue(
                ExpectedRoleCounts.ContainsKey(style),
                $"Button '{Describe(button)}' must use one of the five local Asset Library role styles; actual='{style}'.");
        }

        foreach (var expected in ExpectedRoleCounts)
            Assert.AreEqual(expected.Value, buttons.Count(button => Attribute(button, "Style") == expected.Key), expected.Key);

        var activeChip = buttons.Single(button => Attribute(button, "Tag") == "Active");
        Assert.AreEqual("{StaticResource AssetLibraryCompactChipButton}", Attribute(activeChip, "Style"));
        Assert.AreEqual("{Binding Key}", Attribute(activeChip, "CommandParameter"));
    }

    [TestMethod]
    public void PaletteSwatchIsTheOnlyTransparentChromeRoleAndHasTwoLayerKeyboardFocus()
    {
        var document = LoadPage();
        var roleNames = new[]
        {
            "AssetLibraryPrimaryButton",
            "AssetLibrarySecondaryButton",
            "AssetLibraryCompactChipButton",
            "AssetLibraryIconButton",
            "AssetLibraryPaletteSwatchButton",
        };

        foreach (var roleName in roleNames)
        {
            var style = Style(document, roleName);
            var background = DirectSetterValue(style, "Background");
            if (roleName == "AssetLibraryPaletteSwatchButton")
                Assert.AreEqual("Transparent", background);
            else
                Assert.AreNotEqual("Transparent", background, $"{roleName} must not fall back to transparent/system chrome.");
        }

        var paletteButtons = document.Descendants(Presentation + "Button")
            .Where(button => Attribute(button, "Style") == "{StaticResource AssetLibraryPaletteSwatchButton}")
            .ToArray();
        Assert.HasCount(1, paletteButtons);
        Assert.AreEqual("{Binding Hex}", Attribute(paletteButtons[0], "CommandParameter"));

        var paletteStyle = Style(document, "AssetLibraryPaletteSwatchButton");
        Assert.AreEqual("{StaticResource AssetLibraryPaletteFocusVisualStyle}",
            DirectSetterValue(paletteStyle, "FocusVisualStyle"));

        var focusVisual = Style(document, "AssetLibraryPaletteFocusVisualStyle");
        var focusTemplate = focusVisual.Descendants(Presentation + "ControlTemplate").Single();
        var focusGrid = focusTemplate.Descendants(Presentation + "Grid").Single();
        Assert.AreEqual("-4", Attribute(focusGrid, "Margin"),
            "The keyboard focus visual must live outside the palette button bounds.");
        Assert.AreEqual("False", Attribute(focusGrid, "IsHitTestVisible"));
        var focusNames = focusTemplate.Descendants(Presentation + "Border")
            .Select(border => Attribute(border, "Name"))
            .ToArray();
        CollectionAssert.Contains(focusNames, "PaletteOuterFocus");
        CollectionAssert.Contains(focusNames, "PaletteInnerFocus");
        Assert.AreEqual("{StaticResource AssetLibraryFocusRingBrush}",
            focusTemplate.Descendants(Presentation + "Border")
                .Single(border => Attribute(border, "Name") == "PaletteOuterFocus")
                .Attribute("BorderBrush")?.Value);
        Assert.AreEqual("{StaticResource AssetLibraryPaletteFocusInnerBrush}",
            focusTemplate.Descendants(Presentation + "Border")
                .Single(border => Attribute(border, "Name") == "PaletteInnerFocus")
                .Attribute("BorderBrush")?.Value);

        var paletteTemplate = Resource(document, "ControlTemplate", "AssetLibraryPaletteButtonTemplate");
        Assert.IsTrue(paletteTemplate.Descendants(Presentation + "ContentPresenter").Any());
        Assert.IsFalse(paletteTemplate.Descendants(Presentation + "Border")
            .Any(border => Attribute(border, "Name").Contains("Focus", StringComparison.Ordinal)),
            "Focus chrome must not consume or overlay the real swatch content.");
    }

    [TestMethod]
    public void LocalRoleStylesDeclareHoverPressedFocusAndReadableDisabledStates()
    {
        var document = LoadPage();
        var roleNames = new[]
        {
            "AssetLibraryPrimaryButton",
            "AssetLibrarySecondaryButton",
            "AssetLibraryCompactChipButton",
            "AssetLibraryIconButton",
            "AssetLibraryPaletteSwatchButton",
        };

        foreach (var roleName in roleNames)
        {
            var style = Style(document, roleName);
            AssertTrigger(style, "IsMouseOver", "True", roleName);
            AssertTrigger(style, "IsPressed", "True", roleName);
            var disabled = AssertTrigger(style, "IsEnabled", "False", roleName);
            foreach (var property in new[] { "Background", "Foreground", "BorderBrush", "Cursor" })
                Assert.IsTrue(disabled.Elements(Presentation + "Setter").Any(setter => Attribute(setter, "Property") == property),
                    $"{roleName} disabled state must set {property} explicitly.");
        }

        var standardTemplate = Resource(document, "ControlTemplate", "AssetLibraryButtonTemplate");
        Assert.IsTrue(standardTemplate.Descendants(Presentation + "Trigger")
            .Any(trigger => Attribute(trigger, "Property") == "IsKeyboardFocused" && Attribute(trigger, "Value") == "True"));
        Assert.AreEqual("{StaticResource AssetLibraryPaletteFocusVisualStyle}",
            DirectSetterValue(Style(document, "AssetLibraryPaletteSwatchButton"), "FocusVisualStyle"));

        var localButtonResources = document.Descendants()
            .Where(element => Attribute(element, "Key").StartsWith("AssetLibrary", StringComparison.Ordinal))
            .ToArray();
        Assert.IsFalse(localButtonResources.SelectMany(element => element.DescendantsAndSelf())
            .Any(element => element.Name == Presentation + "Setter" && Attribute(element, "Property") == "Opacity"),
            "Disabled Asset Library controls must use explicit colors, never whole-control opacity.");
    }

    [TestMethod]
    public void RoleColorsMeetDeterministicWcagContrastThresholds()
    {
        var document = LoadPage();

        AssertContrastSet(document, "AssetLibraryPrimaryForegroundColor", 4.5,
            "AssetLibraryPrimaryNormalColor", "AssetLibraryPrimaryHoverColor", "AssetLibraryPrimaryPressedColor");
        AssertContrastSet(document, "AssetLibrarySecondaryForegroundColor", 4.5,
            "AssetLibrarySecondaryNormalColor", "AssetLibrarySecondaryHoverColor", "AssetLibrarySecondaryPressedColor");
        AssertContrastSet(document, "AssetLibraryChipForegroundColor", 4.5,
            "AssetLibraryChipNormalColor", "AssetLibraryChipHoverColor", "AssetLibraryChipPressedColor");
        AssertContrastSet(document, "AssetLibraryPrimaryForegroundColor", 4.5,
            "AssetLibraryChipActiveNormalColor", "AssetLibraryChipActiveHoverColor", "AssetLibraryChipActivePressedColor");

        var focus = Color(document, "AssetLibraryFocusRingColor");
        var darkTheme = XDocument.Load(DarkThemePath, LoadOptions.SetLineInfo);
        var contentSurface = Rgb.Parse(darkTheme.Descendants(Presentation + "SolidColorBrush")
            .Single(element => Attribute(element, "Key") == "ContentBackgroundBrush")
            .Attribute("Color")?.Value ?? string.Empty);
        var cardSurface = Rgb.Parse(darkTheme.Descendants(Presentation + "SolidColorBrush")
            .Single(element => Attribute(element, "Key") == "WorkbenchCardBrush")
            .Attribute("Color")?.Value ?? string.Empty);
        AssertContrast(focus, contentSurface, 3.0, "focus ring / dark content surface");
        AssertContrast(focus, cardSurface, 3.0, "focus ring / dark card surface");
        foreach (var backgroundKey in new[]
                 {
                     "AssetLibraryPrimaryNormalColor", "AssetLibraryPrimaryHoverColor", "AssetLibraryPrimaryPressedColor",
                     "AssetLibrarySecondaryNormalColor", "AssetLibrarySecondaryHoverColor", "AssetLibrarySecondaryPressedColor",
                     "AssetLibraryChipNormalColor", "AssetLibraryChipHoverColor", "AssetLibraryChipPressedColor",
                     "AssetLibraryChipActiveNormalColor", "AssetLibraryChipActiveHoverColor", "AssetLibraryChipActivePressedColor",
                 })
            AssertContrast(focus, Color(document, backgroundKey), 3.0, $"focus ring / {backgroundKey}");

        var disabledBackground = Color(document, "AssetLibraryDisabledBackgroundColor");
        AssertContrast(Color(document, "AssetLibraryDisabledForegroundColor"), disabledBackground, 4.5, "disabled text");
        AssertContrast(Color(document, "AssetLibraryDisabledBorderColor"), disabledBackground, 3.0, "disabled outline");
        AssertContrast(focus, disabledBackground, 3.0, "focus ring / disabled surface");
        AssertContrast(Color(document, "AssetLibraryBorderColor"), Color(document, "AssetLibrarySecondaryNormalColor"), 3.0, "secondary outline");
        AssertContrast(Color(document, "AssetLibraryPaletteHoverBorderColor"), Color(document, "AssetLibrarySecondaryHoverColor"), 3.0, "tab hover outline");
        AssertContrast(Color(document, "AssetLibraryBorderColor"), Color(document, "AssetLibraryChipNormalColor"), 3.0, "chip outline");
        AssertContrast(Color(document, "AssetLibraryActiveBorderColor"), Color(document, "AssetLibraryChipActiveNormalColor"), 3.0, "active chip outline");
        AssertContrast(Color(document, "AssetLibraryPaletteBorderColor"), contentSurface, 3.0, "palette outline / dark content surface");
        AssertContrast(Color(document, "AssetLibraryPaletteFocusInnerColor"), Rgb.Parse("#FFFFFF"), 3.0, "palette inner focus / light swatch");
        AssertContrast(focus, Rgb.Parse("#000000"), 3.0, "palette outer focus / black swatch");
    }

    [TestMethod]
    public void UserFacingActionsRemainMappedToTheirAuditedRoles()
    {
        var document = LoadPage();
        AssertContentRole(document, "导入引用", "AssetLibraryPrimaryButton", 2);
        AssertContentRole(document, "重试", "AssetLibraryPrimaryButton", 1);
        AssertContentRole(document, "已分析", "AssetLibraryCompactChipButton", 1);
        AssertContentRole(document, "低饱和", "AssetLibraryCompactChipButton", 1);
        AssertContentRole(document, "查颜色", "AssetLibrarySecondaryButton", 1);
        AssertContentRole(document, "开始", "AssetLibraryPrimaryButton", 1);
        AssertContentRole(document, "取消", "AssetLibrarySecondaryButton", 1);

        AssertAutomationRole(document, "ToggleAssetOrganizationPane", "AssetLibrarySecondaryButton");
        AssertAutomationRole(document, "ToggleAssetInspectorPane", "AssetLibrarySecondaryButton");
        AssertAutomationRole(document, "PinAssetInspectorPane", "AssetLibrarySecondaryButton");
        AssertAutomationRole(document, "SaveVisualSmartFolder", "AssetLibraryPrimaryButton");
    }

    [TestMethod]
    public void VisualAnalysisTabsUseExplicitReadableLocalChrome()
    {
        var document = LoadPage();
        var tabs = document.Descendants(Presentation + "TabItem").ToArray();

        Assert.HasCount(3, tabs);
        CollectionAssert.AreEquivalent(new[] { "配色", "直方图", "影调" },
            tabs.Select(tab => Attribute(tab, "Header")).ToArray());
        CollectionAssert.AreEquivalent(new[] { "VisualPaletteTab", "VisualHistogramTab", "VisualToneTab" },
            tabs.Select(tab => Attribute(tab, "AutomationProperties.AutomationId")).ToArray());
        Assert.IsTrue(tabs.All(tab =>
            Attribute(tab, "Style") == "{StaticResource AssetLibraryVisualTabItem}"));

        var style = Style(document, "AssetLibraryVisualTabItem");
        Assert.AreEqual(string.Empty, Attribute(style, "BasedOn"),
            "The local tab chrome must not inherit the global disabled Opacity trigger.");
        Assert.AreEqual("{StaticResource AssetLibrarySecondaryNormalBrush}", DirectSetterValue(style, "Background"));
        Assert.AreEqual("{StaticResource AssetLibrarySecondaryForegroundBrush}", DirectSetterValue(style, "Foreground"));
        Assert.AreEqual("{StaticResource AssetLibraryBorderBrush}", DirectSetterValue(style, "BorderBrush"));
        Assert.AreEqual("{x:Null}", DirectSetterValue(style, "FocusVisualStyle"));

        AssertTrigger(style, "IsMouseOver", "True", "AssetLibraryVisualTabItem");
        var selected = AssertTrigger(style, "IsSelected", "True", "AssetLibraryVisualTabItem");
        Assert.IsTrue(selected.Elements(Presentation + "Setter").Any(setter =>
            Attribute(setter, "Property") == "Background" &&
            Attribute(setter, "Value") == "{StaticResource AssetLibraryChipActiveNormalBrush}"));
        var disabled = AssertTrigger(style, "IsEnabled", "False", "AssetLibraryVisualTabItem");
        foreach (var property in new[] { "Background", "Foreground", "BorderBrush", "Cursor" })
            Assert.IsTrue(disabled.Elements(Presentation + "Setter").Any(setter => Attribute(setter, "Property") == property));

        Assert.IsTrue(style.Descendants(Presentation + "MultiTrigger").Any(trigger =>
        {
            var conditions = trigger.Descendants(Presentation + "Condition").ToArray();
            return conditions.Any(condition => Attribute(condition, "Property") == "IsSelected" && Attribute(condition, "Value") == "True") &&
                   conditions.Any(condition => Attribute(condition, "Property") == "IsMouseOver" && Attribute(condition, "Value") == "True");
        }));

        var template = Resource(document, "ControlTemplate", "AssetLibraryVisualTabItemTemplate");
        var focusRing = template.Descendants(Presentation + "Border")
            .Single(border => Attribute(border, "Name") == "FocusRing");
        Assert.AreEqual("Transparent", Attribute(focusRing, "BorderBrush"));
        var chrome = template.Descendants(Presentation + "Border")
            .Single(border => Attribute(border, "Name") == "Chrome");
        Assert.AreEqual("{TemplateBinding Background}", Attribute(chrome, "Background"));
        Assert.AreEqual("{TemplateBinding BorderBrush}", Attribute(chrome, "BorderBrush"));
        Assert.IsTrue(template.Descendants(Presentation + "ContentPresenter")
            .Any(presenter => Attribute(presenter, "ContentSource") == "Header"));
        Assert.IsTrue(template.Descendants(Presentation + "Trigger")
            .Any(trigger => Attribute(trigger, "Property") == "IsKeyboardFocused" && Attribute(trigger, "Value") == "True"));
        Assert.IsFalse(style.Descendants(Presentation + "Setter")
            .Any(setter => Attribute(setter, "Property") == "Opacity"));
    }

    private static void AssertContentRole(XDocument document, string content, string role, int count)
    {
        var matches = document.Descendants(Presentation + "Button")
            .Where(button => Attribute(button, "Content") == content)
            .ToArray();
        Assert.HasCount(count, matches, content);
        Assert.IsTrue(matches.All(button => Attribute(button, "Style") == $"{{StaticResource {role}}}"), content);
    }

    private static void AssertAutomationRole(XDocument document, string automationId, string role)
    {
        var button = document.Descendants(Presentation + "Button")
            .Single(element => Attribute(element, "AutomationProperties.AutomationId") == automationId);
        Assert.AreEqual($"{{StaticResource {role}}}", Attribute(button, "Style"), automationId);
    }

    private static void AssertContrastSet(XDocument document, string foregroundKey, double minimum, params string[] backgroundKeys)
    {
        var foreground = Color(document, foregroundKey);
        foreach (var backgroundKey in backgroundKeys)
            AssertContrast(foreground, Color(document, backgroundKey), minimum, $"{foregroundKey} / {backgroundKey}");
    }

    private static void AssertContrast(Rgb foreground, Rgb background, double minimum, string label)
    {
        var ratio = Contrast(foreground, background);
        Assert.IsGreaterThanOrEqualTo(minimum, ratio, $"{label} contrast is {ratio:F2}:1; required >= {minimum:F1}:1.");
    }

    private static double Contrast(Rgb first, Rgb second)
    {
        var lighter = Math.Max(first.RelativeLuminance, second.RelativeLuminance);
        var darker = Math.Min(first.RelativeLuminance, second.RelativeLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static Rgb Color(XDocument document, string key) =>
        Rgb.Parse(Resource(document, "Color", key).Value.Trim());

    private static XElement AssertTrigger(XElement style, string property, string value, string roleName)
    {
        var trigger = style.Descendants(Presentation + "Trigger")
            .SingleOrDefault(candidate => Attribute(candidate, "Property") == property && Attribute(candidate, "Value") == value);
        Assert.IsNotNull(trigger, $"{roleName} must declare {property}={value}.");
        return trigger;
    }

    private static string DirectSetterValue(XElement style, string property) =>
        style.Elements(Presentation + "Setter")
            .Single(setter => Attribute(setter, "Property") == property)
            .Attribute("Value")?.Value ?? string.Empty;

    private static XElement Style(XDocument document, string key) => Resource(document, "Style", key);

    private static XElement Resource(XDocument document, string localName, string key) =>
        document.Descendants(Presentation + localName)
            .Single(element => Attribute(element, "Key") == key);

    private static XDocument LoadPage() => XDocument.Load(PagePath, LoadOptions.SetLineInfo);

    private static string Describe(XElement button) =>
        Attribute(button, "AutomationProperties.AutomationId") is { Length: > 0 } automationId
            ? automationId
            : Attribute(button, "Content") is { Length: > 0 } content ? content : "templated button";

    private static string Attribute(XElement element, string name) =>
        name is "Key" or "Name"
            ? element.Attribute(Xaml + name)?.Value ?? string.Empty
            : element.Attribute(name)?.Value ?? string.Empty;

    private static string PagePath => Path.Combine(RepositoryRoot, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml");
    private static string DarkThemePath => Path.Combine(RepositoryRoot, "src", "RAWSelectionAssistant", "Resources", "DesignSystem", "Theme.Dark.xaml");

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "RAWSelectionAssistant.sln")))
                    return current.FullName;
                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Repository root was not found.");
        }
    }

    private readonly record struct Rgb(byte Red, byte Green, byte Blue)
    {
        public double RelativeLuminance =>
            (0.2126 * Linear(Red)) + (0.7152 * Linear(Green)) + (0.0722 * Linear(Blue));

        public static Rgb Parse(string value)
        {
            var hex = value.Trim().TrimStart('#');
            Assert.AreEqual(6, hex.Length, $"Expected #RRGGBB, actual '{value}'.");
            return new Rgb(
                byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        private static double Linear(byte component)
        {
            var srgb = component / 255d;
            return srgb <= 0.04045 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }
    }
}

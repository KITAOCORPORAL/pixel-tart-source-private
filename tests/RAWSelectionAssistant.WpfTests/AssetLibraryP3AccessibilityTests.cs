using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP3AccessibilityTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void ToggleComboAndPopupOwnTheirCompleteDarkVisualTrees()
    {
        var document = XDocument.Load(StylesPath);
        var toggleTemplate = Resource(document, "ControlTemplate", "AssetLibraryP3ToggleTemplate");
        var comboTemplate = Resource(document, "ControlTemplate", "AssetLibraryP3ComboBoxTemplate");
        var itemTemplate = Resource(document, "ControlTemplate", "AssetLibraryP3ComboBoxItemTemplate");

        Assert.IsNotNull(toggleTemplate.Descendants(Presentation + "ContentPresenter").SingleOrDefault());
        Assert.IsNotNull(itemTemplate.Descendants(Presentation + "ContentPresenter").SingleOrDefault());

        var popup = comboTemplate.Descendants(Presentation + "Popup").Single();
        Assert.AreEqual("PART_Popup", Attribute(popup, "Name"));
        Assert.AreEqual("{TemplateBinding IsDropDownOpen}", Attribute(popup, "IsOpen"));

        var chrome = comboTemplate.Descendants(Presentation + "Border")
            .Single(element => Attribute(element, "Name") == "DropDownChrome");
        Assert.AreEqual("{TemplateBinding Background}", Attribute(chrome, "Background"));
        Assert.AreEqual("{TemplateBinding BorderBrush}", Attribute(chrome, "BorderBrush"));

        var scroll = comboTemplate.Descendants(Presentation + "ScrollViewer").Single();
        Assert.AreEqual("{TemplateBinding Background}", Attribute(scroll, "Background"));
        Assert.AreEqual("Auto", Attribute(scroll, "HorizontalScrollBarVisibility"));
        Assert.AreEqual("Auto", Attribute(scroll, "VerticalScrollBarVisibility"));

        AssertStyleUsesTemplate(document, "AssetLibraryP3Toggle", "AssetLibraryP3ToggleTemplate");
        AssertStyleUsesTemplate(document, "AssetLibraryP3ComboBox", "AssetLibraryP3ComboBoxTemplate");
        AssertStyleUsesTemplate(document, "AssetLibraryP3ComboBoxItem", "AssetLibraryP3ComboBoxItemTemplate");
    }

    [TestMethod]
    public void HighContrastTracksWindowsAndUsesOnlyDynamicSystemBrushes()
    {
        var source = File.ReadAllText(StylesPath);
        Assert.IsGreaterThanOrEqualTo(3, Count(source, "{DynamicResource {x:Static SystemParameters.HighContrastKey}}"));
        foreach (var key in new[]
                 {
                     "SystemColors.WindowBrushKey", "SystemColors.WindowTextBrushKey",
                     "SystemColors.ControlBrushKey", "SystemColors.ControlTextBrushKey",
                     "SystemColors.HighlightBrushKey", "SystemColors.HighlightTextBrushKey",
                     "SystemColors.GrayTextBrushKey"
                 })
        {
            StringAssert.Contains(source, $"{{DynamicResource {{x:Static {key}}}}}");
        }

        foreach (var state in new[]
                 {
                     "IsMouseOver", "IsPressed", "IsChecked", "IsDropDownOpen",
                     "IsSelected", "IsKeyboardFocused", "IsKeyboardFocusWithin", "IsEnabled"
                 })
        {
            StringAssert.Contains(source, state);
        }
    }

    [TestMethod]
    public void NarrowAndScaledLayoutsHaveNoLargeFixedMinimumAndPopupIsScrollBounded()
    {
        var document = XDocument.Load(StylesPath);
        var fixedMinimums = document.Descendants()
            .SelectMany(element => element.Attributes()
                .Where(attribute => attribute.Name.LocalName == "MinWidth")
                .Select(attribute => (Owner: element.Name.LocalName, Value: attribute.Value)))
            .Concat(document.Descendants(Presentation + "Setter")
                .Where(setter => Attribute(setter, "Property") == "MinWidth")
                .Select(setter => (Owner: "Setter", Value: Attribute(setter, "Value"))))
            .Where(item => double.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(item => (item.Owner, Value: double.Parse(item.Value, CultureInfo.InvariantCulture)))
            .ToArray();

        foreach (var minimum in fixedMinimums)
            Assert.IsLessThanOrEqualTo(64d, minimum.Value, $"{minimum.Owner} has a large fixed MinWidth.");

        var comboStyle = Resource(document, "Style", "AssetLibraryP3ComboBox");
        var maxDropDownHeight = comboStyle.Elements(Presentation + "Setter")
            .Single(setter => Attribute(setter, "Property") == "MaxDropDownHeight");
        Assert.IsLessThanOrEqualTo(400d, double.Parse(Attribute(maxDropDownHeight, "Value"), CultureInfo.InvariantCulture));

        var popupChrome = Resource(document, "ControlTemplate", "AssetLibraryP3ComboBoxTemplate")
            .Descendants(Presentation + "Border")
            .Single(element => Attribute(element, "Name") == "DropDownChrome");
        StringAssert.StartsWith(Attribute(popupChrome, "MinWidth"), "{Binding ActualWidth");
        Assert.IsLessThanOrEqualTo(640d, double.Parse(Attribute(popupChrome, "MaxWidth"), CultureInfo.InvariantCulture));

        var datePickerStyle = Resource(document, "Style", "AssetLibraryP3DatePicker");
        Assert.AreEqual(0, datePickerStyle.Elements(Presentation + "Setter")
            .Count(setter => Attribute(setter, "Property") == "Template"),
            "The field DatePicker must keep its tested product template.");
    }

    [TestMethod]
    public void DarkStatePairsKeepReadableTextContrast()
    {
        foreach (var pair in new[]
                 {
                     (Foreground: "#EAF1F3", Background: "#172027", State: "toggle normal"),
                     (Foreground: "#EAF1F3", Background: "#22313A", State: "toggle hover"),
                     (Foreground: "#F5FFFC", Background: "#0F171D", State: "toggle pressed"),
                     (Foreground: "#F5FFFC", Background: "#114238", State: "toggle checked"),
                     (Foreground: "#F2F6F7", Background: "#151D23", State: "combo normal"),
                     (Foreground: "#F2F6F7", Background: "#1C2830", State: "combo hover"),
                     (Foreground: "#F2F6F7", Background: "#111820", State: "combo opened"),
                     (Foreground: "#ADB8C0", Background: "#252C32", State: "disabled"),
                     (Foreground: "#FFCBC5", Background: "#352024", State: "error")
                 })
        {
            Assert.IsGreaterThanOrEqualTo(4.5d, Contrast(pair.Foreground, pair.Background), pair.State);
        }
    }

    private static XElement Resource(XDocument document, string localName, string key) =>
        document.Root!.Elements(Presentation + localName)
            .Single(element => element.Attribute(Xaml + "Key")?.Value == key);

    private static void AssertStyleUsesTemplate(XDocument document, string styleKey, string templateKey)
    {
        var style = Resource(document, "Style", styleKey);
        var template = style.Elements(Presentation + "Setter")
            .Single(setter => Attribute(setter, "Property") == "Template");
        Assert.AreEqual($"{{StaticResource {templateKey}}}", Attribute(template, "Value"));
    }

    private static string Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value ?? string.Empty;

    private static int Count(string source, string value)
    {
        var count = 0;
        for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
    }

    private static double Contrast(string foreground, string background)
    {
        static double Luminance(string value)
        {
            var rgb = new[] { value[1..3], value[3..5], value[5..7] }
                .Select(component => Convert.ToInt32(component, 16) / 255d)
                .Select(component => component <= .04045d
                    ? component / 12.92d
                    : Math.Pow((component + .055d) / 1.055d, 2.4d))
                .ToArray();
            return .2126d * rgb[0] + .7152d * rgb[1] + .0722d * rgb[2];
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + .05d) / (Math.Min(first, second) + .05d);
    }

    private static string StylesPath => FindRepositoryFile(
        "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryP3Styles.xaml");

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

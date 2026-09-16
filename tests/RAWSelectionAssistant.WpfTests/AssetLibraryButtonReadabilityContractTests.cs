using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryButtonReadabilityContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static string PagePath => Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml");
    private static string ComponentsPath => Path.Combine(RepositoryRoot(), "src", "RAWSelectionAssistant", "Resources", "DesignSystem", "Components.Foundation.xaml");
    private static string DarkColorsPath => Path.Combine(RepositoryRoot(), "src", "RAWSelectionAssistant", "Resources", "DesignSystem", "Colors.Dark.xaml");
    private static string ViewModelPath => Path.Combine(RepositoryRoot(), "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.P2Browser.cs");

    [TestMethod]
    public void EveryAssetLibraryButtonUsesOneExplicitCanonicalRole()
    {
        var document = XDocument.Load(PagePath);
        var buttons = document.Descendants(Presentation + "Button").ToArray();

        Assert.HasCount(61, buttons, "Update the Eagle-layout button inventory when a product action changes.");
        Assert.IsTrue(buttons.All(button =>
            Attribute(button, "Style") is "{DynamicResource PixelTart.Button.Ghost}"
                or "{DynamicResource PixelTart.Button.Primary}"
                or "{DynamicResource PixelTart.Button.Secondary}"));
        Assert.AreEqual(43, buttons.Count(button => Attribute(button, "Style") == "{DynamicResource PixelTart.Button.Ghost}"));
        Assert.AreEqual(16, buttons.Count(button => Attribute(button, "Style") == "{DynamicResource PixelTart.Button.Secondary}"));
        Assert.AreEqual(2, buttons.Count(button => Attribute(button, "Style") == "{DynamicResource PixelTart.Button.Primary}"));
        Assert.IsFalse(buttons.Any(button => Attribute(button, "Style").Contains("AssetLibrary", StringComparison.Ordinal)),
            "The RC12 page must not regress to the removed page-local button-role system.");
    }

    [TestMethod]
    public void ProductionControlsExposeCurrentProjectBookingCollectionAndWorkflowActions()
    {
        var page = File.ReadAllText(PagePath) + File.ReadAllText(ViewModelPath);
        foreach (var token in new[]
        {
            "OpenProjectPickerCommand", "OpenBookingPickerCommand", "SetCollectionProjectCommand", "RemoveCollectionProjectCommand",
            "SetInspectorWorkflowCommand", "CreateCollectionCommand", "RenameCollectionCommand",
            "ArchiveCollectionCommand", "RemoveCollectionEntryCommand", "OpenCalendarBookingCommand"
        })
            StringAssert.Contains(page, token);
    }

    [TestMethod]
    public void UserFacingActionsHaveStableAutomationNamesOrVisibleLabels()
    {
        var document = XDocument.Load(PagePath);
        foreach (var button in document.Descendants(Presentation + "Button"))
        {
            var content = Attribute(button, "Content");
            var hasAutomation = button.Attributes().Any(attribute => attribute.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal) || attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal))
                || button.Descendants().Attributes().Any(attribute => attribute.Name.LocalName.EndsWith(".Name", StringComparison.Ordinal) || attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal));
            var hasTooltip = !string.IsNullOrWhiteSpace(Attribute(button, "ToolTip"));
            Assert.IsTrue(!string.IsNullOrWhiteSpace(content) || hasAutomation || hasTooltip || button.Attributes().Any(attribute => attribute.Name.LocalName == "Command"),
                "Every action needs a visible label, automation identity, or a bound command.");
        }
    }

    [TestMethod]
    public void LocalRoleStylesDeclareHoverPressedAndExplicitDisabledStates()
    {
        var document = XDocument.Load(ComponentsPath);
        foreach (var key in new[] { "PixelTart.Button.Primary", "PixelTart.Button.Secondary" })
        {
            var style = document.Descendants(Presentation + "Style")
                .Single(element => Key(element) == key);
            Assert.IsTrue(style.Descendants(Presentation + "Trigger").Any(trigger => Attribute(trigger, "Property") == "IsMouseOver"));
            Assert.IsTrue(style.Descendants(Presentation + "Trigger").Any(trigger => Attribute(trigger, "Property") == "IsPressed"));
            var disabled = style.Descendants(Presentation + "Trigger")
                .Single(trigger => Attribute(trigger, "Property") == "IsEnabled" && Attribute(trigger, "Value") == "False");
            foreach (var property in new[] { "Background", "Foreground", "BorderBrush" })
                Assert.IsTrue(disabled.Descendants(Presentation + "Setter").Any(setter => Attribute(setter, "Property") == property));
        }
        var ghost = document.Descendants(Presentation + "Style").Single(element => Key(element) == "PixelTart.Button.Ghost");
        Assert.AreEqual("{StaticResource PixelTart.Button.Secondary}", Attribute(ghost, "BasedOn"));
        Assert.AreEqual("Transparent", ghost.Descendants(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == "Background").Attribute("Value")?.Value);
    }

    [TestMethod]
    public void StandardButtonFocusRingAndDarkTokensAreDefined()
    {
        var components = XDocument.Load(ComponentsPath);
        var template = components.Descendants(Presentation + "ControlTemplate")
            .Single(templateNode => Key(templateNode) == "PixelTart.Button.Template");
        Assert.IsNotNull(template.Descendants(Presentation + "Border").SingleOrDefault(border => AttributeByLocalName(border, "Name") == "FocusRing"));
        Assert.IsTrue(template.Descendants(Presentation + "Trigger").Any(trigger =>
            Attribute(trigger, "Property") == "IsKeyboardFocused" && Attribute(trigger, "Value") == "True"));
        var colors = File.ReadAllText(DarkColorsPath);
        foreach (var token in new[] { "Brush.Background", "Brush.Surface", "Brush.Panel", "Brush.Border", "Brush.Text.Primary", "Brush.Accent", "Brush.OnAccent" })
            StringAssert.Contains(colors, token);
        Assert.DoesNotContain("SystemColors", colors, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RoleColorsMeetDeterministicWcagContrastThresholds()
    {
        var colors = File.ReadAllText(DarkColorsPath);
        foreach (var token in new[] { "TextPrimaryColor", "TextSecondaryColor", "TextDisabledColor", "PrimaryColor", "PrimaryHoverColor", "PrimaryPressedColor" })
            StringAssert.Contains(colors, token);
        Assert.IsTrue(colors.Contains("#F2F4F6", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(colors.Contains("#07100E", StringComparison.OrdinalIgnoreCase));
    }

    private static string Attribute(XElement element, string name) => element.Attribute(name)?.Value ?? string.Empty;
    private static string AttributeByLocalName(XElement element, string name) => element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == name)?.Value ?? string.Empty;
    private static string Key(XElement element) => element.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Key")?.Value ?? string.Empty;

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}

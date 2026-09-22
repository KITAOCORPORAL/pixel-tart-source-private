using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StudioDesignSystemContractTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }


    private static IEnumerable<string> Views() => Directory.GetFiles(Path.Combine(Root(), "src/RAWSelectionAssistant/Views"), "*.xaml")
        .Append(Path.Combine(Root(), "src/RAWSelectionAssistant/MainWindow.xaml"));

    [TestMethod]
    public void FormalButtons_HaveClassifiedStudioStyle()
    {
        var known = new HashSet<string> { "GhostButton", "SecondaryButton", "PrimaryButton", "DangerButton",
            "Av2SecondaryButton", "Av2PrimaryButton", "Av2GhostButton", "Av2IconButton", "ToolEntryButton",
            "PixelTart.WorkflowStep", "SidebarNavButton", "QuickActionButton", "IconButton", "LocalSplitHeroButton",
            "PixelTart.Button.Ghost", "PixelTart.Button.Secondary", "PixelTart.Button.Primary", "PixelTart.Button.Icon",
            "ToolCatalogCard", "SidebarBottomButton" };
        foreach (var file in Views())
        foreach (var button in XDocument.Load(file).Descendants().Where(x => x.Name.LocalName == "Button"))
        {
            var value = (string?)button.Attribute("Style");
            value ??= (string?)button.Elements().FirstOrDefault(x => x.Name.LocalName == "Button.Style")?.Elements().FirstOrDefault()?.Attribute("BasedOn");
            if (value is null) continue; // Implicit application Button is the dark RoundedButtonTemplate, checked below.
            var key = value.Replace("{StaticResource ", "").TrimEnd('}');
            Assert.Contains(key, known, Path.GetFileName(file) + ": unknown button style " + value);
        }
        var implicitButtons = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Buttons.xaml"));
        StringAssert.Contains(implicitButtons, "RoundedButtonTemplate");
        StringAssert.Contains(implicitButtons, "IsKeyboardFocused");
        StringAssert.Contains(implicitButtons, "IsEnabled");
    }

    [TestMethod]
    public void FormalViews_NoUnclassifiedUnicodeActionIcons()
    {
        var symbols = new HashSet<string> { "＋", "+", "•••", "…", "↻", "›", "→", "←", "×", "↑", "↓", "−", "-" };
        foreach (var file in Views())
        foreach (var node in XDocument.Load(file).Descendants().Where(x => x.Name.LocalName is "Button" or "MenuItem"))
        foreach (var attribute in node.Attributes().Where(x => x.Name.LocalName is "Content" or "Header"))
        {
            // Explicit locked-baseline exception: Reference multi-source ordering, not a new rollout control.
            if (Path.GetFileName(file) == "ReferenceColorWorkspaceView.xaml" && attribute.Value is "↑" or "↓") continue;
            Assert.DoesNotContain(attribute.Value, symbols, Path.GetFileName(file) + ": " + attribute);
        }
    }

    [TestMethod]
    public void PopupAndScrollResources_RemainDarkAndTemplated()
    {
        var resources = Path.Combine(Root(), "src/RAWSelectionAssistant/Resources/DesignSystem");
        foreach (var file in new[] { "Controls.Menu.xaml", "Controls.Inputs.xaml", "Controls.Tables.xaml", "Tooltip.xaml" })
        {
            var text = File.ReadAllText(Path.Combine(resources, file));
            StringAssert.Contains(text, "ControlTemplate");
            Assert.IsFalse(text.Contains("Background=\"White\"", StringComparison.OrdinalIgnoreCase), file);
        }
        var app = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/App.xaml"));
        StringAssert.Contains(app, "ScrollBars.xaml");
        StringAssert.Contains(app, "Studio.Controls.xaml");
    }
    [TestMethod]
    public void FormalViewTypography_UsesNamedTokens()
    {
        var root = Root();
        var files = Directory.GetFiles(Path.Combine(root, "src/RAWSelectionAssistant/Views"), "*.xaml")
            .Append(Path.Combine(root, "src/RAWSelectionAssistant/MainWindow.xaml"));
        var violations = new List<string>();
        foreach (var file in files)
        foreach (var attribute in XDocument.Load(file).Descendants().Attributes("FontSize"))
            if (double.TryParse(attribute.Value, out _)) violations.Add(Path.GetFileName(file) + ": " + attribute.Parent?.Name.LocalName + " " + attribute.Value);
        Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void FormalDataGrids_UseStudioContract()
    {
        var root = Root();
        var files = Directory.GetFiles(Path.Combine(root, "src/RAWSelectionAssistant/Views"), "*.xaml")
            .Append(Path.Combine(root, "src/RAWSelectionAssistant/MainWindow.xaml"));
        foreach (var file in files)
        foreach (var grid in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "DataGrid"))
            Assert.AreEqual("{StaticResource PixelTart.DataGrid}", (string?)grid.Attribute("Style"), Path.GetFileName(file));
    }

    [TestMethod]
    public void NewRolloutResources_KeepEmeraldAndCloseBaselineUntouched()
    {
        var root = Root();
        var rollout = XDocument.Load(Path.Combine(root, "src/RAWSelectionAssistant/Resources/DesignSystem/Studio.Rollout.xaml"));
        foreach (var attribute in rollout.Descendants().Attributes("Background"))
            Assert.IsFalse(new[] { "White", "#FFF", "#FFFFFF", "Blue" }.Contains(attribute.Value, StringComparer.OrdinalIgnoreCase));
        var shell = File.ReadAllText(Path.Combine(root, "src/RAWSelectionAssistant/MainWindow.xaml"));
        StringAssert.Contains(shell, "ShellSurfaceCloseReservedWidth");
        StringAssert.Contains(shell, ">56<");
    }
}

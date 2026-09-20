using System.IO;
using System.Text.RegularExpressions;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class StageV2StartupCompatibilityTests
{
    [TestMethod]
    public void PlanningFilterToggleStyleTargetTypeTests()
    {
        var xaml = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(xaml, "x:Key=\"DocumentListItem\"");
        StringAssert.Contains(xaml, "TargetType=\"ListBoxItem\"");
        Assert.IsFalse(Regex.IsMatch(xaml, "<ToggleButton[^>]*Style=\"\\{StaticResource (?:GhostButton|PrimaryButton|SecondaryButton)", RegexOptions.Singleline));
    }

    [TestMethod]
    public void AllButtonStylesMatchTargetControlTypeTests()
    {
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.xaml", SearchOption.AllDirectories))
        {
            var xaml = File.ReadAllText(path);
            Assert.IsFalse(Regex.IsMatch(xaml, "<ToggleButton[^>]*Style=\"\\{StaticResource (?:GhostButton|PrimaryButton|SecondaryButton)", RegexOptions.Singleline), path);
            Assert.IsFalse(Regex.IsMatch(xaml, "<RadioButton[^>]*Style=\"\\{StaticResource (?:GhostButton|PrimaryButton|SecondaryButton)", RegexOptions.Singleline), path);
            Assert.IsFalse(Regex.IsMatch(xaml, "<CheckBox[^>]*Style=\"\\{StaticResource (?:GhostButton|PrimaryButton|SecondaryButton)", RegexOptions.Singleline), path);
        }
    }

    [TestMethod]
    public void MainWindowCanLoadPlanningResourcesTests()
    {
        var planning = Read("src/RAWSelectionAssistant/Views/PlanningCenterView.xaml");
        StringAssert.Contains(planning, "DocumentListItem");
        StringAssert.Contains(planning, "SurfaceSecondaryBrush");
        StringAssert.Contains(planning, "AccentBrush");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"), "参考模式");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

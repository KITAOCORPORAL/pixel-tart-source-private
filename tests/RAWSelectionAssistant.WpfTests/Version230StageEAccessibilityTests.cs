using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230StageEAccessibilityTests
{
    [TestMethod]
    public void TetherWorkspace_AllInteractiveControlsHaveAccessibleNames()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot(), "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"));
        AssertInteractiveControlsNamed(document, "联机拍摄现场监看工作区");
    }

    [TestMethod]
    public void ClientMonitor_AllInteractiveControlsHaveAccessibleNames()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot(), "src/RAWSelectionAssistant/Views/ClientMonitorWindow.xaml"));
        AssertInteractiveControlsNamed(document, "第二显示器客户监看窗口");
    }

    [TestMethod]
    public void TetherKeyboardContract_CoversNavigationRatingLockLutAndFullscreen()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml.cs"));
        foreach (var token in new[] { "Key.Left", "Key.Right", "Key.D0", "Key.D5", "Key.L", "Key.K", "Key.C", "Key.F11", "Key.Escape" })
            StringAssert.Contains(source, token);
    }

    [TestMethod]
    public void ClientMonitor_PrivacyAndFailureStatesUseReadableText()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src/RAWSelectionAssistant/Views/ClientMonitorWindow.xaml"));
        var tether = File.ReadAllText(Path.Combine(RepoRoot(), "src/RAWSelectionAssistant/Views/TetherCaptureView.xaml"));
        StringAssert.Contains(xaml, "默认隐藏文件名、路径和私人备注");
        StringAssert.Contains(xaml, "StatusText");
        StringAssert.Contains(tether, "ClientMonitorStatus");
        StringAssert.Contains(tether, "不删除、不移动、不进入回收站");
    }

    private static void AssertInteractiveControlsNamed(XDocument document, string expectedRootName)
    {
        var root = document.Root ?? throw new InvalidDataException("XAML root is missing.");
        Assert.AreEqual(expectedRootName, root.Attribute("AutomationProperties.Name")?.Value);
        var interactiveNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Button", "CheckBox", "ComboBox", "Slider", "TextBox"
        };
        var unnamed = document.Descendants()
            .Where(element => interactiveNames.Contains(element.Name.LocalName))
            .Where(element => string.IsNullOrWhiteSpace(element.Attribute("AutomationProperties.Name")?.Value))
            .Select(element => $"{element.Name.LocalName}:{element.Attribute("Content")?.Value ?? element.Attribute("Command")?.Value ?? "unnamed"}")
            .ToArray();
        Assert.HasCount(0, unnamed, string.Join(Environment.NewLine, unnamed));
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class SingleCloseAuthorityTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestMethod]
    public void WorkspaceSurfaces_HaveNoLocalCloseAndUseSingleShellAuthority()
    {
        var main = ReadDocument("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.AreEqual(1, Named(main, "ShellSurfaceCloseButton").Count(),
            "The application shell must declare exactly one full-page close authority.");

        var fullPages = new Dictionary<string, XDocument>(StringComparer.Ordinal)
        {
            ["Collage"] = ReadDocument("src/RAWSelectionAssistant/Views/CollageView.xaml"),
            ["Organize"] = ReadDocument("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml"),
            ["OnlineSelection"] = ReadDocument("src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml"),
            ["Finance"] = ReadDocument("src/RAWSelectionAssistant/Views/FinanceView.xaml")
        };

        AssertNoLocalClose(fullPages["Collage"], "Collage");
        AssertNoLocalClose(fullPages["Organize"], "Organize");
        Assert.AreEqual(1, SurfaceCloseButtons(fullPages["OnlineSelection"]).Count(),
            "OnlineSelection may retain only its create-modal header X.");
        Assert.IsTrue(SurfaceCloseButtons(fullPages["OnlineSelection"]).Single().Ancestors()
            .Any(element => element.Name.LocalName == "Border" &&
                            element.Descendants().Any(child => child.Name.LocalName == "DatePicker")),
            "OnlineSelection close must remain inside its create modal.");
        Assert.AreEqual(1, SurfaceCloseButtons(fullPages["Finance"]).Count(),
            "Finance may retain only its editor-drawer header X.");
        Assert.IsTrue(SurfaceCloseButtons(fullPages["Finance"]).Single().Ancestors()
            .Any(element => element.Name.LocalName == "Border" &&
                            element.Descendants().Any(child => child.Name.LocalName == "DatePicker")),
            "Finance close must remain inside its editor drawer.");

        foreach (var surfaceName in new[] { "WorkflowWorkspace", "ToolboxFullPage", "LocalSplitWorkspace" })
            Assert.AreEqual(0, SurfaceCloseButtons(Named(main, surfaceName).Single()).Count(),
                $"{surfaceName} is full-page and must not draw a second X.");
    }

    [TestMethod]
    public void RawBatchCollage_HaveExactlyOneVisibleCloseAuthorityByContract()
    {
        var main = ReadDocument("src/RAWSelectionAssistant/MainWindow.xaml");
        var shell = Named(main, "ShellSurfaceCloseButton").Single();
        StringAssert.Contains(Attribute(shell, "Style"), "ShellSurfaceCloseStyle");

        foreach (var relative in new[]
                 {
                     "src/RAWSelectionAssistant/Views/RawToJpegModal.xaml",
                     "src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml"
                 })
        {
            var document = ReadDocument(relative);
            var header = document.Descendants().Single(element => element.Name.LocalName == "SurfaceHeader");
            Assert.AreEqual("False", Attribute(header, "ShowCloseButton"),
                $"{relative} must hide its module header X and use ShellSurfaceCloseButton.");
        }

        AssertNoLocalClose(ReadDocument("src/RAWSelectionAssistant/Views/CollageView.xaml"), "Collage");
    }

    [TestMethod]
    public void ModalDrawerAndTutorial_HideShellWhileTheirSingleHeaderXIsVisible()
    {
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        foreach (var trigger in new[]
                 {
                     "{Binding IsOnboardingActive}",
                     "{Binding IsSettingsModalOpen}",
                     "{Binding TaskCenter.IsTaskDetailsOpen}",
                     "{Binding WorkCalendarPage.IsDetailsOpen}",
                     "{Binding OnlineSelectionPage.IsCreateModalOpen}",
                     "{Binding FinancePage.IsEditorOpen}",
                     "{Binding ElementName=BookingEditorOverlay, Path=Visibility}"
                 })
            StringAssert.Contains(main, trigger, $"Shell close must collapse for local close authority {trigger}.");

        var document = XDocument.Parse(main);
        var tutorial = Named(document, "TutorialOverlay").Single();
        Assert.AreEqual(1, SurfaceCloseButtons(tutorial).Count(), "Tutorial must show exactly one X.");
        Assert.AreEqual("TutorialCalloutCloseButton", Attribute(SurfaceCloseButtons(tutorial).Single(), "AutomationId"));
        Assert.AreEqual(1, tutorial.Descendants(Presentation + "Button")
            .Count(element => AutomationId(element) == "TutorialExitButton"),
            "Tutorial must retain one text exit button beside its single X.");
    }

    [TestMethod]
    public void SharedCloseHitTarget_IsAtLeast40By40Dip()
    {
        var document = ReadDocument("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml");
        var root = document.Root!;
        var button = root.Descendants(Presentation + "Button").Single();
        Assert.IsGreaterThanOrEqualTo(40, Parse(root, "MinWidth"));
        Assert.IsGreaterThanOrEqualTo(40, Parse(root, "MinHeight"));
        Assert.IsGreaterThanOrEqualTo(40, Parse(button, "MinWidth"));
        Assert.IsGreaterThanOrEqualTo(40, Parse(button, "MinHeight"));
        Assert.AreEqual("True", Attribute(root, "IsHitTestVisible"));
        Assert.AreEqual("True", Attribute(button, "IsHitTestVisible"));
    }

    [TestMethod]
    public void CurrentSurface_VisibleCloseButtonCountIsExactlyOne()
    {
        var main = ReadDocument("src/RAWSelectionAssistant/MainWindow.xaml");
        var raw = ReadDocument("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml");
        var batch = ReadDocument("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml");
        var collage = ReadDocument("src/RAWSelectionAssistant/Views/CollageView.xaml");
        var organize = ReadDocument("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml");
        var online = ReadDocument("src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml");
        var finance = ReadDocument("src/RAWSelectionAssistant/Views/FinanceView.xaml");
        var calendarDetails = ReadDocument("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml");
        var quickEditor = ReadDocument("src/RAWSelectionAssistant/Views/QuickBookingEditorView.xaml");
        var fullEditor = ReadDocument("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml");

        var surfaces = new (string Name, int ShellCount, int LocalCount)[]
        {
            ("RAW 转 JPG", 1, VisibleHeaderCloseCount(raw)),
            ("批量压缩", 1, VisibleHeaderCloseCount(batch)),
            ("拼图", 1, SurfaceCloseButtons(collage).Count()),
            ("整理图片", 1, SurfaceCloseButtons(organize).Count()),
            ("归片工作区", 1, SurfaceCloseButtons(Named(main, "WorkflowWorkspace").Single()).Count()),
            ("在线选片项目", 1, SurfaceCloseButtons(online)
                .Count(button => !button.Ancestors().Any(element => HasName(element, "OnlineSelectionCreateSurface")))),
            ("教程", 0, SurfaceCloseButtons(Named(main, "TutorialOverlay").Single()).Count()),
            ("设置", 0, SurfaceCloseButtons(Named(main, "SettingsModal").Single()).Count()),
            ("快速排期编辑器", 0, SurfaceCloseButtons(quickEditor).Count()),
            ("完整排期编辑器", 0, SurfaceCloseButtons(fullEditor).Count()),
            ("排期详情", 0, SurfaceCloseButtons(calendarDetails).Count()),
            ("在线选片创建", 0, SurfaceCloseButtons(online)
                .Count(button => button.Ancestors().Any(element => HasName(element, "OnlineSelectionCreateSurface")))),
            ("收支编辑器", 0, SurfaceCloseButtons(finance)
                .Count(button => button.Ancestors().Any(element => HasName(element, "FinanceEditorSurface")))),
            ("任务详情", 0, SurfaceCloseButtons(Named(main, "TaskDetailsSurface").Single()).Count())
        };

        foreach (var surface in surfaces)
        {
            var visibleCloseButtonCount = surface.ShellCount + surface.LocalCount;
            Assert.AreEqual(1, visibleCloseButtonCount, $"{surface.Name} must have exactly one visible X.");
            Assert.IsLessThanOrEqualTo(1, visibleCloseButtonCount, $"{surface.Name} cannot expose duplicate close authority.");
        }
    }

    private static void AssertNoLocalClose(XDocument document, string surface) =>
        Assert.AreEqual(0, SurfaceCloseButtons(document).Count(),
            $"{surface} must rely on ShellSurfaceCloseButton and cannot draw a local X.");

    private static int VisibleHeaderCloseCount(XDocument document) =>
        document.Descendants().Count(element => element.Name.LocalName == "SurfaceHeader" &&
                                                !string.Equals(Attribute(element, "ShowCloseButton"), "False", StringComparison.Ordinal));

    private static IReadOnlyList<XElement> SurfaceCloseButtons(XContainer source) =>
        source.Descendants().Where(element => element.Name.LocalName == "SurfaceCloseButton").ToArray();

    private static IEnumerable<XElement> Named(XContainer source, string name) =>
        source.Descendants().Where(element => HasName(element, name));

    private static bool HasName(XElement element, string name) => Attribute(element, "Name") == name;

    private static string Attribute(XElement element, string localName) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName)?.Value ?? string.Empty;

    private static string AutomationId(XElement element) =>
        element.Attributes().SingleOrDefault(attribute =>
            attribute.Name.LocalName == "AutomationId" ||
            attribute.Name.LocalName.EndsWith(".AutomationId", StringComparison.Ordinal))?.Value ?? string.Empty;

    private static double Parse(XElement element, string attribute) =>
        double.TryParse(Attribute(element, attribute), out var value) ? value : 0;

    private static XDocument ReadDocument(string relativePath) => XDocument.Parse(Read(relativePath));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}

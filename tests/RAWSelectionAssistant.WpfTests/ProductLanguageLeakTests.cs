using System.IO;
using System.Text.RegularExpressions;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed partial class ProductLanguageLeakTests
{
    private static readonly string[] VisibleAttributes =
    [
        "Text", "Content", "Header", "Title", "ToolTip", "Subtitle", "PlaceholderText",
        "AutomationProperties.Name", "AutomationProperties.HelpText"
    ];

    private static readonly string[] RuntimeMessageFiles =
    [
        "src/RAWSelectionAssistant/ViewModels/RawToJpegViewModel.cs",
        "src/RAWSelectionAssistant/ViewModels/BatchCompressionViewModel.cs",
        "src/RAWSelectionAssistant/ViewModels/ToolPageViewModels.cs",
        "src/RAWSelectionAssistant/ViewModels/TaskCenterViewModels.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P2Browser.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3SmartFolder.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3QueryComposer.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.P3TagManager.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryPage.Canvas.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.Canvas.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryViewModel.ContextualInspector.cs",
        "src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasView.cs",
        "src/PixelTart.Modules.AssetLibrary/FreeCanvas/FreeCanvasSurface.cs",
        "src/PixelTart.Modules.AssetLibrary/AssetLibraryWorkspaceHost.cs"
    ];

    [TestMethod]
    public void UserVisibleXamlLiterals_ContainNoInternalTerms()
    {
        var leaks = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.xaml", SearchOption.AllDirectories))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                foreach (Match match in VisibleAttributeRegex().Matches(line))
                {
                    var value = match.Groups["value"].Value;
                    if (value.StartsWith('{') || !ForbiddenTermRegex().IsMatch(value)) continue;
                    leaks.Add($"{Path.GetRelativePath(Root(), path)}:{lineNumber}: {match.Groups["name"].Value}=\"{value}\"");
                }
            }
        }

        Assert.IsEmpty(leaks, "Internal product-language leaks:\n" + string.Join('\n', leaks));
    }

    [TestMethod]
    public void RuntimeStatusAndErrorMessages_ContainNoPrimaryUiLeaks()
    {
        var leaks = new List<string>();
        foreach (var relative in RuntimeMessageFiles)
        {
            var path = Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));
            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                if (line.Contains("AutomationId", StringComparison.Ordinal) ||
                    line.Contains("TaskIdText", StringComparison.Ordinal) ||
                    line.Contains("DiagnosticText", StringComparison.Ordinal)) continue;
                foreach (Match literal in CSharpStringRegex().Matches(line))
                {
                    var value = literal.Value;
                    if (value.StartsWith("\"{Binding ", StringComparison.Ordinal)) continue; // Binding expressions are not user-visible literals.
                    if (value.Contains("booking.ProjectId", StringComparison.Ordinal) ||
                        value.Contains("P2QueryTotalCount", StringComparison.Ordinal)) continue;
                    if (ForbiddenRuntimeTermRegex().IsMatch(value))
                        leaks.Add($"{relative}:{lineNumber}: {literal.Value}");
                }
            }
        }

        Assert.IsEmpty(leaks, "Internal runtime-message leaks:\n" + string.Join('\n', leaks));
    }

    [TestMethod]
    public void TechnicalTaskId_RemainsOnlyInDiagnosticDetails()
    {
        var taskCenter = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        var viewModel = Text("src/RAWSelectionAssistant/ViewModels/TaskCenterViewModels.cs");
        Assert.DoesNotContain("Text=\"{Binding TaskIdText}\"", taskCenter);
        StringAssert.Contains(taskCenter, "Header=\"技术信息\"");
        StringAssert.Contains(taskCenter, "Text=\"{Binding DiagnosticText}\"");
        StringAssert.Contains(viewModel, "TaskIdText");
    }

    [GeneratedRegex("(?<name>Text|Content|Header|Title|ToolTip|Subtitle|PlaceholderText|AutomationProperties\\.Name|AutomationProperties\\.HelpText)=\"(?<value>[^\"]*)\"")]
    private static partial Regex VisibleAttributeRegex();

    [GeneratedRegex("(^|[^A-Za-z0-9])(P1|P2|P3)([^A-Za-z0-9]|$)|QueryOption|TaskId|LibraryId|AssetId|BookingId|ProjectId|LibRaw|SQLite|SHA-256|ContentHash|(^|[^A-Za-z])(True|False)([^A-Za-z]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenTermRegex();

    [GeneratedRegex("TaskId|LibraryId|AssetId|BookingId|ProjectId|LibRaw|SQLite|SHA-256|ContentHash|canonical|原位引用|托管副本|文件缺失：(True|False)", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenRuntimeTermRegex();

    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"")]
    private static partial Regex CSharpStringRegex();

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class GlobalSurfaceCloseSmokeTests
{
    [TestMethod]
    public void SharedEscapeHatch_IsVector40DipAndHasNoBusinessCommandDependency()
    {
        var view = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml");
        var code = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs");
        XDocument.Parse(view);

        ContainsAll(view, "Width=\"40\"", "Height=\"40\"", "<Path", "Width=\"16\"", "ToolTipText",
            "AutomationProperties.Name", "Click=\"CloseButton_Click\"");
        ContainsAll(code, "CloseRequestedEvent", "RoutingStrategy.Bubble", "RaiseEvent");
        var header = Read("src/RAWSelectionAssistant/Views/SurfaceHeader.xaml");
        XDocument.Parse(header);
        ContainsAll(header, "SurfaceCloseButton", "Title", "Subtitle", "CloseToolTip", "CloseAutomationName", "ShowCloseButton");

        var executableContract = view + WithoutLineComments(code);
        foreach (var forbidden in new[]
        {
            "Command=", "CanExecute", "IsBusy", "CanClose", "CanCancel", "CancelCommand",
            "CancellationToken", "Application.Current.Shutdown", "MainWindow.Close", "❌"
        })
            Assert.IsFalse(executableContract.Contains(forbidden, StringComparison.Ordinal),
                $"The shell escape hatch must not depend on {forbidden}.");
    }

    [TestMethod]
    public void ShellCloseAndEscape_UseOneNavigationOnlyPath()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        ContainsAll(window,
            "AddHandler(SurfaceCloseButton.CloseRequestedEvent",
            "CloseCurrentSurface_Click",
            "if (e.Key == Key.Escape)",
            "await RequestEscapeCloseAsync()",
            "ForceCloseCurrentSurface()");
        ContainsAll(window,
            "Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Left",
            "await RequestEscapeCloseAsync()");
        ContainsAll(viewModel,
            "ISurfaceNavigationHost SurfaceNavigationHost",
            "CloseCurrentSurfaceCommand",
            "public Task CloseCurrentSurfaceAsync()",
            "SurfaceNavigationHost.ReturnToOrigin()",
            "SurfaceNavigationHost.ReturnToWorkbench()");

        var closeMethod = Slice(viewModel, "public Task CloseCurrentSurfaceAsync()", "public void ReturnToOrigin()");
        foreach (var forbidden in new[]
        {
            "RawToJpegPage.CancelCommand", "BatchCompressionPage.CancelCommand", "CollagePage.CancelCommand",
            "OrganizePhotosPage.CancelCommand", "CancellationToken", "CancelCurrentOperation", "CloseRequested?.Invoke"
        })
            Assert.IsFalse(closeMethod.Contains(forbidden, StringComparison.Ordinal),
                $"Shell close must not invoke {forbidden}.");
    }

    [TestMethod]
    public void Escape_ClosesInputPopupBeforeClosingSurface()
    {
        var window = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var escapeBranch = Slice(window, "if (e.Key == Key.Escape)", "if (_viewModel?.IsOnboardingActive == true && e.Key == Key.Tab)");
        ContainsAll(escapeBranch, "TryCloseActiveInputPopup()", "e.Handled = true", "await RequestEscapeCloseAsync()");
        ContainsAll(window, "FindVisualChildren<ComboBox>(RootGrid)", "combo.IsDropDownOpen",
            "FindVisualChildren<DatePicker>(RootGrid)", "picker.IsDropDownOpen");
        Assert.IsLessThan(escapeBranch.IndexOf("await RequestEscapeCloseAsync()", StringComparison.Ordinal),
            escapeBranch.IndexOf("TryCloseActiveInputPopup()", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TutorialButtonTutorialXAndEscape_ConvergeOnSingleForceExitTutorial()
    {
        var mainView = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var mainCode = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");

        ContainsAll(mainView, "Content=\"退出教程\"", "Click=\"TutorialExitButton_Click\"",
            "AutomationProperties.AutomationId=\"TutorialExitButton\"", "AutomationId=\"TutorialCalloutCloseButton\"", "SurfaceCloseButton");
        ContainsAll(viewModel, "public void ForceExitTutorial()", "_onboardingService.DetachForExit()",
            "RestoreNormalWorkspace()", "ReturnToWorkbench()", "TutorialReportNames");
        ContainsAll(mainCode, "if (_viewModel.IsOnboardingActive)", "TutorialExitButton_Click", "ForceExitTutorial()");
        Assert.AreEqual(1, Count(viewModel, "void ForceExitTutorial("), "Only one tutorial-exit authority may exist.");
        Assert.IsFalse(viewModel.Contains("ExitTutorialAsync", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.Contains("TutorialExitCommand", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RawAndBatch_SeparateCloseSurfaceFromCancelTask()
    {
        foreach (var relative in new[]
        {
            "src/RAWSelectionAssistant/Views/RawToJpegModal.xaml",
            "src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml"
        })
        {
            var view = Read(relative);
            XDocument.Parse(view);
            ContainsAll(view, "SurfaceHeader", "Content=\"取消任务\"", "Command=\"{Binding CancelCommand}\"");
            Assert.IsFalse(SurfaceChromeMarkup(view).Contains("CancelCommand", StringComparison.Ordinal));
            Assert.IsFalse(SurfaceChromeMarkup(view).Contains("Command=", StringComparison.Ordinal));
        }

        var window = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var escape = Slice(window, "private async Task RequestEscapeCloseAsync()", "private async void CloseCurrentSurface_Click");
        foreach (var forbidden in new[] { "RawToJpegPage.CancelCommand", "BatchCompressionPage.CancelCommand" })
            Assert.IsFalse(escape.Contains(forbidden, StringComparison.Ordinal));
    }

    [TestMethod]
    public void RequiredSurfaceMatrix_ExposesShellOwnedClose()
    {
        var shell = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RawToJpeg"] = Read("src/RAWSelectionAssistant/Views/RawToJpegModal.xaml"),
            ["BatchCompress"] = Read("src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml"),
            ["Collage"] = shell + Read("src/RAWSelectionAssistant/Views/CollageView.xaml"),
            ["Organize"] = shell + Read("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml"),
            ["LocalSplitWizard"] = Read("src/RAWSelectionAssistant/MainWindow.xaml"),
            ["Tutorial"] = Read("src/RAWSelectionAssistant/MainWindow.xaml"),
            ["BookingQuickCreate"] = Read("src/RAWSelectionAssistant/Views/QuickBookingEditorView.xaml"),
            ["BookingQuickEdit"] = Read("src/RAWSelectionAssistant/Views/QuickBookingEditorView.xaml"),
            ["FullPlanner"] = Read("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml"),
            ["OnlineSelectionCreate"] = Read("src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml"),
            ["OnlineSelectionWorkspace"] = Read("src/RAWSelectionAssistant/MainWindow.xaml"),
            ["FinanceDrawer"] = Read("src/RAWSelectionAssistant/Views/FinanceView.xaml"),
            ["TaskDetail"] = Read("src/RAWSelectionAssistant/MainWindow.xaml"),
            ["Settings"] = Read("src/RAWSelectionAssistant/MainWindow.xaml"),
            ["ErrorDetail"] = Read("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml")
        };

        foreach (var (surface, source) in sources)
            Assert.IsTrue(HasCloseEscapeHatch(source), $"{surface} does not expose a shell-level/shared close escape hatch.");
    }

    [TestMethod]
    public void FailureAndBusyStates_CannotDisableSurfaceClose()
    {
        var shared = Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml") +
                     WithoutLineComments(Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs"));
        foreach (var forbidden in new[]
        {
            "IsEnabled=\"{Binding", "IsBusy", "HasValidationError", "HasTutorialError", "CurrentStepValid",
            "TaskState", "CanExecute", "CanClose", "CanCancel"
        })
            Assert.IsFalse(shared.Contains(forbidden, StringComparison.Ordinal));

        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        ContainsAll(main, "HasTutorialError", "SurfaceCloseButton");
    }

    [TestMethod]
    public void ShellCloseNeverClosesApplicationOrDeletesFiles()
    {
        var source = Read("src/RAWSelectionAssistant.Core/Services/SurfaceNavigationContracts.cs") +
                     Read("src/RAWSelectionAssistant/Views/SurfaceCloseButton.xaml.cs") +
                     Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var shellClose = Slice(source, "private async void CloseCurrentSurface_Click", "private async Task RequestModalActionAsync");
        foreach (var forbidden in new[]
        {
            "Application.Current.Shutdown", "Shutdown()", "MainWindow.Close", "File.Delete", "File.Move",
            "Directory.Delete", "UndoJournal", "CancelCurrentOperation"
        })
            Assert.IsFalse(shellClose.Contains(forbidden, StringComparison.Ordinal),
                $"Surface close must not execute {forbidden}.");
    }

    [TestMethod]
    public void SurfaceNavigationContract_DeclaresHistoryOriginAndFallbackOperations()
    {
        var source = Read("src/RAWSelectionAssistant.Core/Services/SurfaceNavigationContracts.cs");
        ContainsAll(source,
            "interface ISurfaceNavigationHost",
            "PreviousSurface", "CurrentSurface", "OriginSurface", "NavigationHistory",
            "CloseCurrentSurface()", "CloseCurrentSurfaceAsync()", "ReturnToOrigin()", "ReturnToWorkbench()",
            "WorkbenchSurface", "IsValid", "ResolveReturnTargetLocked");
    }

    [TestMethod]
    public void TaskDetailAndErrorDetail_HaveRealCloseRoutes()
    {
        var mainView = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var mainCode = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var taskCenter = Read("src/RAWSelectionAssistant/ViewModels/TaskCenterViewModels.cs");
        ContainsAll(mainView, "AutomationProperties.Name=\"任务详情\"", "AutomationName=\"关闭任务详情\"");
        ContainsAll(taskCenter, "IsTaskDetailsOpen", "CloseDetailsCommand", "CloseDetailsSurface()", "SelectedTask = null");
        ContainsAll(mainCode, "TaskCenter.IsTaskDetailsOpen", "TaskCenter.CloseDetailsSurface()");

        var dialogView = Read("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml");
        var dialogCode = Read("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml.cs");
        ContainsAll(dialogView, "SurfaceCloseButton", "关闭消息或错误详情", "CloseRequested=\"CloseSurfaceRequested\"");
        ContainsAll(dialogCode, "CloseSurfaceRequested", "DialogResult = _confirmation ? false : true", "Close()");
    }

    private static bool HasCloseEscapeHatch(string source) =>
        source.Contains("SurfaceCloseButton", StringComparison.Ordinal) ||
        source.Contains("SurfaceHeader", StringComparison.Ordinal) ||
        source.Contains("CloseButton_Click", StringComparison.Ordinal) ||
        source.Contains("关闭消息对话框", StringComparison.Ordinal);

    private static string SurfaceChromeMarkup(string source)
    {
        var start = source.IndexOf("<views:SurfaceCloseButton", StringComparison.Ordinal);
        if (start < 0) start = source.IndexOf("<views:SurfaceHeader", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        var end = source.IndexOf("/>", start, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, end);
        return source[start..(end + 2)];
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"Missing start marker: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, end, $"Missing end marker: {endMarker}");
        return source[start..end];
    }

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string WithoutLineComments(string source) => string.Join('\n',
        source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}

using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ModalCloseSmokeTests
{
    [TestMethod]
    [DataRow("RawToJpeg")]
    [DataRow("BatchCompress")]
    [DataRow("Collage")]
    [DataRow("PhotoGrouping")]
    public void EscapePageMatrix_UsesShellNavigationWithoutCancellingTasks(string page)
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(viewModel, $"\"{page}\"");
        StringAssert.Contains(source, "ForceCloseCurrentSurface()");
        Assert.DoesNotContain("RequestPageModalActionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RawToJpegPage.CancelCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BatchCompressionPage.CancelCommand", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void RawAndBatchModals_ExposeCancelAndBusySafeProgress()
    {
        foreach (var relative in new[]
        {
            "src/RAWSelectionAssistant/Views/RawToJpegModal.xaml",
            "src/RAWSelectionAssistant/Views/BatchCompressionModal.xaml"
        })
        {
            var source = Read(relative);
            XDocument.Parse(source);
            StringAssert.Contains(source, "Command=\"{Binding CancelCommand}\"");
            StringAssert.Contains(source, "Progress");
        }

        var viewModels = Read("src/RAWSelectionAssistant/ViewModels/ToolPageViewModels.cs") +
                         Read("src/RAWSelectionAssistant/ViewModels/RawToJpegViewModel.cs");
        StringAssert.Contains(viewModels, "IsBusy");
    }

    [TestMethod]
    [DataRow("QuickCreate")]
    [DataRow("QuickEdit")]
    [DataRow("FullPlanning")]
    public void BookingEditorVariants_KeepOneCancelContract(string presentation)
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs") +
                     Read("src/RAWSelectionAssistant/Views/ShootBookingEditorView.xaml");
        var marker = presentation switch
        {
            "QuickCreate" => "BookingEditorModalSurface",
            "QuickEdit" => "BookingEditorPresentation.QuickEdit",
            "FullPlanning" => "BookingEditorPresentation.FullPlanning",
            _ => presentation
        };
        StringAssert.Contains(source, marker);
        StringAssert.Contains(source, "CancelCommand");
        StringAssert.Contains(source, "BookingEditorOverlay");
    }

    [TestMethod]
    public void ToolboxAndOverflowPopups_CloseThroughEscBoundary()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml") +
                     Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        StringAssert.Contains(source, "WorkbenchToolboxPopup");
        StringAssert.Contains(source, "QuickToolsOverflowPopup");
        StringAssert.Contains(source, "RequestModalActionAsync");
        StringAssert.Contains(source, "Key.Escape");
    }

    [TestMethod]
    public void OnlineSelectionCreate_ProvidesCancelAndBusyGuard()
    {
        var view = Read("src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml");
        var viewModel = Read("src/RAWSelectionAssistant/ViewModels/OnlineSelectionViewModels.cs");
        StringAssert.Contains(view, "IsCreateModalOpen");
        StringAssert.Contains(view, "CancelCreateCommand");
        StringAssert.Contains(viewModel, "CancelCreateCommand");
        StringAssert.Contains(viewModel, "CreateAndImportCommand");
        StringAssert.Contains(viewModel, "_ => !IsBusy");
    }

    [TestMethod]
    public void TutorialOverlay_LeavesCardInteractiveAndExitAccessible()
    {
        var xaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var code = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs") +
                   Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(xaml, "x:Name=\"TutorialOverlay\"");
        StringAssert.Contains(xaml, "IsHitTestVisible=\"True\"");
        StringAssert.Contains(xaml, "Panel.ZIndex=\"2200\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=");
        StringAssert.Contains(code, "CloseCurrentSurfaceAsync");
        StringAssert.Contains(code, "ExitTutorialAsync");
    }

    [TestMethod]
    public void TutorialExit_AlwaysCancelsAndRestoresInFinally()
    {
        var source = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(source, "_operationCancellation?.Cancel()");
        StringAssert.Contains(source, "_onboardingService.DetachForExit()");
        StringAssert.Contains(source, "RestoreNormalWorkspace()");
        StringAssert.Contains(source, "finally");
        StringAssert.Contains(source, "_tutorialExitInProgress = false");
        StringAssert.Contains(source, "StatusMessage =");
    }

    [TestMethod]
    public void Step18ReportRecovery_UsesActualCsvJsonTxtFilesAndMissingList()
    {
        var source = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        StringAssert.Contains(source, "TutorialReportNames");
        StringAssert.Contains(source, ".csv");
        StringAssert.Contains(source, ".json");
        StringAssert.Contains(source, ".txt");
        StringAssert.Contains(source, "File.Exists(Path.Combine(reportRoot, name))");
        StringAssert.Contains(source, "TutorialMissingReports");
        StringAssert.Contains(source, "ExpectedReportCount");
        StringAssert.Contains(source, "GeneratedReportCount");
    }

    [TestMethod]
    public void TutorialStep18Failure_OffersRetryRecreateBackAndExitWithoutBusyDeadlock()
    {
        var xaml = Read("src/RAWSelectionAssistant/MainWindow.xaml");
        var source = Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        foreach (var token in new[] { "TutorialRetryCommand", "TutorialRecreateDataCommand", "TutorialBackCommand", "TutorialExitCommand" })
            StringAssert.Contains(xaml + source, token);
        StringAssert.Contains(source, "IsBusy = false");
        StringAssert.Contains(source, "_tutorialCancellationTask = null");
    }

    [TestMethod]
    public void ErrorDialog_ClosesWithEscapeAndSupportsOpenCancelAndBusyCompletionPaths()
    {
        var dialog = Read("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml.cs");
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        StringAssert.Contains(dialog, "e.Key == Key.Escape");
        StringAssert.Contains(dialog, "Close()");
        StringAssert.Contains(main, "await RequestEscapeCloseAsync()");
        StringAssert.Contains(Read("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "finally");
    }

    [TestMethod]
    public void ModalHostContract_ProvidesCloseCancelTokenAndIdempotentDismissal()
    {
        var source = Read("src/RAWSelectionAssistant.Core/Services/ModalInteractionContracts.cs");
        foreach (var token in new[] { "IModalSession", "CanClose", "CanCancel", "CancellationToken", "RequestCloseAsync", "RequestCancelAsync", "ModalHost" })
            StringAssert.Contains(source, token);
        StringAssert.Contains(source, "if (_request is not null)");
        StringAssert.Contains(source, "ReleaseIfClosed");
    }

    [TestMethod]
    public void ModalCloseSmokeMatrix_ListsAllRequiredSurfaces()
    {
        var source = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs") +
                     Read("src/RAWSelectionAssistant/MainWindow.xaml") +
                     Read("src/RAWSelectionAssistant/Views/OnlineSelectionView.xaml");
        foreach (var token in new[]
        {
            "RawToJpeg", "BatchCompress", "BookingEditorOverlay", "QuickBookingEditorHost",
            "BookingEditorPresentation.QuickEdit", "BookingEditorPresentation.FullPlanning",
            "WorkbenchToolboxPopup", "OnlineSelection", "IsCreateModalOpen", "TutorialOverlay", "ThemedMessageDialog"
        })
            StringAssert.Contains(source + Read("src/RAWSelectionAssistant/Views/ThemedMessageDialog.xaml"), token);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln was not found.");
    }
}

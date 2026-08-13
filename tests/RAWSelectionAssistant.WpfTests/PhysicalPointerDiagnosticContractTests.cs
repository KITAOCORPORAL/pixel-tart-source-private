using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class PhysicalPointerDiagnosticContractTests
{
    [TestMethod]
    public void MainWindow_CapturesNativeAndHandledWpfPointerLayers()
    {
        var host = Read("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs");
        ContainsAll(host,
            "Mouse.PreviewMouseDownEvent",
            "Mouse.PreviewMouseUpEvent",
            "UIElement.PreviewMouseLeftButtonDownEvent",
            "UIElement.PreviewMouseLeftButtonUpEvent",
            "Mouse.MouseUpEvent",
            "CompleteWpfMouseDispatch",
            "new MouseButtonEventHandler",
            "true);",
            "HwndSource",
            "AddHook",
            "WM_LBUTTONDOWN",
            "WM_LBUTTONUP",
            "WM_MOUSEMOVE",
            "ButtonBase.ClickEvent",
            "MissingAutomationId",
            "DuplicateCloseAuthorityBanner",
            "PhysicalPointerDiagnosticCopyButton");
        ContainsAll(Read("src/RAWSelectionAssistant/MainWindow.xaml.cs"),
            "CopyPhysicalPointerDiagnosticId_Click",
            "#if INPUT_ROUTING_DIAGNOSTICS",
            "Clipboard.SetText");
    }

    [TestMethod]
    public void Diagnostic_IsAcceptanceOnlyAndWritesFixedSanitizedArtifact()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        ContainsAll(diagnostics,
            "#if INPUT_ROUTING_DIAGNOSTICS",
            ".Acceptance",
            "AppDataPaths.Root",
            "InputDiagnostics",
            "physical-pointer-session.json",
            "PT-INPUT-",
            "UTF8Encoding Utf8WithoutBom = new(false)",
            "SafeToken",
            "WriteThrough");
        Assert.IsFalse(diagnostics.Contains("SpecialFolder.LocalApplicationData", StringComparison.Ordinal));
        Assert.IsFalse(diagnostics.Contains("customer", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(diagnostics.Contains("file_name", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(diagnostics.Contains("project_name", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(diagnostics.Contains("full_path", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(diagnostics.TrimEnd().EndsWith("#endif", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Diagnostic_SeparatesFourPhysicalPointerLayersAndEffectiveState()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        ContainsAll(diagnostics,
            "Layer1Win32",
            "Layer2Wpf",
            "Layer3Target",
            "Layer4Action",
            "LButtonDownReceived",
            "PreviewMouseDownReceived",
            "InputHitTest",
            "ButtonClickReceived",
            "ShellEscapeEntered",
            "TutorialOverlayDetached",
            "SurfaceClosed",
            "PhysicalTargetConfirmed",
            "Layer1Win32.LButtonUpReceived",
            "Layer2Wpf.PreviewMouseUpReceived",
            "IsCloseLike",
            "UncorrelatedActionEvents",
            "WpfWithoutWin32",
            "EffectiveIsEnabled",
            "IsHitTestVisible",
            "BlockingAncestor",
            "VisualParentChain",
            "args.ChangedButton != MouseButton.Left",
            "CurrentTutorialStep");
    }

    [TestMethod]
    public void ShellActions_AreCorrelatedWithoutTreatingUiaInvokeAsPhysicalInput()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        var input = Read("src/RAWSelectionAssistant/Services/InputRoutingDiagnostics.cs");
        var main = Read("src/RAWSelectionAssistant/MainWindow.xaml.cs");
        ContainsAll(diagnostics,
            "if (string.Equals(message, \"WM_LBUTTONDOWN\"",
            "_activeAttempt = CreateAttempt",
            "CanCorrelateWithActiveAttempt(requireWpfDown: true)",
            "TimeSpan.FromSeconds(3)");
        ContainsAll(input, "PhysicalPointerDiagnosticSession.RecordControlEvent", "PhysicalPointerDiagnosticSession.RecordShellEvent");
        ContainsAll(main, "TutorialOverlayDetached", "SurfaceCloseDispatchCompleted",
            "TutorialExitButton_Click", "RecordControlEvent(control, \"CloseClick\"");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        }
    }
}

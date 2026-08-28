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
            "UIElement.MouseLeftButtonDownEvent",
            "UIElement.PreviewMouseLeftButtonUpEvent",
            "UIElement.MouseLeftButtonUpEvent",
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
            "PhysicalPointerDiagnosticCopyButton",
            "Title = \"像素蛋挞 [Physical Pointer Diagnostic]\"",
            "Title = \"像素蛋挞 [Modular Harness Dev]\"",
            "#if MODULAR_HARNESS_DEV_PREVIEW");
        ContainsAll(Read("src/RAWSelectionAssistant/MainWindow.xaml.cs"),
            "CopyPhysicalPointerDiagnosticId_Click",
            "#if INPUT_ROUTING_DIAGNOSTICS",
            "Clipboard.SetText");
    }

    [TestMethod]
    public void Diagnostic_IsAcceptanceOrExplicitDevPreviewOnlyAndWritesFixedSanitizedArtifact()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        ContainsAll(diagnostics,
            "#if INPUT_ROUTING_DIAGNOSTICS",
            ".Acceptance",
            "#if MODULAR_HARNESS_DEV_PREVIEW",
            "#else",
            "return false;",
            "PixelTart_ModularHarness_V1_DevPreview",
            "PIXEL_TART_PHYSICAL_POINTER_DIAGNOSTICS",
            "Environment.GetEnvironmentVariable(DevPreviewOptInEnvironmentVariable)",
            "\"1\"",
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
        Assert.IsFalse(diagnostics.Contains("StartsWith(DevPreviewProcessName", StringComparison.Ordinal));
        Assert.IsFalse(diagnostics.Contains("EndsWith(DevPreviewProcessName", StringComparison.Ordinal));
        Assert.IsFalse(diagnostics.Contains("Contains(DevPreviewProcessName", StringComparison.Ordinal));
        Assert.IsTrue(diagnostics.TrimEnd().EndsWith("#endif", StringComparison.Ordinal));

        var project = Read("src/RAWSelectionAssistant/RAWSelectionAssistant.csproj");
        ContainsAll(project,
            "Condition=\"'$(InputRoutingDiagnostics)' == 'true'\"",
            ";INPUT_ROUTING_DIAGNOSTICS",
            "Condition=\"'$(ModularHarnessDevPreview)' == 'true'\"",
            ";MODULAR_HARNESS_DEV_PREVIEW");
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
        Assert.IsFalse(diagnostics.Contains("if (!IsCloseLike(button)) return;", StringComparison.Ordinal));
        ContainsAll(diagnostics,
            "MouseCapturedElement",
            "IsMouseCaptured",
            "IsMouseCaptureWithin",
            "IsPressed",
            "InstanceId",
            "DownTargetAutomationId",
            "UpTargetAutomationId",
            "ButtonInstanceSameDownUp",
            "ClickMode",
            "CommandCanExecute");
    }

    [TestMethod]
    public void Diagnostic_CorrelatesRealSplitterAndSliderStateTransitions()
    {
        var host = Read("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs");
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");

        ContainsAll(host,
            "Thumb.DragStartedEvent",
            "Thumb.DragCompletedEvent",
            "Keyboard.PreviewKeyDownEvent",
            "Keyboard.KeyDownEvent",
            "Keyboard.PreviewKeyUpEvent",
            "Keyboard.KeyUpEvent",
            "ComponentDispatcher.ThreadFilterMessage",
            "PhysicalPointer_ThreadFilterMessage",
            "WM_KEYDOWN",
            "WM_KEYUP",
            "message.hwnd != _physicalPointerHwndSource.Handle",
            "nativeMessageTime: message.time",
            "scanCode: (int)((nativeKeyData >> 16) & 0xff)",
            "repeatCount: (int)(nativeKeyData & 0xffff)",
            "modifiers: Keyboard.Modifiers",
            "AssetOrganizationSplitter",
            "OrganizationPaneWidth",
            "AssetInspectorSplitter",
            "InspectorPaneWidth",
            "AssetThumbnailSizeSlider",
            "ThumbnailWidth",
            "grid.ColumnDefinitions[0].ActualWidth",
            "inspectorGrid.ColumnDefinitions[4].ActualWidth",
            "slider.Value",
            "viewModel.OrganizationPaneWidth",
            "viewModel.InspectorPaneWidth",
            "viewModel.ThumbnailWidth",
            "viewModel.IsOrganizationPaneCollapsed",
            "viewModel.IsInspectorPaneCollapsed",
            "ExpectedKeyboardAdjustment",
            "FindAcceptanceKeyboardButtonTarget",
            "ToggleAssetOrganizationPane",
            "ToggleAssetInspectorPane",
            "(\"AssetOrganizationSplitter\", Key.Left) => \"Decrease\"",
            "(\"AssetOrganizationSplitter\", Key.Right) => \"Increase\"",
            "(\"AssetInspectorSplitter\", Key.Left) => \"Increase\"",
            "(\"AssetInspectorSplitter\", Key.Right) => \"Decrease\"",
            "(\"AssetThumbnailSizeSlider\", Key.Left) => \"Decrease\"",
            "(\"AssetThumbnailSizeSlider\", Key.Right) => \"Increase\"",
            "DispatcherPriority.ContextIdle",
            "BeginControlStateTransition",
            "CompleteControlStateTransition",
            "RecordWorkspaceRestoreState");
        ContainsAll(diagnostics,
            "RecordWin32Key",
            "RecordWpfKey",
            "IsAcceptanceKeyboardButtonAutomationId",
            "ToggleAssetOrganizationPane",
            "ToggleAssetInspectorPane",
            "KeyAttempts",
            "ControlStateTransitions",
            "ScanCode",
            "RepeatCount",
            "Modifiers",
            "NativeMessageTime",
            "IsExtendedKey",
            "WasPreviouslyDown",
            "IsTransitionUp",
            "PreviewKeyDownReceived",
            "KeyDownReceived",
            "PreviewKeyUpReceived",
            "KeyUpReceived",
            "FocusedElementAtDown",
            "FocusedElementAtUp",
            "FocusedAutomationIdAtDown",
            "FocusedAutomationIdAtUp",
            "FocusParentChainAtDown",
            "FocusParentChainAtUp",
            "ActualFocusedElementAtNativeKeyUp",
            "ActualFocusedAutomationIdAtNativeKeyUp",
            "ActualFocusedElementIsOriginalTargetAtNativeKeyUp",
            "ActualFocusedElementAvailableAtNativeKeyUp",
            "TargetAvailableAtNativeKeyUp",
            "ActivationCompletedOnKeyDown",
            "ActivationFinalizedAtNativeKeyUp",
            "TryFinalizeKeyDownCompletedActivationAtNativeKeyUp",
            "PhysicalKeyName",
            "0x0D => \"Enter\"",
            "BeforeActualValue",
            "AfterActualValue",
            "BeforePersistedValue",
            "AfterPersistedValue",
            "SettingsStateChanged",
            "SettingsWriteBackConfirmed",
            "StateChanged",
            "CompletedAt",
            "CorrelatedPointerAttemptId",
            "CorrelatedKeyAttemptId",
            "Layer1Win32Confirmed",
            "Layer2WpfConfirmed",
            "Layer3TargetConfirmed",
            "Layer4ActionConfirmed",
            "ControlStateTransitionConfirmed",
            "DownControlAutomationId",
            "UpControlAutomationId",
            "MatchesPointerUpTarget",
            "MatchesKeyTarget",
            "BoundaryReached",
            "BoundaryNoOpConfirmed",
            "IsExpectedBoundary",
            "\"Confirmed\"",
            "\"BoundaryNoOpConfirmed\"",
            "\"UnexpectedNoStateChange\"",
            "\"SettingsWriteBackMismatch\"",
            "\"InputUnconfirmed\"");
        Assert.IsTrue(host.TrimStart().StartsWith("#if INPUT_ROUTING_DIAGNOSTICS", StringComparison.Ordinal));
        Assert.IsTrue(diagnostics.TrimStart().StartsWith("#if INPUT_ROUTING_DIAGNOSTICS", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PaneToggleButtonsFlowThroughAllFourWpfKeyEventsWithoutTheRetryEarlyExit()
    {
        var host = Read("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs");
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        var configuration = Slice(host, "private void ConfigurePhysicalPointerDiagnostics", "private void AttachPhysicalPointerHwndHook");
        var keyRoute = Slice(host, "private void RecordPhysicalKey", "private static Button? FindAcceptanceKeyboardButtonTarget");
        var buttonTarget = Slice(host, "private static Button? FindAcceptanceKeyboardButtonTarget", "private void BeginControlStateTransition");
        var clickCorrelation = Slice(diagnostics, "public static void RecordButtonClick", "public static void RecordPointerDownEscapeTarget");

        ContainsAll(configuration,
            "Keyboard.PreviewKeyDownEvent",
            "Keyboard.KeyDownEvent",
            "Keyboard.PreviewKeyUpEvent",
            "Keyboard.KeyUpEvent",
            "new KeyEventHandler",
            "true);");
        ContainsAll(buttonTarget,
            "RetryAssetLibraryLoad",
            "ToggleAssetOrganizationPane",
            "ToggleAssetInspectorPane");
        ContainsAll(keyRoute,
            "if (acceptanceButton is null ||",
            "var activationTarget = acceptanceButton",
            "PhysicalPointerDiagnosticSession.RecordWpfKey(",
            "if (completeTransition && retryButton is not null)");
        Assert.IsFalse(keyRoute.Contains("if (retryButton is null &&", StringComparison.Ordinal),
            "Non-Retry acceptance buttons must not be rejected by Retry-only native-key-up state.");
        ContainsAll(clickCorrelation,
            "IsAcceptanceKeyboardButtonAutomationId(buttonAutomationId)",
            "ButtonClickReceived = true",
            "PhysicalTargetConfirmed = true");
    }

    [TestMethod]
    public void RetryEnter_CapturesFirstChanceNativeInputAndTruthfullyFinalizesAfterTargetDisappears()
    {
        var host = Read("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs");
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        var nativeFilter = Slice(host, "private void PhysicalPointer_ThreadFilterMessage", "private IntPtr PhysicalPointerWindowHook");
        var hwndHook = Slice(host, "private IntPtr PhysicalPointerWindowHook", "private void PhysicalPointer_PreviewMouseDown");

        ContainsAll(host,
            "ComponentDispatcher.ThreadFilterMessage += PhysicalPointer_ThreadFilterMessage",
            "ComponentDispatcher.ThreadFilterMessage -= PhysicalPointer_ThreadFilterMessage");
        ContainsAll(nativeFilter,
            "message.hwnd != _physicalPointerHwndSource.Handle",
            "RecordWin32Key",
            "TryFinalizeKeyDownCompletedActivationAtNativeKeyUp",
            "Keyboard.FocusedElement as DependencyObject");
        Assert.IsFalse(nativeFilter.Contains("handled = true", StringComparison.Ordinal));
        Assert.IsFalse(hwndHook.Contains("RecordWin32Key", StringComparison.Ordinal));
        ContainsAll(diagnostics,
            "else if (!_activeKeyAttempt.Layer1Win32.KeyDownReceived)",
            "_activeKeyAttempt.Origin = \"Win32\"",
            "ActivationCompletedOnKeyDown = true",
            "attempt.Layer2Wpf.PreviewKeyUpReceived",
            "attempt.Layer2Wpf.KeyUpReceived",
            "ActualFocusedElementAtNativeKeyUp = Describe(actualFocusedElement)",
            "ActualFocusedElementIsOriginalTargetAtNativeKeyUp = ReferenceEquals(actualFocusedElement, control)",
            "ActualFocusedElementAvailableAtNativeKeyUp =",
            "actualFocusedElement is not null && IsKeyboardTargetAvailable(actualFocusedElement)",
            "TargetAvailableAtNativeKeyUp = IsKeyboardTargetAvailable(control)",
            "PresentationSource.FromDependencyObject(control) is not null");
        Assert.IsFalse(host.Contains("RaiseEvent(new KeyEventArgs", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Diagnostic_AutomatesCollapsedAndRestartWorkspaceRestoreContracts()
    {
        var host = Read("src/RAWSelectionAssistant/MainWindow.PhysicalPointerDiagnostics.cs");
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        var layoutTests = Read("tests/RAWSelectionAssistant.WpfTests/EmbeddedAssetLibraryWpfTests.cs");
        var settingsTests = Read("tests/RAWSelectionAssistant.Tests/AssetLibraryP1SettingsTests.cs");

        ContainsAll(host,
            "RecordAssetLibraryWorkspaceRestoreState",
            "IsOrganizationPaneVisible",
            "IsInspectorPaneVisible",
            "RecordWorkspaceRestoreState");
        ContainsAll(diagnostics,
            "WorkspaceRestoreSnapshots",
            "PreviousSession",
            "ReadPreviousSessionSummary",
            "ProcessId = Environment.ProcessId",
            "ProcessStartedAt",
            "OrganizationRestoreResult",
            "InspectorRestoreResult",
            "ThumbnailRestoreConfirmed",
            "CollapsedRestored",
            "DeferredByViewport",
            "ExpandedRestored",
            "RestartComparisonPerformed",
            "RestartSettingsMatchPreviousSession");
        ContainsAll(layoutTests,
            "RealSplitterDragKeepsSideWidthBindingsAndCollapseRestoresTheDraggedWidths",
            "Assert.AreEqual(0d, organizationColumn.ActualWidth, .1)",
            "Assert.AreEqual(keyboardAdjustedOrganizationWidth, organizationColumn.ActualWidth, 1d)",
            "Assert.AreEqual(0d, inspectorColumn.ActualWidth, .1)",
            "Assert.AreEqual(retainedInspectorWidth, inspectorColumn.ActualWidth, 1d)",
            "MaximumPersistedPaneWidthsAndPinnedNarrowLayoutKeepTheCollectionInsideTheWorkspace",
            "OrganizationPaneWidth = 420",
            "InspectorPaneWidth = 520");
        ContainsAll(settingsTests,
            "Settings_RoundTripLastPrimaryPageAndAssetWorkspaceLayout",
            "Assert.AreEqual(286d, restored.AssetLibraryWorkspace.OrganizationPaneWidth)",
            "Assert.AreEqual(414d, restored.AssetLibraryWorkspace.InspectorPaneWidth)",
            "Assert.IsTrue(restored.AssetLibraryWorkspace.OrganizationPaneCollapsed)",
            "Assert.IsFalse(restored.AssetLibraryWorkspace.InspectorPaneCollapsed)");
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

    [TestMethod]
    public void PointerDownEscape_CorrelatesBeforeUpWithoutRelaxingNonPhysicalActions()
    {
        var diagnostics = Read("src/RAWSelectionAssistant/Services/PhysicalPointerDiagnosticSession.cs");
        var input = Read("src/RAWSelectionAssistant/Services/InputRoutingDiagnostics.cs");
        ContainsAll(input,
            "string.Equals(eventName, \"PreviewMouseLeftButtonDown\"",
            "args.ChangedButton == MouseButton.Left",
            "ShellEscapePointer.TryResolve(args.OriginalSource as DependencyObject",
            "PhysicalPointerDiagnosticSession.RecordPointerDownEscapeTarget");
        ContainsAll(diagnostics,
            "RecordPointerDownEscapeTarget",
            "action == ShellEscapePointerAction.None",
            "CanConfirmPhysicalPointerDownEscape",
            "_activeAttempt.Layer1Win32.LButtonDownReceived",
            "_activeAttempt.Layer2Wpf.PreviewMouseDownReceived",
            "MatchesLastWpfDownTarget(originalSource)",
            "ShellEscapePointer.GetAction(escapeOwner) == action",
            "IsAncestorOrSelf(escapeOwner, originalSource)",
            "PointerDownEscapeTargetConfirmed = true",
            "PhysicalTargetConfirmed = true",
            "PointerDownEscapeTargetConfirmed",
            "PointerDownEscapeAction");
        Assert.IsFalse(input.Contains("RecordPointerDownEscapeTarget" + Environment.NewLine + "            eventName", StringComparison.Ordinal));
        StringAssert.Contains(diagnostics,
            "if (!CanCorrelateWithActiveAttempt(requireWpfDown: true) || !_activeAttempt!.Layer4Action.PhysicalTargetConfirmed)");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static void ContainsAll(string source, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(source, value);
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, startIndex);
        Assert.IsGreaterThan(startIndex, endIndex);
        return source[startIndex..endIndex];
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

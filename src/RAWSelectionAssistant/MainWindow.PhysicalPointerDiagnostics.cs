#if INPUT_ROUTING_DIAGNOSTICS
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;

namespace RAWSelectionAssistant;

public partial class MainWindow
{
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int VkReturn = 0x0D;
    private const int VkSpace = 0x20;
    private const int VkLeft = 0x25;
    private const int VkRight = 0x27;
    private HwndSource? _physicalPointerHwndSource;
    private PendingControlStateTransition? _pendingControlStateTransition;
    private bool _closeAuthorityScanPending;
    private bool _duplicateCloseAuthorityNoticeShown;

    private void ConfigurePhysicalPointerDiagnostics()
    {
        if (!PhysicalPointerDiagnosticSession.IsEnabled) return;
#if MODULAR_HARNESS_DEV_PREVIEW
        Title = "像素蛋挞 [Modular Harness Dev]";
#else
        Title = "像素蛋挞 [Physical Pointer Diagnostic]";
#endif
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseDown), true);
        AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseUp), true);
        AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseLeftButtonDown), true);
        AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(PhysicalPointer_MouseLeftButtonDown), true);
        AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseLeftButtonUp), true);
        AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(PhysicalPointer_MouseLeftButtonUp), true);
        AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(PhysicalPointer_MouseUpCompleted), true);
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(PhysicalPointer_ButtonClick), true);
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(PhysicalPointer_ControlDragStarted), true);
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(PhysicalPointer_ControlDragCompleted), true);
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(PhysicalPointer_PreviewKeyDown), true);
        AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(PhysicalPointer_KeyDown), true);
        AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(PhysicalPointer_PreviewKeyUp), true);
        AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler(PhysicalPointer_KeyUp), true);
        LayoutUpdated += PhysicalPointer_LayoutUpdated;
        PhysicalPointerDiagnosticCopyButton.Visibility = Visibility.Visible;
        PhysicalPointerDiagnosticSession.Begin(CurrentPointerDiagnosticContext());
    }

    private void AttachPhysicalPointerHwndHook()
    {
        if (!PhysicalPointerDiagnosticSession.IsEnabled || _physicalPointerHwndSource is not null) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        _physicalPointerHwndSource = HwndSource.FromHwnd(handle);
        _physicalPointerHwndSource?.AddHook(PhysicalPointerWindowHook);
    }

    private void DisposePhysicalPointerDiagnostics()
    {
        if (!PhysicalPointerDiagnosticSession.IsEnabled) return;
        PhysicalPointerDiagnosticSession.Complete(CurrentPointerDiagnosticContext());
        _physicalPointerHwndSource?.RemoveHook(PhysicalPointerWindowHook);
        _physicalPointerHwndSource = null;
        LayoutUpdated -= PhysicalPointer_LayoutUpdated;
    }

    private IntPtr PhysicalPointerWindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is WmKeyDown or WmKeyUp)
        {
            var virtualKey = unchecked((int)(long)wParam);
            if (virtualKey is VkLeft or VkRight or VkReturn or VkSpace)
            {
                var nativeKeyData = unchecked((uint)(long)lParam);
                PhysicalPointerDiagnosticSession.RecordWin32Key(
                    message == WmKeyDown ? "WM_KEYDOWN" : "WM_KEYUP",
                    virtualKey,
                    scanCode: (int)((nativeKeyData >> 16) & 0xff),
                    repeatCount: (int)(nativeKeyData & 0xffff),
                    modifiers: Keyboard.Modifiers,
                    nativeMessageTime: GetMessageTime(),
                    isExtendedKey: (nativeKeyData & 0x01000000) != 0,
                    wasPreviouslyDown: (nativeKeyData & 0x40000000) != 0,
                    isTransitionUp: (nativeKeyData & 0x80000000) != 0,
                    context: CurrentPointerDiagnosticContext());
            }
            return IntPtr.Zero;
        }

        var name = message switch
        {
            WmLButtonDown => "WM_LBUTTONDOWN",
            WmLButtonUp => "WM_LBUTTONUP",
            WmMouseMove => "WM_MOUSEMOVE",
            _ => string.Empty
        };
        if (name.Length == 0) return IntPtr.Zero;

        var nativeWindowPoint = new NativePointerPoint
        {
            X = unchecked((short)(long)lParam),
            Y = unchecked((short)((long)lParam >> 16))
        };
        try
        {
            var nativeScreenPoint = nativeWindowPoint;
            ClientToScreen(hwnd, ref nativeScreenPoint);
            var fromDevice = _physicalPointerHwndSource?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var windowPoint = fromDevice.Transform(new Point(nativeWindowPoint.X, nativeWindowPoint.Y));
            var screenPoint = fromDevice.Transform(new Point(nativeScreenPoint.X, nativeScreenPoint.Y));
            PhysicalPointerDiagnosticSession.RecordWin32Mouse(name, windowPoint, screenPoint, CurrentPointerDiagnosticContext());
        }
        catch (InvalidOperationException)
        {
        }
        return IntPtr.Zero;
    }

    private void PhysicalPointer_PreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "PreviewMouseDown");

    private void PhysicalPointer_PreviewMouseUp(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "PreviewMouseUp");

    private void PhysicalPointer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "PreviewMouseLeftButtonDown");

    private void PhysicalPointer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "MouseLeftButtonDown");

    private void PhysicalPointer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "PreviewMouseLeftButtonUp");

    private void PhysicalPointer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "MouseLeftButtonUp");

    private void PhysicalPointer_MouseUpCompleted(object sender, MouseButtonEventArgs e) =>
        PhysicalPointerDiagnosticSession.CompleteWpfMouseDispatch(e.ChangedButton);

    private void RecordPhysicalPointerWpf(MouseButtonEventArgs e, string eventName) =>
        PhysicalPointerDiagnosticSession.RecordWpfMouse(RootGrid, e, eventName, CurrentPointerDiagnosticContext());

    private void PhysicalPointer_ButtonClick(object sender, RoutedEventArgs e) =>
        PhysicalPointerDiagnosticSession.RecordButtonClick(e.OriginalSource as DependencyObject, e.Handled, CurrentPointerDiagnosticContext());

    private void PhysicalPointer_ControlDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!TryCaptureAcceptanceControlState(e.OriginalSource as DependencyObject, out var state)) return;
        BeginControlStateTransition(state, "MouseDrag", inputKey: string.Empty, expectedAdjustment: string.Empty);
    }

    private void PhysicalPointer_ControlDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_pendingControlStateTransition?.InputKind != "MouseDrag") return;
        ScheduleControlStateTransitionCompletion(_pendingControlStateTransition);
    }

    private void PhysicalPointer_PreviewKeyDown(object sender, KeyEventArgs e) =>
        RecordPhysicalKey(e, "PreviewKeyDown", beginTransition: true, completeTransition: false);

    private void PhysicalPointer_KeyDown(object sender, KeyEventArgs e) =>
        RecordPhysicalKey(e, "KeyDown", beginTransition: false, completeTransition: false);

    private void PhysicalPointer_PreviewKeyUp(object sender, KeyEventArgs e) =>
        RecordPhysicalKey(e, "PreviewKeyUp", beginTransition: false, completeTransition: false);

    private void PhysicalPointer_KeyUp(object sender, KeyEventArgs e) =>
        RecordPhysicalKey(e, "KeyUp", beginTransition: false, completeTransition: true);

    private void RecordPhysicalKey(
        KeyEventArgs e,
        string eventName,
        bool beginTransition,
        bool completeTransition)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var focusedElement = Keyboard.FocusedElement as DependencyObject;
        var target = focusedElement ?? e.OriginalSource as DependencyObject;
        if (key is Key.Enter or Key.Space &&
            IsRetryAssetLibraryLoadTarget(target))
        {
            PhysicalPointerDiagnosticSession.RecordWpfKey(
                target!,
                focusedElement,
                key,
                eventName,
                e.IsRepeat,
                Keyboard.Modifiers,
                e.OriginalSource as DependencyObject,
                e.Source as DependencyObject,
                e.Handled,
                CurrentPointerDiagnosticContext());
            return;
        }

        if (key is not (Key.Left or Key.Right) ||
            !TryCaptureAcceptanceControlState(target, out var state))
            return;

        PhysicalPointerDiagnosticSession.RecordWpfKey(
            state.Control,
            focusedElement,
            key,
            eventName,
            e.IsRepeat,
            Keyboard.Modifiers,
            e.OriginalSource as DependencyObject,
            e.Source as DependencyObject,
            e.Handled,
            CurrentPointerDiagnosticContext());

        if (beginTransition && !e.IsRepeat)
        {
            BeginControlStateTransition(
                state,
                "Keyboard",
                inputKey: key.ToString(),
                expectedAdjustment: ExpectedKeyboardAdjustment(state.ControlAutomationId, key));
        }
        if (completeTransition &&
            _pendingControlStateTransition is { InputKind: "Keyboard" } pending &&
            ReferenceEquals(pending.Control, state.Control))
            ScheduleControlStateTransitionCompletion(pending);
    }

    private static bool IsRetryAssetLibraryLoadTarget(DependencyObject? source)
    {
        var button = FindDiagnosticAncestor<Button>(source);
        return button is not null && string.Equals(
            AutomationProperties.GetAutomationId(button),
            "RetryAssetLibraryLoad",
            StringComparison.Ordinal);
    }

    private void BeginControlStateTransition(
        AcceptanceControlState state,
        string inputKind,
        string inputKey,
        string expectedAdjustment)
    {
        if (_pendingControlStateTransition is not null) return;
        var transitionId = PhysicalPointerDiagnosticSession.BeginControlStateTransition(
            state.Control,
            inputKind,
            inputKey,
            expectedAdjustment,
            state.ControlKind,
            state.PropertyName,
            state.ActualValue,
            state.PersistedValue,
            state.MinimumValue,
            state.MaximumValue,
            state.IsCollapsed,
            CurrentPointerDiagnosticContext());
        if (transitionId.Length == 0) return;
        _pendingControlStateTransition = new PendingControlStateTransition(
            transitionId,
            state.Control,
            inputKind);
    }

    private void ScheduleControlStateTransitionCompletion(PendingControlStateTransition pending)
    {
        if (!ReferenceEquals(_pendingControlStateTransition, pending)) return;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (!ReferenceEquals(_pendingControlStateTransition, pending)) return;
            _pendingControlStateTransition = null;
            if (!TryCaptureAcceptanceControlState(pending.Control, out var state)) return;
            PhysicalPointerDiagnosticSession.CompleteControlStateTransition(
                pending.TransitionId,
                pending.Control,
                state.ActualValue,
                state.PersistedValue,
                state.IsCollapsed,
                CurrentPointerDiagnosticContext());
        });
    }

    private bool TryCaptureAcceptanceControlState(
        DependencyObject? source,
        out AcceptanceControlState state)
    {
        if (AssetLibraryWorkspace.Content is not PixelTart.Modules.AssetLibrary.AssetLibraryPage assetLibraryPage)
        {
            state = AcceptanceControlState.None;
            return false;
        }

        var viewModel = assetLibraryPage.ViewModel;
        var splitter = FindDiagnosticAncestor<GridSplitter>(source);
        if (splitter is not null)
        {
            var automationId = AutomationProperties.GetAutomationId(splitter);
            if (splitter.Parent is Grid grid &&
                string.Equals(automationId, "AssetOrganizationSplitter", StringComparison.Ordinal) &&
                grid.ColumnDefinitions.Count > 0)
            {
                state = new(
                    splitter,
                    automationId,
                    "GridSplitter",
                    "OrganizationPaneWidth",
                    grid.ColumnDefinitions[0].ActualWidth,
                    viewModel.OrganizationPaneWidth,
                    180d,
                    420d,
                    viewModel.IsOrganizationPaneCollapsed);
                return true;
            }
            if (splitter.Parent is Grid inspectorGrid &&
                string.Equals(automationId, "AssetInspectorSplitter", StringComparison.Ordinal) &&
                inspectorGrid.ColumnDefinitions.Count > 4)
            {
                state = new(
                    splitter,
                    automationId,
                    "GridSplitter",
                    "InspectorPaneWidth",
                    inspectorGrid.ColumnDefinitions[4].ActualWidth,
                    viewModel.InspectorPaneWidth,
                    260d,
                    520d,
                    viewModel.IsInspectorPaneCollapsed);
                return true;
            }
        }

        var slider = FindDiagnosticAncestor<Slider>(source);
        if (slider is not null &&
            string.Equals(AutomationProperties.GetAutomationId(slider), "AssetThumbnailSizeSlider", StringComparison.Ordinal))
        {
            state = new(
                slider,
                "AssetThumbnailSizeSlider",
                "Slider",
                "ThumbnailWidth",
                slider.Value,
                viewModel.ThumbnailWidth,
                slider.Minimum,
                slider.Maximum,
                false);
            return true;
        }

        state = AcceptanceControlState.None;
        return false;
    }

    private static string ExpectedKeyboardAdjustment(string controlAutomationId, Key key) =>
        (controlAutomationId, key) switch
        {
            ("AssetOrganizationSplitter", Key.Left) => "Decrease",
            ("AssetOrganizationSplitter", Key.Right) => "Increase",
            ("AssetInspectorSplitter", Key.Left) => "Increase",
            ("AssetInspectorSplitter", Key.Right) => "Decrease",
            _ => string.Empty
        };

    private void RecordAssetLibraryWorkspaceRestoreState()
    {
        if (_viewModel?.IsAssetLibraryPage != true ||
            AssetLibraryWorkspace.Content is not PixelTart.Modules.AssetLibrary.AssetLibraryPage assetLibraryPage)
            return;

        var splitters = FindVisualChildren<GridSplitter>(assetLibraryPage).ToArray();
        var organizationSplitter = splitters.FirstOrDefault(splitter => string.Equals(
            AutomationProperties.GetAutomationId(splitter),
            "AssetOrganizationSplitter",
            StringComparison.Ordinal));
        var inspectorSplitter = splitters.FirstOrDefault(splitter => string.Equals(
            AutomationProperties.GetAutomationId(splitter),
            "AssetInspectorSplitter",
            StringComparison.Ordinal));
        var thumbnailSlider = FindVisualChildren<Slider>(assetLibraryPage).FirstOrDefault(slider => string.Equals(
            AutomationProperties.GetAutomationId(slider),
            "AssetThumbnailSizeSlider",
            StringComparison.Ordinal));
        if (organizationSplitter is null || inspectorSplitter is null || thumbnailSlider is null ||
            !TryCaptureAcceptanceControlState(organizationSplitter, out var organization) ||
            !TryCaptureAcceptanceControlState(inspectorSplitter, out var inspector) ||
            !TryCaptureAcceptanceControlState(thumbnailSlider, out var thumbnail))
            return;

        PhysicalPointerDiagnosticSession.RecordWorkspaceRestoreState(
            organization.ActualValue,
            organization.PersistedValue,
            assetLibraryPage.ViewModel.IsOrganizationPaneVisible,
            organization.IsCollapsed,
            inspector.ActualValue,
            inspector.PersistedValue,
            assetLibraryPage.ViewModel.IsInspectorPaneVisible,
            inspector.IsCollapsed,
            thumbnail.ActualValue,
            thumbnail.PersistedValue,
            CurrentPointerDiagnosticContext());
    }

    private static T? FindDiagnosticAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = GetDiagnosticParent(current))
            if (current is T match) return match;
        return null;
    }

    private static DependencyObject? GetDiagnosticParent(DependencyObject source)
    {
        try
        {
            return VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(source);
        }
    }

    private void PhysicalPointer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_closeAuthorityScanPending) return;
        _closeAuthorityScanPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            _closeAuthorityScanPending = false;
            if (!IsLoaded) return;
            RecordAssetLibraryWorkspaceRestoreState();
            var visibleCloseButtons = FindVisualChildren<SurfaceCloseButton>(RootGrid)
                .Where(button => button.IsVisible && button.IsHitTestVisible && button.IsEnabled)
                .ToArray();
            var visibleCloseIds = visibleCloseButtons
                .Select(button => string.IsNullOrWhiteSpace(button.AutomationId)
                    ? "MissingAutomationId"
                    : button.AutomationId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (visibleCloseButtons.Length > 1 && !_duplicateCloseAuthorityNoticeShown)
            {
                _duplicateCloseAuthorityNoticeShown = true;
                DuplicateCloseAuthorityBanner.Visibility = Visibility.Visible;
            }
            PhysicalPointerDiagnosticSession.RecordCloseAuthority(
                _viewModel?.CurrentPage ?? string.Empty,
                visibleCloseIds,
                CurrentPointerDiagnosticContext());
        });
    }

    private PointerDiagnosticContext CurrentPointerDiagnosticContext() => new(
        _viewModel?.CurrentPage ?? string.Empty,
        CurrentOverlayName(),
        _viewModel?.IsOnboardingActive == true,
        _viewModel?.IsOnboardingActive == true ? _viewModel.TutorialStepNumber : null);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePointerPoint point);

    [DllImport("user32.dll")]
    private static extern int GetMessageTime();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePointerPoint
    {
        public int X;
        public int Y;
    }

    private sealed record PendingControlStateTransition(
        string TransitionId,
        DependencyObject Control,
        string InputKind);

    private sealed record AcceptanceControlState(
        DependencyObject Control,
        string ControlAutomationId,
        string ControlKind,
        string PropertyName,
        double ActualValue,
        double PersistedValue,
        double MinimumValue,
        double MaximumValue,
        bool IsCollapsed)
    {
        public static AcceptanceControlState None { get; } = new(
            null!,
            string.Empty,
            string.Empty,
            string.Empty,
            0d,
            0d,
            0d,
            0d,
            false);
    }
}
#endif

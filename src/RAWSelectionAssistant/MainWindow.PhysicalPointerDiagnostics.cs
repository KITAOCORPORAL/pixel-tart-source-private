#if INPUT_ROUTING_DIAGNOSTICS
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
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
    private HwndSource? _physicalPointerHwndSource;
    private bool _closeAuthorityScanPending;
    private bool _duplicateCloseAuthorityNoticeShown;

    private void ConfigurePhysicalPointerDiagnostics()
    {
        if (!PhysicalPointerDiagnosticSession.IsEnabled) return;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseDown), true);
        AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseUp), true);
        AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseLeftButtonDown), true);
        AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(PhysicalPointer_PreviewMouseLeftButtonUp), true);
        AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(PhysicalPointer_MouseUpCompleted), true);
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(PhysicalPointer_ButtonClick), true);
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

    private void PhysicalPointer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        RecordPhysicalPointerWpf(e, "PreviewMouseLeftButtonUp");

    private void PhysicalPointer_MouseUpCompleted(object sender, MouseButtonEventArgs e) =>
        PhysicalPointerDiagnosticSession.CompleteWpfMouseDispatch(e.ChangedButton);

    private void RecordPhysicalPointerWpf(MouseButtonEventArgs e, string eventName) =>
        PhysicalPointerDiagnosticSession.RecordWpfMouse(RootGrid, e, eventName, CurrentPointerDiagnosticContext());

    private void PhysicalPointer_ButtonClick(object sender, RoutedEventArgs e) =>
        PhysicalPointerDiagnosticSession.RecordButtonClick(e.OriginalSource as DependencyObject, e.Handled, CurrentPointerDiagnosticContext());

    private void PhysicalPointer_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_closeAuthorityScanPending) return;
        _closeAuthorityScanPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            _closeAuthorityScanPending = false;
            if (!IsLoaded) return;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePointerPoint
    {
        public int X;
        public int Y;
    }
}
#endif

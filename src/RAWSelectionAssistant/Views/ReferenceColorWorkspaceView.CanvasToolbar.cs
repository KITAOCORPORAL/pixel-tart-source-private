using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.Views;

// Pointer modes are transient view state, not a processing definition or an edit.
internal enum ColorStudioCanvasTool { Select, Hand, Zoom, ColorSample, Brush }

public partial class ReferenceColorWorkspaceView
{
    internal ColorStudioCanvasTool ActiveCanvasTool { get; private set; }
    private bool _canvasOriginalHeld;
    private string? _canvasCompareReturnMode;
    private Window? _canvasWindow;
    private bool CanvasReady => _editor?.SourceImage is not null
        && DataContext is ReferenceColorWorkspaceViewModel { IsLoading: false, IsInstantGenerating: false };

    private void InitializeCanvasToolbar()
    {
        PreviewKeyDown += OnCanvasToolKeyDown;
        PreviewKeyUp += OnCanvasToolKeyUp;
        LostKeyboardFocus += (_, _) => ReleaseCanvasOriginal();
        Loaded += (_, _) =>
        {
            var window = Window.GetWindow(this);
            if (!ReferenceEquals(window, _canvasWindow))
            {
                if (_canvasWindow is not null) _canvasWindow.Deactivated -= OnCanvasWindowDeactivated;
                _canvasWindow = window;
                if (_canvasWindow is not null) _canvasWindow.Deactivated += OnCanvasWindowDeactivated;
            }
            RefreshCanvasToolbar();
        };
        Unloaded += (_, _) =>
        {
            ResetCanvasInteraction();
            if (_canvasWindow is not null) _canvasWindow.Deactivated -= OnCanvasWindowDeactivated;
            _canvasWindow = null;
        };
        RefreshCanvasToolbar();
    }

    private void OnCanvasWindowDeactivated(object? sender, EventArgs e) => ResetCanvasInteraction();

    internal bool SelectCanvasTool(ColorStudioCanvasTool tool)
    {
        if (!CanvasReady) return false;
        if (tool == ColorStudioCanvasTool.ColorSample && (_editor!.IndependentRawMode || _editor.SelectedAdjustmentNode?.Type != ColorStudioNodeType.ColorRange))
            return false;
        CancelCanvasSampling();
        ActiveCanvasTool = tool;
        if (tool == ColorStudioCanvasTool.ColorSample) _editor!.StartSamplingCommand.Execute("普通取样");
        RefreshCanvasToolbar();
        return true;
    }

    private void CancelCanvasSampling()
    {
        if (_editor?.IsSampling == true) _editor.CancelSamplingCommand.Execute(null);
        if (DataContext is ReferenceColorWorkspaceViewModel workspace) workspace.ClearPreviewSelection();
        _inspectingImageColor = false;
        ColorInspectionHint.Visibility = Visibility.Collapsed;
        _panning = _draggingDivider = false;
        if (PreviewCanvas.IsMouseCaptured) PreviewCanvas.ReleaseMouseCapture();
    }

    internal void ResetCanvasInteraction()
    {
        ReleaseCanvasOriginal();
        CancelCanvasSampling();
        ActiveCanvasTool = ColorStudioCanvasTool.Select;
        _canvasCompareReturnMode = null;
        RefreshCanvasToolbar();
    }

    internal void SetCanvasOriginalHeld(bool held)
    {
        if (held && !CanvasReady) return;
        _canvasOriginalHeld = held;
        _editor?.HoldOriginal(held);
    }
    private void ReleaseCanvasOriginal()
    {
        if (_canvasOriginalHeld) SetCanvasOriginalHeld(false);
    }

    internal void ToggleCanvasOriginal()
    {
        if (!CanvasReady || _editor is null) return;
        ReleaseCanvasOriginal();
        if (_editor.ViewMode == "原片")
        {
            _editor.ViewMode = _canvasCompareReturnMode ?? "仿色结果";
            _canvasCompareReturnMode = null;
        }
        else { _canvasCompareReturnMode = _editor.ViewMode; _editor.ViewMode = "原片"; }
        RefreshCanvasToolbar();
    }

    private void RefreshCanvasToolbar()
    {
        if (CanvasSelectButton is null) return;
        CanvasSelectButton.IsChecked = ActiveCanvasTool == ColorStudioCanvasTool.Select;
        CanvasHandButton.IsChecked = ActiveCanvasTool == ColorStudioCanvasTool.Hand;
        CanvasZoomButton.IsChecked = ActiveCanvasTool == ColorStudioCanvasTool.Zoom;
        CanvasSampleButton.IsChecked = _editor?.IsSampling == true;
        CanvasCompareButton.IsChecked = _editor?.ViewMode == "原片";
        CanvasSelectButton.IsEnabled = CanvasHandButton.IsEnabled = CanvasZoomButton.IsEnabled = CanvasCompareButton.IsEnabled = CanvasReady;
        CanvasSampleButton.IsEnabled = CanvasReady && _editor?.IndependentRawMode == false && _editor.SelectedAdjustmentNode?.Type == ColorStudioNodeType.ColorRange;
        StudioTextExtension.Bind(CanvasSampleButton, ToolTipProperty, CanvasSampleButton.IsEnabled
            ? "CanvasSampleHelp"
            : "CanvasSampleDisabled");
        CanvasUndoButton.IsEnabled = CanvasRedoButton.IsEnabled = CanvasReady && _editor?.IndependentRawMode == false && _editor.IsProMode;
        PreviewCanvas.Cursor = (_editor?.IsSampling == true || _inspectingImageColor) ? Cursors.Cross
            : ActiveCanvasTool == ColorStudioCanvasTool.Hand ? Cursors.Hand
            : ActiveCanvasTool == ColorStudioCanvasTool.Zoom ? Cursors.SizeAll : Cursors.Arrow;
        UpdateCanvasZoomLabel();
    }

    private void UpdateCanvasZoomLabel()
    {
        // SourcePixelSize may itself fall back to the proxy. Do not equate a size with HQ coverage.
        var proxy = _editor?.SourceImage is null ||
            (_editor.IndependentRawMode && _editor.IndependentRawSize.IsEmpty);
        ZoomLabel.Text = $"{_zoomPan.Zoom * System.Windows.Media.VisualTreeHelper.GetDpi(ImageViewport).DpiScaleX:P0}" + (proxy ? RAWSelectionAssistant.Services.StudioLocalizationService.Current["CanvasProxySuffix"] : "");
    }

    private void OnCanvasToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ColorStudioCanvasTool>(tag, out var tool)) SelectCanvasTool(tool);
        PreviewCanvas.Focus();
        e.Handled = true;
    }
    private void OnCanvasCompareClick(object sender, RoutedEventArgs e)
    { ToggleCanvasOriginal(); PreviewCanvas.Focus(); e.Handled = true; }

    internal bool CanvasShortcutScope(DependencyObject? focus)
    {
        if (ReferenceEquals(focus, PreviewCanvas)) return true;
        if (focus is not ButtonBase) return false;
        for (var current = focus; current is not null; current = LogicalTreeHelper.GetParent(current))
            if (ReferenceEquals(current, PhotoToolsBand)) return true;
        return PhotoToolsBand.IsAncestorOf(focus);
    }

    internal bool ExecuteCanvasShortcut(Key key, ModifierKeys modifiers, bool repeat = false)
    {
        if (!CanvasReady) return false;
        if (modifiers == ModifierKeys.Control)
        {
            if (key is Key.D0 or Key.NumPad0) { _zoomPan.Fit(); return true; }
            if (key is Key.D1 or Key.NumPad1) { OnActualSizeClick(this, new RoutedEventArgs()); return true; }
            return false;
        }
        if (modifiers != ModifierKeys.None) return false;
        if(key==Key.B){ActiveCanvasTool=ColorStudioCanvasTool.Brush;ToolTip="画笔：当前局部蒙版处理未批准接入，不生成假结果。";return true;}
        if(key==Key.Oem5){ToggleCanvasOriginal();return true;}
        if (repeat) return false;
        return key switch
        {
            Key.V => SelectCanvasTool(ColorStudioCanvasTool.Select),
            Key.H => SelectCanvasTool(ColorStudioCanvasTool.Hand),
            Key.Z => SelectCanvasTool(ColorStudioCanvasTool.Zoom),
            Key.I => SelectCanvasTool(ColorStudioCanvasTool.ColorSample),
            _ => false
        };
    }
    private void OnCanvasToolKeyDown(object sender, KeyEventArgs e)
    {
        if (!e.Handled && CanvasShortcutScope(Keyboard.FocusedElement as DependencyObject))
            e.Handled = ExecuteCanvasShortcut(e.Key, Keyboard.Modifiers, e.IsRepeat);
    }
    private void OnCanvasToolKeyUp(object sender, KeyEventArgs e)
    { /* B is brush, never a comparison hold. Mouse comparison releases via capture. */ }

    internal void ZoomCanvasAt(Point point, bool zoomOut)
    {
        if (_editor?.EffectiveViewMode == "并排对比" && point.X >= PreviewCanvas.ActualWidth / 2)
            point.X -= (PreviewCanvas.ActualWidth + 1) / 2;
        _zoomPan.ZoomAbout(point, zoomOut ? 1 / 1.25 : 1.25);
    }
}

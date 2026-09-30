using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.Views;

public partial class ReferenceColorWorkspaceView
{
    // Diagnostic observations only. No request can invoke a command or inject input.
    private readonly List<object> _nativeSamples = [];
    private long _nativeSampleSequence;

    private void RecordNativeSample(MouseButtonEventArgs input, Point? imagePoint, VisualRgb24? value)
    {
        if (!ColorStudioAcceptanceFixture.NativeObserverRequested) return;
        var point = input.GetPosition(PreviewCanvas);
        var screen = PreviewCanvas.PointToScreen(point);
        var rect = _zoomPan.ImageRect(new Rect(_zoomPan.ViewportSize));
        _nativeSamples.Add(new
        {
            Sequence = ++_nativeSampleSequence, Timestamp = DateTimeOffset.UtcNow,
            InputTimestamp = input.Timestamp, Input = new { screen.X, screen.Y },
            Viewport = new { point.X, point.Y, PreviewCanvas.ActualWidth, PreviewCanvas.ActualHeight },
            Image = imagePoint is { } p ? new { p.X, p.Y } : null,
            ImageRect = new { rect.X, rect.Y, rect.Width, rect.Height },
            _zoomPan.Zoom, _zoomPan.PanX, _zoomPan.PanY, _zoomPan.IsFit,
            Valid = imagePoint is not null, Value = value, Mode = _editor?.SampleMode,
            SelectedNode = _editor?.SelectedAdjustmentNode?.Id
        });
        if (_nativeSamples.Count > 128) _nativeSamples.RemoveAt(0);
    }

    internal object ReadNativeViewportEvidence() => new
    {
        IsSampling = _editor?.IsSampling, Mode = _editor?.SampleMode,
        PositiveSamples = _editor?.PositiveSamples, NegativeSamples = _editor?.NegativeSamples,
        Canvas = NativeBounds(PreviewCanvas),
        _zoomPan.Zoom, _zoomPan.PanX, _zoomPan.PanY, _zoomPan.IsFit,
        ImageSize = new { _zoomPan.ImageSize.Width, _zoomPan.ImageSize.Height },
        Samples = _nativeSamples.ToArray(),
        ColorSpace = ColorSpaceViewport.ReadNativeEvidence()
    };

    internal static object NativeBounds(FrameworkElement element)
    {
        var origin = element.IsVisible ? element.PointToScreen(new Point()) : new Point();
        var dpi = VisualTreeHelper.GetDpi(element);
        return new { origin.X, origin.Y, Width = element.ActualWidth * dpi.DpiScaleX,
            Height = element.ActualHeight * dpi.DpiScaleY, element.IsVisible,
            DipWidth = element.ActualWidth, DipHeight = element.ActualHeight,
            dpi.DpiScaleX, dpi.DpiScaleY };
    }
}

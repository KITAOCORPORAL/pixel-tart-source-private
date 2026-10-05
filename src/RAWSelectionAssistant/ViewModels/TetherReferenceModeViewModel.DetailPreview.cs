using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Services;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel
{
    private bool _rawDetailRequested;
    private BitmapSource? _fullRawDisplay;
    private CancellationTokenSource? _detailRequest;
    public Task DetailPreviewWork { get; private set; } = Task.CompletedTask;
    public Size SourcePixelSize => _frozenRawMaster is { } raw ? new(raw.Width, raw.Height)
        : _source is { } image ? new(image.PixelWidth, image.PixelHeight) : Size.Empty;
    public bool IsDetailPreviewRequested => _rawDetailRequested;
    public void RequestPreviewZoom(double zoom, double dpiScale = 1)
        => DetailPreviewWork = SetPreviewZoomAsync(zoom, dpiScale);
    public async Task SetPreviewZoomAsync(double zoom, double dpiScale = 1)
    {
        if (_frozenRawMaster is not { } master || _rawPreviewMaster is null || !double.IsFinite(zoom) || zoom <= 0) return;
        // Enough image samples for the displayed source-pixel scale. A full master is
        // retained for true 100%; repeated pan/zoom reuses the same input and render cache.
        var needsDetail = Math.Max(master.Width, master.Height) * zoom * Math.Max(1, dpiScale) > Math.Max(_rawPreviewMaster.Width, _rawPreviewMaster.Height) * 1.01;
        if (needsDetail == _rawDetailRequested) return;
        _rawDetailRequested = needsDetail; OnPropertyChanged(nameof(IsDetailPreviewRequested));
        _detailRequest?.Cancel(); using var pending = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _detailRequest = pending;
        try
        {
            await Task.Delay(120, pending.Token);
            var fullDisplay = _fullRawDisplay;
            if (needsDetail && fullDisplay is null)
                fullDisplay = await Task.Run(() => RawDisplayBitmapAdapter.ToBitmap(master.Image.ToVisualRgb24()), pending.Token);
            if (pending.IsCancellationRequested || !ReferenceEquals(master, _frozenRawMaster)) return;
            if (needsDetail) _fullRawDisplay = fullDisplay;
            if (needsDetail && !ReferenceEquals(_source, _fullRawDisplay))
            { _source = _fullRawDisplay; OnPropertyChanged(nameof(SourceImage)); RaiseViewProperties(); }
            await RenderAsync(pending.Token);
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested) { }
        finally { if (ReferenceEquals(_detailRequest, pending)) _detailRequest = null; }
    }
    private HighBitDepthImageBuffer? RawRenderInput(bool interactive) => interactive ? _interactiveRawMaster ?? _rawPreviewMaster
        : _rawDetailRequested ? _frozenRawMaster?.Image ?? _rawPreviewMaster : _rawPreviewMaster;
    private void CancelDetailPreviewRequest()
    {
        if (_detailRequest is null) return;
        _detailRequest.Cancel(); _rawDetailRequested = false; OnPropertyChanged(nameof(IsDetailPreviewRequested));
    }
    private void ResetDetailPreview()
    {
        _detailRequest?.Cancel(); _rawDetailRequested = false; _fullRawDisplay = null;
        OnPropertyChanged(nameof(IsDetailPreviewRequested)); OnPropertyChanged(nameof(SourcePixelSize));
    }
}

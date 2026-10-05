using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class TetherReferenceModeViewModel
{
    // The RAW proxy is immutable and is already in the renderer's encoded-sRGB float
    // contract. Inspection must not substitute its 8-bit display BitmapSource.
    internal HighBitDepthImageBuffer? RangeSelectionRawInput => _rawPreviewMaster;
}

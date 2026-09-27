namespace RAWSelectionAssistant.Core.Services.Color;

/// <summary>Explicit working-space contract. Conversion is performed only by stages that declare support.</summary>
public enum WorkingColorSpace
{
    UnknownCameraNative,
    SRgb,
    LinearSRgb,
    DisplayP3,
    AdobeRgb,
    ProPhotoRgb
}

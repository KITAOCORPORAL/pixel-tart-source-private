namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

/// <summary>
/// Boundary for Capture One styles. The format is proprietary and version-sensitive;
/// no parser is enabled until a legally supplied fixture establishes a compatible subset.
/// </summary>
public sealed record CaptureOnePresetImportResult(bool IsSupported, string Status, IReadOnlyList<string> SupportedFields)
{
    public static CaptureOnePresetImportResult NotVerified { get; } =
        new(false, "INFRASTRUCTURE READY / NOT VERIFIED", Array.Empty<string>());
}

public sealed class CaptureOnePresetImporter
{
    public CaptureOnePresetImportResult Inspect(ReadOnlySpan<byte> data)
    {
        _ = data;
        return CaptureOnePresetImportResult.NotVerified;
    }
}

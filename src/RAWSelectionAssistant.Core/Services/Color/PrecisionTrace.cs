namespace RAWSelectionAssistant.Core.Services.Color;

public sealed record PrecisionTrace(IReadOnlyList<string> Stages)
{
    public bool HasPrecisionBreak => Stages.Any(stage => stage.Contains("PRECISION_BREAK", StringComparison.Ordinal));
    public static PrecisionTrace ProfessionalDefault { get; } = new(["SOURCE:16-bit", "DECODE:ushort RGB48", "WORKING:float RGB", "COLOR_STUDIO:float RGB", "V4:float RGB", "PRESET:float RGB", "FILM:float RGB", "DISPLAY:8-bit adapter", "TIFF:ushort RGB48 quantization"]);
}

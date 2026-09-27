namespace RAWSelectionAssistant.Core.Services.Color;

public sealed record PrecisionAnalysis(int UniqueValues, double Promoted8BitRatio, int MinimumStep, double MedianStep)
{
    public bool LooksLike8BitPromotion => UniqueValues <= 256 || Promoted8BitRatio >= .95;
}

public static class PrecisionAnalysisService
{
    public static PrecisionAnalysis Analyze(ReadOnlySpan<ushort> values)
    {
        if (values.Length == 0) return new(0, 1, 0, 0);
        var unique = values.ToArray().Distinct().OrderBy(x => x).ToArray();
        var promotedCount = 0; for (var i = 0; i < values.Length; i++) if (values[i] % 257 == 0) promotedCount++;
        var promoted = promotedCount / (double)values.Length;
        var steps = unique.Zip(unique.Skip(1), (a, b) => (int)b - a).Where(step => step > 0).OrderBy(step => step).ToArray();
        var median = steps.Length == 0 ? 0 : steps.Length % 2 == 1 ? steps[steps.Length / 2] : (steps[(steps.Length / 2) - 1] + steps[steps.Length / 2]) / 2d;
        return new(unique.Length, promoted, steps.Length == 0 ? 0 : steps[0], median);
    }
}

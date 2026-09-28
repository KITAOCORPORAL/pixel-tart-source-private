using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Resolved, immutable pixel-stage transform produced by Match V4 analysis.</summary>
public sealed record MatchV4ResolvedTransform(
    OklabColor GlobalDelta,
    IReadOnlyList<OklabColor> RegionDeltas,
    ReferenceMatchV4Settings Settings,
    string TransformHash,
    double Strength = 1,
    bool KeepLuminance = false)
{
    public MatchV4ResolvedTransform WithExecution(double strength, bool keepLuminance)
    {
        if (!double.IsFinite(strength) || strength is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(strength));
        var executionText = $"{TransformHash}|strength={strength:R}|keep-luminance={keepLuminance}";
        var executionHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(executionText)));
        return this with { Strength = strength, KeepLuminance = keepLuminance, TransformHash = executionHash };
    }

    public MatchV4ResolvedTransform Normalize()
    {
        Settings.Validate();
        if (RegionDeltas.Count != 3) throw new ArgumentException("V4 pixel transform requires three luminance regions.", nameof(RegionDeltas));
        if (!double.IsFinite(Strength) || Strength is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(Strength));
        if (string.IsNullOrWhiteSpace(TransformHash)) throw new ArgumentException("Transform identity is required.", nameof(TransformHash));
        return this;
    }
}

public sealed record MatchV4ResolvedAnalysis(
    MatchV4ResolvedTransform Transform,
    int RepresentativeSourceCount,
    int RepresentativeReferenceCount,
    ReferenceMatchV4BackendKind Backend,
    bool UsedCpuFallback,
    GpuFailureReason? Failure,
    GpuFallbackStage FailureStage,
    string CacheKey,
    ReferenceLookDecomposition Decomposition);

public sealed record MatchV4PixelExecutionResult(
    HighBitDepthImageBuffer Pixels,
    TimeSpan UploadTime,
    TimeSpan ComputeTime,
    TimeSpan ReadbackTime,
    ReferenceMatchV4BackendKind Backend,
    GpuFailureReason? Failure = null,
    int TileCount = 0)
{
    public TimeSpan ClassificationTime { get; init; }
}

public interface IMatchV4PixelBackend
{
    ReferenceMatchV4BackendKind Kind { get; }
    bool IsAvailable { get; }
    Task<MatchV4PixelExecutionResult> ExecuteAsync(
        HighBitDepthImageBuffer source,
        MatchV4ResolvedTransform transform,
        CancellationToken token = default);
}

/// <summary>CPU pixel-stage oracle. This is also the safe fallback for any GPU failure.</summary>
public sealed class MatchV4CpuPixelBackend : IMatchV4PixelBackend
{
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Cpu;
    public bool IsAvailable => true;

    public Task<MatchV4PixelExecutionResult> ExecuteAsync(HighBitDepthImageBuffer source, MatchV4ResolvedTransform transform, CancellationToken token = default)
    {
        transform = transform.Normalize();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var values = source.Rgb32.ToArray();
        MatchV4PixelApplication.Apply(values, transform, token);
        watch.Stop();
        var result = new HighBitDepthImageBuffer(source.Width, source.Height, values, source.SourceBitDepth,
            source.WorkingColorSpace, source.Orientation, source.Metadata);
        return Task.FromResult(new MatchV4PixelExecutionResult(result, TimeSpan.Zero, watch.Elapsed, TimeSpan.Zero, Kind));
    }
}

public static class MatchV4NumericalContract
{
    // Float GPU and double CPU share this guard around hard protection boundaries.
    public const double ProtectionBoundaryEpsilon = ProtectionClassifierContract.Epsilon;
}

public static class MatchV4PixelApplication
{
    public static void Apply(Span<float> values, MatchV4ResolvedTransform transform, CancellationToken token = default)
    {
        transform = transform.Normalize();
        var settings = transform.Settings;
        for (var index = 0; index < values.Length / 3; index++)
        {
            if ((index & 1023) == 0) token.ThrowIfCancellationRequested();
            var offset = index * 3;
            var lab = OklabColorSpace.FromSrgb(values[offset], values[offset + 1], values[offset + 2]);
            var zone = ProtectionClassifierContract.Classify(lab) & ProtectionClassifierContract.ZoneMask;
            var local = transform.RegionDeltas[zone];
            var weight = zone switch { 0 => 1 - Math.Clamp((lab.L - .2) / .2, 0, 1), 2 => Math.Clamp((lab.L - .6) / .2, 0, 1), _ => 1d };
            var delta = new OklabColor(
                transform.GlobalDelta.L * (1 - weight) + local.L * weight,
                transform.GlobalDelta.A * (1 - weight) + local.A * weight,
                transform.GlobalDelta.B * (1 - weight) + local.B * weight);
            var protection = Protection(lab, settings);
            var strength = transform.Strength * protection;
            var l = transform.KeepLuminance ? lab.L : Math.Clamp(lab.L + Math.Clamp(delta.L, -settings.MaximumLuminanceDisplacement, settings.MaximumLuminanceDisplacement) * strength, 0, 1);
            var a = lab.A + Math.Clamp(delta.A, -settings.MaximumChromaDisplacement, settings.MaximumChromaDisplacement) * strength;
            var b = lab.B + Math.Clamp(delta.B, -settings.MaximumChromaDisplacement, settings.MaximumChromaDisplacement) * strength;
            var rgb = OklabColorSpace.ToSrgbLinear(new(l, a, b));
            values[offset] = (float)rgb.R; values[offset + 1] = (float)rgb.G; values[offset + 2] = (float)rgb.B;
        }
    }

    private static double Protection(OklabColor c, ReferenceMatchV4Settings settings)
    {
        var neutral = Math.Clamp(1 - c.Chroma / .08, 0, 1);

        var skin = (ProtectionClassifierContract.Classify(c) & ProtectionClassifierContract.SkinBit) != 0;
        var highlight = Math.Clamp((c.L - .82) / .18, 0, 1);
        var shadow = Math.Clamp((.2 - c.L) / .2, 0, 1);
        return Math.Clamp(1 - neutral * settings.NeutralProtection - (skin ? settings.SkinProtection : 0) - highlight * settings.HighlightProtection - shadow * Math.Clamp(c.Chroma / .1, 0, 1) * settings.ShadowProtection, .08, 1);
    }
}

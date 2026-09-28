using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// Product-facing V4 seam. It deliberately accepts canonical high-precision source and reference
/// buffers, so the UI cannot accidentally route an 8-bit display proxy into the professional path.
/// The caller owns the reference buffer and must provide the same processing generation as the
/// frozen RAW target. Color Studio opts in explicitly; Stable continues to use Match v3.
/// </summary>
public enum ColorStudioMatchEngine { Stable, MatchV4Beta }

public enum MatchV4ExecutionMode { Auto, Cpu }

public sealed class MatchV4ProductExecutor
{
    private readonly ReferenceMatchV4Engine _engine;
    private readonly MatchV4PixelExecutor _pixelExecutor;

    public MatchV4ProductExecutor(ReferenceMatchV4Engine? engine = null, MatchV4PixelExecutor? pixelExecutor = null,
        GpuCapabilityInfo? capability = null)
    {
        _engine = engine ?? new();
        _pixelExecutor = pixelExecutor ?? new MatchV4PixelExecutor(new MatchV4CpuPixelBackend());
        Capability = capability ?? new("Unavailable", 0, 0, string.Empty, string.Empty, "None", false, false, false,
            "No validated GPU backend is registered.", GpuMemoryBudget.FromBytes(0));
    }

    public GpuCapabilityInfo Capability { get; }

    public MatchV4ResolvedAnalysis Resolve(
        FrozenRawMaster source, HighBitDepthImageBuffer reference, string referenceIdentity,
        ReferenceMatchV4Settings? settings = null, bool preferGpu = true, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceIdentity);
        settings ??= new();
        return _engine.ResolveHighPrecision(source.Image, reference, source.SourceSha256, referenceIdentity, settings, preferGpu, token);
    }

    public MatchV4ProductSession CreateSession(FrozenRawMaster source, HighBitDepthImageBuffer reference,
        string referenceIdentity, ReferenceMatchV4Settings? settings = null,
        MatchV4ExecutionMode mode = MatchV4ExecutionMode.Auto, CancellationToken token = default) =>
        new(this, source, Resolve(source, reference, referenceIdentity, settings, mode == MatchV4ExecutionMode.Auto, token));

    public async Task<ReferenceMatchV4HighPrecisionResult> ExecuteResolvedAsync(
        FrozenRawMaster source, HighBitDepthImageBuffer input, MatchV4ResolvedAnalysis analysis,
        bool preferGpu = true, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(analysis);
        await source.ValidateSourceAsync(token).ConfigureAwait(false);
        var pixel = await _pixelExecutor.ExecuteAsync(input, analysis.Transform, source.ProcessingGenerationId,
            preferGpu && !analysis.UsedCpuFallback, token).ConfigureAwait(false);
        return new(pixel.Pixels, pixel.Backend, analysis.UsedCpuFallback || pixel.UsedCpuFallback ||
            (preferGpu && pixel.Failure is not null),
            analysis.RepresentativeSourceCount, analysis.RepresentativeReferenceCount, 0, 0, analysis.CacheKey,
            pixel.Failure ?? analysis.Failure,
            pixel.Failure is null ? analysis.FailureStage : pixel.Failure == GpuFailureReason.BackendUnavailable
                ? GpuFallbackStage.Initialization : GpuFallbackStage.PixelApplication,
            analysis.Transform.TransformHash, source.ProcessingGenerationId);
    }

    public async Task<ReferenceMatchV4HighPrecisionResult> ExecuteAsync(
        FrozenRawMaster source,
        HighBitDepthImageBuffer reference,
        string referenceIdentity,
        ReferenceMatchV4Settings? settings = null,
        bool preferGpu = true,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceIdentity);
        await source.ValidateSourceAsync(token).ConfigureAwait(false);
        var result = _engine.Match(source.Image, reference, source.SourceSha256, referenceIdentity,
            settings, preferGpu, token, source.ProcessingGenerationId);
        if (result.ProcessingGenerationId != source.ProcessingGenerationId)
            throw new InvalidOperationException("Match V4 result belongs to a different processing generation.");
        return result;
    }
}

using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// Product-facing V4 seam. It deliberately accepts canonical high-precision source and reference
/// buffers, so the UI cannot accidentally route an 8-bit display proxy into the professional path.
/// The caller owns the reference buffer and must provide the same processing generation as the
/// frozen RAW target. This is an experimental opt-in seam until Color Studio exposes engine choice.
/// </summary>
public sealed class MatchV4ProductExecutor(ReferenceMatchV4Engine? engine = null)
{
    private readonly ReferenceMatchV4Engine _engine = engine ?? new();

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

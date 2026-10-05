using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>
/// One analysis, one frozen RAW generation. Display proxies and full-resolution TIFF export
/// execute the same resolved transform; backend failure never triggers a second analysis.
/// </summary>
public sealed class MatchV4ProductSession
{
    private readonly MatchV4ProductExecutor _executor;
    private readonly FrozenRawMaster _master;
    private readonly MatchV4ResolvedAnalysis _analysis;

    public MatchV4ProductSession(MatchV4ProductExecutor executor, FrozenRawMaster master,
        MatchV4ResolvedAnalysis analysis)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _analysis = analysis ?? throw new ArgumentNullException(nameof(analysis));
    }

    public Guid DecodeGenerationId => _master.DecodeGenerationId;
    public Guid ProcessingGenerationId => _master.ProcessingGenerationId;
    public string TransformHash => _analysis.Transform.TransformHash;
    public GpuCapabilityInfo Capability => _executor.Capability;

    public Task<ReferenceMatchV4HighPrecisionResult> PreviewAsync(HighBitDepthImageBuffer proxy,
        double strength, bool keepLuminance, MatchV4ExecutionMode mode, CancellationToken token = default) =>
        ExecuteAsync(proxy, strength, keepLuminance, mode, token);

    public Task<ReferenceMatchV4HighPrecisionResult> ProcessFullResolutionAsync(
        double strength, bool keepLuminance, MatchV4ExecutionMode mode, CancellationToken token = default) =>
        ExecuteAsync(_master.Image, strength, keepLuminance, mode, token);

    public async Task<(ReferenceMatchV4HighPrecisionResult Match, TiffExportResult Export)> ExportTiff16Async(
        string destination, double strength, bool keepLuminance, MatchV4ExecutionMode mode,
        CancellationToken token = default, ReadOnlyMemory<byte> outputIcc = default)
    {
        var matched = await ProcessFullResolutionAsync(strength, keepLuminance, mode, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var exported = await AtomicTiffWriter.WriteRgb48Async(destination, matched.Pixels,
            new(TiffBitDepth.Sixteen, IccProfile: outputIcc, Software: "Pixel Tart", Orientation: matched.Pixels.Orientation),
            token, overwrite: false).ConfigureAwait(false);
        return (matched, exported);
    }

    private Task<ReferenceMatchV4HighPrecisionResult> ExecuteAsync(HighBitDepthImageBuffer input,
        double strength, bool keepLuminance, MatchV4ExecutionMode mode, CancellationToken token)
    {
        if (!string.Equals(input.WorkingColorSpace, _master.Image.WorkingColorSpace, StringComparison.Ordinal) ||
            input.Orientation != _master.Orientation)
            throw new InvalidDataException("V4 preview/export input must retain the frozen master color space and orientation.");
        var execution = _analysis with { Transform = _analysis.Transform.WithExecution(strength, keepLuminance) };
        return _executor.ExecuteResolvedAsync(_master, input, execution, mode == MatchV4ExecutionMode.Auto, token);
    }
}

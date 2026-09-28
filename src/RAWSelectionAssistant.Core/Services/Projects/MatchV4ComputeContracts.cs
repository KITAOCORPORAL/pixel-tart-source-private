namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Platform-neutral compute contract. Implementations must not expose WPF, DX12 or shader types.</summary>
public interface IMatchV4ComputeBackend
{
    ReferenceMatchV4BackendKind Kind { get; }
    bool IsAvailable { get; }
    GpuCapabilityInfo Capability { get; }
    Task<MatchV4BackendResult> ExecuteAsync(
        IReadOnlyList<OklabColor> source,
        IReadOnlyList<OklabColor> reference,
        ReferenceMatchV4Settings settings,
        CancellationToken token = default);
}

/// <summary>Compatibility adapter for the existing engine. New platform backends should implement IMatchV4ComputeBackend.</summary>
public sealed class MatchV4BackendAdapter(IMatchV4ComputeBackend backend) : IColorMatchComputeBackend
{
    public ReferenceMatchV4BackendKind Kind => backend.Kind;
    public bool IsAvailable => backend.IsAvailable;
    public IReadOnlyList<OklabColor> Map(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference, ReferenceMatchV4Settings settings, CancellationToken token = default) =>
        backend.ExecuteAsync(source, reference, settings, token).GetAwaiter().GetResult().MappedSamples;
}

public sealed record MatchV4BackendResult(
    IReadOnlyList<OklabColor> MappedSamples,
    TimeSpan UploadTime,
    TimeSpan ComputeTime,
    TimeSpan ReadbackTime,
    GpuFailureReason? Failure = null,
    GpuFallbackStage FailureStage = GpuFallbackStage.None);

/// <summary>Common CPU implementation of the platform-neutral contract.</summary>
public sealed class MatchV4CpuBackend : IMatchV4ComputeBackend
{
    private readonly CpuColorMatchComputeBackend _inner = new();
    public ReferenceMatchV4BackendKind Kind => ReferenceMatchV4BackendKind.Cpu;
    public bool IsAvailable => true;
    public GpuCapabilityInfo Capability => new("CPU", 0, 0, string.Empty, string.Empty, "CPU", true, true, true, null, GpuMemoryBudget.FromBytes(0));
    public Task<MatchV4BackendResult> ExecuteAsync(IReadOnlyList<OklabColor> source, IReadOnlyList<OklabColor> reference, ReferenceMatchV4Settings settings, CancellationToken token = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var mapped = _inner.Map(source, reference, settings, token);
        started.Stop();
        return Task.FromResult(new MatchV4BackendResult(mapped, TimeSpan.Zero, started.Elapsed, TimeSpan.Zero));
    }
}

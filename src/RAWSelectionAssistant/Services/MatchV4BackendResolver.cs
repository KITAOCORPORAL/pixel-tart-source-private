using RAWSelectionAssistant.Core.Services.Projects;
using PixelTart.MatchV4.Dx12;

namespace RAWSelectionAssistant.Services;

/// <summary>Application composition seam for the Windows DX12 implementation.</summary>
public sealed class MatchV4BackendResolver
{
    public MatchV4BackendResolver()
    {
        var gpu = new Dx12ColorMatchComputeBackend();
        Capability = gpu.Capability;
        Executor = new MatchV4ProductExecutor(
            new ReferenceMatchV4Engine(gpu: new MatchV4BackendAdapter(gpu)),
            new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), gpu),
            Capability);
    }

    public GpuCapabilityInfo Capability { get; }
    public MatchV4ProductExecutor Executor { get; }
}

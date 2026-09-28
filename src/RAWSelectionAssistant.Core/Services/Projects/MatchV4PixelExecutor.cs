using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

public sealed record MatchV4PixelExecutorResult(
    HighBitDepthImageBuffer Pixels,
    ReferenceMatchV4BackendKind Backend,
    bool UsedCpuFallback,
    GpuFailureReason? Failure,
    string TransformHash,
    Guid? ProcessingGenerationId,
    TimeSpan UploadTime,
    TimeSpan ComputeTime,
    TimeSpan ReadbackTime);

/// <summary>Runs the resolved float32 pixel stage and preserves one transform/generation on fallback.</summary>
public sealed class MatchV4PixelExecutor(IMatchV4PixelBackend cpu, IMatchV4PixelBackend? gpu = null)
{
    public async Task<MatchV4PixelExecutorResult> ExecuteAsync(
        HighBitDepthImageBuffer source,
        MatchV4ResolvedTransform transform,
        Guid? processingGenerationId = null,
        bool preferGpu = true,
        CancellationToken token = default)
    {
        transform = transform.Normalize();
        var backend = preferGpu && gpu?.IsAvailable == true ? gpu : cpu;
        GpuFailureReason? failure = preferGpu && backend == cpu ? GpuFailureReason.BackendUnavailable : null;
        try
        {
            var result = await backend.ExecuteAsync(source, transform, token).ConfigureAwait(false);
            return new(result.Pixels, backend.Kind, false, result.Failure ?? failure, transform.TransformHash,
                processingGenerationId, result.UploadTime, result.ComputeTime, result.ReadbackTime);
        }
        catch (OperationCanceledException) { throw; }
        catch (OutOfMemoryException) when (backend.Kind == ReferenceMatchV4BackendKind.Gpu)
        { failure = GpuFailureReason.OutOfMemory; }
        catch when (backend.Kind == ReferenceMatchV4BackendKind.Gpu)
        { failure = GpuFailureReason.ExecutionFailure; }

        var fallback = await cpu.ExecuteAsync(source, transform, token).ConfigureAwait(false);
        return new(fallback.Pixels, ReferenceMatchV4BackendKind.Cpu, true, failure, transform.TransformHash,
            processingGenerationId, fallback.UploadTime, fallback.ComputeTime, fallback.ReadbackTime);
    }
}

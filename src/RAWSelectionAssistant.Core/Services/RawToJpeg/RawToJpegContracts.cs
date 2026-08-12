using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.Core.Services.RawToJpeg;

public interface IRawDecoder
{
    RawDecoderCapability GetCapability();
    Task<RawDecodedImage> DecodeAsync(string sourcePath, RawToJpegOptions options, CancellationToken cancellationToken = default);
}

public interface IRawJpegEncoder
{
    Task EncodeAsync(RawDecodedImage image, Stream destination, RawToJpegOptions options, CancellationToken cancellationToken = default);
}

public interface IRawToJpegSafeConversionService
{
    Task<RawToJpegBatchResult> ConvertAsync(Guid taskId, RawToJpegBatchRequest request,
        IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
        Func<RawToJpegItemResult, Task>? itemCompleted = null,
        CancellationToken cancellationToken = default);
}

public interface IRawToJpegRequestStore
{
    void Register(Guid taskId, RawToJpegBatchRequest request);
    bool TryGet(Guid taskId, out RawToJpegRecoveryCheckpoint checkpoint);
    void Update(Guid taskId, RawToJpegRecoveryCheckpoint checkpoint);
    void Remove(Guid taskId);
}

public interface IRawToJpegTaskCoordinator : ITaskCompletionStateProvider
{
    RawDecoderCapability GetCapability();
    Task<Guid> StartAsync(RawToJpegBatchRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default);
}

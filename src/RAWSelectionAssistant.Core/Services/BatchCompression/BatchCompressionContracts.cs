using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.BatchCompression;

public interface IBatchCompressionEncoder
{
    Task EncodeAsync(string sourcePath, Stream destination, BatchCompressionOptions options, CancellationToken cancellationToken = default);
    Task VerifyDecodableAsync(string imagePath, CancellationToken cancellationToken = default);
}

public interface IBatchCompressionService
{
    Task<BatchCompressionResult> CompressAsync(
        Guid taskId,
        BatchCompressionRequest request,
        IProgress<(double Progress, string CurrentFile, TaskResultSummary Summary)>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IBatchCompressionRequestStore
{
    void Register(Guid taskId, BatchCompressionRequest request);
    bool TryGet(Guid taskId, out BatchCompressionRecoveryCheckpoint checkpoint);
    void Update(Guid taskId, BatchCompressionRecoveryCheckpoint checkpoint);
    void Remove(Guid taskId);
}

public interface IBatchCompressionTaskCoordinator
{
    Task<Guid> StartAsync(BatchCompressionRequest request, CancellationToken cancellationToken = default);
    Task WaitForCompletionAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid taskId, CancellationToken cancellationToken = default);
}

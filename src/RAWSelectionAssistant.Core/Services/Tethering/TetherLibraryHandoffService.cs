using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.Tethering;

public sealed class TetherLibraryHandoffService(IdentityBridgeRepository bridge,
    Func<CancellationToken, Task<AssetLibraryContainerDescriptor?>> currentLibrary, ITetherAnnotationRepository? annotations = null, TetherCaptureContextStore? contexts = null, PlanningProjectStore? planning = null)
{
    public async Task<string> DescribePendingAsync()
    {
        var operations = await bridge.ListIncompleteOperationsAsync();
        return operations.Count == 0 ? "没有待处理交接操作。" : string.Join(Environment.NewLine,
            operations.Select(x => $"{x.OperationId:D} | {x.State} | {x.LastErrorCode ?? "等待执行"}")) +
            "\n失败：使用顶层核对重试。内容/身份冲突：保留原记录，请核对素材库源文件；不会自动改连。";
    }
    public async Task<int> RetryFailedAsync(CancellationToken token = default)
    {
        var count = 0;
        foreach (var operation in await bridge.ListIncompleteOperationsAsync(token))
            if (operation.OperationType == "TetherToLibrary" && operation.State == CrossModuleOperationState.Failed &&
                await bridge.RetryOperationAsync(operation.OperationId, operation.AttemptCount, token)) count++;
        return count;
    }
    public async Task<TetherAssetLibraryLink?> ResolveLinkAsync(Guid tetherId, CancellationToken token = default)
    {
        var library = await currentLibrary(token);
        return library is null ? null : await bridge.GetLinkAsync(tetherId, library.LibraryId, token);
    }
    public async Task<string?> ResolveAnnotationAsync(Guid tetherId, AnnotationConflictChoice choice, CancellationToken token = default)
    {
        var library = await currentLibrary(token);
        if (library is null || annotations is null) return "LIBRARY_OFFLINE";
        var link = await bridge.GetLinkAsync(tetherId, library.LibraryId, token);
        if (link is null || link.State != IdentityBridgeState.Ready) return "IDENTITY_NOT_READY";
        var captured = await annotations.GetByAssetAsync(tetherId, token);
        if (captured is null) return "ANNOTATION_MISSING";
        await using var lease = await AssetLibraryWriteLease.AcquireForHandoffAsync(library, token);
        await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
        var item = await repository.GetAssetAsync(link.AssetId, token);
        if (item is null) return "ASSET_MISSING";
        var store = new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath));
        var expected = await store.ReadAsync(item.AssetId, token);
        var proposal = TetherAnnotationMerge.Propose(item, captured, choice);
        var written = await store.CompareExchangeAsync(expected, new(item.AssetId, proposal.Rating, proposal.ColorLabel ?? "", proposal.PhotographerNote), token);
        if (!written.Applied) return "ANNOTATION_CONCURRENT_CONFLICT";
        await annotations.UpsertAsync(captured with { Rating = proposal.Rating, ColorLabel = proposal.ColorLabel,
            PhotographerNote = proposal.PhotographerNote, ExpectedLibraryRevision=written.Current.Revision, UpdatedAtUtc = DateTimeOffset.UtcNow }, token);
        return null;
    }
    public async Task<string?> HandoffAsync(TetherAssetRecord asset, CancellationToken token = default)
    {
        if (asset.StabilityState != TetherStabilityState.Stable) return "SOURCE_NOT_STABLE";
        var library = await currentLibrary(token);
        if (library is null) return "LIBRARY_OFFLINE";
        // Only the copy service's committed path is eligible; never infer a destination.
        var path = asset.ProjectCopyPath ?? asset.SourcePath;
        if (!File.Exists(path)) return "SOURCE_MISSING";
        string hash;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        hash = Convert.ToHexString(await SHA256.HashDataAsync(input, token)).ToLowerInvariant();
        var key = $"tether:{asset.Id:D}:{library.LibraryId:D}";
        using var execution = bridge.TryAcquireExecutionLock(key);
        if (execution is null) return "HANDOFF_RUNNING";
        var operation = await bridge.BeginOperationAsync(key,
            "TetherToLibrary", "TetherAsset", asset.Id, library.LibraryId, hash, token);
        if (operation.State == CrossModuleOperationState.Conflict) return "IDENTITY_CONFLICT";
        if (operation.State == CrossModuleOperationState.Ready)
        {
            var persisted = await bridge.GetLinkAsync(asset.Id, library.LibraryId, token);
            if (persisted is null || persisted.State != IdentityBridgeState.Ready) return "IDENTITY_CONFLICT";
            await using var check = new SqliteAssetLibraryRepository(library.DatabasePath);
            var resolution = await new AssetLibraryIdentityResolver(library, check).ResolveAsync(new(library.LibraryId, persisted.AssetId, hash), token);
            return resolution.Status == AssetResolutionStatus.Available ? null : "SOURCE_" + resolution.Status.ToString().ToUpperInvariant();
        }
        if (operation.State == CrossModuleOperationState.Failed) return operation.LastErrorCode ?? "HANDOFF_FAILED_RETRY_REQUIRED";
        if (operation.State == CrossModuleOperationState.Running)
        {
            // Exclusive OS file handle proves no live handoff holds this operation. Recover crash residue.
            if (!await bridge.RetryOperationAsync(operation.OperationId, operation.AttemptCount, token)) return "HANDOFF_STALE";
        }
        var claim = await bridge.TryClaimOperationAsync(operation.OperationId, token);
        if (claim is null) return "HANDOFF_RUNNING";
        try
        {
            await using var lease = await AssetLibraryWriteLease.AcquireForHandoffAsync(library, token);
            await using var repository = new SqliteAssetLibraryRepository(library.DatabasePath);
            await repository.InitializeAsync(token);
            await using (var checkConnection = await new AssetLibraryDatabase(library.DatabasePath).OpenConnectionAsync(cancellationToken: token))
            await using (var checkCommand = checkConnection.CreateCommand())
            {
                checkCommand.CommandText = "SELECT ContentHash FROM AssetItems WHERE NormalizedSourcePath=$path AND ContentHash IS NOT NULL;";
                checkCommand.Parameters.AddWithValue("$path",(OperatingSystem.IsWindows() ? Path.GetFullPath(path).ToUpperInvariant() : Path.GetFullPath(path)));
                await using var reader = await checkCommand.ExecuteReaderAsync(token);
                while(await reader.ReadAsync(token))
                    if (!string.Equals(reader.GetString(0),hash,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SOURCE_CHANGED");
            }
            var result = (await repository.ImportWithIdentitiesAsync([new(path, ComputeContentHash: true)], token)).Items.Single();
            if (result.AssetId == Guid.Empty) throw new InvalidOperationException("IMPORT_AMBIGUOUS");
            var resolved = await new AssetLibraryIdentityResolver(library, repository).ResolveAsync(new(library.LibraryId, result.AssetId, hash), token);
            if (resolved.Status != AssetResolutionStatus.Available) throw new InvalidOperationException("SOURCE_" + resolved.Status.ToString().ToUpperInvariant());
            if (asset.ProjectId is Guid projectId)
                await repository.SaveProjectAssetLinkAsync(new(projectId, result.AssetId, "TetherCapture", asset.ReadyAtUtc ?? asset.FirstSeenAtUtc), token);
            if (contexts is not null && planning is not null && await contexts.LoadAsync(asset.Id, token) is { CurrentShotId: not null } frozen)
            {
                if (frozen.ProjectId != asset.ProjectId) throw new InvalidOperationException("CAPTURE_CONTEXT_CONFLICT");
                await planning.AddCaptureRelationAsync(frozen.BindCapture(library.LibraryId, result.AssetId, asset.ReadyAtUtc ?? asset.FirstSeenAtUtc), token);
                if (frozen.BookingId is Guid booking) await repository.SaveBookingAssetLinkAsync(new(booking, result.AssetId, asset.ReadyAtUtc ?? asset.FirstSeenAtUtc), token);
            }
            var link = await bridge.SaveLinkAsync(asset.Id, library.LibraryId, result.AssetId, hash, IdentityBridgeState.Ready, cancellationToken: token);
            if (link.State != IdentityBridgeState.Ready) throw new InvalidOperationException("IDENTITY_CONFLICT");
            if (annotations is not null && await annotations.GetByAssetAsync(asset.Id, token) is { } annotation)
            {
                var metadata = await repository.GetAssetAsync(result.AssetId, token) ?? throw new InvalidOperationException("ASSET_MISSING");
                var proposal = TetherAnnotationMerge.Propose(metadata, annotation);
                if (proposal.Conflicts.Count != 0) throw new InvalidOperationException("ANNOTATION_CONFLICT");
                var expected=new PhotographerAnnotation(metadata.AssetId,metadata.Rating,metadata.ColorLabel,metadata.Comment,metadata.AnnotationRevision);
                var imported=await new PhotographerAnnotationStore(new AssetLibraryDatabase(library.DatabasePath)).CompareExchangeAsync(expected,expected with{Rating=proposal.Rating,Color=proposal.ColorLabel??"",Note=proposal.PhotographerNote},token);
                if(!imported.Applied)throw new InvalidOperationException("ANNOTATION_CONCURRENT_CONFLICT");            }
            if (!await bridge.CompleteClaimAsync(claim, result.AssetId, token)) return "HANDOFF_STALE";
            return null;
        }
        catch (OperationCanceledException)
        { await bridge.FailClaimAsync(claim, "HANDOFF_CANCELLED", CancellationToken.None); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Import may already have committed. Retrying finds the same AssetId and repairs the product link.
            var code = ex is InvalidOperationException ? ex.Message : "HANDOFF_IO_FAILED";
            await bridge.FailClaimAsync(claim, code, CancellationToken.None);
            return code;
        }
    }
}

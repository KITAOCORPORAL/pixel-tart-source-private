using System.Security.Cryptography;

namespace RAWSelectionAssistant.Core.Services.AssetLibrary;

/// <summary>
/// Resolves an existing portable-library identity without importing, copying or
/// mutating either the library database or the source file.
/// </summary>
public sealed class AssetLibraryIdentityResolver(
    AssetLibraryContainerDescriptor library,
    IAssetLibraryRepository repository)
{
    public async Task<AssetResolutionResult> ResolveAsync(AssetResolutionRequest request,CancellationToken cancellationToken=default)
    {
        ValidateRequest(request);
        if(request.LibraryId==library.LibraryId && File.Exists(library.DatabasePath))
        {
            if(await repository.GetAssetAsync(request.AssetId,cancellationToken) is null) return Result(AssetResolutionStatus.Missing,request,message:"源素材身份不存在，未使用历史映射。");
            var mapped=await CrossLibraryRelocation.ResolveAsync(library,request,cancellationToken);
            if(mapped is not null)return mapped;
        }
        return await ResolveLocalAsync(request,cancellationToken);
    }
    public async Task<AssetResolutionResult> ResolveLocalAsync(
        AssetResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        if (request.LibraryId != library.LibraryId)
            return Result(AssetResolutionStatus.Missing, request, message: "素材引用不属于当前素材库。");

        if (!File.Exists(library.DatabasePath)) return Result(AssetResolutionStatus.Offline, request, message: "素材库数据库离线。");
        var asset = await repository.GetAssetAsync(request.AssetId, cancellationToken).ConfigureAwait(false);
        if (asset is null)
            return Result(AssetResolutionStatus.Missing, request, message: "素材库中不存在该素材身份。");

        var paths = new[] { asset.ManagedCopyPath, asset.SourcePath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();
        if (paths.Length == 0)
            return Result(AssetResolutionStatus.Offline, request, message: "素材记录仍在，但当前没有可读取的文件位置。");

        var expected = request.NormalizedExpectedContentHash ?? NormalizeHash(asset.ContentHash);
        var candidates = new List<(string Path, string Hash)>();
        foreach (var path in paths)
            candidates.Add((path, await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false)));

        if (expected is not null)
        {
            var matches = candidates.Where(candidate => string.Equals(candidate.Hash, expected, StringComparison.Ordinal)).ToArray();
            if (matches.Length > 0)
                return Result(AssetResolutionStatus.Available, request, matches[0].Path, matches[0].Hash);
            return Result(AssetResolutionStatus.Changed, request, candidates[0].Path, candidates[0].Hash,
                "可读取文件的内容与素材身份记录不一致。");
        }

        var distinctHashes = candidates.Select(candidate => candidate.Hash).Distinct(StringComparer.Ordinal).ToArray();
        if (distinctHashes.Length > 1)
            return Result(AssetResolutionStatus.Ambiguous, request, message: "源路径与托管副本内容不一致，无法自动选择。");
        return Result(AssetResolutionStatus.Available, request, candidates[0].Path, candidates[0].Hash);
    }

    private static void ValidateRequest(AssetResolutionRequest request)
    {
        if (request.LibraryId == Guid.Empty) throw new ArgumentException("素材库身份不能为空。", nameof(request));
        if (request.AssetId == Guid.Empty) throw new ArgumentException("素材身份不能为空。", nameof(request));
        if (request.NormalizedExpectedContentHash is { } hash &&
            (hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
            throw new ArgumentException("ExpectedContentHash 必须是完整 SHA-256。", nameof(request));
    }

    private static string? NormalizeHash(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().ToLowerInvariant();

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
    }

    private static AssetResolutionResult Result(
        AssetResolutionStatus status,
        AssetResolutionRequest request,
        string? path = null,
        string? hash = null,
        string? message = null) =>
        new(status, request.LibraryId, request.AssetId, path, hash, message);
}

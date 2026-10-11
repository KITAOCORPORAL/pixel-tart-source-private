using RAWSelectionAssistant.Core.Services.AssetLibrary;
using System.Security.Cryptography;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private async Task<bool> ValidateTargetIdentityAsync(ReferenceTargetItem target, CancellationToken token)
    {
        if (target.LibraryId is Guid libraryId && target.AssetId is Guid assetId)
        {
            await using var repository = OpenMetadataRepository(target);
            if (repository is null) { target.Error = "素材库离线，调整已保留"; return false; }
            try
            {
                var root = Directory.GetParent(Path.GetDirectoryName(repository.DatabasePath)!)?.FullName;
                if (root is null) { target.Error = "素材库身份不可解析"; return false; }
                var library = string.Equals(Path.GetExtension(root), ".ptlibrary", StringComparison.OrdinalIgnoreCase)
                    ? await new AssetLibraryContainerService().OpenAsync(root, token)
                    : new AssetLibraryContainerDescriptor(new Guid(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(repository.DatabasePath)))[..16]),
                        "Legacy",Path.GetDirectoryName(repository.DatabasePath)!,repository.DatabasePath,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"","","Reference");
                var result = await new AssetLibraryIdentityResolver(library, repository).ResolveAsync(new(libraryId, assetId, target.SourceHash), token);
                if (result.Status != AssetResolutionStatus.Available) { target.Error = result.Message ?? result.Status.ToString(); return false; }
                if (!string.Equals(Path.GetFullPath(target.Path), result.ResolvedPath, StringComparison.OrdinalIgnoreCase))
                { target.Path = result.ResolvedPath!; target.RawMaster = null; target.SourceThumbnail = null; target.Thumbnail = null; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { target.Error = "素材库离线或身份清单无效"; return false; }
        }
        if (!File.Exists(target.Path)) { target.Error = "源文件缺失或离线"; return false; }
        await using var stream = new FileStream(target.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
        if (target.SourceHash is not null && !string.Equals(target.SourceHash.Trim(), hash, StringComparison.OrdinalIgnoreCase))
        { target.Error = "源文件内容已变化，未替换历史照片"; return false; }
        target.ObservedSourceHash = hash;
        return true;
    }
}

using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace PixelTart.Modules.AssetLibrary;
public sealed partial class AssetLibraryViewModel
{
    public async Task<AssetResolutionResult> ResolveStableAssetAsync(Guid libraryId, Guid assetId, string? hash = null)
    {
        if (libraryId != CanvasLibraryId) return new(AssetResolutionStatus.Missing, libraryId, assetId, null, null, "素材库身份不匹配");
        var root = System.IO.Directory.GetParent(System.IO.Path.GetDirectoryName(_databasePath)!)?.FullName;
        if (root is null) return new(AssetResolutionStatus.Offline, libraryId, assetId, null, null, "素材库离线");
        try
        {
            var descriptor = string.Equals(System.IO.Path.GetExtension(root), ".ptlibrary", StringComparison.OrdinalIgnoreCase)
                ? await new AssetLibraryContainerService().OpenAsync(root)
                : new AssetLibraryContainerDescriptor(CanvasLibraryId, "Legacy", System.IO.Path.GetDirectoryName(_databasePath)!, _databasePath,
                    DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"","","Reference");
            return await new AssetLibraryIdentityResolver(descriptor, _repository).ResolveAsync(new(libraryId, assetId, hash));
        }
        catch (System.IO.IOException) { return new(AssetResolutionStatus.Offline, libraryId, assetId, null, null, "素材库离线"); }
    }
}

using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace PixelTart.Modules.AssetLibrary;
public sealed partial class AssetLibraryViewModel
{
    public async Task RelocateSelectedAssetAsync()
    {
        var selected=CaptureOrderedSelection();if(selected.Count!=1){Status="请选择一个待重新定位素材。";return;}
        await RelocateAssetAsync(CanvasLibraryId,selected[0].AssetId,selected[0].ContentHash);
    }
    public async Task RelocateAssetAsync(Guid libraryId,Guid assetId,string? hash=null)
    {
        if(libraryId!=CanvasLibraryId){Status="请先明确打开引用所属素材库。";return;}
        var asset=await _repository.GetAssetAsync(assetId);if(asset is null){Status="素材身份不存在。";return;}
        var dialog=new Microsoft.Win32.OpenFileDialog{Title="按内容 hash 重新定位；同名不会自动确认"};if(dialog.ShowDialog()!=true)return;
        var root=System.IO.Directory.GetParent(System.IO.Path.GetDirectoryName(_databasePath)!)?.FullName;
        if(root is null){Status="素材库离线。";return;}
        var descriptor=string.Equals(System.IO.Path.GetExtension(root),".ptlibrary",StringComparison.OrdinalIgnoreCase)
            ? await new AssetLibraryContainerService().OpenAsync(root)
            :new AssetLibraryContainerDescriptor(CanvasLibraryId,"Legacy",System.IO.Path.GetDirectoryName(_databasePath)!,_databasePath,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,"","","Reference");
        if(System.Windows.MessageBox.Show("是否将候选文件作为当前库内的重新定位？选“否”可明确选择另一素材库建立映射。","素材定位方式",System.Windows.MessageBoxButton.YesNo)==System.Windows.MessageBoxResult.No)
        {
            var folder=new Microsoft.Win32.OpenFolderDialog{Title="明确选择目标 .ptlibrary"};if(folder.ShowDialog()!=true)return;
            var target=await new AssetLibraryContainerService().OpenAsync(folder.FolderName);
            await using var targetRepo=new SqliteAssetLibraryRepository(target.DatabasePath);
            var candidates=new List<RAWSelectionAssistant.Core.Models.AssetItem>();string? cursor=null;
            do{var page=await targetRepo.QueryAsync(new(PageSize:500,Cursor:cursor));candidates.AddRange(page.Items.Where(x=>string.Equals(System.IO.Path.GetFullPath(x.SourcePath),System.IO.Path.GetFullPath(dialog.FileName),StringComparison.OrdinalIgnoreCase)));cursor=page.NextCursor;}while(cursor is not null);
            if(candidates.Count!=1){Status="目标库中候选身份缺失或歧义；请先在目标库确认素材。";return;}
            var mapped=await CrossLibraryRelocation.LinkAsync(descriptor,new(CanvasLibraryId,assetId,hash??asset.ContentHash),target,candidates[0].AssetId);
            Status=mapped.Message??"已建立可追踪跨库映射，原素材身份保留。";return;
        }        var result=await new AssetRelocationService(descriptor).RelocateAsync(new(CanvasLibraryId,assetId,hash??asset.ContentHash),dialog.FileName);
        if(result.Status==AssetResolutionStatus.Available)await RefreshAsync();
        Status=result.Message;
    }
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

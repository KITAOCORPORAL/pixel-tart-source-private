using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Utilities;
namespace PixelTart.Modules.AssetLibrary;
public sealed partial class AssetLibraryViewModel
{
    private AsyncCommand? _openCapturedPage;
    public AsyncCommand OpenCapturedPageCommand => _openCapturedPage ??= new AsyncCommand(OpenCapturedPageAsync, () => true);
    public async Task<AssetImportIdentity> ImportBrowserImageAsync(string source, string managedRoot, CancellationToken token)
    {
        if (!IsReady) throw new InvalidOperationException("素材库尚未就绪。");
        var result = await _repository.ImportWithIdentitiesAsync([new(source, AssetImportMode.ManagedCopy, managedRoot, true)], token);
        var identity = result.Items.Single();
        if (identity.AssetId == Guid.Empty || identity.IsMissing || identity.Disposition is AssetImportIdentityDisposition.Ambiguous or AssetImportIdentityDisposition.Missing)
            throw new InvalidDataException("素材身份冲突或原图不可用，未确认入库。");
        var asset=await _repository.GetAssetAsync(identity.AssetId,token) ?? throw new InvalidDataException("未找到入库素材。");
        if(asset.IsArchived || (await _repository.ListTrashEntriesAsync(token)).Any(x=>x.AssetId==asset.AssetId))
            throw new InvalidDataException("已有相同素材处于归档或回收状态，请先在桌面处理，未另建重复素材。");
        var path=asset.ManagedCopyPath ?? asset.SourcePath;
        if(!File.Exists(path))throw new InvalidDataException("已有素材文件不可用，未返回成功。");
        await using var stream=File.OpenRead(path);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
        if(!hash.Equals(identity.ContentHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("素材文件与内容身份不一致。");
        return identity;
    }
    public Task SaveBrowserProjectLinkAsync(Guid project, Guid asset, CancellationToken token) =>
        _repository.SaveProjectAssetLinkAsync(new(project,asset,"reference",DateTimeOffset.UtcNow),token);
    public async Task<bool> HasBrowserProjectLinkAsync(Guid project, Guid asset, CancellationToken token) =>
        (await _repository.ListProjectAssetLinksAsync(assetId:asset,projectId:project,cancellationToken:token)).Any();
    public async Task<AssetItem?> GetBrowserAssetAsync(Guid id,CancellationToken token)
    {
        var asset=await _repository.GetAssetAsync(id,token);
        if(asset is null||asset.IsArchived||(await _repository.ListTrashEntriesAsync(token)).Any(x=>x.AssetId==id))return null;
        var path=asset.ManagedCopyPath??asset.SourcePath;if(!File.Exists(path))return null;
        await using var stream=File.OpenRead(path);var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
        return hash.Equals(asset.ContentHash,StringComparison.OrdinalIgnoreCase)?asset:null;
    }
    public async Task RevealBrowserAssetAsync(Guid id, CancellationToken token)
    {
        var asset=await _repository.GetAssetAsync(id,token) ?? throw new InvalidDataException("素材已不存在。");
        ClearFilters(); SearchText=asset.DisplayName;
        if(RefreshCommand.CanExecute(null)){RefreshCommand.Execute(null);await RefreshCommand.ExecutionTask;}
        var visible=Assets.FirstOrDefault(x=>x.AssetId==id) ?? asset;
        SyncSelection([visible]);Status="浏览器图片已入库，可在右侧打开原网页。";
    }
    private async Task OpenCapturedPageAsync()
    {
        if(SelectedAsset is null){Status="请选择一张采集素材。";return;}
        var journal=new BrowserCaptureJournal(Path.Combine(AppDataPaths.Root,"BrowserCapture","captures.sqlite"));
        var record=await journal.LatestSourceAsync(CanvasLibraryId,SelectedAsset.AssetId);
        if(record is null || !Uri.TryCreate(record.PageUrl,UriKind.Absolute,out var page) ||
           page.Scheme is not ("http" or "https") || page.UserInfo.Length!=0){Status="该素材没有可打开的采集原网页。";return;}
        try{Process.Start(new ProcessStartInfo(page.AbsoluteUri){UseShellExecute=true});Status="已打开采集原网页："+record.PageTitle;}
        catch(Exception e)when(e is IOException or System.ComponentModel.Win32Exception){Status="无法打开原网页，请检查默认浏览器。";}
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.BrowserCapture.Contracts;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Database;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services.BrowserCapture;
using RAWSelectionAssistant.Views;
namespace RAWSelectionAssistant;
public partial class App
{
    private CapturePipeServer? _capturePipe;
    private string? _captureExtensionOrigin;
    private readonly SemaphoreSlim _captureGate=new(1,1);
    private void StartBrowserCaptureBridge()
    {
        var name=Environment.GetEnvironmentVariable("GUANGJING_CAPTURE_PIPE");var id=Environment.GetEnvironmentVariable("GUANGJING_CAPTURE_EXTENSION_ID");
        if(string.IsNullOrEmpty(name)||id is null||id.Length!=32||id.Any(c=>c<'a'||c>'p'))return;
        _captureExtensionOrigin="chrome-extension://"+id+"/";
        _capturePipe=new CapturePipeServer(name,(request,token)=>Dispatcher.InvokeAsync(()=>HandleBrowserCaptureAsync(request,token)).Task.Unwrap());
    }
    private async Task<AssetLibraryWorkspaceHost> CaptureHost(CancellationToken token)
    {
        _mainViewModel!.NavigateCommand.Execute("AssetLibrary");
        for(var i=0;i<100;i++){
            token.ThrowIfCancellationRequested();
            if(MainWindow is MainWindow main && main.FindName("AssetLibraryWorkspace")is ModuleWorkspaceHost slot && slot.Content is AssetLibraryWorkspaceHost host)return host;
            await Dispatcher.Yield(DispatcherPriority.Loaded);await Task.Delay(50,token);
        }
        throw new IOException("素材库页面尚未就绪，请稍后重试。");
    }
    private async Task<IReadOnlyList<AssetLibraryContainerDescriptor>> CaptureLibraries(CancellationToken token)
    {
        var result=new List<AssetLibraryContainerDescriptor>();
        foreach(var entry in _mainViewModel!.Settings.AssetLibraryPortable.RecentLibraries.ToArray()){
            try{var d=await new AssetLibraryContainerService().OpenAsync(entry.ContainerPath,token);if(d.LibraryId==entry.LibraryId)result.Add(d);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidDataException){}
        }
        return result.GroupBy(x=>x.LibraryId).Where(g=>g.Select(x=>Path.GetFullPath(x.ContainerPath)).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1).Select(g=>g.First()).ToArray();
    }
    private static string CaptureUrl(string value)
    {
        if(value.Length>4096||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme is not("http"or"https")||uri.UserInfo.Length!=0)throw new InvalidDataException("采集来源地址无效。");
        return uri.AbsoluteUri;
    }
    private async Task<CaptureReply> HandleBrowserCaptureAsync(CaptureRequest request,CancellationToken token)
    {
        if(request.ExtensionOrigin!=_captureExtensionOrigin)return new(false,"OriginRejected","扩展身份不匹配。");
        await _captureGate.WaitAsync(token);
        try{
            if(_mainViewModel is null)return new(false,"DesktopUnavailable","桌面尚未就绪。",Retryable:true);
            var libraries=await CaptureLibraries(token);
            var projects=await new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)).ListAsync(token);
            await using var tray=new SqliteInspirationTrayService(Path.Combine(AppDataPaths.Root,"InspirationTray","tray.sqlite"));
            var boards=await tray.ListCollectionsAsync(token);
            if(request.Operation=="destinations")return new(true,"Ready","请选择明确目的地。",new(libraries.Select(x=>new CaptureLibraryOption(x.LibraryId,x.DisplayName)).ToArray(),projects.Select(x=>new CaptureOption(x.Id,x.Name)).ToArray(),boards.Select(x=>new CaptureOption(x.CollectionId,x.Name)).ToArray()));
            if(request.Operation!="capture"||request.Capture is not{} capture)return new(false,"InvalidRequest","不支持此采集请求。");
            if(capture.Destination is null||capture.PageUrl is null||capture.ImageUrl is null||capture.PageTitle is null||capture.FileName is null||capture.ContentSha256 is null||capture.ImageBase64 is null)throw new InvalidDataException("采集请求缺少必要字段。");
            var target=capture.Destination;
            var library=libraries.SingleOrDefault(x=>x.LibraryId==target.LibraryId);
            bool Valid()=>library is not null && (target.ProjectId is null||projects.Any(p=>p.Id==target.ProjectId)) && (target.BoardId is null||boards.Any(b=>b.CollectionId==target.BoardId && (b.ProjectId is null||projects.Any(p=>p.Id==b.ProjectId)) && (target.ProjectId is null||b.ProjectId is null||b.ProjectId==target.ProjectId)));
            if(!Valid())return new(false,"DestinationInvalid","原目的地已失效，请重新选择；没有改送其他项目。");
            if(capture.CaptureId==Guid.Empty||target.LibraryId==Guid.Empty||target.ProjectId==Guid.Empty||target.BoardId==Guid.Empty||capture.PageTitle.Length>1024||capture.CapturedAtUtc==default||capture.CapturedAtUtc>DateTimeOffset.UtcNow.AddMinutes(5))throw new InvalidDataException("采集身份或时间无效。");
            var pageUrl=CaptureUrl(capture.PageUrl);var imageUrl=CaptureUrl(capture.ImageUrl);if(capture.RenderedImageUrl is{} rendered)CaptureUrl(rendered);
            if(capture.ImageBase64.Length>CaptureWire.MaximumImageBytes*4L/3+8)return new(false,"ImageTooLarge","首版单图上限16 MiB，未缩小或替换图片。");
            byte[] bytes;try{bytes=Convert.FromBase64String(capture.ImageBase64);}catch(FormatException){throw new InvalidDataException("图片字节格式无效。");}
            if(bytes.Length==0||bytes.Length>CaptureWire.MaximumImageBytes)throw new InvalidDataException("图片大小超出首版范围。");
            var extension=bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255?".jpg":bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})?".png":null;
            if(extension is null)return new(false,"UnsupportedOriginal","首版仅接收JPEG或PNG原始字节，未用缩略图替代。");
            using(var stream=new MemoryStream(bytes,false)){
                var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=decoder.Frames[0];
                if(frame.PixelWidth<=0||frame.PixelHeight<=0||(long)frame.PixelWidth*frame.PixelHeight>32_000_000)throw new InvalidDataException("图片像素尺寸超出首版范围。");
            }
            var hash=Convert.ToHexString(SHA256.HashData(bytes));if(!hash.Equals(capture.ContentSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("图片字节摘要不一致。");
            var fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{capture.CaptureId,target,pageUrl,imageUrl,capture.PageTitle,capture.CapturedAtUtc,hash,capture.FileName,capture.RenderedImageUrl,capture.SourceSelection},CaptureWire.Json)));
            var journal=new BrowserCaptureJournal(Path.Combine(AppDataPaths.Root,"BrowserCapture","captures.sqlite"));
            var record=new BrowserCaptureRecord(capture.CaptureId,target.LibraryId,target.ProjectId,target.BoardId,pageUrl,imageUrl,capture.PageTitle,capture.CapturedAtUtc,hash);
            var saved=await journal.ReserveAsync(record,fingerprint,token);
            var host=await CaptureHost(token);await host.SwitchToContainerAsync(library!.ContainerPath);
            if(host.CurrentDescriptor?.LibraryId!=target.LibraryId||host.CurrentPage is not{} page||!page.ViewModel.IsReady)return new(false,"DesktopBusy","目标素材库尚未打开，采集仍待处理。",Retryable:true);
            var vm=page.ViewModel;
            if(saved.AssetId is Guid previous){
                var existing=await vm.GetBrowserAssetAsync(previous,token);if(existing is null||existing.IsArchived||!string.Equals(existing.ContentHash,saved.ContentHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("已入库素材不可用或内容已变化，请在桌面处理后重新采集。");
                if(target.ProjectId is Guid expectedProject && !await vm.HasBrowserProjectLinkAsync(expectedProject,previous,token))throw new InvalidDataException("原项目关联已变化，请明确重新采集，未伪造成功回执。");
                if(target.BoardId is Guid expectedBoard && !(await tray.ListCollectionEntriesAsync(expectedBoard,token)).Any(x=>x.Reference.LibraryId==target.LibraryId&&x.Reference.AssetId==previous))throw new InvalidDataException("原灵感板关联已变化，请明确重新采集。");
                await vm.RevealBrowserAssetAsync(previous,token);return new(true,"Completed","光镜已确认入库。",Receipt:Receipt(saved));
            }
            var safe=Path.GetFileName(capture.FileName);if(string.IsNullOrWhiteSpace(safe)||safe!=capture.FileName||safe.IndexOfAny(Path.GetInvalidFileNameChars())>=0)safe="capture-"+hash[..12]+extension;
            safe=Path.GetFileNameWithoutExtension(safe)[..Math.Min(100,Path.GetFileNameWithoutExtension(safe).Length)]+extension;
            var sourceRoot=Path.Combine(AppDataPaths.Root,"BrowserCapture","originals",hash);Directory.CreateDirectory(sourceRoot);var source=Path.Combine(sourceRoot,safe);
            if(!File.Exists(source)){
                var temporary=source+"."+capture.CaptureId.ToString("N")+".part";
                try{
                    await using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)){await file.WriteAsync(bytes,token);await file.FlushAsync(token);file.Flush(true);}
                    File.Move(temporary,source,false);
                }finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            else if(!Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source,token))).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("采集缓存内容发生变化。");
            var imported=await vm.ImportBrowserImageAsync(source,library.ManagedAssetsPath,token);
            // Re-read destination identities after import; a deleted target never falls through to another target.
            projects=await new SqliteProjectRepository(new PixelTartDatabase(AppDataPaths.DatabaseFile)).ListAsync(token);boards=await tray.ListCollectionsAsync(token);
            if(!Valid())return new(false,"DestinationInvalid","目的地已失效，采集未确认成功，请重新选择。");
            if(target.ProjectId is Guid project){await vm.SaveBrowserProjectLinkAsync(project,imported.AssetId,token);if(!await vm.HasBrowserProjectLinkAsync(project,imported.AssetId,token))throw new IOException("项目关联没有保存。");}
            if(target.BoardId is Guid board){
                await tray.AddAsync(new(target.LibraryId,imported.AssetId,hash),"浏览器采集",cancellationToken:token);
                var entry=(await tray.ListAsync(token)).Single(x=>x.Reference.LibraryId==target.LibraryId&&x.Reference.AssetId==imported.AssetId);
                await tray.AddEntriesToCollectionAsync(board,[entry.TrayEntryId],token);
                if(!(await tray.ListCollectionEntriesAsync(board,token)).Any(x=>x.Reference.AssetId==imported.AssetId&&x.Reference.LibraryId==target.LibraryId))throw new IOException("灵感板关联没有保存。");
            }
            record=record with{AssetId=imported.AssetId,Outcome=imported.Disposition==AssetImportIdentityDisposition.Imported?"Imported":"Reused",ImportedAtUtc=DateTimeOffset.UtcNow};
            await journal.CompleteAsync(record,token);await vm.RevealBrowserAssetAsync(imported.AssetId,token);
            return new(true,"Completed","光镜已确认入库及目的地关联。",Receipt:Receipt(record));
        }catch(Exception e)when(e is InvalidDataException or ArgumentException or FormatException or NotSupportedException){return new(false,"CaptureRejected",e.Message);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException){return new(false,"DesktopUnavailable","桌面入库未完成，待处理采集已保留："+e.Message,Retryable:true);}
        finally{_captureGate.Release();}
    }
    private static CaptureReceipt Receipt(BrowserCaptureRecord record)=>new(record.CaptureId,record.LibraryId,record.ProjectId,record.BoardId,record.AssetId!.Value,record.ContentHash,record.Outcome,record.PageUrl,record.CapturedAtUtc,record.ImportedAtUtc!.Value);
}

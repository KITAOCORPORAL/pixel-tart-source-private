using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using Microsoft.Data.Sqlite;
var mode=args[0];var root=args[1];Directory.CreateDirectory(root);var db=Path.Combine(root,"library.db");
if(mode is "setup-tether" or "fail-tether" or "retry-tether" or "stale-tether")
{
    var product=new RAWSelectionAssistant.Core.Services.Database.PixelTartDatabase(Path.Combine(root,"product.db"));
    var annotations=new RAWSelectionAssistant.Core.Services.Database.SqliteTetherAnnotationRepository(product);
    var bridge=new RAWSelectionAssistant.Core.Services.Database.IdentityBridgeRepository(product);
    if(mode=="setup-tether")
    {
        await new RAWSelectionAssistant.Core.Services.Database.DatabaseMigrator(product,new RAWSelectionAssistant.Core.Services.Database.DatabaseBackupService(product,Path.Combine(root,"backups"))).MigrateAsync();
        var session=Guid.NewGuid();var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow.ToString("O");var path=Path.Combine(root,"capture.jpg");await File.WriteAllBytesAsync(path,[1,2,3]);
        await using(var c=await product.OpenConnectionAsync(write:true))await using(var command=c.CreateCommand())
        {
            command.CommandText="INSERT INTO TetherSessions(Id,ProjectId,ProviderType,WatchDirectory,NormalizedWatchDirectory,State,StartedAtUtc,DiscoveryCutoffUtc,ImportExisting,CopyToProject,CopyToBackup,CreatedAtUtc,UpdatedAtUtc) VALUES($s,NULL,'WatchFolder',$dir,$dir,'Stopped',$now,$now,0,0,0,$now,$now); INSERT INTO TetherAssets(Id,SessionId,ProjectId,SourcePath,NormalizedSourcePath,FileName,Extension,MediaKind,FileSize,ModifiedAtUtc,FirstSeenAtUtc,ReadyAtUtc,StabilityState,ProcessingState,PreviewState,UpdatedAtUtc) VALUES($a,$s,NULL,$p,$p,'capture.jpg','.jpg','PreviewImage',3,$now,$now,$now,'Stable','Ready','None',$now);";
            command.Parameters.AddWithValue("$s",session.ToString("D"));command.Parameters.AddWithValue("$a",id.ToString("D"));command.Parameters.AddWithValue("$dir",root);command.Parameters.AddWithValue("$p",path);command.Parameters.AddWithValue("$now",now);await command.ExecuteNonQueryAsync();
        }
        var library=await new AssetLibraryContainerService().CreateAsync(Path.Combine(root,"tether.ptlibrary"),"synthetic");var handoff=new RAWSelectionAssistant.Core.Services.Tethering.TetherLibraryHandoffService(bridge,_=>Task.FromResult<AssetLibraryContainerDescriptor?>(library),annotations);
        await handoff.HandoffAsync((await new RAWSelectionAssistant.Core.Services.Database.SqliteTetherAssetRepository(product).GetAsync(id))!);
        await File.WriteAllTextAsync(Path.Combine(root,"tether-id"),id.ToString());return;
    }
    var assetId=Guid.Parse(await File.ReadAllTextAsync(Path.Combine(root,"tether-id")));var lib=await new AssetLibraryContainerService().OpenAsync(Path.Combine(root,"tether.ptlibrary"));
    var transfer=new RAWSelectionAssistant.Core.Services.Tethering.TetherLibraryHandoffService(bridge,_=>Task.FromResult<AssetLibraryContainerDescriptor?>(lib),annotations);
    var fallback=new RAWSelectionAssistant.Core.Services.Tethering.TetherAnnotationService(annotations);var inject=false;
    Task<AssetLibraryContainerDescriptor?> Provider(CancellationToken _){if(inject)throw new IOException("injected library failure");return Task.FromResult<AssetLibraryContainerDescriptor?>(lib);}
    var linked=new RAWSelectionAssistant.Core.Services.Tethering.LinkedTetherAnnotationService(fallback,transfer,Provider);
    if(mode=="fail-tether")
    {
        var current=(await linked.GetAsync(assetId))!;var draft=current with{Rating=4,PhotographerNote="draft",ClientNote="customer-independent"};await File.WriteAllTextAsync(Path.Combine(root,"stale.json"),JsonSerializer.Serialize(draft));inject=true;
        if((await linked.SaveAsync(draft)).Success)throw new Exception("injection failed");if((await annotations.GetByAssetAsync(assetId))!.ClientNote!="customer-independent")throw new Exception("customer lost");Console.WriteLine("PRODUCT_COMMIT_LIBRARY_FAILED");return;
    }
    var pending=mode=="stale-tether"?JsonSerializer.Deserialize<RAWSelectionAssistant.Core.Models.TetherAnnotationRecord>(await File.ReadAllTextAsync(Path.Combine(root,"stale.json")))!:(await annotations.GetByAssetAsync(assetId))!;
    var result=await linked.SaveAsync(pending);if(mode=="retry-tether"&&!result.Success)throw new Exception("restart retry failed");if(mode=="stale-tether"&&result.Success)throw new Exception("stale accepted");Console.WriteLine(mode=="retry-tether"?"RESTART_RETRY_SUCCESS":"STALE_PROCESS_REJECTED");return;
}if(mode=="setup"){
var file=Path.Combine(root,"fixture.bin");await File.WriteAllBytesAsync(file,[1,2,3]);await using var repo=new SqliteAssetLibraryRepository(db);var id=(await repo.ImportWithIdentitiesAsync([new(file,ComputeContentHash:true)])).Items.Single().AssetId;var snapshot=await new PhotographerAnnotationStore(new AssetLibraryDatabase(db)).ReadAsync(id);await File.WriteAllTextAsync(Path.Combine(root,"expected.json"),JsonSerializer.Serialize(snapshot));return;}
if(mode=="lock"){using var c=new SqliteConnection("Data Source="+db);c.Open();using var tx=c.BeginTransaction(deferred:false);File.WriteAllText(Path.Combine(root,"locked"),Environment.ProcessId.ToString());await Task.Delay(Timeout.Infinite);return;}
var expected=JsonSerializer.Deserialize<PhotographerAnnotation>(await File.ReadAllTextAsync(Path.Combine(root,"expected.json")))!;var store=new PhotographerAnnotationStore(new AssetLibraryDatabase(db));
if(mode=="write"){var label=args[2];await File.WriteAllTextAsync(Path.Combine(root,label+".ready"),Environment.ProcessId.ToString());var deadline=DateTime.UtcNow.AddSeconds(20);while(!File.Exists(Path.Combine(root,"go"))){if(DateTime.UtcNow>deadline)throw new TimeoutException();await Task.Delay(20);}var result=await store.CompareExchangeAsync(expected,expected with{Rating=int.Parse(args[3]),Note=label});await File.WriteAllTextAsync(Path.Combine(root,label+".result"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,result.Applied,result.Current}));return;}
if(mode=="aba"){var current=await store.ReadAsync(expected.AssetId);await store.CompareExchangeAsync(current,current with{Rating=expected.Rating,Note=expected.Note});var stale=await store.CompareExchangeAsync(expected,expected with{Rating=3});if(stale.Applied)throw new Exception("ABA revived stale version");Console.WriteLine("ABA_REJECTED");return;}
if(mode=="after-crash"){var current=await store.ReadAsync(expected.AssetId);var result=await store.CompareExchangeAsync(current,current with{Rating=5});if(!result.Applied)throw new Exception("lock retained");Console.WriteLine("CRASH_LOCK_RELEASED");}

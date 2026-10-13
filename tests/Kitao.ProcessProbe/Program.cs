using System.Text.Json;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using Microsoft.Data.Sqlite;
var mode=args[0];var root=args[1];Directory.CreateDirectory(root);var db=Path.Combine(root,"library.db");
if(mode=="batch11-decode")
{
    var path=args[2];var rows=new List<object>();
    try{using var raw=Sdcb.LibRaw.RawContext.OpenFile(path);rows.Add(new{entry="RawContext.OpenFile",status="opened",version=Sdcb.LibRaw.RawContext.Version});}
    catch(Sdcb.LibRaw.LibRawException error){rows.Add(new{entry="RawContext.OpenFile",stage="OpenFile",code=(int)error.ErrorCode,name=error.ErrorCode.ToString(),explain=error.ErrorExplain});}
    foreach(var decodeMode in new[]{RAWSelectionAssistant.Core.Models.RawDecodeMode.FastPreview,RAWSelectionAssistant.Core.Models.RawDecodeMode.ProfessionalDecode})
    {
        try{var decoded=await new RAWSelectionAssistant.Core.Services.RawToJpeg.LibRawDecoder().DecodeAsync(path,new(DecodeMode:decodeMode));rows.Add(new{entry=decodeMode.ToString(),status="decoded",decoded.Width,decoded.Height});}
        catch(RAWSelectionAssistant.Core.Services.RawToJpeg.RawDecodeException e){rows.Add(new{entry=decodeMode.ToString(),e.Stage,e.NativeCode,e.NativeName,e.ErrorCode});}
    }
    try{await new RAWSelectionAssistant.Core.Services.RawToJpeg.ProfessionalRawDecoder().DecodeAsync(path,new());rows.Add(new{entry="PV4",status="decoded"});}catch(Exception e){var inner=e as Sdcb.LibRaw.LibRawException??e.InnerException as Sdcb.LibRaw.LibRawException;rows.Add(new{entry="PV4",stage="OpenFile",nativeCode=inner is null?(int?)null:(int)inner.ErrorCode,nativeName=inner?.ErrorCode.ToString(),message=e.Message});}
    var nativeHandle=Sdcb.LibRaw.Natives.LibRawNative.Initialize(Sdcb.LibRaw.LibRawInitFlags.None);
    var bytes=File.ReadAllBytes(path);var memory=System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
    try{System.Runtime.InteropServices.Marshal.Copy(bytes,0,memory,bytes.Length);var result=Sdcb.LibRaw.Natives.LibRawNative.OpenBuffer(nativeHandle,memory,bytes.LongLength);rows.Add(new{entry="native.OpenBuffer",code=(int)result,name=result.ToString(),pathIndependent=true});}
    finally{Sdcb.LibRaw.Natives.LibRawNative.Close(nativeHandle);System.Runtime.InteropServices.Marshal.FreeHGlobal(memory);}
    var module=System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Where(m=>new[]{"raw_r.dll","jpeg8.dll","lcms2.dll","zlib1.dll"}.Contains(m.ModuleName)).Select(m=>new{name=m.ModuleName,path=m.FileName,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(m.FileName)))}).ToArray();
    using var file=File.OpenRead(path);var header=new byte[32];file.ReadExactly(header);file.Position=0;var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file));
    await File.WriteAllTextAsync(Path.Combine(root,"sony-decode.json"),JsonSerializer.Serialize(new{hash,header=Convert.ToHexString(header),architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),wrapper=typeof(Sdcb.LibRaw.RawContext).Assembly.GetName().Version?.ToString(),nativeVersion=Sdcb.LibRaw.RawContext.Version,rows,module},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(mode=="raw-early-signal")
{
    var input=args[2];using var context=Sdcb.LibRaw.RawContext.OpenFile(input);context.Unpack();
    var offset=checked((int)System.Runtime.InteropServices.Marshal.OffsetOf<Sdcb.LibRaw.Natives.LibRawData>(nameof(Sdcb.LibRaw.Natives.LibRawData.RawData)));
    var raw=System.Runtime.InteropServices.Marshal.PtrToStructure<Sdcb.LibRaw.Natives.LibRawRawData>(IntPtr.Add(context.UnsafeGetHandle(),offset));
    var sample=new byte[512];if(raw.RawImage!=IntPtr.Zero)System.Runtime.InteropServices.Marshal.Copy(raw.RawImage,sample,0,sample.Length);
    await File.WriteAllTextAsync(Path.Combine(root,"early-signal.json"),JsonSerializer.Serialize(new{decoder=Sdcb.LibRaw.RawContext.Version,make=context.ImageParams.Make,model=context.ImageParams.Model,rawWidth=context.RawWidth,rawHeight=context.RawHeight,hasMosaic=raw.RawImage!=IntPtr.Zero,hasFloat=raw.FloatImage!=IntPtr.Zero,hasThreeColor=raw.Color3Image!=IntPtr.Zero,hasFourColor=raw.Color4Image!=IntPtr.Zero,black=raw.Color.Black,white=raw.Color.Maximum,rawBps=raw.Color.RawBps,filters=raw.IParams.Filters,earlySampleSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sample)),claim="Read-only pre-DcrawProcess pointer availability; not reconstruction or recovered highlight"},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(mode is "raw-stage-basic" or "raw-stage-stream")
{
    var input=args[2];var copy=Path.Combine(root,"input"+Path.GetExtension(input));File.Copy(input,copy);var rows=new List<object>();var watch=System.Diagnostics.Stopwatch.StartNew();
    void Record(string stage){var p=System.Diagnostics.Process.GetCurrentProcess();p.Refresh();rows.Add(new{stage,ms=watch.ElapsedMilliseconds,workingSet=p.WorkingSet64,peakWorkingSet=p.PeakWorkingSet64,managed=GC.GetTotalMemory(false)});watch.Restart();}
    var definition=new RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawDefinition();
    var master=await new RAWSelectionAssistant.Core.Services.RawToJpeg.ProfessionalRawDecoder(Record).DecodeAsync(copy,definition);Record("DecodeAndMaster");var output=Path.Combine(root,"output.tif");
    if(mode=="raw-stage-basic")
    {
        var full=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.Render(master,definition);Record("FullRender");
        using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None)){RAWSelectionAssistant.Core.Services.Export.TiffExport.WriteRgb48(file,full);file.Flush(true);}Record("Encode");
        using(var file=File.OpenRead(output)){var back=RAWSelectionAssistant.Core.Services.Export.TiffReadBack.Read(file);GC.KeepAlive(back);}Record("WholeFileValidation");GC.KeepAlive(full);
    }
    else
    {
        long rowTicks=0;var encodeWatch=System.Diagnostics.Stopwatch.StartNew();
        await RAWSelectionAssistant.Core.Services.Export.AtomicTiffWriter.WriteRowsAsync(output,master.Image.Width,master.Image.Height,(y,row)=>{var t=System.Diagnostics.Stopwatch.GetTimestamp();RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.FillRgb16Row(master,definition,y,row);rowTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-t;},stage:Record);
        rows.Add(new{stage="RenderRowAggregate",ms=(long)(rowTicks*1000d/System.Diagnostics.Stopwatch.Frequency),scope="CPU render time accumulated inside render+encode; not extra elapsed time"});
        Record("AtomicCommit");
        var timings=new List<object>();for(var i=0;i<3;i++){var preview=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.RenderPreview(master,definition with{ExposureEv=i*.25},1600);Record("AreaPreview-"+i);GC.KeepAlive(preview);}
        for(var i=0;i<3;i++){var tile=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.RenderRegion(master,definition,100+i*100,200,512,512);Record("NativeTile-"+i);GC.KeepAlive(tile);}
    }
    string hash;using(var file=File.OpenRead(output)){hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file));}Record("StreamingHash");
    using var original=File.OpenRead(input);var unchanged=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(original))==master.Contract.SourceSha256;
    await File.WriteAllTextAsync(Path.Combine(root,"stages.json"),JsonSerializer.Serialize(new{mode,master.Contract.Model,master.Image.Width,master.Image.Height,sourceUnchanged=unchanged,outputSha256=hash,rows},new JsonSerializerOptions{WriteIndented=true}));return;
}
if(mode=="raw-perf")
{
    var input=args[2];var copy=Path.Combine(root,"input"+Path.GetExtension(input));File.Copy(input,copy);var rows=new List<object>();
    void Measure(string stage,long ms){var p=System.Diagnostics.Process.GetCurrentProcess();p.Refresh();rows.Add(new{stage,ms,workingSet=p.WorkingSet64,peakWorkingSet=p.PeakWorkingSet64,managed=GC.GetTotalMemory(false)});}
    var definition=new RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawDefinition();var watch=System.Diagnostics.Stopwatch.StartNew();
    var master=await new RAWSelectionAssistant.Core.Services.RawToJpeg.ProfessionalRawDecoder().DecodeAsync(copy,definition);Measure("Decode",watch.ElapsedMilliseconds);
    for(var i=0;i<4;i++){watch.Restart();var preview=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.Render(master,definition with{ExposureEv=i*.25},1600);Measure("Preview1600-"+i,watch.ElapsedMilliseconds);GC.KeepAlive(preview);}
    watch.Restart();var full=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.Render(master,definition);Measure("RenderFull",watch.ElapsedMilliseconds);
    watch.Restart();await RAWSelectionAssistant.Core.Services.Export.AtomicTiffWriter.WriteRgb48Async(Path.Combine(root,"output.tif"),full);Measure("WriteTiff16",watch.ElapsedMilliseconds);
    await File.WriteAllTextAsync(Path.Combine(root,"performance.json"),JsonSerializer.Serialize(new{model=master.Contract.Model,master.Image.Width,master.Image.Height,rows},new JsonSerializerOptions{WriteIndented=true}));return;
}if(mode=="raw-corpus")
{
    var input=args[2];var results=new List<object>();var decoder=new RAWSelectionAssistant.Core.Services.RawToJpeg.ProfessionalRawDecoder();var ordinal=0;
    foreach(var file in Directory.EnumerateFiles(input).Where(p=>RAWSelectionAssistant.Core.Services.Projects.RawMatchTiff16ProductPipeline.IsRaw(p)).Order())
    {
        var alias="RAW-"+(++ordinal).ToString("00");var copy=Path.Combine(root,alias+Path.GetExtension(file));File.Copy(file,copy,false);
        var definition=new RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawDefinition();var watch=System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var master=await decoder.DecodeAsync(copy,definition);var decodeMs=watch.ElapsedMilliseconds;watch.Restart();
            var preview=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.Render(master,definition,1600);var previewMs=watch.ElapsedMilliseconds;watch.Restart();
            var full=RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.Render(master,definition);var fullMs=watch.ElapsedMilliseconds;watch.Restart();
            var output=Path.Combine(root,alias+".tif");await RAWSelectionAssistant.Core.Services.Export.AtomicTiffWriter.WriteRgb48Async(output,full);var exportMs=watch.ElapsedMilliseconds;
            int previewError=0,readBackError=0;double evError=0;
            for(var y=0;y<preview.Height;y++)for(var x=0;x<preview.Width;x++)for(var c=0;c<3;c++)
            {var sx=(int)((x+.5)*full.Width/preview.Width);var sy=(int)((y+.5)*full.Height/preview.Height);var p=preview.Rgb32.Span[(y*preview.Width+x)*3+c];var f=full.Rgb32.Span[(sy*full.Width+sx)*3+c];if(p!=f)previewError++;}
            using(var stream=File.OpenRead(output)){var back=RAWSelectionAssistant.Core.Services.Export.TiffReadBack.Read(stream);for(var i=0;i<back.Rgb48Samples.Length;i++)readBackError=Math.Max(readBackError,Math.Abs(back.Rgb48Samples.Span[i]-(int)Math.Round(full.Rgb32.Span[i]*65535)));}
            for(var i=0;i<master.Image.Rgb32.Length;i+=3003){var v=master.Image.Rgb32.Span[i];evError=Math.Max(evError,Math.Abs(RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.ApplyExposure(new(v,v,v),1).X-2d*v));}
            watch.Restart();var gains=definition with{WhiteBalance="CameraGains",RedGain=1.25};var changed=await decoder.DecodeAsync(copy,gains);var wbDecodeMs=watch.ElapsedMilliseconds;var differences=0;
            for(var i=0;i<master.Image.Rgb32.Length;i+=3003)if(master.Image.Rgb32.Span[i]!=changed.Image.Rgb32.Span[i])differences++;
            watch.Restart();var reopened=await decoder.DecodeAsync(copy,definition);var reopenMs=watch.ElapsedMilliseconds;
            var reopenEqual=master.Image.Rgb32.Span.SequenceEqual(reopened.Image.Rgb32.Span);
            var jpeg=Path.ChangeExtension(file,".JPG");string? pairedJpegHash=null;bool? jpegRejected=null;
            if(File.Exists(jpeg)){var jpegCopy=Path.Combine(root,alias+".JPG");File.Copy(jpeg,jpegCopy);pairedJpegHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(jpeg)));try{await decoder.DecodeAsync(jpegCopy,definition);jpegRejected=false;}catch(NotSupportedException){jpegRejected=true;}}
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(file)));
            results.Add(new{alias,master.Contract,master.Image.Width,master.Image.Height,decodeMs,previewMs,fullMs,exportMs,wbDecodeMs,reopenMs,sourceUnchanged=hash==master.Contract.SourceSha256,peakWorkingSet=System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64,previewError,readBackError,evError,reopenEqual,wbChangedSamples=differences,normalizedGains=changed.Contract.NormalizedWhiteBalance,pairedJpegHash,jpegRejected,outputHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(output)))});
            Console.WriteLine(alias+" "+master.Contract.Model+" "+master.Image.Width+"x"+master.Image.Height+" previewErrors="+previewError+" wbChanged="+differences+" reopen="+reopenEqual);
        }
        catch(Exception e){results.Add(new{alias,error=e.GetType().Name,message=e.Message});Console.WriteLine(alias+" FAILED "+e.Message);}
        GC.Collect();GC.WaitForPendingFinalizers();
    }
    await File.WriteAllTextAsync(Path.Combine(root,"results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));return;
}if(mode=="raw-identify")
{
    var candidates=Directory.EnumerateFiles(args[2],"*",SearchOption.AllDirectories).Where(p=>RAWSelectionAssistant.Core.Services.Projects.RawMatchTiff16ProductPipeline.IsRaw(p));var rows=new List<object>();var index=0;
    foreach(var file in candidates.Take(1000))try{using var context=Sdcb.LibRaw.RawContext.OpenFile(file);rows.Add(new{alias="AVAILABLE-"+(++index),make=context.ImageParams.Make,model=context.ImageParams.Model,path=file});}catch{};
    await File.WriteAllTextAsync(Path.Combine(root,"inventory-private.json"),JsonSerializer.Serialize(rows));Console.WriteLine("identified="+rows.Count);return;
}if(mode=="raw-api")
{
    if(args.Length>2){using var context=Sdcb.LibRaw.RawContext.OpenFile(args[2]);Console.WriteLine("LibRaw "+Sdcb.LibRaw.RawContext.Version);Console.WriteLine(JsonSerializer.Serialize(Sdcb.LibRaw.OutputParams.FromLibRaw(context.UnsafeGetHandle())));}
    foreach(var type in new[]{typeof(Sdcb.LibRaw.OutputParams),typeof(Sdcb.LibRaw.RawContext),typeof(Sdcb.LibRaw.Natives.LibRawColorData)}){Console.WriteLine(type.FullName);foreach(var property in type.GetProperties())Console.WriteLine(property.Name+":"+property.PropertyType);foreach(var field in type.GetFields())Console.WriteLine(field.Name+":"+field.FieldType);}
    Console.WriteLine(string.Join(",",Enum.GetNames<Sdcb.LibRaw.LibRawColorSpace>()));return;
}if(mode=="undo-prepare")
{
    var file=Path.Combine(root,"fixture.bin");await File.WriteAllBytesAsync(file,[1]);await using var repo=new SqliteAssetLibraryRepository(db);var id=(await repo.ImportWithIdentitiesAsync([new(file)])).Items.Single().AssetId;var annotations=new PhotographerAnnotationStore(new AssetLibraryDatabase(db));var before=await annotations.ReadAsync(id);var written=await annotations.CompareExchangeAsync(before,before with{Rating=4});await File.WriteAllTextAsync(Path.Combine(root,"undo.json"),JsonSerializer.Serialize(written.UndoToken));await File.WriteAllTextAsync(Path.Combine(root,"id"),id.ToString());return;
}
if(mode=="undo-other")
{
    var id=Guid.Parse(await File.ReadAllTextAsync(Path.Combine(root,"id")));var annotations=new PhotographerAnnotationStore(new AssetLibraryDatabase(db));var current=await annotations.ReadAsync(id);await annotations.CompareExchangeAsync(current,current with{Rating=5});return;
}
if(mode=="undo-stale")
{
    await using var repo=new SqliteAssetLibraryRepository(db);var undo=JsonSerializer.Deserialize<RAWSelectionAssistant.Core.Models.AssetLibraryUndoToken>(await File.ReadAllTextAsync(Path.Combine(root,"undo.json")))!;
    if(await repo.UndoAsync(undo))throw new Exception("stale undo accepted");Console.WriteLine("STALE_UNDO_REJECTED");return;
}
if(mode.StartsWith("version-"))
{
    var repo=new RAWSelectionAssistant.Core.Services.Projects.StudioVersionRepository(Path.Combine(root,"versions"));var hash=new string('b',64);var identity=RAWSelectionAssistant.Core.Services.Projects.StudioVersionRepository.Identity(null,null,hash);
    if(mode=="version-prepare"){var empty=await repo.LoadAsync(identity);var version=new RAWSelectionAssistant.Core.Services.Projects.StudioEditVersion(Guid.NewGuid(),"base",DateTimeOffset.UtcNow,null,null,hash,null,null,null,RAWSelectionAssistant.Core.Services.Projects.ColorStudioMatchEngine.Stable,RAWSelectionAssistant.Core.Services.Projects.MatchV4ExecutionMode.Auto);var saved=await repo.SaveAsync(empty,[version],version.Id);await File.WriteAllTextAsync(Path.Combine(root,"catalog.json"),JsonSerializer.Serialize(saved));return;}
    var catalogExpected=JsonSerializer.Deserialize<RAWSelectionAssistant.Core.Services.Projects.StudioVersionCatalog>(await File.ReadAllTextAsync(Path.Combine(root,"catalog.json")))!;
    if(mode=="version-update"){await repo.SaveAsync(catalogExpected,[catalogExpected.Versions[0] with{Name="other process",Archived=false}],catalogExpected.SelectedId);return;}
    try{await repo.SaveAsync(catalogExpected,[catalogExpected.Versions[0] with{Name="stale"}],catalogExpected.SelectedId);throw new Exception("stale catalog accepted");}catch(InvalidOperationException){Console.WriteLine("STALE_CATALOG_REJECTED");return;}
}if(mode=="export-crash-pv4")
{
    var point=args[2];var d=new RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawDefinition();var image=new RAWSelectionAssistant.Core.Services.Color.HighBitDepthImageBuffer(128,96,Enumerable.Repeat(.18f,128*96*3).ToArray(),"16",RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.WorkingSpace);
    var master=new RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawMaster(image,new("synthetic","Test","Synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));
    var temp=Path.Combine(root,"encoding.tmp");await RAWSelectionAssistant.Core.Services.Export.AtomicTiffWriter.WriteRowsAsync(temp,128,96,(y,row)=>{RAWSelectionAssistant.Core.Services.Projects.ProfessionalRawPipeline.FillRgb16Row(master,d,y,row);if(point=="Encoding"&&y==16)Environment.Exit(73);});
    var id=Guid.Parse("b5555555-5555-5555-5555-555555555555");var output=Path.Combine(root,"out.png");
    var receipt=new RAWSelectionAssistant.Core.Services.Projects.StudioExportReceipt(id,null,null,new string('a',64),id,4,"TIFF16",output,"",DateTimeOffset.UtcNow,new(1,".tif",16,"Original",128,96,null,null,"None","sRGB","orientation1","none",RawDefinition:d,RawInput:master.Contract));
    await RAWSelectionAssistant.Core.Services.Projects.StudioExportReceiptStore.CommitOutputAsync(temp,output,receipt,checkpoint:stage=>{if(stage==point)Environment.Exit(73);});return;
}
if(mode=="export-crash")
{
    var stage=args[2];var output=Path.Combine(root,"out.png");var temp=Path.Combine(root,"encoding.tmp");var id=Guid.Parse("b5555555-5555-5555-5555-555555555555");
    if(stage=="Encoding"){await File.WriteAllBytesAsync(temp,[1]);Environment.Exit(73);}
    await File.WriteAllBytesAsync(temp,[1,2,3,4]);
    var receipt=new RAWSelectionAssistant.Core.Services.Projects.StudioExportReceipt(id,null,null,new string('a',64),id,1,"PNG",output,"",DateTimeOffset.UtcNow);
    await RAWSelectionAssistant.Core.Services.Projects.StudioExportReceiptStore.CommitOutputAsync(temp,output,receipt,checkpoint:point=>{if(point==stage)Environment.Exit(73);});return;
}
if(mode=="export-race")
{
    var label=args[2];await File.WriteAllTextAsync(Path.Combine(root,label+".ready"),Environment.ProcessId.ToString());var deadline=DateTime.UtcNow.AddSeconds(20);while(!File.Exists(Path.Combine(root,"go"))){if(DateTime.UtcNow>deadline)throw new TimeoutException();await Task.Delay(20);}
    var id=Guid.Parse("b5555555-5555-5555-5555-555555555555");var result=await RAWSelectionAssistant.Core.Services.Projects.ExportRecovery.RecoverAsync(Path.Combine(root,"out.png"),id,new string('a',64));await File.WriteAllTextAsync(Path.Combine(root,label+".result"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,result.State}));return;
}if(mode=="export-recover")
{
    var id=Guid.Parse("b5555555-5555-5555-5555-555555555555");var result=await RAWSelectionAssistant.Core.Services.Projects.ExportRecovery.RecoverAsync(Path.Combine(root,"out.png"),id,new string('a',64));
    Console.WriteLine(result.State);if(args.Length>2 && result.State!=args[2])throw new Exception("unexpected recovery state");return;
}if(mode is "setup-tether" or "fail-tether" or "retry-tether" or "stale-tether")
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

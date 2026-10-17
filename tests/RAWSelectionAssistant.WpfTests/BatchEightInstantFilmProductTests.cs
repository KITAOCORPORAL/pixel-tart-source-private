using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Services.Publishing;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchEightInstantFilmProductTests
{
    private static ProfessionalRawMaster Master(int width=96,int height=64,ushort orientation=6)
    {
        var d=new ProfessionalRawDefinition();var pixels=new float[width*height*3];
        for(var y=0;y<height;y++)for(var x=0;x<width;x++){var i=(y*width+x)*3;pixels[i]=.05f+x*.6f/width;pixels[i+1]=.1f+y*.4f/height;pixels[i+2]=.3f;}
        return new(new(width,height,pixels,"16",ProfessionalRawPipeline.WorkingSpace,orientation),new("synthetic","Test","Synthetic",new string('b',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));
    }
    [TestMethod]
    public async Task FilmQuickFormatsAndPublishingUseSameFrozenFullResolutionDefinition()
    {
        using var temp=new Temp();var master=Master();var film=new InstantFilmDefinition(true,FilmId:"reclaimed-blue600",ChemicalGeneration:"ReclaimedCreative",Tier:"实验胶片");
        var definition=new ProfessionalRawDefinition{InstantFilm=film with{DiceEnabled=true,Snapshot=InstantFilmRandom.Roll(film,12345)}};
        var size=ProfessionalRawPipeline.OutputSize(master,definition);
        foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
        {
            var path=temp.File(format.ToString()+".tif");var result=await ProfessionalRawExport.WriteAsync(master,definition,path,format);var inspected=ProfessionalRawExport.Inspect(path,master,definition,format);
            Assert.AreEqual(size.Width,result.Width);Assert.AreEqual(size.Height,result.Height);Assert.AreEqual(1,result.Orientation);
            if(format!=StudioExportFormat.Jpeg)Assert.AreEqual(0,inspected.MaximumError);else Assert.IsLessThanOrEqualTo(5,inspected.MeanAbsoluteError);
        }
        var source=temp.File("Tiff.tif");var recipe=new ExportRecipeSnapshot(1,".tif",16,"Original",size.Width,size.Height,null,null,"None","sRGB","orientation","none",RawDefinition:definition,RawInput:master.Contract);
        await StudioExportReceiptStore.WriteAsync(source,new(Guid.NewGuid(),null,null,master.Contract.SourceSha256,Guid.NewGuid(),4,"intermediate",source,"",DateTimeOffset.UtcNow,recipe));
        var result2=await new PublishingExportService(new WpfPublishingRenderer()).ExportAsync(Guid.NewGuid(),new([source],temp.File("published"),new(new(false),false,[],PublishingOutputFormat.Tiff,OutputBitDepth:PublishingOutputBitDepth.Sixteen)));
        Assert.AreEqual(PublishingItemState.Completed,result2.Items[0].State,result2.Items[0].ErrorMessage);
        var receipt=await ExportRecovery.ReadAsync(result2.Items[0].DestinationPath+".kitao-export.json");
        Assert.AreEqual(JsonSerializer.Serialize(definition),JsonSerializer.Serialize(receipt.RecipeSnapshot!.RawDefinition));
        ProfessionalRawExport.Inspect(result2.Items[0].DestinationPath!,master,definition,StudioExportFormat.Tiff);
    }
    [TestMethod]
    public async Task FullJpegAdapterRoundTripsWorkspaceVersionsAndNeverUsesProxyForExport()
    {
        using var temp=new Temp();var path=temp.File("input.jpg");SaveJpeg(path,120,80);
        var definition=InstantFilmJpegInput.Definition;var master=await InstantFilmJpegInput.ReadAsync(path,definition,CancellationToken.None);
        Assert.AreEqual(120,master.Image.Width);Assert.IsFalse(master.Contract.WhiteBalanceApplied);Assert.AreEqual("sRGB",master.Image.WorkingColorSpace);
        using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));await workspace.LoadTargetAsync(path);
        workspace.InstantDevelopmentDemo=true;workspace.InstantCameraAnimation=false;var watch=System.Diagnostics.Stopwatch.StartNew();var task=workspace.ToggleInstantDiceCommand.ExecuteAsync(null);
        Assert.IsTrue(workspace.IsInstantGenerating);Assert.IsFalse(workspace.GenerateInstantFilmCommand.CanExecute(null));Assert.IsFalse(workspace.ExportSelectedCommand.CanExecute(null));
        await task;Assert.IsGreaterThanOrEqualTo(4000L,watch.ElapsedMilliseconds);Assert.IsNotNull(workspace.ActiveTarget!.RawDefinition,workspace.StatusText);Assert.AreEqual(5,workspace.ActiveTarget.RawDefinition.ProcessingVersion);
        var json=temp.File("session.ptstudio.json");await workspace.SaveSessionAsync(json);
        var before=Pixels(workspace.Editor.MatchedImage!);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));await reopened.LoadSessionAsync(json);
        CollectionAssert.AreEqual(before,Pixels(reopened.Editor.MatchedImage!));Assert.IsTrue(reopened.InstantDiceEnabled);Assert.AreEqual(workspace.ActiveTarget.RawDefinition!.InstantFilm!.Snapshot!.RootSeed,reopened.ActiveTarget!.RawDefinition!.InstantFilm!.Snapshot!.RootSeed);
        reopened.QuickExportFormat=StudioExportFormat.Tiff;await reopened.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,reopened.ActiveTarget!.ExportStatus,reopened.ActiveTarget.Error);
        using var input=File.OpenRead(reopened.ActiveTarget.OutputPath!);var output=TiffReadBack.Read(input);var size=ProfessionalRawPipeline.OutputSize(master,reopened.ActiveTarget.RawDefinition!);Assert.AreEqual(size.Width,output.Width);Assert.AreEqual(size.Height,output.Height);
        reopened.QuickExportFormat=StudioExportFormat.Source;await reopened.ExportSelectedCommand.ExecuteAsync(null);
        Assert.AreEqual(ReferenceExportStatus.Succeeded,reopened.ActiveTarget.ExportStatus,reopened.ActiveTarget.Error);
        Assert.AreEqual(".jpg",Path.GetExtension(reopened.ActiveTarget.OutputPath));
        using(var jpg=File.OpenRead(reopened.ActiveTarget.OutputPath!)){Assert.AreEqual(255,jpg.ReadByte());Assert.AreEqual(216,jpg.ReadByte());}
        IReadOnlyList<string>? prepared=null;reopened.OpenPublishing=paths=>prepared=paths;await reopened.PreparePublishingCommand.ExecuteAsync(null);
        Assert.IsNotNull(prepared);Assert.HasCount(1,prepared);Assert.AreEqual(".tif",Path.GetExtension(prepared[0]));
        var preparedReceipt=await ExportRecovery.ReadAsync(prepared[0]+".kitao-export.json");Assert.AreEqual(size.Width,preparedReceipt.RecipeSnapshot!.Width);Assert.AreEqual(size.Height,preparedReceipt.RecipeSnapshot.Height);
        Assert.AreEqual(JsonSerializer.Serialize(reopened.ActiveTarget.RawDefinition),JsonSerializer.Serialize(preparedReceipt.RecipeSnapshot.RawDefinition));
        await reopened.DisableInstantFilmCommand.ExecuteAsync(null);Assert.IsTrue(reopened.ActiveTarget.RawDefinition!.InstantFilm!.ImageEffectsDisabled);
    }
    [TestMethod]
    public async Task MultiTileViewportCoversPanDpiAndStaleRequestsNeverWin()
    {
        using var temp=new Temp();var path=temp.File("source.arw");await File.WriteAllBytesAsync(path,[1,2,3]);
        using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root),rawDecoder:new Legacy(),professionalRawDecoder:new Linear());
        await workspace.LoadTargetAsync(path);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);
        foreach(var dpi in new[]{1d,1.25,1.5,2})
        {
            await workspace.UpdateProfessionalViewportAsync(new Rect(100,50,1200,700),1/dpi,dpi);
            Assert.HasCount(6,workspace.Editor.IndependentDetailTiles);Assert.IsTrue(workspace.RawPipelineStatus.Contains("100%"));
        }
        var old=workspace.UpdateProfessionalViewportAsync(new Rect(0,0,1024,700),1,1);var current=workspace.UpdateProfessionalViewportAsync(new Rect(500,100,400,400),1,1);await Task.WhenAll(old,current);
        Assert.HasCount(1,workspace.Editor.IndependentDetailTiles);Assert.AreEqual(new Rect(500,100,400,400),workspace.Editor.IndependentDetailTiles[0].Region);
        var stale=workspace.UpdateProfessionalViewportAsync(new Rect(0,0,800,600),1,1);workspace.RawExposureEv=1;await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);await stale;Assert.IsEmpty(workspace.Editor.IndependentDetailTiles);
        workspace.InstantCameraAnimation=false;var generating=workspace.GenerateInstantFilmAsync(false);await Task.Delay(40);workspace.Dispose();await generating;Assert.IsFalse(workspace.IsInstantGenerating);
    }
    [TestMethod]
    public async Task FilmCancellationAndInvalidInputKeepExistingDestinationAndReleaseGlobalWork()
    {
        using var temp=new Temp();var master=Master();var d=new ProfessionalRawDefinition{InstantFilm=new(true)};
        foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
        {
            var path=temp.File(format+".out");using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(()=>ProfessionalRawExport.WriteAsync(master,d,path,format,cancellation.Token));Assert.IsFalse(File.Exists(path));
            await File.WriteAllTextAsync(path,"keep");await Assert.ThrowsAsync<IOException>(()=>ProfessionalRawExport.WriteAsync(master,d,path,format));Assert.AreEqual("keep",await File.ReadAllTextAsync(path));
        }
        Assert.AreEqual(1,ProfessionalRawResourcePolicy.HeavyWork.CurrentCount);
    }
    [TestMethod]
    public async Task CameraPathEjectsFullyBeforeFourSecondDevelopmentAndLifecycleKeepsOldVersion()
    {
        using var temp=new Temp();var path=temp.File("camera.jpg");SaveJpeg(path,60,40);
        using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));await workspace.LoadTargetAsync(path);
        var frames=new List<(InstantDevelopmentFrame Frame,long Milliseconds)>();var watch=System.Diagnostics.Stopwatch.StartNew();
        workspace.PropertyChanged+=(_,args)=>{if(args.PropertyName==nameof(workspace.InstantDevelopment)&&workspace.InstantDevelopment is {} f)frames.Add((f,watch.ElapsedMilliseconds));};
        workspace.InstantDevelopmentDemo=true;workspace.InstantCameraAnimation=true;await workspace.GenerateInstantFilmAsync(false);
        Assert.IsNotNull(workspace.ActiveTarget!.RawDefinition,workspace.StatusText);
        Assert.IsTrue(frames.Any(x=>x.Frame.Stage==InstantDevelopmentStage.Camera));
        Assert.IsTrue(frames.Any(x=>x.Frame.Stage==InstantDevelopmentStage.Ejecting&&x.Frame.Progress>=.999));
        var developing=frames.First(x=>x.Frame.Stage==InstantDevelopmentStage.Developing);var completed=frames.First(x=>x.Frame.Stage==InstantDevelopmentStage.Completed);
        Assert.IsGreaterThanOrEqualTo(3950L,completed.Milliseconds-developing.Milliseconds);
        Assert.IsFalse(frames.Where(x=>x.Milliseconds>=developing.Milliseconds).Any(x=>x.Frame.CameraVisible));
        var previous=workspace.ActiveTarget.ActiveEditVersionId;var pixels=Pixels(workspace.Editor.MatchedImage!);
        var cancelled=workspace.GenerateInstantFilmAsync(false);workspace.ExitInstantPresentationForLifecycle();await cancelled;
        Assert.AreEqual(previous,workspace.ActiveTarget.ActiveEditVersionId);CollectionAssert.AreEqual(pixels,Pixels(workspace.Editor.MatchedImage!));
        workspace.InstantEnvironmentCelsius=double.NaN;await workspace.GenerateInstantFilmAsync(false);Assert.AreEqual(previous,workspace.ActiveTarget.ActiveEditVersionId);
    }
    [TestMethod]
    public async Task AuthorizedRawInstantFilmFullOutputsUseFrozenDefinition()
    {
        var corpus=Environment.GetEnvironmentVariable("KITAO_BATCH08_CORPUS");var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH08_EVIDENCE");
        if(string.IsNullOrEmpty(corpus)||string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Authorized local RAW corpus and evidence opt-in required.");return;}
        Directory.CreateDirectory(evidence);var results=new List<object>();
        foreach(var name in new[]{"CANON-5DM3.CR2","DSC04831.ARW"})
        {
            using var temp=new Temp();var source=Path.Combine(corpus,name);var d=new ProfessionalRawDefinition();
            var master=await new ProfessionalRawDecoder().DecodeAsync(source,d);
            var film=new InstantFilmDefinition(true,Tier:"轻微偶然");d=d with{InstantFilm=film with{DiceEnabled=true,Snapshot=InstantFilmRandom.Roll(film,20261012)}};
            var preview=ProfessionalRawPipeline.RenderPreview(master,d,1600);var restored=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(d))!;
            CollectionAssert.AreEqual(preview.Rgb32.ToArray(),ProfessionalRawPipeline.RenderPreview(master,restored,1600).Rgb32.ToArray());
            var rows=new List<object>();
            foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
            {
                var path=temp.File(format+".out");var watch=System.Diagnostics.Stopwatch.StartNew();await ProfessionalRawExport.WriteAsync(master,d,path,format);var inspection=ProfessionalRawExport.Inspect(path,master,d,format);
                rows.Add(new{format=format.ToString(),inspection,milliseconds=watch.ElapsedMilliseconds,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))});
            }
            var unchanged=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)))==master.Contract.SourceSha256;Assert.IsTrue(unchanged);
            results.Add(new{master.Contract.Model,master.Image.Width,master.Image.Height,definition=d,sourceUnchanged=unchanged,previewReopenExact=true,rows});
        }
        await File.WriteAllTextAsync(Path.Combine(evidence,"instant-real.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
    }
    [TestMethod]
    public async Task BatchNineCurrentJpegEditsSurviveGenerationAndReopenWithoutProxy()
    {
        using var temp=new Temp();var path=temp.File("edited.jpg");SaveJpeg(path,120,80);
        using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));await workspace.LoadTargetAsync(path);
        var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",1}})]);
        workspace.Editor.ApplyTargetSnapshot(null,null,stack,render:false);
        var expected=await workspace.Editor.ProcessForExportAsync(path,null,null,default,stack);
        Assert.IsFalse(Pixels(expected).SequenceEqual(Pixels(StudioQuickExport.Load(path))),"Fixture must actually change source pixels.");
        workspace.InstantLayout="LegacyFree";workspace.InstantPaperFrame=false;workspace.InstantImageEffectsDisabled=true;workspace.InstantCameraAnimation=false;
        var oldVersion=workspace.ActiveTarget!.ActiveEditVersionId;await workspace.GenerateInstantFilmAsync(false);
        Assert.IsNotNull(workspace.ActiveTarget.RawDefinition,workspace.StatusText);
        var recipe=workspace.ActiveTarget.RawDefinition.InstantInput!;Assert.AreEqual("CurrentEdits",recipe.Mode);Assert.AreEqual(oldVersion,recipe.OriginVersionId);Assert.HasCount(1,recipe.Stack!.Nodes);
        CollectionAssert.AreEqual(Pixels(expected),Pixels(workspace.Editor.MatchedImage!));
        var session=temp.File("edit.ptstudio.json");await workspace.SaveSessionAsync(session);
        using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root));await reopened.LoadSessionAsync(session);
        CollectionAssert.AreEqual(Pixels(expected),Pixels(reopened.Editor.MatchedImage!));Assert.AreEqual(120,reopened.ActiveTarget!.LinearRawMaster!.Image.Width);
    }
    [TestMethod]
    [DoNotParallelize]
    public async Task BatchNineMultipleWorkspacesPressureCancellationAndBudgetRelease()
    {
        using var temp=new Temp();var workspaces=new List<ReferenceColorWorkspaceViewModel>();var records=new List<object>();
        var process=System.Diagnostics.Process.GetCurrentProcess();long peakAccounted=0;
        void Record(string stage){process.Refresh();peakAccounted=Math.Max(peakAccounted,StudioGlobalResources.AccountedBytes);records.Add(new{stage,accounted=StudioGlobalResources.AccountedBytes,workingSet=process.WorkingSet64,peakWorkingSet=process.PeakWorkingSet64,privateBytes=process.PrivateMemorySize64});}
        try
        {
            for(var i=0;i<3;i++)
            {
                var path=temp.File($"workspace-{i}.arw");await File.WriteAllBytesAsync(path,[1,2,(byte)i]);
                var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(temp.Root),rawDecoder:new Legacy(),professionalRawDecoder:new Linear());workspaces.Add(workspace);
                await workspace.LoadTargetAsync(path);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Record("master-hq-"+i);
            }
            await Task.WhenAll(workspaces.Select(w=>w.UpdateProfessionalViewportAsync(new Rect(0,0,1024,800),1,1)));Record("tiles");
            var active=workspaces[0].ActiveTarget!.LinearRawMaster!;
            using(var pressure=StudioGlobalResources.ReserveWork(1900L*1024*1024))
            {
                Record("eviction-active-reference");Assert.IsTrue(float.IsFinite(active.Image.Rgb32.Span[0]));
                Assert.Throws<InvalidOperationException>(()=>StudioGlobalResources.ReserveWork(StudioGlobalResources.TotalLimit));
            }
            workspaces[0].QuickExportFormat=StudioExportFormat.Tiff;await workspaces[0].ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,workspaces[0].ActiveTarget!.ExportStatus,workspaces[0].ActiveTarget.Error);Record("export");
            workspaces[2].QuickExportFormat=StudioExportFormat.Tiff;var exporting=workspaces[2].ExportSelectedCommand.ExecuteAsync(null);workspaces[2].Dispose();await exporting;Record("close-active-export");
            var pending=workspaces[1].UpdateProfessionalViewportAsync(new Rect(0,0,1000,800),1,1);workspaces[1].Dispose();await pending;Record("cancel-close");
            var replacement=temp.File("replacement.arw");await File.WriteAllBytesAsync(replacement,[9,8,7]);await workspaces[0].LoadTargetAsync(replacement);Record("switch-photo");
        }
        finally{foreach(var workspace in workspaces)workspace.Dispose();}
        Record("disposed");Assert.AreEqual(0L,StudioGlobalResources.AccountedBytes);Assert.AreEqual(1,ProfessionalRawResourcePolicy.HeavyWork.CurrentCount);
        var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH09_EVIDENCE");if(!string.IsNullOrEmpty(evidence)){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"workspace-pressure.json"),JsonSerializer.Serialize(new{scope="Three actual workspace viewmodels, synthetic 1800x1000 decoder; no desktop UI",peakAccounted,records},new JsonSerializerOptions{WriteIndented=true}));}
    }
    [TestMethod]
    public async Task BatchNineColdStagesMeasureLayoutFilmHqAndEncodingSeparately()
    {
        var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH09_EVIDENCE");if(string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Measurement opt-in required.");return;}
        using var temp=new Temp();var master=Master(1800,1200,1);var film=new InstantFilmDefinition(true,AlgorithmVersion:2){PaperGeometry=new()};var d=new ProfessionalRawDefinition{InstantFilm=film};
        var watch=System.Diagnostics.Stopwatch.StartNew();var sink=0d;
        for(var i=0;i<100000;i++){var layout=InstantPaperLayout.Create(1800,1200,film);sink+=layout.Width;}
        var layoutMs=watch.Elapsed.TotalMilliseconds;watch.Restart();
        for(var y=0;y<1200;y++)for(var x=0;x<1800;x++)sink+=InstantFilmPipeline.Apply(new(.2,.4,.6),(x+.5)/1800,(y+.5)/1200,film).X;
        var filmMs=watch.Elapsed.TotalMilliseconds;var size=ProfessionalRawPipeline.OutputSize(master,d);var reference=new float[size.Width*size.Height*3];watch.Restart();
        for(var y=0;y<size.Height;y++)for(var x=0;x<size.Width;x++){var value=ProfessionalRawPipeline.RenderOutputPixel(master,d,x,y);var i=(y*size.Width+x)*3;reference[i]=(float)value.X;reference[i+1]=(float)value.Y;reference[i+2]=(float)value.Z;}
        var perPixelLayoutMs=watch.Elapsed.TotalMilliseconds;watch.Restart();var optimized=ProfessionalRawPipeline.RenderOutput(master,d);var preparedLayoutMs=watch.Elapsed.TotalMilliseconds;
        CollectionAssert.AreEqual(reference,optimized.Rgb32.ToArray());watch.Restart();var hq=ProfessionalRawPipeline.RenderPreview(master,d,800);var hqMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart();using(var stream=File.Create(temp.File("encode.tif")))TiffExport.WriteRgb48(stream,optimized,new(IccProfile:StudioQuickExport.SrgbProfileBytes()));var encodingMs=watch.Elapsed.TotalMilliseconds;
        Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,"cold-stages.json"),JsonSerializer.Serialize(new{input="synthetic 1800x1200",cacheUsed=false,layoutIterations=100000,layoutMs,filmMs,perPixelLayoutMs,preparedLayoutMs,identicalPixels=true,hqMs,hqEdge=800,encodingMs,encoding="TIFF16 from already rendered pixels, excludes film/layout",sink},new JsonSerializerOptions{WriteIndented=true}));
    }
    [TestMethod]
    public async Task BatchNineV2PaperFormatsRoundTripAllExifAndBothFits()
    {
        using var temp=new Temp();
        for(ushort orientation=1;orientation<=8;orientation++)foreach(var fit in new[]{"CropFill","FitEntire"})
        {
            var master=Master(48,32,orientation);var d=new ProfessionalRawDefinition{InstantFilm=new(true,AlgorithmVersion:2){PaperGeometry=new(Fit:fit,CropX:.8,CropY:.2)}};
            foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
            {
                var path=temp.File($"{orientation}-{fit}-{format}.out");await ProfessionalRawExport.WriteAsync(master,d,path,format);
                var decoded=ProfessionalRawExport.Inspect(path,master,d,format);var size=ProfessionalRawPipeline.OutputSize(master,d);
                Assert.AreEqual(size.Width,decoded.Width);Assert.AreEqual(size.Height,decoded.Height);Assert.AreEqual(1,decoded.Orientation);
                if(format!=StudioExportFormat.Jpeg)Assert.AreEqual(0,decoded.MaximumError);
            }
        }
    }
    private static byte[] Pixels(BitmapSource source){var bitmap=new FormatConvertedBitmap(source,PixelFormats.Rgb24,null,0);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*3];bitmap.CopyPixels(pixels,bitmap.PixelWidth*3,0);return pixels;}
    private static void SaveJpeg(string path,int width,int height){var p=new byte[width*height*3];for(var i=0;i<p.Length;i+=3){p[i]=80;p[i+1]=130;p[i+2]=190;}var encoder=new JpegBitmapEncoder{QualityLevel=95};encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width,height,96,96,PixelFormats.Rgb24,null,p,width*3)));using var file=File.Create(path);encoder.Save(file);}
    private sealed class Linear:IProfessionalRawDecoder
    {public Task<ProfessionalRawMaster> DecodeAsync(string path,ProfessionalRawDefinition definition,CancellationToken token=default){var m=Master(1800,1000,1);return Task.FromResult(m with{Contract=m.Contract with{SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),DecodeKey=definition.DecodeKey}});}}
    private sealed class Legacy:IRawDecoder
    {public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>Task.FromResult(RawDecodedImage.FromRgb48(12,8,72,Enumerable.Repeat((ushort)20000,288).ToArray(),new("Test","Synthetic",null,1,"sRGB")));}
    private sealed class Temp:IDisposable{public string Root{get;}=Path.Combine(Path.GetTempPath(),"Kitao-batch08-"+Guid.NewGuid());public Temp()=>Directory.CreateDirectory(Root);public string File(string name)=>Path.Combine(Root,name);public void Dispose(){try{Directory.Delete(Root,true);}catch{}}}
    private sealed class Dialogs(string root):IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
    }
}

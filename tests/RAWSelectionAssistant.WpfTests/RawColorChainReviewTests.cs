using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;

/// <summary>Opt-in, at most three existing authorized RAWs. Full App/commands; not pointer or camera calibration acceptance.</summary>
[TestClass][DoNotParallelize]
public sealed class RawColorChainReviewTests
{
    [TestInitialize]
    public void RequireExplicitAuthorizedRun()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KITAO_RAW_CHAIN_INPUTS")))
            Assert.Inconclusive("Opt-in real-photo review requires an explicitly authorized manifest; no directory discovery.");
    }
    private sealed record Input(string id,string name,string model,string path,string sha256);
    private sealed record Receipt(int ProducerProcessId,string DataRoot,Guid TargetId,Guid? VersionId,string Stack,string PreviewSha256,string SourceSha256);
    private static string Root=>Environment.GetEnvironmentVariable("KITAO_RAW_CHAIN_ROOT")??throw new InvalidOperationException("Explicit isolated review root required");
    private static Input Selected()
    {
        var manifest=Environment.GetEnvironmentVariable("KITAO_RAW_CHAIN_INPUTS")??throw new InvalidOperationException("Explicit authorized manifest required");
        var inputs=JsonSerializer.Deserialize<Input[]>(File.ReadAllText(manifest))!;Assert.IsTrue(inputs.Length is >=1 and <=3);
        var value=inputs.Single(x=>x.id==Environment.GetEnvironmentVariable("KITAO_RAW_CHAIN_ID"));
        Assert.AreEqual(value.sha256,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(value.path))),true);return value;
    }
    private static async Task Write(string name,object value){Directory.CreateDirectory(Root);await File.WriteAllTextAsync(Path.Combine(Root,name+".json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));}
    private static byte[] Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var pixels=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(pixels,rgb.PixelWidth*3,0);return pixels;}
    private static BitmapSource Display(ReferenceColorWorkspaceViewModel vm)=>vm.Editor.MatchedImage??vm.Editor.SourceImage??throw new AssertFailedException("No displayed RAW image");
    private static string Digest(BitmapSource image)=>Convert.ToHexString(SHA256.HashData(Pixels(image)));
    private static void Save(BitmapSource image,string name)
    {
        if(Math.Max(image.PixelWidth,image.PixelHeight)>1200){var scale=1200d/Math.Max(image.PixelWidth,image.PixelHeight);image=new TransformedBitmap(image,new ScaleTransform(scale,scale));}
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(Root,name+".png"));png.Save(file);
    }
    private static object SourceStats(HighBitDepthImageBuffer image)
    {
        long zero=0,one=0,negative=0,above=0,nonfinite=0;double min=double.PositiveInfinity,max=double.NegativeInfinity;
        foreach(var v in image.Rgb32.Span){if(!float.IsFinite(v)){nonfinite++;continue;}min=Math.Min(min,v);max=Math.Max(max,v);if(v==0)zero++;if(v==1)one++;if(v<0)negative++;if(v>1)above++;}
        return new{image.Width,image.Height,image.SourceBitDepth,image.WorkingColorSpace,image.Orientation,min,max,zeroChannelSamples=zero,oneChannelSamples=one,negative,above,nonfinite,note="Already after LibRaw development; not a sensor clipping or scene-linear claim"};
    }
    private static async Task<object> Export(ReferenceColorWorkspaceViewModel vm,string phase)
    {
        var target=vm.ActiveTarget!;vm.SetR53MainVariant();target.IsSelected=true;vm.QuickExportFormat=StudioExportFormat.Tiff;
        var directory=Path.Combine(Root,phase+"-export");Directory.CreateDirectory(directory);Environment.SetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY",directory);
        await vm.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,target.ExportStatus,vm.ExportFailureSummary+" "+target.Error);
        var receipt=await ExportRecovery.ReadAsync(target.OutputPath+".kitao-export.json");Assert.AreEqual(target.ActiveEditVersionId,receipt.EditVersionId);Assert.AreEqual(target.SourceHash,receipt.SourceHash,true);
        Assert.IsNull(receipt.RecipeSnapshot!.RawDefinition,"This probe reviews the default legacy RAW16 route, not opt-in PV4");
        using var stream=File.OpenRead(target.OutputPath!);var output=TiffReadBack.Read(stream);Assert.AreEqual(16,output.BitsPerSample);Assert.AreEqual(1,output.Orientation);Assert.IsNotNull(target.RawMaster);
        var master=target.RawMaster.Image;Assert.AreEqual(master.Width,output.Width);Assert.AreEqual(master.Height,output.Height);
        var boundary=OutputStats(output.Rgb48Samples,master);var parity=PreviewParity(output.Rgb48Samples,output.Width,output.Height,Display(vm));
        Assert.IsLessThanOrEqualTo(1,parity.maximum8BitDifference,"Point-tool preview and full export samples must share the same frozen state");
        Save(Display(vm),phase+"-preview");
        var result=new{phase,path=target.OutputPath,sha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(target.OutputPath!))),output.Width,output.Height,output.BitsPerSample,receipt.ProcessingVersion,receipt.EditVersionId,stack=JsonSerializer.Serialize(vm.Editor.AdjustmentStack),adjustmentFingerprint=receipt.RecipeSnapshot.AdjustmentFingerprint,boundary,parity=new{parity.maximum8BitDifference,parity.mean8BitDifference,parity.comparedChannels},displayEdge=1200,decodeReduced=false};
        await Write(phase+"-output",result);return result;
    }
    private static object OutputStats(ReadOnlyMemory<ushort> output,HighBitDepthImageBuffer master)
    {
        long zero=0,one=0,newLow=0,newHigh=0;var before=master.Rgb32.Span;var after=output.Span;
        for(var i=0;i<after.Length;i++){if(after[i]==0){zero++;if(before[i]>0)newLow++;}if(after[i]==65535){one++;if(before[i]<1)newHigh++;}}
        return new{zeroChannelSamples=zero,maximumChannelSamples=one,newLowRelativeToDecodedInput=newLow,newHighRelativeToDecodedInput=newHigh,note="Output encoding boundary relative to same decoded master, not newly clipped sensor sites"};
    }
    private static (int maximum8BitDifference,double mean8BitDifference,long comparedChannels) PreviewParity(ReadOnlyMemory<ushort> output,int width,int height,BitmapSource preview)
    {
        var pixels=Pixels(preview);var data=output.Span;long sum=0;int maximum=0;
        for(var y=0;y<preview.PixelHeight;y++)for(var x=0;x<preview.PixelWidth;x++){var sx=Math.Min(width-1,(int)((x+.5)*width/preview.PixelWidth));var sy=Math.Min(height-1,(int)((y+.5)*height/preview.PixelHeight));for(var c=0;c<3;c++){var expected=(int)Math.Round(data[(sy*width+sx)*3+c]/65535d*255);var error=Math.Abs(expected-pixels[(y*preview.PixelWidth+x)*3+c]);sum+=error;maximum=Math.Max(maximum,error);}}
        return(maximum,sum/(double)pixels.Length,pixels.LongLength);
    }
    private static object CompareMaster(ReadOnlyMemory<ushort> before,HighBitDepthImageBuffer after)
    {
        var a=before.Span;var b=after.Rgb32.Span;Assert.AreEqual(a.Length,b.Length);long count=0,sum=0;int maximum=0,maximumIndex=-1,maximumBefore=0,maximumAfter=0,left=after.Width,top=after.Height,right=-1,bottom=-1;long border=0,interior=0;
        for(var i=0;i<a.Length;i++){var current=(int)Math.Round(b[i]*65535d);var error=Math.Abs(a[i]-current);if(error==0)continue;count++;sum+=error;if(error>maximum){maximum=error;maximumIndex=i;maximumBefore=a[i];maximumAfter=current;}var p=i/3;var x=p%after.Width;var y=p/after.Width;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);if(x<16||y<16||x>=after.Width-16||y>=after.Height-16)border++;else interior++;}
        return new{changedChannels=count,maximumU16Difference=maximum,maximumLocation=new{x=maximumIndex<0?-1:maximumIndex/3%after.Width,y=maximumIndex<0?-1:maximumIndex/3/after.Width,channel=maximumIndex<0?-1:maximumIndex%3,before=maximumBefore,after=maximumAfter},meanAbsoluteU16=sum/(double)a.Length,changedBounds=new{left,top,right,bottom},borderChannels=border,interiorChannels=interior,sameDimensions=true,scope="Reopened post-LibRaw master vs original identity TIFF16; no second demosaic in the comparison"};
    }
    private static async Task Render(ReferenceColorWorkspaceViewModel vm){if(vm.Editor.ApplyCommand.CanExecute(null))await vm.Editor.ApplyCommand.ExecuteAsync(null);await vm.AnalysisWork;if(vm.Editor.AdjustmentStack.Nodes.Count>0)Assert.IsNotNull(vm.Editor.MatchedImage,vm.Editor.StatusText);else Assert.IsNotNull(vm.Editor.SourceImage,"An unchanged default is displayed through SourceImage, not a fabricated matched frame");}
    private static void RunHost(Func<MainWindow,MainViewModel,Task> action)
    {
        Exception? failure=null;var complete=false;var thread=new Thread(()=>
        {
            try
            {
                var configured=Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT")??throw new InvalidOperationException("Isolation required");Assert.AreEqual(Path.GetFullPath(configured),AppDataPaths.Root,true);Directory.CreateDirectory(Root);
                if(!File.Exists(AppDataPaths.SettingsFile))new SettingsService(new FileLogService()).SaveAsync(new AppSettings{Weather=new(){Enabled=false,AutoRefreshEnabled=false}}).GetAwaiter().GetResult();
                var application=new App();application.InitializeComponent();var busy=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};timer.Tick+=async(_,_)=>
                {
                    if(busy||application.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel main} window)return;busy=true;
                    try{main.ForceExitTutorial();main.NavigateCommand.Execute("ReferenceColor");await action(window,main);complete=true;}catch(Exception error){failure=error;}finally{timer.Stop();application.Shutdown();}
                };timer.Start();application.Run();
            }
            catch(Exception error){failure=error;}
        }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(8)),"RAW review host timeout");if(failure is not null)throw new AssertFailedException(failure.ToString(),failure);Assert.IsTrue(complete);
    }
    [TestMethod]
    public void FullAppDefaultNamedRawPreviewExportAndSave()=>RunHost(async(window,main)=>
    {
        var input=Selected();
        try{using var context=RawContext.OpenFile(input.path);await Write("native-initial-parameters",new{version=RawContext.Version,parameters=OutputParams.FromLibRaw(context.UnsafeGetHandle()),scope="Read native initialization of shipped binary only, no unpack or second development; explicit production overrides documented separately"});}catch(LibRawException error){await Write("native-open-failure",new{version=RawContext.Version,code=error.ErrorCode.ToString(),message=error.Message});}
        var vm=main.ReferenceColorPage;Assert.IsTrue(vm.UsePhotoGradePage);await vm.LoadTargetAsync(input.path);await vm.AnalysisWork;
        if(vm.ActiveTarget is null||vm.ActiveTarget.RawMaster is null)
        {await Write("admission",new{input.id,input.name,input.model,status="NOT_ADMITTED",message=vm.StatusText,targetErrors=vm.Targets.Select(t=>t.Error),failure=vm.LastProcessingFailure,exportRun=false,visualReview="NOT_RUN_NO_DECODED_PIXELS",scope="Actual full App LoadTargetAsync production entry after file choice; no embedded/raster fallback"});return;}
        var target=vm.ActiveTarget;Assert.IsFalse(vm.IsProfessionalRaw);Assert.IsNull(target.RawDefinition);await Render(vm);
        await Write("admission",new{input.id,input.name,input.model,status="DEFAULT_RAW16_ADMITTED",processId=Environment.ProcessId,fullApp=true,vm.UsePhotoGradePage,vm.IsProfessionalRaw,engine=vm.Editor.MatchEngine.ToString(),state=JsonSerializer.Serialize(vm.Editor.AdjustmentStack),source=SourceStats(target.RawMaster.Image),budgets=new{ProfessionalRawResourcePolicy.MasterBudgetBytes,ProfessionalRawResourcePolicy.PreviewBudgetBytes,StudioGlobalResources.MasterLimit,StudioGlobalResources.TotalLimit},scope="Production entry after file choice, no mouse/file-dialog acceptance"});
        await Export(vm,"default");
        if(vm.Editor.ReadOnlyTarget)await vm.CreateR53VariantAsync(false);
        var exposure=ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure");vm.Editor.SetToolParameter(exposure,.5);await Render(vm);Assert.AreEqual(.5,vm.Editor.ToolValue(exposure));
        await Export(vm,"display-exposure-plus05");
        var session=Path.Combine(Root,"review.ptstudio.json");await vm.SaveSessionAsync(session);var receipt=new Receipt(Environment.ProcessId,AppDataPaths.Root,target.Id,target.ActiveEditVersionId,JsonSerializer.Serialize(vm.Editor.AdjustmentStack),Digest(Display(vm)),input.sha256);await Write("reopen-receipt",receipt);
        Assert.AreEqual(input.sha256,Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input.path))),true);
    });
    [TestMethod]
    public void IndependentAppReopenNamedRawPreservesStateAndPreview()=>RunHost(async(window,main)=>
    {
        var input=Selected();var receipt=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(Root,"reopen-receipt.json")))!;Assert.AreNotEqual(receipt.ProducerProcessId,Environment.ProcessId);Assert.AreEqual(receipt.DataRoot,AppDataPaths.Root,true);
        var vm=main.ReferenceColorPage;await vm.LoadSessionAsync(Path.Combine(Root,"review.ptstudio.json"));await Render(vm);Assert.AreEqual(receipt.TargetId,vm.ActiveTarget!.Id);Assert.AreEqual(receipt.VersionId,vm.ActiveTarget.ActiveEditVersionId);Assert.AreEqual(receipt.Stack,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));Save(Display(vm),"independent-reopen-preview");
        using(var original=File.OpenRead(Path.Combine(Root,"default-export",Path.GetFileNameWithoutExtension(input.name)+"_仿色.tif"))){var decoded=TiffReadBack.Read(original);await Write("reopen-decoded-master-comparison",CompareMaster(decoded.Rgb48Samples,vm.ActiveTarget.RawMaster!.Image));}
        await Write("reopen-observation",new{producerPid=receipt.ProducerProcessId,reopenPid=Environment.ProcessId,expectedPreview=receipt.PreviewSha256,actualPreview=Digest(Display(vm)),sameStack=receipt.Stack==JsonSerializer.Serialize(vm.Editor.AdjustmentStack),sameVersion=receipt.VersionId==vm.ActiveTarget.ActiveEditVersionId,sourceUnchanged=input.sha256==Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input.path)))});
        await Export(vm,"independent-reopen");
        Assert.AreEqual(receipt.PreviewSha256,Digest(Display(vm)));Assert.IsNull(vm.ActiveTarget.RawDefinition);
        Save(Display(vm),"independent-reopen-preview");await Write("independent-reopen",new{producerPid=receipt.ProducerProcessId,reopenPid=Environment.ProcessId,sameStack=true,sameVersion=true,previewExact=true,processingDomain=vm.ActiveTarget.RawMaster!.Image.WorkingColorSpace,sourceUnchanged=receipt.SourceSha256==Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input.path)))});
    });
}

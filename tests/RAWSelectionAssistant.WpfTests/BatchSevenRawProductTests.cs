using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Publishing;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Services.Publishing;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchSevenRawProductTests
{
    private static ProfessionalRawMaster Master(int w,int h,ushort orientation=1)
    {var d=new ProfessionalRawDefinition();var rgb=new float[w*h*3];for(var y=0;y<h;y++)for(var x=0;x<w;x++){var i=(y*w+x)*3;rgb[i]=.08f+x/(float)w*.4f;rgb[i+1]=.13f+y/(float)h*.3f;rgb[i+2]=.19f;}
        return new(new(w,h,rgb,"16",ProfessionalRawPipeline.WorkingSpace,orientation),new("synthetic","Test","Synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));}
    [TestMethod]
    public async Task ThreeRealEncodersPreservePrecisionOrientationProfileAndPixelContract()
    {
        var root=Path.Combine(Path.GetTempPath(),"PV4-output-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try{foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg}){
            var master=Master(91,63,6);var path=Path.Combine(root,format+".bin");var result=await ProfessionalRawExport.WriteAsync(master,new(),path,format);Assert.IsTrue(result.Srgb);Assert.AreEqual(format==StudioExportFormat.Tiff?91:63,result.Width);Assert.AreEqual(format==StudioExportFormat.Jpeg?8:16,result.BitDepth);
            var checkedResult=ProfessionalRawExport.Inspect(path,master,new(),format);Assert.AreEqual(format==StudioExportFormat.Jpeg?95:null,checkedResult.JpegQuality);Assert.IsTrue(checkedResult.MeanAbsoluteError<=5);}}
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task PublishingKeepsRawDefinitionRejectsFakeIccAndUsesActualJpegQuality()
    {
        var root=Path.Combine(Path.GetTempPath(),"PV4-publish-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var source=Path.Combine(root,"intermediate.tif");var master=Master(97,73,6);await ProfessionalRawExport.WriteAsync(master,new(),source,StudioExportFormat.Tiff);
            await StudioExportReceiptStore.WriteAsync(source,new(Guid.NewGuid(),null,null,master.Contract.SourceSha256,Guid.NewGuid(),4,"intermediate",source,"",DateTimeOffset.UtcNow,new(1,".tif",16,"Original",97,73,null,null,"None","sRGB","orientation","none",RawDefinition:new(),RawInput:master.Contract)));
            var service=new PublishingExportService(new WpfPublishingRenderer());
            foreach(var format in new[]{PublishingOutputFormat.Jpeg,PublishingOutputFormat.Png,PublishingOutputFormat.Tiff}){
                var folder=Path.Combine(root,format.ToString());var options=new PublishingOptions(new(false,JpegQuality:91,Dpi:240),false,[],format,OutputBitDepth:format==PublishingOutputFormat.Tiff?PublishingOutputBitDepth.Sixteen:PublishingOutputBitDepth.Eight,MetadataPolicy:ExportRecipeMetadataPolicy.Strip);
                var result=await service.ExportAsync(Guid.NewGuid(),new([source],folder,options));Assert.AreEqual(1,result.Items.Count(x=>x.State==PublishingItemState.Completed),result.Items[0].ErrorMessage);
                var receipt=await ExportRecovery.ReadAsync(result.Items[0].DestinationPath+".kitao-export.json");Assert.AreEqual(4,receipt.ProcessingVersion);Assert.IsNotNull(receipt.RecipeSnapshot!.RawDefinition);Assert.AreEqual(format==PublishingOutputFormat.Jpeg?91:null,receipt.RecipeSnapshot.Quality);Assert.AreEqual(format==PublishingOutputFormat.Tiff?16:8,receipt.RecipeSnapshot.BitDepth);
                using var file=File.OpenRead(result.Items[0].DestinationPath!);var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];Assert.IsTrue(frame.ColorContexts?.Count>0);Assert.AreEqual(format==PublishingOutputFormat.Tiff?97:73,frame.PixelWidth);Assert.AreEqual(format==PublishingOutputFormat.Tiff?73:97,frame.PixelHeight);Assert.AreEqual(240,Math.Round(frame.DpiX));
            }
            var rejected=await service.ExportAsync(Guid.NewGuid(),new([source],Path.Combine(root,"bad"),new(new(false),false,[],ColorSpaceProfile:"Fake Adobe RGB")));Assert.AreEqual(PublishingItemState.Failed,rejected.Items[0].State);
            var wrongDepth=await service.ExportAsync(Guid.NewGuid(),new([source],Path.Combine(root,"wrong-depth"),new(new(false),false,[],OutputBitDepth:PublishingOutputBitDepth.Sixteen)));Assert.AreEqual(PublishingItemState.Failed,wrongDepth.Items[0].State);
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task NativeViewportDebouncesAndRejectsStaleExposureAndKeepsOneMaster()
    {
        var root=Path.Combine(Path.GetTempPath(),"PV4-detail-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var path=Path.Combine(root,"fake.arw");await File.WriteAllBytesAsync(path,[1,2,3]);using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new Legacy(),professionalRawDecoder:new Decoder());
            await workspace.LoadTargetAsync(path);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Assert.IsTrue(workspace.IsProfessionalRaw);
            var old=workspace.UpdateProfessionalViewportAsync(new(100,120,64,48),1,1);var newer=workspace.UpdateProfessionalViewportAsync(new(200,220,64,48),1,1);await Task.WhenAll(old,newer);
            Assert.AreEqual(new Rect(200,220,64,48),workspace.Editor.IndependentDetailRegion);Assert.AreEqual(64,workspace.Editor.IndependentDetailMatched!.PixelWidth);Assert.AreEqual(1800,workspace.Editor.SourcePixelSize.Width);
            workspace.RequestProfessionalViewport(new(20,20,32,32),1,1);Assert.IsTrue(workspace.StopProcessingCommand.CanExecute(null));workspace.StopProcessingCommand.Execute(null);await workspace.ProfessionalDetailWork;Assert.IsNull(workspace.Editor.IndependentDetailMatched);
            var stale=workspace.UpdateProfessionalViewportAsync(new(20,20,32,32),1,1);workspace.RawExposureEv=1;await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);await stale;Assert.IsNull(workspace.Editor.IndependentDetailMatched);
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task AuthorizedRawQuickExportAllFormatsNativeDetailAndPublishingRecordRealResults()
    {
        var corpus=Environment.GetEnvironmentVariable("KITAO_BATCH07_CORPUS");var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH07_EVIDENCE");
        if(string.IsNullOrEmpty(corpus)||string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Authorized local corpus opt-in required.");return;}
        Directory.CreateDirectory(evidence);var records=new List<object>();
        foreach(var name in new[]{"DSC04831.ARW","CANON-5DM3.CR2"})
        {
            var root=Path.Combine(Path.GetTempPath(),"PV4-real-"+Guid.NewGuid());Directory.CreateDirectory(root);
            try
            {
                var input=Path.Combine(root,name);File.Copy(Path.Combine(corpus,name),input);var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input)));
                using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await workspace.LoadTargetAsync(input);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Assert.IsTrue(workspace.IsProfessionalRaw,workspace.StatusText);
                var timer=System.Diagnostics.Stopwatch.StartNew();await workspace.UpdateProfessionalViewportAsync(new(200,200,512,512),1,1);var detailMs=timer.ElapsedMilliseconds;Assert.AreEqual(512,workspace.Editor.IndependentDetailMatched!.PixelWidth);
                foreach(var format in new[]{StudioExportFormat.Jpeg,StudioExportFormat.Tiff,StudioExportFormat.Png})
                {
                    workspace.QuickExportFormat=format;timer.Restart();await workspace.ExportSelectedCommand.ExecuteAsync(null);var exportMs=timer.ElapsedMilliseconds;Assert.AreEqual(ReferenceExportStatus.Succeeded,workspace.ActiveTarget!.ExportStatus,format+" "+workspace.ActiveTarget.Error);
                    var output=workspace.ActiveTarget.OutputPath!;var actual=ProfessionalRawExport.Inspect(output,workspace.ActiveTarget.LinearRawMaster!,workspace.ActiveTarget.RawDefinition!,format);var receipt=await ExportRecovery.ReadAsync(output+".kitao-export.json");Assert.AreEqual(actual.BitDepth,receipt.RecipeSnapshot!.BitDepth);Assert.IsNotNull(receipt.RecipeSnapshot.OutputValidation);records.Add(new{model=workspace.ActiveTarget.RawInput!.Model,format=format.ToString(),actual,detailMs,exportMs,peakWorkingSet=System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64,receipt.OutputHash});
                }
                var publish=new PublishingExportService(new WpfPublishingRenderer());string? intermediate=null;workspace.OpenPublishing=paths=>intermediate=paths.Single();await workspace.PreparePublishingCommand.ExecuteAsync(null);Assert.IsNotNull(intermediate);
                foreach(var format in new[]{PublishingOutputFormat.Tiff,PublishingOutputFormat.Png,PublishingOutputFormat.Jpeg})
                {
                    var options=new PublishingOptions(new(false,JpegQuality:95),false,[],format,OutputBitDepth:format==PublishingOutputFormat.Tiff?PublishingOutputBitDepth.Sixteen:PublishingOutputBitDepth.Eight,MetadataPolicy:ExportRecipeMetadataPolicy.Strip);
                    var result=await publish.ExportAsync(Guid.NewGuid(),new([intermediate!],Path.Combine(root,"publish-"+format),options));Assert.AreEqual(PublishingItemState.Completed,result.Items[0].State,result.Items[0].ErrorMessage);var receipt=await ExportRecovery.ReadAsync(result.Items[0].DestinationPath+".kitao-export.json");Assert.IsNotNull(receipt.RecipeSnapshot!.RawDefinition);Assert.IsNotNull(receipt.RecipeSnapshot.OutputValidation);records.Add(new{model=workspace.ActiveTarget!.RawInput!.Model,publishing=format.ToString(),receipt.RecipeSnapshot,receipt.OutputHash});
                }
                Assert.AreEqual(hash,Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input))));
                // Managed Publishing intermediates outside the test workspace are deleted only after successful hash-verified use.
                var intermediateDirectory=Path.GetDirectoryName(intermediate!)!;if(intermediateDirectory.StartsWith(Path.Combine(Path.GetTempPath(),"PixelTartPublishing"),StringComparison.OrdinalIgnoreCase))Directory.Delete(intermediateDirectory,true);
            }
            finally{Directory.Delete(root,true);}GC.Collect();GC.WaitForPendingFinalizers();
        }
        await File.WriteAllTextAsync(Path.Combine(evidence,"product-results.json"),System.Text.Json.JsonSerializer.Serialize(records,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    [TestMethod]
    public async Task CancellingRowEncoderPreservesExistingDestinationAndLeavesNoPartialOutput()
    {
        var root=Path.Combine(Path.GetTempPath(),"PV4-cancel-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var master=Master(97,73);var occupied=Path.Combine(root,"occupied");await File.WriteAllBytesAsync(occupied,[7,8,9]);await Assert.ThrowsAsync<IOException>(()=>ProfessionalRawExport.WriteAsync(master,new(),occupied,StudioExportFormat.Jpeg));CollectionAssert.AreEqual(new byte[]{7,8,9},await File.ReadAllBytesAsync(occupied));
            foreach(var format in new[]{StudioExportFormat.Jpeg,StudioExportFormat.Png,StudioExportFormat.Tiff}){using var cancelled=new CancellationTokenSource();var path=Path.Combine(root,format.ToString());await Assert.ThrowsAsync<OperationCanceledException>(()=>ProfessionalRawExport.WriteAsync(master,new(),path,format,cancelled.Token,stage:stage=>{if(stage=="Encoded")cancelled.Cancel();}));Assert.IsFalse(File.Exists(path));Assert.AreEqual(0,Directory.GetFiles(root,"*.tmp").Length);}
            Assert.AreEqual(1,ProfessionalRawResourcePolicy.HeavyWork.CurrentCount);
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Decoder:IProfessionalRawDecoder
    {public async Task<ProfessionalRawMaster> DecodeAsync(string path,ProfessionalRawDefinition d,CancellationToken token=default){var m=Master(1800,1200);return m with{Contract=m.Contract with{SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path,token))),DecodeKey=d.DecodeKey}};}}
    private sealed class Legacy:IRawDecoder
    {public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>Task.FromResult(RawDecodedImage.FromRgb48(16,12,96,new ushort[16*12*3],new("Test","Synthetic",null,1,"sRGB")));}
    private sealed class Dialogs(string root):IDialogService
    {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}}
}

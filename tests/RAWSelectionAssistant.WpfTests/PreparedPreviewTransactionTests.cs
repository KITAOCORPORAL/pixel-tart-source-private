using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class PreparedPreviewTransactionTests
{
    [TestMethod] public Task PreparedApplyRendersOnceCachesComparisonAndKeepsOneHistoryStep()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"input.png",71));
            vm.InstantEngineVersion="V3";vm.InstantInputMode="Original";vm.InstantDevelopmentDemo=false;
            using(var trace=StudioPreviewDiagnostics.Begin())
            {
                await vm.GenerateInstantFilmCommand.ExecuteAsync(null);
                Assert.AreEqual(1,trace.Observations.Count(x=>x.Stage=="EffectPreview"));Assert.AreEqual(1,trace.Observations.Count(x=>x.Stage=="ComparePreview"));
            }
            var before=Pixels(vm.Editor.MatchedImage!);var count=vm.ActiveTarget!.EditVersions.Count;
            var oldRecipe=JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition);
            // Identical frozen recipe is eligible for comparison reuse; changing response
            // remains conservatively keyed even when Amount=0.
            using(var trace=StudioPreviewDiagnostics.Begin())
            {
                await vm.GenerateInstantFilmCommand.ExecuteAsync(null);
                Assert.AreEqual(1,trace.Observations.Count(x=>x.Stage=="EffectPreview"));Assert.AreEqual(0,trace.Observations.Count(x=>x.Stage=="ComparePreview"),"Old="+oldRecipe+" New="+JsonSerializer.Serialize(vm.ActiveTarget.RawDefinition));
            }
            Assert.AreEqual(count+1,vm.ActiveTarget.EditVersions.Count);Assert.AreEqual(before,Pixels(vm.Editor.MatchedImage!));
            await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.AreEqual(before,Pixels(vm.Editor.MatchedImage!));
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task SrgbUsesOriginalFileNotCurrentEditsAndUndoRestoresPixels()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            var path=Image(root,"input.png",61);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(path);
            vm.Editor.Exposure=.8;await Task.Delay(600);var priorPixels=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);var stack=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);
            vm.SrgbExposureEv=.3;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);Assert.IsNotNull(vm.ActiveTarget!.RawDefinition,vm.StatusText);
            var definition=vm.ActiveTarget.RawDefinition!;using var expectedMaster=(await RenderedSrgbInput.ReadAsync(path,definition,CancellationToken.None)).Image;
            CollectionAssert.AreEqual(expectedMaster.Rgb32.ToArray(),vm.ActiveTarget.LinearRawMaster!.Image.Rgb32.ToArray());
            Assert.IsNull(vm.ActiveTarget.ColorAdjustmentStackSnapshot);var exposedPixels=Pixels(vm.Editor.MatchedImage!);
            await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(vm.ActiveTarget.RawDefinition);Assert.AreEqual(stack,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));Assert.AreEqual(priorPixels,Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!));
            await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(exposedPixels,Pixels(vm.Editor.MatchedImage!));
        }finally{Directory.Delete(root,true);}
    });
    [DataTestMethod][DataRow("PreviewPrepared")][DataRow("PreviewCommit")]
    public Task SrgbFailureRollsBackPixelsModeRecipeAndHistory(string phase)=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"input.png",79));vm.SrgbExposureEv=.2;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            var target=vm.ActiveTarget!;var version=target.ActiveEditVersionId;var definition=JsonSerializer.Serialize(target.RawDefinition);var pixels=Pixels(vm.Editor.MatchedImage!);var count=target.EditVersions.Count;var mode=vm.Editor.IndependentRawMode;
            vm.SrgbExposureEv=.8;using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p==phase)throw new IOException("injected prepared/presentation failure");}))await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            Assert.AreEqual(version,target.ActiveEditVersionId);Assert.AreEqual(definition,JsonSerializer.Serialize(target.RawDefinition));Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));Assert.AreEqual(mode,vm.Editor.IndependentRawMode);Assert.AreEqual(count,target.EditVersions.Count);Assert.IsNotNull(vm.LastProcessingFailure);Assert.IsFalse(vm.IsInstantGenerating);
            await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.IsNull(target.RawDefinition);await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task SrgbRejectsChangedFileIncludingCachedInputAndPreparedBoundary()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            var path=Image(root,"input.png",55);var bytes=File.ReadAllBytes(path);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(path);vm.SrgbExposureEv=.2;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            var target=vm.ActiveTarget!;var id=target.ActiveEditVersionId;var pixels=Pixels(vm.Editor.MatchedImage!);var count=target.EditVersions.Count;
            Image(root,"input.png",99);await vm.ApplySrgbExposureCommand.ExecuteAsync(null);Assert.AreEqual(id,target.ActiveEditVersionId);Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));Assert.IsNotNull(vm.LastProcessingFailure);
            File.WriteAllBytes(path,bytes);vm.SrgbExposureEv=.9;
            using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PreviewPrepared")Image(root,"input.png",101);}))await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            Assert.AreEqual(id,target.ActiveEditVersionId);Assert.AreEqual(count,target.EditVersions.Count);Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task CommitRemainsBusyAndCancellationCannotAppendHistory()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"input.png",82));vm.SrgbExposureEv=.2;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);var id=vm.ActiveTarget!.ActiveEditVersionId;var count=vm.ActiveTarget.EditVersions.Count;var pixels=Pixels(vm.Editor.MatchedImage!);
            bool visited=false;using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PreviewCommit"){visited=true;Assert.IsTrue(vm.IsInstantGenerating);Assert.IsFalse(vm.ApplySrgbExposureCommand.CanExecute(null));vm.ExitInstantPresentationForLifecycle();}}))await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            Assert.IsTrue(visited);Assert.AreEqual(id,vm.ActiveTarget.ActiveEditVersionId);Assert.AreEqual(count,vm.ActiveTarget.EditVersions.Count);Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task ReopenTransfersOnePreparedMasterAndFailureKeepsOldWorkspace()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            var path=Image(root,"input.png",65);var project=Path.Combine(root,"project.ptstudio.json");using(var creator=new ReferenceColorWorkspaceViewModel(new Dialogs(root))){await creator.LoadTargetAsync(path);creator.SrgbExposureEv=.4;await creator.ApplySrgbExposureCommand.ExecuteAsync(null);await creator.SaveSessionAsync(project);}
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Image(root,"old.png",29));vm.Editor.Exposure=.6;await Task.Delay(500);var old=vm.ActiveTarget;var oldStack=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);var oldPixels=Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!);
            using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="SessionTransfer")throw new IOException("Transfer failed");}))await Assert.ThrowsAsync<IOException>(()=>vm.LoadSessionAsync(project));
            Assert.AreSame(old,vm.ActiveTarget);Assert.AreEqual(oldStack,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));Assert.AreEqual(oldPixels,Pixels(vm.Editor.MatchedImage??vm.Editor.SourceImage!));
            using(var trace=StudioPreviewDiagnostics.Begin()){await vm.LoadSessionAsync(project);Assert.AreEqual(1,trace.Observations.Count(x=>x.Stage=="DecodeMaster"));Assert.AreEqual(1,trace.Observations.Count(x=>x.Stage=="EffectPreview"));}
            Assert.AreEqual(.4,vm.ActiveTarget!.RawDefinition!.SrgbExposure!.ExposureEv);Assert.IsNotNull(vm.ActiveTarget.LinearRawMaster);Assert.IsTrue(vm.Editor.IndependentRawMode);
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task SwitchAndDisposeRejectPendingExposureWithoutRestoringWrongViewOrLease()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            var first=Image(root,"input.png",54);var second=Image(root,"second.png",123);
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(first);vm.SrgbExposureEv=.2;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);var original=vm.ActiveTarget!;var version=original.ActiveEditVersionId;
            Task? switching=null;using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PreviewPrepared")switching=vm.LoadTargetAsync(second);}))await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            if(switching is not null)await switching;Assert.AreEqual(second,vm.ActiveTarget!.Path);Assert.IsNull(vm.ActiveTarget.RawDefinition);Assert.AreEqual(version,original.ActiveEditVersionId);
            var next=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await next.LoadTargetAsync(first);next.SrgbExposureEv=.1;await next.ApplySrgbExposureCommand.ExecuteAsync(null);var held=StudioGlobalResources.AccountedBytes;var account=next.ActiveTarget!.LinearRawMaster!.Image.Rgb32.Length*4L;
            using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="PreviewCommit")next.Dispose();}))await next.ApplySrgbExposureCommand.ExecuteAsync(null);
            Assert.IsNull(next.ActiveTarget.LinearRawMaster);Assert.IsTrue(StudioGlobalResources.AccountedBytes<=held-account);next.Dispose();
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod] public Task ExposureWrongDecodeKeyRebuildsAndCodecFailuresAreStructured()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=NewRoot();try
        {
            var path=Image(root,"input.png",58);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(path);vm.SrgbExposureEv=.2;await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            var target=vm.ActiveTarget!;var old=target.LinearRawMaster!;target.LinearRawMaster=old with{Contract=old.Contract with{DecodeKey="wrong"}};
            using(var trace=StudioPreviewDiagnostics.Begin()){await vm.ApplySrgbExposureCommand.ExecuteAsync(null);Assert.IsTrue(trace.Observations.Any(x=>x.Stage=="SecondDecode"));}
            Assert.AreEqual(target.RawDefinition!.DecodeKey,target.LinearRawMaster!.Contract.DecodeKey);
            var pixels=Pixels(vm.Editor.MatchedImage!);var id=target.ActiveEditVersionId;target.LinearRawMaster=null;
            using(var capture=CandidateProcessingEvidence.Capture(p=>{if(p=="SecondDecode")throw new System.Runtime.InteropServices.COMException("codec boundary");}))await vm.ApplySrgbExposureCommand.ExecuteAsync(null);
            Assert.IsNotNull(vm.LastProcessingFailure);Assert.AreEqual(id,target.ActiveEditVersionId);Assert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));Assert.IsFalse(vm.IsInstantGenerating);
        }finally{Directory.Delete(root,true);}
    });
    [DataTestMethod][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)][DataRow(5)][DataRow(6)][DataRow(7)][DataRow(8)]
    public async Task MappedTiffReadsExactlyOldCodesAndReleasesFile(int orientation)
    {
        var root=NewRoot();var held=StudioGlobalResources.AccountedBytes;try
        {
            var path=Path.Combine(root,"source.tif");await AtomicTiffWriter.WriteRowsAsync(path,19,13,(y,row)=>{for(int i=0;i<57;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i*2)..],(ushort)((i*379+y*121)%65536));},new(IccProfile:StudioQuickExport.SrgbProfileBytes(),Orientation:(ushort)orientation));
            var input=SecondExposureDecoder.Describe(path);Assert.AreEqual(3,input.Version);using(var reference=BoundedSrgbTiffInput.Read(input,CancellationToken.None))using(var mapped=new BoundedTiffPixelSource(input,CancellationToken.None))
            {
                Parallel.For(0,reference.Height,y=>{for(int x=0;x<reference.Width;x++){var v=mapped.Read(x,y);int i=(y*reference.Width+x)*3;Assert.AreEqual((double)reference.Rgb32.Span[i],v.X);Assert.AreEqual((double)reference.Rgb32.Span[i+1],v.Y);Assert.AreEqual((double)reference.Rgb32.Span[i+2],v.Z);}});
                Assert.Throws<IOException>(()=>{using var f=File.Open(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite);});
            }
            using(var f=File.Open(path,FileMode.Open,FileAccess.Write,FileShare.None)){}Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);
        }finally{Directory.Delete(root,true);}
    }
    private static string NewRoot(){var p=Path.Combine(Path.GetTempPath(),"PreparedPreview-"+Guid.NewGuid());Directory.CreateDirectory(p);return p;}
    private static string Image(string root,string name,byte value){var p=Path.Combine(root,name);StudioQuickExport.Encode(BitmapSource.Create(61,43,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat(value,61*43*3).ToArray(),61*3),p,p,StudioExportFormat.Png);return p;}
    private static string Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);var b=new byte[rgb.PixelWidth*rgb.PixelHeight*6];rgb.CopyPixels(b,rgb.PixelWidth*6,0);return Convert.ToHexString(SHA256.HashData(b));}
    private sealed class Dialogs(string root):IDialogService
    {public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool a)=>false;public void RevealFile(string p){}}
}

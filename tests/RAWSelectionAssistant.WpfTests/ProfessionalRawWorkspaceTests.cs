using System.IO;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class ProfessionalRawWorkspaceTests
{
    [TestMethod]
    public async Task ExplicitVersionPreviewSaveReloadAndFullExportUseSameDefinition()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-PV4-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var input=Path.Combine(root,"synthetic.arw");await File.WriteAllBytesAsync(input,[1,2,3]);var dialogs=new Dialogs(root);var linear=new FakeLinear();var versions=new StudioVersionRepository(Path.Combine(root,"versions"));
            using var workspace=new ReferenceColorWorkspaceViewModel(dialogs,rawDecoder:new FakeLegacy(),versionRepository:versions,professionalRawDecoder:linear);
            await workspace.LoadTargetAsync(input);var oldId=workspace.ActiveTarget!.ActiveEditVersionId;
            await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Assert.IsTrue(workspace.IsProfessionalRaw);Assert.AreNotEqual(oldId,workspace.ActiveTarget.ActiveEditVersionId);Assert.IsTrue(workspace.Editor.IndependentRawMode);Assert.IsNotNull(workspace.Editor.MatchedImage);
            workspace.RawExposureEv=1;await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);Assert.AreEqual(1,workspace.ActiveTarget.RawDefinition!.ExposureEv);Assert.AreEqual(1,linear.Decodes,"Exposure reuses the linear master");var valid=workspace.ActiveTarget.RawDefinition;linear.Fail=true;workspace.RawUseCameraWhiteBalance=false;workspace.RawRedGain=2;await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);Assert.AreEqual(valid,workspace.ActiveTarget.RawDefinition);linear.Fail=false;
            var file=Path.Combine(root,"session.ptstudio.json");await workspace.SaveSessionAsync(file);await workspace.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,workspace.ActiveTarget.ExportStatus);
            using(var stream=File.OpenRead(workspace.ActiveTarget.OutputPath!)){var tiff=TiffReadBack.Read(stream);Assert.AreEqual(16,tiff.BitsPerSample);Assert.AreEqual(12,tiff.Width);}
            var receipt=await ExportRecovery.ReadAsync(workspace.ActiveTarget.OutputPath+".kitao-export.json");Assert.AreEqual(4,receipt.ProcessingVersion);Assert.AreEqual(workspace.ActiveTarget.RawDefinition,receipt.RecipeSnapshot!.RawDefinition);Assert.AreEqual(workspace.ActiveTarget.SourceHash,receipt.RecipeSnapshot.RawInput!.SourceSha256.ToLowerInvariant());
            using var restored=new ReferenceColorWorkspaceViewModel(dialogs,rawDecoder:new FakeLegacy(),versionRepository:versions,professionalRawDecoder:new FakeLinear());await restored.LoadSessionAsync(file);Assert.IsTrue(restored.IsProfessionalRaw);Assert.AreEqual(1,restored.ActiveTarget!.RawDefinition!.ExposureEv);CollectionAssert.AreEqual(Pixels(workspace.Editor.MatchedImage!),Pixels(restored.Editor.MatchedImage!));
            await workspace.SaveEditVersionsAsync(); // Shared catalog persistence is explicit; the portable project save does not write it.
            var legacyDecoder=new FakeLegacy();using(var fromCatalog=new ReferenceColorWorkspaceViewModel(dialogs,rawDecoder:legacyDecoder,versionRepository:versions,professionalRawDecoder:new FakeLinear()))
            {await fromCatalog.LoadTargetAsync(input);Assert.IsTrue(fromCatalog.IsProfessionalRaw);Assert.AreEqual(1,fromCatalog.ActiveTarget!.RawDefinition!.ExposureEv);Assert.AreEqual(0,legacyDecoder.Decodes,"Persistent PV4 must not invoke legacy decoder");}
            var old=restored.ActiveTarget.EditVersions.First(v=>v.RawDefinition is null);await restored.SwitchEditVersionAsync(old.Id);Assert.IsFalse(restored.IsProfessionalRaw);Assert.IsFalse(restored.Editor.IndependentRawMode);
            await restored.SwitchEditVersionAsync(receipt.EditVersionId);Assert.IsTrue(restored.IsProfessionalRaw);restored.RawUseCameraWhiteBalance=false;restored.RawRedGain=1.25;await restored.ApplyProfessionalRawCommand.ExecuteAsync(null);Assert.AreEqual("CameraGains",restored.ActiveTarget.RawDefinition!.WhiteBalance);Assert.AreEqual(1.25,restored.ActiveTarget.RawDefinition.RedGain);
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task R53OriginalToExistingProfessionalVariantRefreshesReadOnlyState()
    {
        var root = Path.Combine(Path.GetTempPath(), "R53-raw-identity-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "synthetic.arw"); await File.WriteAllBytesAsync(input, [1,2,3]);
            using var vm = new ReferenceColorWorkspaceViewModel(new Dialogs(root), rawDecoder: new FakeLegacy(), professionalRawDecoder: new FakeLinear());
            await vm.LoadTargetAsync(input);
            await vm.CreateProfessionalRawCommand.ExecuteAsync(null);
            var professional = vm.ActiveTarget!.ActiveEditVersionId!.Value;
            await vm.SwitchEditVersionAsync(vm.R53Variants.Single(v => v.IsOriginal).Id);
            Assert.IsTrue(vm.Editor.ReadOnlyTarget);
            await vm.SwitchEditVersionAsync(professional);
            Assert.IsFalse(vm.Editor.ReadOnlyTarget);
            Assert.IsTrue(vm.IsProfessionalRaw);
            Assert.AreEqual(professional, vm.ActiveTarget.ActiveEditVersionId);
        }
        finally { Directory.Delete(root, true); }
    }
    private static byte[] Pixels(System.Windows.Media.Imaging.BitmapSource image){var stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;var result=new byte[stride*image.PixelHeight];image.CopyPixels(result,stride,0);return result;}
    [TestMethod]
    public async Task DecodeFailureKeepsOldVersionAndParametersAndLateCreationCannotReplaceAnotherTarget()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-PV4-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var input=Path.Combine(root,"synthetic.arw");await File.WriteAllBytesAsync(input,[1,2,3]);
            using(var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:new Failing()))
            {await workspace.LoadTargetAsync(input);workspace.CreateEditVersion();var initialCount=workspace.ActiveTarget!.EditVersions.Count;var id=workspace.ActiveTarget!.ActiveEditVersionId;await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Assert.IsFalse(workspace.IsProfessionalRaw);Assert.AreEqual(id,workspace.ActiveTarget.ActiveEditVersionId);Assert.AreEqual(initialCount,workspace.ActiveTarget.EditVersions.Count);Assert.IsTrue(workspace.StatusText.Contains("缺少配置"));}
            var delayed=new Delayed();using var concurrent=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:delayed);
            await concurrent.LoadTargetAsync(input);var old=concurrent.ActiveTarget!;var create=concurrent.CreateProfessionalRawCommand.ExecuteAsync(null);await delayed.Started.Task;
            var second=Path.Combine(root,"second.arw");await File.WriteAllBytesAsync(second,[4,5,6]);await concurrent.LoadTargetAsync(second);delayed.Release.SetResult();await create;
            Assert.IsFalse(concurrent.IsProfessionalRaw);Assert.IsNull(old.RawDefinition);Assert.AreEqual(second,concurrent.ActiveTarget!.Path);
        }
        finally{Directory.Delete(root,true);}
    }
    [TestMethod]
    public async Task AuthorizedRealFilesUseProductVersionSaveReopenPreviewAndTiffExport()
    {
        var corpus=Environment.GetEnvironmentVariable("KITAO_BATCH06_CORPUS");var evidence=Environment.GetEnvironmentVariable("KITAO_BATCH06_EVIDENCE");
        if(string.IsNullOrEmpty(corpus)||string.IsNullOrEmpty(evidence)){Assert.Inconclusive("Set authorized batch06 corpus and evidence directory; not a synthetic test.");return;}
        Directory.CreateDirectory(evidence);var results=new List<object>();
        foreach(var name in new[]{"CANON-5DM3.CR2","DSC04831.ARW"})
        {
            var root=Path.Combine(Path.GetTempPath(),"Kitao-PV4-real-"+Guid.NewGuid());Directory.CreateDirectory(root);
            try
            {
                var input=Path.Combine(root,name);File.Copy(Path.Combine(corpus,name),input);var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input)));
                var versions=new StudioVersionRepository(Path.Combine(root,"versions"));using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),versionRepository:versions);
                await workspace.LoadTargetAsync(input);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);Assert.IsTrue(workspace.IsProfessionalRaw,workspace.StatusText);
                workspace.RawExposureEv=1;await workspace.ApplyProfessionalRawCommand.ExecuteAsync(null);Assert.AreEqual(1,workspace.ActiveTarget!.RawDefinition!.ExposureEv);
                var expected=ProfessionalRawPipeline.RenderPreview(workspace.ActiveTarget.LinearRawMaster!,workspace.ActiveTarget.RawDefinition,1600);
                var expectedImage=StudioQuickExport.ApplyOrientation(RawDisplayBitmapAdapter.ToBitmap(expected.ToVisualRgb24()),expected.Orientation);
                CollectionAssert.AreEqual(Pixels(expectedImage),Pixels(workspace.Editor.MatchedImage!));
                var session=Path.Combine(root,"session.ptstudio.json");await workspace.SaveSessionAsync(session);await workspace.ExportSelectedCommand.ExecuteAsync(null);Assert.AreEqual(ReferenceExportStatus.Succeeded,workspace.ActiveTarget.ExportStatus,workspace.ActiveTarget.Error);
                using(var stream=File.OpenRead(workspace.ActiveTarget.OutputPath!)){var tiff=TiffReadBack.Read(stream);Assert.AreEqual(16,tiff.BitsPerSample);Assert.IsTrue(tiff.IccProfile.Length>0);Assert.AreEqual(expected.Orientation,tiff.Orientation);}
                var receipt=await ExportRecovery.ReadAsync(workspace.ActiveTarget.OutputPath+".kitao-export.json");Assert.AreEqual(workspace.ActiveTarget.RawDefinition,receipt.RecipeSnapshot!.RawDefinition);Assert.AreEqual(4,receipt.ProcessingVersion);
                using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root),versionRepository:versions);await restored.LoadSessionAsync(session);Assert.IsTrue(restored.IsProfessionalRaw,restored.StatusText);CollectionAssert.AreEqual(Pixels(workspace.Editor.MatchedImage!),Pixels(restored.Editor.MatchedImage!));
                Assert.AreEqual(hash,Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(input))));
                results.Add(new{model=workspace.ActiveTarget.RawInput!.Model,sourceHash=hash,saveReopen=true,previewOracle=true,tiff16=true,icc=true,processingVersion=receipt.ProcessingVersion,outputHash=receipt.OutputHash});
            }
            finally{Directory.Delete(root,true);}
            GC.Collect();GC.WaitForPendingFinalizers();
        }
        await File.WriteAllTextAsync(Path.Combine(evidence,"product-real.json"),System.Text.Json.JsonSerializer.Serialize(results,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
    [TestMethod]
    public async Task LegacyRawSessionTransferKeepsHighPrecisionSourceForSubsequentEditing()
    {
        var root=Path.Combine(Path.GetTempPath(),"Kitao-LegacyTransfer-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            var input=Path.Combine(root,"synthetic.arw");await File.WriteAllBytesAsync(input,[1,2,3]);var session=Path.Combine(root,"legacy.ptstudio.json");
            using var first=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy());await first.LoadTargetAsync(input);first.Editor.Exposure=.3;await Task.Delay(500);await first.SaveSessionAsync(session);
            using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy());await restored.LoadSessionAsync(session);
            Assert.IsNull(restored.ActiveTarget!.RawDefinition);Assert.IsFalse(restored.Editor.IndependentRawMode);
            first.Editor.Exposure=.8;restored.Editor.Exposure=.8;await Task.Delay(650);
            Assert.IsNotNull(first.Editor.MatchedImage);Assert.IsNotNull(restored.Editor.MatchedImage);
            CollectionAssert.AreEqual(Pixels(first.Editor.MatchedImage),Pixels(restored.Editor.MatchedImage));
            using(var capture=Kitao.InstantFilm.Research.CandidateProcessingEvidence.Capture(p=>{if(p=="SessionTransfer")throw new IOException("rollback legacy source");}))await Assert.ThrowsAsync<IOException>(()=>restored.LoadSessionAsync(session));
            first.Editor.Exposure=.6;restored.Editor.Exposure=.6;await Task.Delay(650);
            CollectionAssert.AreEqual(Pixels(first.Editor.MatchedImage!),Pixels(restored.Editor.MatchedImage!));
            // Bitmap identity alone is insufficient: the next edit must retain the same
            // high precision RAW branch and frozen generation, not a decoded RGB24 proxy.
            Assert.AreSame(restored.ActiveTarget.RawMaster,typeof(TetherReferenceModeViewModel).GetField("_frozenRawMaster",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(restored.Editor));
            Assert.IsNotNull(typeof(TetherReferenceModeViewModel).GetField("_rawPreviewMaster",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(restored.Editor));
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Failing:IProfessionalRawDecoder
    {public Task<ProfessionalRawMaster> DecodeAsync(string path,ProfessionalRawDefinition definition,CancellationToken token=default)=>throw new InvalidDataException("缺少配置");}
    private sealed class Delayed:IProfessionalRawDecoder
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ProfessionalRawMaster> DecodeAsync(string path,ProfessionalRawDefinition definition,CancellationToken token=default){Started.SetResult();await Release.Task;return await new FakeLinear().DecodeAsync(path,definition,token);}
    }
    private sealed class FakeLinear:IProfessionalRawDecoder
    {
        public int Decodes; public bool Fail;
        public async Task<ProfessionalRawMaster> DecodeAsync(string path,ProfessionalRawDefinition definition,CancellationToken token=default)
        {if(Fail)throw new InvalidDataException("缺少配置");Decodes++;var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path,token)));var contract=new ProfessionalRawContract("synthetic","Test","Synthetic",hash,definition.DecodeKey,[1,1,1,1],[1,1,1,1],[1,0,0,0,0,1,0,0,0,0,1,0],65535);return new(new HighBitDepthImageBuffer(12,8,Enumerable.Repeat(.18f,12*8*3).ToArray(),"16","LibRaw Linear ProPhoto D65"),contract);}
    }
    private sealed class FakeLegacy:IRawDecoder
    {public int Decodes; public bool Fail;public RawDecoderCapability GetCapability()=>throw new NotSupportedException();public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default){Decodes++;return Task.FromResult(RawDecodedImage.FromRgb48(12,8,72,Enumerable.Repeat((ushort)20000,12*8*3).ToArray(),new("Test","Synthetic",null,1,"sRGB")));}}
    private sealed class Dialogs(string root):IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
    }
}

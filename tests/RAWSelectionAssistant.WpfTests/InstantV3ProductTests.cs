using System.IO;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class InstantV3ProductTests
{
 [TestMethod] public async Task RealV3ChoiceParametersUndoRedoProjectAndDirectExports()
 {
  var root=Path.Combine(Path.GetTempPath(),"InstantV3Product-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var input=Path.Combine(root,"synthetic.arw");await File.WriteAllBytesAsync(input,[1,2,3]);var versions=new StudioVersionRepository(Path.Combine(root,"versions"));
   using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),versionRepository:versions,professionalRawDecoder:new FakeLinear());
   await workspace.LoadTargetAsync(input);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);
   workspace.InstantEngineVersion="V3";workspace.InstantCameraAnimation=false;workspace.InstantV3Exposure=.4;workspace.InstantV3Texture=.02;workspace.InstantV3Seed=777;workspace.InstantV3Stock="Womb01351.Stock0";workspace.InstantV3Pattern="Leather";workspace.InstantV3Reflection=.15;workspace.InstantV3DefectKind="Crescent";workspace.InstantV3DefectAmount=.12;
   await workspace.GenerateInstantFilmCommand.ExecuteAsync(null);
   Assert.AreEqual(3,workspace.ActiveTarget!.RawDefinition!.InstantFilm!.AlgorithmVersion,workspace.StatusText);
   Assert.AreEqual(.4,workspace.ActiveTarget.RawDefinition.InstantFilm.V3!.Response.ExposureEv);
   var active=workspace.ActiveTarget.ActiveEditVersionId;await workspace.UndoInstantV3Command.ExecuteAsync(null);Assert.AreNotEqual(active,workspace.ActiveTarget.ActiveEditVersionId);
   await workspace.RedoInstantV3Command.ExecuteAsync(null);Assert.AreEqual(active,workspace.ActiveTarget.ActiveEditVersionId);
   var session=Path.Combine(root,"project.ptstudio.json");await workspace.SaveSessionAsync(session);
   using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),versionRepository:versions,professionalRawDecoder:new FakeLinear());await restored.LoadSessionAsync(session);
   Assert.AreEqual("V3",restored.InstantEngineVersion);Assert.AreEqual("Womb01351.Stock0",restored.InstantV3Stock);Assert.AreEqual("Leather",restored.InstantV3Pattern);Assert.AreEqual(.15,restored.InstantV3Reflection);Assert.AreEqual(777UL,restored.InstantV3Seed);
   var master=await new FakeLinear().DecodeAsync(input,restored.ActiveTarget!.RawDefinition!);
   foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg})
   {
    var file=Path.Combine(root,"direct-"+format+StudioQuickExport.Extension(input,format));var result=await ProfessionalRawExport.WriteAsync(master,restored.ActiveTarget.RawDefinition!,file,format);Assert.AreEqual(1,result.Orientation);Assert.IsTrue(result.Srgb);Assert.IsTrue(File.Exists(file));
   }
  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod] public async Task FiveFamiliesUseSameFormalModuleAndUnknownVersionDoesNotMigrate()
 {
  var root=Path.Combine(Path.GetTempPath(),"InstantV3Families-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var file=Path.Combine(root,"x.arw");await File.WriteAllBytesAsync(file,[1,2,3]);var decoder=new FakeLinear();
   foreach(var id in new[]{"itype-color","itype-bw","sx70-color","reclaimed-blue600","blue-duochrome600"})
   {
    using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:decoder);
    await workspace.LoadTargetAsync(file);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);workspace.InstantEngineVersion="V3";workspace.InstantCameraAnimation=false;workspace.SelectedInstantProfile=InstantFilmProfiles.Available.Single(p=>p.Id==id);workspace.InstantV3Exposure=.2;workspace.InstantV3Seed=991;
    await workspace.GenerateInstantFilmCommand.ExecuteAsync(null);var film=workspace.ActiveTarget!.RawDefinition!.InstantFilm!;Assert.AreEqual(3,film.AlgorithmVersion,workspace.StatusText);Assert.AreEqual(id,film.FilmId);Assert.AreEqual(991UL,film.V3!.Random.RootSeed);
    var master=await decoder.DecodeAsync(file,workspace.ActiveTarget.RawDefinition);using var region=ProfessionalRawPipeline.RenderRegion(master,workspace.ActiveTarget.RawDefinition,0,0,4,4);
    Assert.AreEqual(4,region.Width);Assert.Throws<InvalidDataException>(()=>(film with{AlgorithmVersion=999}).Validate());
    await workspace.UpdateProfessionalViewportAsync(new System.Windows.Rect(0,0,4,4),1,1);
   }
  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod] public async Task InflightGenerationSwitchDoesNotCommitOntoNextPhoto()
 {
  var root=Path.Combine(Path.GetTempPath(),"InstantV3Cancel-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var a=Path.Combine(root,"a.arw");var b=Path.Combine(root,"b.arw");await File.WriteAllBytesAsync(a,[1,2,3]);await File.WriteAllBytesAsync(b,[4,5,6]);
   using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:new FakeLinear());
   await workspace.LoadTargetAsync(a);await workspace.CreateProfessionalRawCommand.ExecuteAsync(null);var first=workspace.ActiveTarget!;var previous=first.RawDefinition;workspace.InstantEngineVersion="V3";workspace.InstantCameraAnimation=true;
   var task=workspace.GenerateInstantFilmAsync(false);var timer=System.Diagnostics.Stopwatch.StartNew();while(!workspace.IsInstantGenerating&&!task.IsCompleted){if(timer.ElapsedMilliseconds>5000)Assert.Fail("generation not entered");await Task.Delay(1);}
   Assert.IsTrue(workspace.IsInstantGenerating,"This must cancel running work, not a pre-cancel");workspace.ExitInstantPresentationForLifecycle();await workspace.LoadTargetAsync(b);await task;
   Assert.AreEqual(b,workspace.ActiveTarget!.Path);Assert.AreEqual(previous,first.RawDefinition);Assert.IsNull(workspace.ActiveTarget.RawDefinition);
  }
  finally{Directory.Delete(root,true);}
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

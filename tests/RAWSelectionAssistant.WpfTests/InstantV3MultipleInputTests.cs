using System.IO;using System.Security.Cryptography;using System.Windows.Media;using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Core.Services.RawToJpeg;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize] public sealed class InstantV3MultipleInputTests
{
 [TestMethod] public async Task SelectedSecondSourcePersistsRendersDirectExportsAndRejectsChangedSource()
 {
  var root=Path.Combine(Path.GetTempPath(),"V3Multiple-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var input=Path.Combine(root,"first.arw");await File.WriteAllBytesAsync(input,[1,2,3]);var second=Path.Combine(root,"second.png");var image=BitmapSource.Create(17,13,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,17*13).SelectMany(i=>new[]{(byte)(i%255),(byte)60,(byte)220}).ToArray(),17*3);StudioQuickExport.Encode(image,"x.png",second,StudioExportFormat.Png);
   var versions=new StudioVersionRepository(Path.Combine(root,"versions"));using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root,second),rawDecoder:new FakeLegacy(),versionRepository:versions,professionalRawDecoder:new FakeLinear());await vm.LoadTargetAsync(input);await vm.CreateProfessionalRawCommand.ExecuteAsync(null);vm.InstantEngineVersion="V3";vm.InstantCameraAnimation=false;
   await vm.SelectSecondExposureCommand.ExecuteAsync(null);Assert.IsNotNull(vm.InstantSecondInput);vm.InstantSecondWeight=.35;vm.InstantSecondCropX=.2;vm.InstantSecondZoom=1.25;await vm.GenerateInstantFilmCommand.ExecuteAsync(null);
   var definition=vm.ActiveTarget!.RawDefinition!;Assert.IsNotNull(definition.InstantFilm!.V3!.MultipleInput,vm.StatusText);Assert.AreEqual(.35,definition.InstantFilm.V3.MultipleInput.Weight);
   var file=Path.Combine(root,"multi.ptstudio.json");await vm.SaveSessionAsync(file);using var restored=new ReferenceColorWorkspaceViewModel(new Dialogs(root),versionRepository:versions,rawDecoder:new FakeLegacy(),professionalRawDecoder:new FakeLinear());await restored.LoadSessionAsync(file);Assert.AreEqual(.35,restored.InstantSecondWeight);Assert.AreEqual(1.25,restored.InstantSecondZoom);Assert.AreEqual("second.png",restored.InstantSecondSourceName);
   var master=await new FakeLinear().DecodeAsync(input,definition);foreach(var format in new[]{StudioExportFormat.Tiff,StudioExportFormat.Png,StudioExportFormat.Jpeg}){var output=Path.Combine(root,"result-"+format+StudioQuickExport.Extension(input,format));await ProfessionalRawExport.WriteAsync(master,definition,output,format);Assert.IsTrue(File.Exists(output));}
   var held=StudioGlobalResources.AccountedBytes;
   using(var session=Kitao.InstantFilm.Research.InstantV3ProductPipeline.Open(master,definition))
   {Assert.IsTrue(StudioGlobalResources.AccountedBytes>=held+17L*13*12);}
   Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);
   await vm.UndoInstantV3Command.ExecuteAsync(null);var beforeMulti=vm.ActiveTarget!.ActiveEditVersionId;var beforeDefinition=vm.ActiveTarget.RawDefinition;
   var beforePixels=vm.Editor.MatchedImage;File.Move(second,second+".held");
   await vm.RedoInstantV3Command.ExecuteAsync(null);Assert.AreEqual(beforeMulti,vm.ActiveTarget.ActiveEditVersionId);Assert.AreEqual(beforeDefinition,vm.ActiveTarget.RawDefinition);Assert.AreSame(beforePixels,vm.Editor.MatchedImage);

   await Assert.ThrowsAsync<FileNotFoundException>(async()=>await ProfessionalRawExport.WriteAsync(master,definition,Path.Combine(root,"missing.tif"),StudioExportFormat.Tiff));Assert.IsFalse(File.Exists(Path.Combine(root,"missing.tif")));File.Move(second+".held",second);
   await vm.RedoInstantV3Command.ExecuteAsync(null);Assert.IsNotNull(vm.ActiveTarget.RawDefinition!.InstantFilm!.V3!.MultipleInput);Assert.AreEqual(.35,vm.InstantSecondWeight);
   Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);
   await File.AppendAllTextAsync(second,"changed");await Assert.ThrowsAsync<InvalidDataException>(async()=>await ProfessionalRawExport.WriteAsync(master,definition,Path.Combine(root,"changed.tif"),StudioExportFormat.Tiff));Assert.IsFalse(File.Exists(Path.Combine(root,"changed.tif")));
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
    private sealed class Dialogs(string root,string? second=null):IDialogService
    {
        public string? ChooseFolder(string title,string? initialDirectory=null)=>root;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>second is null?[]:[second];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
    }
}

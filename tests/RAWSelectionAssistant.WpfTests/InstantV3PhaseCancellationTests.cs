using System.IO;using System.Security.Cryptography;using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Models;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Core.Services.RawToJpeg;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class InstantV3PhaseCancellationTests
{
 [TestMethod]public async Task CancelAtActualPixelEncodeAndValidationKeepsNoPartialAndOldDefinitionExportable()
 {
  var root=Path.Combine(Path.GetTempPath(),"V3Phases-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var source=Path.Combine(root,"x.arw");await File.WriteAllBytesAsync(source,[1,2,3]);var definition=new ProfessionalRawDefinition(){InstantFilm=new(true,AlgorithmVersion:3){V3=CandidateRecipe.Create("modern-itype-v3") with{Creative=new(Version:2)}}};var master=await new FakeLinear().DecodeAsync(source,definition);
   foreach(var phase in new[]{"PixelRender","EncodeStrip","OutputValidation"})
   {
    using var cancellation=new CancellationTokenSource();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var allow=new ManualResetEventSlim();
    var path=Path.Combine(root,phase+".png");using var capture=CandidateProcessingEvidence.Capture(p=>{if(p==phase&&!entered.Task.IsCompleted){entered.SetResult();allow.Wait(TimeSpan.FromSeconds(10));}});
    var task=Task.Run(()=>ProfessionalRawExport.WriteAsync(master,definition,path,StudioExportFormat.Png,cancellation.Token));await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.IsFalse(task.IsCompleted);cancellation.Cancel();allow.Set();await Assert.ThrowsAsync<OperationCanceledException>(async()=>await task);Assert.IsFalse(File.Exists(path));Assert.IsFalse(Directory.EnumerateFiles(root,".v3-*").Any());
   }
   await ProfessionalRawExport.WriteAsync(master,definition,Path.Combine(root,"still-editable.tif"),StudioExportFormat.Tiff);Assert.IsTrue(File.Exists(Path.Combine(root,"still-editable.tif")));
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

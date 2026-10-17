using System.IO;using System.Security.Cryptography;using System.Windows;using System.Windows.Controls;using System.Windows.Data;
using RAWSelectionAssistant.Core.Models;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Core.Services.RawToJpeg;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Services;using RAWSelectionAssistant.ViewModels;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize] public sealed class InstantV3CorrectionBindingTests
{
 [TestMethod] public Task RealSliderAndSeedBindingResetOnNewPhotoAndShapesDoNotCouple()=>RunSta(async()=>
 {
  var root=Path.Combine(Path.GetTempPath(),"V3Binding-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var a=Path.Combine(root,"a.arw");var b=Path.Combine(root,"b.arw");await File.WriteAllBytesAsync(a,[1,2,3]);await File.WriteAllBytesAsync(b,[4,5,6]);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:new FakeLinear());await vm.LoadTargetAsync(a);await vm.CreateProfessionalRawCommand.ExecuteAsync(null);vm.InstantEngineVersion="V3";vm.InstantCameraAnimation=false;
   var slider=new Slider{Minimum=0,Maximum=1,DataContext=vm};slider.SetBinding(Slider.ValueProperty,new Binding("InstantV3DefectAmount"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});
   slider.SetCurrentValue(Slider.ValueProperty,.6);slider.GetBindingExpression(Slider.ValueProperty)!.UpdateSource();Assert.AreEqual(.6,vm.InstantV3DefectAmount);Assert.AreEqual(0,vm.InstantV3EmulsionDefects);
   vm.InstantV3DefectKind="None";await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var recipe=vm.ActiveTarget!.RawDefinition!.InstantFilm!.V3!;Assert.AreEqual(2,recipe.Creative!.Version);Assert.AreEqual(0,recipe.Emulsion.DevelopmentDefects);Assert.AreEqual(.6,recipe.Defects!.Amount);
   vm.InstantV3Seed=123;var text=new TextBox{DataContext=vm};text.SetBinding(TextBox.TextProperty,new Binding("InstantV3Seed"){Mode=BindingMode.TwoWay});Assert.AreEqual("123",text.Text);
   await vm.RollInstantFilmCommand.ExecuteAsync(null);Assert.AreEqual(vm.InstantV3Seed,vm.ActiveTarget.RawDefinition.InstantFilm!.V3!.Random.RootSeed);Assert.AreEqual(vm.InstantV3Seed.ToString(),text.Text);
   await vm.LoadTargetAsync(b);Assert.AreEqual(0,vm.InstantV3DefectAmount);Assert.AreEqual(0,slider.Value);Assert.AreEqual("Base",vm.InstantV3Stock);Assert.AreEqual("None",vm.InstantV3DefectKind);Assert.AreEqual(20261017UL,vm.InstantV3Seed);
  }
  finally{Directory.Delete(root,true);}
 });
 [TestMethod] public Task BoundPageHistoryIncludesV3DisableAndFailureDoesNotDiscardRedo()=>RunSta(async()=>
 {
  EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"V3HistoryClosure-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var input=Path.Combine(root,"first.arw");await File.WriteAllBytesAsync(input,[1,2,3]);
   using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root),rawDecoder:new FakeLegacy(),professionalRawDecoder:new FakeLinear());
   await vm.LoadTargetAsync(input);await vm.CreateProfessionalRawCommand.ExecuteAsync(null);vm.InstantEngineVersion="V3";vm.InstantCameraAnimation=false;
   await vm.GenerateInstantFilmCommand.ExecuteAsync(null);var target=vm.ActiveTarget!;var enabled=target.ActiveEditVersionId;
   var view=new RAWSelectionAssistant.Views.PhotoGradePageView{DataContext=vm};view.Measure(new Size(1400,850));view.Arrange(new Rect(0,0,1400,850));view.UpdateLayout();
   Assert.AreSame(vm.PhotoGradeUndoCommand,((Button)view.FindName("PhotoUndo")).Command);
   await vm.DisableInstantFilmCommand.ExecuteAsync(null);var disabled=target.ActiveEditVersionId;
   Assert.AreNotEqual(enabled,disabled);Assert.AreEqual(0,target.RawDefinition!.InstantFilm!.V3!.Amount);Assert.IsTrue(vm.InstantImageEffectsDisabled);
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);Assert.AreEqual(enabled,target.ActiveEditVersionId);Assert.IsFalse(vm.InstantImageEffectsDisabled);
   await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(disabled,target.ActiveEditVersionId);Assert.IsTrue(vm.InstantImageEffectsDisabled);
   await vm.PhotoGradeUndoCommand.ExecuteAsync(null);var versions=target.EditVersions.Select(x=>x.Id).ToArray();
   vm.InstantV3DefectScale=double.NaN;await vm.DisableInstantFilmCommand.ExecuteAsync(null);
   Assert.AreEqual(enabled,target.ActiveEditVersionId);CollectionAssert.AreEqual(versions,target.EditVersions.Select(x=>x.Id).ToArray());Assert.IsFalse(vm.InstantImageEffectsDisabled);
   await vm.PhotoGradeRedoCommand.ExecuteAsync(null);Assert.AreEqual(disabled,target.ActiveEditVersionId);
  }
  finally{Directory.Delete(root,true);}
 });
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

using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class RawPostImportMemoryTests
{
 [TestMethod]public Task CollapsedLegacyPageDoesNotPromotePhotoPageProxyButVisibleActualSizeStillWorks()=>RunSta(async()=>
 {
  EnsureTestApplication();var path=Path.Combine(Path.GetTempPath(),"no-raw-memory-"+Guid.NewGuid().ToString("N")+".raf");await File.WriteAllTextAsync(path,"Owned synthetic header; no native RAW decoder is used.");
  using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(path),rawDecoder:new SyntheticDecoder());
  var view=new ReferenceColorWorkspaceView{Visibility=Visibility.Collapsed,DataContext=workspace};
  Window? window=null;
  try
  {
   await workspace.ChooseTargetCommand.ExecuteAsync(null);await Task.Delay(250);await workspace.Editor.DetailPreviewWork;
   Assert.IsFalse(workspace.Editor.IsDetailPreviewRequested,"The inactive legacy page must not use its default zoom to request a full RAW display.");
   Assert.AreEqual(1600,workspace.Editor.SourceImage!.PixelWidth);
   view.Visibility=Visibility.Visible;window=new Window{Content=view,Width=1500,Height=900,Left=-4000,Top=0,ShowInTaskbar=false};window.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);window.UpdateLayout();
   var viewport=(ColorStudioImageViewport)view.FindName("ImageViewport");viewport.State.SetZoom(1);await workspace.Editor.DetailPreviewWork;
   Assert.IsTrue(workspace.Editor.IsDetailPreviewRequested,"A visible legacy viewport still owns its explicit actual-size request.");Assert.AreEqual(2400,workspace.Editor.SourceImage!.PixelWidth);
  }
  finally{window?.Close();view.DataContext=null;}
 });
 [TestMethod]public Task VisiblePhotoPageOwnsActualSizeRequestsWithoutHiddenLegacyHelp()=>RunSta(async()=>
 {
  EnsureTestApplication();var path=Path.Combine(Path.GetTempPath(),"no-raw-photo-owner-"+Guid.NewGuid().ToString("N")+".raf");await File.WriteAllTextAsync(path,"Synthetic header, cache-like decoder only.");
  using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs(path),rawDecoder:new SyntheticDecoder());var view=new PhotoGradePageView{DataContext=workspace};var window=new Window{Content=view,Width=1500,Height=900,Left=-4000,Top=0,ShowInTaskbar=false};
  try
  {
   window.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);await workspace.ChooseTargetCommand.ExecuteAsync(null);await Task.Delay(250);await workspace.Editor.DetailPreviewWork;
   Assert.IsFalse(workspace.Editor.IsDetailPreviewRequested,"Fit must remain a bounded proxy.");Assert.AreEqual(1600,workspace.Editor.SourceImage!.PixelWidth);
   var viewport=(ColorStudioImageViewport)view.FindName("PhotoViewport");viewport.State.SetZoom(1);await workspace.Editor.DetailPreviewWork;
   Assert.IsTrue(workspace.Editor.IsDetailPreviewRequested,"The visible photo page must request its own full-resolution pixels.");Assert.AreEqual(2400,workspace.Editor.SourceImage!.PixelWidth);
  }
  finally{view.DataContext=null;window.Close();}
 });
 [TestMethod]public Task DisplayAdapterAvoidsManagedFrameDuplicateAndOwnsItsBitmapPixels()=>RunSta(()=>
 {
  RawDisplayBitmapAdapter.ToBitmap(new VisualPixelBuffer(1,1,new byte[3]));
  const int width=512,height=512;var pixels=Enumerable.Range(0,width*height*3).Select(i=>(byte)(i%251)).ToArray();var expected=(byte[])pixels.Clone();var buffer=new VisualPixelBuffer(width,height,pixels);
  long before=GC.GetAllocatedBytesForCurrentThread();var bitmap=RawDisplayBitmapAdapter.ToBitmap(buffer);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
  Assert.IsLessThan(pixels.Length/4L,allocated,"Creating the WIC bitmap must not duplicate the entire owned RGB24 frame in managed memory.");
  Array.Fill(pixels,(byte)0);var actual=new byte[expected.Length];bitmap.CopyPixels(actual,width*3,0);CollectionAssert.AreEqual(expected,actual,"The immutable bitmap must own its copied native pixels after the source array is reused.");return Task.CompletedTask;
 });
 [TestMethod]public Task DisplayAdapterHonorsOffsetSlices()=>RunSta(()=>
 {
  var storage=new byte[]{201,202,203,1,2,3,4,5,6,7,8,9,10,11,12,204};var image=RawDisplayBitmapAdapter.ToBitmap(new VisualPixelBuffer(2,2,storage.AsMemory(3,12)));var actual=new byte[12];image.CopyPixels(actual,6,0);CollectionAssert.AreEqual(Enumerable.Range(1,12).Select(v=>(byte)v).ToArray(),actual);return Task.CompletedTask;
 });
 [TestMethod]public Task DisposedEditorReleasesItsFullDisplayWhileEditorRemainsReachable()=>RunSta(async()=>
 {
  var full=new HighBitDepthImageBuffer(1800,2,Enumerable.Repeat(.4f,10800).ToArray(),"16");var master=new FrozenRawMaster("owned-synthetic.raf","synthetic",Guid.NewGuid(),full);var pipeline=new RawMatchTiff16ProductPipeline(new SyntheticDecoder());var proxy=pipeline.PreviewMaster(full,1600);
  var editor=new TetherReferenceModeViewModel(allowReferenceManagement:true);await editor.SetSourceAsync(null,RawDisplayBitmapAdapter.ToBitmap(proxy.ToVisualRgb24()),rawPreviewMaster:proxy,frozenRawMaster:master,render:false);await editor.SetPreviewZoomAsync(1);
  var display=new WeakReference(editor.SourceImage!);editor.Dispose();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();GC.Collect(2,GCCollectionMode.Forced,true,true);
  Assert.IsFalse(display.IsAlive,"A disposed editor still retained its full-resolution WIC bitmap.");GC.KeepAlive(editor);full.Dispose();proxy.Dispose();
 });
 sealed class SyntheticDecoder:IRawDecoder
 {
  public RawDecoderCapability GetCapability()=>new(true,"Synthetic_NoRaw","1",[],[]);
  public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>Task.FromResult(RawDecodedImage.FromRgb48(2400,1200,2400*6,new ushort[2400*1200*3],new("Synthetic","No RAW",null,1,"sRGB")));
 }
 sealed class Dialogs(string source):IDialogService
 {
  public string? ChooseFolder(string title,string? initialDirectory=null)=>null;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[source];public string? ChooseSaveFile(string title,string filter,string extension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string value){}public void ShowError(string value)=>throw new InvalidDataException(value);public bool Confirm(string text,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> choices)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
 }
}

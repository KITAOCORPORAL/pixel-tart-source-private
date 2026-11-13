using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class RasterPreviewNativeCacheTests
{
 [TestMethod]public Task NativePreviewCachePreservesPrecisionAlphaOrientationAndDpi()=>RunSta(()=>
 {
  foreach(var bits in new[]{32,48})
  foreach(var orientation in new ushort[]{1,6})
  {
   var path=Fixture(31,17,bits,orientation);
   try
   {
    var previous=OnLoadBaseline(path,orientation);var preview=StudioQuickExport.LoadPreviewSource(path);
    Assert.AreEqual(previous.PixelWidth,preview.PixelWidth);Assert.AreEqual(previous.PixelHeight,preview.PixelHeight);
    Assert.AreEqual(previous.Format,preview.Format);Assert.AreEqual(previous.DpiX,preview.DpiX);Assert.AreEqual(previous.DpiY,preview.DpiY);
    CollectionAssert.AreEqual(Pixels(previous),Pixels(preview),"Native detachment must retain exactly the existing normalized samples.");
   }
   finally{File.Delete(path);}
  }
  return Task.CompletedTask;
 });
 [TestMethod]public Task NativePreviewCacheIsFrozenAndIndependentOfDecoderThreadAndFile()=>RunSta(async()=>
 {
  var path=Fixture(29,11,48,1);
  try
  {
   var preview=StudioQuickExport.LoadPreviewSource(path);var expected=Pixels(preview);Assert.IsTrue(preview.IsFrozen);
   using var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
   var actual=await Task.Run(()=>{var converted=new FormatConvertedBitmap(preview,PixelFormats.Rgb48,null,0);converted.Freeze();return Pixels(converted);});
   CollectionAssert.AreEqual(expected,actual,"A preview worker must not walk or read the closed decoder.");
  }
  finally{File.Delete(path);}
 });
 [TestMethod]public Task NativePreviewCacheAvoidsFullManagedStagingArray()=>RunSta(()=>
 {
  var path=Fixture(512,512,48,1);
  try
  {
   StudioQuickExport.Load(path);StudioQuickExport.LoadPreviewSource(path);
   var start=GC.GetAllocatedBytesForCurrentThread();var old=StudioQuickExport.Load(path);var previousBytes=GC.GetAllocatedBytesForCurrentThread()-start;
   start=GC.GetAllocatedBytesForCurrentThread();var preview=StudioQuickExport.LoadPreviewSource(path);var nativeBytes=GC.GetAllocatedBytesForCurrentThread()-start;
   Assert.IsGreaterThanOrEqualTo(512L*512*6,previousBytes);
   Assert.IsLessThan(previousBytes/2,nativeBytes,"Preview detachment reintroduced a full managed raster staging buffer.");
   CollectionAssert.AreEqual(Pixels(old),Pixels(preview));
  }
  finally{File.Delete(path);}
  return Task.CompletedTask;
 });
 [TestMethod]public Task FrozenExportUsesTheValidatedSourceInsteadOfActiveEditor()=>RunSta(async()=>
 {
  var path=Fixture(19,13,48,1);
  try
  {
   using var editor=new RAWSelectionAssistant.ViewModels.TetherReferenceModeViewModel(allowReferenceManagement:true);
   var unrelated=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{1,2,3,255},4);unrelated.Freeze();
   await editor.SetSourceAsync(null,unrelated,render:false);
   var snapshot=StudioQuickExport.LoadPreviewSource(path);
   var expected=await editor.ProcessForExportAsync(path,null,null,CancellationToken.None);
   var actual=await editor.ProcessForExportAsync(path,null,null,CancellationToken.None,frozenSource:snapshot);
   Assert.AreEqual(expected.PixelWidth,actual.PixelWidth);Assert.AreEqual(expected.PixelHeight,actual.PixelHeight);Assert.AreEqual(expected.Format,actual.Format);
   CollectionAssert.AreEqual(Pixels(expected),Pixels(actual));
   var mutable=new WriteableBitmap(1,1,96,96,PixelFormats.Bgra32,null);
   try{await editor.ProcessForExportAsync(path,null,null,CancellationToken.None,frozenSource:mutable);Assert.Fail("Mutable source snapshot was accepted");}catch(InvalidOperationException){}
  }
  finally{File.Delete(path);}
 });
 [TestMethod]public Task SourceCacheMatchesContentIdentityAndRejectsChangedBytesWithSameStamp()=>RunSta(async()=>
 {
  var path=Fixture(17,9,48,1);var replacement=Fixture(17,9,32,1);var root=Path.Combine(Path.GetTempPath(),"kitao-cache-export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try
  {
   using var workspace=new RAWSelectionAssistant.ViewModels.ReferenceColorWorkspaceViewModel(new ExportDialogs(path,root));
   await workspace.ChooseTargetCommand.ExecuteAsync(null);await workspace.AnalysisWork;
   var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
   var resolver=typeof(RAWSelectionAssistant.ViewModels.ReferenceColorWorkspaceViewModel).GetMethod("FrozenRasterExportSource",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
   Assert.IsNotNull(resolver.Invoke(workspace,[path,hash]));Assert.IsNull(resolver.Invoke(workspace,[path,"wrong-content-hash"]));
   var stamp=File.GetLastWriteTimeUtc(path);File.Copy(replacement,path,true);File.SetLastWriteTimeUtc(path,stamp);
   await workspace.ExportSelectedCommand.ExecuteAsync(null);
   Assert.AreEqual(RAWSelectionAssistant.ViewModels.ReferenceExportStatus.Failed,workspace.Targets.Single().ExportStatus);
   Assert.IsFalse(Directory.EnumerateFiles(root,"*.tif").Any(),"A stale cached frame must never become a successful export.");
  }
  finally
  {
   File.Delete(path);File.Delete(replacement);
   var parent=Directory.GetParent(Path.GetFullPath(root))!.FullName;var expected=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
   if(!string.Equals(parent,expected,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("kitao-cache-export-"))throw new IOException("Unexpected cleanup root");
   Directory.Delete(root,true);
  }
 });
 sealed class ExportDialogs(string source,string output):RAWSelectionAssistant.Services.IDialogService
 {
  public string? ChooseFolder(string title,string? initialDirectory=null)=>output;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[source];
  public string? ChooseSaveFile(string title,string filter,string extension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;
  public void ShowInfo(string text){}public void ShowError(string text)=>throw new IOException(text);public bool Confirm(string text,string title)=>true;
  public RAWSelectionAssistant.Services.HelpAction ShowHelp()=>RAWSelectionAssistant.Services.HelpAction.None;public void ShowFeedback(){}
  public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates)=>null;
  public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
 }
 static BitmapSource OnLoadBaseline(string path,ushort orientation)
 {
  using var file=File.OpenRead(path);var source=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
  var normalized=StudioQuickExport.ApplyOrientation(source,orientation);var bytes=Pixels(normalized);var stride=(normalized.PixelWidth*normalized.Format.BitsPerPixel+7)/8;
  var bitmap=BitmapSource.Create(normalized.PixelWidth,normalized.PixelHeight,normalized.DpiX,normalized.DpiY,normalized.Format,normalized.Palette,bytes,stride);bitmap.Freeze();return bitmap;
 }
 static byte[] Pixels(BitmapSource bitmap){var stride=(bitmap.PixelWidth*bitmap.Format.BitsPerPixel+7)/8;var result=new byte[stride*bitmap.PixelHeight];bitmap.CopyPixels(result,stride,0);return result;}
 static string Fixture(int width,int height,int bits,ushort orientation)
 {
  var path=Path.Combine(Path.GetTempPath(),"kitao-native-preview-"+Guid.NewGuid().ToString("N")+".tif");
  BitmapSource bitmap;
  if(bits==48){var data=Enumerable.Range(0,width*height*3).Select(i=>(ushort)((i*319)%65536)).ToArray();bitmap=BitmapSource.Create(width,height,123,117,PixelFormats.Rgb48,null,data,width*6);}
  else{var data=Enumerable.Range(0,width*height*4).Select(i=>(byte)((i*17)%251)).ToArray();bitmap=BitmapSource.Create(width,height,123,117,PixelFormats.Bgra32,null,data,width*4);}
  var metadata=new BitmapMetadata("tiff");metadata.SetQuery("/ifd/{ushort=274}",orientation);
  var encoder=new TiffBitmapEncoder{Compression=TiffCompressOption.None};encoder.Frames.Add(BitmapFrame.Create(bitmap,null,metadata,null));using var file=File.Create(path);encoder.Save(file);return path;
 }
}

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
    var previous=StudioQuickExport.Load(path);var preview=StudioQuickExport.LoadPreviewSource(path);
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

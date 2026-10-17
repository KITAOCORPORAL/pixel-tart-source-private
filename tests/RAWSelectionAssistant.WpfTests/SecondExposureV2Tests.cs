using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class SecondExposureV2Tests
{
 [DataTestMethod][DataRow("png")][DataRow("tif")]
 public void TransparentPixelsCompositeWhiteAndLegacyVersionRemainsUnchanged(string extension)
 {
  var root=Path.Combine(Path.GetTempPath(),"SecondV2-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Path.Combine(root,"alpha."+extension);byte[] pixels=[0,0,255,0,255,0,0,128,0,255,0,255,40,70,90,64];
   var source=BitmapSource.Create(4,1,96,96,PixelFormats.Bgra32,null,pixels,16);
   StudioQuickExport.Encode(source,path,path,extension=="png"?StudioExportFormat.Png:StudioExportFormat.Tiff);
   var input=SecondExposureDecoder.Describe(path);Assert.AreEqual(2,input.Version);var held=StudioGlobalResources.AccountedBytes;
   using var image=new SecondExposureDecoder().Read(input,CancellationToken.None);
   Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);Assert.AreEqual(1f,image.Rgb32.Span[0]);Assert.AreEqual(1f,image.Rgb32.Span[1]);Assert.AreEqual(1f,image.Rgb32.Span[2]);
   for(int x=0;x<4;x++)for(int c=0;c<3;c++){double a=pixels[x*4+3]/255d,expected=pixels[x*4+2-c]/255d*a+1-a;Assert.AreEqual(expected,image.Rgb32.Span[x*3+c],2d/65535);}
   using var legacy=new SecondExposureDecoder().Read(input with{Version=1},CancellationToken.None);Assert.AreNotEqual(image.Rgb32.Span[2],legacy.Rgb32.Span[2]);
  }
  finally{Directory.Delete(root,true);}
 }
 [DataTestMethod][DataRow((ushort)6)][DataRow((uint)6)]
 public void OrientationNormalizesShortAndLongAndPreservesEmbeddedSrgb(object orientation)
 {
  var root=Path.Combine(Path.GetTempPath(),"SecondOrientation-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Path.Combine(root,"oriented.tif");var source=BitmapSource.Create(2,3,96,96,PixelFormats.Rgb24,null,Enumerable.Range(0,18).Select(i=>(byte)(i*13)).ToArray(),6);
   var metadata=new BitmapMetadata("tiff");metadata.SetQuery("/ifd/{ushort=274}",orientation);var encoder=new TiffBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source,null,metadata,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>([new ColorContext(PixelFormats.Bgra32)])));using(var file=File.Create(path))encoder.Save(file);
   var input=SecondExposureDecoder.Describe(path);Assert.AreEqual(3,input.Width);Assert.AreEqual(2,input.Height);using var actual=new SecondExposureDecoder().Read(input,CancellationToken.None);Assert.AreEqual(3,actual.Width);Assert.AreEqual(1,actual.Orientation);
   var reference=StudioQuickExport.Load(path);var rgb=new FormatConvertedBitmap(reference,PixelFormats.Rgb48,null,0);var values=new ushort[18];rgb.CopyPixels(values,18,0);for(int i=0;i<18;i++)Assert.AreEqual(values[i]/65535d,actual.Rgb32.Span[i],2d/65535);
  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod]public void UnsupportedSyntheticIccRejectsInsteadOfSilentlyRelabeling()
 {
  var root=Path.Combine(Path.GetTempPath(),"SecondIcc-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var profile=Path.Combine(TestResources.Root,"tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc");
   var path=Path.Combine(root,"linear.tif");var bitmap=BitmapSource.Create(2,1,96,96,PixelFormats.Rgb48,null,new ushort[]{11796,11796,11796,32768,32768,32768},12);
   var encoder=new TiffBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap,null,null,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>([new ColorContext(new Uri(profile))])));using(var file=File.Create(path))encoder.Save(file);
   var held=StudioGlobalResources.AccountedBytes;
   // This controlled matrix profile is a research-only synthetic subset, not a WIC-admitted real profile.
   Assert.Throws<NotSupportedException>(()=>new SecondExposureDecoder().Read(SecondExposureDecoder.Describe(path),CancellationToken.None));
   Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);

  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod]public void IdentityDamageMissingAndPhaseCancellationReleaseWorkspace()
 {
  var root=Path.Combine(Path.GetTempPath(),"SecondFailure-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var path=Path.Combine(root,"a.png");StudioQuickExport.Encode(BitmapSource.Create(31,19,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat((byte)123,31*19*3).ToArray(),31*3),path,path,StudioExportFormat.Png);
   var input=SecondExposureDecoder.Describe(path);var held=StudioGlobalResources.AccountedBytes;
   foreach(var phase in new[]{"SecondHashBefore","SecondDecode","SecondCopyRow","SecondHashAfter"})
   {
    using var cancel=new CancellationTokenSource();bool entered=false;using var capture=CandidateProcessingEvidence.Capture(p=>{if(p==phase){entered=true;cancel.Cancel();}});
    Assert.Throws<OperationCanceledException>(()=>new SecondExposureDecoder().Read(input,cancel.Token));Assert.IsTrue(entered);Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);
   }
   File.AppendAllText(path,"changed");Assert.Throws<InvalidDataException>(()=>new SecondExposureDecoder().Read(input,CancellationToken.None));File.Delete(path);Assert.Throws<FileNotFoundException>(()=>new SecondExposureDecoder().Read(input,CancellationToken.None));
   File.WriteAllText(path,"bad file");Assert.Throws<System.NotSupportedException>(()=>SecondExposureDecoder.Describe(path));Assert.AreEqual(held,StudioGlobalResources.AccountedBytes);
  }
  finally{Directory.Delete(root,true);}
 }
}

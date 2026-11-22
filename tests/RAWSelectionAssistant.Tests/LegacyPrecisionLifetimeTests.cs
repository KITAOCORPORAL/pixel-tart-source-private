using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class LegacyPrecisionLifetimeTests
{
 public TestContext TestContext{get;set;}=null!;
 static RawDecodedImage Decoded(ushort orientation,int width=43,int height=37)
 {var data=new ushort[width*height*3];for(int i=0;i<data.Length;i++)data[i]=(ushort)((i*1777+127)%65536);return RawDecodedImage.FromRgb48(width,height,width*6,data,new RawImageMetadata("Frozen","ZeroRaw",null,orientation,"sRGB"));}
 [DataTestMethod][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)][DataRow(5)][DataRow(6)][DataRow(7)][DataRow(8)]
 public void FusedDecodedOrientationMatchesEveryFloatAndMetadata(int direction)
 {
  var decoded=Decoded((ushort)direction);var before=decoded.Rgb48Pixels!.ToArray();using var oldSource=HighBitDepthImageBuffer.FromRaw(decoded);using var expected=RawImageOrientation.NormalizePixels(oldSource);using var actual=RawImageOrientation.FromDecodedRgb48(decoded);
  Assert.AreEqual(expected.Width,actual.Width);Assert.AreEqual(expected.Height,actual.Height);Assert.AreEqual(expected.Orientation,actual.Orientation);Assert.AreEqual(expected.Metadata,actual.Metadata);Assert.AreEqual(expected.SourceBitDepth,actual.SourceBitDepth);Assert.AreEqual(expected.WorkingColorSpace,actual.WorkingColorSpace);CollectionAssert.AreEqual(expected.Rgb32.ToArray(),actual.Rgb32.ToArray());CollectionAssert.AreEqual(before,decoded.Rgb48Pixels);
 }
 [TestMethod]public void FusedOrientationAllocatesOneFloatFrame()
 {
  var decoded=Decoded(8,1024,256);using(var warm=RawImageOrientation.FromDecodedRgb48(decoded)){}
  var start=GC.GetAllocatedBytesForCurrentThread();using(var old=HighBitDepthImageBuffer.FromRaw(decoded))using(var rotated=RawImageOrientation.NormalizePixels(old)){}var oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  start=GC.GetAllocatedBytesForCurrentThread();using(var current=RawImageOrientation.FromDecodedRgb48(decoded)){}var newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  TestContext.WriteLine($"Orientation allocation original={oldBytes}; fused={newBytes}; mandatory float bytes={decoded.Width*decoded.Height*12}");Assert.IsTrue(newBytes<oldBytes*.55);
 }
 [DataTestMethod][DataRow(ColorStudioNodeType.Preset)][DataRow(ColorStudioNodeType.WhiteBalance)][DataRow(ColorStudioNodeType.BasicTone)][DataRow(ColorStudioNodeType.ColorBalance)][DataRow(ColorStudioNodeType.Levels)][DataRow(ColorStudioNodeType.Curve)][DataRow(ColorStudioNodeType.SkinTone)][DataRow(ColorStudioNodeType.Details)]
 public void DisplayOnlyMatchesOriginalFloatAdapterEveryByte(ColorStudioNodeType type)
 {
  using var source=HighBitDepthImageBuffer.FromRaw(Decoded(1));var before=source.Rgb32.ToArray();var node=new ColorAdjustmentStackNode(Guid.NewGuid(),type,type.ToString(),NumericParameters:type switch{ColorStudioNodeType.BasicTone=>new Dictionary<string,double>{{"exposure",.5},{"highlights",-21}},ColorStudioNodeType.WhiteBalance=>new Dictionary<string,double>{{"temperature",15}},ColorStudioNodeType.Curve=>new Dictionary<string,double>{{"rgb_y3",.66}},ColorStudioNodeType.Preset=>new Dictionary<string,double>{{"contrast",9}},ColorStudioNodeType.Details=>new Dictionary<string,double>{{"sharpen",8}},ColorStudioNodeType.ColorBalance=>new Dictionary<string,double>{{"master_hue",31},{"master_amount",8}},ColorStudioNodeType.Levels=>new Dictionary<string,double>{{"rgb_gamma",1.2}},_=>new Dictionary<string,double>{{"hue_uniformity",40}}});var stack=new ColorAdjustmentStack([node],ProcessingVersion:2);var pipe=new RawMatchTiff16ProductPipeline(new Reject());var original=pipe.Render(source,null,stack);using var owner=original.ProcessingPixels;var actual=pipe.RenderDisplay(source,null,stack);CollectionAssert.AreEqual(original.Pixels.Rgb24.ToArray(),actual.Rgb24.ToArray());CollectionAssert.AreEqual(before,source.Rgb32.ToArray());
 }
 [TestMethod]public void DisplayOnlyAllocationAndMixedStackProxyAreExact()
 {
  using var source=HighBitDepthImageBuffer.FromRaw(Decoded(1,1024,256));
  var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.WhiteBalance,"WB",NumericParameters:new Dictionary<string,double>{{"temperature",7}}),new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Tone",NumericParameters:new Dictionary<string,double>{{"exposure",.5}})]);
  var pipe=new RawMatchTiff16ProductPipeline(new Reject());var old=pipe.Render(source,null,stack,320);using var oldOwner=old.ProcessingPixels;CollectionAssert.AreEqual(old.Pixels.Rgb24.ToArray(),pipe.RenderDisplay(source,null,stack,320).Rgb24.ToArray());
  var start=GC.GetAllocatedBytesForCurrentThread();var full=pipe.Render(source,null,stack);using var owned=full.ProcessingPixels;var before=GC.GetAllocatedBytesForCurrentThread()-start;
  start=GC.GetAllocatedBytesForCurrentThread();var current=pipe.RenderDisplay(source,null,stack);var after=GC.GetAllocatedBytesForCurrentThread()-start;
  CollectionAssert.AreEqual(full.Pixels.Rgb24.ToArray(),current.Rgb24.ToArray());TestContext.WriteLine($"Display allocation original={before}; rows={after}; same RGB24 bytes");Assert.IsTrue(after<before/3);
 }
 [TestMethod]public void DisplayAdapterNeverRoundsViaRgb48AndBorrowedSourceSurvivesCancel()
 {
  var values=Enumerable.Range(0,300).Select(i=>(i+.5f)/255).ToArray();using var source=new HighBitDepthImageBuffer(100,1,values,"16","sRGB");var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"neutral")]);var pipe=new RawMatchTiff16ProductPipeline(new Reject());var actual=pipe.RenderDisplay(source,null,stack);CollectionAssert.AreEqual(source.ToVisualRgb24().Rgb24.ToArray(),actual.Rgb24.ToArray());bool cancelled=false;try{pipe.RenderDisplay(source,null,stack,token:new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Assert.IsTrue(cancelled);CollectionAssert.AreEqual(values,source.Rgb32.ToArray());
 }
 [TestMethod]public async Task UprightEntryKeepsDecoderOptionsAndInputOwnershipOnFailureAndCancel()
 {
  var input=Decoded(8);var original=input.Rgb48Pixels!.ToArray();var decoder=new Supplied(input);var pipe=new RawMatchTiff16ProductPipeline(decoder);var root=Path.Combine(Path.GetTempPath(),"upright-entry-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   var file=Path.Combine(root,"marker.raf");await File.WriteAllTextAsync(file,"zero RAW marker");var master=await pipe.DecodeUprightFrozenMasterAsync(file);using var owner=master.Image;using var old=HighBitDepthImageBuffer.FromRaw(input);using var expected=RawImageOrientation.NormalizePixels(old);CollectionAssert.AreEqual(expected.Rgb32.ToArray(),master.Image.Rgb32.ToArray());Assert.AreEqual(false,decoder.Options!.AutoRotate);Assert.AreEqual(RawDecodeMode.ProfessionalDecode,decoder.Options.DecodeMode);
   bool failure=false;try{await pipe.DecodeUprightFrozenMasterAsync(Path.Combine(root,"missing.raf"));}catch(FileNotFoundException){failure=true;}Assert.IsTrue(failure);
   bool cancelled=false;try{await pipe.DecodeUprightFrozenMasterAsync(file,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Assert.IsTrue(cancelled);CollectionAssert.AreEqual(original,input.Rgb48Pixels);Assert.IsGreaterThan(0,master.Image.Rgb32.Length);
  }
  finally{Directory.Delete(root,true);}
 }
 sealed class Supplied(RawDecodedImage image):IRawDecoder
 {public RawToJpegOptions? Options;public RawDecoderCapability GetCapability()=>new(true,"SuppliedRgb48","test",[".raf"],[".raf"]);public Task<RawDecodedImage> DecodeAsync(string p,RawToJpegOptions options,CancellationToken token=default){Options=options;return Task.FromResult(image);}}
 sealed class Reject:IRawDecoder
 {public RawDecoderCapability GetCapability()=>throw new InvalidOperationException("No RAW");public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new InvalidOperationException("No RAW");}
}

using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using System.Runtime.InteropServices;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class OwnedRgb48BackingTests
{
 public TestContext TestContext{get;set;}=null!;
 [DataTestMethod][DataRow(1)][DataRow(2)][DataRow(3)][DataRow(4)][DataRow(5)][DataRow(6)][DataRow(7)][DataRow(8)]
 public async Task OwnedRowsPreserveFloatSamplesMetadataAndDecoderArray(int orientation)
 {
  var values=Enumerable.Range(0,43*37*3).Select(i=>(ushort)((i*1777)%65536)).ToArray();var decoded=RawDecodedImage.FromRgb48(43,37,43*6,values,new("Frozen","OwnedRGB48",null,(ushort)orientation,"sRGB"));
  var marker=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".raf");await File.WriteAllTextAsync(marker,"no RAW");
  try
  {
   using var eager=HighBitDepthImageBuffer.FromRaw(decoded);using var expected=RawImageOrientation.NormalizePixels(eager);var frame=await new RawMatchTiff16ProductPipeline(new Supplied(decoded)).DecodeUprightFrozenMasterAsync(marker);using var source=frame.Image;
   Assert.IsFalse(source.IsFloatMaterialized);Assert.AreEqual((long)values.Length*2,source.StorageBytes);Assert.AreEqual(expected.Width,source.Width);Assert.AreEqual(expected.Metadata,source.Metadata);
   var row=new float[source.Width*3];for(int y=0;y<source.Height;y++){source.CopyRgb32(y*row.Length,row);CollectionAssert.AreEqual(expected.Rgb32.Slice(y*row.Length,row.Length).ToArray(),row);}
   CollectionAssert.AreEqual(expected.ToVisualRgb24().Rgb24.ToArray(),source.ToVisualRgb24().Rgb24.ToArray());CollectionAssert.AreEqual(expected.ToRgb48(),source.ToRgb48());Assert.IsFalse(source.IsFloatMaterialized);
   values[0]^=65535;CollectionAssert.AreEqual(expected.ToRgb48(),source.ToRgb48(),"Source must own a copy, not borrow decoder mutable memory");
   var legacy=source.Rgb32;CollectionAssert.AreEqual(expected.Rgb32.ToArray(),legacy.ToArray());Assert.IsTrue(source.IsFloatMaterialized);Assert.AreEqual((long)values.Length*4,source.StorageBytes);
  }
  finally{File.Delete(marker);}
 }
 [TestMethod]public async Task AllUshortValuesAndConcurrentCompatibilityExpansionAreExact()
 {
  var words=Enumerable.Range(0,65536*3).Select(i=>(ushort)(i%65536)).ToArray();var image=RawDecodedImage.FromRgb48(256,256,256*6,words,new("Frozen","AllU16",null,1,"sRGB"));var marker=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".raf");await File.WriteAllTextAsync(marker,"no RAW");
  try
  {
   var frozen=await new RawMatchTiff16ProductPipeline(new Supplied(image)).DecodeUprightFrozenMasterAsync(marker);using var source=frozen.Image;using var eager=HighBitDepthImageBuffer.FromRaw(image);CollectionAssert.AreEqual(eager.ToVisualRgb24().Rgb24.ToArray(),source.ToVisualRgb24().Rgb24.ToArray());Assert.IsFalse(source.IsFloatMaterialized);
   var expanded=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Task.Run(()=>source.Rgb32)));foreach(var memory in expanded){CollectionAssert.AreEqual(eager.Rgb32.ToArray(),memory.ToArray());Assert.IsTrue(MemoryMarshal.TryGetArray(memory,out var a));Assert.IsTrue(MemoryMarshal.TryGetArray(expanded[0],out var b));Assert.AreSame(a.Array,b.Array);}
  }
  finally{File.Delete(marker);}
 }
 [TestMethod]public async Task OwnedSourceSurvivesRowCancellationFailureAndOtherBorrower()
 {
  var words=Enumerable.Range(0,129*65*3).Select(i=>(ushort)(i*17%65536)).ToArray();var decoded=RawDecodedImage.FromRgb48(129,65,129*6,words,new("Frozen","Owned",null,8,"sRGB"));var marker=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".raf");await File.WriteAllTextAsync(marker,"no RAW");
  try
  {
   var frame=await new RawMatchTiff16ProductPipeline(new Supplied(decoded)).DecodeUprightFrozenMasterAsync(marker);using var source=frame.Image;var expected=source.ToRgb48();var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Tone",NumericParameters:new Dictionary<string,double>{{"exposure",.5}})]);var renderer=new ColorStudioRenderPipeline();using var cancel=new CancellationTokenSource();using var first=renderer.TryCreatePointExportRows(source,stack,cancel.Token);using var other=renderer.TryCreatePointExportRows(source,stack);Assert.IsNotNull(first);Assert.IsNotNull(other);var row=new byte[source.Width*6];first.FillRow(0,row);cancel.Cancel();bool cancelled=false;try{first.FillRow(20,row);}catch(OperationCanceledException){cancelled=true;}Assert.IsTrue(cancelled);first.Dispose();other.FillRow(20,row);CollectionAssert.AreEqual(expected,source.ToRgb48());Assert.IsFalse(source.IsFloatMaterialized);
   bool failed=false;try{renderer.TryCreatePointExportRows(source,new([new(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"bad",NumericParameters:new Dictionary<string,double>{{"exposure",double.NaN}})]));}catch(ArgumentException){failed=true;}Assert.IsTrue(failed);CollectionAssert.AreEqual(expected,source.ToRgb48());Assert.IsFalse(source.IsFloatMaterialized);
  }
  finally{File.Delete(marker);}
 }
 sealed class Supplied(RawDecodedImage decoded):IRawDecoder
 {public RawDecoderCapability GetCapability()=>new(true,"Supplied","test",[".raf"],[".raf"]);public Task<RawDecodedImage> DecodeAsync(string p,RawToJpegOptions o,CancellationToken token=default)=>Task.FromResult(decoded);}
}

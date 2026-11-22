using System.Buffers.Binary;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class LegacyHighPrecisionExportTests
{
 public TestContext TestContext {get;set;}=null!;
 static HighBitDepthImageBuffer Source(int width=43,int height=37,ushort orientation=8)
 {var p=new float[width*height*3];for(int i=0;i<p.Length;i++)p[i]=(i%127-3)/120f;return new(width,height,p,"16","sRGB",orientation);}
 static ColorAdjustmentStackNode Node(ColorStudioNodeType type)=>new(Guid.NewGuid(),type,type.ToString(),NumericParameters:type switch
 {
  ColorStudioNodeType.Preset=>new Dictionary<string,double>{{"exposure",.3},{"contrast",11},{"saturation",-9}},
  ColorStudioNodeType.WhiteBalance=>new Dictionary<string,double>{{"temperature",23},{"tint",-8}},
  ColorStudioNodeType.BasicTone=>new Dictionary<string,double>{{"exposure",.7},{"highlights",-33},{"shadows",12},{"contrast",4}},
  ColorStudioNodeType.ColorBalance=>new Dictionary<string,double>{{"master_hue",33},{"master_amount",14},{"highlights_amount",9}},
  ColorStudioNodeType.Levels=>new Dictionary<string,double>{{"rgb_gamma",1.17},{"r_black",.01},{"rgb_white",.91}},
  ColorStudioNodeType.Curve=>new Dictionary<string,double>{{"rgb_y3",.66},{"r_y1",.19}},
  _=>new Dictionary<string,double>{{"hue_uniformity",28},{"chroma_uniformity",33},{"lightness_uniformity",12}}
 });
 static byte[] Original(HighBitDepthImageBuffer source,ColorAdjustmentStack stack)
 {
  var pipeline=new RawMatchTiff16ProductPipeline(new RejectDecoder());using var output=pipeline.Render(source,null,stack).ProcessingPixels!;
  using var bytes=new MemoryStream();TiffExport.WriteRgb48(bytes,output,new(Orientation:source.Orientation));return bytes.ToArray();
 }
 static byte[] Rows(HighBitDepthImageBuffer source,ColorAdjustmentStack stack)
 {
  using var rows=new ColorStudioRenderPipeline().TryCreatePointExportRows(source,stack);Assert.IsNotNull(rows);
  using var bytes=new MemoryStream();TiffExport.WriteRgb48Rows(bytes,source.Width,source.Height,rows.FillRow,new(Orientation:source.Orientation));return bytes.ToArray();
 }
 [DataTestMethod][DataRow(ColorStudioNodeType.Preset)][DataRow(ColorStudioNodeType.WhiteBalance)][DataRow(ColorStudioNodeType.BasicTone)][DataRow(ColorStudioNodeType.ColorBalance)][DataRow(ColorStudioNodeType.Levels)][DataRow(ColorStudioNodeType.Curve)][DataRow(ColorStudioNodeType.SkinTone)]
 public void PointRowsMatchLegacyTiffEveryByte(ColorStudioNodeType type)
 {using var source=Source();var before=source.Rgb32.ToArray();var stack=new ColorAdjustmentStack([Node(type)],ProcessingVersion:2);CollectionAssert.AreEqual(Original(source,stack),Rows(source,stack));CollectionAssert.AreEqual(before,source.Rgb32.ToArray());}
 [TestMethod] public void OrderedMixedPointStackAndLastPartialStripMatchEveryByte()
 {using var source=Source();var stack=new ColorAdjustmentStack(new[]{Node(ColorStudioNodeType.WhiteBalance),Node(ColorStudioNodeType.BasicTone),Node(ColorStudioNodeType.Curve),Node(ColorStudioNodeType.Preset)},ProcessingVersion:1);CollectionAssert.AreEqual(Original(source,stack),Rows(source,stack));}
 [TestMethod] public void PointRowsUseOnlyScopedStripScratch()
 {
  using var source=Source(1024,256);var stack=new ColorAdjustmentStack([Node(ColorStudioNodeType.WhiteBalance),Node(ColorStudioNodeType.BasicTone)],ProcessingVersion:2);
  var pipe=new RawMatchTiff16ProductPipeline(new RejectDecoder());
  using(var warm=pipe.Render(source,null,stack).ProcessingPixels){}
  var before=GC.GetAllocatedBytesForCurrentThread();using(var old=pipe.Render(source,null,stack).ProcessingPixels){}var legacyAllocated=GC.GetAllocatedBytesForCurrentThread()-before;
  before=GC.GetAllocatedBytesForCurrentThread();
  using(var scope=GraphPixelAllocation.Enter())using(var rows=new ColorStudioRenderPipeline().TryCreatePointExportRows(source,stack))
  {Assert.IsNotNull(rows);var bytes=new byte[source.Width*6];for(int y=0;y<source.Height;y++)rows.FillRow(y,bytes);}
  var rowAllocated=GC.GetAllocatedBytesForCurrentThread()-before;
  TestContext.WriteLine($"Legacy allocated bytes={legacyAllocated}; scoped strip allocated bytes={rowAllocated}; width={source.Width}; height={source.Height}; no forced GC");
  Assert.IsTrue(rowAllocated<legacyAllocated/3,$"Strip allocation {rowAllocated} vs original {legacyAllocated}");
 }
 [TestMethod] public void NeutralBorrowersCannotDisposeSharedMaster()
 {
  using var source=Source();var before=source.Rgb32.ToArray();var stack=new ColorAdjustmentStack([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"neutral")]);
  var renderer=new ColorStudioRenderPipeline();using var first=renderer.RenderForExport(source,null,stack);using var second=renderer.RenderForExport(source,null,stack);
  Assert.IsTrue(first.IsBorrowed);Assert.AreSame(source,first.Pixels);first.Dispose();Assert.AreSame(source,second.Pixels);CollectionAssert.AreEqual(before,source.Rgb32.ToArray());
  using(var rows=renderer.TryCreatePointExportRows(source,stack)){Assert.IsNotNull(rows);Assert.IsTrue(rows.BorrowsSource);CollectionAssert.AreEqual(Original(source,stack),Rows(source,stack));}
  CollectionAssert.AreEqual(before,source.Rgb32.ToArray());
 }
 [TestMethod] public async Task MidWriteCancellationKeepsSourceAndRemovesTemporaryFile()
 {
  var root=Path.Combine(Path.GetTempPath(),"legacy-export-cancel-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   using var source=Source(257,65);var before=source.Rgb32.ToArray();using var cancellation=new CancellationTokenSource();using var rows=new ColorStudioRenderPipeline().TryCreatePointExportRows(source,new([Node(ColorStudioNodeType.BasicTone)]),cancellation.Token);Assert.IsNotNull(rows);
   bool cancelled=false;try{await AtomicTiffWriter.WriteRowsAsync(Path.Combine(root,"cancelled.tif"),source.Width,source.Height,(y,row)=>{rows.FillRow(y,row);if(y==18)cancellation.Cancel();},token:cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
   Assert.IsTrue(cancelled);Assert.AreEqual(0,Directory.GetFiles(root).Length);CollectionAssert.AreEqual(before,source.Rgb32.ToArray());rows.Dispose();CollectionAssert.AreEqual(before,source.Rgb32.ToArray());
  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod] public void LaterStripFailureReleasesOwnedWorkAndPreservesSource()
 {
  using var source=Source(129,40);var values=source.Rgb32.ToArray();values[129*3*32]=float.NaN;using var invalid=new HighBitDepthImageBuffer(129,40,values,"16","sRGB");using var rows=new ColorStudioRenderPipeline().TryCreatePointExportRows(invalid,new([Node(ColorStudioNodeType.BasicTone)]));Assert.IsNotNull(rows);
  var row=new byte[129*6];rows.FillRow(0,row);bool failed=false;try{rows.FillRow(32,row);}catch(ArgumentException){failed=true;}Assert.IsTrue(failed);rows.Dispose();Assert.IsTrue(float.IsNaN(invalid.Rgb32.Span[129*3*32]));Assert.AreEqual(values.Length,invalid.Rgb32.Length);
 }
 [TestMethod] public async Task ExportFailureAndPreCancellationDoNotReleaseSharedFrozenInput()
 {
  var root=Path.Combine(Path.GetTempPath(),"legacy-export-failure-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try
  {
   using var source=Source();var before=source.Rgb32.ToArray();var marker=Path.Combine(root,"marker.raf");await File.WriteAllTextAsync(marker,"not RAW");var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(marker)));var frozen=new FrozenRawMaster(marker,hash,Guid.NewGuid(),source);var pipe=new RawMatchTiff16ProductPipeline(new RejectDecoder());
   bool cancelled=false;try{await pipe.ExportAsync(frozen,Path.Combine(root,"never.tif"),null,null,new CancellationToken(true));}catch(OperationCanceledException){cancelled=true;}Assert.IsTrue(cancelled);
   var occupied=Path.Combine(root,"occupied.tif");Directory.CreateDirectory(occupied);bool failed=false;try{await pipe.ExportAsync(frozen,occupied,null,null);}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}Assert.IsTrue(failed);CollectionAssert.AreEqual(before,source.Rgb32.ToArray());Assert.AreEqual(1,Directory.GetFiles(root).Length);
  }
  finally{Directory.Delete(root,true);}
 }
 [TestMethod] public void SpatialAndLayerStacksKeepLegacyProcessingAndOwnedResult()
 {
  using var source=Source();var before=source.Rgb32.ToArray();var renderer=new ColorStudioRenderPipeline();
  foreach(var node in new[]{new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Details,"spatial",NumericParameters:new Dictionary<string,double>{{"sharpen",8}}),Node(ColorStudioNodeType.BasicTone) with{CompositeVersion=1,Opacity=.4}})
  {
   var stack=new ColorAdjustmentStack([node],ProcessingVersion:2);Assert.IsNull(renderer.TryCreatePointExportRows(source,stack));var expected=Original(source,stack);using(var actual=renderer.RenderForExport(source,null,stack)){Assert.IsFalse(actual.IsBorrowed);Assert.AreNotSame(source,actual.Pixels);using var stream=new MemoryStream();TiffExport.WriteRgb48(stream,actual.Pixels,new(Orientation:source.Orientation));CollectionAssert.AreEqual(expected,stream.ToArray());}CollectionAssert.AreEqual(before,source.Rgb32.ToArray());
  }
 }
 sealed class RejectDecoder:RAWSelectionAssistant.Core.Services.RawToJpeg.IRawDecoder
 {
  public RAWSelectionAssistant.Core.Models.RawDecoderCapability GetCapability()=>throw new InvalidOperationException("No RAW admission");
  public Task<RAWSelectionAssistant.Core.Models.RawDecodedImage> DecodeAsync(string path,RAWSelectionAssistant.Core.Models.RawToJpegOptions options,CancellationToken token=default)=>throw new InvalidOperationException("No RAW admission");
 }
}

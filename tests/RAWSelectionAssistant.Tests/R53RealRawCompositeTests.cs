using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
namespace RAWSelectionAssistant.Tests;
[TestClass] public sealed class R53RealRawCompositeTests
{
    [TestMethod] public async Task ExplicitNamedRawMaskBlendTiffReadbackAndSourceIntegrity()
    {
        var input=Environment.GetEnvironmentVariable("KITAO_R53_RAW_INPUT");var output=Environment.GetEnvironmentVariable("KITAO_R53_RAW_OUTPUT");
        if(string.IsNullOrWhiteSpace(input)||string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Existing authorized named RAW is opt-in; no private directory traversal.");return;}
        Directory.CreateDirectory(output);var watch=Stopwatch.StartNew();var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var master=await pipeline.DecodeFrozenMasterAsync(input);var decodeMs=watch.Elapsed.TotalMilliseconds;using var upright=RawImageOrientation.NormalizePixels(master.Image);
        var node=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"RAW local exposure",NumericParameters:new Dictionary<string,double>{{"exposure",.4}}){CompositeVersion=1,Opacity=.65,Blend=StudioLayerBlend.Screen,LocalMask=new([new([new(.2,.35),new(.45,.55)],.15,.5,.8,1)])};var stack=new ColorAdjustmentStack([node],ProcessingVersion:2);
        watch.Restart();using var full=BoundedLegacyRawRenderer.Render(upright,null,stack,null);var renderMs=watch.Elapsed.TotalMilliseconds;var file=Path.Combine(output,"named-raw-mask-derived.tif");watch.Restart();await AtomicTiffWriter.WriteRgb48Async(file,full,new(TiffBitDepth.Sixteen,Software:"Guangjing R53 diagnostic",Orientation:full.Orientation),overwrite:false);var encodeMs=watch.Elapsed.TotalMilliseconds;
        TiffReadBackResult back;using(var stream=File.OpenRead(file))back=TiffReadBack.Read(stream);CollectionAssert.AreEqual(full.ToRgb48(),back.Rgb48Samples.ToArray());await master.ValidateSourceAsync();
        var points=new List<object>();double max=0;foreach(var (x,y) in new[]{(0,0),(upright.Width/4,upright.Height/2),(upright.Width/2,upright.Height/2),(upright.Width-1,upright.Height-1)}){
            var i=(y*upright.Width+x)*3;using var one=new HighBitDepthImageBuffer(1,1,upright.Rgb32.Slice(i,3).ToArray(),"16");var a=ColorStudioProcessingAnalysis.Create(new VisualPixelBuffer(1,1,new byte[3]),stack,null);using var expected=new ColorStudioRenderPipeline().Render(one,a,null,stack,captureNodeDiagnostics:false,tile:new(x,y,upright.Width,upright.Height)).ProcessingPixels!;for(int c=0;c<3;c++)max=Math.Max(max,Math.Abs(full.Rgb32.Span[i+c]-expected.Rgb32.Span[c]));points.Add(new{x,y,before=upright.Rgb32.Slice(i,3).ToArray(),after=full.Rgb32.Slice(i,3).ToArray()});}
        Assert.AreEqual(0,max,1e-6);Assert.IsFalse(upright.Rgb32.ToArray().SequenceEqual(full.Rgb32.ToArray()));
        File.WriteAllText(Path.Combine(output,"REAL_RAW_RESULT.json"),JsonSerializer.Serialize(new{sourceFile=Path.GetFileName(input),master.SourceSha256,upright.Width,upright.Height,sourceOrientation=master.Image.Orientation,outputOrientation=upright.Orientation,master.Image.WorkingColorSpace,decodeMs,renderMs,encodeMs,encodedTiffMaxU16Delta=0,sampledFullFrameMaxDelta=max,points,peakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,CalibrationEligible=false,scope="One existing named RAW; not all cameras, physical calibration, ICC equivalence, pointer or DPI acceptance."},new JsonSerializerOptions{WriteIndented=true}));
    }
}

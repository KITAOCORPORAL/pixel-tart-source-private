using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
[DoNotParallelize]
public sealed class SpatialClosureRealTests
{
    [TestMethod]
    public async Task OfficialCandidateDecodesSonyAndOldCameraFilesWithoutPreviewFallback()
    {
        var executable=Environment.GetEnvironmentVariable("KITAO_REVIEWED_LIBRAW");var evidence=Environment.GetEnvironmentVariable("KITAO_SPATIAL_EVIDENCE");
        if(string.IsNullOrWhiteSpace(executable)||string.IsNullOrWhiteSpace(evidence)){Assert.Inconclusive("Reviewed official candidate opt-in required.");return;}
        Directory.CreateDirectory(evidence);var native=Path.Combine(Path.GetDirectoryName(executable)!,"libraw.dll");
        string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
        var config=new ReviewedRawCandidateConfiguration(executable,Hash(executable),Hash(native));var results=new List<object>();
        foreach(var (name,sourceHash) in new[]{("DSC09916.ARW","6F596BB234EE5DE206C66594BC84205349C363FD6A89AE188FF76DAB7C5E53B9"),("TNAN8886.CR3","B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401"),("DSCF0347.RAF","52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865")})
        {
            var path=Path.Combine("D:/AI AGENT/PixelTart-TestAssets/CompanyRAW",name);Assert.AreEqual(sourceHash,Hash(path));var watch=Stopwatch.StartNew();
            try{var actual=await ReviewedLibRawCandidate.DecodeAsync(path,config);Assert.AreEqual("16",actual.Pixels.SourceBitDepth);Assert.AreEqual(sourceHash,actual.SourceSha256);Assert.IsGreaterThan(3000,actual.Pixels.Width);Assert.IsGreaterThan(3000,actual.Pixels.Height);results.Add(new{name,status="PASS",actual.Pixels.Width,actual.Pixels.Height,actual.DecoderVersion,actual.NumericDomain,milliseconds=watch.Elapsed.TotalMilliseconds,pixelHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(actual.Pixels.Rgb32.Span))),sourceUnchanged=Hash(path)==sourceHash});}
            catch(Exception error){results.Add(new{name,status="FAILED",error=error.ToString()});throw;}
            finally{await File.WriteAllTextAsync(Path.Combine(evidence,"candidate-real.json"),JsonSerializer.Serialize(new{configuration=config,results},new JsonSerializerOptions{WriteIndented=true}));}
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        }
        await Assert.ThrowsAsync<InvalidDataException>(()=>ReviewedLibRawCandidate.DecodeAsync("D:/AI AGENT/PixelTart-TestAssets/CompanyRAW/DSC09916.ARW",config with{ExecutableSha256=new string('0',64)}));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>ReviewedLibRawCandidate.DecodeAsync("D:/AI AGENT/PixelTart-TestAssets/CompanyRAW/DSC09916.ARW",config,cancelled.Token));
    }
    [TestMethod]
    public async Task RealCanonSpatialNodesRecordIsolatedOperationPeakAndPixelReference()
    {
        var evidence=Environment.GetEnvironmentVariable("KITAO_SPATIAL_EVIDENCE");if(string.IsNullOrWhiteSpace(evidence)){Assert.Inconclusive("Authorized real measurement opt-in required.");return;}
        Directory.CreateDirectory(evidence);var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var frozen=await pipeline.DecodeFrozenMasterAsync("D:/AI AGENT/PixelTart-TestAssets/CompanyRAW/TNAN8886.CR3");var source=RawImageOrientation.NormalizePixels(frozen.Image);var process=Process.GetCurrentProcess();var rows=new List<object>();var failures=0;
        var referencePixels=source.ToVisualRgb24();var analysis=RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis.VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"reference",referencePixels));
        var look=new ReferenceLook(Guid.NewGuid(),"Authorized test",null,[new(Guid.NewGuid(),null,"Canon reference","local-test",frozen.SourceSha256,1,analysis)],new(),DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
        var nodes=new[]{new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.ReferenceMatch,"ReferenceMatch"),new(Guid.NewGuid(),ColorStudioNodeType.Details,"Details",NumericParameters:new Dictionary<string,double>{{"clarity",10},{"structure",15},{"sharpen",20}}),new(Guid.NewGuid(),ColorStudioNodeType.Develop,"Structure",NumericParameters:new Dictionary<string,double>{{"structure",20}}),new(Guid.NewGuid(),ColorStudioNodeType.Film,"Bloom",FilmSettings:new(true,BloomAmount:20,SpatialVersion:2)),new(Guid.NewGuid(),ColorStudioNodeType.Film,"Halation",FilmSettings:new(true,HalationAmount:20,SpatialVersion:2))};
        foreach(var node in nodes)
        {
            var watch=Stopwatch.StartNew();long peakWorking=0,peakPrivate=0,peakManaged=0,peakLedger=0;using var poll=new CancellationTokenSource();
            var monitor=Task.Run(async()=>{while(!poll.IsCancellationRequested){process.Refresh();peakWorking=Math.Max(peakWorking,process.WorkingSet64);peakPrivate=Math.Max(peakPrivate,process.PrivateMemorySize64);peakManaged=Math.Max(peakManaged,GC.GetTotalMemory(false));peakLedger=Math.Max(peakLedger,StudioGlobalResources.AccountedBytes);try{await Task.Delay(10,poll.Token);}catch(OperationCanceledException){}}});
            try
            {
                var plan=SpatialLegacyRawRenderer.Plan(source,node);var actual=await Task.Run(()=>SpatialLegacyRawRenderer.RenderNode(source,node,node.Type==ColorStudioNodeType.ReferenceMatch?look:null));var tiledMs=watch.Elapsed.TotalMilliseconds;
                process.Refresh();var tilePeak=new{peakWorking,peakPrivate,peakManaged,peakLedger};peakWorking=0;peakPrivate=0;peakManaged=0;peakLedger=0;
                // Full real reference can exceed2GiB for Details; never pretend a crop verifies full-image processing.
                var referenceEstimate=(long)source.PixelCount*(node.Type==ColorStudioNodeType.Details?140:node.Type==ColorStudioNodeType.Film?64:60)+64L*1024*1024;
                double? maximum=null;string oracle;double? fullMs=null;
                if(referenceEstimate>StudioGlobalResources.TotalLimit){oracle="FULL_REAL_REFERENCE_BUDGET_REJECTED; synthetic full-image golden covers node";}
                else{using var admission=StudioGlobalResources.ReserveWork(referenceEstimate);watch.Restart();var expected=pipeline.Render(source,node.Type==ColorStudioNodeType.ReferenceMatch?look:null,new([node])).ProcessingPixels!;fullMs=watch.Elapsed.TotalMilliseconds;double max=0;for(var i=0;i<actual.Rgb32.Length;i++)max=Math.Max(max,Math.Abs(actual.Rgb32.Span[i]-expected.Rgb32.Span[i]));maximum=max;Assert.IsLessThanOrEqualTo(node.Type==ColorStudioNodeType.Details||node.Type==ColorStudioNodeType.Develop?2e-6:0,max);oracle="FULL_REAL_PIXELS_COMPARED";}
                poll.Cancel();await monitor;rows.Add(new{node=node.Name,status="PASS",plan,tiledMs,fullMs,oracle,maximum,tilePeak,fullReferencePeak=new{peakWorking,peakPrivate,peakManaged,peakLedger},peakWorking,peakPrivate,peakManaged,peakLedger,processLifetimePeak=process.PeakWorkingSet64});
            }
            catch(Exception error){failures++;poll.Cancel();await monitor;rows.Add(new{node=node.Name,status="FAILED_OR_REJECTED",error=error.ToString(),peakWorking,peakPrivate,peakManaged,peakLedger});}
            finally{await File.WriteAllTextAsync(Path.Combine(evidence,"spatial-real-nodes.json"),JsonSerializer.Serialize(new{source=frozen.SourceSha256,source.Width,source.Height,rows,desktopWindowOpened=false},new JsonSerializerOptions{WriteIndented=true}));}
            GC.Collect();GC.WaitForPendingFinalizers();
        }
        Assert.AreEqual(5,rows.Count);Assert.AreEqual(0,failures,"Per-node failed/rejected outcomes must not be promoted to test pass.");
    }
}

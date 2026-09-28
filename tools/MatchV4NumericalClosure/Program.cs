using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
using PixelTart.MatchV4.Dx12;

var root = Arg("--root");
var output = Arg("--output") ?? throw new ArgumentException("External --output is required.");
Directory.CreateDirectory(output);
var backend = new Dx12ColorMatchComputeBackend();
if (!backend.IsAvailable) throw new InvalidOperationException("A real GPU is required.");
var rows = new List<object>();
var executor = new MatchV4ProductExecutor(new ReferenceMatchV4Engine(gpu: new MatchV4BackendAdapter(backend)), new MatchV4PixelExecutor(new MatchV4CpuPixelBackend(), backend), backend.Capability);
var settings = new ReferenceMatchV4Settings(MaximumRepresentativeSamples:256, SinkhornIterations:24, TileSize:1024);
var names = (Arg("--fixture") is { } one ? new[] { one } : new[] { "TNAN8886.CR3", "DSCF0347.RAF", "DSCF0370.RAF", "LMAN1714.RAF" });
var decoder = new RawMatchTiff16ProductPipeline(new LibRawDecoder());
var reference = await decoder.DecodeFrozenMasterAsync(Find(names[0]));
foreach(var name in names)
{
    Console.WriteLine("Starting " + name);
    var decode = Stopwatch.StartNew();
    var master = name == names[0] ? reference : await decoder.DecodeFrozenMasterAsync(Find(name));
    decode.Stop();
    var refMaster = name == names[0] ? await decoder.DecodeFrozenMasterAsync(Find(names.Length > 1 ? names[1] : "TNAN8886.CR3")) : reference;
    var analysisWatch = Stopwatch.StartNew();
    var analysis = executor.Resolve(master, refMaster.Image, refMaster.SourceSha256, settings);
    analysisWatch.Stop();
    var session = new MatchV4ProductSession(executor, master, analysis);
    var repeats = new List<object>(); var allPass = true; string? firstHash = null; bool repeatStable = true;
    for(var repeat=0; repeat<(Arg("--single") is null ? 3 : 1); repeat++)
    {
        var clock = Stopwatch.StartNew();
        var cpu = await session.ProcessFullResolutionAsync(.72, false, MatchV4ExecutionMode.Cpu);
        var cpuMs = clock.Elapsed.TotalMilliseconds; clock.Restart();
        var gpu = await session.ProcessFullResolutionAsync(.72, false, MatchV4ExecutionMode.Auto);
        var gpuMs = clock.Elapsed.TotalMilliseconds;
        var metrics = ClosureMetrics.Compare(cpu.Pixels,gpu.Pixels);
        var hash = Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(gpu.Pixels.Rgb32.Span)));
        firstHash ??= hash; repeatStable &= hash == firstHash;
        var route = gpu.Backend == ReferenceMatchV4BackendKind.Gpu && !gpu.UsedCpuFallback && gpu.GpuFailure is null;
        allPass &= metrics.Pass && route;
        repeats.Add(new { repeat, cpuMs, gpuMs, gpu.Backend, gpu.UsedCpuFallback, gpu.GpuFailure, metrics, outputSha256=hash });
        Console.WriteLine($"{name} repeat {repeat}: max={metrics.Max:R} pass={metrics.Pass} route={route}");
    }
    object? tiff = null;
    if(allPass)
    {
        var clock=Stopwatch.StartNew();
        var cpuExport=await session.ExportTiff16Async(Path.Combine(output,name+"-cpu.tif"),.72,false,MatchV4ExecutionMode.Cpu);
        var cpuExportMs=clock.Elapsed.TotalMilliseconds; clock.Restart();
        var gpuExport=await session.ExportTiff16Async(Path.Combine(output,name+"-gpu.tif"),.72,false,MatchV4ExecutionMode.Auto);
        var gpuExportMs=clock.Elapsed.TotalMilliseconds;
        using var cs=File.OpenRead(Path.Combine(output,name+"-cpu.tif")); using var gs=File.OpenRead(Path.Combine(output,name+"-gpu.tif"));
        var cr=TiffReadBack.Read(cs); var gr=TiffReadBack.Read(gs);
        var readback=ClosureMetrics.Tiff(cr,gr);
        tiff=new { cpuExportMs,gpuExportMs,readback,identity=new{master.SourceSha256,referenceIdentity=refMaster.SourceSha256,session.DecodeGenerationId,session.ProcessingGenerationId,cpuTransform=cpuExport.Match.TransformHash,gpuTransform=gpuExport.Match.TransformHash, same=cpuExport.Match.TransformHash==gpuExport.Match.TransformHash && cpuExport.Match.ProcessingGenerationId==gpuExport.Match.ProcessingGenerationId},gpuExport.Match.Backend,gpuExport.Match.UsedCpuFallback };
    }
    var proxy=decoder.PreviewMaster(master.Image,1600);
    var pc=await session.PreviewAsync(proxy,.72,false,MatchV4ExecutionMode.Cpu);var pg=await session.PreviewAsync(proxy,.72,false,MatchV4ExecutionMode.Auto);
    var timing=await ((IMatchV4PixelBackend)backend).ExecuteAsync(master.Image,analysis.Transform.WithExecution(.72,false));
    rows.Add(new { file=name,master.SourceSha256,master.Width,master.Height,megapixels=master.Image.PixelCount/1e6,decodeMs=name==names[0]?(double?)null:decode.Elapsed.TotalMilliseconds,masterFloatBytes=master.Image.Rgb32.Length*4L,analysisMs=analysisWatch.Elapsed.TotalMilliseconds,analysis, repeats,repeatStable,fullResPass=allPass,proxyParity=ClosureMetrics.Compare(pc.Pixels,pg.Pixels),tiff,timing=new { classificationMs=timing.ClassificationTime.TotalMilliseconds,uploadMs=timing.UploadTime.TotalMilliseconds,computeMs=timing.ComputeTime.TotalMilliseconds,readbackMs=timing.ReadbackTime.TotalMilliseconds,timing.TileCount },workingSet=Process.GetCurrentProcess().WorkingSet64 });
    await File.WriteAllTextAsync(Path.Combine(output,"full-resolution.json"),JsonSerializer.Serialize(new{hardware=backend.Capability,rows},new JsonSerializerOptions{WriteIndented=true}));
    GC.Collect(); GC.WaitForPendingFinalizers();
}
return 0;
string Find(string name)=>Directory.EnumerateFiles(root!,name,SearchOption.AllDirectories).Single();
string? Arg(string key){var i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:null;}

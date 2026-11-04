using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RawReopenStageProbe;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--self-test-no-raw"){Probe.SerializationSelfTest(args[1]);return 0;}
        if(args.Length != 4 || args[0] != "--decode-once") { Console.Error.WriteLine("Usage: --decode-once <frozen-manifest.json> <new-result-directory> <first|second>"); return 2; }
        if(args[3] is not ("first" or "second")) return 2;
        var manifest=JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(args[1]),Probe.Json)!;
        if(manifest.Version!="RAW-REOPEN-001-StageProbeV1.3" || manifest.Input.Id!="company-02" || manifest.Input.Sha256!="52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865")throw new InvalidDataException("Only the approved unchanged company-02 input is admitted");
        if(Directory.Exists(args[2]))throw new IOException("Existing result directories must be preserved");
        if(Probe.HashFile(manifest.Input.Path)!=manifest.Input.Sha256)throw new InvalidDataException("Input changed");
        foreach(var file in manifest.Files) if(Probe.HashFile(file.Path)!=file.Sha256)throw new InvalidDataException("Frozen binary changed: "+file.Path);
        if(7752L*5184*3*sizeof(float)>ProfessionalRawResourcePolicy.MasterBudgetBytes || StudioGlobalResources.TotalLimit!=2147483648L)throw new InvalidDataException("Existing budget does not admit this master");
        var marker=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,args[3]+".consumed");
        using(var once=new FileStream(marker,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { var bytes=System.Text.Encoding.UTF8.GetBytes(Environment.ProcessId.ToString());once.Write(bytes); }
        Directory.CreateDirectory(args[2]);Probe.Initialize(args[2],manifest,args[3]);
        try
        {
            var loadedVersion=RawContext.Version;
            var loadedRaw=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>m.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase));
            if(!Path.GetFullPath(loadedRaw.FileName).Equals(Path.GetFullPath(manifest.NativePath),StringComparison.OrdinalIgnoreCase)||Probe.HashFile(loadedRaw.FileName)!=manifest.Files.Single(f=>f.Path==manifest.NativePath).Sha256)throw new InvalidDataException("Unexpected actual native module");
            Probe.Write("actual-native-loaded",new{loadedRaw.FileName,sha256=Probe.HashFile(loadedRaw.FileName),loadedVersion,loader="Unmodified Sdcb.LibRaw loader"});
            Probe.Write("runtime",new{processId=Environment.ProcessId,architecture=RuntimeInformation.ProcessArchitecture.ToString(),os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,Environment.ProcessorCount,Environment.CurrentDirectory,culture=System.Globalization.CultureInfo.CurrentCulture.Name,utcNow=DateTimeOffset.UtcNow,libRaw=RawContext.Version,wrapper=typeof(RawContext).Assembly.FullName,rawOptions=new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode),masterBudgetBytes=ProfessionalRawResourcePolicy.MasterBudgetBytes,totalBudgetBytes=StudioGlobalResources.TotalLimit,environment=new[]{"OMP_NUM_THREADS","OMP_DYNAMIC","OMP_THREAD_LIMIT","OMP_SCHEDULE","OMP_PROC_BIND","OMP_PLACES","KMP_AFFINITY","KMP_BLOCKTIME","OPENBLAS_NUM_THREADS","DOTNET_PROCESSOR_COUNT","COMPlus_ThreadPool_ForceMinWorkerThreads"}.ToDictionary(k=>k,Environment.GetEnvironmentVariable),note="No thread, algorithm, decoder or output-parameter overrides; unmodified wrapper loader uses the identical shipped native binary; actual loaded path/hash checked"});
            var decoder=new ObservedLibRawDecoder();
            var decoded=await decoder.DecodeAsync(manifest.Input.Path,new RawToJpegOptions(AutoRotate:false,DecodeMode:RawDecodeMode.ProfessionalDecode));
            Probe.Event("DecodeAsyncReturned_AllUsingScopesExited");
            if(decoded.Width!=7752||decoded.Height!=5184||decoded.BitsPerChannel!=16||decoded.Stride!=decoded.Width*6||decoded.Rgb48Pixels is null)throw new InvalidDataException("Unexpected output layout");
            Probe.CheckReturned(decoded.Rgb48Pixels);
            using var master=HighBitDepthImageBuffer.FromRaw(decoded);
            Probe.Float("float-master",master);
            var upright=RawImageOrientation.NormalizePixels(master);
            try
            {
                Probe.Float("upright-master",upright);
                if(upright.Orientation!=1)throw new InvalidDataException("Orientation not normalized");
                long conversionErrors=0,roundTripErrors=0;
                for(int i=0;i<decoded.Rgb48Pixels.Length;i++){if(BitConverter.SingleToInt32Bits(master.Rgb32.Span[i])!=BitConverter.SingleToInt32Bits(decoded.Rgb48Pixels[i]/65535f))conversionErrors++;if((int)Math.Round(master.Rgb32.Span[i]*65535d)!=decoded.Rgb48Pixels[i])roundTripErrors++;}
                Probe.Write("conversion-check",new{conversionErrors,roundTripErrors,orientationNoOp=ReferenceEquals(master,upright),sourceOrientation=decoded.Metadata.Orientation,masterFloatBytes=master.Rgb32.Length*sizeof(float),layout=new{decoded.Width,decoded.Height,decoded.Stride,decoded.Channels,decoded.BitsPerChannel,decoded.PixelFormat}});
            }
            finally{if(!ReferenceEquals(master,upright))upright.Dispose();}
            Probe.Write("loaded-modules",Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(m=>m.FileName.StartsWith(AppContext.BaseDirectory,StringComparison.OrdinalIgnoreCase)||m.FileName.Equals(manifest.NativePath,StringComparison.OrdinalIgnoreCase)||m.ModuleName.Contains("vcomp",StringComparison.OrdinalIgnoreCase)||m.ModuleName.Contains("libomp",StringComparison.OrdinalIgnoreCase)).Select(m=>new{m.ModuleName,m.FileName,sha256=Probe.HashFile(m.FileName)}).ToArray());
            if(Probe.HashFile(manifest.Input.Path)!=manifest.Input.Sha256)throw new InvalidDataException("Input changed during probe");
            Probe.Finish("COMPLETED",null);return 0;
        }
        catch(Exception ex){Probe.Finish("FAILED_NO_AUTOMATIC_RETRY",ex.ToString());Console.Error.WriteLine(ex);return 1;}
    }
}
internal sealed record FileIdentity(string Path,string Sha256);
internal sealed record InputIdentity(string Id,string Path,string Sha256);
internal sealed record Manifest(string Version,InputIdentity Input,string NativePath,FileIdentity[] Files);
internal static class Probe
{
    internal static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    private static string root="";
    private static readonly Dictionary<string,int> counts=new();
    private static readonly List<object> stages=new();
    private static readonly List<object> events=new();
    private static readonly Stopwatch clock=Stopwatch.StartNew();
    private static Timer? sampler;
    private static long maxPrivate,maxWorking;
    private static string nativeHash="",managedHash="";
    private static readonly object sync=new();
    internal static void Initialize(string output,Manifest manifest,string slot){root=output;Write("manifest-used",manifest);Write("run-slot",new{slot,pid=Environment.ProcessId,version=manifest.Version});sampler=new Timer(_=>Sample(),null,0,50);Event("Initialized");}
    private static void Sample(){lock(sync){using var p=Process.GetCurrentProcess();p.Refresh();maxPrivate=Math.Max(maxPrivate,p.PrivateMemorySize64);maxWorking=Math.Max(maxWorking,p.WorkingSet64);}}
    internal static void SerializationSelfTest(string output){if(Directory.Exists(output))throw new IOException("Preserve self-test receipt");Directory.CreateDirectory(output);root=output;var data=new{output=Snapshot(new OutputParams()),unpack=Snapshot(default(RawUnpackParams)),nativeOutput=Snapshot(default(LibRawOutputParams)),color=Snapshot(default(LibRawColorData)),image=Snapshot(Activator.CreateInstance(typeof(RawContext).GetProperty("ImageParams")!.PropertyType)),skippedByRefLikeProperties=new[]{typeof(RawUnpackParams),typeof(LibRawOutputParams),typeof(LibRawColorData),typeof(RawContext).GetProperty("ImageParams")!.PropertyType}.SelectMany(t=>t.GetProperties().Where(p=>p.PropertyType.IsByRefLike||p.PropertyType.IsByRef||p.PropertyType.IsPointer).Select(p=>t.FullName+"."+p.Name)).ToArray(),nativeCalls=0};Write("serialization-self-test",data);}
    internal static string HashFile(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(s));}
    internal static void Write(string name,object value)=>File.WriteAllText(Path.Combine(root,name+".json"),JsonSerializer.Serialize(value,Json)+"\n");
    internal static void Event(string name){Sample();lock(sync){events.Add(new{name,elapsedMs=clock.Elapsed.TotalMilliseconds,threadId=Environment.CurrentManagedThreadId,privateBytes=maxPrivate,workingBytes=maxWorking});Write("events",events);}}
    internal static void Count(string name){counts[name]=counts.GetValueOrDefault(name)+1;Write("calls",counts);if(counts[name]!=1)throw new InvalidOperationException("Only one default "+name+" call per process is allowed");Event(name);}
    internal static void Orientation(ushort value)=>Write("source-orientation",new{value});
    internal static object? Snapshot(object? v,int depth=0)
    {
        if(v is null)return null;var t=v.GetType();if(v is IntPtr p)return "0x"+p.ToInt64().ToString("X");if(v is UIntPtr up)return "0x"+up.ToUInt64().ToString("X");if(v is string||t.IsPrimitive||v is decimal)return v;if(t.IsEnum)return Convert.ToInt64(v);
        if(v is Array a)return a.Cast<object?>().Select(x=>Snapshot(x,depth+1)).ToArray();if(depth>8)throw new InvalidDataException("Unexpected parameter nesting");
        var result=new SortedDictionary<string,object?>();foreach(var f in t.GetFields(BindingFlags.Public|BindingFlags.Instance))result[f.Name]=Snapshot(f.GetValue(v),depth+1);foreach(var p2 in t.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0&&!p.PropertyType.IsByRefLike&&!p.PropertyType.IsByRef&&!p.PropertyType.IsPointer))if(!result.ContainsKey(p2.Name))result[p2.Name]=Snapshot(p2.GetValue(v),depth+1);return result;
    }
    internal static void Parameters(string stage,RawContext ctx)
    {
        if(!Environment.Is64BitProcess||typeof(RawContext).Assembly.GetName().Version!=new Version(0,21,1,7)||RawContext.Version!="0.21.1-Release")throw new NotSupportedException("Unverified ABI");
        var outputOffset=Marshal.OffsetOf<LibRawData>(nameof(LibRawData.OutputParams));var known=Marshal.OffsetOf(typeof(RawContext).Assembly.GetType("Sdcb.LibRaw.Natives.LibRawDataX64",true)!,"OutputParams");if(outputOffset!=known)throw new InvalidDataException("Output ABI mismatch");
        IntPtr At(string field)=>IntPtr.Add(ctx.UnsafeGetHandle(),checked((int)Marshal.OffsetOf<LibRawData>(field)));
        var raw=Marshal.PtrToStructure<RawUnpackParams>(At(nameof(LibRawData.RawUnpackParams)));
        var nativeOutput=Marshal.PtrToStructure<LibRawOutputParams>(At(nameof(LibRawData.OutputParams)));
        var color=Marshal.PtrToStructure<LibRawColorData>(At(nameof(LibRawData.Color)));
        if(color.Maximum!=ctx.ColorMaximum||color.CamMul.Length!=4||Enumerable.Range(0,4).Any(i=>color.CamMul[i]!=ctx.CameraMultipler[i])||Enumerable.Range(0,12).Any(i=>color.RgbCam[i]!=ctx.RgbCamera[i/4,i%4]))throw new InvalidDataException("Color ABI/getter mismatch");
        Write(stage+"-parameters",new{output=Snapshot(OutputParams.FromLibRaw(ctx.UnsafeGetHandle())),nativeOutput=Snapshot(nativeOutput),unpack=Snapshot(raw),color=Snapshot(color),image=Snapshot(ctx.ImageParams),rawWidth=ctx.RawWidth,rawHeight=ctx.RawHeight,outputOffset=outputOffset.ToInt64(),scope="Read-only native parameter structs and public getters; no raw sensor buffer or post-hoc inferred parameter equality"});Event(stage+"-parameters");
    }
    internal static void Configured(OutputParams output){Write("configured-output",Snapshot(output)!);Event("ConfiguredOutputBeforeWrapperCommit");}
    private static string Dump(string name,ReadOnlySpan<byte> bytes,object layout){string p=Path.Combine(root,name+".bin");using(var s=new FileStream(p,FileMode.CreateNew,FileAccess.Write,FileShare.Read,1024*1024))s.Write(bytes);var hash=Convert.ToHexString(SHA256.HashData(bytes));stages.Add(new{name,path=p,sha256=hash,bytes=bytes.Length,layout});Write("stages",stages);Event(name);return hash;}
    internal static void Native(ProcessedImage image){if(image.Bits!=16||image.Channels!=3)throw new InvalidDataException("Expected RGB16");var samples=checked(image.Width*image.Height*3);if(image.DataSize<samples*2)throw new InvalidDataException("Short native image");nativeHash=Dump("native-rgb16",MemoryMarshal.AsBytes(image.AsSpan<ushort>()[..samples]),new{image.Width,image.Height,image.Channels,image.Bits,image.DataSize,validBytes=samples*2,stride=image.Width*6,littleEndian=BitConverter.IsLittleEndian});}
    internal static void Managed(ProcessedImage image,ushort[]? values){if(values is null)throw new InvalidDataException("Missing copied RGB16");managedHash=Dump("managed-rgb16",MemoryMarshal.AsBytes(values.AsSpan()),new{image.Width,image.Height,samples=values.Length});var nativeAfter=Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.AsSpan<ushort>()[..values.Length])));Write("copy-check",new{nativeHash,managedHash,nativeAfter,exact=nativeHash==managedHash&&nativeAfter==nativeHash});if(nativeHash!=managedHash||nativeAfter!=nativeHash)throw new InvalidDataException("Native/managed copy differs");}
    internal static void CheckReturned(ushort[] values){var returned=Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan())));Write("returned-copy-check",new{managedHash,returned,exact=returned==managedHash});if(returned!=managedHash)throw new InvalidDataException("Copy changed after native disposal");}
    internal static void Float(string name,HighBitDepthImageBuffer image)=>Dump(name,MemoryMarshal.AsBytes(image.Rgb32.Span),new{image.Width,image.Height,image.Orientation,image.WorkingColorSpace,image.SourceBitDepth,encoding="IEEE754 little-endian float32 RGB"});
    internal static void Finish(string status,string? error){sampler?.Dispose();Sample();using var p=Process.GetCurrentProcess();p.Refresh();Write("result",new{status,error,version="RAW-REOPEN-001-StageProbeV1.3",pid=Environment.ProcessId,elapsedMs=clock.Elapsed.TotalMilliseconds,counts,peakWorkingSetOs=p.PeakWorkingSet64,peakWorkingSetSampled=maxWorking,peakPrivateBytesSampled=maxPrivate,privateSamplePeriodMs=50,scope="At most one default native development in an isolated process; completed counts are authoritative; no App preview/export/reopen rerun; full stage binaries retained locally"});}
}

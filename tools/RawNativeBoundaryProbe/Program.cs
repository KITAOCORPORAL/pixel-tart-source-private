using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
namespace RawNativeBoundaryProbe;
internal sealed record FileIdentity(string Path,string Sha256);
internal sealed record InputIdentity(string Id,string Path,string Sha256);
internal sealed record Manifest(string Version,InputIdentity Input,string NativePath,FileIdentity[] Files,Dictionary<string,string?> Environment,Dictionary<string,string?> StartupEnvironment);
internal sealed record Layout(int Width,int Height,int SourcePixelChannels,int ValidChannels,int SourcePitch,int Left=0,int Top=0);
internal static class Program
{
 const string Version="RAW-REOPEN-001-NativeBoundaryV2.1";
 const string RawHash="F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9";
 const string BindingHash="6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3";
 static readonly string[] EnvNames={"OMP_NUM_THREADS","OMP_DYNAMIC","OMP_THREAD_LIMIT","OMP_SCHEDULE","OMP_PROC_BIND","OMP_PLACES","KMP_AFFINITY","KMP_BLOCKTIME","OPENBLAS_NUM_THREADS","DOTNET_PROCESSOR_COUNT","COMPlus_ThreadPool_ForceMinWorkerThreads"};
 static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
 static string Root="";
 static readonly Dictionary<string,int> Counts=new();
 static readonly List<object> Stages=new(),Events=new();
 static readonly Stopwatch Clock=Stopwatch.StartNew();
 static readonly object Sync=new();
 static long MaxPrivate,MaxWorking;
 static Timer? Sampler;
 static int Off<T>(string f)=>checked((int)Marshal.OffsetOf<T>(f));
 static IntPtr At(RawContext c,string f)=>IntPtr.Add(c.UnsafeGetHandle(),Off<LibRawData>(f));
 static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
 static void Write(string name,object? value)=>File.WriteAllText(Path.Combine(Root,name+".json"),JsonSerializer.Serialize(value,Json)+"\n");
 static string HashFile(string p){using var s=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(s));}
 static Dictionary<string,string?> EnvironmentNow()=>EnvNames.ToDictionary(k=>k,System.Environment.GetEnvironmentVariable);
 static object? Snapshot(object? v,int depth=0)
 {
  if(v is null)return null;var t=v.GetType();if(v is IntPtr p)return "0x"+p.ToInt64().ToString("X");if(v is UIntPtr up)return "0x"+up.ToUInt64().ToString("X");if(v is string||t.IsPrimitive||v is decimal)return v;if(t.IsEnum)return Convert.ToInt64(v);
  if(v is Array a)return a.Cast<object?>().Select(x=>Snapshot(x,depth+1)).ToArray();Require(depth<=8,"Unexpected nesting");
  var r=new SortedDictionary<string,object?>();foreach(var f in t.GetFields(BindingFlags.Public|BindingFlags.Instance))r[f.Name]=Snapshot(f.GetValue(v),depth+1);
  foreach(var x in t.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(x=>x.CanRead&&x.GetIndexParameters().Length==0&&!x.PropertyType.IsByRefLike&&!x.PropertyType.IsByRef&&!x.PropertyType.IsPointer))if(!r.ContainsKey(x.Name))r[x.Name]=Snapshot(x.GetValue(v),depth+1);return r;
 }
 static void Count(string n){Counts[n]=Counts.GetValueOrDefault(n)+1;Write("calls",Counts);Require(Counts[n]==1,"No repeated decoder calls");Event(n+"_enter");}
 static void Sample(){lock(Sync){using var p=Process.GetCurrentProcess();p.Refresh();MaxPrivate=Math.Max(MaxPrivate,p.PrivateMemorySize64);MaxWorking=Math.Max(MaxWorking,p.WorkingSet64);}}
 static void Event(string n){Sample();Events.Add(new{name=n,elapsedMs=Clock.Elapsed.TotalMilliseconds,managedThreadId=System.Environment.CurrentManagedThreadId,osThreadId=GetCurrentThreadId(),privateBytes=MaxPrivate,workingBytes=MaxWorking});Write("events",Events);}
 static void NativeIdentity(string? expected=null)
 {
  Require(System.Environment.Is64BitProcess&&BitConverter.IsLittleEndian,"Requires win-x64 LE");Require(typeof(RawContext).Assembly.GetName().Version==new System.Version(0,21,1,7),"Binding version");Require(HashFile(typeof(RawContext).Assembly.Location)==BindingHash,"Binding bytes");
  var ver=RawContext.Version;Require(ver=="0.21.1-Release","Native version");var m=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>m.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase));Require(HashFile(m.FileName)==RawHash,"Native bytes");if(expected!=null)Require(Path.GetFullPath(m.FileName).Equals(Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase),"Actual native path");
  Write("actual-native-loaded",new{m.FileName,sha256=RawHash,version=ver,binding=typeof(RawContext).Assembly.Location,bindingSha256=BindingHash});
 }
 static void VerifyLayout(RawContext c)
 {
  // Fixed binding 43e9c177 + actual DLL disassembly. Not portable to other DLLs/headers.
  Require(Off<LibRawData>(nameof(LibRawData.Image))==0&&Off<LibRawData>(nameof(LibRawData.ImageSizes))==8,"Image/sizes ABI");
  Require(Off<LibRawData>(nameof(LibRawData.ImageParams))==0xc0&&Off<LibRawData>(nameof(LibRawData.OtherParams))==0x2efa0&&Off<LibRawData>(nameof(LibRawData.RawData))==0x2f3e0&&Off<LibRawData>(nameof(LibRawData.ParentClass))==0x5d140,"Top-level ABI");
  Require(Off<LibRawRawData>(nameof(LibRawRawData.RawImage))==8&&Off<LibRawRawData>(nameof(LibRawRawData.Sizes))==512,"Raw ABI");
  Require(Off<LibRawImageSizes>(nameof(LibRawImageSizes.RawPitch))==16&&Off<LibRawImageSizes>(nameof(LibRawImageSizes.Flip))==32&&Marshal.SizeOf<LibRawImageSizes>()==184,"Sizes ABI");
  var h=c.UnsafeGetHandle();CheckReadable(h,381256);
  Require(LibRawNative.GetImageParameters(h)==At(c,nameof(LibRawData.ImageParams))&&LibRawNative.GetImageOtherParameters(h)==At(c,nameof(LibRawData.OtherParams))&&LibRawNative.GetLensInformation(h)==At(c,nameof(LibRawData.LensInfo)),"Accessor address ABI");
  var s=Sizes(c);Require(s.RawWidth==c.RawWidth&&s.RawHeight==c.RawHeight&&s.IWidth==c.Width&&s.IHeight==c.Height,"Dimension accessor ABI");
  Require(Marshal.ReadIntPtr(At(c,nameof(LibRawData.ParentClass)))==IntPtr.Subtract(h,8),"C++ parent/imgdata delta");
 }
 static LibRawImageSizes Sizes(RawContext c)=>Marshal.PtrToStructure<LibRawImageSizes>(At(c,nameof(LibRawData.ImageSizes)));
 static void Parameters(string stage,RawContext c)
 {
  VerifyLayout(c);var color=Marshal.PtrToStructure<LibRawColorData>(At(c,nameof(LibRawData.Color)));
  Require(color.Maximum==c.ColorMaximum&&Enumerable.Range(0,4).All(i=>color.CamMul[i]==c.CameraMultipler[i])&&Enumerable.Range(0,12).All(i=>color.RgbCam[i]==c.RgbCamera[i/4,i%4]),"Color accessors");
  Write(stage+"-parameters",new{output=Snapshot(OutputParams.FromLibRaw(c.UnsafeGetHandle())),nativeOutput=Snapshot(Marshal.PtrToStructure<LibRawOutputParams>(At(c,nameof(LibRawData.OutputParams)))),unpack=Snapshot(Marshal.PtrToStructure<RawUnpackParams>(At(c,nameof(LibRawData.RawUnpackParams)))),color=Snapshot(color),image=Snapshot(c.ImageParams),sizes=Snapshot(Sizes(c)),decoder=Snapshot(c.DecoderInfo),rawWidth=c.RawWidth,rawHeight=c.RawHeight});
 }
 static void CheckLayout(Layout l)
 {
  Require(l.Width>0&&l.Height>0&&l.Width<=20000&&l.Height<=20000&&l.ValidChannels>0&&l.ValidChannels<=l.SourcePixelChannels&&l.SourcePixelChannels<=4&&l.Left>=0&&l.Top>=0,"Invalid dimensions/channels/rectangle");
  Require(l.SourcePitch%2==0&&l.SourcePitch>=checked((l.Left+l.Width)*l.SourcePixelChannels*2),"Invalid pitch");
  Require(checked((long)l.Height*l.Width*l.ValidChannels*2)<=536870912&&checked((long)(l.Top+l.Height)*l.SourcePitch)<=1073741824,"Layout budget");
 }
 static unsafe object Dump(string name,IntPtr ptr,Layout l,string domain)
 {
  CheckLayout(l);long last=checked((long)(l.Top+l.Height-1)*l.SourcePitch+(long)(l.Left+l.Width)*l.SourcePixelChannels*2);CheckReadable(ptr,last);
  int packedPitch=checked(l.Width*l.ValidChannels*2);byte[]? row=l.SourcePixelChannels==l.ValidChannels?null:new byte[packedPitch];
  using var whole=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var chunk=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var fs=new FileStream(Path.Combine(Root,name+".bin"),FileMode.CreateNew,FileAccess.Write,FileShare.Read,65536);
  var chunks=new List<object>();int start=0;
  for(int y=0;y<l.Height;y++)
  {
   var p=(ushort*)((byte*)ptr+checked((long)(y+l.Top)*l.SourcePitch+(long)l.Left*l.SourcePixelChannels*2));ReadOnlySpan<byte> bytes;
   if(row==null)bytes=new ReadOnlySpan<byte>(p,packedPitch);
   else{var dst=MemoryMarshal.Cast<byte,ushort>(row.AsSpan());for(int x=0;x<l.Width;x++)for(int c=0;c<l.ValidChannels;c++)dst[x*l.ValidChannels+c]=p[x*l.SourcePixelChannels+c];bytes=row;}
   fs.Write(bytes);whole.AppendData(bytes);chunk.AppendData(bytes);
   if((y+1)%64==0||y==l.Height-1){chunks.Add(new{firstRow=start,rows=y-start+1,sha256=Convert.ToHexString(chunk.GetHashAndReset())});start=y+1;}
  }
  fs.Flush();var item=new{name,path=fs.Name,sha256=Convert.ToHexString(whole.GetHashAndReset()),bytes=fs.Length,layout=l,packedPitch,domain,encoding="uint16 little-endian row-major; only declared valid channels",chunks,observerManagedPixelBufferBytes=row?.Length??0};Stages.Add(item);Write("stages",Stages);Event(name+"_captured");return item;
 }
 static void Unpacked(RawContext c)
 {
  var s=Sizes(c);var r=At(c,nameof(LibRawData.RawData));CheckReadable(r,712);var raw=Marshal.ReadIntPtr(r,8);var aliases=Enumerable.Range(2,5).Select(i=>Marshal.ReadIntPtr(r,i*8)).ToArray();
  Require(c.ImageParams.Filters==9&&c.ImageParams.Colors==3&&raw!=IntPtr.Zero&&aliases.All(p=>p==IntPtr.Zero),"Only verified single-plane X-Trans unpack layout admitted");
  var saved=Marshal.PtrToStructure<LibRawImageSizes>(IntPtr.Add(r,512));Require(JsonSerializer.Serialize(Snapshot(s))==JsonSerializer.Serialize(Snapshot(saved)),"Saved/current raw sizes mismatch");
  Require(s.RawWidth==7872&&s.RawHeight==5196&&s.Width==7752&&s.Height==5184&&s.LeftMargin+s.Width<=s.RawWidth&&s.TopMargin+s.Height<=s.RawHeight&&s.RawPitch==s.RawWidth*2,"Unexpected authorized RAW rectangle/pitch");
  Write("unpacked-layout",new{rawPointer="0x"+raw.ToInt64().ToString("X"),rawAlloc="0x"+Marshal.ReadIntPtr(r).ToInt64().ToString("X"),savedSizes=Snapshot(saved),sizes=Snapshot(s),lifetime="Borrowed raw_image after successful Unpack; captured before DcrawProcess; never retained after context disposal",selection="Only active width/height at margins; excludes optical-black/unused raw margins and pitch padding; one mosaic U16 sample per site; no direction transform",cfa=Snapshot(c.ImageParams.XTrans),decoder=Snapshot(c.DecoderInfo)});
  Dump("unpack-active-mosaic",raw,new(s.Width,s.Height,1,1,checked((int)s.RawPitch),s.LeftMargin,s.TopMargin),"Unpack active sensor mosaic; no WB/gamma/demosaic");
 }
 static void Processed(RawContext c)
 {
  var s=Sizes(c);var ip=c.ImageParams;var image=Marshal.ReadIntPtr(c.UnsafeGetHandle());
  Require(s.Width==7752&&s.Height==5184&&s.IWidth==s.Width&&s.IHeight==s.Height&&s.Flip==0&&ip.Colors==3,"Postprocess layout not verified");
  Write("processed-layout",new{imagePointer="0x"+image.ToInt64().ToString("X"),sizes=Snapshot(s),colors=ip.Colors,filters=ip.Filters,sourcePixelBytes=8,validChannels=3,unusedChannel=3,lifetime="Borrowed image after successful DcrawProcess, before MakeDcrawMemoryImage; context still alive",domain="LibRaw processed RGB U16 before memory-image gamma/auto-bright curve lookup; not sensor values, not final RGB16",validity="raw2image initializes image; process completes interpolation including border_interpolate; convert_to_rgb writes output RGB. Include output rectangle only, never unused fourth slot"});
  Dump("process-image-rgb",image,new(s.Width,s.Height,4,3,checked(s.IWidth*8)),"DcrawProcess image RGB before output curve; no bytewise cross-stage comparison");
 }
 static void Decode(Manifest manifest)
 {
  Count("OpenFile");using var c=RawContext.OpenFile(manifest.Input.Path);Event("OpenFile_return");Parameters("opened",c);
  Count("Unpack");c.Unpack();Event("Unpack_return");Parameters("unpacked",c);Unpacked(c);
  Count("DcrawProcess");c.DcrawProcess(output=>
  {
   // Same assignments as frozen product DecodeCore, ProfessionalDecode/AutoRotate=false.
   output.HalfSize=false;output.UseCameraWb=true;output.UseAutoWb=false;output.UseCameraMatrix=true;output.OutputColor=LibRawColorSpace.SRGB;output.OutputBps=16;output.OutputTiff=false;output.Interpolation=true;output.UserFlip=0;
   Write("configured-output",Snapshot(output));
  });Event("DcrawProcess_return");Parameters("processed",c);Processed(c);
  Count("MakeDcrawMemoryImage");using var image=c.MakeDcrawMemoryImage();Event("MakeDcrawMemoryImage_return");
  Require(image.ImageType==ProcessedImageType.Bitmap&&image.Width==7752&&image.Height==5184&&image.Channels==3&&image.Bits==16&&image.DataSize==241118208,"Memory image length/layout");
  Write("memory-layout",new{image.Width,image.Height,image.Channels,image.Bits,image.DataSize,stride=image.Width*6,validBytes=image.Width*image.Height*6,orientation=Sizes(c).Flip,lifetime="ProcessedImage owned by using; span captured before ClearDcrawMemory, which precedes context Recycle/Close"});
  unsafe{fixed(ushort* p=image.AsSpan<ushort>())Dump("memory-rgb16",(IntPtr)p,new(image.Width,image.Height,3,3,image.Width*6),"MakeDcrawMemoryImage returned RGB16 (gamma/auto-bright output curve applied)");}
 }
 static unsafe void SelfTest(string output)
 {
  Require(!Directory.Exists(output),"Preserve self-test");Directory.CreateDirectory(output);Root=output;Write("startup-environment",EnvironmentNow());NativeIdentity();
  using(var c=new RawContext(LibRawNative.Initialize())){Require(c.UnsafeGetHandle()!=IntPtr.Zero,"Init failed");VerifyLayout(c);Write("abi-layout",new{nativeVersion=RawContext.Version,structs=new[]{typeof(LibRawData),typeof(LibRawRawData),typeof(LibRawImageSizes),typeof(LibRawProcessedImage)}.Select(t=>new{name=t.FullName,size=Marshal.SizeOf(t),fields=t.GetFields().Select(f=>new{f.Name,offset=Marshal.OffsetOf(t,f.Name).ToInt64()}).ToArray()}).ToArray(),init=1,recycle=1,close=1,open=0,unpack=0,process=0,memoryImage=0});}
  const int pitch=48;var a=new ushort[48];for(int i=0;i<a.Length;i++)a[i]=checked((ushort)(i+1));var l=new Layout(3,2,4,3,pitch,1,0);object d1,d2,d3;
  fixed(ushort* p=a){d1=Dump("synthetic-a",(IntPtr)p,l,"Synthetic only");for(int y=0;y<2;y++){for(int x=0;x<6;x++)a[y*24+x*4+3]=65535;a[y*24]=65534;a[y*24+23]=65533;}d2=Dump("synthetic-padding-unused-mutated",(IntPtr)p,l,"Synthetic only");a[4]++;d3=Dump("synthetic-valid-mutated",(IntPtr)p,l,"Synthetic only");}
  string H(object o)=>JsonSerializer.SerializeToElement(o,Json).GetProperty("sha256").GetString()!;
  Require(H(d1)==H(d2)&&H(d1)!=H(d3),"Padding/channel exclusion failed");int rejected=0;foreach(var bad in new[]{l with{Width=0},l with{SourcePitch=7},l with{ValidChannels=5},l with{Left=-1},l with{Height=int.MaxValue}}){try{CheckLayout(bad);}catch(Exception e)when(e is InvalidDataException or OverflowException){rejected++;}}Require(rejected==5,"Invalid layout accepted");
  Write("self-test",new{environment=EnvironmentNow(),status="SELF_TEST_COMPLETED_NOT_A_PRODUCT_FIX",validSampleMutationDetected=true,paddingUnusedAndOutsideRectangleExcluded=true,invalidLayoutsRejected=rejected,open=0,unpack=0,process=0,memoryImage=0,smallSyntheticBufferBytes=a.Length*2});
 }
 public static async Task<int> Main(string[] args)
 {
  if(args.Length==2&&args[0]=="--self-test-no-raw"){SelfTest(args[1]);return 0;}
  if(args.Length!=4||args[0]!="--decode-once"||args[3] is not("first" or "second"))return 2;
  var m=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(args[1]),Json)!;Require(m.Version==Version&&m.Input.Id=="company-02"&&m.Input.Sha256=="52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865","Not authorized input/version");
  Require(!Directory.Exists(args[2]),"Preserve old result");foreach(var f in m.Files)Require(HashFile(f.Path)==f.Sha256,"Frozen file changed: "+f.Path);Require(HashFile(m.Input.Path)==m.Input.Sha256,"Input changed");Require(m.Environment.Count==EnvNames.Length&&m.Environment.GetValueOrDefault("OMP_NUM_THREADS")=="6"&&EnvNames.Where(n=>n!="OMP_NUM_THREADS").All(n=>m.Environment.ContainsKey(n)&&m.Environment[n]==null),"Manifest does not preserve the original thread environment");Require(m.StartupEnvironment.Count==EnvNames.Length&&EnvNames.All(n=>m.StartupEnvironment.ContainsKey(n)&&EnvironmentNow()[n]==m.StartupEnvironment[n]),"Startup environment drift; do not set or clear OMP_NUM_THREADS");
  using(var consumed=new FileStream(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,args[3]+".consumed"),FileMode.CreateNew,FileAccess.Write,FileShare.None)){consumed.Write(System.Text.Encoding.UTF8.GetBytes(System.Environment.ProcessId.ToString()));}
  Root=args[2];Directory.CreateDirectory(Root);Write("manifest-used",m);Write("startup-environment",EnvironmentNow());Write("run-slot",new{slot=args[3],pid=System.Environment.ProcessId,version=Version});Sampler=new Timer(_=>Sample(),null,0,50);
  string status="FAILED_NO_AUTOMATIC_RETRY";string? error=null;
  try{NativeIdentity(m.NativePath);Require(EnvNames.All(n=>EnvironmentNow()[n]==m.Environment[n]),"Post-binding environment differs from prior pair");Write("runtime",new{System.Environment.ProcessId,architecture=RuntimeInformation.ProcessArchitecture.ToString(),os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,System.Environment.ProcessorCount,System.Environment.CurrentDirectory,culture=System.Globalization.CultureInfo.CurrentCulture.Name,utcNow=DateTimeOffset.UtcNow,environment=EnvironmentNow(),nativeThreadPolicy="Original binding behavior unchanged; effective OMP checked after binding; actual worker count not instrumented"});await Task.Run(()=>Decode(m));Event("AllNativeOwnersDisposed");Require(HashFile(m.Input.Path)==m.Input.Sha256,"Input changed during run");Write("loaded-modules",Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(x=>x.FileName.StartsWith(AppContext.BaseDirectory,StringComparison.OrdinalIgnoreCase)||x.ModuleName.Contains("VCOMP",StringComparison.OrdinalIgnoreCase)||x.ModuleName.StartsWith("VCRUNTIME",StringComparison.OrdinalIgnoreCase)||x.ModuleName.StartsWith("MSVCP",StringComparison.OrdinalIgnoreCase)).Select(x=>new{x.ModuleName,x.FileName,sha256=HashFile(x.FileName)}).ToArray());status="OBSERVATION_COMPLETED_NOT_A_FIX";return 0;}
  catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);return 1;}
  finally{Sampler.Dispose();Sample();using var p=Process.GetCurrentProcess();p.Refresh();Write("result",new{version=Version,status,error,pid=System.Environment.ProcessId,elapsedMs=Clock.Elapsed.TotalMilliseconds,counts=Counts,peakWorkingSetOs=p.PeakWorkingSet64,peakPrivateBytesSampled=MaxPrivate,peakWorkingBytesSampled=MaxWorking,memorySampleMs=50,fullManagedPixelCopies=0,maxManagedPixelRowBytes=46512,appOrManagedFloatCalls=0});}
 }
 [StructLayout(LayoutKind.Sequential)]struct Mbi{public IntPtr BaseAddress,AllocationBase;public uint AllocationProtect;public ushort PartitionId;public UIntPtr RegionSize;public uint State,Protect,Type;}
 [DllImport("kernel32.dll")]static extern UIntPtr VirtualQuery(IntPtr p,out Mbi b,UIntPtr n);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 static void CheckReadable(IntPtr p,long bytes)
 {
  Require(p!=IntPtr.Zero&&bytes>0&&bytes<=1073741824,"Invalid pointer/range");long cursor=p.ToInt64(),end=checked(cursor+bytes);while(cursor<end){Require(VirtualQuery(new IntPtr(cursor),out var m,(UIntPtr)Marshal.SizeOf<Mbi>())!=(UIntPtr)0,"VirtualQuery failed");Require(m.State==0x1000&&(m.Protect&0x100)==0&&(m.Protect&0xff) is (0x02 or 0x04 or 0x08 or 0x20 or 0x40 or 0x80),"Unreadable range");long next=checked(m.BaseAddress.ToInt64()+(long)m.RegionSize.ToUInt64());Require(next>cursor,"Invalid region");cursor=next;}
 }
}

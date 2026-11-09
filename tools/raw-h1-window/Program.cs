using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
static class Program {
 static string Root="";static Dictionary<string,int> Calls=new();
 static readonly string[] EnvNames={"OMP_NUM_THREADS","OMP_DYNAMIC","OMP_THREAD_LIMIT","OMP_SCHEDULE","OMP_PROC_BIND","OMP_PLACES","KMP_AFFINITY","KMP_BLOCKTIME","OPENBLAS_NUM_THREADS","DOTNET_PROCESSOR_COUNT","COMPlus_ThreadPool_ForceMinWorkerThreads"};
 static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
 static Dictionary<string,string?> Env()=>EnvNames.ToDictionary(n=>n,n=>Environment.GetEnvironmentVariable(n));
 static void Check(bool b,string message){if(!b)throw new InvalidOperationException(message);}
 static void Write(string n,object v){using var f=new FileStream(Path.Combine(Root,n+".json"),FileMode.CreateNew);JsonSerializer.Serialize(f,v,new JsonSerializerOptions{WriteIndented=true});}
 static void Count(string n){Calls[n]=Calls.GetValueOrDefault(n)+1;Check(Calls[n]==1,"Each call at most once: "+n);}
 static object? Values(object? v){if(v is null)return null;if(v is IntPtr p)return "0x"+p.ToInt64().ToString("X");var t=v.GetType();if(v is string||t.IsPrimitive||v is decimal)return v;if(t.IsEnum)return Convert.ToInt64(v);if(v is Array a)return a.Cast<object?>().Select(Values).ToArray();var d=new SortedDictionary<string,object?>();foreach(var f in t.GetFields(BindingFlags.Public|BindingFlags.Instance))d[f.Name]=Values(f.GetValue(v));foreach(var p2 in t.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0&&!p.PropertyType.IsByRefLike&&!p.PropertyType.IsPointer))if(!d.ContainsKey(p2.Name))d[p2.Name]=Values(p2.GetValue(v));return d;}
 // Same processing assignments as fixed validation commit 4c322975; no quality/OMP override.
 static void Configure(OutputParams o){o.HalfSize=false;o.UseCameraWb=true;o.UseAutoWb=false;o.UseCameraMatrix=true;o.OutputColor=LibRawColorSpace.SRGB;o.OutputBps=16;o.OutputTiff=false;o.Interpolation=true;o.UserFlip=0;Write("configured-output",Values(o));}
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr Abi();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Test();
 public static int Main(string[]a){
  if(a.Length!=3||a[0] is not("--selftest" or "--once"))return 2;
  Root=a[2];Check(!Directory.Exists(Root),"Preserve result directory");Directory.CreateDirectory(Root);bool raw=a[0]=="--once";string status="FAILED_NO_RETRY";string? error=null;
  try{
   var cfg=JsonDocument.Parse(File.ReadAllText(a[1])).RootElement;
   foreach(var f in cfg.GetProperty("files").EnumerateArray())Check(Hash(f.GetProperty("path").GetString()!)==f.GetProperty("sha256").GetString(),"Frozen file drift");
   if(raw){var reservation=cfg.GetProperty("reservationPath").GetString()!;var ticket=JsonDocument.Parse(File.ReadAllText(reservation)).RootElement;Check(ticket.GetProperty("task").GetString()=="RAW-REOPEN-001-H1-WINDOW-20261109"&&ticket.GetProperty("maximum").GetInt32()==2&&ticket.GetProperty("hostManifestSha256").GetString()==Hash(a[1]),"Independent H1 reservation identity");using(var entered=new FileStream(reservation+".entered",FileMode.CreateNew)){} }
   Write("startup",new{Environment.ProcessId,environment=Env(),raw,observerSink=false});
   Check(RawContext.Version=="0.21.1-Release","Version");
   var nativePath=Path.GetFullPath(cfg.GetProperty("nativePath").GetString()!);var handle=NativeLibrary.Load(nativePath);Check(Hash(nativePath)=="305EEC498644F01EC707E8AC3E58A4B5670043D35FBC853626FF13AC6FF15484","Native identity");Check(Hash(typeof(RawContext).Assembly.Location)=="6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3","Binding identity");Check(Env()["OMP_NUM_THREADS"]=="6"&&EnvNames.Skip(1).All(n=>Env()[n]==null),"Preserve original effective environment");
   var abi=JsonDocument.Parse(Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<Abi>(NativeLibrary.GetExport(handle,"rr001i_abi"))())!).RootElement;
   Check(abi.GetProperty("dataBytes").GetInt32()==Marshal.SizeOf<LibRawData>()&&Marshal.SizeOf<LibRawData>()==381256&&abi.GetProperty("sizes").GetInt32()==8&&abi.GetProperty("image").GetInt32()==0&&abi.GetProperty("slotBytes").GetInt32()==8,"ABI identity");var actual=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>string.Equals(m.ModuleName,"raw_r.dll",StringComparison.OrdinalIgnoreCase));Check(actual.BaseAddress==handle&&Path.GetFullPath(actual.FileName).Equals(nativePath,StringComparison.OrdinalIgnoreCase),"Actual loaded native module");Write("loaded-native",new{path=actual.FileName,sha256=Hash(actual.FileName),abi,effectiveEnvironment=Env()});
   if(!raw){var o=new OutputParams();Configure(o);Check(!o.HalfSize&&o.UseCameraWb&&!o.UseAutoWb&&o.UseCameraMatrix&&o.OutputBps==16&&o.UserFlip==0,"Original processing defaults");Check(Marshal.GetDelegateForFunctionPointer<Test>(NativeLibrary.GetExport(handle,"rr001i_self_test"))()==0,"Native synthetic self-test");Count("Initialize");var context=new RawContext(LibRawNative.Initialize());Count("ManagedDispose");context.Dispose();Write("NO_RAW_SELF_TEST",new{status="PASS",zeroRaw=true,abi,counts=Calls,observerSinkInitialized=false});status="SELFTEST_COMPLETED";return 0;}
   var input=cfg.GetProperty("inputPath").GetString()!;Check(Hash(input)=="52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865","Authorized input identity");
   Task.Run(()=>{Count("OpenFile");using var c=RawContext.OpenFile(input);Count("Unpack");c.Unpack();Write("unpacked",new{c.RawWidth,c.RawHeight,decoder=c.DecoderInfo.DecoderName});Count("DcrawProcess");c.DcrawProcess(Configure);Count("MakeDcrawMemoryImage");using var image=c.MakeDcrawMemoryImage();Write("memory-layout",new{image.Width,image.Height,image.Channels,image.Bits,image.DataSize,pixelsNotCopiedOrCompared=true});}).GetAwaiter().GetResult();
   Check(Hash(input)=="52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865","Input unchanged");status="completed";return 0;
  }catch(Exception e){error=e.ToString();return 1;}
  finally{using var p=Process.GetCurrentProcess();p.Refresh();Write("result",new{status,error,counts=Calls,zeroRaw=!raw,Environment.ProcessId,environment=Env(),peakWorkingSetBytes=p.PeakWorkingSet64,fullFramePayloads=0,originalPixelParameters=true});}
 }
}

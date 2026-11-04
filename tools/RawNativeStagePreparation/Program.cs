using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
// Intentionally NO RAW/decode mode. This executable cannot spend a decode slot.
internal static class Program {
 const string BindingSha="6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3";
 static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
 static void Check(bool p,string message){if(!p)throw new InvalidDataException(message);}
 static readonly string[] Vars={"OMP_NUM_THREADS","OMP_DYNAMIC","OMP_THREAD_LIMIT","OMP_SCHEDULE","OMP_PROC_BIND","OMP_PLACES","KMP_AFFINITY","KMP_BLOCKTIME"};
 static object Env()=>Vars.ToDictionary(k=>k,Environment.GetEnvironmentVariable);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Abi();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int SelfTest();
 static T Fn<T>(IntPtr h,string n)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(h,n));
 static void Main(string[] args){
  Check(args.Length==3&&args[0]=="--self-test-no-raw","Only --self-test-no-raw <isolated-DLL> <NEW-receipt> is supported");
  string dll=Path.GetFullPath(args[1]),receipt=Path.GetFullPath(args[2]);Check(!File.Exists(receipt),"Preserve existing receipt");
  Check(OperatingSystem.IsWindows()&&Environment.Is64BitProcess&&BitConverter.IsLittleEndian,"win-x64 LE only");
  Check(Hash(typeof(RawContext).Assembly.Location)==BindingSha,"Unreviewed binding");
  Check(dll.Equals(Path.Combine(AppContext.BaseDirectory,"raw_r.dll"),StringComparison.OrdinalIgnoreCase),"DLL must be isolated beside this host as raw_r.dll");
  object before=Env();IntPtr native=NativeLibrary.Load(dll);
  // Use the original binding resolver, never register a second one.
  var abi=JsonDocument.Parse(Marshal.PtrToStringAnsi(Fn<Abi>(native,"rr001_abi")())!);
  var fields=new Dictionary<string,string>{{"image",nameof(LibRawData.Image)},{"sizes",nameof(LibRawData.ImageSizes)},{"idata",nameof(LibRawData.ImageParams)},{"color",nameof(LibRawData.Color)},{"params",nameof(LibRawData.OutputParams)},{"rawparams",nameof(LibRawData.RawUnpackParams)},{"rawdata",nameof(LibRawData.RawData)},{"parent",nameof(LibRawData.ParentClass)}};
  foreach(var (key,field) in fields)Check(abi.RootElement.GetProperty(key).GetInt32()==(int)Marshal.OffsetOf<LibRawData>(field),"ABI mismatch: "+key);
  Check(abi.RootElement.GetProperty("pointerBytes").GetInt32()==8,"Pointer ABI");
  Check(abi.RootElement.GetProperty("dataBytes").GetInt32()==Marshal.SizeOf<LibRawData>(),"Data size ABI");
  Check(Fn<SelfTest>(native,"rr001_self_test")()==0,"Native synthetic hash/exclusion test failed");
  Check(RawContext.Version=="0.21.1-Release","Native version");
  using(var c=new RawContext(LibRawNative.Initialize())){
    var h=c.UnsafeGetHandle();
    Check(LibRawNative.GetImageParameters(h)==IntPtr.Add(h,(int)Marshal.OffsetOf<LibRawData>(nameof(LibRawData.ImageParams))),"Accessor/context ABI");
    Check(Marshal.ReadIntPtr(h)==IntPtr.Zero,"No-RAW context unexpectedly has image");
  }
  var loaded=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(m=>m.BaseAddress==native).ToArray();
  Check(loaded.Length==1&&Path.GetFullPath(loaded[0].FileName).Equals(dll,StringComparison.OrdinalIgnoreCase),"Loaded path mismatch");
  // RawContext initializer may set OMP itself; this host never sets or clears it.
  var r=new{status="NO_RAW_NATIVE_SELF_TEST_COMPLETED_NOT_A_FIX",utc=DateTimeOffset.UtcNow,environmentBeforeBinding=before,environmentAfterBinding=Env(),os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,Environment.ProcessorCount,native=dll,nativeSha256=Hash(dll),bindingSha256=BindingSha,abi=abi.RootElement,counts=new{open=0,unpack=0,process=0,memoryImage=0,init=1},sameAsProduction=false};
  using var output=new FileStream(receipt,FileMode.CreateNew);JsonSerializer.Serialize(output,r,new JsonSerializerOptions{WriteIndented=true});
 }
}

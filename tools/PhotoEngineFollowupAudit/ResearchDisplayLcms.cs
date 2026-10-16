using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color.DisplayPreview;

/// <summary>Research-only LCMS binding, never loaded by the product or shipped in its Release.</summary>
internal sealed class ResearchDisplayLcms : IDisplayPreviewTransform, IDisposable
{
 private readonly IntPtr library;
 private readonly object gate=new();private bool disposed;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint VersionFn();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr OpenFn(IntPtr data,uint length);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CloseFn(IntPtr profile);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateFn(IntPtr source,uint sourceFormat,IntPtr target,uint targetFormat,uint intent,uint flags);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DeleteFn(IntPtr transform);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RunFn(IntPtr transform,IntPtr input,IntPtr output,uint count);
 private readonly OpenFn open;private readonly CloseFn close;private readonly CreateFn create;private readonly DeleteFn delete;private readonly RunFn run;
 private static readonly HashSet<string> Sources=["2B3AA1645779A9E634744FAF9B01E9102B0C9B88FD6DECED7934DF86B949AF7E"];
 private static readonly HashSet<string> Targets=[..Sources,"D1CA53DF625D4BB2EA73A6B1D541819DFBE1F9474DB621BF8C8FE331EC96A0D0","691FDDD2773EEF6F13032B0D34CA89B76E472676E494D06DA95D3B75BE806E57"];
 public string EngineIdentity=>"ResearchOnlyLCMS212:"+NativeSha;
 public const string NativeSha="87BF94F27F345384420383F247178F825C0101928FEDD3A4887FF60E9D4A76EB";
 public ResearchDisplayLcms(string native)
 {
  if(SonyProbe.Hash(native)!=NativeSha)throw new InvalidDataException("Unreviewed LCMS binary");
  library=NativeLibrary.Load(Path.GetFullPath(native));
  try
  {
   T Load<T>(string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
   if(Load<VersionFn>("cmsGetEncodedCMMversion")()!=2120)throw new InvalidDataException("Expected LCMS2.12");
   open=Load<OpenFn>("cmsOpenProfileFromMem");close=Load<CloseFn>("cmsCloseProfile");create=Load<CreateFn>("cmsCreateTransform");delete=Load<DeleteFn>("cmsDeleteTransform");run=Load<RunFn>("cmsDoTransform");
  }
  catch{NativeLibrary.Free(library);throw;}
 }
 public unsafe ushort[] Convert(ReadOnlySpan<ushort> input,ReadOnlySpan<byte> source,ReadOnlySpan<byte> destination,DisplayPreviewContract contract,CancellationToken token)
 {
  contract.Validate();token.ThrowIfCancellationRequested();
  if(input.Length==0||input.Length%3!=0)throw new InvalidDataException("RGB16 triplets required");
  string Hash(ReadOnlySpan<byte> b)=>System.Convert.ToHexString(SHA256.HashData(b));
  if(!Sources.Contains(Hash(source))||!Targets.Contains(Hash(destination)))throw new InvalidDataException("Profile content has not been admitted for this research run; no arbitrary ICC support");
  if(source.SequenceEqual(destination))return input.ToArray(); // Explicit same-profile identity, no lossy engine round-trip.
  var result=new ushort[input.Length];
  lock(gate)
  {
   ObjectDisposedException.ThrowIf(disposed,this);
   fixed(byte* sp=source)fixed(byte* dp=destination)fixed(ushort* ip=input)fixed(ushort* op=result)
   {
    var s=open((IntPtr)sp,(uint)source.Length);var d=open((IntPtr)dp,(uint)destination.Length);IntPtr t=IntPtr.Zero;
    try
    {
     if(s==IntPtr.Zero||d==IntPtr.Zero)throw new InvalidDataException("LCMS rejected profile");
     const uint rgb16=(4u<<16)|(3u<<3)|2u;
     t=create(s,rgb16,d,rgb16,1,0x140);if(t==IntPtr.Zero)throw new InvalidDataException("LCMS refused display transform");
     for(int pixel=0;pixel<input.Length/3;pixel+=4096){token.ThrowIfCancellationRequested();run(t,(IntPtr)(ip+pixel*3),(IntPtr)(op+pixel*3),(uint)Math.Min(4096,input.Length/3-pixel));}
     token.ThrowIfCancellationRequested();return result;
    }
    finally{if(t!=IntPtr.Zero)delete(t);if(d!=IntPtr.Zero)close(d);if(s!=IntPtr.Zero)close(s);}
   }
  }
 }
 public void Dispose(){lock(gate){if(!disposed){NativeLibrary.Free(library);disposed=true;}}}
}

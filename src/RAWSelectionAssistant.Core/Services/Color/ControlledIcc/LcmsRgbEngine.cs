using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
/// <summary>Product RGB adapter to the already shipped LCMS2.12 artifact. No new CMS math/parser.
/// Research ControlledIccLinearV1 gate remains unchanged. CPU float, intent relative, BPC off, no optimization.</summary>
public sealed class LcmsRgbEngine:IDisposable
{
 public const string Version="BundledLittleCms212ExactV1",ExpectedSha="87BF94F27F345384420383F247178F825C0101928FEDD3A4887FF60E9D4A76EB";
 private readonly IntPtr library;private readonly object lifetime=new();private bool disposed;private int borrowers;
 private void Borrow(){lock(lifetime){ObjectDisposedException.ThrowIf(disposed,this);borrowers++;}}
 private void Return(){lock(lifetime){borrowers--;if(disposed&&borrowers==0)NativeLibrary.Free(library);}}
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint V();
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Open(byte[] b,uint n);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Close(IntPtr p);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate uint Sig(IntPtr p);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Create(IntPtr a,uint af,IntPtr b,uint bf,uint intent,uint flags);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Delete(IntPtr p);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Do(IntPtr t,float[] a,float[] b,uint n);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Gamma(IntPtr context,double gamma);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Param(IntPtr context,int type,double[] parameters);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void FreeCurve(IntPtr c);
 [StructLayout(LayoutKind.Sequential)]private struct XyY{public double X,Y,L;public XyY(double x,double y){X=x;Y=y;L=1;}}
 [StructLayout(LayoutKind.Sequential)]private struct Primaries{public XyY R,G,B;}
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate IntPtr Rgb(ref XyY white,ref Primaries primaries,IntPtr[] curves);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Save(IntPtr profile,byte[]? bytes,ref uint size);
 private T Fn<T>(string n)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,n));
 public LcmsRgbEngine(string? file=null){file??=Path.Combine(AppContext.BaseDirectory,"lcms2.dll");if(!File.Exists(file)){var native=Path.Combine(AppContext.BaseDirectory,"runtimes","win-x64","native","lcms2.dll");if(File.Exists(native))file=native;}using var stream=File.OpenRead(file);if(Convert.ToHexString(SHA256.HashData(stream))!=ExpectedSha)throw new InvalidDataException("LCMS binary not evaluated for product RGB path");library=NativeLibrary.Load(Path.GetFullPath(file));if(Fn<V>("cmsGetEncodedCMMversion")()!=2120){NativeLibrary.Free(library);throw new InvalidDataException("Expected existing LCMS2.12");}}
 private IntPtr OpenProfile(byte[] b){RgbIccIdentityV1.FromBytes(b,"Validate native input");var p=Fn<Open>("cmsOpenProfileFromMem")(b,(uint)b.Length);if(p==IntPtr.Zero)throw new InvalidDataException("LCMS refused ICC");if(Fn<Sig>("cmsGetColorSpace")(p)!=0x52474220||Fn<Sig>("cmsGetDeviceClass")(p) is not(0x6D6E7472 or 0x73636E72)){Fn<Close>("cmsCloseProfile")(p);throw new NotSupportedException("RGB profiles only");}return p;}
 public void Validate(RgbIccIdentityV1 profile){Borrow();try{var p=OpenProfile(profile.Bytes());Fn<Close>("cmsCloseProfile")(p);}finally{Return();}}
 public sealed class Transform:IDisposable{private readonly LcmsRgbEngine engine;private readonly IntPtr a,b;private IntPtr t;internal Transform(LcmsRgbEngine engine,RgbIccIdentityV1 from,RgbIccIdentityV1 to){this.engine=engine;a=engine.OpenProfile(from.Bytes());try{b=engine.OpenProfile(to.Bytes());const uint rgbFloat=(1u<<22)|(4u<<16)|(3u<<3)|4;t=engine.Fn<Create>("cmsCreateTransform")(a,rgbFloat,b,rgbFloat,1,0x140);if(t==IntPtr.Zero)throw new InvalidDataException("ICC exact transform refused");}catch{if(b!=0)engine.Fn<Close>("cmsCloseProfile")(b);engine.Fn<Close>("cmsCloseProfile")(a);throw;}}
 public float[] Convert(ReadOnlySpan<float> values,CancellationToken token=default){ObjectDisposedException.ThrowIf(t==0,this);if(values.Length%3!=0||values.Length==0)throw new ArgumentException("RGB triples required");foreach(float x in values)if(!float.IsFinite(x))throw new InvalidDataException("Nonfinite color value");var result=new float[values.Length];for(int offset=0;offset<values.Length;offset+=12288){token.ThrowIfCancellationRequested();var input=values.Slice(offset,Math.Min(12288,values.Length-offset)).ToArray();var output=new float[input.Length];engine.Fn<Do>("cmsDoTransform")(t,input,output,(uint)(input.Length/3));for(int i=0;i<output.Length;i++){if(!float.IsFinite(output[i]))throw new InvalidDataException("ICC nonfinite output");result[offset+i]=output[i];}}return result;}
 public void Dispose(){if(t==0)return;engine.Fn<Delete>("cmsDeleteTransform")(t);t=0;engine.Fn<Close>("cmsCloseProfile")(b);engine.Fn<Close>("cmsCloseProfile")(a);engine.Return();}}
 public Transform CreateTransform(RgbIccIdentityV1 from,RgbIccIdentityV1 to){Borrow();try{return new(this,from,to);}catch{Return();throw;}}
 [StructLayout(LayoutKind.Sequential)]private struct Xyz{public double X,Y,Z;public Xyz(double x,double y,double z){X=x;Y=y;Z=z;}}
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Adapt(ref Xyz result,ref Xyz source,ref Xyz destination,ref Xyz value);
 public ColorVector3 AdaptWhite(ColorVector3 xyz,ColorVector3 sourceWhite,ColorVector3 destinationWhite){Borrow();try{var src=new Xyz(sourceWhite.X,sourceWhite.Y,sourceWhite.Z);var dst=new Xyz(destinationWhite.X,destinationWhite.Y,destinationWhite.Z);var input=new Xyz(xyz.X,xyz.Y,xyz.Z);var output=new Xyz();if(Fn<Adapt>("cmsAdaptToIlluminant")(ref output,ref src,ref dst,ref input)==0)throw new InvalidDataException("LCMS chromatic adaptation refused");return new(output.X,output.Y,output.Z);}finally{Return();}}
 public RgbIccIdentityV1 StandardProfile(string id)
 {
  ObjectDisposedException.ThrowIf(disposed,this);var white=new XyY(.3127,.3290);var primaries=new Primaries{R=new(.64,.33),G=new(.30,.60),B=new(.15,.06)};bool srgb=true;double gamma=1;
  switch(id){case "sRGB":break;case "DisplayP3":primaries=new(){R=new(.68,.32),G=new(.265,.69),B=new(.15,.06)};break;case "AdobeRGB":srgb=false;gamma=563d/256;primaries.G=new(.21,.71);break;case "LibRawProPhotoD65LinearV1":srgb=false;gamma=1;white=new(.3127,.3290);primaries=new(){R=new(.73511742,.26107185),G=new(.13835370,.87748421),B=new(.06859433,.01381262)};break;case "LinearProPhotoD50V2":case "ProPhotoRGB":srgb=false;gamma=1.8;white=new(.3457,.3585);primaries=new(){R=new(.7347,.2653),G=new(.1596,.8404),B=new(.0366,.0001)};break;default:throw new ArgumentException("Unknown standard RGB space");}
  var curve=id is "LinearProPhotoD50V2" or "LibRawProPhotoD65LinearV1"?Fn<Gamma>("cmsBuildGamma")(0,1):srgb?Fn<Param>("cmsBuildParametricToneCurve")(0,4,[2.4,1/1.055,.055/1.055,1/12.92,.04045]):id=="ProPhotoRGB"?Fn<Param>("cmsBuildParametricToneCurve")(0,4,[1.8,1,0,1/16d,.03125]):Fn<Gamma>("cmsBuildGamma")(0,gamma);if(curve==0)throw new InvalidDataException("ICC curve creation failed");IntPtr profile=0;
  try{profile=Fn<Rgb>("cmsCreateRGBProfile")(ref white,ref primaries,[curve,curve,curve]);if(profile==0)throw new InvalidDataException("ICC creation failed");uint size=0;Fn<Save>("cmsSaveProfileToMem")(profile,null,ref size);var bytes=new byte[size];if(Fn<Save>("cmsSaveProfileToMem")(profile,bytes,ref size)==0)throw new InvalidDataException("ICC serialization failed"); // stable date, no copyrighted Adobe profile copied
   byte[] date=[0x07,0xD0,0,1,0,1,0,0,0,0,0,0];date.CopyTo(bytes,24);Array.Clear(bytes,84,16);return RgbIccIdentityV1.FromBytes(bytes,id+" public-primary compatible / LCMS2.12");}
  finally{if(profile!=0)Fn<Close>("cmsCloseProfile")(profile);Fn<FreeCurve>("cmsFreeToneCurve")(curve);}
 }
 public void Dispose(){lock(lifetime){if(disposed)return;disposed=true;if(borrowers==0)NativeLibrary.Free(library);}}
}

using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public sealed class V3NumericalStageTests
{
 [StructLayout(LayoutKind.Sequential)]struct Description{public Guid Format;public uint Width,Height;}
 [StructLayout(LayoutKind.Sequential)]struct Plane{public Guid Format;public IntPtr Buffer;public uint Stride,Size;}
 [DllImport("ole32.dll",ExactSpelling=true)]static extern int CoCreateInstance(in Guid clsid,IntPtr outer,uint context,in Guid iid,out IntPtr value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int FromHandle(IntPtr s,IntPtr file,IntPtr vendor,uint options,out IntPtr decoder);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Frame(IntPtr s,uint index,out IntPtr frame);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Support(IntPtr s,ref uint w,ref uint h,uint transform,uint options,[In]Guid[] formats,[Out]Description[] descriptions,uint count,out int yes);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int CopyPlanes(IntPtr s,IntPtr rect,uint w,uint h,uint transform,uint options,[In]Plane[] planes,uint count);
 static T Method<T>(IntPtr s,int slot)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(s),slot*IntPtr.Size));
 static string Output=>Environment.GetEnvironmentVariable("KITAO_V3_NUMERIC_OUTPUT")??throw new InvalidOperationException("Explicit output required");
 static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
 static byte[] Dump(BitmapSource image,string name){var b=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var bytes=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(bytes,b.PixelWidth*4,0);File.WriteAllBytes(Path.Combine(Output,name+".bgra"),bytes);return bytes;}
 [TestMethod]public Task ExtractNativeColorAndOrientationStages()=>RunSta(async()=>{
  EnsureTestApplication();Directory.CreateDirectory(Output);
  var manifest=Environment.GetEnvironmentVariable("KITAO_V3_NUMERIC_INPUTS")??throw new InvalidOperationException("Explicit inputs required");
  var paths=JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifest))!;var results=new List<object>();
  using(var stream=new ColorContext(PixelFormats.Bgra32).OpenProfileStream())using(var file=File.Create(Path.Combine(Output,"wic-target.icc")))stream.CopyTo(file);
  foreach(var path in paths){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var header=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];var name=Path.GetFileNameWithoutExtension(path);BitmapSource native;
   if(Path.GetExtension(path).Equals(".jpg",StringComparison.OrdinalIgnoreCase))native=CanvasJpegNativePreview.Read(stream,header.PixelWidth,header.PixelHeight,200L*1024*1024,CancellationToken.None,out _,out _);else native=header;
   var decoded=Dump(native,name+"-native");var context=header.ColorContexts?.FirstOrDefault();
   if(context is not null){using var profile=context.OpenProfileStream();using var dest=File.Create(Path.Combine(Output,name+"-source.icc"));profile.CopyTo(dest);}
   BitmapSource converted=context is null?native:new ColorConvertedBitmap(native,context,new ColorContext(PixelFormats.Bgra32),PixelFormats.Bgra32);var afterIcc=Dump(converted,name+"-icc");
   int orientation=1;if(header.Metadata is BitmapMetadata md)foreach(var q in new[]{"/app1/ifd/{ushort=274}","/ifd/{ushort=274}"})try{if(md.GetQuery(q)is ushort v)orientation=v;}catch(NotSupportedException){}
   BitmapSource oriented=converted;if(orientation!=1){var matrix=orientation switch{2=>new Matrix(-1,0,0,1,0,0),3=>new Matrix(-1,0,0,-1,0,0),4=>new Matrix(1,0,0,-1,0,0),5=>new Matrix(0,1,1,0,0,0),6=>new Matrix(0,1,-1,0,0,0),7=>new Matrix(0,-1,-1,0,0,0),_=>new Matrix(0,-1,1,0,0,0)};oriented=new TransformedBitmap(converted,new MatrixTransform(matrix));}var afterOrientation=Dump(oriented,name+"-oriented");
   if((long)header.PixelWidth*header.PixelHeight>4194304){using var session=new CanvasPhotoPreviewSession();var item=new RAWSelectionAssistant.Core.Services.FreeCanvas.CanvasObject{SourcePath=path,ContentHash=Hash(File.ReadAllBytes(path)),Width=1400,Height=788};var product=session.Read(item,1400,788,new(),CancellationToken.None);CollectionAssert.AreEqual(afterOrientation,product.Pixels,"Observed stage pipeline must exactly match actual frozen v3 session");}
   object? planar=null;if(Path.GetExtension(path).Equals(".jpg",StringComparison.OrdinalIgnoreCase))planar=ExtractPlanes(stream,header.PixelWidth,header.PixelHeight,name);
   results.Add(new{name,sourceSha256=Hash(File.ReadAllBytes(path)),sourceWidth=header.PixelWidth,sourceHeight=header.PixelHeight,width=native.PixelWidth,height=native.PixelHeight,orientation,orientedWidth=oriented.PixelWidth,orientedHeight=oriented.PixelHeight,profileCount=header.ColorContexts?.Count??0,nativeHash=Hash(decoded),iccHash=Hash(afterIcc),orientedHash=Hash(afterOrientation),planar});
  }
  File.WriteAllText(Path.Combine(Output,"STAGES.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));await Task.CompletedTask;
 });
 static object ExtractPlanes(FileStream stream,int sourceWidth,int sourceHeight,string name){IntPtr factory=IntPtr.Zero,decoder=IntPtr.Zero,frame=IntPtr.Zero,planar=IntPtr.Zero;var buffers=new List<IntPtr>();try{
  Marshal.ThrowExceptionForHR(CoCreateInstance(new("cacaf262-9370-4615-a13b-9f5539da4c0a"),IntPtr.Zero,1,new("ec5ec8a9-c395-4314-9c77-54d7a935ff70"),out factory));stream.Position=0;Marshal.ThrowExceptionForHR(Method<FromHandle>(factory,5)(factory,stream.SafeFileHandle.DangerousGetHandle(),IntPtr.Zero,0,out decoder));Marshal.ThrowExceptionForHR(Method<Frame>(decoder,13)(decoder,0,out frame));var iid=new Guid("3AFF9CCE-BE95-4303-B927-E7D16FF4A613");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(frame,ref iid,out planar));
  var formats=new[]{new Guid("91B4DB54-2DF9-42F0-B449-2909BB3DF88E"),new Guid("1339F224-6BFE-4C3E-9302-E4F3A6D0CA2A"),new Guid("B8145053-2116-49F0-8835-ED844B205C51")};var records=new List<object>();
  foreach(uint options in new uint[]{0,1}){double scale=Math.Min(1,1024d/Math.Max(sourceWidth,sourceHeight));uint w=(uint)Math.Round(sourceWidth*scale),h=(uint)Math.Round(sourceHeight*scale);var descriptions=new Description[3];var rc=Method<Support>(planar,3)(planar,ref w,ref h,0,options,formats,descriptions,3,out var yes);if(rc<0||yes==0){records.Add(new{options,supported=false,rc});continue;}var planes=new Plane[3];for(int n=0;n<3;n++){int bytes=checked((int)(descriptions[n].Width*descriptions[n].Height));var ptr=Marshal.AllocHGlobal(bytes);buffers.Add(ptr);planes[n]=new(){Format=formats[n],Buffer=ptr,Stride=descriptions[n].Width,Size=(uint)bytes};}Marshal.ThrowExceptionForHR(Method<CopyPlanes>(planar,4)(planar,IntPtr.Zero,w,h,0,options,planes,3));var sizes=new List<object>();for(int n=0;n<3;n++){var bytes=new byte[planes[n].Size];Marshal.Copy(planes[n].Buffer,bytes,0,bytes.Length);File.WriteAllBytes(Path.Combine(Output,$"{name}-plane{n}-opt{options}.raw"),bytes);sizes.Add(new{n,width=descriptions[n].Width,height=descriptions[n].Height,sha256=Hash(bytes)});}records.Add(new{options,supported=true,width=w,height=h,planes=sizes});}return records;
 }catch(COMException e){return new{error=e.HResult};}finally{foreach(var p in buffers)Marshal.FreeHGlobal(p);if(planar!=IntPtr.Zero)Marshal.Release(planar);if(frame!=IntPtr.Zero)Marshal.Release(frame);if(decoder!=IntPtr.Zero)Marshal.Release(decoder);if(factory!=IntPtr.Zero)Marshal.Release(factory);}}
}

using System.IO;using System.Runtime.InteropServices;using System.Windows.Media;using System.Windows.Media.Imaging;
namespace PixelTart.Modules.AssetLibrary.FreeCanvas;
/// <summary>WIC native JPEG IDCT scale only. Never calls GetThumbnail/Preview or a full-frame CopyPixels.</summary>
internal static class CanvasJpegNativePreview
{
 [DllImport("ole32.dll",ExactSpelling=true)]static extern int CoCreateInstance(in Guid clsid,IntPtr outer,uint context,in Guid iid,out IntPtr value);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int FromHandle(IntPtr self,IntPtr file,IntPtr vendor,uint options,out IntPtr decoder);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int GetFrame(IntPtr self,uint index,out IntPtr frame);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ClosestSize(IntPtr self,ref uint width,ref uint height);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ClosestFormat(IntPtr self,ref Guid format);
 [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Copy(IntPtr self,IntPtr rect,uint width,uint height,ref Guid format,uint transform,uint stride,uint size,[Out]byte[] pixels);
 static T Method<T>(IntPtr self,int slot)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self),slot*IntPtr.Size));
 internal static BitmapSource Read(FileStream stream,int sourceWidth,int sourceHeight,long availableBytes,CancellationToken token,out int nativeWidth,out int nativeHeight)
 {
  IntPtr factory=IntPtr.Zero,decoder=IntPtr.Zero,frame=IntPtr.Zero,transform=IntPtr.Zero;nativeWidth=nativeHeight=0;
  try{
   token.ThrowIfCancellationRequested();Marshal.ThrowExceptionForHR(CoCreateInstance(new("cacaf262-9370-4615-a13b-9f5539da4c0a"),IntPtr.Zero,1,new("ec5ec8a9-c395-4314-9c77-54d7a935ff70"),out factory));
   stream.Position=0;Marshal.ThrowExceptionForHR(Method<FromHandle>(factory,5)(factory,stream.SafeFileHandle.DangerousGetHandle(),IntPtr.Zero,0,out decoder));Marshal.ThrowExceptionForHR(Method<GetFrame>(decoder,13)(decoder,0,out frame));
   var iid=new Guid("3b16811b-6a43-4ec9-b713-3d5a0c13b940");Marshal.ThrowExceptionForHR(Marshal.QueryInterface(frame,ref iid,out transform));
   double scale=Math.Min(1,1024d/Math.Max(sourceWidth,sourceHeight));uint w=(uint)Math.Max(1,Math.Round(sourceWidth*scale)),h=(uint)Math.Max(1,Math.Round(sourceHeight*scale));Marshal.ThrowExceptionForHR(Method<ClosestSize>(transform,4)(transform,ref w,ref h));
   if(w==0||h==0||w>3072||h>3072||(long)w*h>4194304||checked((long)w*h*32)>availableBytes)throw new InvalidDataException("照片的原生缩小尺寸超过本次预览预算；请缩小照片副本后重试。旧版本保留。");
   var format=new Guid("6fddc324-4e03-4bfe-b185-3d77768dc90c");Marshal.ThrowExceptionForHR(Method<ClosestFormat>(transform,5)(transform,ref format));if(format!=new Guid("6fddc324-4e03-4bfe-b185-3d77768dc90c"))throw new NotSupportedException("此JPEG不支持有界24位原生预览，未回退整图解码。");
   token.ThrowIfCancellationRequested();var bytes=new byte[checked((int)(w*h*3))];Marshal.ThrowExceptionForHR(Method<Copy>(transform,3)(transform,IntPtr.Zero,w,h,ref format,0,w*3,(uint)bytes.Length,bytes));token.ThrowIfCancellationRequested();
   nativeWidth=(int)w;nativeHeight=(int)h;var result=BitmapSource.Create((int)w,(int)h,96,96,PixelFormats.Bgr24,null,bytes,(int)w*3);result.Freeze();return result;
  }catch(COMException e){throw new NotSupportedException("这张照片无法使用有界JPEG预览；原图和旧版本未修改。",e);}
  finally{if(transform!=IntPtr.Zero)Marshal.Release(transform);if(frame!=IntPtr.Zero)Marshal.Release(frame);if(decoder!=IntPtr.Zero)Marshal.Release(decoder);if(factory!=IntPtr.Zero)Marshal.Release(factory);}
 }
}

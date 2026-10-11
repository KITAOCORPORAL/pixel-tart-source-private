using System.Runtime.InteropServices;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

/// <summary>Windows in-box WIC row encoders: JPEG95/4:4:4 and RGB16 PNG. No redistributed codec dependency.</summary>
internal static unsafe class ProfessionalWicRowEncoder
{
    [DllImport("ole32.dll")]private static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")]private static extern void CoUninitialize();
    [DllImport("ole32.dll")]private static extern int CoCreateInstance(in Guid clsid,IntPtr outer,uint context,in Guid iid,out IntPtr instance);
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]private static extern int SHCreateStreamOnFileEx(string path,uint mode,uint attributes,bool create,IntPtr template,out IntPtr stream);
    private static void Check(int hr)=>Marshal.ThrowExceptionForHR(hr);
    private static void* Method(IntPtr obj,int slot)=>((void**)*(void**)obj)[slot];
    private static void Release(IntPtr obj){if(obj!=IntPtr.Zero)((delegate* unmanaged[Stdcall]<IntPtr,uint>)Method(obj,2))(obj);}
    [StructLayout(LayoutKind.Sequential)]private struct Property{public uint Type;public ushort Vt,Cf;public uint Hint;public IntPtr Name;public Guid Clsid;}
    [StructLayout(LayoutKind.Explicit,Size=24)]private struct Variant{[FieldOffset(0)]public ushort Vt;[FieldOffset(8)]public float Float;[FieldOffset(8)]public byte Byte;}
    public static void Write(string path,ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token)
    {
        var size=ProfessionalRawPipeline.OrientedSize(master);
        WriteRows(path,size.Width,size.Height,95,(y,row)=>{var rgb=ProfessionalRawPipeline.RenderRegion(master,definition,0,y,size.Width,1,token).Rgb32.Span;for(var i=0;i<size.Width;i++){row[i*3]=Q(rgb[i*3+2]);row[i*3+1]=Q(rgb[i*3+1]);row[i*3+2]=Q(rgb[i*3]);}},token);
    }
    public static void WritePng(string path,ProfessionalRawMaster master,ProfessionalRawDefinition definition,CancellationToken token)
    {
        var size=ProfessionalRawPipeline.OrientedSize(master);
        WriteRows(path,size.Width,size.Height,95,(y,row)=>{var rgb=ProfessionalRawPipeline.RenderRegion(master,definition,0,y,size.Width,1,token).Rgb32.Span;for(var i=0;i<rgb.Length;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(i*2),(ushort)Math.Clamp(Math.Round(rgb[i]*65535),0,65535));},token,png:true);
    }
    public static void WriteBitmap(string path,System.Windows.Media.Imaging.BitmapSource bitmap,int quality,CancellationToken token,int dpi=96,bool png=false)
    {
        var converted=new System.Windows.Media.Imaging.FormatConvertedBitmap(bitmap,System.Windows.Media.PixelFormats.Bgr24,null,0);
        WriteRows(path,bitmap.PixelWidth,bitmap.PixelHeight,quality,(y,row)=>converted.CopyPixels(new System.Windows.Int32Rect(0,y,bitmap.PixelWidth,1),row,row.Length,0),token,png:png,highDepth:false,dpi:dpi);
    }
    private static byte Q(float v)=>(byte)Math.Clamp(Math.Round(v*255),0,255);
    private static void WriteRows(string path,int width,int height,int quality,Action<int,byte[]> fillRow,CancellationToken token,bool png=false,bool highDepth=true,int dpi=96)
    {
        if(File.Exists(path))throw new IOException("编码目的文件已存在，未覆盖。");
        var init=CoInitializeEx(IntPtr.Zero,0);if(init<0&&init!=unchecked((int)0x80010106))Check(init);
        IntPtr factory=0,encoder=0,frame=0,bag=0,stream=0,color=0;
        try
        {
            Check(CoCreateInstance(new("cacaf262-9370-4615-a13b-9f5539da4c0a"),0,1,new("ec5ec8a9-c395-4314-9c77-54d7a935ff70"),out factory));
            var format=png?new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf"):new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057"); // GUID_ContainerFormatJpeg
            Check(((delegate* unmanaged[Stdcall]<IntPtr,Guid*,IntPtr,IntPtr*,int>)Method(factory,8))(factory,&format,0,&encoder));
            Check(SHCreateStreamOnFileEx(path,0x1011,0x80,true,0,out stream));
            Check(((delegate* unmanaged[Stdcall]<IntPtr,IntPtr,uint,int>)Method(encoder,3))(encoder,stream,2));
            Check(((delegate* unmanaged[Stdcall]<IntPtr,IntPtr*,IntPtr*,int>)Method(encoder,10))(encoder,&frame,&bag));
            if(!png){Set(bag,"ImageQuality",new Variant{Vt=4,Float=quality/100f});Set(bag,"JpegYCrCbSubsampling",new Variant{Vt=17,Byte=3});}
            Check(((delegate* unmanaged[Stdcall]<IntPtr,IntPtr,int>)Method(frame,3))(frame,bag));

            Check(((delegate* unmanaged[Stdcall]<IntPtr,uint,uint,int>)Method(frame,4))(frame,(uint)width,(uint)height));
            Check(((delegate* unmanaged[Stdcall]<IntPtr,double,double,int>)Method(frame,5))(frame,dpi,dpi));
            var pixels=new Guid(png&&highDepth?"6fddc324-4e03-4bfe-b185-3d77768dc915":"6fddc324-4e03-4bfe-b185-3d77768dc90c");var wanted=pixels;
            Check(((delegate* unmanaged[Stdcall]<IntPtr,Guid*,int>)Method(frame,6))(frame,&pixels));if(pixels!=wanted)throw new NotSupportedException("WIC拒绝请求的RGB8/RGB16行格式，未降位深回退。");
            Check(((delegate* unmanaged[Stdcall]<IntPtr,IntPtr*,int>)Method(factory,15))(factory,&color));var profile=StudioQuickExport.SrgbProfileBytes();
            fixed(byte* p=profile)Check(((delegate* unmanaged[Stdcall]<IntPtr,byte*,uint,int>)Method(color,4))(color,p,(uint)profile.Length));
            Check(((delegate* unmanaged[Stdcall]<IntPtr,uint,IntPtr*,int>)Method(frame,7))(frame,1,&color));
            var row=new byte[width*3*(png&&highDepth?2:1)];
            for(var y=0;y<height;y++)
            {
                token.ThrowIfCancellationRequested();fillRow(y,row);
                fixed(byte* p=row)Check(((delegate* unmanaged[Stdcall]<IntPtr,uint,uint,uint,byte*,int>)Method(frame,10))(frame,1,(uint)row.Length,(uint)row.Length,p));
            }
            Check(((delegate* unmanaged[Stdcall]<IntPtr,int>)Method(frame,12))(frame));Check(((delegate* unmanaged[Stdcall]<IntPtr,int>)Method(encoder,11))(encoder));token.ThrowIfCancellationRequested();
        }
        finally{Release(color);Release(bag);Release(frame);Release(encoder);Release(stream);Release(factory);if(init>=0)CoUninitialize();}
    }
    private static void Set(IntPtr bag,string name,Variant value)
    {var text=Marshal.StringToCoTaskMemUni(name);try{var property=new Property{Name=text,Vt=value.Vt};Check(((delegate* unmanaged[Stdcall]<IntPtr,uint,Property*,Variant*,int>)Method(bag,4))(bag,1,&property,&value));}finally{Marshal.FreeCoTaskMem(text);}}
}

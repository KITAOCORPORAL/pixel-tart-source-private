using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record ProfessionalRawDefinition(int ProcessingVersion=4,string WhiteBalance="Camera",double RedGain=1,double GreenGain=1,double BlueGain=1,double ExposureEv=0,
    string InputConfiguration="LibRaw021LinearProPhotoD65V1",string DisplayTransform="LuminanceShoulderNeutralGamutV1")
{
    public ProfessionalRawDefinition Validate()
    {
        if(ProcessingVersion!=4||InputConfiguration!="LibRaw021LinearProPhotoD65V1"||DisplayTransform!="LuminanceShoulderNeutralGamutV1"||WhiteBalance is not("Camera" or "CameraGains"))throw new InvalidDataException("不支持的 RAW 处理定义。");
        if(new[]{RedGain,GreenGain,BlueGain}.Any(x=>!double.IsFinite(x)||x<.125||x>8)||!double.IsFinite(ExposureEv)||ExposureEv is < -10 or > 10)throw new InvalidDataException("RAW 增益或曝光越界。");return this;
    }
    public string DecodeKey=>FormattableString.Invariant($"{InputConfiguration}:{WhiteBalance}:{(WhiteBalance=="Camera"?1:RedGain):R}:{(WhiteBalance=="Camera"?1:GreenGain):R}:{(WhiteBalance=="Camera"?1:BlueGain):R}");
}
public sealed record ProfessionalRawContract(string DecoderVersion,string Make,string Model,string SourceSha256,string DecodeKey,
    double[] CameraWhiteBalance,double[] AppliedWhiteBalance,double[] CameraToRgbMatrix,int WhiteLevel,
    string BlackLevelPolicy="LibRaw metadata: user_black=-1, user_cblack=-1000001 (all channels)",string OutputSpace="LibRaw Linear ProPhoto D65",int OutputBits=16,
    bool WhiteBalanceApplied=true,bool CameraMatrixApplied=true,bool AutoBright=false,string Gamma="1,1",int HighlightMode=0,
    bool DecoderClipped=true,int SaturatedChannelSamples=0,string Characterization="LibRaw embedded/builtin camera matrix; no proprietary profile")
{
    public string? UnpackParametersJson { get; init; }
    public string? OutputParametersJson { get; init; }
    public uint BlackLevel { get; init; }
    public uint[] ChannelBlackLevels { get; init; } = [];
    public uint BlackPatternRows { get; init; }
    public uint BlackPatternColumns { get; init; }
    public string? BlackPatternSha256 { get; init; }
    public double[] NormalizedWhiteBalance { get; init; } = [];
    public int ZeroChannelSamples { get; init; }
    public string DecoderWarnings { get; init; } = "None";
    public string WhiteBalanceSource { get; init; } = "Metadata cam_mul; explicit user_mul, auto/camera flags off";
    public string Demosaic { get; init; } = "AHD (user_qual=3), three-color Bayer only";
    public string ClippingPolicy { get; init; } = "LibRaw black subtraction, scale_colors and convert_to_rgb clip to uint16; no lost signal recovery";
}
public sealed record ProfessionalRawMaster(HighBitDepthImageBuffer Image,ProfessionalRawContract Contract);
public static class ProfessionalRawPipeline
{
    public const string WorkingSpace = "LibRaw Linear ProPhoto D65";
    // Inverse of LibRaw 0.21.1 colorconst.cpp prophoto_rgb (linear sRGB D65 -> ProPhoto D65).
    // This is an output-space definition, never a replacement camera profile.
    private static readonly ColorMatrix3x3 ToLinearSrgb=Invert(new(.529317,.330092,.140588,.098368,.873465,.028169,.016879,.117663,.865457));
    private static ColorMatrix3x3 Invert(ColorMatrix3x3 m)
    {
        var a=m.M11;var b=m.M12;var c=m.M13;var d=m.M21;var e=m.M22;var f=m.M23;var g=m.M31;var h=m.M32;var i=m.M33;var det=a*(e*i-f*h)-b*(d*i-f*g)+c*(d*h-e*g);
        return new((e*i-f*h)/det,(c*h-b*i)/det,(b*f-c*e)/det,(f*g-d*i)/det,(a*i-c*g)/det,(c*d-a*f)/det,(d*h-e*g)/det,(b*g-a*h)/det,(a*e-b*d)/det);
    }
    public static ColorVector3 ApplyExposure(ColorVector3 value,double ev)
    {if(!double.IsFinite(ev)||ev is < -10 or > 10)throw new ArgumentOutOfRangeException(nameof(ev));var gain=Math.Pow(2,ev);return new(value.X*gain,value.Y*gain,value.Z*gain);}
    public static ColorVector3 Display(ColorVector3 working)
    {
        var linear=ToLinearSrgb.Transform(working);
        // Fixed luminance shoulder preserves ratios before a neutral-axis gamut compression.
        var y=.2126*linear.X+.7152*linear.Y+.0722*linear.Z;
        if(y<=0)return new(0,0,0);
        var mapped=y<=.75?y:.75+.25*(1-Math.Exp(-(y-.75)/.25));var scale=mapped/y;
        Span<double> rgb=stackalloc double[]{linear.X*scale,linear.Y*scale,linear.Z*scale};var chroma=1d;
        foreach(var c in rgb){var delta=c-mapped;if(delta>0)chroma=Math.Min(chroma,(1-mapped)/delta);else if(delta<0)chroma=Math.Min(chroma,-mapped/delta);}
        double Encode(double c){var x=Math.Clamp(mapped+(c-mapped)*chroma,0,1);return x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055;}
        return new(Encode(rgb[0]),Encode(rgb[1]),Encode(rgb[2]));
    }
    public static HighBitDepthImageBuffer Render(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int maximumEdge=0,CancellationToken token=default)
    {
        definition.Validate();if(master.Contract.DecodeKey!=definition.DecodeKey)throw new InvalidDataException("白平衡定义与 RAW master 不一致，需要重新解码。");
        if(!master.Contract.WhiteBalanceApplied || !master.Contract.CameraMatrixApplied || master.Contract.AutoBright || master.Contract.Gamma!="1,1" || master.Contract.OutputBits!=16 || master.Contract.OutputSpace!=WorkingSpace || master.Image.WorkingColorSpace!=WorkingSpace)throw new InvalidDataException("RAW master 不是已定义的线性输入域。");
        var image=master.Image;var scale=maximumEdge>0?Math.Min(1,maximumEdge/(double)Math.Max(image.Width,image.Height)):1;
        var width=Math.Max(1,(int)Math.Round(image.Width*scale));var height=Math.Max(1,(int)Math.Round(image.Height*scale));var output=new float[checked(width*height*3)];var input=image.Rgb32.Span;
        for(var y=0;y<height;y++){token.ThrowIfCancellationRequested();var sy=Math.Min(image.Height-1,(int)((y+.5)*image.Height/height));for(var x=0;x<width;x++){var sx=Math.Min(image.Width-1,(int)((x+.5)*image.Width/width));var i=(sy*image.Width+sx)*3;if(!float.IsFinite(input[i])||!float.IsFinite(input[i+1])||!float.IsFinite(input[i+2]))throw new InvalidDataException("RAW master 包含非有限值。");var value=Display(ApplyExposure(new(input[i],input[i+1],input[i+2]),definition.ExposureEv));var j=(y*width+x)*3;output[j]=(float)value.X;output[j+1]=(float)value.Y;output[j+2]=(float)value.Z;}}
        return new(width,height,output,"16","sRGB",image.Orientation,image.Metadata);
    }
    public static void ValidateMaster(ProfessionalRawMaster master,ProfessionalRawDefinition definition)
    {
        definition.Validate();
        if(master.Contract.DecodeKey!=definition.DecodeKey||master.Image.WorkingColorSpace!=WorkingSpace||!master.Contract.WhiteBalanceApplied||!master.Contract.CameraMatrixApplied||master.Contract.Gamma!="1,1"||master.Contract.AutoBright||master.Contract.OutputBits!=16||master.Contract.OutputSpace!=WorkingSpace)throw new InvalidDataException("RAW master/definition domain mismatch.");
    }
    public static ColorVector3 RenderPixel(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int x,int y)
    {
        var input=master.Image.Rgb32.Span;var i=(y*master.Image.Width+x)*3;
        if(!float.IsFinite(input[i])||!float.IsFinite(input[i+1])||!float.IsFinite(input[i+2]))throw new InvalidDataException("Non-finite RAW pixel.");
        return Display(ApplyExposure(new(input[i],input[i+1],input[i+2]),definition.ExposureEv));
    }
    public static void FillRgb16Row(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int y,Memory<byte> row)
    {
        var bytes=row.Span;for(var x=0;x<master.Image.Width;x++){var v=RenderPixel(master,definition,x,y);var i=x*6;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes[i..],Quantize(v.X));System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes[(i+2)..],Quantize(v.Y));System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes[(i+4)..],Quantize(v.Z));}
    }
    // Cast to float matches historical full Render -> ToRgb48 exactly, not a new tone algorithm.
    private static ushort Quantize(double value)=>(ushort)Math.Clamp(Math.Round((float)value*65535),0,65535);
    private static double DecodeDisplay(double v)=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);
    private static double EncodeDisplay(double v)=>v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;
    public static HighBitDepthImageBuffer RenderPreview(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int maximumEdge=1600,CancellationToken token=default)
    {
        if(maximumEdge<=0)throw new ArgumentOutOfRangeException(nameof(maximumEdge));ValidateMaster(master,definition);var image=master.Image;var scale=Math.Min(1,maximumEdge/(double)Math.Max(image.Width,image.Height));
        var width=Math.Max(1,(int)Math.Round(image.Width*scale));var height=Math.Max(1,(int)Math.Round(image.Height*scale));
        if(scale==1)return Render(master,definition,token:token);
        var output=new float[checked(width*height*3)];var ratioX=image.Width/(double)width;var ratioY=image.Height/(double)height;
        // Exact box-footprint integration in display-linear sRGB, after the SAME per-pixel full display transform.
        // Nonlinear tone/gamut must not be applied after averaging scene values: that would not match the full reference.
        Parallel.For(0,height,new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},y=>
        {
            token.ThrowIfCancellationRequested();var ya=y*ratioY;var yb=(y+1)*ratioY;
            for(var x=0;x<width;x++)
            {
                var xa=x*ratioX;var xb=(x+1)*ratioX;double r=0,g=0,b=0;
                for(var sy=(int)ya;sy<Math.Min(image.Height,(int)Math.Ceiling(yb));sy++)for(var sx=(int)xa;sx<Math.Min(image.Width,(int)Math.Ceiling(xb));sx++)
                {var weight=(Math.Min(xb,sx+1)-Math.Max(xa,sx))*(Math.Min(yb,sy+1)-Math.Max(ya,sy));var value=RenderPixel(master,definition,sx,sy);r+=DecodeDisplay((float)value.X)*weight;g+=DecodeDisplay((float)value.Y)*weight;b+=DecodeDisplay((float)value.Z)*weight;}
                var area=ratioX*ratioY;var i=(y*width+x)*3;output[i]=(float)EncodeDisplay(r/area);output[i+1]=(float)EncodeDisplay(g/area);output[i+2]=(float)EncodeDisplay(b/area);
            }
        });
        return new(width,height,output,"16","sRGB",image.Orientation);
    }
    public static (int Width,int Height) OrientedSize(ProfessionalRawMaster master)=>master.Image.Orientation is >=5 and <=8?(master.Image.Height,master.Image.Width):(master.Image.Width,master.Image.Height);
    public static HighBitDepthImageBuffer RenderRegion(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int left,int top,int width,int height,CancellationToken token=default)
    {
        ValidateMaster(master,definition);var size=OrientedSize(master);
        if(left<0||top<0||width<=0||height<=0||left+width>size.Width||top+height>size.Height)throw new ArgumentOutOfRangeException(nameof(left));
        var output=new float[checked(width*height*3)];var w=master.Image.Width;var h=master.Image.Height;
        for(var y=0;y<height;y++){token.ThrowIfCancellationRequested();for(var x=0;x<width;x++){
            var ox=left+x;var oy=top+y;var (sx,sy)=master.Image.Orientation switch{2=>(w-1-ox,oy),3=>(w-1-ox,h-1-oy),4=>(ox,h-1-oy),5=>(oy,ox),6=>(oy,h-1-ox),7=>(w-1-oy,h-1-ox),8=>(w-1-oy,ox),_=>(ox,oy)};
            var value=RenderPixel(master,definition,sx,sy);var i=(y*width+x)*3;output[i]=(float)value.X;output[i+1]=(float)value.Y;output[i+2]=(float)value.Z;}}
        return new(width,height,output,"16","sRGB",1);
    }}

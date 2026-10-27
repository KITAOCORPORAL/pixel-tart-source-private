using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record ProfessionalRawDefinition(int ProcessingVersion=4,string WhiteBalance="Camera",double RedGain=1,double GreenGain=1,double BlueGain=1,double ExposureEv=0,
    string InputConfiguration="LibRaw021LinearProPhotoD65V1",string DisplayTransform="LuminanceShoulderNeutralGamutV1")
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public InstantFilmDefinition? InstantFilm {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public InstantInputRecipe? InstantInput {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RAWSelectionAssistant.Core.Services.Color.PhotoWideV1.RenderedSrgbExposure? SrgbExposure {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RenderedInputVersion {get;init;}
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsRenderedInput => InputConfiguration is "RenderedSrgbFloatV1" or "LegacyRawEditsSrgbFloatV1";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LightPrintDefinition? LightPrint {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProfessionalRawLocalEdits? LocalEdits {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ProfessionalRawGeometry? Geometry {get;init;}
    public ProfessionalRawDefinition Validate()
    {
        InstantFilm?.Validate();
        if(LocalEdits is not null||Geometry is not null){if(ProcessingVersion!=4||IsRenderedInput||InstantFilm?.Enabled==true)throw new NotSupportedException("RAW local extension v1 requires linear PV4 base without an instant-paper recipe");LocalEdits?.Freeze();Geometry?.Validate();}
        if(LightPrint is{} light){if(light.Version!=1||InstantFilm?.AlgorithmVersion!=3||!LightPrintFrames.Available.Any(f=>f.Id==light.FrameId&&f.Shippable))throw new InvalidDataException("Unknown/non-distributable light-print frame");light.WindowAdjustments?.Normalize();if(light.WindowAdjustments?.Nodes.Any(n=>n.Type is not(ColorStudioNodeType.BasicTone or ColorStudioNodeType.WhiteBalance or ColorStudioNodeType.Curve))==true)throw new NotSupportedException("Unsupported lightprint window node");}
        if(RenderedInputVersion is not null&&(RenderedInputVersion!=2||InputConfiguration!="RenderedSrgbFloatV1"))throw new InvalidDataException("Unknown rendered input version");
        if(SrgbExposure is{} exposure){exposure.Validate();if(InputConfiguration!="RenderedSrgbFloatV1"||InstantFilm is not null)throw new InvalidDataException("Rendered exposure does not accept scene RAW or mixed V3 chain");}
        if(InstantInput is {} input)
        {
            if(InputConfiguration=="LegacyRawEditsSrgbFloatV1")LegacyRawInstantInput.ValidateRecipe(input);
            else if(input.Version!=1||input.LegacyRaw is not null||input.Mode is not("Original" or "CurrentEdits")||InputConfiguration!="RenderedSrgbFloatV1")throw new InvalidDataException("Unknown instant input recipe.");
            input.Stack?.Normalize();input.Film?.Validate();
        }
        if(IsRenderedInput)
        {
            var raw=InputConfiguration=="LegacyRawEditsSrgbFloatV1";
            if(ProcessingVersion!=(raw?6:5)||DisplayTransform!=(raw?"LegacyRawEditsIdentityV1":"RenderedSrgbIdentityV1")||WhiteBalance!="Rendered"||ExposureEv!=0||RedGain!=1||GreenGain!=1||BlueGain!=1||raw&&InstantInput?.LegacyRaw is null)
                throw new InvalidDataException("Rendered input rejects repeated RAW gains, exposure and display transforms.");
            return this;
        }
        if(ProcessingVersion!=4||InputConfiguration!="LibRaw021LinearProPhotoD65V1"||DisplayTransform!="LuminanceShoulderNeutralGamutV1"||WhiteBalance is not("Camera" or "CameraGains"))throw new InvalidDataException("不支持的 RAW 处理定义。");
        if(new[]{RedGain,GreenGain,BlueGain}.Any(x=>!double.IsFinite(x)||x<.125||x>8)||!double.IsFinite(ExposureEv)||ExposureEv is < -10 or > 10)throw new InvalidDataException("RAW 增益或曝光越界。");return this;
    }
    public string DecodeKey=>FormattableString.Invariant($"{InputConfiguration}:{WhiteBalance}:{(WhiteBalance=="Camera"?1:RedGain):R}:{(WhiteBalance=="Camera"?1:GreenGain):R}:{(WhiteBalance=="Camera"?1:BlueGain):R}") + (RenderedInputVersion is null?"":":rasterInput2") + (InstantInput is null ? "" : ":recipe:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(InstantInput)))));
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
        if(ProfessionalRawLocalRenderer.Active(definition))return ProfessionalRawLocalRenderer.Preview(master,definition,maximumEdge,token);
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))return Kitao.InstantFilm.Research.InstantV3ProductPipeline.Preview(master,definition,maximumEdge>0?maximumEdge:Math.Max(OutputSize(master,definition).Width,OutputSize(master,definition).Height),token);
        if(definition.InstantFilm?.Enabled==true||definition.IsRenderedInput)return RenderOutput(master,definition,maximumEdge,token);
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
        if(definition.IsRenderedInput){if(master.Image.WorkingColorSpace!="sRGB"||master.Contract.DecodeKey!=definition.DecodeKey)throw new InvalidDataException("已渲染输入域失配。");return;}
        if(master.Contract.DecodeKey!=definition.DecodeKey||master.Image.WorkingColorSpace!=WorkingSpace||!master.Contract.WhiteBalanceApplied||!master.Contract.CameraMatrixApplied||master.Contract.Gamma!="1,1"||master.Contract.AutoBright||master.Contract.OutputBits!=16||master.Contract.OutputSpace!=WorkingSpace)throw new InvalidDataException("RAW master/definition domain mismatch.");
    }
    public static ColorVector3 RenderPixel(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int x,int y)
    {
        var input=master.Image.Rgb32.Span;var i=(y*master.Image.Width+x)*3;
        if(!float.IsFinite(input[i])||!float.IsFinite(input[i+1])||!float.IsFinite(input[i+2]))throw new InvalidDataException("Non-finite RAW pixel.");
        var encoded=new ColorVector3(input[i],input[i+1],input[i+2]);return definition.IsRenderedInput?(definition.SrgbExposure is{} exposure?exposure.Apply(encoded):encoded):Display(ApplyExposure(encoded,definition.ExposureEv));
    }
    public static (int Width,int Height) OutputSize(ProfessionalRawMaster master,ProfessionalRawDefinition definition){if(ProfessionalRawLocalRenderer.Active(definition))return ProfessionalRawLocalRenderer.Size(master,definition);if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))return Kitao.InstantFilm.Research.InstantV3ProductPipeline.Size(master,definition);var size=OrientedSize(master);var layout=InstantFilmPipeline.Layout(size.Width,size.Height,definition.InstantFilm);return(layout.Width,layout.Height);}
    public static int OutputOrientation(ProfessionalRawMaster master,ProfessionalRawDefinition definition)=>ProfessionalRawLocalRenderer.Active(definition)||definition.InstantFilm?.Enabled==true||definition.IsRenderedInput?1:master.Image.Orientation;
    public static ColorVector3 RenderOutputPixel(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int x,int y)
    {
        if(ProfessionalRawLocalRenderer.Active(definition)){using var pixel=ProfessionalRawLocalRenderer.Region(master,definition,x,y,1,1);var rgb=pixel.Rgb32.Span;return new(rgb[0],rgb[1],rgb[2]);}
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))throw new NotSupportedException("V3 spatial output requires bounded region/strip session, not pointwise export");
        var size=OrientedSize(master);
        return RenderOutputPixel(master,definition,InstantPaperLayout.Create(size.Width,size.Height,definition.InstantFilm),x,y);
    }
    private static ColorVector3 RenderOutputPixel(ProfessionalRawMaster master,ProfessionalRawDefinition definition,InstantPaperLayout layout,int x,int y)
    {
        var original=OrientedSize(master);
        if(!layout.TrySource(x,y,out var sx,out var sy))return InstantFilmPipeline.Paper(x/(double)layout.Width,y/(double)layout.Height,definition.InstantFilm);
        var w=master.Image.Width;var h=master.Image.Height;var(px,py)=master.Image.Orientation switch{2=>(w-1-sx,sy),3=>(w-1-sx,h-1-sy),4=>(sx,h-1-sy),5=>(sy,sx),6=>(sy,h-1-sx),7=>(w-1-sy,h-1-sx),8=>(w-1-sy,sx),_=>(sx,sy)};
        var value=RenderPixel(master,definition,px,py);return definition.InstantFilm is {} film?InstantFilmPipeline.Apply(value,(sx+.5)/original.Width,(sy+.5)/original.Height,film):value;
    }
    public static HighBitDepthImageBuffer RenderOutput(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int maxEdge=0,CancellationToken token=default)
    {
        if(ProfessionalRawLocalRenderer.Active(definition))return ProfessionalRawLocalRenderer.Preview(master,definition,maxEdge,token);
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))return Kitao.InstantFilm.Research.InstantV3ProductPipeline.Preview(master,definition,maxEdge>0?maxEdge:Math.Max(OutputSize(master,definition).Width,OutputSize(master,definition).Height),token);
        ValidateMaster(master,definition);var size=OutputSize(master,definition);var scale=maxEdge>0?Math.Min(1,maxEdge/(double)Math.Max(size.Width,size.Height)):1;var width=Math.Max(1,(int)Math.Round(size.Width*scale));var height=Math.Max(1,(int)Math.Round(size.Height*scale));var data=new float[width*height*3];
        var sourceSize=OrientedSize(master);var preparedLayout=InstantPaperLayout.Create(sourceSize.Width,sourceSize.Height,definition.InstantFilm);
        for(var y=0;y<height;y++){token.ThrowIfCancellationRequested();for(var x=0;x<width;x++){var v=RenderOutputPixel(master,definition,preparedLayout,(int)((x+.5)*size.Width/width),(int)((y+.5)*size.Height/height));var i=(y*width+x)*3;data[i]=(float)v.X;data[i+1]=(float)v.Y;data[i+2]=(float)v.Z;}}return new(width,height,data,"16","sRGB",1);
    }
    public static void FillOutputRgb16Row(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int y,Memory<byte> row){if(ProfessionalRawLocalRenderer.Active(definition)){using var pixels=ProfessionalRawLocalRenderer.Region(master,definition,0,y,OutputSize(master,definition).Width,1);for(int i=0;i<pixels.Rgb32.Length;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i*2)..],Quantize(pixels.Rgb32.Span[i]));return;}var sourceSize=OrientedSize(master);var preparedLayout=InstantPaperLayout.Create(sourceSize.Width,sourceSize.Height,definition.InstantFilm);var width=OutputSize(master,definition).Width;for(var x=0;x<width;x++){var v=RenderOutputPixel(master,definition,preparedLayout,x,y);var i=x*6;System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[i..],Quantize(v.X));System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i+2)..],Quantize(v.Y));System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.Span[(i+4)..],Quantize(v.Z));}}
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
        if(ProfessionalRawLocalRenderer.Active(definition))return ProfessionalRawLocalRenderer.Preview(master,definition,maximumEdge,token);
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))return Kitao.InstantFilm.Research.InstantV3ProductPipeline.Preview(master,definition,maximumEdge,token);
        if(maximumEdge<=0)throw new ArgumentOutOfRangeException(nameof(maximumEdge));ValidateMaster(master,definition);var image=master.Image;var transformed=definition.InstantFilm?.Enabled==true||definition.IsRenderedInput;var outputSize=transformed?OutputSize(master,definition):(Width:image.Width,Height:image.Height);var scale=Math.Min(1,maximumEdge/(double)Math.Max(outputSize.Width,outputSize.Height));
        var sourceSize=OrientedSize(master);var preparedLayout=InstantPaperLayout.Create(sourceSize.Width,sourceSize.Height,definition.InstantFilm);
        var width=Math.Max(1,(int)Math.Round(outputSize.Width*scale));var height=Math.Max(1,(int)Math.Round(outputSize.Height*scale));
        if(scale==1)return Render(master,definition,token:token);
        var output=new float[checked(width*height*3)];var ratioX=outputSize.Width/(double)width;var ratioY=outputSize.Height/(double)height;
        // Exact box-footprint integration in display-linear sRGB, after the SAME per-pixel full display transform.
        // Nonlinear tone/gamut must not be applied after averaging scene values: that would not match the full reference.
        // Contiguous bands reuse the boundary row between adjacent output footprints.
        // Summation order within each destination pixel is unchanged from the reference.
        Parallel.For(0,Math.Min(4,height),new ParallelOptions{CancellationToken=token,MaxDegreeOfParallelism=4},band=>
        {
            var row=new double[outputSize.Width*3];var previousRow=-1;
            var sums=new double[width*3];
            for(var y=band*height/Math.Min(4,height);y<(band+1)*height/Math.Min(4,height);y++)
            {
                token.ThrowIfCancellationRequested();Array.Clear(sums);var ya=y*ratioY;var yb=(y+1)*ratioY;
                for(var sy=(int)ya;sy<Math.Min(outputSize.Height,(int)Math.Ceiling(yb));sy++)
                {
                    token.ThrowIfCancellationRequested();var wy=Math.Min(yb,sy+1)-Math.Max(ya,sy);
                    if(previousRow!=sy)
                    {
                        for(var sx=0;sx<outputSize.Width;sx++){var v=transformed?RenderOutputPixel(master,definition,preparedLayout,sx,sy):RenderPixel(master,definition,sx,sy);var j=sx*3;row[j]=DecodeDisplay((float)v.X);row[j+1]=DecodeDisplay((float)v.Y);row[j+2]=DecodeDisplay((float)v.Z);}
                        previousRow=sy;
                    }
                    for(var x=0;x<width;x++){var xa=x*ratioX;var xb=(x+1)*ratioX;var j=x*3;for(var sx=(int)xa;sx<Math.Min(outputSize.Width,(int)Math.Ceiling(xb));sx++){var weight=(Math.Min(xb,sx+1)-Math.Max(xa,sx))*wy;var k=sx*3;sums[j]+=row[k]*weight;sums[j+1]+=row[k+1]*weight;sums[j+2]+=row[k+2]*weight;}}
                }
                var area=ratioX*ratioY;for(var x=0;x<width*3;x++)output[y*width*3+x]=(float)EncodeDisplay(sums[x]/area);
            }
        });
        return new(width,height,output,"16","sRGB",transformed?(ushort)1:image.Orientation);
    }
    public static (int Width,int Height) OrientedSize(ProfessionalRawMaster master)=>master.Image.Orientation is >=5 and <=8?(master.Image.Height,master.Image.Width):(master.Image.Width,master.Image.Height);
    public static HighBitDepthImageBuffer RenderRegion(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int left,int top,int width,int height,CancellationToken token=default)
    {
        if(ProfessionalRawLocalRenderer.Active(definition))return ProfessionalRawLocalRenderer.Region(master,definition,left,top,width,height,token);
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))return Kitao.InstantFilm.Research.InstantV3ProductPipeline.Region(master,definition,left,top,width,height,token);
        ValidateMaster(master,definition);var size=OutputSize(master,definition);
        var sourceSize=OrientedSize(master);var preparedLayout=InstantPaperLayout.Create(sourceSize.Width,sourceSize.Height,definition.InstantFilm);
        if(left<0||top<0||width<=0||height<=0||left+width>size.Width||top+height>size.Height)throw new ArgumentOutOfRangeException(nameof(left));
        var output=new float[checked(width*height*3)];
        for(var y=0;y<height;y++){token.ThrowIfCancellationRequested();for(var x=0;x<width;x++){
            var ox=left+x;var oy=top+y;
            var value=RenderOutputPixel(master,definition,preparedLayout,ox,oy);var i=(y*width+x)*3;output[i]=(float)value.X;output[i+1]=(float)value.Y;output[i+2]=(float)value.Z;}}
        return new(width,height,output,"16","sRGB",1);
    }}

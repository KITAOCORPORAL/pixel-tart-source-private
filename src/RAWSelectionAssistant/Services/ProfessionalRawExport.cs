using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

public sealed record ProfessionalOutputInspection(int Width,int Height,int BitDepth,int Orientation,bool Srgb,int? JpegQuality,double MeanAbsoluteError,double RootMeanSquareError,int MaximumError)
{ public string EncoderConfiguration {get;init;}="PV4RowsV1"; }

public static class ProfessionalRawExport
{
    public static async Task<ProfessionalOutputInspection> WriteAsync(ProfessionalRawMaster master,ProfessionalRawDefinition definition,string destination,StudioExportFormat format,CancellationToken token=default,Action<string>? stage=null)
    {
        ProfessionalRawPipeline.ValidateMaster(master,definition);
        if(File.Exists(destination))throw new IOException("输出已存在，未覆盖。");
        var staged=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!,".pv4-"+Guid.NewGuid().ToString("N")+".tmp");
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try
        {
            if(!Enum.IsDefined(format))throw new NotSupportedException("Unknown RAW output format.");
            if(format is StudioExportFormat.Source or StudioExportFormat.Tiff)
            {
                await Task.Run(async()=>await AtomicTiffWriter.WriteRowsAsync(staged,master.Image.Width,master.Image.Height,(y,row)=>ProfessionalRawPipeline.FillRgb16Row(master,definition,y,row),new(IccProfile:StudioQuickExport.SrgbProfileBytes(),Software:"Kitao Studio PV4",Orientation:master.Image.Orientation),token,stage:stage),token);
                token.ThrowIfCancellationRequested();File.Move(staged,destination,false);
                return new(master.Image.Width,master.Image.Height,16,master.Image.Orientation,true,null,0,0,0);
            }
            var result=await Task.Run(()=>WriteRaster(master,definition,staged,format,token,stage),token);token.ThrowIfCancellationRequested();File.Move(staged,destination,false);return result;
        }
        catch{try{if(File.Exists(staged))File.Delete(staged);}catch{}throw;}
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }

    private static ProfessionalOutputInspection WriteRaster(ProfessionalRawMaster master,ProfessionalRawDefinition definition,string destination,StudioExportFormat format,CancellationToken token,Action<string>? stage)
    {
        if(format==StudioExportFormat.Jpeg)
        {ProfessionalWicRowEncoder.Write(destination,master,definition,token);stage?.Invoke("Encoded");return Inspect(destination,master,definition,format,token,stage);}
        ProfessionalWicRowEncoder.WritePng(destination,master,definition,token);stage?.Invoke("Encoded");return Inspect(destination,master,definition,format,token,stage);
    }
    public static ProfessionalOutputInspection Inspect(string path,ProfessionalRawMaster master,ProfessionalRawDefinition definition,StudioExportFormat format,CancellationToken token=default,Action<string>? stage=null)
    {
        if(format is StudioExportFormat.Source or StudioExportFormat.Tiff)
        {
            using var stream=File.OpenRead(path);var expected=new byte[master.Image.Width*6];
            var result=TiffStreamValidator.Validate(stream,token,(y,row)=>{ProfessionalRawPipeline.FillRgb16Row(master,definition,y,expected);if(!row.Span.SequenceEqual(expected))throw new InvalidDataException("TIFF16 pixels differ from PV4 definition.");});
            if(result.Width!=master.Image.Width||result.Height!=master.Image.Height||result.Orientation!=master.Image.Orientation||!result.IccProfile.AsSpan().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("TIFF output contract mismatch.");
            return new(result.Width,result.Height,16,result.Orientation,true,null,0,0,0);
        }
        if(format==StudioExportFormat.Jpeg)ValidateJpeg444(path);
        using var input=File.OpenRead(path);var frame=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
        var size=ProfessionalRawPipeline.OrientedSize(master);var bits=frame.Format.BitsPerPixel>32?16:8;
        if(frame.PixelWidth!=size.Width||frame.PixelHeight!=size.Height||bits!=(format==StudioExportFormat.Png?16:8)||frame.ColorContexts is not {Count:>0})throw new InvalidDataException("Encoded size/depth/profile mismatch.");
        using(var profile=frame.ColorContexts[0].OpenProfileStream()){using var memory=new MemoryStream();profile.CopyTo(memory);if(!memory.ToArray().AsSpan().SequenceEqual(StudioQuickExport.SrgbProfileBytes()))throw new InvalidDataException("Only sRGB output is supported.");}
        var raster=new FormatConvertedBitmap(frame,bits==16?PixelFormats.Rgb48:PixelFormats.Rgb24,null,0);var stride=size.Width*3*(bits/8);var row=new byte[stride];double total=0,squares=0;var max=0;double signedR=0,signedG=0,signedB=0;
        for(var y=0;y<size.Height;y++)
        {
            token.ThrowIfCancellationRequested();raster.CopyPixels(new System.Windows.Int32Rect(0,y,size.Width,1),row,stride,0);
            var oracle=ProfessionalRawPipeline.RenderRegion(master,definition,0,y,size.Width,1,token).Rgb32.Span;
            for(var i=0;i<oracle.Length;i++){var wanted=bits==16?(int)Math.Round(oracle[i]*65535):Q8(oracle[i]);var actual=bits==16?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(i*2)):row[i];var error=Math.Abs(actual-wanted);total+=error;squares+=(double)error*error;max=Math.Max(max,error);if(i%3==0)signedR+=actual-wanted;else if(i%3==1)signedG+=actual-wanted;else signedB+=actual-wanted;}
        }
        var n=(double)size.Width*size.Height*3;var mae=total/n;var rmse=Math.Sqrt(squares/n);
        if(format==StudioExportFormat.Png&&max!=0)throw new InvalidDataException("Lossless PNG16 mismatch.");
        // Quality gate in 8-bit sRGB, distinct from lossless equality; max error is diagnostic, not hidden.
        if(format==StudioExportFormat.Jpeg&&(mae>5||rmse>15))throw new InvalidDataException($"JPEG95 exceeds declared tolerance: {master.Contract.Model}, MAE={mae}, RMSE={rmse}, bias={signedR/(n/3)},{signedG/(n/3)},{signedB/(n/3)}, max={max}.");
        stage?.Invoke("Validated");return new(size.Width,size.Height,bits,1,true,format==StudioExportFormat.Jpeg?95:null,mae,rmse,max){EncoderConfiguration=format==StudioExportFormat.Jpeg?"WindowsWIC-JPEG95-444-RowsV1":"WindowsWIC-PNG16-RowsV1"};
    }
    private static void ValidateJpeg444(string path)
    {
        using var input=File.OpenRead(path);using var reader=new BinaryReader(input);
        if(reader.ReadByte()!=255||reader.ReadByte()!=216)throw new InvalidDataException("Not a JPEG container.");
        while(input.Position<input.Length)
        {
            if(reader.ReadByte()!=255)throw new InvalidDataException("Invalid JPEG marker.");byte marker;do{marker=reader.ReadByte();}while(marker==255);
            if(marker is 0xD9 or 0xDA)break;if(marker==0xD8||marker is >=0xD0 and <=0xD7)continue;
            var length=(reader.ReadByte()<<8)|reader.ReadByte();if(length<2||input.Position+length-2>input.Length)throw new InvalidDataException("Invalid JPEG segment.");var end=input.Position+length-2;
            if(marker is 0xC0 or 0xC1 or 0xC2)
            {
                var bits=reader.ReadByte();reader.ReadUInt16();reader.ReadUInt16();var channels=reader.ReadByte();
                if(bits!=8||channels!=3)throw new InvalidDataException("JPEG is not RGB8.");for(var i=0;i<channels;i++){reader.ReadByte();if(reader.ReadByte()!=0x11)throw new InvalidDataException("JPEG chroma sampling is not 4:4:4.");reader.ReadByte();}return;
            }
            input.Position=end;
        }
        throw new InvalidDataException("JPEG SOF not found.");
    }
    private static byte Q8(double v)=>(byte)Math.Clamp(Math.Round((float)v*255),0,255);
}

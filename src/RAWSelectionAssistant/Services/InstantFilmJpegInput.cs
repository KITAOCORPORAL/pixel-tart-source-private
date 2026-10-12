using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Services;

/// <summary>Full-resolution decoded JPEG, never a UI proxy; WIC applies an embedded profile once.</summary>
public static class InstantFilmJpegInput
{
    public static ProfessionalRawDefinition Definition => new(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
    public static ProfessionalRawMaster FromRendered(BitmapSource image,string sourceHash,ProfessionalRawDefinition definition)
    {
        definition.Validate();
        if((long)image.PixelWidth*image.PixelHeight*24>StudioGlobalResources.MasterLimit)throw new NotSupportedException("完整编辑输入超过准入预算；未缩小源图。");
        var converted=new FormatConvertedBitmap(image,PixelFormats.Rgb48,null,0);
        var rgb=new float[checked(image.PixelWidth*image.PixelHeight*3)];var row=new ushort[image.PixelWidth*3];
        for(var y=0;y<image.PixelHeight;y++)
        {
            converted.CopyPixels(new System.Windows.Int32Rect(0,y,image.PixelWidth,1),row,row.Length*2,0);
            for(var x=0;x<row.Length;x++)rgb[y*row.Length+x]=row[x]/65535f;
        }
        var contract=new ProfessionalRawContract("CurrentEdits-FullResolution-V1","Rendered","JPEG",sourceHash,definition.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",OutputBits:16,WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB",Characterization:"Persisted full-resolution edit recipe; source precision not recovered");
        return new(new HighBitDepthImageBuffer(image.PixelWidth,image.PixelHeight,rgb,"16","sRGB",1),contract);
    }
    public static async Task<ProfessionalRawMaster> ReadAsync(string path,ProfessionalRawDefinition definition,CancellationToken token)
    {
        definition.Validate();
        if(definition.InputConfiguration!="RenderedSrgbFloatV1" || Path.GetExtension(path).ToLowerInvariant() is not (".jpg" or ".jpeg"))
            throw new NotSupportedException("即时胶片已渲染入口只接受JPEG；其他格式未适配。");
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try
        {
            using var reservation=StudioGlobalResources.ReserveWork(1024L*1024*1024);
            return await Task.Run(()=>
            {
                token.ThrowIfCancellationRequested();using var input=File.OpenRead(path);
                var hash=Convert.ToHexString(SHA256.HashData(input));input.Position=0;
                var frame=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                if((long)frame.PixelWidth*frame.PixelHeight*20>1024L*1024*1024)throw new NotSupportedException("JPEG工作缓冲超过预算，未缩小原图。");
                ushort orientation=1;
                if(frame.Metadata is BitmapMetadata metadata){try{var value=metadata.GetQuery("/app1/ifd/{ushort=274}");if(value is ushort o)orientation=o;}catch(NotSupportedException){}}
                BitmapSource converted=frame.ColorContexts is {Count:>0}
                    ? new ColorConvertedBitmap(frame,frame.ColorContexts[0],new ColorContext(PixelFormats.Bgr24),PixelFormats.Rgb24)
                    : new FormatConvertedBitmap(frame,PixelFormats.Rgb24,null,0);
                var rgb=new float[checked(frame.PixelWidth*frame.PixelHeight*3)];var row=new byte[frame.PixelWidth*3];
                for(var y=0;y<frame.PixelHeight;y++){token.ThrowIfCancellationRequested();converted.CopyPixels(new System.Windows.Int32Rect(0,y,frame.PixelWidth,1),row,row.Length,0);for(var x=0;x<row.Length;x++)rgb[y*row.Length+x]=row[x]/255f;}
                var contract=new ProfessionalRawContract("WindowsWIC-JPEG-sRGB-V1","Rendered","JPEG",hash,definition.DecodeKey,[],[],[],255,OutputSpace:"sRGB",OutputBits:8,WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB",Characterization:"Embedded ICC converted by WIC; untagged assumed sRGB");
                return new ProfessionalRawMaster(new HighBitDepthImageBuffer(frame.PixelWidth,frame.PixelHeight,rgb,"8","sRGB",orientation),contract);
            },token);
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Export;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try{return Run(args);}catch(ControlledIccException e){Console.Error.WriteLine(JsonSerializer.Serialize(new{status="REFUSED",code=e.Code,message=e.Message}));return 2;}
        catch(OperationCanceledException){Console.Error.WriteLine("{\"status\":\"CANCELLED\"}");return 3;}
        catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}
    }
    static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static int Run(string[] args)
    {
        if(args.Length==2&&args[0]=="inspect")
        {
            var p=EmbeddedIccInspector.Read(args[1]);Console.WriteLine(JsonSerializer.Serialize(new{status="INSPECTED_NOT_COLOR_ACCURACY_APPROVAL",p.Sha256,p.Version,p.ColorSpace,p.Pcs,p.Tags}));return 0;
        }
        if(args.Length!=6||args[0]!="--explicit-research")throw new ControlledIccException("ICC_DISABLED","Usage: --explicit-research sourcePhoto outputNewDirectory externalLcms workingIcc outputIcc. No product defaults changed.");
        var sourcePath=Path.GetFullPath(args[1]);var directory=Path.GetFullPath(args[2]);
        if(Directory.Exists(directory))throw new IOException("New directory required; never overwrite prior results.");
        var input=EmbeddedIccInspector.Read(sourcePath);var working=ControlledIccProfile.Read(File.ReadAllBytes(args[4]));var output=ControlledIccProfile.Read(File.ReadAllBytes(args[5]));
        var sourceHash=Hash(File.ReadAllBytes(sourcePath));
        using var adapter=new ControlledIccTransform(new(true,args[3],"2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4",new[]{input.Sha256,working.Sha256,output.Sha256}));
        using var file=File.OpenRead(sourcePath);
        // IgnoreColorProfile prevents WIC auto-conversion; LCMS below owns exactly one conversion.
        var frame=BitmapDecoder.Create(file,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];
        if(frame.PixelWidth<=0||frame.PixelHeight<=0||(long)frame.PixelWidth*frame.PixelHeight>50000000)throw new ControlledIccException("ICC_IMAGE_BUDGET","First experiment capped at50MP.");
        if(frame.Metadata is BitmapMetadata md)
        {
            foreach(var q in new[]{"/ifd/{ushort=274}","/app1/ifd/{ushort=274}"})if(md.ContainsQuery(q)&&Convert.ToInt32(md.GetQuery(q))!=1)throw new ControlledIccException("ICC_ORIENTATION","V1 requires orientation1; no silent project geometry change.");
        }
        var rgb48=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var pixels=new ushort[checked(frame.PixelWidth*frame.PixelHeight*3)];rgb48.CopyPixels(pixels,frame.PixelWidth*6,0);
        var floats=pixels.Select(x=>x/65535f).ToArray();
        var recipe=new ControlledIccRecipe(ControlledIccTransform.AlgorithmVersion,sourceHash,input.Sha256,working.Sha256,output.Sha256,-1);recipe.Validate();
        var linear=adapter.ConvertRgb(floats,input,working);var edit=recipe.ApplyFrozenEdit(linear);var encoded=adapter.ConvertRgb(edit,working,output);
        Directory.CreateDirectory(directory);
        var recipePath=Path.Combine(directory,"recipe.json");File.WriteAllText(recipePath,JsonSerializer.Serialize(recipe,new JsonSerializerOptions{WriteIndented=true}));
        var reopened=JsonSerializer.Deserialize<ControlledIccRecipe>(File.ReadAllText(recipePath))??throw new InvalidDataException();reopened.Validate();
        if(reopened.SourceFileSha256!=Hash(File.ReadAllBytes(sourcePath))||reopened.InputProfileSha256!=input.Sha256||reopened.WorkingProfileSha256!=working.Sha256||reopened.OutputProfileSha256!=output.Sha256)throw new ControlledIccException("ICC_RECIPE_IDENTITY","Reopen identity mismatch.");
        var reencoded=adapter.ConvertRgb(reopened.ApplyFrozenEdit(adapter.ConvertRgb(floats,input,working)),working,output);
        if(!encoded.SequenceEqual(reencoded))throw new InvalidDataException("Preview/reopen differs.");
        // Preview pixels are a detached48-bit bitmap with explicit output-profile identity, not the old default preview path.
        var quantized=encoded.Select(v=>(ushort)Math.Clamp(Math.Round(v*65535),0,65535)).ToArray();
        var preview=BitmapSource.Create(frame.PixelWidth,frame.PixelHeight,96,96,PixelFormats.Rgb48,null,quantized,frame.PixelWidth*6);preview.Freeze();
        var previewCopy=new ushort[quantized.Length];preview.CopyPixels(previewCopy,frame.PixelWidth*6,0);if(!previewCopy.SequenceEqual(quantized))throw new InvalidDataException("Preview copy differs.");
        using(var stream=new FileStream(Path.Combine(directory,"output.tif"),FileMode.CreateNew))TiffExport.WriteRgb48(stream,new(frame.PixelWidth,frame.PixelHeight,encoded,"32f","ControlledIccLinearV1-output",1),new(IccProfile:output.CopyBytes(),Software:ControlledIccTransform.AlgorithmVersion));
        string profilePath=Path.Combine(directory,"output.icc");File.WriteAllBytes(profilePath,output.CopyBytes());
        var context=new ColorContext(new Uri(profilePath));
        foreach(var format in new[]{"png","jpg"})
        {
            BitmapEncoder encoder=format=="png"?new PngBitmapEncoder():new JpegBitmapEncoder{QualityLevel=100};
            BitmapSource image=format=="png"?preview:new FormatConvertedBitmap(preview,PixelFormats.Rgb24,null,0);
            encoder.Frames.Add(BitmapFrame.Create(image,null,null,new System.Collections.ObjectModel.ReadOnlyCollection<ColorContext>(new[]{context})));
            using var stream=new FileStream(Path.Combine(directory,"output."+format),FileMode.CreateNew);encoder.Save(stream);
        }
        var results=new List<object>();
        foreach(var format in new[]{"tif","png","jpg"})
        {
            string p=Path.Combine(directory,"output."+format);byte[] embedded;
            if(format=="png")embedded=ReadPngIcc(p);else embedded=EmbeddedIccInspector.Read(p).CopyBytes();
            if(Hash(embedded)!=output.Sha256)throw new InvalidDataException("Output ICC hash differs.");
            using var s=File.OpenRead(p);var f=BitmapDecoder.Create(s,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];
            var b=new FormatConvertedBitmap(f,PixelFormats.Rgb48,null,0);var read=new ushort[quantized.Length];b.CopyPixels(read,frame.PixelWidth*6,0);
            double max=0,sum=0;for(int i=0;i<read.Length;i++){double d=Math.Abs(read[i]/65535d-encoded[i]);max=Math.Max(max,d);sum+=d;}
            if(format!="jpg"&&!read.SequenceEqual(quantized))throw new InvalidDataException("Lossless16-bit export mismatch.");
            results.Add(new{format,iccSha256=Hash(embedded),sha256=Hash(File.ReadAllBytes(p)),bitDepth=format=="jpg"?8:16,maxAbs=max,meanAbs=sum/read.Length,lossy=format=="jpg"});
        }
        File.WriteAllBytes(Path.Combine(directory,"decoded-input-f32.bin"),System.Runtime.InteropServices.MemoryMarshal.AsBytes(floats.AsSpan()).ToArray());
        File.WriteAllBytes(Path.Combine(directory,"preview-output-f32.bin"),System.Runtime.InteropServices.MemoryMarshal.AsBytes(encoded.AsSpan()).ToArray());
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{status="VERIFIED_ISOLATED_WORKFLOW_NOT_PRODUCT_ADMISSION",version=recipe.AlgorithmVersion,sourceHash,inputProfile=input.Sha256,workingProfile=working.Sha256,outputProfile=output.Sha256,adapter.EngineVersion,adapter.EngineSha256,width=frame.PixelWidth,height=frame.PixelHeight,recipe,previewReopenExact=true,output=results,display="Detached RGB48 + explicit output profile; no physical display calibration claim",CalibrationEligible=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(Path.Combine(directory,"result.json"));return 0;
    }
    static byte[] ReadPngIcc(string path)
    {
        using var s=File.OpenRead(path);s.Position=8;while(s.Position<s.Length)
        {
            byte[] h=new byte[8];s.ReadExactly(h);uint n=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(h);if(n>16*1024*1024||s.Position+n+4>s.Length)throw new InvalidDataException();
            var data=new byte[n];s.ReadExactly(data);s.Position+=4;
            if(System.Text.Encoding.ASCII.GetString(h,4,4)=="iCCP")
            {
                int z=Array.IndexOf(data,(byte)0);if(z<0||z+2>=data.Length||data[z+1]!=0)throw new InvalidDataException();using var input=new MemoryStream(data,z+2,data.Length-z-2);using var zip=new System.IO.Compression.ZLibStream(input,System.IO.Compression.CompressionMode.Decompress);using var output=new MemoryStream();zip.CopyTo(output);if(output.Length>ControlledIccProfile.MaximumBytes)throw new InvalidDataException();return output.ToArray();
            }
        }
        throw new ControlledIccException("ICC_MISSING","Export lacks iCCP.");
    }
}

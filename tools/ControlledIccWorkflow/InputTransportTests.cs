using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Export;
internal static class InputTransportTests
{
    public static int Run(string[] args)
    {
        if(args.Length!=4)throw new ArgumentException("--input-selftest newdir externalDLL linearICC");
        var dir=Path.GetFullPath(args[1]);if(Directory.Exists(dir))throw new IOException("Newdirectory required");Directory.CreateDirectory(dir);var profile=ControlledIccProfile.Read(File.ReadAllBytes(args[3]));
        using var engine=new ControlledIccTransform(new(true,args[2],"2f864b9f1bf5839fbc963265a0c9c94a4709124439e5ab5bfcae5f522483a1c4",new[]{profile.Sha256}));
        var observations=new List<object>();
        foreach(var values in new[]{new ushort[]{0,32768,65535},new ushort[]{8192,16384,49152},new ushort[]{16384,16384,16384}})
        {
            var rgb=Enumerable.Range(0,32*32).SelectMany(_=>values).ToArray();var im=BitmapSource.Create(32,32,96,96,PixelFormats.Rgb48,null,rgb,192);
            string tif=Path.Combine(dir,"rgb48-"+values[0]+".tif");using(var stream=File.Create(tif))TiffExport.WriteRgb48(stream,new(32,32,rgb),new(IccProfile:profile.CopyBytes()));
            using(var st=File.OpenRead(tif)){var f=BitmapDecoder.Create(st,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];var decoded=new ushort[rgb.Length];new FormatConvertedBitmap(f,PixelFormats.Rgb48,null,0).CopyPixels(decoded,192,0);if(!rgb.SequenceEqual(decoded))throw new InvalidDataException("WIC RGB48transport changed codes");var inp=decoded.Select(v=>v/65535f).ToArray();var o=engine.ConvertRgb(inp,profile,profile);if(inp.Where((v,i)=>v!=o[i]).Any())throw new InvalidDataException("IdentityLCMSchangedtransport");observations.Add(new{file=Path.GetFileName(tif),source=values,wic=decoded.Take(3),lcmsInput=inp.Take(3),lcmsOutput=o.Take(3),exact=true});}
            var rgb24=new byte[32*32*3];for(int i=0;i<rgb24.Length;i++)rgb24[i]=(byte)Math.Round(rgb[i]/257d);
            var bitmap=BitmapSource.Create(32,32,96,96,PixelFormats.Rgb24,null,rgb24,96);var enc=new JpegBitmapEncoder{QualityLevel=100};enc.Frames.Add(BitmapFrame.Create(bitmap));using var ms=new MemoryStream();enc.Save(ms);var jpeg=ms.ToArray();
            foreach(int pieces in new[]{1,3})
            {
                var p=Path.Combine(dir,$"rgb24-{values[0]}-{pieces}.jpg");File.WriteAllBytes(p,WithIcc(jpeg,profile.CopyBytes(),pieces));var actual=EmbeddedIccInspector.Read(p);if(actual.Sha256!=profile.Sha256)throw new InvalidDataException("APP2identitydiffers");
                using var st=File.OpenRead(p);var f=BitmapDecoder.Create(st,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];var raw24=new byte[rgb24.Length];new FormatConvertedBitmap(f,PixelFormats.Rgb24,null,0).CopyPixels(raw24,96,0);var raw48=new ushort[rgb.Length];new FormatConvertedBitmap(f,PixelFormats.Rgb48,null,0).CopyPixels(raw48,192,0);
                for(int i=0;i<raw24.Length;i++)if(raw48[i]!=raw24[i]*257)throw new InvalidDataException("WIC8to16channelorcodechange");
                var inp=raw48.Select(x=>x/65535f).ToArray();var o=engine.ConvertRgb(inp,profile,profile);if(!inp.SequenceEqual(o))throw new InvalidDataException("LCMSinputchanged");double max=raw24.Select((v,i)=>Math.Abs(v-rgb24[i])/255d).Max();if(max>3/255d)throw new InvalidDataException("UniformJPEGquality100error>3codes");observations.Add(new{file=Path.GetFileName(p),pieces,source=rgb24.Take(3),wic=raw24.Take(3),upcast=raw48.Take(3),lcmsInput=inp.Take(3),lcmsOutput=o.Take(3),maxAbsLossy=max,profile=actual.Sha256,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))});
            }
        }
        var orientations=new List<object>();
        foreach(int orientation in new[]{1,2,3,4,5,6,7,8}){var pixel=BitmapSource.Create(4,3,96,96,PixelFormats.Rgb24,null,new byte[36],12);var md=new BitmapMetadata("jpg");md.SetQuery("/app1/ifd/{ushort=274}",(ushort)orientation);var enc=new JpegBitmapEncoder{QualityLevel=100};enc.Frames.Add(BitmapFrame.Create(pixel,null,md,null));using var stream=new MemoryStream();enc.Save(stream);var file=Path.Combine(dir,"orientation-"+orientation+".jpg");File.WriteAllBytes(file,WithIcc(stream.ToArray(),profile.CopyBytes(),1));using var input=File.OpenRead(file);var decoded=BitmapDecoder.Create(input,BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnLoad).Frames[0];var meta=(BitmapMetadata)decoded.Metadata;int found=Convert.ToInt32(meta.GetQuery("/app1/ifd/{ushort=274}"));if(found!=orientation)throw new InvalidDataException("EXIForientationtransportchanged");orientations.Add(new{file=Path.GetFileName(file),orientation,policy=orientation==1?"SUPPORTED":"ICC_ORIENTATION_REFUSED_BY_WORKFLOW"});}
        var info=new{status="PASSED",orientations,wic=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsCodecs.dll")).FileVersion,formatConversion="RGB24->RGB48 exactcode*257; noColorConvertedBitmap; decoder IgnoreColorProfile",observations};File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(info,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(info));return 0;
    }
    internal static byte[] WithIcc(byte[] jpeg,byte[] icc,int chunks)
    {
        using var s=new MemoryStream();s.Write(jpeg.AsSpan(0,2));for(int c=0;c<chunks;c++){int from=c*icc.Length/chunks,to=(c+1)*icc.Length/chunks,n=14+to-from;s.Write(new byte[]{255,226,(byte)((n+2)>>8),(byte)(n+2)});s.Write("ICC_PROFILE\0"u8);s.WriteByte((byte)(c+1));s.WriteByte((byte)chunks);s.Write(icc.AsSpan(from,to-from));}s.Write(jpeg.AsSpan(2));return s.ToArray();
    }
}

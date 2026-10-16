using Directory = System.IO.Directory;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using Sdcb.LibRaw;

internal static class SonyProbe
{
    internal const string AuthorizedSha = "1FE22BE78C66AB0DDCCF753D10187F142B980146FFF7F5D188533905B7F38B93";
    internal static string Hash(string path) { using var stream=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static async Task Run(string copy, string output)
    {
        if(Directory.Exists(output))throw new IOException("New output directory required.");
        if(Hash(copy)!=AuthorizedSha)throw new InvalidDataException("Sony_DSC08015 exact authorized identity required.");
        Directory.CreateDirectory(output);
        var rows=new List<object>(); var process=Process.GetCurrentProcess();var clock=Stopwatch.StartNew();
        var metadata=ImageMetadataReader.ReadMetadata(copy);
        // Only specifically admitted EXIF identity/geometry fields. Never enumerate GPS, serial or private directory listings.
        var wanted=new HashSet<string>{"Make","Model","Compression","Image Width","Image Height","Exif Image Width","Exif Image Height","Orientation","Bits Per Sample","Sony RAW File Type","Sony Quality"};
        var fields=metadata.SelectMany(directory=>directory.Tags.Where(tag=>wanted.Contains(tag.Name)).Select(tag=>new{directory=directory.Name,tag=tag.Name,value=tag.Description})).ToArray();
        void Sample(string stage){process.Refresh();rows.Add(new{stage,ms=clock.Elapsed.TotalMilliseconds,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,peakWorkingSet=process.PeakWorkingSet64});}
        object? container=null; object? failure=null; object? decoded=null;
        try
        {
            using var raw=RawContext.OpenFile(copy);
            var native = typeof(RawContext).GetProperty("RawData",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(raw)!;
            var sizes=native.GetType().GetField("ImageSizes")!.GetValue(native)!;
            var sizeValues=sizes.GetType().GetFields().Where(f=>f.FieldType.IsPrimitive).ToDictionary(f=>f.Name,f=>f.GetValue(sizes));
            container=new{make=raw.ImageParams.Make,model=raw.ImageParams.Model,decoder=raw.DecoderInfo,geometry=sizeValues,version=RawContext.Version};
        }
        catch(Exception error){container=new{open="REFUSED",error=error.ToString()};}
        try
        {
            Sample("before-old-product");
            var decoder=new LibRawDecoder();
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            try { await decoder.DecodeAsync(copy,new(DecodeMode:RawDecodeMode.ProfessionalDecode),cancel.Token);throw new InvalidDataException("Expected pre-cancel"); }
            catch(OperationCanceledException){rows.Add(new{operation="pre-cancel",status="PASSED"});}
            var watch=Stopwatch.StartNew();var image=await decoder.DecodeAsync(copy,new(DecodeMode:RawDecodeMode.ProfessionalDecode));
            decoded=new{status="OLD_PRODUCT_DECODED",image.Width,image.Height,image.BitsPerChannel,image.Metadata,image.PixelFormat,elapsedMs=watch.Elapsed.TotalMilliseconds,pixelSha256=Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Rgb48Pixels!.AsSpan())))};
            Sample("after-old-product");
        }
        catch(RawDecodeException error){failure=new{status="REFUSED",error.ErrorCode,error.Stage,error.NativeCode,error.NativeName,message=error.Message};}
        catch(Exception error){failure=new{status="FAILED",type=error.GetType().Name,message=error.Message};}
        for(int i=0;i<5;i++){await Task.Delay(1000);Sample("natural-after-operation-"+i);}
        var modules=process.Modules.Cast<ProcessModule>().Where(m=>new[]{"raw_r.dll","Sdcb.LibRaw.dll","PhotoEngineFollowupAudit.dll"}.Contains(m.ModuleName)).Select(m=>new{name=m.ModuleName,sha256=Hash(m.FileName)}).ToArray();
        if(Hash(copy)!=AuthorizedSha)throw new InvalidDataException("Copy changed.");
        await File.WriteAllTextAsync(Path.Combine(output,"SONY_DSC08015_OLD_PRODUCT.json"),JsonSerializer.Serialize(new{identity="Sony_DSC08015",sourceSha256=AuthorizedSha,bytes=new FileInfo(copy).Length,containerType="TIFF-based Sony ARW; compression evidence only from admitted tags/decoder",metadata=fields,container,decoded,failure,rows,modules,originalReadOnly=true,oldDsc09916IdentityUnchanged=true,desktop="NOT_RUN",scope="Backend product decoder result for this file only; not all Sony models/variants."},new JsonSerializerOptions{WriteIndented=true}));
    }
}

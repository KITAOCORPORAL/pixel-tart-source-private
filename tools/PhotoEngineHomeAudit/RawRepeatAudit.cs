using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
internal static class RawRepeatAudit
{
 public static async Task Run(string source,string expected,string output)
 {
  if(File.Exists(output))throw new IOException("new result path");
  using(var f=File.OpenRead(source))if(Convert.ToHexString(SHA256.HashData(f))!=expected)throw new InvalidDataException("source identity");
  var pipeline=new RawMatchTiff16ProductPipeline(new LibRawDecoder());var frozen=await pipeline.DecodeFrozenMasterAsync(source);
  var rgbHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(frozen.Image.Rgb32.Span)));
  var rendered=pipeline.Render(frozen.Image,null,null).ProcessingPixels!;var renderedHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(rendered.Rgb32.Span)));
  var modules=System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Where(m=>new[]{"raw_r.dll","jpeg8.dll","lcms2.dll"}.Contains(m.ModuleName)).Select(m=>new{name=m.ModuleName,path=m.FileName,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(m.FileName)))});
  File.WriteAllText(output,JsonSerializer.Serialize(new{frozen.Image.Width,frozen.Image.Height,frozen.Image.Orientation,rgbHash,renderedHash,sourceSha256=frozen.SourceSha256,OMP_NUM_THREADS=Environment.GetEnvironmentVariable("OMP_NUM_THREADS"),decoder=Sdcb.LibRaw.RawContext.Version,modules},new JsonSerializerOptions{WriteIndented=true}));
 }
 public static void CompareTiff(string first,string second,string output)
 {
  if(File.Exists(output))throw new IOException("new result path");using var a=File.OpenRead(first);using var b=File.OpenRead(second);var ra=TiffStreamValidator.Validate(a);var rb=TiffStreamValidator.Validate(b);
  if(ra.Width!=rb.Width||ra.Height!=rb.Height||ra.BitsPerSample!=16||rb.BitsPerSample!=16)throw new InvalidDataException("geometry/depth");
  // The product's single-strip RGB16 TIFF pixels are the tail. No source-photo mutation.
  a.Position=a.Length-ra.PixelBytes;b.Position=b.Length-rb.PixelBytes;var x=new byte[ra.Width*6];var y=new byte[x.Length];long changed=0;int maximum=0;double total=0;
  for(int row=0;row<ra.Height;row++){a.ReadExactly(x);b.ReadExactly(y);for(int i=0;i<x.Length;i+=2){int delta=Math.Abs(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(x.AsSpan(i))-System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(y.AsSpan(i)));if(delta!=0)changed++;maximum=Math.Max(maximum,delta);total+=delta;}}
  File.WriteAllText(output,JsonSerializer.Serialize(new{first,second,ra.Width,ra.Height,pixelsFirst=ra.PixelSha256,pixelsSecond=rb.PixelSha256,changedChannelSamples=changed,maximum16BitCodeDifference=maximum,meanAbsoluteCodeDifference=total/(ra.Width*(double)ra.Height*3),iccExact=ra.IccProfile.SequenceEqual(rb.IccProfile),classification=changed==0?"PIXEL_IDENTICAL":"UNEXPLAINED_LEGACY_XTRANS_DIFFERENCE_BLOCKING"},new JsonSerializerOptions{WriteIndented=true}));
 }
}

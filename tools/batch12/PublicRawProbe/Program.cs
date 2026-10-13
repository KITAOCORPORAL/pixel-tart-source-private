using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
var path=args[0];var output=args[1];var d=new ProfessionalRawDefinition();var master=await new ProfessionalRawDecoder().DecodeAsync(path,d);
if(master.Contract.SourceSha256!="CD7492209F002050F24E4AE4C2365F8702145D917F4979E2456AEDDB33AEBC6F")throw new Exception("Wrong public sample hash.");
if(master.Image.Orientation!=1 || master.Contract.BlackLevel!=2049 || master.Contract.Gamma!="1,1" || master.Contract.AutoBright || !master.Contract.WhiteBalanceApplied || !master.Contract.CameraMatrixApplied || master.Contract.OutputBits!=16 || master.Image.WorkingColorSpace!=ProfessionalRawPipeline.WorkingSpace)throw new Exception("Native contract drift.");
if(!master.Contract.NormalizedWhiteBalance.SequenceEqual(new[]{1.5732421875,1d,2.30078125,1d}))throw new Exception("Camera metadata WB drift.");
var native=ProfessionalRawPipeline.RenderOutput(master,d);var hq=ProfessionalRawPipeline.RenderPreview(master,d,Math.Max(native.Width,native.Height));
var bytes=new byte[native.Rgb32.Length*4];Buffer.BlockCopy(native.Rgb32.ToArray(),0,bytes,0,bytes.Length);
if(!native.Rgb32.Span.SequenceEqual(hq.Rgb32.Span))throw new Exception("Native/1:1 HQ mismatch.");
var size=ProfessionalRawPipeline.OrientedSize(master);var region=ProfessionalRawPipeline.RenderRegion(master,d,0,0,Math.Min(16,size.Width),Math.Min(16,size.Height));
for(var y=0;y<region.Height;y++)for(var x=0;x<region.Width;x++)for(var c=0;c<3;c++)
 if(region.Rgb32.Span[(y*region.Width+x)*3+c]!=native.Rgb32.Span[(y*native.Width+x)*3+c])throw new Exception("Region/native mismatch.");
using var file=File.OpenRead(path);var sourceHash=Convert.ToHexString(SHA256.HashData(file));
if(sourceHash!=master.Contract.SourceSha256)throw new Exception("Source mutation.");
var values=native.Rgb32.ToArray();if(values.Any(x=>!float.IsFinite(x)||x<0||x>1))throw new Exception("Invalid output samples.");
var report=new{SampleId="00_0010",DataSource="Cube++ Zenodo4153431, CC-BY-4.0",SourceSha256=sourceHash,Definition=d,master.Contract,
 Width=master.Image.Width,Height=master.Image.Height,InputOrientation=master.Image.Orientation,OutputOrientation=native.Orientation,
 OutputSpace=native.WorkingColorSpace,NativeHqExact=true,RegionNativeExact=true,OutputMin=values.Min(),OutputMax=values.Max(),OutputFloatSha256=Convert.ToHexString(SHA256.HashData(bytes)),
 Limitations=new[]{"Native raw uses camera metadata WB; NOT Cube ground-truth WB", "Native pipeline has built-in camera matrix and clipping, not calibrated scene truth", "Sample orientation=1; orientations2-8 only synthetic", "Illumination error measured separately on published downscaled sensor PNG"}};
File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine($"{sourceHash} {master.Contract.Model} {native.Width}x{native.Height} {native.WorkingColorSpace}");


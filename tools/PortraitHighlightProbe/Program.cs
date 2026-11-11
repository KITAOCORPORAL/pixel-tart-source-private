using System.Runtime.InteropServices;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;
using RAWSelectionAssistant.Core.Services.Projects;
using PortraitHighlightResearch;
using RAWSelectionAssistant.Core.Services.Export;
if(args.Length is <6 or >9)throw new ArgumentException("input.f32 output-directory width height EV domain [trace] [candidate-recipe.json]");
var inputPath=args[0];var output=Path.GetFullPath(args[1]);int width=int.Parse(args[2]),height=int.Parse(args[3]);double ev=double.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture);var domain=args[5];bool trace=args.Skip(6).Contains("trace");
var iccArg=args.Skip(6).FirstOrDefault(x=>x.StartsWith("icc="));
var recipePath=args.Skip(6).FirstOrDefault(x=>x!="trace"&&!x.StartsWith("icc="));
if(width<=0||height<=0||width>512||height>512||checked((long)width*height)>512*512)throw new InvalidDataException("ROI exceeds 512x512 hard bound");
if(new FileInfo(inputPath).Length!=checked((long)width*height*12))throw new InvalidDataException("Invalid input length");
if(domain is not("encoded-srgb" or "linear-prophoto-d65"))throw new InvalidDataException("Unknown input domain");
Directory.CreateDirectory(output);var input=new float[width*height*3];using(var f=File.OpenRead(inputPath))f.ReadExactly(MemoryMarshal.AsBytes(input.AsSpan()));
foreach(var v in input)if(!float.IsFinite(v))throw new InvalidDataException("Non-finite input");
void Write(string name,float[] p){using var f=File.Create(Path.Combine(output,name+".f32"));f.Write(MemoryMarshal.AsBytes(p.AsSpan()));}
var result=new float[input.Length];var candidate=new float[input.Length];var stages=new Dictionary<string,double[]>();int at=0;
Action<string,ColorVector3>? observe=trace?(name,v)=>{if(!stages.TryGetValue(name,out var a)){a=new double[input.Length];stages.Add(name,a);}a[at]=v.X;a[at+1]=v.Y;a[at+2]=v.Z;}:null;
var exposure=new RenderedSrgbExposure(ExposureEv:ev).Validate();var candidateRecipe=recipePath is null?new MaxChannelShoulderV1(ExposureEv:ev).Validate():JsonSerializer.Deserialize<MaxChannelShoulderV1>(File.ReadAllText(recipePath))!.Validate();
if(candidateRecipe.ExposureEv!=ev)throw new InvalidDataException("Frozen candidate recipe EV mismatch");
void WriteStage(string stage,double[] values){Write(stage,Array.ConvertAll(values,v=>(float)v));using var f=File.Create(Path.Combine(output,stage+".f64"));f.Write(MemoryMarshal.AsBytes(values.AsSpan()));}
for(int i=0;i<input.Length;i+=3){at=i;var c=new ColorVector3(input[i],input[i+1],input[i+2]);var r=domain=="encoded-srgb"?exposure.Apply(c,observe):ProfessionalRawPipeline.Display(ProfessionalRawPipeline.ApplyExposure(c,ev),observe);result[i]=(float)r.X;result[i+1]=(float)r.Y;result[i+2]=(float)r.Z;if(domain=="encoded-srgb"){var c1=candidateRecipe.ApplyRendered(c);candidate[i]=(float)c1.X;candidate[i+1]=(float)c1.Y;candidate[i+2]=(float)c1.Z;}}
Write("output",result);if(domain=="encoded-srgb")
{
 Write("candidate",candidate);
 var profile=iccArg is null?Array.Empty<byte>():File.ReadAllBytes(iccArg[4..]);
 using var image=new HighBitDepthImageBuffer(width,height,candidate,"32f","sRGB",1);
 using var file=File.Create(Path.Combine(output,"candidate-product-rgb16.tif"));
 TiffExport.WriteRgb48(file,image,new(IccProfile:profile,Software:"PortraitHighlightResearchV1",RequireHighBitDepthSource:true));
}foreach(var(stage,values)in stages)WriteStage(stage,values);
if(domain=="encoded-srgb")
{
 using var image=new HighBitDepthImageBuffer(width,height,input,"32f","sRGB",1);
 using var basic=ColorStudioToolProcessor.Apply(image,new(Guid.Parse("97e8c578-6d37-4709-a57c-a07ae8a11001"),ColorStudioNodeType.BasicTone,"Highlight diagnostic",NumericParameters:new Dictionary<string,double>{{"exposure",ev}}),CancellationToken.None,trace?(pixel,stage,value)=>{at=pixel*3;observe?.Invoke(stage,value);}:null);
 using var f=File.Create(Path.Combine(output,"basic-tone-output.f32"));f.Write(MemoryMarshal.AsBytes(basic.Rgb32.Span));foreach(var(stage,values)in stages.Where(p=>p.Key.StartsWith("basic-")))WriteStage(stage,values);
}
Console.WriteLine(JsonSerializer.Serialize(new{width,height,ev,domain,trace,stages=stages.Keys,sourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(inputPath)))}));

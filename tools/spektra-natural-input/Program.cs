// Research caller of unchanged production decoder/display. Not MatrixDecodeResearchV2.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Projects;
using Sdcb.LibRaw;
var configPath=Environment.GetEnvironmentVariable("COLOR_RESEARCH_ENV")??throw new ArgumentException("Explicit COLOR_RESEARCH_ENV required");using var hostConfig=JsonDocument.Parse(File.ReadAllText(configPath));if(hostConfig.RootElement.GetProperty("version").GetString()!="ColorEngineHomeEnvironmentV1")throw new InvalidDataException("Host config version");var root=Path.GetDirectoryName(Path.GetFullPath(hostConfig.RootElement.GetProperty("sourceFile").GetString()!))!;
void Report(string file,object report){var temp=file+".partial-"+Guid.NewGuid().ToString("N");File.WriteAllText(temp,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,file,false);}
string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
if(args.Length==1&&args[0]=="inventory"){
 var records=new List<object>();foreach(var p in Directory.GetFiles(root).Order()){
  try{using var raw=RawContext.OpenFile(p);records.Add(new{file=Path.GetFileName(p),sha256=Hash(p),make=raw.ImageParams.Make,model=raw.ImageParams.Model,width=raw.Width,height=raw.Height,rawWidth=raw.RawWidth,rawHeight=raw.RawHeight,filters=raw.ImageParams.Filters,colors=raw.ImageParams.Colors});}catch(Exception e){records.Add(new{file=Path.GetFileName(p),error=e.ToString()});}
 }Console.WriteLine(JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}));return;
}
string ValidateOutput(string path){
 var allowed=Path.GetFullPath(hostConfig.RootElement.GetProperty("authorizedOutputRoot").GetString()!).TrimEnd(Path.DirectorySeparatorChar);
 var dest=Path.GetFullPath(path);var source=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
 bool Under(string child,string parent)=>child.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 if(!Under(dest,allowed)||dest.Equals(allowed,StringComparison.OrdinalIgnoreCase)||Under(dest,source)||Under(source,dest)||dest.Equals(source,StringComparison.OrdinalIgnoreCase))throw new IOException("Output outside authorized research root or overlaps CompanyRAW");
 if(Directory.Exists(dest)||File.Exists(dest))throw new IOException("Output already exists");
 var parent=Directory.GetParent(dest);if(parent is null||!parent.Exists)throw new IOException("Output parent required");
 for(var d=parent;d is not null;d=d.Parent)if((d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Output path includes reparse point");
 return dest;
}
if(args.Length==2&&args[0]=="validate-output"){Console.WriteLine(ValidateOutput(args[1]));return;}
if(args.Length!=3)throw new ArgumentException("<explicit authorized filename> <expectedSHA256> <new output under explicit host research root>");
var file=Path.GetFullPath(Path.Combine(root,args[0]));if(Path.GetDirectoryName(file)!=root)throw new InvalidDataException("Outside CompanyRAW root");if(!file.Equals(Path.GetFullPath(hostConfig.RootElement.GetProperty("sourceFile").GetString()!),StringComparison.OrdinalIgnoreCase)||args[1]!=hostConfig.RootElement.GetProperty("sourceSha256").GetString())throw new InvalidDataException("Frozen source whitelist mismatch");var dest=ValidateOutput(args[2]);
using var cancel=new CancellationTokenSource();Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancel.Cancel();};var before=Hash(file);if(before!=args[1])throw new InvalidDataException("Source hash differs");Directory.CreateDirectory(dest);var watch=Stopwatch.StartNew();var stages=new List<string>();
try{
 var definition=new ProfessionalRawDefinition(ExposureEv:0);var master=await new ProfessionalRawDecoder(s=>stages.Add(s)).DecodeAsync(file,definition,cancel.Token);watch.Stop();double decodeMs=watch.Elapsed.TotalMilliseconds;
 using(master.Image){
  var(w,h)=ProfessionalRawPipeline.OrientedSize(master);if((long)w*h*12>ProfessionalRawResourcePolicy.MasterBudgetBytes)throw new InvalidDataException("Production master budget exceeded");
  // Stream oriented linear and actual production display rows; never borrow pixels after Dispose.
  using var linear=new FileStream(Path.Combine(dest,"production-linear-d65.f32"),FileMode.CreateNew);using var display=new FileStream(Path.Combine(dest,"production-display-srgb.f32"),FileMode.CreateNew);var a=new float[w*3];var b=new float[w*3];long[] zeros=new long[3],ones=new long[3];double[] minimum=[double.MaxValue,double.MaxValue,double.MaxValue],maximum=[double.MinValue,double.MinValue,double.MinValue];
  var sw=Stopwatch.StartNew();for(int y=0;y<h;y++){cancel.Token.ThrowIfCancellationRequested();
   for(int x=0;x<w;x++){
    int rw=master.Image.Width,rh=master.Image.Height;var(px,py)=master.Image.Orientation switch{2=>(rw-1-x,y),3=>(rw-1-x,rh-1-y),4=>(x,rh-1-y),5=>(y,x),6=>(y,rh-1-x),7=>(rw-1-y,rh-1-x),8=>(rw-1-y,x),_=>(x,y)};
    var input=master.Image.Rgb32.Span;int si=(py*rw+px)*3,j=x*3;var v=ProfessionalRawPipeline.RenderOutputPixel(master,definition,x,y);for(int c=0;c<3;c++){a[j+c]=input[si+c];if(!float.IsFinite(a[j+c]))throw new InvalidDataException("Nonfinite master");minimum[c]=Math.Min(minimum[c],a[j+c]);maximum[c]=Math.Max(maximum[c],a[j+c]);if(a[j+c]==0)zeros[c]++;if(a[j+c]==1)ones[c]++;}b[j]=(float)v.X;b[j+1]=(float)v.Y;b[j+2]=(float)v.Z;
   }linear.Write(MemoryMarshal.AsBytes(a.AsSpan()));display.Write(MemoryMarshal.AsBytes(b.AsSpan()));
  }sw.Stop();linear.Flush();display.Flush();linear.Dispose();display.Dispose();var after=Hash(file);if(before!=after)throw new IOException("Source mutated");
  var report=new{version="ProductionNaturalInputV2",status="DECODED",sourceSha256=before,sourceUnchanged=true,width=w,height=h,storedOrientation=1,originalOrientation=master.Image.Orientation,originalWidth=master.Image.Width,originalHeight=master.Image.Height,master.Contract,definition,stages,decodeMs,prepareAndWriteRowsMs=sw.Elapsed.TotalMilliseconds,minimum,maximum,zeroChannels=zeros,oneChannels=ones,productionLinearSha256=Hash(Path.Combine(dest,"production-linear-d65.f32")),productionDisplaySha256=Hash(Path.Combine(dest,"production-display-srgb.f32")),processPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64,limits="Existing production integer LibRawWB+demosaic+matrix clipping retained; not MatrixDecodeResearchV2. Display=unchanged production RenderOutputPixel EV0 once. Row coordinate orientation applied equally."};Report(Path.Combine(dest,"report.json"),report);Console.WriteLine(JsonSerializer.Serialize(report));
 }
}catch(Exception e){var report=new{version="ProductionNaturalInputV2",status="REJECTED_OR_FAILED",sourceSha256=before,stages,error=e.ToString(),elapsedMs=watch.Elapsed.TotalMilliseconds,processPeakWorkingSetBytes=Process.GetCurrentProcess().PeakWorkingSet64};try{Report(Path.Combine(dest,"report.json"),report);}catch(Exception reporting){Console.Error.WriteLine("Original failure: "+e+"\nFailure-report error: "+reporting);}Console.WriteLine(JsonSerializer.Serialize(report));Environment.ExitCode=2;}

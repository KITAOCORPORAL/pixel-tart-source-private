using RAWSelectionAssistant.Core.Services.Projects;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
using Sdcb.LibRaw;

// Tool-only contract. No product serializer or default pipeline registration.
static class ResearchContract
{
 public const string Domain="LibRaw ProPhoto D65 linear float32, nominal 0..1, negatives and >1 retained";
 static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
 static readonly string Root=FindRoot();
 static string FindRoot(){var p=new DirectoryInfo(AppContext.BaseDirectory);while(p is not null){if(File.Exists(Path.Combine(p.FullName,"AGENTS.md")))return p.FullName;p=p.Parent;}throw new InvalidDataException("Research source root required");}
 static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
 static readonly string[] Sources=["tools/color-research-decoder/Program.cs","tools/color-research-decoder/ResearchContract.cs","tools/color-research-decoder/ResearchDecoder.csproj","tools/color-research-decoder/effective-matrix-v1.json","src/RAWSelectionAssistant.Core/Services/RawToJpeg/ProfessionalRawDecoder.cs","src/RAWSelectionAssistant.Core/Services/RawToJpeg/ProfessionalLibRawAdapter.cs","src/RAWSelectionAssistant.Core/Services/Projects/ProfessionalRawPipeline.cs","src/RAWSelectionAssistant.Core/Services/Color/HighBitDepthImageBuffer.cs","src/RAWSelectionAssistant.Core/Services/Color/GraphPixelAllocation.cs","src/RAWSelectionAssistant.Core/Services/Export/TiffExport.cs","src/RAWSelectionAssistant.Core/Services/Color/ControlledIcc/LcmsRgbEngine.cs"];
 static JsonObject SourceManifest(){var o=new JsonObject();foreach(var s in Sources)o[s]=Hash(Path.Combine(Root,s));return o;}
 static JsonObject Dependencies(){var o=new JsonObject();foreach(var p in Directory.GetFiles(AppContext.BaseDirectory,"*.dll",SearchOption.AllDirectories).Order(StringComparer.Ordinal)){o[Path.GetRelativePath(AppContext.BaseDirectory,p).Replace('\\','/')]=Hash(p);}o["runtimeCoreLibSha256"]=Hash(typeof(object).Assembly.Location);o["runtimeFramework"]=RuntimeInformation.FrameworkDescription;o["architecture"]=RuntimeInformation.ProcessArchitecture.ToString();return o;}
 static JsonObject ExpectedEffective(string mode){var n=JsonNode.Parse(File.ReadAllText(Path.Combine(Root,"tools/color-research-decoder/effective-matrix-v1.json")))!.AsObject();if(mode=="headroom")n["HighlightMode"]=1;return n;}
 public static void VerifyLoadedDependencies(){
  Require(RuntimeInformation.ProcessArchitecture==Architecture.X64 && typeof(RawContext).Assembly.GetName().Version==new Version(0,21,1,7),"Unsupported ABI");
  Require(Hash(typeof(RawContext).Assembly.Location)=="6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3","Managed backend changed");
  var native=System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Single(m=>m.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase));
  Require(Hash(native.FileName)=="F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9","Native backend changed");
 }
 public static void ValidateEffective(object actual,string mode){if(!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actual),ExpectedEffective(mode)))throw new InvalidDataException("Effective decode settings changed");}
 static JsonObject Identity(string mode,string hash,object? observation){return new(){["implementationVersion"]="ColorResearchDecoderV2",["implementationSourceSha256"]=SourceManifest(),["dependencies"]=Dependencies(),["sourceSha256"]=hash,["mode"]=mode,["arithmetic"]=mode=="baseline"?"unchanged native integer matrix/clamp then RGB16 normalization by 65535f":"float32 matrix accumulation and dot; divide 65535f; no FMA promise",["matrixVersion"]=mode=="baseline"?"PinnedLibRawIntegerMatrixV1":"LibRawProPhotoD65FloatMatrixV1",["proPhotoD65Coefficients"]=JsonSerializer.SerializeToNode(new double[]{.529317,.330092,.140588,.098368,.873465,.028169,.016879,.117663,.865457}),["decodeSnapshot"]=Snapshot(mode,hash),["outputDecodeSettings"]=ExpectedEffective(mode),["unpackSettings"]=JsonSerializer.SerializeToNode(new{UseRawSpeed=0,UseDngSdk=0,Options=2,ShotSelect=0,Specials=0,MaxRawMemoryMb=2048,SonyArw2PosterizationThr=0,CoolscanNefGamma=1,P4ShotOrder=new int[5],CustomCameraStrings="null"}),["orientation"]=1,["rawFlip"]=0,["layout"]="D800 RGBG uint16, 7378x4924; raw 7424x4924 pitch14848; zero margins; final filters2492765332; black0; no recovery",["wbRule"]="Camera505,256,352,256; native min-normalized except isolated headroom max-normalized; no upstream clipping recovery"};}
 static JsonObject Snapshot(string mode,string hash)=>new(){["effectiveSettings"]=ExpectedEffective(mode),["cameraWb"]=JsonSerializer.SerializeToNode(new double[]{505,256,352,256}),["appliedWb"]=JsonSerializer.SerializeToNode(new double[]{505,256,352,256}),["scaledWb"]=JsonSerializer.SerializeToNode(mode=="headroom"?new float[]{1,256f/505,352f/505,256f/505}:new float[]{505f/256,1,352f/256,1}),["restore"]=mode=="headroom"?505d/256:1,["floatMatrix"]=JsonSerializer.SerializeToNode(new float[]{.888929f,.155704f,-.044635985f,.02428915f,1.3642278f,-.3885149f,.029352998f,-.20289531f,1.1735413f}),["rgbCam"]=JsonSerializer.SerializeToNode(new float[]{1.7815799f,-.61339307f,-.1681868f,0,-.17356461f,1.6453252f,-.4717606f,0,.022766983f,-.44616398f,1.4233971f,0}),["white"]=hash.StartsWith("3A0C")?4095:16383,["black"]=0,["cblackFirst6"]=JsonSerializer.SerializeToNode(new int[6]),["storage"]="RGB little-endian IEEE754 float32; alpha absent; orientation1",["preview"]=JsonSerializer.SerializeToNode(new{ExposureEv=-2,edge=800,Display="unchanged pinned product ProfessionalRawPipeline"})};
 public static void ValidateObservation(object observation,string mode,string hash){
  if(mode=="baseline"){
   var c=(ProfessionalRawContract)observation;ValidateEffective(JsonNode.Parse(c.OutputParametersJson!)!,mode);
   Require(c.Make=="Nikon"&&c.Model=="D800"&&c.SourceSha256==hash&&c.WhiteLevel==(hash.StartsWith("3A0C")?4095:16383),"Baseline identity mismatch");
   Require(c.CameraToRgbMatrix.SequenceEqual(new double[]{(double)1.7815799f,(double)(-.61339307f),(double)(-.1681868f),0,(double)(-.17356461f),(double)1.6453252f,(double)(-.4717606f),0,(double).022766983f,(double)(-.44616398f),(double)1.4233971f,0}),"Baseline matrix mismatch");
   Require(c.CameraWhiteBalance.SequenceEqual(new double[]{505,256,352,256})&&c.AppliedWhiteBalance.SequenceEqual(c.CameraWhiteBalance),"Baseline WB mismatch");return;
  }
  var o=JsonSerializer.SerializeToNode(observation)!;var e=Snapshot(mode,hash);
  Require(o["cblack"]!.AsArray().Take(6).All(x=>x!.GetValue<uint>()==0),"Observed channel black changed");
  foreach(var pair in new[]{("actualOptions","effectiveSettings"),("cameraWb","cameraWb"),("appliedWb","appliedWb"),("scaledWb","scaledWb"),("matrix","floatMatrix"),("rgbCam","rgbCam"),("restore","restore"),("black","black"),("white","white")})
   Require(JsonNode.DeepEquals(o[pair.Item1],e[pair.Item2]),"Observed algorithm differs: "+pair.Item1);
 }
 public static void CallbackLifetime(Action install,Action body,Action detach,Action clear){try{install();body();}finally{try{detach();}finally{clear();}}}
 public static JsonObject Create(string mode,string hash,string pixelHash,object? observation)=>new(){["version"]=mode=="headroom"?"WbHeadroomResearchV2":mode=="baseline"?"BaselineResearchV2":"MatrixDecodeResearchV2",["mode"]=mode,["defaultEnabled"]=false,["sourceSha256"]=hash,["backendHash"]="F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9",["width"]=7378,["height"]=4924,["domain"]=Domain,["exposure"]=-2,["rawRecovery"]=false,["pixelHash"]=pixelHash,["algorithmIdentity"]=Identity(mode,hash,observation)};
 // Legacy V1 is archive-read-only, not an algorithm reconstruction contract.
 public static void ValidateLegacy(JsonElement r){
  var allowed=new[]{"version","mode","defaultEnabled","sourceSha256","backendHash","width","height","domain","exposure","rawRecovery","pixelHash"};
  Require(r.EnumerateObject().All(p=>allowed.Contains(p.Name)),"Unknown legacy field");
  Require(r.EnumerateObject().Select(p=>p.Name).Distinct().Count()==r.EnumerateObject().Count(),"Duplicate legacy field");
  Require(r.GetProperty("rawRecovery").ValueKind==JsonValueKind.False,"Legacy recovery must be false");
 }
 public static float[] CopyOwned(HighBitDepthImageBuffer image)=>image.Rgb32.ToArray();
 static void Require(bool ok,string text){if(!ok)throw new InvalidDataException(text);}
 public static bool ReopenV2(string folder,string dest,CancellationToken token){
  var r=JsonNode.Parse(File.ReadAllText(Path.Combine(folder,"recipe.json")))!.AsObject();var v=r["version"]?.GetValue<string>();
  if(v is not("MatrixDecodeResearchV2" or "BaselineResearchV2" or "WbHeadroomResearchV2" or "SyntheticReopenFixtureV1"))return false;
  Require(!Directory.Exists(dest),"Destination exists");
  var fixture=v=="SyntheticReopenFixtureV1";var mode=r["mode"]!.GetValue<string>();var hash=r["sourceSha256"]!.GetValue<string>();
  Require(mode is "baseline" or "matrix" or "headroom","Unknown mode");
  Require(fixture||hash is "3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5" or "593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A","Unadmitted source");
  var expected=Create(mode,hash,r["pixelHash"]!.GetValue<string>(),null);
  if(fixture){expected["version"]=v;expected["width"]=8;expected["height"]=4;}
  Require(JsonNode.DeepEquals(r,expected),"Incomplete, unknown or mismatched V2 identity");
  int w=r["width"]!.GetValue<int>(),h=r["height"]!.GetValue<int>();var path=Path.Combine(folder,"master.f32");
  using(var f=File.OpenRead(path)){Require(f.Length==checked((long)w*h*12)&&Convert.ToHexString(SHA256.HashData(f))==r["pixelHash"]!.GetValue<string>(),"Saved master hash/length mismatch");}
  token.ThrowIfCancellationRequested();var data=new float[checked(w*h*3)];using(var f=File.OpenRead(path))f.ReadExactly(MemoryMarshal.AsBytes(data.AsSpan()));
  Require(data.All(float.IsFinite),"Nonfinite saved master");
  // Hash the exact loaded bytes too: do not trust a separate hash/read TOCTOU window.
  Require(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(data.AsSpan())))==r["pixelHash"]!.GetValue<string>(),"Loaded master changed");
  Render(data,w,h,hash,dest,token);
  using var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"report.json")));using var fresh=JsonDocument.Parse(File.ReadAllText(Path.Combine(dest,"report.json")));
  Require(old.RootElement.GetProperty("previewHash").GetString()==fresh.RootElement.GetProperty("previewHash").GetString(),"Preview mismatch");
  Require(old.RootElement.GetProperty("exportPixelHash").GetString()==fresh.RootElement.GetProperty("exportPixelHash").GetString(),"Export mismatch");
  File.WriteAllText(Path.Combine(dest,"reopen.json"),JsonSerializer.Serialize(new{version="ResearchReopenGateV2",freshProcess=true,synthetic=fixture,fullDomain=true,width=w,height=h,pixelHash=r["pixelHash"]!.GetValue<string>(),finite=true,previewExact=true,exportExact=true,sourceDecodedAgain=false},Pretty));return true;
 }
 public static void Render(float[] data,int w,int h,string hash,string dest,CancellationToken token){
  token.ThrowIfCancellationRequested();Directory.CreateDirectory(dest);using var image=new HighBitDepthImageBuffer(w,h,data,"32f",ProfessionalRawPipeline.WorkingSpace,1);var d=new ProfessionalRawDefinition(ExposureEv:-2);var master=new ProfessionalRawMaster(image,new("ResearchAdapterV2","Nikon","D800",hash,d.DecodeKey,[],[],[],16383));
  using var p=ProfessionalRawPipeline.RenderPreview(master,d,800,token);var ph=Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(p.Rgb32.Span)));
  using(var f=new FileStream(Path.Combine(dest,"preview-display.f32"),FileMode.CreateNew))f.Write(MemoryMarshal.AsBytes(p.Rgb32.Span));
  using var cms=new LcmsRgbEngine();var profile=cms.StandardProfile("sRGB").Bytes();using(var f=new FileStream(Path.Combine(dest,"display-rgb16.tif"),FileMode.CreateNew))TiffExport.WriteRgb48Rows(f,w,h,(y,row)=>{token.ThrowIfCancellationRequested();ProfessionalRawPipeline.FillRgb16Row(master,d,y,row);},new(IccProfile:profile,Software:"Isolated research V2"));
  TiffStreamValidation t;using(var f=File.OpenRead(Path.Combine(dest,"display-rgb16.tif")))t=TiffStreamValidator.Validate(f);
  File.WriteAllText(Path.Combine(dest,"report.json"),JsonSerializer.Serialize(new{previewHash=ph,exportPixelHash=t.PixelSha256},Pretty));
 }
 public static void WriteFixture(string dest){Require(!Directory.Exists(dest),"Destination exists");var data=Enumerable.Range(0,96).Select(i=>(i-12)/48f).ToArray();var hash=new string('0',64);var r=Create("matrix",hash,Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(data.AsSpan()))),null);r["version"]="SyntheticReopenFixtureV1";r["width"]=8;r["height"]=4;Render(data,8,4,hash,dest,default);File.WriteAllText(Path.Combine(dest,"recipe.json"),r.ToJsonString(Pretty));using var f=new FileStream(Path.Combine(dest,"master.f32"),FileMode.CreateNew);f.Write(MemoryMarshal.AsBytes(data.AsSpan()));}
 public static void TestRecordedObservation(){
  var doc=JsonNode.Parse(File.ReadAllText(Path.Combine(Root,"docs/research/color-engine-20261029/round3/matrix3-report.json")))!;
  // V1 report omitted some identity fields; this is a check against archived
  // stage metadata, not a claim that old recipe already contained them.
  var ob=doc["observation"]!;var e=Snapshot("matrix",doc["recipe"]!["sourceSha256"]!.GetValue<string>());
  ob["cblack"]=JsonSerializer.SerializeToNode(new uint[6]);
  foreach(var key in new[]{"cameraWb","appliedWb","rgbCam","black","white"})ob[key]=e[key]?.DeepClone();
  ValidateObservation(ob,"matrix",doc["recipe"]!["sourceSha256"]!.GetValue<string>());
  var wrong=doc["observation"]!.DeepClone();wrong["appliedWb"]![0]=504;
  try{ValidateObservation(wrong,"matrix",doc["recipe"]!["sourceSha256"]!.GetValue<string>());throw new Exception("WB accepted");}catch(InvalidDataException){}
  Console.WriteLine("recorded-effective-settings-and-matrix PASS; missing V1 fields supplied from frozen D800 contract; not a new RAW decode");
 }
 public static void TestLifecycle(){using var scope=GraphPixelAllocation.Enter();var a=GraphPixelAllocation.Allocate(3);a[0]=.25f;var b=new HighBitDepthImageBuffer(1,1,a);var copy=CopyOwned(b);b.Dispose();var reused=GraphPixelAllocation.Allocate(3);Require(ReferenceEquals(a,reused),"Pool reuse not exercised");reused[0]=99;Require(copy[0]==.25f,"Disposed backing leaked");try{_=b.Rgb32;throw new Exception("Disposed access allowed");}catch(ObjectDisposedException){}using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{Render(copy,1,1,new string('0',64),Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()),cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}foreach(var fault in new[]{"install","body","detach"}){bool cleared=false,detached=false;try{CallbackLifetime(()=>{if(fault=="install")throw new IOException();},()=>{if(fault=="body")throw new IOException();},()=>{detached=true;if(fault=="detach")throw new IOException();},()=>cleared=true);}catch(IOException){}Require(cleared&&detached,"Callback cleanup failed");}Console.WriteLine("owned-copy/pool-reuse/disposed-access/pre-cancel/callback-exception-cleanup PASS");}
}

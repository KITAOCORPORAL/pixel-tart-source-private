using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using Sdcb.LibRaw;
namespace RAWSelectionAssistant.Services.Diagnostics;
public static class XTransStableDiagnostic
{
 public const string CandidateId="XTransStableInputSeparateRgbV1/157BBA02166BDB78E171154E1F4B03AE58F7DF97A110282B37AB3B5988FDC67F";
 public static bool Compiled {
  get {
#if XTRANS_STABLE_INPUT_V1_DIAGNOSTIC
   return true;
#else
   return false;
#endif
  }
 }
 public static bool Enabled=>Compiled&&Environment.GetEnvironmentVariable("PIXEL_TART_XTRANS_STABLE_V1")=="1";
 private static DiagnosticRun? currentRun;
 public static DiagnosticRun CurrentRun=>currentRun??=DiagnosticRun.Open();
 public static string Status=>Enabled?"隔离诊断 · X-Trans stable-input V1 · 独立缓存":"";
 public static IRawDecoder Create(){
#if XTRANS_STABLE_INPUT_V1_DIAGNOSTIC
  if(Enabled)return new XTransStableDiagnosticDecoder(new LibRawDecoder(),CurrentRun);
#endif
  return new LibRawDecoder();
 }
 public sealed record FileIdentity(string Path,string Sha256);
 public sealed record Input(string Path,string Sha256,bool XTrans);
 public sealed record Plan(int Version,string Task,string ProductSourceSha,string CacheRoot,string EvidenceRoot,string LedgerRoot,string WorkerDll,string Dotnet,string CandidateNativeSha256,string BindingSha256,string DefaultNativeSha256,FileIdentity[] Files,Input[] Inputs,Dictionary<string,int> Steps,int MaximumHeavyDecodes){public bool AuthorizedForHeavyDecode {get;init;}}
 public sealed class DiagnosticRun
 {
  private static readonly object TraceGate=new();public Plan Manifest{get;}public string Step{get;}public string PlanPath{get;}
  private DiagnosticRun(Plan p,string step,string path){Manifest=p;Step=step;PlanPath=path;}
  public static DiagnosticRun Open(){if(!Compiled)throw new NotSupportedException("Candidate not compiled");var path=Environment.GetEnvironmentVariable("PIXEL_TART_XTRANS_PLAN")??throw new InvalidDataException("Diagnostic run manifest required");var step=Environment.GetEnvironmentVariable("PIXEL_TART_XTRANS_STEP")??throw new InvalidDataException("Diagnostic step required");var bytes=File.ReadAllBytes(path);DiagnosticRawRgbCache.RejectDuplicateKeys(bytes);var p=JsonSerializer.Deserialize<Plan>(bytes)!;RawDiagnosticTaskAuthorization.Validate(p.Version,p.Task,p.AuthorizedForHeavyDecode);if(p.MaximumHeavyDecodes!=6||!p.Steps.ContainsKey(step)||!Path.IsPathFullyQualified(p.CacheRoot)||!Path.IsPathFullyQualified(p.EvidenceRoot)||!Path.IsPathFullyQualified(p.LedgerRoot))throw new InvalidDataException("Unapproved diagnostic plan");foreach(var f in p.Files)if(!Hash(f.Path).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Diagnostic frozen file drift: "+f.Path);Directory.CreateDirectory(p.EvidenceRoot);return new(p,step,path);}
  public static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
  public Input ValidateInput(string path){var full=Path.GetFullPath(path);var input=Manifest.Inputs.SingleOrDefault(x=>Path.GetFullPath(x.Path).Equals(full,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException("RAW is outside this regression input allowlist");if(!Hash(full).Equals(input.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Authorized input SHA drift");return input;}
  public void Trace(string kind,object value){lock(TraceGate){var line=JsonSerializer.Serialize(new{atUtc=DateTimeOffset.UtcNow,task=Manifest.Task,step=Step,pid=Environment.ProcessId,kind,value});File.AppendAllText(Path.Combine(Manifest.EvidenceRoot,Step+"-"+Environment.ProcessId+".jsonl"),line+"\n");}}
  public string Reserve(Input input,string identity){Directory.CreateDirectory(Manifest.LedgerRoot);var lockPath=Path.Combine(Manifest.LedgerRoot,"reserve.lock");using var gate=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);if(File.Exists(Path.Combine(Manifest.LedgerRoot,"STOP.json")))throw new InvalidDataException("Diagnostic batch stopped after failure");var records=Directory.GetFiles(Manifest.LedgerRoot,"*.consumed").Select(p=>JsonDocument.Parse(File.ReadAllText(p)).RootElement.Clone()).ToArray();if(records.Length>=Manifest.MaximumHeavyDecodes||records.Count(r=>r.GetProperty("step").GetString()==Step)>=Manifest.Steps[Step])throw new InvalidDataException("No remaining decode allowance for this product path");var path=Path.Combine(Manifest.LedgerRoot,Guid.NewGuid().ToString("N")+".consumed");using var f=new FileStream(path,FileMode.CreateNew);JsonSerializer.Serialize(f,new{task=Manifest.Task,step=Step,maximum=Manifest.MaximumHeavyDecodes,stepLimit=Manifest.Steps[Step],inputSha=input.Sha256,identity,planSha=Hash(PlanPath),reservationId=Path.GetFileNameWithoutExtension(path),failedAttemptsConsume=true,atUtc=DateTimeOffset.UtcNow});f.Flush(true);Trace("DecodeReserved",new{path,inputSha=input.Sha256,identity});return path;}
  public void Stop(Exception e){Trace("DecodeFailed",new{error=e.ToString()});var path=Path.Combine(Manifest.LedgerRoot,"STOP.json");try{using var f=new FileStream(path,FileMode.CreateNew);JsonSerializer.Serialize(f,new{Manifest.Task,Step,error=e.Message});f.Flush(true);}catch(IOException){} }
 }
}
public sealed class XTransStableDiagnosticDecoder(IRawDecoder original,XTransStableDiagnostic.DiagnosticRun run):IRawDecoder
{
 private static readonly SemaphoreSlim Gate=new(1,1);private readonly DiagnosticRawRgbCache cache=new(run.Manifest.CacheRoot,run.Trace);private string? defaultIdentity;
 public RawDecoderCapability GetCapability()=>original.GetCapability();
 public async Task<RawDecodedImage> DecodeAsync(string sourcePath,RawToJpegOptions options,CancellationToken token=default)
 {
  await Gate.WaitAsync(token);try{
   var input=run.ValidateInput(sourcePath);var capability=original.GetCapability();if(!capability.IsAvailable)throw new InvalidDataException("Existing default decoder unavailable");var module=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>m.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase));var defaultSha=XTransStableDiagnostic.DiagnosticRun.Hash(module.FileName);if(defaultSha!=run.Manifest.DefaultNativeSha256)throw new InvalidDataException("Parent default native must remain unchanged");defaultIdentity??="ExistingDefault/"+defaultSha;
   int filters,colors;using(var header=RawContext.OpenFile(sourcePath)){filters=unchecked((int)header.ImageParams.Filters);colors=header.ImageParams.Colors;}run.Trace("HeaderOnlyProbe",new{input.Sha256,filters,colors,nativeSha=defaultSha,noUnpack=true});
   if(filters!=9||colors!=3||!input.XTrans||options.DecodeMode!=RawDecodeMode.ProfessionalDecode){var ticket=run.Reserve(input,defaultIdentity);var timer=Stopwatch.StartNew();try{var value=await original.DecodeAsync(sourcePath,options,token);run.Trace("DefaultDecodeCompleted",new{ticket,identity=defaultIdentity,elapsedMs=timer.Elapsed.TotalMilliseconds,value.Width,value.Height,value.BitsPerChannel});return value;}catch(Exception e){run.Stop(e);throw;}}
   var identity=XTransStableDiagnostic.CandidateId;var hit=await Task.Run(()=>cache.Read(input.Sha256,identity,options,token),token);if(hit is not null)return hit;
   var reservation=run.Reserve(input,identity);try{var value=await DecodeWorker(input,options,reservation,token);await Task.Run(()=>cache.Write(input.Sha256,identity,options,value,token),token);return value;}catch(Exception e){run.Stop(e);throw;}
  }finally{Gate.Release();}
 }
 private async Task<RawDecodedImage> DecodeWorker(XTransStableDiagnostic.Input input,RawToJpegOptions options,string reservation,CancellationToken token)
 {
#if XTRANS_STABLE_INPUT_V1_DIAGNOSTIC
  var folder=Path.Combine(run.Manifest.EvidenceRoot,"worker-"+Path.GetFileNameWithoutExtension(reservation));Directory.CreateDirectory(folder);var request=Path.Combine(folder,"request.json");File.WriteAllText(request,JsonSerializer.Serialize(new{task=run.Manifest.Task,step=run.Step,input,reservation,options,nativeSha256=run.Manifest.CandidateNativeSha256,bindingSha256=run.Manifest.BindingSha256,outputRoot=folder,planPath=run.PlanPath,planSha=XTransStableDiagnostic.DiagnosticRun.Hash(run.PlanPath)}));
  var si=new ProcessStartInfo(run.Manifest.Dotnet){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(run.Manifest.WorkerDll)!};foreach(var a in new[]{run.Manifest.WorkerDll,"--decode-once",request})si.ArgumentList.Add(a);using var process=Process.Start(si)??throw new IOException("Candidate worker launch failed");var clock=Stopwatch.StartNew();long peak=0,privatePeak=0,combinedWorkingPeak=0,combinedPrivatePeak=0;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();try{while(!process.WaitForExit(50)){token.ThrowIfCancellationRequested();process.Refresh();peak=Math.Max(peak,process.PeakWorkingSet64);privatePeak=Math.Max(privatePeak,process.PrivateMemorySize64);using(var parent=Process.GetCurrentProcess()){parent.Refresh();combinedWorkingPeak=Math.Max(combinedWorkingPeak,parent.WorkingSet64+process.WorkingSet64);combinedPrivatePeak=Math.Max(combinedPrivatePeak,parent.PrivateMemorySize64+process.PrivateMemorySize64);}if(peak>2147483648||privatePeak>2147483648||clock.Elapsed.TotalSeconds>600)throw new IOException("Diagnostic worker exceeded2GiB/600s");}}catch{if(!process.HasExited)process.Kill(true);throw;}
  File.WriteAllText(Path.Combine(folder,"stdout.log"),await stdout);File.WriteAllText(Path.Combine(folder,"stderr.log"),await stderr);run.Trace("CandidateWorkerExited",new{reservation,pid=process.Id,exitCode=process.ExitCode,elapsedMs=clock.Elapsed.TotalMilliseconds,peakWorkingSetBytes=peak,peakPrivateBytes50ms=privatePeak,combinedWorkingSetPeak50ms=combinedWorkingPeak,combinedPrivateBytesPeak50ms=combinedPrivatePeak});if(process.ExitCode!=0)throw new IOException("Candidate worker failed; see frozen receipt");
  var response=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"result.json"))).RootElement;if(response.GetProperty("status").GetString()!="completed"||response.GetProperty("nativeSha256").GetString()!=run.Manifest.CandidateNativeSha256)throw new InvalidDataException("Candidate response identity");var meta=JsonSerializer.Deserialize<RawImageMetadata>(response.GetProperty("metadata"))!;var width=response.GetProperty("width").GetInt32();var height=response.GetProperty("height").GetInt32();var p=Path.Combine(folder,"decoded.rgb48");if(new FileInfo(p).Length!=checked((long)width*height*6)||XTransStableDiagnostic.DiagnosticRun.Hash(p)!=response.GetProperty("pixelSha256").GetString())throw new InvalidDataException("Candidate decoded pixel contract");var values=new ushort[checked(width*height*3)];using(var stream=File.OpenRead(p)){var block=new byte[1048576];for(int at=0;at<values.Length*2;){var n=Math.Min(block.Length,values.Length*2-at);stream.ReadExactly(block.AsSpan(0,n));Buffer.BlockCopy(block,0,values,at,n);at+=n;}}return RawDecodedImage.FromRgb48(width,height,width*6,values,meta);
#else
  throw new NotSupportedException("Candidate worker only exists in an explicit diagnostic build");
#endif
 }
}

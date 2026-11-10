using System.Diagnostics;
namespace RAWSelectionAssistant.Services.Diagnostics;
public sealed class DiagnosticProductOperation : IDisposable
{
 private readonly XTransStableDiagnostic.DiagnosticRun? run;private readonly Stopwatch clock=Stopwatch.StartNew();private readonly string name;private bool completed;private bool disposed;private readonly object gate=new();private Timer? sampler;private long peakWorking,peakPrivate;private int samples;
 private DiagnosticProductOperation(string name){this.name=name;if(XTransStableDiagnostic.Enabled){run=XTransStableDiagnostic.CurrentRun;run.Trace("ProductPathBegin",new{name});Sample();sampler=new Timer(_=>Sample(),null,50,50);}}
 private void Sample(){lock(gate){if(disposed)return;using var p=Process.GetCurrentProcess();p.Refresh();peakWorking=Math.Max(peakWorking,p.WorkingSet64);peakPrivate=Math.Max(peakPrivate,p.PrivateMemorySize64);samples++;}}
 public static DiagnosticProductOperation Begin(string name)=>new(name);
 public void Complete(){completed=true;}
 public void Dispose(){if(disposed)return;if(run is null){disposed=true;return;}sampler?.Dispose();Sample();lock(gate){disposed=true;}if(run is null)return;using var p=Process.GetCurrentProcess();p.Refresh();run.Trace("ProductPathEnd",new{name,completed,elapsedMs=clock.Elapsed.TotalMilliseconds,pathLocalWorkingSetPeak50ms=peakWorking,pathLocalPrivatePeak50ms=peakPrivate,samples,lifetimeOsPeakWorkingSetBytes=p.PeakWorkingSet64,privateBytesAtEnd=p.PrivateMemorySize64,timingScope="Product command after source/destination dialog; includes decode/render/encode and own metadata"});if(!completed)run.Stop(new System.IO.IOException("Product regression path did not complete: "+name));}
}

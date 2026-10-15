using System.Diagnostics;
using System.IO;
using System.Text.Json;
internal sealed class WorkflowMemory:IDisposable
{
    static string stage="startup";readonly List<object> samples=[];readonly Stopwatch clock=Stopwatch.StartNew();readonly CancellationTokenSource stop=new();readonly Task sampler;readonly string? destination;
    WorkflowMemory(string? path){destination=path;sampler=Task.Run(async()=>{while(!stop.IsCancellationRequested){Sample();try{await Task.Delay(20,stop.Token);}catch(OperationCanceledException){}}});}
    void Sample(){using var p=Process.GetCurrentProcess();p.Refresh();lock(samples)samples.Add(new{wallMs=clock.Elapsed.TotalMilliseconds,stage,managed=GC.GetTotalMemory(false),allocated=GC.GetTotalAllocatedBytes(),workingSet=p.WorkingSet64,privateBytes=p.PrivateMemorySize64,gc=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},cpuMs=p.TotalProcessorTime.TotalMilliseconds});}
    public static WorkflowMemory Begin(string[] args)=>new(Environment.GetEnvironmentVariable("KITAO_ICC_MEMORY_LOG"));
    public static void Stage(string name)=>stage=name;
    public void Dispose(){stage="natural-release-no-GC";if(destination!=null)Thread.Sleep(10000);stop.Cancel();sampler.GetAwaiter().GetResult();Sample();if(destination!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);File.WriteAllText(destination,JsonSerializer.Serialize(new{samples,nativeOwnership="LCMS contexts/transforms finally-released; WIC/allocator attribution UNAVAILABLE; WS is measured independently",forcedGc=false},new JsonSerializerOptions{WriteIndented=true}));}stop.Dispose();}
}

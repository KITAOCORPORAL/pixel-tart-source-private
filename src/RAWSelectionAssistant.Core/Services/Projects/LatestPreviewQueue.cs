namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>One in-flight immutable snapshot plus one replaceable latest pending snapshot.
/// Same-context input never cancels useful work; invalidation cancels, then waits for retirement before starting new work.</summary>
public sealed class LatestPreviewQueue<T,TResult>(Func<T,CancellationToken,Task<TResult>> render,Action<long,T,TResult> publish,Action<Exception>? failure=null,bool latestOnly=false)
{
 private readonly object gate=new();private (long Revision,long Context,T Snapshot)? pending;private CancellationTokenSource? active;private Task work=Task.CompletedTask;private long sequence,published,context;private bool running;
 public long Submit(T snapshot){lock(gate){var revision=++sequence;pending=(revision,context,snapshot);if(latestOnly)active?.Cancel();if(!running){running=true;work=Run();}return revision;}}
 public Task Work{get{lock(gate)return work;}}
 public long Published{get{lock(gate)return published;}}
 public void Invalidate(){lock(gate){context++;pending=null;active?.Cancel();}}
 private async Task Run()
 {
  await Task.Yield();while(true){(long Revision,long Context,T Snapshot) request;CancellationTokenSource cts;lock(gate){if(pending is null){running=false;return;}request=pending.Value;pending=null;cts=new();active=cts;}
   try{var result=await render(request.Snapshot,cts.Token);lock(gate){if(request.Context==context&&!cts.IsCancellationRequested&&request.Revision>published&&(!latestOnly||request.Revision==sequence)){published=request.Revision;publish(request.Revision,request.Snapshot,result);}}}
   catch(OperationCanceledException)when(cts.IsCancellationRequested){}catch(Exception error){failure?.Invoke(error);}finally{lock(gate){if(ReferenceEquals(active,cts))active=null;}cts.Dispose();}
  }
 }
}

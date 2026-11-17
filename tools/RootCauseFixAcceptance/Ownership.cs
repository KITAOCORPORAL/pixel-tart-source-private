using System.Text.Json;
using RAWSelectionAssistant.Utilities;
class Program
{
 static int Main(string[] args)
 {
  var role=args[0];var name=args[1];var root=Path.GetFullPath(args[2]);Directory.CreateDirectory(root);
  void Save(string n,object v)=>File.WriteAllText(Path.Combine(root,n+".json"),JsonSerializer.Serialize(v));
  if(role=="unowned")
  {
   using var existing=new Mutex(false,$"Local\\{name}-Mutex");using var manager=new SingleInstanceManager(name);
   if(!manager.TryAcquire())throw new Exception("Existing unowned kernel object must be acquired");Save(role,new{ok=true,outcome=manager.LastAcquisition.ToString()});return 0;
  }
  if(role=="abandoned")
  {
   Mutex? retained=null;var thread=new Thread(()=>{retained=new Mutex(false,$"Local\\{name}-Mutex");retained.WaitOne();});thread.Start();thread.Join();
   using var manager=new SingleInstanceManager(name);if(!manager.TryAcquire())throw new Exception("Abandoned mutex not acquired");Save(role,new{ok=true,outcome=manager.LastAcquisition.ToString()});retained!.Dispose();return 0;
  }
  using var current=new SingleInstanceManager(name);
  current.ActivationRequested+=()=>{Save("activation-"+Environment.ProcessId,new{pid=Environment.ProcessId});if(role=="owner-ack")current.AcknowledgeActivation();};
  bool acquired=current.TryAcquire();Save(role+"-"+Environment.ProcessId,new{pid=Environment.ProcessId,acquired,outcome=current.LastAcquisition.ToString()});
  if(role.StartsWith("owner"))
  {
   if(!acquired)throw new Exception("Owner failed acquisition");File.WriteAllText(Path.Combine(root,"unsaved-"+Environment.ProcessId+".txt"),"in-memory-work-not-committed");
   while(!File.Exists(Path.Combine(root,"finish-"+Environment.ProcessId)))Thread.Sleep(30);
  }
  return 0;
 }
}

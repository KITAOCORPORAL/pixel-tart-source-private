using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Utilities;
using RAWSelectionAssistant.Core.Models;
class Program
{
 static int Main(string[] args)
 {
  var role=args[0];var root=Path.GetFullPath(args[1]);var nonce=args[2];Directory.CreateDirectory(root);
  if(!nonce.All(char.IsAsciiLetterOrDigit)||nonce.Length<8)throw new InvalidOperationException("Unique probe namespace required");
  var instance=nonce+"-"+(role=="ownerB"?"rootB":"rootA");
  using var self=Process.GetCurrentProcess();var executable=self.MainModule!.FileName!;
  if(!self.ProcessName.StartsWith("KitaoOwnershipProbe_",StringComparison.Ordinal))throw new InvalidOperationException("Probe-only executable name required");
  void Save(string suffix,object value)=>File.WriteAllText(Path.Combine(root,role+suffix+".json"),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
  if(role=="recoverA")
  {
   var owned=JsonSerializer.Deserialize<int[]>(File.ReadAllText(Path.Combine(root,"OWNED_PIDS.json")))!;
   var all=Process.GetProcessesByName(self.ProcessName);
   try{if(all.Any(p=>!owned.Contains(p.Id)&&p.Id!=self.Id||!string.Equals(p.MainModule!.FileName,executable,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Foreign process in probe name; do not invoke recovery");}
   finally{foreach(var p in all)p.Dispose();}
  }
  using var manager=new SingleInstanceManager(instance);
  manager.ActivationRequested+=()=>Save("-activation",new{pid=self.Id,instance,acknowledged=false,reason="Synthetic owner has no ready UI; no activation acknowledgment"});
  var acquired=manager.TryAcquire();
  Save("-acquire",new{pid=self.Id,role,instance,acquired,processName=self.ProcessName,exe=executable,productName=FileVersionInfo.GetVersionInfo(executable).ProductName,expectedProduct=Branding.ProductName,mainWindow=self.MainWindowHandle.ToInt64(),utc=DateTimeOffset.UtcNow});
  if(role=="recoverA")return acquired?0:2;
  var until=DateTime.UtcNow.AddSeconds(45);while(DateTime.UtcNow<until&&!File.Exists(Path.Combine(root,"FINISH")))Thread.Sleep(100);
  return acquired?0:3;
 }
}

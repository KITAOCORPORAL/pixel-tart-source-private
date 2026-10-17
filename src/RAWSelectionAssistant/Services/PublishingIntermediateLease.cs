using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace RAWSelectionAssistant.Services;
public sealed record PublishingIntermediateFile(string Path,string? Sha256);
public sealed record PublishingIntermediateManifest(Guid Id,string Directory,string State,int ProcessId,DateTimeOffset CreatedAt,IReadOnlyList<PublishingIntermediateFile> Files);
/// <summary>Only exact files created by this owned task are eligible for release. Never recursively deletes directories.</summary>
public sealed class PublishingIntermediateLease:IDisposable
{
 public string DirectoryPath {get;}
 private readonly string manifestPath;
 private readonly List<PublishingIntermediateFile> files=[];
 private readonly Guid id=Guid.NewGuid();
 private readonly DateTimeOffset created=DateTimeOffset.UtcNow;
 private FileStream? gate;
 public PublishingIntermediateLease(string? root=null)
 {
  var parent=root??Path.Combine(Path.GetTempPath(),"PixelTartPublishing");Directory.CreateDirectory(parent);DirectoryPath=Path.Combine(parent,id.ToString("N"));Directory.CreateDirectory(DirectoryPath);
  manifestPath=Path.Combine(DirectoryPath,"owner.json");gate=new FileStream(Path.Combine(DirectoryPath,"lease.lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);Write("Preparing");
 }
 public void OwnNewFile(string path)
 {
  path=Path.GetFullPath(path);if(!path.StartsWith(DirectoryPath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||File.Exists(path))throw new IOException("Not a new task-owned intermediate");
  files.Add(new(path,null));Write("Preparing");
 }
 public void Complete(string path)
 {
  for(int i=0;i<files.Count;i++)if(files[i].Path==Path.GetFullPath(path)){using var f=File.OpenRead(path);files[i]=files[i] with{Sha256=Convert.ToHexString(SHA256.HashData(f))};Write("Preparing");return;}
  throw new IOException("Intermediate not owned");
 }
 public void CompleteExisting()
 {foreach(var file in files.ToArray())if(File.Exists(file.Path))Complete(file.Path);}
 public void Handoff(){Write("RetainedForPublishingAndRetry");}
 private void Write(string state)
 {
  var temp=manifestPath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,new PublishingIntermediateManifest(id,DirectoryPath,state,Environment.ProcessId,created,files.ToArray()),new JsonSerializerOptions{WriteIndented=true});file.Flush(true);}File.Move(temp,manifestPath,true);}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public string ManifestPath=>manifestPath;
 public void Dispose(){gate?.Dispose();gate=null;}
 public static IReadOnlyList<string> Release(string manifestPath)
 {
  manifestPath=Path.GetFullPath(manifestPath);var directory=Path.GetDirectoryName(manifestPath)!;
  if(Path.GetFileName(manifestPath)!="owner.json")throw new InvalidDataException("Explicit owner manifest required");
  RejectLink(directory);RejectLink(manifestPath);using var gate=new FileStream(Path.Combine(directory,"lease.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
  var owner=JsonSerializer.Deserialize<PublishingIntermediateManifest>(File.ReadAllText(manifestPath))??throw new InvalidDataException("Missing owner");
  if(owner.Directory!=directory||Path.GetFileName(directory)!=owner.Id.ToString("N"))throw new InvalidDataException("Owner identity mismatch");
  var removed=new List<string>();
  foreach(var file in owner.Files)
  {
   var path=Path.GetFullPath(file.Path);if(!path.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Owned file escaped directory");RejectLink(path);
   if(!File.Exists(path))continue;
   if(file.Sha256 is null)throw new IOException("Unverified intermediate retained for review");
   if(file.Sha256 is not null){using var stream=File.OpenRead(path);if(Convert.ToHexString(SHA256.HashData(stream))!=file.Sha256)throw new IOException("Intermediate changed; refusing to delete");}
   removed.Add(path);
  }
  foreach(var path in removed)File.Delete(path);
  // Keep a durable released record. Unknown files/subdirectories are deliberately untouched.
  var temp=manifestPath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllText(temp,JsonSerializer.Serialize(owner with{State="Released"},new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,manifestPath,true);}finally{if(File.Exists(temp))File.Delete(temp);}
  return removed;
 }
 private static void RejectLink(string path)
 {for(var p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Publishing lease does not follow links");}
}

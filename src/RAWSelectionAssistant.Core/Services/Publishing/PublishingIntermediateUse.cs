using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Publishing;
/// <summary>Read leases prevent explicit intermediate cleanup while preview/export/retry consumes the source.</summary>
public static class PublishingIntermediateUse
{
 public static IDisposable Acquire(IEnumerable<string> paths)
 {
  var held=new List<FileStream>();
  try
  {
   foreach(var directory in paths.Select(path=>Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(path)))).Where(x=>x is not null).Distinct(StringComparer.OrdinalIgnoreCase))
   {
    var manifest=Path.Combine(directory!,"owner.json");var gate=Path.Combine(directory!,"lease.lock");if(!File.Exists(manifest)||!File.Exists(gate))continue;
    using var doc=JsonDocument.Parse(File.ReadAllText(manifest));var root=doc.RootElement;
    // Not every owner.json belongs to this mechanism. Do not lock arbitrary unrelated files.
    if(!root.TryGetProperty("Id",out var id)||!id.TryGetGuid(out var identity)||!root.TryGetProperty("Directory",out var location)||location.GetString()!=directory||Path.GetFileName(directory)!=identity.ToString("N"))continue;
    if((File.GetAttributes(directory!)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Publishing intermediate symlink");
    held.Add(new FileStream(gate,FileMode.Open,FileAccess.Read,FileShare.Read));
   }
   return new Lease(held);
  }
  catch{foreach(var f in held)f.Dispose();throw;}
 }
 private sealed class Lease(List<FileStream> held):IDisposable{public void Dispose(){foreach(var file in held)file.Dispose();held.Clear();}}
}

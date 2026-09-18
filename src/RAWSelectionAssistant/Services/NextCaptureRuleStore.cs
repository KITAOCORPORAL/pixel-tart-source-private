using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Tethering;
using RAWSelectionAssistant.Core.Utilities;
namespace RAWSelectionAssistant.Services;
public sealed class NextCaptureRuleStore(string? path=null)
{
    private readonly string _path=path??Path.Combine(AppDataPaths.DataDirectory,"next-capture-rule.json");
    public async Task<NextCaptureRule> LoadAsync(CancellationToken token=default){try{if(!File.Exists(_path))return new();await using var stream=File.OpenRead(_path);return await JsonSerializer.DeserializeAsync<NextCaptureRule>(stream,cancellationToken:token)??new();}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){return new();}}
    public async Task SaveAsync(NextCaptureRule value,CancellationToken token=default){var full=Path.GetFullPath(_path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";try{await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){await JsonSerializer.SerializeAsync(stream,value,cancellationToken:token);await stream.FlushAsync(token);stream.Flush(true);}File.Move(temporary,full,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
}

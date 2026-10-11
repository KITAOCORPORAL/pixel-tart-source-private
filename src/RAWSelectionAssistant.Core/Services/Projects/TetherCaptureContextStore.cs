using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed class TetherCaptureContextStore(string directory)
{
    public async Task FreezeAsync(Guid assetId, ShootExecutionContext context, CancellationToken token = default)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, assetId.ToString("N") + ".json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, context, cancellationToken: token); await stream.FlushAsync(token); stream.Flush(true); }
            try { File.Move(temporary, path, false); } catch (IOException) when (File.Exists(path)) { }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public async Task<ShootExecutionContext?> LoadAsync(Guid assetId, CancellationToken token = default)
    {
        var path = Path.Combine(directory, assetId.ToString("N") + ".json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ShootExecutionContext>(stream, cancellationToken: token);
    }
}

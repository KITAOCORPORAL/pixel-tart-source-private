using System.Text.Json;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

public sealed record TetherWorkspacePreferences(double InspectorWidth = 360,
    bool ExposureExpanded = true, bool ReferenceExpanded = true, bool ShotExpanded = true,
    bool SessionExpanded = true, bool InformationExpanded = false, bool AnnotationExpanded = true,
    bool LutExpanded = false, bool ClientExpanded = false, bool FileExpanded = false);

public sealed class TetherWorkspacePreferenceStore(string? path = null)
{
    private readonly string _path = path ?? Path.Combine(AppDataPaths.DataDirectory, "tether-workspace-preferences.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<TetherWorkspacePreferences> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token); try
        {
            if (!File.Exists(_path)) return new(); await using var stream = File.OpenRead(_path);
            var value = await JsonSerializer.DeserializeAsync<TetherWorkspacePreferences>(stream, cancellationToken: token) ?? new();
            return value with { InspectorWidth = Math.Clamp(value.InspectorWidth, 320, 480) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
        finally { _gate.Release(); }
    }
    public async Task SaveAsync(TetherWorkspacePreferences value, CancellationToken token = default)
    {
        await _gate.WaitAsync(token); var full=Path.GetFullPath(_path); var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!); await using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { await JsonSerializer.SerializeAsync(stream,value with { InspectorWidth=Math.Clamp(value.InspectorWidth,320,480) },cancellationToken:token); await stream.FlushAsync(token); stream.Flush(true); }
            File.Move(temporary,full,true);
        }
        finally { if(File.Exists(temporary))File.Delete(temporary); _gate.Release(); }
    }
}

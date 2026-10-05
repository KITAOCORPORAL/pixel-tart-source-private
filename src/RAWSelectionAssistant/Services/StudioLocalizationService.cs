using System.ComponentModel;
using System.IO;
using System.Text.Json;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Services;

/// <summary>Studio presentation strings; never translates persisted engine identifiers or user content.</summary>
public sealed class StudioLocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<StudioLocalizationService> Shared = new(() => new(Path.Combine(AppDataPaths.Root, "studio-language.json")));
    public static StudioLocalizationService Current => Shared.Value;
    private readonly string? _settingsPath;
    private readonly Dictionary<string, Dictionary<string,string>> _catalogs = [];
    private string _language = "zh-CN";
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<StudioLanguageOption> Languages { get; } = [new("zh-CN","简体中文"),new("zh-TW","繁體中文"),new("en-US","English")];
    public string Language => _language;
    public string? LastPersistenceError { get; private set; }
    public StudioLocalizationService(string? settingsPath)
    {
        _settingsPath = settingsPath;
        var assembly = typeof(StudioLocalizationService).Assembly;
        foreach(var code in new[]{"zh-CN","zh-TW","en-US"})
        {
            using var stream=assembly.GetManifestResourceStream($"RAWSelectionAssistant.Resources.Studio.{code}.json");
            _catalogs[code]=stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??[];
        }
        if(settingsPath is not null)
        {
            try { if(File.Exists(settingsPath)) { var setting=JsonSerializer.Deserialize<LanguageSettings>(File.ReadAllText(settingsPath));if(setting is not null&&_catalogs.ContainsKey(setting.Language))_language=setting.Language; } }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException) { LastPersistenceError=error.GetType().Name; }
        }
    }
    public string this[string key] => _catalogs[_language].TryGetValue(key,out var value) ? value : _catalogs["zh-CN"].TryGetValue(key,out var chinese) ? chinese : key;
    public bool SetLanguage(string language, bool persist = true)
    {
        if(!_catalogs.ContainsKey(language))return false;
        _language=language;LastPersistenceError=null;
        if(persist && _settingsPath is not null)
        {
            var temporary=_settingsPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_settingsPath))!);File.WriteAllText(temporary,JsonSerializer.Serialize(new LanguageSettings(language)));File.Move(temporary,_settingsPath,true); }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException) { LastPersistenceError=error.GetType().Name;try{File.Delete(temporary);}catch(IOException){} }
        }
        PropertyChanged?.Invoke(this,new(nameof(Language)));PropertyChanged?.Invoke(this,new("Item[]"));return true;
    }
    public bool HasTranslation(string language,string key)=>_catalogs.TryGetValue(language,out var catalog)&&catalog.ContainsKey(key);
    private sealed record LanguageSettings(string Language);
}
public sealed record StudioLanguageOption(string Code,string DisplayText);

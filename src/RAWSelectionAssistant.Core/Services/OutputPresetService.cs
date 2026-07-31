using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class OutputPresetService
{
    private readonly IFeatureGateService _featureGateService;
    private readonly ILogService _logService;
    private readonly string _presetFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public OutputPresetService(
        IFeatureGateService featureGateService,
        ILogService logService,
        string? presetFilePath = null)
    {
        _featureGateService = featureGateService;
        _logService = logService;
        _presetFilePath = presetFilePath ?? Path.Combine(AppDataPaths.ProjectDirectory, "output-presets.json");
        AppDataPaths.EnsureCreated();
    }

    public async Task<FeatureAccessResult> SaveAsync(OutputPreset preset, CancellationToken cancellationToken = default)
    {
        var access = _featureGateService.Check(LicensedFeature.OutputPresets);
        if (!access.Allowed) return access;

        var presets = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var index = presets.FindIndex(x => x.Id == preset.Id);
        if (index >= 0) presets[index] = preset;
        else presets.Add(preset);
        Directory.CreateDirectory(Path.GetDirectoryName(_presetFilePath)!);
        await File.WriteAllTextAsync(_presetFilePath, JsonSerializer.Serialize(presets, _jsonOptions), cancellationToken).ConfigureAwait(false);
        return access;
    }

    public async Task<IReadOnlyList<OutputPreset>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_presetFilePath)) return [];
            var json = await File.ReadAllTextAsync(_presetFilePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<OutputPreset>>(json, _jsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.Error("输出预设损坏或无法读取。", ex);
            return [];
        }
    }

    public static string RenderFolderName(OutputPreset preset, string projectName, CollectionCategory category, DateTimeOffset now)
    {
        return preset.FolderNameTemplate
            .Replace("{Project}", projectName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Category}", category.ToChinese(), StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Time}", now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);
    }
}

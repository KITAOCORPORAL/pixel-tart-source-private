using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;

namespace RAWSelectionAssistant.Core.Services;

public sealed class ProjectHistoryService
{
    private readonly IFeatureGateService _featureGateService;
    private readonly ILogService _logService;
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ProjectHistoryService(
        IFeatureGateService featureGateService,
        ILogService logService,
        string? historyFilePath = null)
    {
        _featureGateService = featureGateService;
        _logService = logService;
        _historyFilePath = historyFilePath ?? Path.Combine(AppDataPaths.ProjectDirectory, "projects.json");
        AppDataPaths.EnsureCreated();
    }

    public async Task UpsertAsync(PhotoProjectRecord project, CancellationToken cancellationToken = default)
    {
        var projects = (await LoadAllAsync(cancellationToken).ConfigureAwait(false)).ToList();
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var index = projects.FindIndex(x => x.Id == project.Id);
        if (index >= 0) projects[index] = project;
        else projects.Add(project);
        await SaveAsync(projects, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PhotoProjectRecord>> LoadVisibleAsync(CancellationToken cancellationToken = default)
    {
        var all = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return _featureGateService.HasAccess(LicensedFeature.UnlimitedProjectHistory)
            ? all
            : all.Take(1).ToList();
    }

    public async Task<IReadOnlyList<PhotoProjectRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_historyFilePath)) return [];
            await using var stream = File.OpenRead(_historyFilePath);
            var projects = await JsonSerializer.DeserializeAsync<List<PhotoProjectRecord>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            return (projects ?? []).OrderByDescending(x => x.UpdatedAt).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logService.Error("项目历史损坏或无法读取，将以空历史继续。", ex);
            return [];
        }
    }

    private async Task SaveAsync(IReadOnlyList<PhotoProjectRecord> projects, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_historyFilePath)!);
        var temporary = _historyFilePath + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
        {
            await JsonSerializer.SerializeAsync(stream, projects, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporary, _historyFilePath, true);
    }
}

using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Database;

namespace RAWSelectionAssistant.Core.Services.OnlineSelection;

public sealed class SelectionCollectionProjectWriter(IProjectRepository repository)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteAsync(SelectionProject selection, SelectionFinalResult result,
        IReadOnlyList<SelectionAsset> assets, CancellationToken cancellationToken = default)
    {
        SelectionHttpWorkflow.ValidateFinal(selection, assets, result);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var id = selection.CollectionProjectId ?? selection.Id;
            var projects = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
            var project = projects.FirstOrDefault(item => item.Id == id) ?? new PhotoProjectRecord { Id = id, Name = selection.Name };
            // An unbound legacy selection gets its own project. Never guess by name or current UI focus.
            project.SelectionInputs = result.Items.Where(item => item.Selected).Select(item => item.OriginalFileName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            project.SelectionCount = project.SelectionInputs.Count;
            project.SourceDirectories = project.SourceDirectories.Concat(assets.Select(item => Path.GetDirectoryName(item.LocalSourcePath)).OfType<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            project.Status = PhotoProjectStatus.Ready;
            await repository.UpsertAsync(project, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}

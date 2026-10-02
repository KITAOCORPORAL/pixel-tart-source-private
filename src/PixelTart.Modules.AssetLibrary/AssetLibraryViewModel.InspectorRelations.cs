using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private string _organizationFolderSearch = "", _inspectorTagSearch = "";
    private bool _inspectorRelationsBusy, _inspectorRelationsLoaded;
    private IReadOnlyList<InspectorTagChoice> _inspectorTagChoices = [];
    public string OrganizationFolderSearch
    {
        get => _organizationFolderSearch;
        set { if (SetProperty(ref _organizationFolderSearch, value ?? "")) FilterOrganizationFolderNames(); }
    }
    private void FilterOrganizationFolderNames()
    {
        foreach (var node in OrganizationFolders) node.ApplyNameFilter(OrganizationFolderSearch.Trim());
    }

    public ObservableCollection<InspectorRelationChip> InspectorTagChips { get; } = [];
    public ObservableCollection<InspectorRelationChip> InspectorFolderChips { get; } = [];
    public IEnumerable<InspectorTagChoice> InspectorSelectedTags => _inspectorTagChoices.Where(tag => tag.HasMembership && MatchesInspectorTag(tag));
    public IEnumerable<InspectorTagChoice> InspectorAvailableTags => _inspectorTagChoices.Where(tag => !tag.HasMembership && MatchesInspectorTag(tag));
    public bool CanEditInspectorRelations => IsReady && HasSelection && _inspectorRelationsLoaded && !_inspectorRelationsBusy;
    public string InspectorTagSearch
    {
        get => _inspectorTagSearch;
        set { if (SetProperty(ref _inspectorTagSearch, value ?? "")) NotifyInspectorTagPicker(); }
    }
    public string InspectorCreateTagLabel => string.IsNullOrWhiteSpace(InspectorTagSearch) ? "输入名称以新建标签" : $"新建标签：{InspectorTagSearch.Trim()}";
    public AsyncCommand CreateInspectorTagCommand { get; private set; } = null!;
    private bool MatchesInspectorTag(InspectorTagChoice tag) => tag.Name.Contains(InspectorTagSearch.Trim(), StringComparison.OrdinalIgnoreCase);

    private void InitializeInspectorRelations()
    {
        CreateInspectorTagCommand = new(async () =>
        {
            var ids = SelectedAssetIds.ToArray();
            var name = InspectorTagSearch.Trim();
            await MutateInspectorRelationsAsync(ids, async () =>
            {
                var tag = await _repository.SaveTagAsync(new(Guid.NewGuid(), name), _lifetimeCancellation.Token);
                RememberBrowserMutationResult(await _browserCommands.AddTagAsync(ids, tag.TagId, _lifetimeCancellation.Token));
                InspectorTagSearch = "";
            });
        }, () => CanEditInspectorRelations && !string.IsNullOrWhiteSpace(InspectorTagSearch)
            && !Tags.Any(tag => string.Equals(tag.Name, InspectorTagSearch.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private void NotifyInspectorTagPicker()
    {
        OnPropertyChanged(nameof(InspectorSelectedTags)); OnPropertyChanged(nameof(InspectorAvailableTags));
        OnPropertyChanged(nameof(InspectorCreateTagLabel)); OnPropertyChanged(nameof(CanEditInspectorRelations));
        CreateInspectorTagCommand?.RaiseCanExecuteChanged();
        AddInspectorFolderCommand?.RaiseCanExecuteChanged();
    }

    private void ClearInspectorRelations()
    {
        _inspectorRelationsLoaded = false;
        InspectorTagChips.Clear(); InspectorFolderChips.Clear(); _inspectorTagChoices = [];
        NotifyInspectorTagPicker();
    }

    private void PublishInspectorRelations(HashSet<Guid> assetIds, IReadOnlyList<AssetFolderMembership> folders, IReadOnlyList<AssetTagMembership> tags)
    {
        var ids = assetIds.ToArray();
        InspectorTagChips.Clear(); InspectorFolderChips.Clear();
        var choices = new List<InspectorTagChoice>();
        foreach (var tag in Tags.Where(tag => !tag.IsArchived && (tag.TagGroupId is null || TagGroups.Any(group => group.TagGroupId == tag.TagGroupId && !group.IsArchived))))
        {
            var count = tags.Where(link => link.TagId == tag.TagId && assetIds.Contains(link.AssetId)).Select(link => link.AssetId).Distinct().Count();
            var remove = new AsyncCommand(() => MutateInspectorRelationsAsync(ids, async () =>
                RememberBrowserMutationResult(await _browserCommands.RemoveTagAsync(ids, tag.TagId, _lifetimeCancellation.Token))), () => CanEditInspectorRelations);
            var toggle = new AsyncCommand(() => MutateInspectorRelationsAsync(ids, async () =>
                RememberBrowserMutationResult(count == ids.Length
                    ? await _browserCommands.RemoveTagAsync(ids, tag.TagId, _lifetimeCancellation.Token)
                    : await _browserCommands.AddTagAsync(ids, tag.TagId, _lifetimeCancellation.Token))), () => CanEditInspectorRelations);
            choices.Add(new(tag.TagId, tag.Name, count, ids.Length, toggle));
            if (count > 0) InspectorTagChips.Add(new(tag.TagId, tag.Name, count, ids.Length, remove));
        }
        foreach (var folder in Folders.Where(folder => !folder.IsArchived))
        {
            var count = folders.Where(link => link.FolderId == folder.FolderId && assetIds.Contains(link.AssetId)).Select(link => link.AssetId).Distinct().Count();
            if (count == 0) continue;
            var remove = new AsyncCommand(() => MutateInspectorRelationsAsync(ids, async () =>
                RememberBrowserMutationResult(await _browserCommands.RemoveFromFolderAsync(ids, folder.FolderId, _lifetimeCancellation.Token))), () => CanEditInspectorRelations);
            InspectorFolderChips.Add(new(folder.FolderId, folder.Name, count, ids.Length, remove));
        }
        _inspectorTagChoices = choices;
        _inspectorRelationsLoaded = true;
        NotifyInspectorTagPicker();
    }

    private Task MutateInspectorRelationsAsync(Guid[] assetIds, Func<Task> action) => RunTrackedP3OperationAsync(async () =>
    {
        // A delayed click must never apply a previous asset's picker to a new selection.
        if (!CanEditInspectorRelations || !SelectedAssetIds.ToHashSet().SetEquals(assetIds)) return;
        _inspectorRelationsBusy = true;
        NotifyInspectorTagPicker();
        try { await InspectorMutationAsync(action, refreshOrganizations: true); }
        finally { _inspectorRelationsBusy = false; NotifyInspectorTagPicker(); }
    });
}

public sealed record InspectorRelationChip(Guid Id, string Name, int MemberCount, int SelectionCount, AsyncCommand RemoveCommand)
{
    public string Label => SelectionCount > 1 ? $"{Name} ({MemberCount}/{SelectionCount})" : Name;
    public string RemoveLabel => $"从所选素材移除{ Name }关系";
}

public sealed record InspectorTagChoice(Guid Id, string Name, int MemberCount, int SelectionCount, AsyncCommand ToggleCommand)
{
    public bool HasMembership => MemberCount > 0;
    public bool? IsChecked => MemberCount == 0 ? false : MemberCount == SelectionCount ? true : null;
    public string Label => SelectionCount > 1 ? $"{Name} ({MemberCount}/{SelectionCount})" : Name;
}

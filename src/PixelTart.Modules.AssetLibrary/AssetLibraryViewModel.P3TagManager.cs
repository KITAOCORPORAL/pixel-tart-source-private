using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private CancellationTokenSource? _p3TagSearchCancellation;
    private CancellationTokenSource? _p3BatchPreviewCancellation;
    private CancellationTokenSource? _p3TagMergePreviewCancellation;
    private long _p3TagSearchGeneration;
    private long _p3BatchPreviewGeneration;
    private long _p3TagMergePreviewGeneration;
    private IReadOnlyList<AssetTag> _p3AllTags = [];
    private IReadOnlyList<TagGroup> _p3AllTagGroups = [];
    private bool _p3TagManagerOpen;
    private bool _p3TagManagerLoading;
    private bool _p3TagShowArchived;
    private string _p3TagSearchText = string.Empty;
    private string _p3TagNameInput = string.Empty;
    private string _p3TagGroupNameInput = string.Empty;
    private AssetTag? _p3SelectedManagedTag;
    private TagGroup? _p3SelectedManagedTagGroup;
    private AssetTag? _p3MergeTargetTag;
    private TagGroup? _p3MoveTargetTagGroup;
    private string _p3TagManagerStatus = "打开管理器后可管理标签和标签组。";
    private bool _p3TagMergePreviewReady;
    private string? _p3TagMergePreviewFingerprint;
    private IReadOnlyList<AssetTag> _p3MergeSourceTags = [];
    private string _p3TagMergePreviewSummary = "先选择源标签和目标标签，再预览合并影响。";
    private string _p3BatchTagAction = "不更改";
    private AssetTag? _p3BatchTag;
    private string _p3BatchFolderAction = "不更改";
    private AssetFolder? _p3BatchFolder;
    private string _p3BatchRatingAction = "不更改";
    private int _p3BatchRating;
    private string _p3BatchCommentAction = "不更改";
    private string _p3BatchComment = string.Empty;
    private string _p3BatchArchiveChoice = "不更改";
    private string _p3BatchMissingChoice = "不更改";
    private bool _p3BatchPreviewReady;
    private AssetBatchMetadataPreview? _p3BatchPreviewContract;
    private string _p3BatchPreviewSummary = "先选择素材，再预览批量修改。";

    public ObservableCollection<AssetTag> P3ManagedTags { get; } = [];
    public ObservableCollection<TagGroup> P3ManagedTagGroups { get; } = [];

    public bool P3TagManagerOpen { get => _p3TagManagerOpen; private set => SetProperty(ref _p3TagManagerOpen, value); }
    public bool P3TagManagerLoading { get => _p3TagManagerLoading; private set => SetProperty(ref _p3TagManagerLoading, value); }
    public bool P3TagShowArchived { get => _p3TagShowArchived; set { if (SetProperty(ref _p3TagShowArchived, value)) ScheduleP3TagFilter(); } }
    public string P3TagSearchText { get => _p3TagSearchText; set { if (SetProperty(ref _p3TagSearchText, value ?? string.Empty)) ScheduleP3TagFilter(); } }
    public string P3TagNameInput { get => _p3TagNameInput; set { if (SetProperty(ref _p3TagNameInput, value ?? string.Empty)) RaiseP3TagCommands(); } }
    public string P3TagGroupNameInput { get => _p3TagGroupNameInput; set { if (SetProperty(ref _p3TagGroupNameInput, value ?? string.Empty)) RaiseP3TagCommands(); } }

    public AssetTag? P3SelectedManagedTag
    {
        get => _p3SelectedManagedTag;
        set
        {
            if (!SetProperty(ref _p3SelectedManagedTag, value)) return;
            if (value is not null) P3TagNameInput = value.Name;
            InvalidateP3TagMergePreview();
            RaiseP3TagCommands();
        }
    }

    public TagGroup? P3SelectedManagedTagGroup
    {
        get => _p3SelectedManagedTagGroup;
        set
        {
            if (!SetProperty(ref _p3SelectedManagedTagGroup, value)) return;
            if (value is not null) P3TagGroupNameInput = value.Name;
            ScheduleP3TagFilter();
            RaiseP3TagCommands();
        }
    }

    public AssetTag? P3MergeTargetTag
    {
        get => _p3MergeTargetTag;
        set
        {
            if (!SetProperty(ref _p3MergeTargetTag, value)) return;
            InvalidateP3TagMergePreview();
            RaiseP3TagCommands();
        }
    }
    public TagGroup? P3MoveTargetTagGroup { get => _p3MoveTargetTagGroup; set { if (SetProperty(ref _p3MoveTargetTagGroup, value)) RaiseP3TagCommands(); } }
    public string P3TagManagerStatus { get => _p3TagManagerStatus; private set => SetProperty(ref _p3TagManagerStatus, value); }
    public bool P3TagMergePreviewReady { get => _p3TagMergePreviewReady; private set { if (SetProperty(ref _p3TagMergePreviewReady, value)) MergeP3TagCommand?.RaiseCanExecuteChanged(); } }
    public string P3TagMergePreviewSummary { get => _p3TagMergePreviewSummary; private set => SetProperty(ref _p3TagMergePreviewSummary, value); }
    public IReadOnlyList<AssetTag> P3MergeSourceTags => _p3MergeSourceTags;

    public IReadOnlyList<string> P3MembershipActions { get; } = ["不更改", "添加", "移除"];
    public IReadOnlyList<string> P3ValueActions { get; } = ["不更改", "设置", "清除"];
    public IReadOnlyList<string> P3BooleanChoices { get; } = ["不更改", "是", "否"];
    public IReadOnlyList<int> P3RatingValues { get; } = [0, 1, 2, 3, 4, 5];

    public string P3BatchTagAction { get => _p3BatchTagAction; set => SetP3BatchField(ref _p3BatchTagAction, value); }
    public AssetTag? P3BatchTag { get => _p3BatchTag; set => SetP3BatchField(ref _p3BatchTag, value); }
    public string P3BatchFolderAction { get => _p3BatchFolderAction; set => SetP3BatchField(ref _p3BatchFolderAction, value); }
    public AssetFolder? P3BatchFolder { get => _p3BatchFolder; set => SetP3BatchField(ref _p3BatchFolder, value); }
    public string P3BatchRatingAction { get => _p3BatchRatingAction; set => SetP3BatchField(ref _p3BatchRatingAction, value); }
    public int P3BatchRating { get => _p3BatchRating; set => SetP3BatchField(ref _p3BatchRating, Math.Clamp(value, 0, 5)); }
    public string P3BatchCommentAction { get => _p3BatchCommentAction; set => SetP3BatchField(ref _p3BatchCommentAction, value); }
    public string P3BatchComment { get => _p3BatchComment; set => SetP3BatchField(ref _p3BatchComment, value ?? string.Empty); }
    public string P3BatchArchiveChoice { get => _p3BatchArchiveChoice; set => SetP3BatchField(ref _p3BatchArchiveChoice, value); }
    public string P3BatchMissingChoice { get => _p3BatchMissingChoice; set => SetP3BatchField(ref _p3BatchMissingChoice, value); }
    public bool P3BatchPreviewReady { get => _p3BatchPreviewReady; private set { if (SetProperty(ref _p3BatchPreviewReady, value)) ApplyP3BatchMetadataCommand?.RaiseCanExecuteChanged(); } }
    public string P3BatchPreviewSummary { get => _p3BatchPreviewSummary; private set => SetProperty(ref _p3BatchPreviewSummary, value); }
    public string P3BatchSelectionState => SelectionCount switch { 0 => "没有选择素材", 1 => "已选择 1 项", _ => $"已选择 {SelectionCount:N0} 项（混合值会明确标记）" };
    public string P3BatchCommonState => SelectionCount switch
    {
        0 => "共同值：无",
        1 => $"当前评分：{SelectedAssets.FirstOrDefault()?.Rating ?? 0}",
        _ => $"{MultipleRatingSummary}；共同标签：{MultipleTagSummary}；共同文件夹：{MultipleFolderSummary}"
    };

    public AssetCommand ToggleP3TagManagerCommand { get; private set; } = null!;
    public AsyncCommand RefreshP3TagManagerCommand { get; private set; } = null!;
    public AsyncCommand CreateP3TagGroupCommand { get; private set; } = null!;
    public AsyncCommand RenameP3TagGroupCommand { get; private set; } = null!;
    public AsyncCommand ToggleArchiveP3TagGroupCommand { get; private set; } = null!;
    public AsyncCommand<string> MoveP3TagGroupCommand { get; private set; } = null!;
    public AsyncCommand CreateP3TagCommand { get; private set; } = null!;
    public AsyncCommand RenameP3TagCommand { get; private set; } = null!;
    public AsyncCommand MoveP3TagCommand { get; private set; } = null!;
    public AsyncCommand<string> ReorderP3TagCommand { get; private set; } = null!;
    public AsyncCommand PreviewP3TagMergeCommand { get; private set; } = null!;
    public AsyncCommand MergeP3TagCommand { get; private set; } = null!;
    public AsyncCommand ToggleArchiveP3TagCommand { get; private set; } = null!;
    public AsyncCommand PreviewP3BatchMetadataCommand { get; private set; } = null!;
    public AsyncCommand ApplyP3BatchMetadataCommand { get; private set; } = null!;

    private void InitializeP3TagManager()
    {
        ToggleP3TagManagerCommand = new(() =>
        {
            P3TagManagerOpen = !P3TagManagerOpen;
            if (P3TagManagerOpen) _ = LoadP3TagManagerAsync();
            else CancelP3TagManagerWork();
        });
        RefreshP3TagManagerCommand = new(LoadP3TagManagerAsync, () => IsReady && !P3TagManagerLoading);
        CreateP3TagGroupCommand = new(CreateP3TagGroupAsync, () => IsReady && !string.IsNullOrWhiteSpace(P3TagGroupNameInput));
        RenameP3TagGroupCommand = new(RenameP3TagGroupAsync, () => IsReady && P3SelectedManagedTagGroup is not null && !string.IsNullOrWhiteSpace(P3TagGroupNameInput));
        ToggleArchiveP3TagGroupCommand = new(ToggleArchiveP3TagGroupAsync, () => IsReady && P3SelectedManagedTagGroup is not null);
        MoveP3TagGroupCommand = new(MoveP3TagGroupAsync, _ => IsReady && P3SelectedManagedTagGroup is not null);
        CreateP3TagCommand = new(CreateP3TagAsync, () => IsReady && !string.IsNullOrWhiteSpace(P3TagNameInput));
        RenameP3TagCommand = new(RenameP3TagAsync, () => IsReady && P3SelectedManagedTag is not null && !string.IsNullOrWhiteSpace(P3TagNameInput));
        MoveP3TagCommand = new(MoveP3TagAsync, () => IsReady && P3SelectedManagedTag is not null);
        ReorderP3TagCommand = new(ReorderP3TagAsync, _ => IsReady && P3SelectedManagedTag is not null);
        PreviewP3TagMergeCommand = new(PreviewP3TagMergeAsync, CanPreviewP3TagMerge);
        MergeP3TagCommand = new(MergeP3TagAsync, () => IsReady && P3TagMergePreviewReady && CanPreviewP3TagMerge());
        ToggleArchiveP3TagCommand = new(ToggleArchiveP3TagAsync, () => IsReady && P3SelectedManagedTag is not null);
        PreviewP3BatchMetadataCommand = new(PreviewP3BatchMetadataAsync, () => IsReady && SelectionCount > 0);
        ApplyP3BatchMetadataCommand = new(ApplyP3BatchMetadataAsync, () => IsReady && SelectionCount > 0 && P3BatchPreviewReady);
    }

    private async Task LoadP3TagManagerAsync()
    {
        P3TagManagerLoading = true;
        try
        {
            var groups = await _repository.ListTagGroupsAsync(includeArchived: true, _lifetimeCancellation.Token);
            var tags = await _repository.ListTagsAsync(includeArchived: true, cancellationToken: _lifetimeCancellation.Token);
            _p3AllTagGroups = groups.OrderBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            _p3AllTags = tags.OrderBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            ApplyP3TagFilter();
            P3TagManagerStatus = $"已载入 {_p3AllTags.Count:N0} 个标签、{_p3AllTagGroups.Count:N0} 个标签组。";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) { P3TagManagerStatus = $"标签管理器载入失败：{exception.Message}"; }
        finally { P3TagManagerLoading = false; RaiseP3TagCommands(); }
    }

    private void ScheduleP3TagFilter()
    {
        if (!P3TagManagerOpen) return;
        var previous = Interlocked.Exchange(ref _p3TagSearchCancellation, null);
        previous?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3TagSearchCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3TagSearchGeneration);
        _ = ApplyP3TagFilterAfterDelayAsync(generation, cancellation);
    }

    private async Task ApplyP3TagFilterAfterDelayAsync(long generation, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(280), cancellation.Token);
            if (generation != Volatile.Read(ref _p3TagSearchGeneration) || !ReferenceEquals(_p3TagSearchCancellation, cancellation)) return;
            ApplyP3TagFilter();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_p3TagSearchCancellation, cancellation)) _p3TagSearchCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ApplyP3TagFilter()
    {
        var selectedGroupId = P3SelectedManagedTagGroup?.TagGroupId;
        var text = P3TagSearchText.Trim();
        P3ManagedTagGroups.Clear();
        foreach (var group in _p3AllTagGroups.Where(group => P3TagShowArchived || !group.IsArchived)) P3ManagedTagGroups.Add(group);
        var visibleGroupIds = P3ManagedTagGroups.Select(group => group.TagGroupId).ToHashSet();
        if (selectedGroupId is Guid groupId && !visibleGroupIds.Contains(groupId))
        {
            P3SelectedManagedTagGroup = null;
            selectedGroupId = null;
        }
        P3ManagedTags.Clear();
        foreach (var tag in _p3AllTags.Where(tag => P3TagShowArchived ||
                                                   !tag.IsArchived && (tag.TagGroupId is null || visibleGroupIds.Contains(tag.TagGroupId.Value)))
                     .Where(tag => selectedGroupId is null || tag.TagGroupId == selectedGroupId)
                     .Where(tag => text.Length == 0 || tag.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)))
            P3ManagedTags.Add(tag);
        if (P3SelectedManagedTag is not null && P3ManagedTags.All(tag => tag.TagId != P3SelectedManagedTag.TagId))
            P3SelectedManagedTag = null;
        SetP3MergeSourceTags(_p3MergeSourceTags.Where(source =>
            P3ManagedTags.Any(tag => tag.TagId == source.TagId) && IsP3TagEffectivelyActive(source)));
    }

    private async Task CreateP3TagGroupAsync() => await RunP3TagMutationAsync(async () =>
    {
        await _repository.SaveTagGroupAsync(new(Guid.NewGuid(), P3TagGroupNameInput.Trim()), _lifetimeCancellation.Token);
        P3TagGroupNameInput = string.Empty;
        return "已新建标签组。";
    });

    private async Task RenameP3TagGroupAsync() => await RunP3TagMutationAsync(async () =>
    {
        var group = P3SelectedManagedTagGroup!;
        await _repository.SaveTagGroupAsync(group with { Name = P3TagGroupNameInput.Trim() }, _lifetimeCancellation.Token);
        return "已重命名标签组。";
    });

    private async Task ToggleArchiveP3TagGroupAsync() => await RunP3TagMutationAsync(async () =>
    {
        var group = P3SelectedManagedTagGroup!;
        RememberP3MetadataResult(await _repository.SetTagGroupArchivedAsync(group.TagGroupId, !group.IsArchived, _lifetimeCancellation.Token));
        return group.IsArchived ? "标签组及其标签已恢复。" : "标签组及其标签已归档，可恢复。";
    });

    private async Task MoveP3TagGroupAsync(string? direction) => await RunP3TagMutationAsync(async () =>
    {
        var selected = P3SelectedManagedTagGroup!;
        var active = _p3AllTagGroups.OrderBy(group => group.SortOrder).ToList();
        var index = active.FindIndex(group => group.TagGroupId == selected.TagGroupId);
        if (index < 0) return "标签组不在当前排序范围内。";
        var target = Math.Clamp(index + (direction == "up" ? -1 : 1), 0, active.Count - 1);
        if (index >= 0 && index != target) (active[index], active[target]) = (active[target], active[index]);
        RememberP3MetadataResult(await _repository.ReorderTagGroupsAsync(active.Select(group => group.TagGroupId), _lifetimeCancellation.Token));
        return "标签组顺序已更新。";
    });

    private async Task CreateP3TagAsync() => await RunP3TagMutationAsync(async () =>
    {
        await _repository.SaveTagAsync(new(Guid.NewGuid(), P3TagNameInput.Trim(), P3SelectedManagedTagGroup?.TagGroupId), _lifetimeCancellation.Token);
        P3TagNameInput = string.Empty;
        return "已新建标签。";
    });

    private async Task RenameP3TagAsync() => await RunP3TagMutationAsync(async () =>
    {
        RememberP3MetadataResult(await _repository.RenameTagAsync(P3SelectedManagedTag!.TagId, P3TagNameInput.Trim(), _lifetimeCancellation.Token));
        return "已重命名标签。";
    });

    private async Task MoveP3TagAsync() => await RunP3TagMutationAsync(async () =>
    {
        RememberP3MetadataResult(await _repository.MoveTagsToGroupAsync([P3SelectedManagedTag!.TagId], P3MoveTargetTagGroup?.TagGroupId, _lifetimeCancellation.Token));
        return P3MoveTargetTagGroup is null ? "标签已移到未分组。" : $"标签已移到“{P3MoveTargetTagGroup.Name}”。";
    });

    private async Task ReorderP3TagAsync(string? direction) => await RunP3TagMutationAsync(async () =>
    {
        var tag = P3SelectedManagedTag!;
        var siblings = _p3AllTags.Where(item => item.TagGroupId == tag.TagGroupId).OrderBy(item => item.SortOrder).ToList();
        var index = siblings.FindIndex(item => item.TagId == tag.TagId);
        var target = Math.Clamp(index + (direction == "up" ? -1 : 1), 0, siblings.Count - 1);
        if (index >= 0 && index != target) (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        RememberP3MetadataResult(await _repository.ReorderTagsAsync(tag.TagGroupId, siblings.Select(item => item.TagId), _lifetimeCancellation.Token));
        return "标签顺序已更新。";
    });

    private async Task MergeP3TagAsync() => await RunP3TagMutationAsync(async () =>
    {
        var sources = _p3MergeSourceTags.ToArray();
        var target = P3MergeTargetTag!;
        var current = await CaptureP3TagMergePreviewAsync(sources, target, _lifetimeCancellation.Token);
        if (!string.Equals(current.Fingerprint, _p3TagMergePreviewFingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("标签关系已变化，请重新预览后再合并。");
        RememberP3MetadataResult(await _repository.MergeTagsAsync(sources.Select(source => source.TagId), target.TagId, _lifetimeCancellation.Token));
        _p3TagMergePreviewFingerprint = null;
        return $"已把 {sources.Length:N0} 个源标签合并到“{target.Name}”；重复关系已去重。";
    });

    private async Task PreviewP3TagMergeAsync()
    {
        var sources = _p3MergeSourceTags.ToArray();
        var target = P3MergeTargetTag;
        if (target is null || sources.Length == 0 || sources.Any(source => source.TagId == target.TagId)) return;
        var previous = Interlocked.Exchange(ref _p3TagMergePreviewCancellation, null);
        previous?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3TagMergePreviewCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3TagMergePreviewGeneration);
        P3TagMergePreviewReady = false;
        try
        {
            var preview = await CaptureP3TagMergePreviewAsync(sources, target, cancellation.Token);
            if (generation != Volatile.Read(ref _p3TagMergePreviewGeneration) ||
                !ReferenceEquals(_p3TagMergePreviewCancellation, cancellation) ||
                !P3MergeSelectionMatches(sources, target)) return;
            _p3TagMergePreviewFingerprint = preview.Fingerprint;
            P3TagMergePreviewSummary = $"{sources.Length:N0} 个源标签共影响 {preview.SourceAssetCount:N0} 项；目标“{target.Name}”已有 {preview.TargetAssetCount:N0} 项；{preview.DuplicateRelationshipCount:N0} 条重复关系会去重。执行后源标签归档，可撤销。";
            P3TagMergePreviewReady = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            P3TagMergePreviewReady = false;
            P3TagMergePreviewSummary = $"合并预览失败：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_p3TagMergePreviewCancellation, cancellation)) _p3TagMergePreviewCancellation = null;
            cancellation.Dispose();
        }
    }

    internal void SetP3MergeSourceTags(IEnumerable<AssetTag> sources)
    {
        _p3MergeSourceTags = sources
            .Where(source => source is not null)
            .Where(IsP3TagEffectivelyActive)
            .DistinctBy(source => source.TagId)
            .OrderBy(source => source.TagId)
            .ToArray();
        OnPropertyChanged(nameof(P3MergeSourceTags));
        InvalidateP3TagMergePreview();
        RaiseP3TagCommands();
    }

    private bool CanPreviewP3TagMerge() => IsReady && _p3MergeSourceTags.Count > 0 &&
        P3MergeTargetTag is not null && IsP3TagEffectivelyActive(P3MergeTargetTag) &&
        _p3MergeSourceTags.All(source => IsP3TagEffectivelyActive(source) && source.TagId != P3MergeTargetTag.TagId);

    private bool P3MergeSelectionMatches(IReadOnlyList<AssetTag> sources, AssetTag target) =>
        P3MergeTargetTag?.TagId == target.TagId &&
        _p3MergeSourceTags.Select(source => source.TagId).SequenceEqual(sources.Select(source => source.TagId));

    private async Task<P3TagMergePreviewState> CaptureP3TagMergePreviewAsync(
        IReadOnlyList<AssetTag> sources,
        AssetTag target,
        CancellationToken cancellationToken)
    {
        var groups = (await _repository.ListTagGroupsAsync(includeArchived: true, cancellationToken))
            .ToDictionary(group => group.TagGroupId);
        var tags = (await _repository.ListTagsAsync(includeArchived: true, cancellationToken: cancellationToken))
            .ToDictionary(tag => tag.TagId);
        var currentSources = sources.Select(source => tags.TryGetValue(source.TagId, out var current)
                ? current
                : throw new InvalidOperationException($"源标签 {source.TagId:D} 已不存在，请重新预览。"))
            .ToArray();
        if (!tags.TryGetValue(target.TagId, out var currentTarget))
            throw new InvalidOperationException($"目标标签 {target.TagId:D} 已不存在，请重新预览。");
        if (currentSources.Any(source => !IsP3TagEffectivelyActive(source, groups)) ||
            !IsP3TagEffectivelyActive(currentTarget, groups))
            throw new InvalidOperationException("源标签或目标标签已归档（包括其标签组已归档），请重新选择。");

        var sourceMemberships = new List<Guid>();
        foreach (var source in currentSources)
            sourceMemberships.AddRange((await _repository.ListTagMembershipsAsync(
                tagId: source.TagId, cancellationToken: cancellationToken)).Select(item => item.AssetId));
        var targetMemberships = (await _repository.ListTagMembershipsAsync(
            tagId: currentTarget.TagId, cancellationToken: cancellationToken)).Select(item => item.AssetId).ToArray();
        var sourceAssets = sourceMemberships.Distinct().OrderBy(id => id).ToArray();
        var allRelationships = sourceMemberships.Concat(targetMemberships).ToArray();
        var distinctRelationships = allRelationships.Distinct().OrderBy(id => id).ToArray();
        var stateContract = string.Join("|", currentSources.Append(currentTarget).Select(tag =>
            $"{tag.TagId:D},{tag.Name.Normalize(NormalizationForm.FormC)},{tag.TagGroupId?.ToString("D") ?? "-"},{tag.IsArchived},{(tag.TagGroupId is Guid groupId && groups.TryGetValue(groupId, out var group) ? group.IsArchived : tag.TagGroupId is not null)}"));
        var contract = stateContract + ":" + string.Join(",", allRelationships.OrderBy(id => id));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contract))).ToLowerInvariant();
        return new(fingerprint, sourceAssets.Length, targetMemberships.Distinct().Count(),
            allRelationships.Length - distinctRelationships.Length);
    }

    private bool IsP3TagEffectivelyActive(AssetTag tag) =>
        IsP3TagEffectivelyActive(tag, _p3AllTagGroups.ToDictionary(group => group.TagGroupId));

    private static bool IsP3TagEffectivelyActive(AssetTag tag, IReadOnlyDictionary<Guid, TagGroup> groups) =>
        !tag.IsArchived && (tag.TagGroupId is null ||
                            groups.TryGetValue(tag.TagGroupId.Value, out var group) && !group.IsArchived);

    private async Task ToggleArchiveP3TagAsync() => await RunP3TagMutationAsync(async () =>
    {
        var tag = P3SelectedManagedTag!;
        RememberP3MetadataResult(await _repository.SetTagArchivedAsync(tag.TagId, !tag.IsArchived, _lifetimeCancellation.Token));
        return tag.IsArchived ? "标签已恢复。" : "标签已归档，可恢复。";
    });

    private async Task RunP3TagMutationAsync(Func<Task<string>> operation)
    {
        try
        {
            P3TagManagerStatus = await operation();
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            await LoadP3TagManagerAsync();
            RaiseP2CommandStates();
        }
        catch (Exception exception) { P3TagManagerStatus = $"操作未完成：{exception.Message}"; }
    }

    private AssetBatchMetadataRequest BuildP3BatchMetadataRequest() => new(
        SelectedAssetIds.ToArray(),
        AddTagIds: P3BatchTagAction == "添加" && P3BatchTag is not null ? [P3BatchTag.TagId] : null,
        RemoveTagIds: P3BatchTagAction == "移除" && P3BatchTag is not null ? [P3BatchTag.TagId] : null,
        AddFolderIds: P3BatchFolderAction == "添加" && P3BatchFolder is not null ? [P3BatchFolder.FolderId] : null,
        RemoveFolderIds: P3BatchFolderAction == "移除" && P3BatchFolder is not null ? [P3BatchFolder.FolderId] : null,
        Rating: P3BatchRatingAction == "设置" ? P3BatchRating : null,
        ClearRating: P3BatchRatingAction == "清除",
        Comment: P3BatchCommentAction == "设置" ? P3BatchComment : null,
        ClearComment: P3BatchCommentAction == "清除",
        IsArchived: ParseP3BooleanChoice(P3BatchArchiveChoice),
        IsMissing: ParseP3BooleanChoice(P3BatchMissingChoice));

    private async Task PreviewP3BatchMetadataAsync()
    {
        var previous = Interlocked.Exchange(ref _p3BatchPreviewCancellation, null);
        previous?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3BatchPreviewCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3BatchPreviewGeneration);
        P3BatchPreviewReady = false;
        if (!HasP3BatchMutation(out var validationError))
        {
            P3BatchPreviewSummary = validationError;
            cancellation.Dispose();
            if (ReferenceEquals(_p3BatchPreviewCancellation, cancellation)) _p3BatchPreviewCancellation = null;
            return;
        }
        try
        {
            var preview = await _repository.PreviewBatchMetadataAsync(BuildP3BatchMetadataRequest(), cancellation.Token);
            if (generation != Volatile.Read(ref _p3BatchPreviewGeneration) || !ReferenceEquals(_p3BatchPreviewCancellation, cancellation)) return;
            _p3BatchPreviewContract = preview;
            var mixed = string.Join("、", new[] { preview.HasMixedRatings ? "评分为混合值" : null, preview.HasMixedComments ? "备注为混合值" : null }.Where(item => item is not null));
            P3BatchPreviewSummary = $"选中 {preview.AssetCount:N0} 项，实际将改变 {preview.ChangedCount:N0} 项；已有 {preview.ExistingTagRelationships:N0} 条标签关系、{preview.ExistingFolderRelationships:N0} 条文件夹关系" +
                                    (mixed.Length == 0 ? "。" : $"；{mixed}。") +
                                    (preview.ConflictOverrides.Count == 0 ? string.Empty : $" 冲突覆盖 {preview.ConflictOverrideCount:N0} 项：" + string.Join("；", preview.ConflictOverrides)) +
                                    (preview.Warnings.Count == 0 || preview.Warnings.SequenceEqual(preview.ConflictOverrides) ? string.Empty : " " + string.Join("；", preview.Warnings));
            P3BatchPreviewReady = preview.AssetCount > 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) { P3BatchPreviewSummary = $"预览失败：{exception.Message}"; }
        finally
        {
            if (ReferenceEquals(_p3BatchPreviewCancellation, cancellation)) _p3BatchPreviewCancellation = null;
            cancellation.Dispose();
        }
    }

    private async Task ApplyP3BatchMetadataAsync()
    {
        var preview = _p3BatchPreviewContract;
        if (preview is null)
        {
            P3BatchPreviewReady = false;
            P3BatchPreviewSummary = "预览契约不存在，请重新预览后再应用。";
            return;
        }
        var request = BuildP3BatchMetadataRequest();
        P3BatchPreviewReady = false;
        _p3BatchPreviewContract = null;
        try
        {
            var result = await _repository.ApplyBatchMetadataAsync(request, preview, _lifetimeCancellation.Token);
            RememberP3MetadataResult(result);
            P3BatchPreviewSummary = $"已安全更新 {result.ChangedCount:N0} 项素材库元数据；源文件未改动。";
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            await RefreshAsync();
            await RefreshSelectionSummaryAsync();
        }
        catch (Exception exception) { P3BatchPreviewSummary = $"批量修改失败，事务未部分提交：{exception.Message}"; }
    }

    private void RememberP3MetadataResult(AssetLibraryBatchResult result)
    {
        RememberBrowserMutationResult(result);
    }

    private void OnP3SelectionChanged(IReadOnlyList<AssetItem> _)
    {
        InvalidateP3BatchPreview();
        OnPropertyChanged(nameof(P3BatchSelectionState));
        OnPropertyChanged(nameof(P3BatchCommonState));
        PreviewP3BatchMetadataCommand?.RaiseCanExecuteChanged();
        ApplyP3BatchMetadataCommand?.RaiseCanExecuteChanged();
    }

    private void InvalidateP3BatchPreview()
    {
        var previous = Interlocked.Exchange(ref _p3BatchPreviewCancellation, null);
        previous?.Cancel();
        Interlocked.Increment(ref _p3BatchPreviewGeneration);
        _p3BatchPreviewContract = null;
        P3BatchPreviewReady = false;
        P3BatchPreviewSummary = SelectionCount == 0 ? "先选择素材，再预览批量修改。" : "批量字段已变化，请重新预览影响范围。";
    }

    private void SetP3BatchField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName)) return;
        InvalidateP3BatchPreview();
    }

    private static bool? ParseP3BooleanChoice(string value) => value switch { "是" => true, "否" => false, _ => null };

    private bool HasP3BatchMutation(out string error)
    {
        if (P3BatchTagAction != "不更改" && P3BatchTag is null) { error = "请选择要添加或移除的标签。"; return false; }
        if (P3BatchFolderAction != "不更改" && P3BatchFolder is null) { error = "请选择要加入或移出的文件夹。"; return false; }
        var hasChange = P3BatchTagAction != "不更改" || P3BatchFolderAction != "不更改" ||
                        P3BatchRatingAction != "不更改" || P3BatchCommentAction != "不更改" ||
                        P3BatchArchiveChoice != "不更改" || P3BatchMissingChoice != "不更改";
        error = hasChange ? string.Empty : "请先选择至少一个要修改的元数据字段。";
        return hasChange;
    }

    private void InvalidateP3TagMergePreview()
    {
        var previous = Interlocked.Exchange(ref _p3TagMergePreviewCancellation, null);
        previous?.Cancel();
        Interlocked.Increment(ref _p3TagMergePreviewGeneration);
        _p3TagMergePreviewFingerprint = null;
        P3TagMergePreviewReady = false;
        P3TagMergePreviewSummary = "源标签或目标标签已变化，请重新预览合并影响。";
    }

    private void RaiseP3TagCommands()
    {
        CreateP3TagGroupCommand?.RaiseCanExecuteChanged();
        RenameP3TagGroupCommand?.RaiseCanExecuteChanged();
        ToggleArchiveP3TagGroupCommand?.RaiseCanExecuteChanged();
        CreateP3TagCommand?.RaiseCanExecuteChanged();
        RenameP3TagCommand?.RaiseCanExecuteChanged();
        MoveP3TagCommand?.RaiseCanExecuteChanged();
        PreviewP3TagMergeCommand?.RaiseCanExecuteChanged();
        MergeP3TagCommand?.RaiseCanExecuteChanged();
        ToggleArchiveP3TagCommand?.RaiseCanExecuteChanged();
    }

    private void CancelP3TagManagerWork()
    {
        var search = Interlocked.Exchange(ref _p3TagSearchCancellation, null);
        search?.Cancel();
        var preview = Interlocked.Exchange(ref _p3BatchPreviewCancellation, null);
        preview?.Cancel();
        _p3BatchPreviewContract = null;
        P3BatchPreviewReady = false;
        var mergePreview = Interlocked.Exchange(ref _p3TagMergePreviewCancellation, null);
        mergePreview?.Cancel();
        Interlocked.Increment(ref _p3TagSearchGeneration);
        Interlocked.Increment(ref _p3BatchPreviewGeneration);
        Interlocked.Increment(ref _p3TagMergePreviewGeneration);
    }

    private void DisposeP3TagManager() => CancelP3TagManagerWork();

    private sealed record P3TagMergePreviewState(
        string Fingerprint,
        int SourceAssetCount,
        int TargetAssetCount,
        int DuplicateRelationshipCount);
}

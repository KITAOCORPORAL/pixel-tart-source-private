using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.Duplicates;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.FreeCanvas;

namespace PixelTart.Modules.AssetLibrary;

public sealed record DuplicateCandidateView(DuplicateCandidate Candidate, DuplicateGroupView Owner, bool IsSuggested)
{
    public AssetItem Asset => Candidate.Asset;
    public string Dimensions => Asset.Width is int width && Asset.Height is int height ? $"{width:N0} × {height:N0}" : "尺寸未知";
    public string FileSize => Asset.FileSize < 1024 * 1024 ? $"{Asset.FileSize / 1024d:F0} KB" : $"{Asset.FileSize / 1024d / 1024d:F1} MB";
    public string Rating => Asset.Rating == 0 ? "未评分" : new string('★', Asset.Rating);
    public string SuggestedLabel => IsSuggested ? "建议保留" : string.Empty;
    public string UsageLabel => Candidate.EffectiveUsage.IsInUse
        ? $"使用中 · 画布 {Candidate.EffectiveUsage.CanvasCount} · 灵感板 {Candidate.EffectiveUsage.InspirationBoardCount} · 项目 {Candidate.EffectiveUsage.ProjectCount} · 策划 {Candidate.EffectiveUsage.PlanningCount}"
        : "暂无引用";
}

public sealed class DuplicateGroupView
{
    public DuplicateGroupView(DuplicateGroup group)
    {
        Group = group;
        Header = group.Kind == DuplicateGroupKind.Exact ? $"完全相同 · {group.CopyCount} 份" : $"视觉相似 · {group.CopyCount} 份";
        SimilarityLabel = group.Kind == DuplicateGroupKind.Exact ? "内容一致" : $"相似程度 {group.Similarity:P0}";
        Items = group.Items.Select(item => new DuplicateCandidateView(item, this, item.Asset.AssetId == group.SuggestedKeepAssetId)).ToArray();
    }
    public DuplicateGroup Group { get; }
    public string Header { get; }
    public string SimilarityLabel { get; }
    public IReadOnlyList<DuplicateCandidateView> Items { get; }
}

public sealed partial class AssetLibraryViewModel
{
    private bool _isDuplicateWorkspaceOpen;
    private bool _isDuplicateScanRunning;
    private double _duplicateStrictness = .65;
    private IReadOnlyList<DuplicateCandidate> _duplicateCandidates = [];

    public ObservableCollection<DuplicateGroupView> DuplicateGroups { get; } = [];
    public bool IsDuplicateWorkspaceOpen { get => _isDuplicateWorkspaceOpen; set => SetProperty(ref _isDuplicateWorkspaceOpen, value); }
    public bool IsDuplicateScanRunning { get => _isDuplicateScanRunning; private set => SetProperty(ref _isDuplicateScanRunning, value); }
    public bool HasNoDuplicateGroups => !IsDuplicateScanRunning && DuplicateGroups.Count == 0;
    public double DuplicateStrictness
    {
        get => _duplicateStrictness;
        set
        {
            if (!SetProperty(ref _duplicateStrictness, value) || _duplicateCandidates.Count == 0) return;
            PublishDuplicateGroups(_duplicateCandidates);
        }
    }
    public AsyncCommand<DuplicateCandidateView> ReplaceDuplicateReferencesCommand { get; private set; } = null!;
    public AsyncCommand<DuplicateCandidateView> TrashDuplicateCandidateCommand { get; private set; } = null!;
    public AsyncCommand RefreshDuplicatesCommand { get; private set; } = null!;

    private void InitializeDuplicateFinder()
    {
        ReplaceDuplicateReferencesCommand = new(ReplaceDuplicateReferencesAsync);
        TrashDuplicateCandidateCommand = new(TrashDuplicateCandidateAsync);
        RefreshDuplicatesCommand = new(OpenDuplicateWorkspaceAsync);
    }

    private DuplicateReferenceProtectionService CreateReferenceProtection() => new([
        new InspirationReferenceParticipant(_inspirationTrayDatabasePath),
        new CanvasReferenceParticipant(new CanvasDocumentStore(CanvasDirectory)),
        new ProjectReferenceParticipant(_repository)
    ]);

    private async Task<int> CountDuplicateGroupsAsync(CancellationToken cancellationToken)
    {
        var assets = await LoadAllActiveAssetsAsync(cancellationToken).ConfigureAwait(false);
        return DuplicateFinder.FindExact(assets.Select(asset => new DuplicateCandidate(asset))).Count;
    }

    internal async Task OpenDuplicateWorkspaceAsync()
    {
        if (IsDuplicateScanRunning) return;
        IsDuplicateWorkspaceOpen = true; IsDuplicateScanRunning = true; OnPropertyChanged(nameof(HasNoDuplicateGroups));
        Status = "正在后台扫描重复素材…";
        try
        {
            var assets = await LoadAllActiveAssetsAsync(_lifetimeCancellation.Token);
            var protection = CreateReferenceProtection();
            var candidates = new List<DuplicateCandidate>(assets.Count);
            foreach (var asset in assets)
            {
                _lifetimeCancellation.Token.ThrowIfCancellationRequested();
                AssetVisualAnalysisResult? analysis = null;
                try { analysis = (await _featureStore.GetFeaturesAsync(asset.AssetId, _lifetimeCancellation.Token)).Analysis; } catch (KeyNotFoundException) { }
                var usage = await protection.GetUsageAsync(asset.AssetId, _lifetimeCancellation.Token);
                var tags = (await _repository.ListTagMembershipsAsync(assetId: asset.AssetId, cancellationToken: _lifetimeCancellation.Token)).Count;
                candidates.Add(new(asset, analysis, usage, tags));
            }
            _duplicateCandidates = candidates;
            PublishDuplicateGroups(candidates);
            Status = $"发现 {DuplicateGroups.Count:N0} 个重复或相似素材组；未修改任何源文件。";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception error) { Status = "重复素材扫描暂时无法完成：" + error.Message; }
        finally { IsDuplicateScanRunning = false; OnPropertyChanged(nameof(HasNoDuplicateGroups)); }
    }

    private void PublishDuplicateGroups(IReadOnlyList<DuplicateCandidate> candidates)
    {
        var exact = DuplicateFinder.FindExact(candidates);
        var exactIds = exact.SelectMany(group => group.Items).Select(item => item.Asset.AssetId).ToHashSet();
        var similar = DuplicateFinder.FindSimilar(candidates.Where(item => !exactIds.Contains(item.Asset.AssetId)), DuplicateStrictness);
        DuplicateGroups.Clear();
        foreach (var group in exact.Concat(similar)) DuplicateGroups.Add(new(group));
        OnPropertyChanged(nameof(HasNoDuplicateGroups));
        var nav = SystemCollections.FirstOrDefault(item => item.Collection == AssetLibrarySystemCollection.DuplicateAssets);
        if (nav is not null) nav.Count = DuplicateGroups.Count;
    }

    private async Task<IReadOnlyList<AssetItem>> LoadAllActiveAssetsAsync(CancellationToken cancellationToken)
    {
        var result = new List<AssetItem>(); string? cursor = null;
        do
        {
            var page = await _repository.QueryAsync(new AssetLibraryQuery(PageSize: 500, Cursor: cursor), cancellationToken);
            result.AddRange(page.Items); cursor = page.NextCursor;
        } while (cursor is not null);
        return result;
    }

    private async Task ReplaceDuplicateReferencesAsync(DuplicateCandidateView? target)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Asset.ContentHash)) return;
        var protection = CreateReferenceProtection(); var replaced = 0;
        try
        {
            foreach (var source in target.Owner.Items.Where(item => item.Asset.AssetId != target.Asset.AssetId))
            {
                var result = await protection.ReplaceAsync(source.Asset.AssetId, new(CanvasLibraryId, target.Asset), _lifetimeCancellation.Token);
                replaced += result.UpdatedReferenceCount;
            }
            Status = $"已将 {replaced:N0} 处引用安全替换为 {target.Asset.DisplayName}；未移除素材。";
            await OpenDuplicateWorkspaceAsync();
        }
        catch (Exception error) { Status = error.Message; }
    }

    private async Task TrashDuplicateCandidateAsync(DuplicateCandidateView? candidate)
    {
        if (candidate is null) return;
        try
        {
            await new DuplicateTrashSafetyService(_repository, CreateReferenceProtection()).MoveToTrashAsync(candidate.Asset.AssetId, false, _lifetimeCancellation.Token);
            Status = $"{candidate.Asset.DisplayName} 已移到 Pixel Tart 回收站；磁盘源文件未删除。";
            await OpenDuplicateWorkspaceAsync();
        }
        catch (InvalidOperationException error) { Status = error.Message; }
    }
}

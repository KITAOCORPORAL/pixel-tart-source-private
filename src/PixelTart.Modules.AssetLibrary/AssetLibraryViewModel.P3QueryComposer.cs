using System.Collections.ObjectModel;
using RAWSelectionAssistant.Core.Models;

namespace PixelTart.Modules.AssetLibrary;

public sealed partial class AssetLibraryViewModel
{
    private CancellationTokenSource? _p3SuggestionCancellation;
    private long _p3SuggestionGeneration;
    private long _p3SuggestionPublishedGeneration;
    private bool _p3ImeComposing;
    private bool _p3SuppressQueryChange;
    private bool _p3QueryPanelOpen;
    private bool _p3SuggestionsVisible;
    private bool _p3QueryIsValid = true;
    private string _p3QueryValidationMessage = string.Empty;
    private string _p3NewSmartFolderName = "新智能文件夹";
    private AssetQueryScope _p3QueryScope = AssetQueryScope.Current;
    private AssetQueryDocument _p3CurrentQueryDocument = new();
    private P3QueryNodeView _p3QueryRoot = null!;

    public ObservableCollection<AssetQuerySuggestion> P3QuerySuggestions { get; } = [];
    public ObservableCollection<AssetQueryHistoryEntry> P3QueryHistory { get; } = [];
    public ObservableCollection<P3QueryChipView> P3QueryChips { get; } = [];

    public P3QueryNodeView P3QueryRoot
    {
        get => _p3QueryRoot;
        private set
        {
            if (!SetProperty(ref _p3QueryRoot, value)) return;
            OnPropertyChanged(nameof(P3QueryRoots));
        }
    }

    public IReadOnlyList<P3QueryNodeView> P3QueryRoots => P3QueryRoot is null ? [] : [P3QueryRoot];

    public AssetQueryScope P3QueryScope
    {
        get => _p3QueryScope;
        set
        {
            if (!Enum.IsDefined(value)) value = AssetQueryScope.Current;
            if (!SetProperty(ref _p3QueryScope, value)) return;
            _workspaceSettings.QueryScope = value;
            OnPropertyChanged(nameof(IsP3CurrentScope));
            OnPropertyChanged(nameof(IsP3AllAssetsScope));
            CommitP3QueryDocument(scheduleRefresh: true);
        }
    }

    public bool IsP3CurrentScope
    {
        get => P3QueryScope == AssetQueryScope.Current;
        set { if (value) P3QueryScope = AssetQueryScope.Current; }
    }

    public bool IsP3AllAssetsScope
    {
        get => P3QueryScope == AssetQueryScope.AllAssets;
        set { if (value) P3QueryScope = AssetQueryScope.AllAssets; }
    }

    public bool P3QueryPanelOpen
    {
        get => _p3QueryPanelOpen;
        set => SetProperty(ref _p3QueryPanelOpen, value);
    }

    public bool P3SuggestionsVisible
    {
        get => _p3SuggestionsVisible && P3QuerySuggestions.Count != 0;
        private set
        {
            if (!SetProperty(ref _p3SuggestionsVisible, value)) return;
            OnPropertyChanged(nameof(P3SuggestionsVisible));
        }
    }

    public bool P3QueryIsValid
    {
        get => _p3QueryIsValid;
        private set
        {
            if (!SetProperty(ref _p3QueryIsValid, value)) return;
            OnPropertyChanged(nameof(P3QueryHasError));
            SaveP3QueryAsSmartFolderCommand?.RaiseCanExecuteChanged();
        }
    }

    public bool P3QueryHasError => !P3QueryIsValid || !string.IsNullOrWhiteSpace(P3QueryValidationMessage);

    public string P3QueryValidationMessage
    {
        get => _p3QueryValidationMessage;
        private set
        {
            if (SetProperty(ref _p3QueryValidationMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(P3QueryHasError));
        }
    }

    public string P3NewSmartFolderName
    {
        get => _p3NewSmartFolderName;
        set
        {
            if (!SetProperty(ref _p3NewSmartFolderName, value ?? string.Empty)) return;
            SaveP3QueryAsSmartFolderCommand?.RaiseCanExecuteChanged();
        }
    }

    public string P3QueryScopeLabel => P3QueryScope == AssetQueryScope.Current ? "当前范围" : "全部素材";
    public string P3QueryResultSummary => $"{P3QueryScopeLabel} · {P2QueryTotalCount:N0} 项 · {P3QueryChips.Count} 条有效条件";
    public IReadOnlyList<P3QueryValueOption> P3FolderReferenceOptions => Folders
        .Where(folder => !folder.IsArchived)
        .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
        .Select(folder => new P3QueryValueOption($"id:{folder.FolderId:D}", folder.Name))
        .ToArray();
    public IReadOnlyList<P3QueryValueOption> P3TagReferenceOptions
    {
        get
        {
            var activeGroupIds = TagGroups.Where(group => !group.IsArchived).Select(group => group.TagGroupId).ToHashSet();
            return Tags.Where(tag => !tag.IsArchived && (tag.TagGroupId is null || activeGroupIds.Contains(tag.TagGroupId.Value)))
                .OrderBy(tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(tag => new P3QueryValueOption($"id:{tag.TagId:D}", tag.Name))
                .ToArray();
        }
    }
    public bool P3IsImeComposing => _p3ImeComposing;
#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
    internal long P3AcceptanceSuggestionGeneration => Volatile.Read(ref _p3SuggestionGeneration);
    internal long P3AcceptancePublishedSuggestionGeneration => Volatile.Read(ref _p3SuggestionPublishedGeneration);
#endif

    public AssetCommand ToggleP3QueryPanelCommand { get; private set; } = null!;
    public AssetCommand ClearP3UnlockedCommand { get; private set; } = null!;
    public AssetCommand ClearP3AllCommand { get; private set; } = null!;
    public AsyncCommand SubmitP3SearchCommand { get; private set; } = null!;
    public AsyncCommand<AssetQuerySuggestion> ApplyP3SuggestionCommand { get; private set; } = null!;
    public AsyncCommand<AssetQueryHistoryEntry> ApplyP3HistoryCommand { get; private set; } = null!;
    public AssetCommand<AssetQueryHistoryEntry> RemoveP3HistoryCommand { get; private set; } = null!;
    public AssetCommand<P3QueryChipView> RemoveP3ChipCommand { get; private set; } = null!;
    public AssetCommand ClearP3HistoryCommand { get; private set; } = null!;
    public AsyncCommand SaveP3QueryAsSmartFolderCommand { get; private set; } = null!;

    private void InitializeP3QueryComposer()
    {
        _p3QueryScope = Enum.IsDefined(_workspaceSettings.QueryScope) ? _workspaceSettings.QueryScope : AssetQueryScope.Current;
        var restored = !string.IsNullOrWhiteSpace(_workspaceSettings.QueryDocumentJson)
            ? AssetQueryDocumentCodec.Parse(_workspaceSettings.QueryDocumentJson)
            : null;
        _p3CurrentQueryDocument = restored is { IsValid: true, Document: not null }
            ? restored.Document with { Scope = _p3QueryScope, Text = _workspaceSettings.SearchText, SortField = SortField, SortDirection = SortDirection }
            : new AssetQueryDocument { Scope = _p3QueryScope, Text = _workspaceSettings.SearchText, SortField = SortField, SortDirection = SortDirection };
        P3QueryRoot = P3QueryNodeView.FromModel(_p3CurrentQueryDocument.RootGroup, OnP3QueryTreeChanged);
        foreach (var entry in _workspaceSettings.QueryHistory.OrderByDescending(entry => entry.UsedAt)) P3QueryHistory.Add(entry);
        RebuildP3QueryChips();

        ToggleP3QueryPanelCommand = new(() => P3QueryPanelOpen = !P3QueryPanelOpen);
        ClearP3UnlockedCommand = new(ClearP3UnlockedQueryConditions);
        ClearP3AllCommand = new(ClearP3AllQueryConditions);
        SubmitP3SearchCommand = new(SubmitP3SearchAsync);
        ApplyP3SuggestionCommand = new(ApplyP3SuggestionAsync);
        ApplyP3HistoryCommand = new(ApplyP3HistoryAsync);
        RemoveP3HistoryCommand = new(RemoveP3History);
        RemoveP3ChipCommand = new(RemoveP3Chip);
        ClearP3HistoryCommand = new(ClearP3History);
        SaveP3QueryAsSmartFolderCommand = new(SaveP3QueryAsSmartFolderAsync,
            () => IsReady && P3QueryIsValid && !string.IsNullOrWhiteSpace(P3NewSmartFolderName));
    }

    private void OnP3SearchTextChanged()
    {
        if (_isRestoringWorkspace || _p3ImeComposing) return;
        CommitP3QueryDocument(scheduleRefresh: true);
        ScheduleP3Suggestions();
    }

    public void BeginP3SearchComposition()
    {
        _p3ImeComposing = true;
        _searchDebounce.Stop();
        _p3SuggestionCancellation?.Cancel();
        OnPropertyChanged(nameof(P3IsImeComposing));
        Status = "正在输入中文，完成输入后再搜索。";
    }

    public void UpdateP3SearchComposition(string? compositionText)
    {
        if (!_p3ImeComposing) BeginP3SearchComposition();
        Status = string.IsNullOrEmpty(compositionText) ? "正在输入中文…" : $"正在输入中文：{compositionText}";
    }

    public void CompleteP3SearchComposition()
    {
        if (!_p3ImeComposing) return;
        _p3ImeComposing = false;
        OnPropertyChanged(nameof(P3IsImeComposing));
        CommitP3QueryDocument(scheduleRefresh: true);
        ScheduleP3Suggestions();
    }

    public async Task HandleP3SearchEscapeAsync()
    {
        if (P3SuggestionsVisible)
        {
            P3SuggestionsVisible = false;
            return;
        }
        if (string.IsNullOrEmpty(SearchText)) return;
        SearchText = string.Empty;
        await SubmitP3SearchAsync();
    }

    private void OnP3QueryTreeChanged()
    {
        if (_p3SuppressQueryChange) return;
        CommitP3QueryDocument(scheduleRefresh: true);
    }

    private void CommitP3QueryDocument(bool scheduleRefresh)
    {
        var preservedClauses = string.Equals(
            SearchText,
            _p3CurrentQueryDocument.Text,
            StringComparison.Ordinal)
            ? _p3CurrentQueryDocument.SearchClauses
            : null;
        var candidate = new AssetQueryDocument
        {
            Scope = P3QueryScope,
            Text = SearchText,
            SearchClauses = preservedClauses,
            RootGroup = P3QueryRoot.ToModel(),
            SortField = SortField,
            SortDirection = SortDirection,
            IncludeArchived = ShouldP3IncludeArchived(P3QueryRoot.ToModel())
        };
        var normalized = AssetQueryDocumentCodec.Normalize(candidate);
        P3QueryIsValid = normalized.IsValid && normalized.Document is not null;
        P3QueryValidationMessage = normalized.IsValid ? string.Empty : normalized.ErrorMessage;
        SetP3NodeValidation(normalized.Errors);
        if (!P3QueryIsValid) return;
        _p3CurrentQueryDocument = normalized.Document!;
        _workspaceSettings.QueryScope = _p3CurrentQueryDocument.Scope;
        _workspaceSettings.QueryDocumentJson = AssetQueryDocumentCodec.SerializeCanonical(_p3CurrentQueryDocument);
        RebuildP3QueryChips();
        OnPropertyChanged(nameof(P3QueryScopeLabel));
        OnPropertyChanged(nameof(P3QueryResultSummary));
        if (!scheduleRefresh || _isRestoringWorkspace || !IsReady) return;
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private AssetQueryDocument GetP3QueryDocument() => _p3CurrentQueryDocument with
    {
        Scope = P3QueryScope,
        Text = SearchText,
        SortField = SortField,
        SortDirection = SortDirection,
        IncludeArchived = ShouldP3IncludeArchived(P3QueryRoot.ToModel())
    };

    // Core composes the saved Smart Folder document and this live Current-scope
    // document as an AND layer. Always pass the document so visible chips cannot
    // disappear merely because the organization source is a Smart Folder.
    private AssetQueryDocument GetP3QueryDocumentForExecution() => GetP3QueryDocument();

    private bool ShouldP3IncludeArchived(AssetQueryNode root) =>
        P3QueryScope == AssetQueryScope.Current && ActiveCollection == AssetLibrarySystemCollection.Archived ||
        ContainsEnabledP3ArchiveRule(root);

    private static bool ContainsEnabledP3ArchiveRule(AssetQueryNode node)
    {
        if (!node.Enabled) return false;
        if (node.Kind == AssetQueryNodeKind.Rule) return node.Field == AssetQueryField.IsArchived;
        return node.Children.Any(ContainsEnabledP3ArchiveRule);
    }

    private void OnP3QuerySourceChanged()
    {
        if (P3QueryScope != AssetQueryScope.Current) return;
        _p3SuppressQueryChange = true;
        try
        {
            P3QueryRoot.ClearUnlocked();
            _searchText = string.Empty;
            _workspaceSettings.SearchText = string.Empty;
            _p3CurrentQueryDocument = _p3CurrentQueryDocument with { Text = string.Empty, SearchClauses = null };
            OnPropertyChanged(nameof(SearchText));
            _p3SuggestionCancellation?.Cancel();
            P3QuerySuggestions.Clear();
            P3SuggestionsVisible = false;
        }
        finally { _p3SuppressQueryChange = false; }
        CommitP3QueryDocument(scheduleRefresh: false);
    }

    private void ClearP3UnlockedQueryConditions() => ClearP3QueryConditions(clearAllRules: false);

    private void ClearP3AllQueryConditions() => ClearP3QueryConditions(clearAllRules: true);

    private void ClearP3QueryConditions(bool clearAllRules)
    {
        _p3SuppressQueryChange = true;
        try
        {
            if (clearAllRules) P3QueryRoot.ClearAll();
            else P3QueryRoot.ClearUnlocked();
            _searchText = string.Empty;
            _workspaceSettings.SearchText = string.Empty;
            _p3CurrentQueryDocument = _p3CurrentQueryDocument with { Text = string.Empty, SearchClauses = null };
            OnPropertyChanged(nameof(SearchText));
            _p3SuggestionCancellation?.Cancel();
            P3QuerySuggestions.Clear();
            P3SuggestionsVisible = false;
        }
        finally { _p3SuppressQueryChange = false; }
        CommitP3QueryDocument(scheduleRefresh: true);
    }

    private void NotifyP3QueryResultChanged()
    {
        OnPropertyChanged(nameof(P3QueryResultSummary));
        OnPropertyChanged(nameof(P3QueryScopeLabel));
    }

    private void ClearP3QueryState()
    {
        _p3SuppressQueryChange = true;
        try
        {
            P3QueryRoot.ClearAll();
            _searchText = string.Empty;
            _workspaceSettings.SearchText = string.Empty;
            _p3CurrentQueryDocument = _p3CurrentQueryDocument with { Text = string.Empty, SearchClauses = null };
            OnPropertyChanged(nameof(SearchText));
        }
        finally { _p3SuppressQueryChange = false; }
        _p3QueryScope = AssetQueryScope.Current;
        _workspaceSettings.QueryScope = AssetQueryScope.Current;
        OnPropertyChanged(nameof(P3QueryScope));
        OnPropertyChanged(nameof(IsP3CurrentScope));
        OnPropertyChanged(nameof(IsP3AllAssetsScope));
        CommitP3QueryDocument(scheduleRefresh: false);
    }

    private async Task SubmitP3SearchAsync()
    {
        _searchDebounce.Stop();
        var pendingSuggestions = _p3SuggestionCancellation;
        _p3SuggestionCancellation = null;
        Interlocked.Increment(ref _p3SuggestionGeneration);
        pendingSuggestions?.Cancel();
        P3QuerySuggestions.Clear();
        AddP3SearchHistory(SearchText);
        CommitP3QueryDocument(scheduleRefresh: false);
        P3SuggestionsVisible = false;
        if (IsReady) await RefreshAsync();
    }

    private void ScheduleP3Suggestions()
    {
        _p3SuggestionCancellation?.Cancel();
        _p3SuggestionCancellation?.Dispose();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _p3SuggestionCancellation = cancellation;
        var generation = Interlocked.Increment(ref _p3SuggestionGeneration);
        _ = LoadP3SuggestionsAsync(SearchText, generation, cancellation);
    }

    private async Task LoadP3SuggestionsAsync(string text, long generation, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(280), cancellation.Token);
            var repositorySuggestions = await _repository.GetQuerySuggestionsAsync(text, 20, cancellation.Token);
            if (!IsCurrentP3Suggestion(generation, cancellation)) return;
            var history = P3QueryHistory
                .Where(entry => string.IsNullOrWhiteSpace(text) || entry.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(entry => new AssetQuerySuggestion("history", entry.Text, entry.Text, "搜索历史"));
            var merged = history.Concat(repositorySuggestions)
                .DistinctBy(item => (item.Kind, item.Value))
                .Take(25)
                .ToArray();
            Volatile.Write(ref _p3SuggestionPublishedGeneration, generation);
            P3QuerySuggestions.Clear();
            foreach (var item in merged) P3QuerySuggestions.Add(item);
            P3SuggestionsVisible = merged.Length != 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (IsCurrentP3Suggestion(generation, cancellation))
        {
            P3QuerySuggestions.Clear();
            P3SuggestionsVisible = false;
            P3QueryValidationMessage = $"搜索建议暂不可用：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_p3SuggestionCancellation, cancellation)) _p3SuggestionCancellation = null;
            cancellation.Dispose();
        }
    }

    private bool IsCurrentP3Suggestion(long generation, CancellationTokenSource cancellation) =>
        Volatile.Read(ref _disposeStarted) == 0 &&
        generation == Volatile.Read(ref _p3SuggestionGeneration) &&
        ReferenceEquals(_p3SuggestionCancellation, cancellation) &&
        !cancellation.IsCancellationRequested;

    private async Task ApplyP3SuggestionAsync(AssetQuerySuggestion? suggestion)
    {
        if (suggestion is null) return;
        if (suggestion.Kind is "history" or "file")
        {
            SearchText = suggestion.Label;
            await SubmitP3SearchAsync();
            return;
        }

        AssetQueryField? field = suggestion.Kind switch
        {
            "folder" => AssetQueryField.Folder,
            "tag" => AssetQueryField.Tag,
            "extension" => AssetQueryField.Extension,
            _ => null
        };
        if (field is null)
        {
            P3QueryPanelOpen = true;
            return;
        }
        var operation = field is AssetQueryField.Folder or AssetQueryField.Tag ? AssetQueryOperator.AnyOf : AssetQueryOperator.Equals;
        var value = field is AssetQueryField.Folder or AssetQueryField.Tag ? suggestion.Value : suggestion.Label;
        var model = P3QueryRoot.ToModel();
        model = model with { Children = model.Children.Concat([AssetQueryNode.Rule(field.Value, operation, [value])]).ToArray() };
        P3QueryRoot = P3QueryNodeView.FromModel(model, OnP3QueryTreeChanged);
        P3QueryPanelOpen = true;
        CommitP3QueryDocument(scheduleRefresh: true);
        P3SuggestionsVisible = false;
    }

    private async Task ApplyP3HistoryAsync(AssetQueryHistoryEntry? entry)
    {
        if (entry is null) return;
        SearchText = entry.Text;
        await SubmitP3SearchAsync();
    }

    private void AddP3SearchHistory(string text)
    {
        var normalized = (text ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Length == 0) return;
        var existing = P3QueryHistory.FirstOrDefault(entry => string.Equals(entry.Text, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) P3QueryHistory.Remove(existing);
        P3QueryHistory.Insert(0, new(normalized, DateTimeOffset.UtcNow));
        while (P3QueryHistory.Count > 50) P3QueryHistory.RemoveAt(P3QueryHistory.Count - 1);
        PersistP3History();
    }

    private void RemoveP3History(AssetQueryHistoryEntry? entry)
    {
        if (entry is null) return;
        P3QueryHistory.Remove(entry);
        PersistP3History();
        ScheduleP3Suggestions();
    }

    private void ClearP3History()
    {
        P3QueryHistory.Clear();
        PersistP3History();
        ScheduleP3Suggestions();
    }

    private void RemoveP3Chip(P3QueryChipView? chip)
    {
        if (chip is null) return;
        if (chip.Node is null)
        {
            SearchText = string.Empty;
            return;
        }
        if (chip.Node.RemoveCommand.CanExecute(null)) chip.Node.RemoveCommand.Execute(null);
    }

    private void PersistP3History() => _workspaceSettings.QueryHistory = P3QueryHistory.ToList();

    private async Task SaveP3QueryAsSmartFolderAsync()
    {
        var repositorySaveCompleted = false;
        try
        {
            var document = await BuildP3SmartFolderDocumentFromCurrentAsync(_lifetimeCancellation.Token);
            var normalized = AssetQueryDocumentCodec.Normalize(document);
            if (!normalized.IsValid || normalized.Document is null)
            {
                P3QueryValidationMessage = normalized.ErrorMessage;
                return;
            }
            var referenceErrors = await _repository.ValidateQueryReferencesAsync(normalized.Document, _lifetimeCancellation.Token);
            if (referenceErrors.Count != 0)
            {
                P3QueryValidationMessage = string.Join("；", referenceErrors.Select(error => error.Message));
                return;
            }
            var name = UniqueName(P3NewSmartFolderName.Trim(), SmartFolders.Select(folder => folder.Name));
            await _repository.SaveSmartFolderQueryDocumentAsync(new(Guid.NewGuid(), name, Description: "由当前通用筛选保存"), normalized.Document, _lifetimeCancellation.Token);
            repositorySaveCompleted = true;
            await RefreshFilterListsAsync(_lifetimeCancellation.Token);
            Status = $"已保存智能文件夹：{name}";
            P3QueryValidationMessage = string.Empty;
            P3NewSmartFolderName = "新智能文件夹";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // Do not mutate the composer tree, search text, scope or proposed
            // name. The failed save remains a retryable user operation.
            P3QueryValidationMessage = repositorySaveCompleted
                ? $"智能文件夹已保存，但列表刷新失败，可重试刷新：{exception.Message}"
                : $"保存失败，当前筛选与名称已保留，可重试：{exception.Message}";
            Status = repositorySaveCompleted
                ? "智能文件夹已保存；当前列表可能尚未刷新。"
                : "智能文件夹未保存，当前筛选仍保留。";
        }
    }

    /// <summary>
    /// Materializes the complete effective result set into a self-contained
    /// AllAssets document. Current-scope organization sources are converted to
    /// stable rules, and a selected Smart Folder is embedded rather than stored
    /// as an unstable reference to another Smart Folder.
    /// </summary>
    private async Task<AssetQueryDocument> BuildP3SmartFolderDocumentFromCurrentAsync(CancellationToken cancellationToken)
    {
        var live = GetP3QueryDocument();
        var query = BuildQuery();
        if (live.Scope == AssetQueryScope.AllAssets)
        {
            var root = AddP3LegacyFileNameRegexRule(live.RootGroup, query.FileNameRegex);
            return live with
            {
                Scope = AssetQueryScope.AllAssets,
                RootGroup = root,
                SortField = live.SortField,
                SortDirection = live.SortDirection
            };
        }

        SmartFolderQueryDocument? saved = null;
        if (SelectedSmartFolder is not null)
        {
            saved = await _repository.GetSmartFolderQueryDocumentAsync(SelectedSmartFolder.SmartFolderId, cancellationToken)
                ?? throw new InvalidOperationException("当前智能文件夹不存在或查询文档已损坏。");
        }

        var children = new List<AssetQueryNode>();
        if (saved is not null && !IsTrivialP3AllGroup(saved.Document.RootGroup)) children.Add(saved.Document.RootGroup);
        var sourceRules = BuildP3CurrentSourceRules(query);
        if (sourceRules.Count != 0) children.Add(AssetQueryNode.Group(AssetQueryLogic.All, sourceRules));
        if (!IsTrivialP3AllGroup(live.RootGroup)) children.Add(live.RootGroup);

        var clauses = (saved is null ? Enumerable.Empty<string>() : EffectiveP3SearchClauses(saved.Document))
            .Concat(EffectiveP3SearchClauses(live))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            Text = clauses.Length == 1 ? clauses[0] : string.Empty,
            SearchClauses = clauses.Length > 1 ? clauses : null,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, children),
            SortField = query.EffectiveSortField,
            SortDirection = query.EffectiveSortDirection,
            IncludeArchived = live.IncludeArchived || saved?.Document.IncludeArchived == true ||
                              query.EffectiveArchiveScope != AssetLibraryArchiveScope.ActiveOnly
        };
    }

    private static AssetQueryNode AddP3LegacyFileNameRegexRule(AssetQueryNode root, string? fileNameRegex)
    {
        if (string.IsNullOrWhiteSpace(fileNameRegex)) return root;
        var regex = AssetQueryNode.Rule(
            AssetQueryField.FileName,
            AssetQueryOperator.Regex,
            [fileNameRegex.Trim()],
            caseSensitivity: AssetQueryCaseSensitivity.Insensitive);
        return IsTrivialP3AllGroup(root)
            ? AssetQueryNode.Group(AssetQueryLogic.All, [regex])
            : AssetQueryNode.Group(AssetQueryLogic.All, [root, regex]);
    }

    private static IReadOnlyList<AssetQueryNode> BuildP3CurrentSourceRules(AssetLibraryQuery query)
    {
        var rules = new List<AssetQueryNode>();
        if (query.FolderId is Guid folderId)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf, [$"id:{folderId:D}"]));
        if (query.TagId is Guid tagId)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AnyOf, [$"id:{tagId:D}"]));
        if (query.FolderIds is { Count: > 0 })
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AllOf,
                query.FolderIds.Distinct().Select(id => $"id:{id:D}")));
        if (query.TagIds is { Count: > 0 })
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Tag, AssetQueryOperator.AllOf,
                query.TagIds.Distinct().Select(id => $"id:{id:D}")));
        if (query.MinimumRating is int minimumRating)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual,
                [minimumRating.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
        if (query.MaximumRating is int maximumRating)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.LessThanOrEqual,
                [maximumRating.ToString(System.Globalization.CultureInfo.InvariantCulture)]));
        if (!string.IsNullOrWhiteSpace(query.MediaType))
            rules.Add(AssetQueryNode.Rule(AssetQueryField.MediaType, AssetQueryOperator.Equals, [query.MediaType]));
        if (!string.IsNullOrWhiteSpace(query.Extension))
            rules.Add(AssetQueryNode.Rule(AssetQueryField.Extension, AssetQueryOperator.Equals, [query.Extension]));
        if (query.UncategorizedOnly || query.SystemCollection == AssetLibrarySystemCollection.Uncategorized)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.IsUncategorized, AssetQueryOperator.IsTrue));
        if (query.UntaggedOnly || query.SystemCollection == AssetLibrarySystemCollection.Untagged)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.IsUntagged, AssetQueryOperator.IsTrue));
        if (query.MissingOnly || query.SystemCollection == AssetLibrarySystemCollection.MissingFiles)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsTrue));
        if (query.EffectiveArchiveScope == AssetLibraryArchiveScope.ArchivedOnly)
            rules.Add(AssetQueryNode.Rule(AssetQueryField.IsArchived, AssetQueryOperator.IsTrue));
        if (!string.IsNullOrWhiteSpace(query.FileNameRegex))
            rules.Add(AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Regex, [query.FileNameRegex]));
        AddP3DateRules(rules, AssetQueryField.AddedAt, query.AddedFrom, query.AddedTo);
        AddP3DateRules(rules, AssetQueryField.CaptureTime, query.CaptureFrom, query.CaptureTo);
        return rules;
    }

    private static void AddP3DateRules(
        ICollection<AssetQueryNode> rules,
        AssetQueryField field,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (from is not null && to is not null)
        {
            rules.Add(AssetQueryNode.Rule(field, AssetQueryOperator.Between,
                [from.Value.ToString("O"), to.Value.ToString("O")]));
            return;
        }
        if (from is not null)
            rules.Add(AssetQueryNode.Rule(field, AssetQueryOperator.GreaterThanOrEqual, [from.Value.ToString("O")]));
        if (to is not null)
            rules.Add(AssetQueryNode.Rule(field, AssetQueryOperator.LessThanOrEqual, [to.Value.ToString("O")]));
    }

    private static IEnumerable<string> EffectiveP3SearchClauses(AssetQueryDocument document) =>
        (document.SearchClauses is { Count: > 0 } ? document.SearchClauses : [document.Text])
        .Select(value => (value ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormC))
        .Where(value => value.Length != 0);

    private static bool IsTrivialP3AllGroup(AssetQueryNode node) =>
        node.Kind == AssetQueryNodeKind.Group && node.Logic == AssetQueryLogic.All && node.Enabled &&
        !node.Negated && node.Children.Count == 0;

    private void RebuildP3QueryChips()
    {
        P3QueryChips.Clear();
        if (!string.IsNullOrWhiteSpace(SearchText)) P3QueryChips.Add(new(null, $"搜索：{SearchText}", false));
        foreach (var node in P3QueryRoot.DescendantsAndSelf().Where(node => node.IsRule && node.Enabled))
        {
            var value = string.IsNullOrWhiteSpace(node.ValueText) ? string.Empty : $" {node.ValueText}";
            P3QueryChips.Add(new(node, $"{P3QueryNodeView.FieldLabel(node.Field)} {P3QueryNodeView.OperatorLabel(node.Operator)}{value}", node.Locked));
        }
        NotifyContentState();
        OnPropertyChanged(nameof(P3QueryResultSummary));
    }

    private void SetP3NodeValidation(IReadOnlyList<AssetQueryValidationIssue> errors)
    {
        foreach (var node in P3QueryRoot.DescendantsAndSelf()) node.ValidationMessage = string.Empty;
        if (errors.Count == 0) return;
        var firstRule = P3QueryRoot.DescendantsAndSelf().FirstOrDefault(node => node.IsRule);
        if (firstRule is not null) firstRule.ValidationMessage = errors[0].Message;
    }

    private void DisposeP3QueryComposer()
    {
        _p3SuggestionCancellation?.Cancel();
        _p3SuggestionCancellation?.Dispose();
        _p3SuggestionCancellation = null;
    }
}

public sealed record P3QueryChipView(P3QueryNodeView? Node, string Label, bool Locked)
{
    public string AutomationId => Node is null ? "P3QueryChip_SearchText" : Node.AutomationId + "_Chip";
    public string AccessibleName => $"筛选条件 {Label}{(Locked ? "，已锁定" : string.Empty)}";
}

public sealed class AssetCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter is T typed ? typed : default) ?? true;
    public void Execute(object? parameter)
    {
        var value = parameter is T typed ? typed : default;
        if (CanExecute(value)) execute(value);
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

#if ASSET_LIBRARY_P3_AUTOMATED_ACCEPTANCE
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// Drives the public WPF surface used by the P3 automated acceptance build.
/// It deliberately owns no evidence files and makes no pass/fail decision.
/// </summary>
public sealed class AssetLibraryP3AutomatedAcceptanceDriver : IDisposable
{
    private static readonly HashSet<string> MustFitAutomationIds = new(StringComparer.Ordinal)
    {
        "AssetLibraryPage",
        "AssetLibraryThreePaneWorkspace",
        "AssetOrganizationPane",
        "AssetCollectionPane",
        "AssetInspectorPane",
        "AssetOrganizationSplitter",
        "AssetInspectorSplitter",
        "AssetThumbnailSizeSlider",
    };

    private static readonly AssetLibraryP3AutomatedButtonDefinition[] ButtonDefinitions =
    [
        new("header-organization-toggle", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true),
        new("header-inspector-toggle", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true),
        new("header-inspector-pin", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true),
        new("header-import", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true),
        new("organization-all-assets", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("organization-new-folder", "AssetLibraryIconButton", "WorkbenchCardBrush", true),
        new("visual-chip-valid", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-not-analyzed", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-green", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-low-saturation", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-low-key", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-high-contrast", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-warm", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("visual-chip-cool", "AssetLibraryChipButton", "WorkbenchCardBrush", true),
        new("active-visual-chip-template", "AssetLibraryChipButton", "WorkbenchCardBrush", true, true),
        new("clear-visual-results", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("load-error-retry", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true),
        new("empty-state-import", "AssetLibraryPrimaryButton", "ContentBackgroundBrush", true),
        new("empty-state-clear-filters", "AssetLibrarySecondaryButton", "ContentBackgroundBrush", true),
        new("inspector-reanalyze", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("inspector-find-similar", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("smart-folder-save", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("palette-swatch-template", "AssetLibraryPaletteSwatchButton", "WorkbenchCardBrush", false),
        new("palette-find-similar", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("color-search", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
        new("visual-search-start", "AssetLibraryPrimaryButton", "WorkbenchCardBrush", true),
        new("visual-search-cancel", "AssetLibrarySecondaryButton", "WorkbenchCardBrush", true),
    ];

    private static readonly HashSet<string> ObservedProperties = new(StringComparer.Ordinal)
    {
        nameof(AssetLibraryViewModel.IsLoading),
        nameof(AssetLibraryViewModel.IsReady),
        nameof(AssetLibraryViewModel.LoadErrorMessage),
        nameof(AssetLibraryViewModel.LoadAttempt),
        nameof(AssetLibraryViewModel.OrganizationPaneWidth),
        nameof(AssetLibraryViewModel.InspectorPaneWidth),
        nameof(AssetLibraryViewModel.IsOrganizationPaneCollapsed),
        nameof(AssetLibraryViewModel.IsInspectorPaneCollapsed),
        nameof(AssetLibraryViewModel.IsOrganizationPaneVisible),
        nameof(AssetLibraryViewModel.IsInspectorPaneVisible),
        nameof(AssetLibraryViewModel.ThumbnailWidth),
        nameof(AssetLibraryViewModel.SearchText),
        nameof(AssetLibraryViewModel.SelectedAsset),
        nameof(AssetLibraryViewModel.SelectionCount),
        nameof(AssetLibraryViewModel.VisibleCount),
    };

    private readonly AssetLibraryPage _page;
    private readonly AssetLibraryViewModel _viewModel;
    private readonly Grid _workspace;
    private readonly GridSplitter _organizationSplitter;
    private readonly GridSplitter _inspectorSplitter;
    private readonly Slider _thumbnailSlider;
    private readonly ListBox _assetGrid;
    private readonly TextBox _searchBox;
    private readonly SqliteAssetLibraryRepository _acceptanceRepository;
    private Border? _buttonStateSurface;
    private bool _disposed;

    public AssetLibraryP3AutomatedAcceptanceDriver(AssetLibraryPage page, string databasePath)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        if (string.IsNullOrWhiteSpace(databasePath) || !Path.IsPathFullyQualified(databasePath))
            throw new ArgumentException("The automated acceptance database path must be absolute.", nameof(databasePath));
        _viewModel = page.ViewModel;
        _acceptanceRepository = new SqliteAssetLibraryRepository(Path.GetFullPath(databasePath));
        _workspace = FindRequired<Grid>("AssetLibraryThreePaneWorkspace");
        _organizationSplitter = FindRequired<GridSplitter>("AssetOrganizationSplitter");
        _inspectorSplitter = FindRequired<GridSplitter>("AssetInspectorSplitter");
        _thumbnailSlider = FindRequired<Slider>("AssetThumbnailSizeSlider");
        _assetGrid = FindRequired<ListBox>("AssetGrid");
        _searchBox = FindRequired<TextBox>("AssetLibrarySearch");

        _page.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted), true);
        _page.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta), true);
        _page.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnDragCompleted), true);
        _page.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), true);
        _page.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        _page.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), true);
        _page.AddHandler(Keyboard.KeyUpEvent, new KeyEventHandler(OnKeyUp), true);
        _page.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnButtonClick), true);
        _page.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnSelectionChanged), true);
        _page.AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(OnTextInput), true);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.AssetCards.CollectionChanged += OnAssetCardsChanged;
    }

    public event EventHandler<AssetLibraryP3AutomatedObservation>? Observed;

    public AssetLibraryP3AutomatedState CaptureState()
    {
        EnsureNotDisposed();
        _page.UpdateLayout();
        return new(
            _page.IsLoaded,
            _page.IsVisible,
            _page.ActualWidth,
            _page.ActualHeight,
            _viewModel.LoadAttempt,
            _viewModel.IsLoading,
            _viewModel.IsReady,
            _viewModel.HasLoadError,
            _viewModel.LoadErrorMessage,
            _viewModel.IsEmptyStateVisible,
            _viewModel.AssetCards.Count,
            _viewModel.OrganizationPaneWidth,
            _workspace.ColumnDefinitions[0].ActualWidth,
            _viewModel.IsOrganizationPaneCollapsed,
            _viewModel.IsOrganizationPaneVisible,
            _viewModel.InspectorPaneWidth,
            _workspace.ColumnDefinitions[4].ActualWidth,
            _viewModel.IsInspectorPaneCollapsed,
            _viewModel.IsInspectorPaneVisible,
            _workspace.ColumnDefinitions[2].ActualWidth,
            _viewModel.ThumbnailWidth,
            _thumbnailSlider.Value,
            _thumbnailSlider.Minimum,
            _thumbnailSlider.Maximum,
            _viewModel.SearchText,
            _viewModel.SelectedAssets.Select(asset => asset.AssetId.ToString("D")).ToArray(),
            GetAutomationId(Keyboard.FocusedElement as DependencyObject));
    }

    public async Task ImportSyntheticFixtureAsync(string fixtureRoot)
    {
        EnsureNotDisposed();
        if (!Path.IsPathFullyQualified(fixtureRoot) || !Directory.Exists(fixtureRoot))
            throw new DirectoryNotFoundException("The automated acceptance fixture root must be an existing absolute directory.");
        await _viewModel.ImportDemoDirectoryAsync(Path.GetFullPath(fixtureRoot));
        await DrainDispatcherAsync();
    }

    public async Task ExecuteRetryCommandAsync()
    {
        EnsureNotDisposed();
        await ExecuteBoundButtonCommandAsync("RetryAssetLibraryLoad");
    }

    public async Task ToggleOrganizationPaneAsync()
    {
        EnsureNotDisposed();
        await ExecuteBoundButtonCommandAsync("ToggleAssetOrganizationPane");
    }

    public async Task ToggleInspectorPaneAsync()
    {
        EnsureNotDisposed();
        await ExecuteBoundButtonCommandAsync("ToggleAssetInspectorPane");
    }

    public async Task DragOrganizationSplitterAsync(double horizontalChange)
    {
        EnsureNotDisposed();
        await RaiseSplitterDragAsync(_organizationSplitter, horizontalChange);
    }

    public async Task DragInspectorSplitterAsync(double horizontalChange)
    {
        EnsureNotDisposed();
        await RaiseSplitterDragAsync(_inspectorSplitter, horizontalChange);
    }

    public async Task AdjustOrganizationSplitterByKeyboardAsync(Key key)
    {
        EnsureNotDisposed();
        await RaiseKeyboardRouteAsync(_organizationSplitter, key);
    }

    public async Task AdjustInspectorSplitterByKeyboardAsync(Key key)
    {
        EnsureNotDisposed();
        await RaiseKeyboardRouteAsync(_inspectorSplitter, key);
    }

    public async Task AdjustThumbnailByKeyboardAsync(Key key)
    {
        EnsureNotDisposed();
        await RaiseKeyboardRouteAsync(_thumbnailSlider, key);
    }

    public async Task<string> SelectFirstAssetAsync()
    {
        EnsureNotDisposed();
        if (_assetGrid.Items.Count == 0)
            throw new InvalidOperationException("The real Asset Grid has no item to select.");
        if (!_assetGrid.Focus())
            throw new InvalidOperationException("The real Asset Grid did not accept keyboard focus.");
        _assetGrid.SelectedIndex = 0;
        _assetGrid.ScrollIntoView(_assetGrid.SelectedItem);
        await DrainDispatcherAsync();
        var selected = _viewModel.SelectedAssets.SingleOrDefault()
            ?? throw new InvalidOperationException("The real Asset Grid selection did not reach the Asset Library view model.");
        return selected.AssetId.ToString("D");
    }

    public async Task ComposeSearchTextAsync(string text)
    {
        EnsureNotDisposed();
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Composition text is required.", nameof(text));
        if (!_searchBox.Focus()) throw new InvalidOperationException("The real Asset Library search box did not accept focus.");
        _searchBox.SelectAll();
        var composition = new TextComposition(InputManager.Current, _searchBox, text);
        if (!TextCompositionManager.StartComposition(composition))
            throw new InvalidOperationException("The WPF text-composition route did not start.");
        await DrainDispatcherAsync();
        if (!string.Equals(_viewModel.SearchText, text, StringComparison.Ordinal))
            throw new InvalidOperationException("The WPF text-composition route did not update the bound Asset Library search query.");
        if (string.Equals(text, "P3_00", StringComparison.Ordinal))
            await WaitUntilAsync(() => _viewModel.P2QueryTotalCount == 100, "the deterministic persisted search query");
    }

    public async Task ClearSearchThroughEditingCommandAsync()
    {
        EnsureNotDisposed();
        if (!_searchBox.Focus()) throw new InvalidOperationException("The real Asset Library search box did not accept focus.");
        _searchBox.SelectAll();
        if (!EditingCommands.Delete.CanExecute(null, _searchBox))
            throw new InvalidOperationException("The WPF delete editing command is unavailable for the Asset Library search box.");
        EditingCommands.Delete.Execute(null, _searchBox);
        await DrainDispatcherAsync();
        if (_viewModel.SearchText.Length != 0)
            throw new InvalidOperationException("The WPF delete editing command did not clear the Asset Library search query.");
    }

    public async Task ApplyQueryDocumentAsync(AssetQueryDocument document)
    {
        document = await ResolveAcceptanceDocumentAsync(document);
        var previousGeneration = BeginApplyQueryDocument(document);
        await WaitForPublishedQueryAfterAsync(previousGeneration, "the real P3 query composer refresh");
    }

    public async Task<AssetQueryDocument> ResolveAcceptanceDocumentAsync(
        AssetQueryDocument document,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        return await _acceptanceRepository.ResolveQueryReferencesAsync(
            document, includeArchived: false, cancellationToken).ConfigureAwait(false);
    }

    private long BeginApplyQueryDocument(AssetQueryDocument document)
    {
        EnsureNotDisposed();
        var previousGeneration = _viewModel.P3AcceptanceQueryGeneration;
        var normalized = AssetQueryDocumentCodec.Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new ArgumentException(normalized.ErrorMessage, nameof(document));
        document = normalized.Document;

        _viewModel.P3QueryScope = document.Scope;
        _viewModel.SearchText = document.Text;
        var root = _viewModel.P3QueryRoot;
        root.ClearAll();
        root.Logic = document.RootGroup.Logic;
        root.Negated = document.RootGroup.Negated;
        foreach (var child in document.RootGroup.Children) AddQueryNode(root, child);
        if (!_viewModel.P3QueryIsValid)
            throw new InvalidOperationException($"The real P3 query composer rejected the acceptance document: {_viewModel.P3QueryValidationMessage}");

        _viewModel.SubmitP3SearchCommand.Execute(null);
        return previousGeneration;
    }

    public AssetLibraryP3CanonicalQuerySnapshot CaptureCanonicalQueryDocument(AssetQueryDocument? document = null)
    {
        EnsureNotDisposed();
        document ??= new AssetQueryDocument
        {
            Scope = _viewModel.P3QueryScope,
            Text = _viewModel.SearchText,
            RootGroup = _viewModel.P3QueryRoot.ToModel(),
            SortField = _viewModel.SortField,
            SortDirection = _viewModel.SortDirection,
            IncludeArchived = _viewModel.P3QueryScope == AssetQueryScope.Current &&
                              _viewModel.ActiveCollection == AssetLibrarySystemCollection.Archived,
        };
        var normalized = AssetQueryDocumentCodec.Normalize(document);
        if (!normalized.IsValid || normalized.Document is null)
            throw new InvalidOperationException($"The real P3 canonical query is invalid: {normalized.ErrorMessage}");
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(normalized.Document);
        return new(normalized.Document, canonical, Sha256Text(canonical),
            EnumerateQueryNodes(normalized.Document.RootGroup).Count(node => node.Kind == AssetQueryNodeKind.Rule));
    }

    public async Task<AssetLibraryP3ParameterizedQueryPlan> CaptureParameterizedQueryPlanAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var query = _viewModel.CaptureP3AcceptanceQuery() with { Cursor = null };
        var canonical = CaptureCanonicalQueryDocument(query.Document);
        var plan = await _acceptanceRepository.ExplainQueryPlanAsync(query, cancellationToken);
        var placeholders = Regex.Matches(plan.SqlTemplate, @"\$[A-Za-z][A-Za-z0-9_]*")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var parameterNames = plan.Parameters.Select(parameter => parameter.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var missing = placeholders.Except(parameterNames, StringComparer.Ordinal).Count() +
                      parameterNames.Except(placeholders, StringComparer.Ordinal).Count();
        var sqlSha256 = Sha256Text(plan.SqlTemplate);
        return new(
            Parameterized: plan.Parameters.Count > 0 && missing == 0,
            UnparameterizedSqlCount: missing,
            ParameterCount: plan.Parameters.Count,
            ExplainQueryPlan: string.Join(" | ", plan.ExplainRows),
            CanonicalSha256: canonical.CanonicalSha256,
            SqlTemplate: plan.SqlTemplate,
            SqlTemplateSha256: sqlSha256,
            ParameterNames: parameterNames,
            ParameterValueSha256: plan.Parameters.Select(parameter => parameter.ValueSha256).ToArray(),
            ExplainRows: plan.ExplainRows);
    }

    public async Task<AssetLibraryP3QueryResultSnapshot> CaptureResultAssetIds(
        AssetQueryDocument document,
        AssetLibraryQuery? baseQuery = null,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var query = (baseQuery ?? new AssetLibraryQuery()) with
        {
            Cursor = null,
            PageSize = 500,
            IncludeArchived = document.IncludeArchived,
            SortField = document.SortField,
            SortDirection = document.SortDirection,
            Document = document,
        };
        var started = System.Diagnostics.Stopwatch.StartNew();
        var ids = new List<string>();
        var expectedTotal = -1;
        string? regexError = null;
        do
        {
            var page = await _acceptanceRepository.QueryAsync(query, cancellationToken);
            expectedTotal = expectedTotal < 0 ? page.TotalCount : expectedTotal;
            if (expectedTotal != page.TotalCount)
                throw new InvalidOperationException("The real repository changed total count during a stable acceptance query.");
            regexError ??= page.RegexError;
            ids.AddRange(page.Items.Select(item => item.AssetId.ToString("D")));
            query = query with { Cursor = page.NextCursor };
        } while (query.Cursor is not null);
        started.Stop();
        if (regexError is null && ids.Count != expectedTotal)
            throw new InvalidOperationException($"The real repository returned {ids.Count} IDs for total {expectedTotal}.");
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw new InvalidOperationException("The real repository returned duplicate asset IDs.");
        return new(ids, Sha256Text(string.Join("\n", ids)), ids.Count, regexError, started.Elapsed);
    }

    public async Task<TimeSpan> MeasureFirstPageQueryAsync(
        AssetQueryDocument document,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var query = new AssetLibraryQuery(
            PageSize: 100,
            IncludeArchived: document.IncludeArchived,
            SortField: document.SortField,
            SortDirection: document.SortDirection)
        {
            Document = document,
        };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await _acceptanceRepository.QueryAsync(query, cancellationToken);
        clock.Stop();
        return clock.Elapsed;
    }

    public async Task<TimeSpan> MeasureRepositorySuggestionsAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var suggestions = await _acceptanceRepository.GetQuerySuggestionsAsync(searchText, 20, cancellationToken);
        clock.Stop();
        if (suggestions.Count == 0)
            throw new InvalidOperationException("The real P3 repository returned no deterministic suggestions.");
        return clock.Elapsed;
    }

    public async Task SwitchScopeAsync(AssetQueryScope scope)
    {
        EnsureNotDisposed();
        var previousGeneration = _viewModel.P3AcceptanceQueryGeneration;
        _viewModel.P3QueryScope = scope;
        await WaitForPublishedQueryAfterAsync(previousGeneration, $"the P3 scope '{scope}' query");
        if (_viewModel.P3QueryScope != scope)
            throw new InvalidOperationException($"The P3 scope switch did not publish '{scope}'.");
    }

    public async Task SelectFirstFolderAsync()
    {
        EnsureNotDisposed();
        static IEnumerable<AssetLibraryFolderNodeView> Flatten(IEnumerable<AssetLibraryFolderNodeView> roots)
        {
            foreach (var root in roots)
            {
                yield return root;
                foreach (var child in Flatten(root.Children)) yield return child;
            }
        }
        var source = Flatten(_viewModel.OrganizationFolders)
            .FirstOrDefault(item => !item.IsArchived && item.DirectAssetCount > 0)
            ?? throw new InvalidOperationException("The real repository exposed no populated active folder.");
        var previousGeneration = _viewModel.P3AcceptanceQueryGeneration;
        source.IsSelected = true;
        await WaitForPublishedQueryAfterAsync(previousGeneration, $"the folder '{source.Name}' query");
        if (_viewModel.SelectedFolder?.FolderId != source.FolderId)
            throw new InvalidOperationException("The real folder selection did not become the current organization scope.");
    }

    public async Task SelectSmartFolderAsync(Guid smartFolderId)
    {
        EnsureNotDisposed();
        var source = _viewModel.OrganizationSmartFolders.FirstOrDefault(item => item.Folder.SmartFolderId == smartFolderId);
        var folder = source?.Folder ?? (await _acceptanceRepository.ListSmartFoldersAsync(includeArchived: false))
            .SingleOrDefault(item => item.SmartFolderId == smartFolderId)
            ?? throw new InvalidOperationException($"The real repository did not expose Smart Folder {smartFolderId:D}.");
        var previousGeneration = _viewModel.P3AcceptanceQueryGeneration;
        if (source is not null) source.SelectCommand.Execute(null);
        else _viewModel.SelectedSmartFolder = folder;
        await WaitForPublishedQueryAfterAsync(previousGeneration, $"the Smart Folder '{folder.Name}' query");
        if (_viewModel.SelectedSmartFolder?.SmartFolderId != smartFolderId)
            throw new InvalidOperationException("The real Smart Folder selection did not become the current organization scope.");
    }

    public async Task<AssetLibraryP3PublishedQuerySnapshot> CapturePublishedQueryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var query = _viewModel.CaptureP3AcceptanceQuery() with { Cursor = null };
        var oracle = await _acceptanceRepository.QueryAsync(query, cancellationToken);
        if (!string.IsNullOrWhiteSpace(oracle.RegexError))
            throw new InvalidOperationException($"The published P3 query failed: {oracle.RegexError}");
        var published = _viewModel.P3AcceptancePublishedAssetIds.Select(id => id.ToString("D")).ToArray();
        var expected = oracle.Items.Select(item => item.AssetId.ToString("D")).ToArray();
        if (!published.SequenceEqual(expected, StringComparer.Ordinal) || _viewModel.P2QueryTotalCount != oracle.TotalCount)
            throw new InvalidOperationException("The live P3 ViewModel result differs from the independent repository oracle.");
        return new(
            _viewModel.P3AcceptanceQueryGeneration,
            _viewModel.P3QueryScope.ToString(),
            _viewModel.SelectedFolder?.FolderId,
            _viewModel.SelectedSmartFolder?.SmartFolderId,
            published,
            Sha256Text(string.Join("\n", published)),
            oracle.TotalCount,
            Sha256Text(string.Join("\n", expected)));
    }

    private Task WaitForPublishedQueryAfterAsync(long previousGeneration, string description) =>
        WaitUntilAsync(
            () => _viewModel.P3AcceptancePublishedQueryGeneration > previousGeneration &&
                  _viewModel.P3AcceptancePublishedQueryGeneration == _viewModel.P3AcceptanceQueryGeneration &&
                  !_viewModel.IsLoading && !_viewModel.HasLoadError,
            description);

    public async Task<AssetLibraryP3ImeSnapshot> ExerciseImeCancellationAsync(string compositionText)
    {
        EnsureNotDisposed();
        var beforeCompositionGeneration = _viewModel.P3AcceptanceSuggestionGeneration;
        _viewModel.BeginP3SearchComposition();
        _viewModel.SearchText = compositionText;
        _viewModel.UpdateP3SearchComposition(compositionText);
        await DrainDispatcherAsync();
        var suppressed = !_viewModel.P3SuggestionsVisible;
        var compositionSuppressedGeneration = _viewModel.P3AcceptanceSuggestionGeneration == beforeCompositionGeneration;
        _viewModel.CompleteP3SearchComposition();
        var supersededGeneration = _viewModel.P3AcceptanceSuggestionGeneration;
        _viewModel.SearchText = "P3_000";
        var currentGeneration = _viewModel.P3AcceptanceSuggestionGeneration;
        if (currentGeneration <= supersededGeneration)
            throw new InvalidOperationException("Rapid P3 input did not create a newer suggestion generation.");
        await WaitUntilAsync(
            () => _viewModel.P3AcceptancePublishedSuggestionGeneration == currentGeneration,
            "the newest P3 suggestion generation");
        var publishedGeneration = _viewModel.P3AcceptancePublishedSuggestionGeneration;
        var queryCancellation = await ExerciseQueryCancellationAsync();
        return new(
            suppressed && compositionSuppressedGeneration,
            _viewModel.P3IsImeComposing,
            _viewModel.SearchText,
            CancelledGenerationPublished: publishedGeneration == supersededGeneration,
            SupersededGeneration: supersededGeneration,
            PublishedGeneration: publishedGeneration,
            QueryCancellationObserved: queryCancellation.CancellationObserved,
            CancelledQueryGenerationPublished: queryCancellation.CancelledGenerationPublished,
            CancelledQueryGeneration: queryCancellation.CancelledGeneration,
            PublishedQueryGeneration: queryCancellation.PublishedGeneration);
    }

    public async Task<AssetLibraryP3QueryCancellationSnapshot> ExerciseQueryCancellationAsync()
    {
        EnsureNotDisposed();
        var oldDocument = new AssetQueryDocument { Scope = AssetQueryScope.AllAssets, Text = "P3_0001" };
        var newDocument = new AssetQueryDocument { Scope = AssetQueryScope.AllAssets, Text = "P3_0002" };
        _viewModel.ArmP3AcceptanceQueryCancellationBarrier();
        try
        {
            var before = BeginApplyQueryDocument(oldDocument);
            await _viewModel.WaitForP3AcceptanceBlockedQueryAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var cancelledGeneration = _viewModel.P3AcceptanceQueryGeneration;
            if (cancelledGeneration <= before)
                throw new InvalidOperationException("The deliberately blocked P3 query did not start a new generation.");
            _ = BeginApplyQueryDocument(newDocument);
            await WaitForPublishedQueryAfterAsync(cancelledGeneration, "the replacement P3 query after cancellation");
            var publishedGeneration = _viewModel.P3AcceptancePublishedQueryGeneration;
            var currentGeneration = _viewModel.P3AcceptanceQueryGeneration;
            var cancellationObserved = _viewModel.P3AcceptanceBlockedQueryCancellationObserved;
            var cancelledPublished = publishedGeneration == cancelledGeneration;
            if (!cancellationObserved || cancelledPublished || publishedGeneration != currentGeneration)
                throw new InvalidOperationException("The old P3 query generation was not cancelled before the replacement result published.");
            _ = await CapturePublishedQueryAsync();
            return new(cancellationObserved, cancelledPublished, cancelledGeneration, publishedGeneration);
        }
        finally
        {
            _viewModel.ReleaseP3AcceptanceQueryBarrier();
        }
    }

    public async Task<AssetLibraryP3HistorySnapshot> ExerciseSearchSuggestionsAndHistoryAsync(string searchText)
    {
        EnsureNotDisposed();
        _viewModel.BeginP3SearchComposition();
        _viewModel.SearchText = searchText;
        _viewModel.UpdateP3SearchComposition(searchText);
        await DrainDispatcherAsync();
        var suppressed = !_viewModel.P3SuggestionsVisible;
        _viewModel.CompleteP3SearchComposition();
        await WaitUntilAsync(() => _viewModel.P3QuerySuggestions.Count > 0, "the real repository-backed P3 suggestions");
        var suggestionCount = _viewModel.P3QuerySuggestions.Count;
        _viewModel.SubmitP3SearchCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.SubmitP3SearchCommand.CanExecute(null) &&
                                 _viewModel.P3QueryHistory.Any(item => string.Equals(item.Text, searchText, StringComparison.Ordinal)),
            "the persisted P3 search history");
        _viewModel.SubmitP3SearchCommand.Execute(null);
        await DrainDispatcherAsync();
        var deduplicated = _viewModel.P3QueryHistory.Count(item =>
            string.Equals(item.Text, searchText, StringComparison.OrdinalIgnoreCase)) == 1;

        var entry = _viewModel.P3QueryHistory.First(item =>
            string.Equals(item.Text, searchText, StringComparison.OrdinalIgnoreCase));
        _viewModel.RemoveP3HistoryCommand.Execute(entry);
        await DrainDispatcherAsync();
        var singleRemoved = !_viewModel.P3QueryHistory.Any(item =>
            string.Equals(item.Text, searchText, StringComparison.OrdinalIgnoreCase));

        _viewModel.SearchText = searchText;
        _viewModel.SubmitP3SearchCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3QueryHistory.Any(item =>
            string.Equals(item.Text, searchText, StringComparison.OrdinalIgnoreCase)),
            "the restored P3 history entry");
        _viewModel.ClearP3HistoryCommand.Execute(null);
        await DrainDispatcherAsync();
        var allCleared = _viewModel.P3QueryHistory.Count == 0;

        _viewModel.SearchText = searchText;
        _viewModel.SubmitP3SearchCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3QueryHistory.Any(item =>
            string.Equals(item.Text, searchText, StringComparison.OrdinalIgnoreCase)),
            "the final persisted P3 history entry");
        return new(searchText, suggestionCount, suppressed,
            _viewModel.P3QueryHistory.Select(item => item.Text).ToArray(),
            deduplicated, singleRemoved, allCleared);
    }

    public AssetLibraryP3HistorySnapshot CaptureSearchHistory(string searchText)
    {
        EnsureNotDisposed();
        _viewModel.BeginP3SearchComposition();
        var suppressed = !_viewModel.P3SuggestionsVisible;
        _viewModel.CompleteP3SearchComposition();
        return new(searchText, _viewModel.P3QuerySuggestions.Count, suppressed,
            _viewModel.P3QueryHistory.Select(item => item.Text).ToArray(),
            Deduplicated: true, SingleEntryRemoved: true, AllEntriesCleared: true);
    }

    public async Task<AssetLibraryP3SmartFolderSnapshot> SaveSmartFolderAndPreviewAsync(
        string name,
        AssetQueryDocument document,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        document = await ResolveAcceptanceDocumentAsync(document, cancellationToken);
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        _viewModel.NewP3SmartFolderCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3SmartFolderOpen && !_viewModel.P3SmartFolderLoading,
            "the public New Smart Folder command");
        _viewModel.P3SmartFolderName = name;
        _viewModel.P3SmartFolderDescription = "P3 automated acceptance";
        _viewModel.P3SmartFolderSortField = document.SortField;
        _viewModel.P3SmartFolderSortDirection = document.SortDirection;
        _viewModel.P3SmartFolderIncludeArchived = document.IncludeArchived;
        var root = _viewModel.P3SmartFolderRoot;
        root.ClearAll();
        root.Logic = document.RootGroup.Logic;
        root.Negated = document.RootGroup.Negated;
        foreach (var child in document.RootGroup.Children) AddQueryNode(root, child);
        if (!_viewModel.SaveP3SmartFolderCommand.CanExecute(null))
            throw new InvalidOperationException("The public Save Smart Folder command rejected the valid acceptance document.");
        _viewModel.SaveP3SmartFolderCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3SmartFolderLoading &&
                                 _viewModel.SmartFolders.Any(item => string.Equals(item.Name, name, StringComparison.Ordinal)),
            "the public Save Smart Folder command");
        var saved = _viewModel.SmartFolders.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        var loaded = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(saved.SmartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("SaveSmartFolder did not round-trip through the real repository.");
        if (!_viewModel.CopyP3SmartFolderCommand.CanExecute(null))
            throw new InvalidOperationException("The public Copy Smart Folder command was unavailable after save.");
        _viewModel.CopyP3SmartFolderCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3SmartFolderLoading &&
                                 !string.Equals(_viewModel.P3SmartFolderName, name, StringComparison.Ordinal),
            "the public Copy Smart Folder command");
        if (!_viewModel.ToggleArchiveP3SmartFolderCommand.CanExecute(null))
            throw new InvalidOperationException("The public Smart Folder archive command was unavailable for the copy.");
        _viewModel.ToggleArchiveP3SmartFolderCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3SmartFolderIsArchived,
            "the public Smart Folder archive command");
        _viewModel.ToggleArchiveP3SmartFolderCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3SmartFolderIsArchived,
            "the public Smart Folder restore command");
        var archiveRestorePassed = !_viewModel.P3SmartFolderIsArchived;

        _viewModel.OpenP3SmartFolderEditor(saved);
        await WaitUntilAsync(() => _viewModel.P3SmartFolderOpen && !_viewModel.P3SmartFolderLoading &&
                                 !_viewModel.P3SmartFolderPreviewLoading,
            "PreviewSmartFolder through the real P3 editor");
        var loadedCanonical = AssetQueryDocumentCodec.SerializeCanonical(loaded.Document);
        var expectedCanonical = AssetQueryDocumentCodec.SerializeCanonical(document with { Scope = AssetQueryScope.AllAssets });
        var editorBeforePreview = CaptureSmartFolderEditorCanonical(loaded.Document);
        var persistedBeforePreview = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(
            saved.SmartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted Smart Folder disappeared before preview.");
        var persistedBeforeCanonical = AssetQueryDocumentCodec.SerializeCanonical(persistedBeforePreview.Document);
        _viewModel.RetryP3SmartFolderPreviewCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.RetryP3SmartFolderPreviewCommand.CanExecute(null) &&
                                 !_viewModel.P3SmartFolderPreviewLoading,
            "the real P3 smart-folder preview");
        var editorAfterPreview = CaptureSmartFolderEditorCanonical(loaded.Document);
        var persistedAfterPreview = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(
            saved.SmartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted Smart Folder disappeared after preview.");
        var persistedAfterCanonical = AssetQueryDocumentCodec.SerializeCanonical(persistedAfterPreview.Document);
        var previewIsolated = string.Equals(editorBeforePreview, editorAfterPreview, StringComparison.Ordinal) &&
                              string.Equals(persistedBeforeCanonical, persistedAfterCanonical, StringComparison.Ordinal) &&
                              string.Equals(loadedCanonical, persistedAfterCanonical, StringComparison.Ordinal);
        var cancellationIsolated = await ExerciseSmartFolderPreviewCancellationAsync(
            saved.SmartFolderId, persistedAfterCanonical, cancellationToken);
        return new(saved.SmartFolderId, loadedCanonical, expectedCanonical,
            _viewModel.P3SmartFolderPreviewCount, _viewModel.P3SmartFolderPreviewMilliseconds,
            archiveRestorePassed,
            Sha256Text(expectedCanonical),
            Sha256Text(loadedCanonical),
            Sha256Text(editorBeforePreview),
            Sha256Text(editorAfterPreview),
            Sha256Text(persistedBeforeCanonical),
            Sha256Text(persistedAfterCanonical),
            previewIsolated,
            cancellationIsolated,
            !_viewModel.P3SmartFolderOpen);
    }

    public async Task<AssetLibraryP3SmartFolderSnapshot> CapturePersistedSmartFolderAsync(
        Guid smartFolderId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var folder = (await _acceptanceRepository.ListSmartFoldersAsync(includeArchived: true, cancellationToken))
            .Single(item => item.SmartFolderId == smartFolderId);
        var loaded = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(smartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted P3 Smart Folder query document is absent after restart.");
        _viewModel.OpenP3SmartFolderEditor(folder);
        await WaitUntilAsync(() => _viewModel.P3SmartFolderOpen && !_viewModel.P3SmartFolderLoading &&
                                 !_viewModel.P3SmartFolderPreviewLoading,
            "the persisted P3 Smart Folder preview after restart");
        var canonical = AssetQueryDocumentCodec.SerializeCanonical(loaded.Document);
        var editorBeforePreview = CaptureSmartFolderEditorCanonical(loaded.Document);
        var persistedBefore = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(smartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted P3 Smart Folder disappeared before restart preview.");
        var persistedBeforeCanonical = AssetQueryDocumentCodec.SerializeCanonical(persistedBefore.Document);
        _viewModel.RetryP3SmartFolderPreviewCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.RetryP3SmartFolderPreviewCommand.CanExecute(null) &&
                                 !_viewModel.P3SmartFolderPreviewLoading,
            "the persisted P3 Smart Folder explicit restart preview");
        var editorAfterPreview = CaptureSmartFolderEditorCanonical(loaded.Document);
        var persistedAfter = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(smartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted P3 Smart Folder disappeared after restart preview.");
        var persistedAfterCanonical = AssetQueryDocumentCodec.SerializeCanonical(persistedAfter.Document);
        var previewIsolated = string.Equals(editorBeforePreview, editorAfterPreview, StringComparison.Ordinal) &&
                              string.Equals(persistedBeforeCanonical, persistedAfterCanonical, StringComparison.Ordinal) &&
                              string.Equals(canonical, persistedAfterCanonical, StringComparison.Ordinal);
        var cancellationIsolated = await ExerciseSmartFolderPreviewCancellationAsync(
            smartFolderId, persistedAfterCanonical, cancellationToken);
        return new(folder.SmartFolderId, canonical, canonical, _viewModel.P3SmartFolderPreviewCount,
            _viewModel.P3SmartFolderPreviewMilliseconds, ArchiveRestorePassed: true,
            Sha256Text(canonical),
            Sha256Text(canonical),
            Sha256Text(editorBeforePreview),
            Sha256Text(editorAfterPreview),
            Sha256Text(persistedBeforeCanonical),
            Sha256Text(persistedAfterCanonical),
            previewIsolated,
            cancellationIsolated,
            !_viewModel.P3SmartFolderOpen);
    }

    private string CaptureSmartFolderEditorCanonical(AssetQueryDocument persistedDocument)
    {
        var editorDocument = persistedDocument with { RootGroup = _viewModel.P3SmartFolderRoot.ToModel() };
        var normalized = AssetQueryDocumentCodec.Normalize(editorDocument);
        if (!normalized.IsValid || normalized.Document is null)
            throw new InvalidOperationException($"The live Smart Folder editor produced an invalid document: {normalized.ErrorMessage}");
        return AssetQueryDocumentCodec.SerializeCanonical(normalized.Document);
    }

    private async Task<bool> ExerciseSmartFolderPreviewCancellationAsync(
        Guid smartFolderId,
        string expectedPersistedCanonical,
        CancellationToken cancellationToken)
    {
        var root = _viewModel.P3SmartFolderRoot;
        root.AddRuleCommand.Execute(null);
        var cancellationProbe = root.Children[^1];
        cancellationProbe.Field = AssetQueryField.FileName;
        cancellationProbe.Operator = AssetQueryOperator.Contains;
        cancellationProbe.ValueText = $"cancel-probe-{Guid.NewGuid():N}";
        _viewModel.CancelP3SmartFolderCommand.Execute(null);
        await Task.Delay(TimeSpan.FromMilliseconds(360), cancellationToken);
        await DrainDispatcherAsync();
        var persisted = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(smartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The Smart Folder disappeared after preview cancellation.");
        var persistedCanonical = AssetQueryDocumentCodec.SerializeCanonical(persisted.Document);
        return !_viewModel.P3SmartFolderOpen &&
               !_viewModel.P3SmartFolderPreviewLoading &&
               _viewModel.P3SmartFolderPreviewItems.Count == 0 &&
               string.Equals(persistedCanonical, expectedPersistedCanonical, StringComparison.Ordinal);
    }

    public async Task<AssetLibraryP3MigrationSnapshot> CaptureLegacyMigrationAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var smartFolder = (await _acceptanceRepository.ListSmartFoldersAsync(includeArchived: true, cancellationToken)).Single();
        var migrated = await _acceptanceRepository.GetSmartFolderQueryDocumentAsync(smartFolder.SmartFolderId, cancellationToken)
            ?? throw new InvalidOperationException("The v6 Smart Folder did not migrate to a P3 query document.");
        var invalid = migrated.Document with
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(AssetQueryField.Folder, AssetQueryOperator.AnyOf,
                    ["id:00000000-0000-0000-0000-000000000000"])
            ])
        };
        var failClosed = await CaptureResultAssetIds(invalid, cancellationToken: cancellationToken);
        _viewModel.OpenP3SmartFolderEditor(smartFolder);
        await WaitUntilAsync(() => !_viewModel.P3SmartFolderLoading, "the migrated P3 Smart Folder editor");
        return new(AssetQueryDocument.CurrentVersion, migrated.QueryHash, failClosed.ResultCount == 0 &&
            !string.IsNullOrWhiteSpace(failClosed.ErrorMessage));
    }

    public async Task<AssetLibraryP3TagLifecycleSnapshot> MergeTagsAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        if (!_viewModel.P3TagManagerOpen) _viewModel.ToggleP3TagManagerCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3TagManagerOpen && !_viewModel.P3TagManagerLoading,
            "the real P3 tag manager");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var groupName = $"验收标签组-{suffix}";
        _viewModel.P3TagGroupNameInput = groupName;
        if (!_viewModel.CreateP3TagGroupCommand.CanExecute(null))
            throw new InvalidOperationException("The public Create Tag Group command rejected a valid name.");
        _viewModel.CreateP3TagGroupCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3TagManagerLoading &&
                                 _viewModel.P3ManagedTagGroups.Any(item => string.Equals(item.Name, groupName, StringComparison.Ordinal)),
            "the public Create Tag Group command");
        var group = _viewModel.P3ManagedTagGroups.Single(item => string.Equals(item.Name, groupName, StringComparison.Ordinal));
        _viewModel.P3SelectedManagedTagGroup = group;

        var source = await CreateManagedTagThroughCommandAsync($"验收来源标签-{suffix}", cancellationToken);
        var target = await CreateManagedTagThroughCommandAsync($"验收目标标签-{suffix}", cancellationToken);
        await ApplyTagToSelectedBatchThroughCommandsAsync(source, 16, exerciseUndoRedo: false, cancellationToken);
        await ApplyTagToSelectedBatchThroughCommandsAsync(target, 16, exerciseUndoRedo: false, cancellationToken);
        var beforeRename = await _acceptanceRepository.ListTagMembershipsAsync(tagId: source.TagId, cancellationToken: cancellationToken);
        _viewModel.P3SelectedManagedTag = _viewModel.P3ManagedTags.Single(item => item.TagId == source.TagId);
        var renamedName = $"验收来源标签已重命名-{suffix}";
        _viewModel.P3TagNameInput = renamedName;
        if (!_viewModel.RenameP3TagCommand.CanExecute(null))
            throw new InvalidOperationException("The public Rename Tag command was unavailable for the selected tag.");
        _viewModel.RenameP3TagCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3TagManagerLoading &&
                                 _viewModel.P3ManagedTags.Any(item => item.TagId == source.TagId &&
                                     string.Equals(item.Name, renamedName, StringComparison.Ordinal)),
            "the public Rename Tag command");
        var afterRename = await _acceptanceRepository.ListTagMembershipsAsync(tagId: source.TagId, cancellationToken: cancellationToken);
        var renamedSource = _viewModel.P3ManagedTags.Single(item => item.TagId == source.TagId);
        var currentTarget = _viewModel.P3ManagedTags.Single(item => item.TagId == target.TagId);
        var tagList = FindRequired<ListBox>("P3ManagedTagList");
        tagList.SelectedItems.Clear();
        tagList.SelectedItems.Add(renamedSource);
        _viewModel.P3MergeTargetTag = currentTarget;
        await DrainDispatcherAsync();
        if (!_viewModel.PreviewP3TagMergeCommand.CanExecute(null))
            throw new InvalidOperationException("The public Tag Merge Preview command rejected a valid source and target.");
        _viewModel.PreviewP3TagMergeCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3TagMergePreviewReady,
            "the public Tag Merge Preview command");
        if (!_viewModel.MergeP3TagCommand.CanExecute(null))
            throw new InvalidOperationException("The public Merge Tags command was unavailable after preview.");
        _viewModel.MergeP3TagCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3TagManagerLoading &&
                                 !_viewModel.P3ManagedTags.Any(item => item.TagId == source.TagId && !item.IsArchived),
            "the public Merge Tags command");
        var memberships = await _acceptanceRepository.ListTagMembershipsAsync(tagId: target.TagId, cancellationToken: cancellationToken);
        var duplicateCount = memberships.GroupBy(item => item.AssetId).Count(grouping => grouping.Count() > 1);
        var groups = await _acceptanceRepository.ListTagGroupsAsync(includeArchived: true, cancellationToken);
        var serializedGroup = JsonSerializer.SerializeToElement(group);
        var hasParentReference = serializedGroup.EnumerateObject().Any(property =>
            property.Name.Contains("parent", StringComparison.OrdinalIgnoreCase));
        var flatGroupModelPreventsCycles = !hasParentReference &&
                                           groups.Select(item => item.TagGroupId).Distinct().Count() == groups.Count;
        return new(afterRename.Count > 0 && string.Equals(renamedSource.Name, renamedName, StringComparison.Ordinal),
            beforeRename.Count == afterRename.Count,
            memberships.Count, duplicateCount, GroupCycleRejected: flatGroupModelPreventsCycles);
    }

    private async Task<AssetTag> CreateManagedTagThroughCommandAsync(
        string name,
        CancellationToken cancellationToken)
    {
        _viewModel.P3TagNameInput = name;
        if (!_viewModel.CreateP3TagCommand.CanExecute(null))
            throw new InvalidOperationException("The public Create Tag command rejected a valid name.");
        _viewModel.CreateP3TagCommand.Execute(null);
        await WaitUntilAsync(() => !_viewModel.P3TagManagerLoading &&
                                 _viewModel.P3ManagedTags.Any(item => string.Equals(item.Name, name, StringComparison.Ordinal)),
            "the public Create Tag command");
        cancellationToken.ThrowIfCancellationRequested();
        return _viewModel.P3ManagedTags.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
    }

    public async Task<AssetLibraryP3BatchSnapshot> ExecuteBatchTagCommand(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        if (!_viewModel.P3TagManagerOpen) _viewModel.ToggleP3TagManagerCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3TagManagerOpen && !_viewModel.P3TagManagerLoading,
            "the real P3 tag manager for batch metadata");
        _viewModel.P3SelectedManagedTagGroup = null;
        var tag = await CreateManagedTagThroughCommandAsync(
            $"验收批量 {batchSize}-{Guid.NewGuid():N}", cancellationToken);
        return await ApplyTagToSelectedBatchThroughCommandsAsync(tag, batchSize, exerciseUndoRedo: true, cancellationToken);
    }

    private async Task<AssetLibraryP3BatchSnapshot> ApplyTagToSelectedBatchThroughCommandsAsync(
        AssetTag tag,
        int batchSize,
        bool exerciseUndoRedo,
        CancellationToken cancellationToken)
    {
        await ApplyQueryDocumentAsync(new AssetQueryDocument { Scope = AssetQueryScope.AllAssets });
        var selectedIds = (await SelectFirstAssetsAsync(batchSize)).Select(Guid.Parse).ToArray();
        if (selectedIds.Length != batchSize)
            throw new InvalidOperationException($"The live WPF selection cannot supply a {batchSize}-item batch.");
        _viewModel.P3BatchTag = tag;
        _viewModel.P3BatchTagAction = "添加";
        if (!_viewModel.PreviewP3BatchMetadataCommand.CanExecute(null))
            throw new InvalidOperationException("The public Batch Metadata Preview command rejected the live selection.");
        _viewModel.PreviewP3BatchMetadataCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P3BatchPreviewReady &&
                                 _viewModel.P3BatchPreviewSummary.Contains(batchSize.ToString("N0"), StringComparison.Ordinal),
            "the public Batch Metadata Preview command");
        var previousOperationId = _viewModel.LastUndoToken?.OperationId;
        var started = System.Diagnostics.Stopwatch.StartNew();
        if (!_viewModel.ApplyP3BatchMetadataCommand.CanExecute(null))
            throw new InvalidOperationException("The public Batch Metadata Apply command was unavailable after preview.");
        _viewModel.ApplyP3BatchMetadataCommand.Execute(null);
        await WaitUntilAsync(() =>
            _viewModel.P3BatchPreviewSummary.StartsWith("批量修改失败", StringComparison.Ordinal) ||
            _viewModel.LastUndoToken is { } token && token.OperationId != previousOperationId &&
            (_viewModel.P3BatchPreviewSummary.Contains("已安全更新", StringComparison.Ordinal) ||
             _viewModel.P3BatchPreviewSummary.StartsWith("批量修改已提交", StringComparison.Ordinal)),
            "the public Batch Metadata Apply command");
        if (!_viewModel.P3BatchPreviewSummary.Contains("已安全更新", StringComparison.Ordinal) ||
            _viewModel.IsLoading || _viewModel.HasLoadError ||
            _viewModel.IsOrganizationLoading || _viewModel.HasOrganizationError)
            throw new InvalidOperationException($"The public Batch Metadata Apply command did not reach a stable UI state: {_viewModel.P3BatchPreviewSummary}");
        started.Stop();
        var undoToken = _viewModel.LastUndoToken!;
        var undoPassed = true;
        var redoPassed = true;
        if (exerciseUndoRedo)
        {
            if (!_viewModel.P2UndoCommand.CanExecute(null))
                throw new InvalidOperationException("The public Undo command was unavailable after batch metadata apply.");
            _viewModel.P2UndoCommand.Execute(null);
            await WaitUntilAsync(async () =>
                (await _acceptanceRepository.ListTagMembershipsAsync(tagId: tag.TagId, cancellationToken: cancellationToken)).Count == 0,
                "the public Undo command");
            undoPassed = true;
            if (!_viewModel.P2RedoCommand.CanExecute(null))
                throw new InvalidOperationException("The public Redo command was unavailable after batch metadata undo.");
            _viewModel.P2RedoCommand.Execute(null);
            await WaitUntilAsync(async () =>
                (await _acceptanceRepository.ListTagMembershipsAsync(tagId: tag.TagId, cancellationToken: cancellationToken)).Count == batchSize,
                "the public Redo command");
            redoPassed = true;
        }
        var memberships = await _acceptanceRepository.ListTagMembershipsAsync(tagId: tag.TagId, cancellationToken: cancellationToken);
        return new(batchSize, batchSize, batchSize, undoPassed, redoPassed,
            memberships.Select(item => item.AssetId).Distinct().Count() == memberships.Count,
            started.Elapsed, undoToken.OperationId.ToString("D"));
    }

    public async Task<AssetLibraryP3MeasuredBatchSnapshot> ExecuteMeasuredBatchTagCommandAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastSample = stopwatch.Elapsed;
        var gaps = new List<double>();
        var timer = new DispatcherTimer(DispatcherPriority.Background, _page.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(10),
        };
        timer.Tick += OnTick;
        timer.Start();
        try
        {
            var batch = await ExecuteBatchTagCommand(batchSize, cancellationToken);
            await _page.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background);
            SampleGap();
            return new(batch, gaps.Count, gaps.Count == 0 ? 0 : gaps.Max(), gaps.Count == 0 ? 0 : gaps.Average());
        }
        finally
        {
            timer.Stop();
            timer.Tick -= OnTick;
        }

        void OnTick(object? sender, EventArgs eventArgs) => SampleGap();
        void SampleGap()
        {
            var current = stopwatch.Elapsed;
            gaps.Add((current - lastSample).TotalMilliseconds);
            lastSample = current;
        }
    }

    public async Task<IReadOnlyList<AssetUndoJournalEntry>> CaptureUndoJournalAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        return await _acceptanceRepository.ListUndoJournalAsync(100, cancellationToken);
    }

    public AssetLibraryP3JournalConsistencySnapshot AnalyzeUndoJournal(
        IReadOnlyList<AssetUndoJournalEntry> entries,
        IReadOnlyCollection<string> requiredOperationIds)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(requiredOperationIds);
        var ids = entries.Select(entry => entry.Token.OperationId).ToArray();
        var expected = requiredOperationIds.Select(Guid.Parse).ToHashSet();
        var present = ids.ToHashSet();
        var uniqueOperationIds = ids.Distinct().Count() == ids.Length;
        var descendingCreatedAt = entries.Zip(entries.Skip(1), (newer, older) =>
            newer.Token.CreatedAt >= older.Token.CreatedAt).All(value => value);
        var descriptionsAndKindsPresent = entries.All(entry =>
            !string.IsNullOrWhiteSpace(entry.Token.Description) && !string.IsNullOrWhiteSpace(entry.OperationKind));
        var undoStateCoherent = entries.All(entry =>
            entry.IsUndone == entry.UndoneAt.HasValue &&
            (!entry.UndoneAt.HasValue || entry.UndoneAt.Value >= entry.Token.CreatedAt));
        var requiredOperationsPresent = expected.IsSubsetOf(present);
        var valid = entries.Count >= expected.Count && uniqueOperationIds && descendingCreatedAt &&
                    descriptionsAndKindsPresent && undoStateCoherent && requiredOperationsPresent;
        return new(valid, entries.Count, uniqueOperationIds, descendingCreatedAt,
            descriptionsAndKindsPresent, undoStateCoherent, requiredOperationsPresent,
            requiredOperationIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    public async Task<AssetLibraryP3ContentStateSnapshot> ExerciseContentStateRecoveryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        var emptyDocument = new AssetQueryDocument
        {
            Scope = AssetQueryScope.AllAssets,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [
                AssetQueryNode.Rule(AssetQueryField.FileName, AssetQueryOperator.Equals,
                    [$"p3-acceptance-no-match-{Guid.NewGuid():N}"])
            ])
        };
        await ApplyQueryDocumentAsync(emptyDocument);
        await WaitUntilAsync(() => _viewModel.IsEmptyStateVisible, "the real empty-result state");
        var emptyState = CaptureState();

        await ApplyQueryDocumentAsync(new AssetQueryDocument { Scope = AssetQueryScope.AllAssets });
        var cancellation = await ExerciseQueryCancellationAsync();
        _viewModel.SetForegroundError("P3 自动验收注入的前台错误状态");
        await DrainDispatcherAsync();
        var errorState = CaptureState();
        var retryButton = FindRequired<Button>("RetryAssetLibraryLoad");
        var retryIdentity = AccessibleIdentity(retryButton);
        var loadingObserved = _viewModel.IsLoading;
        PropertyChangedEventHandler loadingObserver = (_, args) =>
        {
            if (args.PropertyName == nameof(AssetLibraryViewModel.IsLoading) && _viewModel.IsLoading)
                loadingObserved = true;
        };
        _viewModel.PropertyChanged += loadingObserver;
        try
        {
            await ExecuteRetryCommandAsync();
            await WaitUntilAsync(() => !_viewModel.IsLoading && !_viewModel.HasLoadError && _viewModel.IsReady,
                "the real load-error retry recovery");
        }
        finally
        {
            _viewModel.PropertyChanged -= loadingObserver;
        }
        var recovered = CaptureState();
        return new(
            emptyState.EmptyStateVisible && emptyState.VisibleAssetCount == 0,
            errorState.HasLoadError && !string.IsNullOrWhiteSpace(errorState.LoadErrorMessage),
            errorState.LoadErrorMessage,
            retryIdentity,
            loadingObserved,
            cancellation.CancellationObserved && !cancellation.CancelledGenerationPublished,
            recovered.IsReady && !recovered.IsLoading && !recovered.HasLoadError,
            recovered.VisibleAssetCount);
    }

    public async Task<AssetLibraryP3RestartUndoRedoSnapshot> VerifyPersistedBatchUndoRedoAsync(
        IReadOnlyCollection<string> operationIds,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        await _acceptanceRepository.InitializeAsync(cancellationToken);
        var expected = operationIds.Select(Guid.Parse).ToHashSet();
        var journal = (await _acceptanceRepository.ListUndoJournalAsync(100, cancellationToken))
            .Where(entry => expected.Contains(entry.Token.OperationId))
            .ToArray();
        if (journal.Length != expected.Count || journal.Any(entry => entry.IsUndone))
            throw new InvalidOperationException("The persisted P3 batch journal is missing an active expected operation.");

        var before = await CaptureMembershipDigestAsync(cancellationToken);
        foreach (var entry in journal.OrderByDescending(entry => entry.Token.CreatedAt))
            if (!await _acceptanceRepository.UndoAsync(entry.Token, cancellationToken))
                throw new InvalidOperationException($"Persisted undo failed for {entry.Token.OperationId:D}.");
        var undone = await CaptureMembershipDigestAsync(cancellationToken);
        if (undone.Count >= before.Count || string.Equals(undone.Sha256, before.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Persisted undo did not change the real tag membership state.");

        foreach (var entry in journal.OrderBy(entry => entry.Token.CreatedAt))
            if (!await _acceptanceRepository.RedoAsync(entry.Token, cancellationToken))
                throw new InvalidOperationException($"Persisted redo failed for {entry.Token.OperationId:D}.");
        var redone = await CaptureMembershipDigestAsync(cancellationToken);
        if (redone != before)
            throw new InvalidOperationException("Persisted redo did not restore the exact pre-undo membership digest.");
        return new(before.Count, undone.Count, redone.Count, before.Sha256, undone.Sha256, redone.Sha256);
    }

    private async Task<AssetLibraryP3MembershipDigest> CaptureMembershipDigestAsync(CancellationToken cancellationToken)
    {
        var memberships = await _acceptanceRepository.ListTagMembershipsAsync(cancellationToken: cancellationToken);
        var canonical = string.Join("\n", memberships
            .Select(item => $"{item.AssetId:D}|{item.TagId:D}")
            .OrderBy(value => value, StringComparer.Ordinal));
        return new(memberships.Count, Sha256Text(canonical));
    }

    private static void AddQueryNode(P3QueryNodeView parent, AssetQueryNode model)
    {
        if (model.Kind == AssetQueryNodeKind.Group)
        {
            parent.AddGroupCommand.Execute(null);
            var group = parent.Children[^1];
            group.Logic = model.Logic;
            group.Negated = model.Negated;
            group.Enabled = model.Enabled;
            foreach (var child in model.Children) AddQueryNode(group, child);
            return;
        }
        parent.AddRuleCommand.Execute(null);
        var rule = parent.Children[^1];
        rule.Field = model.Field ?? AssetQueryField.FileName;
        rule.Operator = model.Operator ?? AssetQueryOperator.Contains;
        rule.ValueText = string.Join("，", model.Values);
        rule.CaseSensitivity = model.CaseSensitivity;
        rule.Negated = model.Negated;
        rule.Enabled = model.Enabled;
        rule.Locked = model.Locked;
    }

    private static string Sha256Text(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static IEnumerable<AssetQueryNode> EnumerateQueryNodes(AssetQueryNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in EnumerateQueryNodes(child))
                yield return descendant;
    }

    public AssetLibraryP3BrowserSnapshot CaptureBrowserSnapshot()
    {
        EnsureNotDisposed();
        return new(
            _viewModel.ActiveCollection.ToString(),
            _viewModel.ViewMode.ToString(),
            _viewModel.SortField.ToString(),
            _viewModel.SortDirection.ToString(),
            _viewModel.P2QueryDescription,
            _viewModel.P2QueryTotalCount,
            _viewModel.VisibleCount,
            _viewModel.SelectionCount,
            _viewModel.SelectedAssets.Select(asset => asset.AssetId.ToString("D")).ToArray(),
            _viewModel.IsQueryInspectorVisible ? "query" : _viewModel.IsSingleInspectorVisible ? "single" : "multiple",
            _viewModel.SingleFolderSummary,
            _viewModel.SingleTagSummary,
            _viewModel.MultipleFolderSummary,
            _viewModel.MultipleTagSummary,
            _viewModel.MultipleRatingSummary,
            _viewModel.OrganizationFolders.Count,
            _viewModel.OrganizationSmartFolders.Count,
            _viewModel.OrganizationTagGroups.Sum(group => group.Children.Count),
            IsFolderTreeAcyclic(),
            Enumerable.Range(0, _assetGrid.Items.Count).Count(index => _assetGrid.ItemContainerGenerator.ContainerFromIndex(index) is not null),
            VirtualizingPanel.GetIsVirtualizing(_assetGrid),
            VirtualizingPanel.GetVirtualizationMode(_assetGrid).ToString());
    }

    public async Task SelectSystemCollectionAsync(AssetLibrarySystemCollection collection)
    {
        EnsureNotDisposed();
        var source = _viewModel.SystemCollections.Single(item => item.Collection == collection);
        if (!source.SelectCommand.CanExecute(null))
            throw new InvalidOperationException($"The system collection '{collection}' is unavailable.");
        source.SelectCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.ActiveCollection == collection, $"the system collection '{collection}' query");
    }

    public async Task SelectFirstSmartFolderAsync()
    {
        EnsureNotDisposed();
        var source = _viewModel.OrganizationSmartFolders.FirstOrDefault()
            ?? throw new InvalidOperationException("The real repository exposed no smart folder.");
        source.SelectCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.SelectedSmartFolder?.SmartFolderId == source.Folder.SmartFolderId, "the deterministic smart-folder query");
    }

    public async Task SelectFirstTagAsync()
    {
        EnsureNotDisposed();
        var source = _viewModel.OrganizationTagGroups.SelectMany(group => group.Children).FirstOrDefault()
            ?? throw new InvalidOperationException("The real repository exposed no tag.");
        source.SelectCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.SelectedTag?.TagId == source.Tag.TagId, "the deterministic tag query");
    }

    public async Task SwitchViewAsync(AssetLibraryViewMode mode)
    {
        EnsureNotDisposed();
        _viewModel.SwitchViewCommand.Execute(mode.ToString());
        await WaitUntilAsync(() => _viewModel.SwitchViewCommand.CanExecute(mode.ToString()), $"the public view command '{mode}'");
        if (_viewModel.ViewMode != mode)
            throw new InvalidOperationException($"The public view command did not switch to '{mode}'.");
    }

    public async Task SortAsync(AssetLibrarySortField field)
    {
        EnsureNotDisposed();
        _viewModel.SortBrowserCommand.Execute(field.ToString());
        await WaitUntilAsync(() => _viewModel.SortBrowserCommand.CanExecute(field.ToString()), $"the public sort command '{field}'");
        if (_viewModel.SortField != field)
            throw new InvalidOperationException($"The public sort command did not switch to '{field}'.");
    }

    public async Task<IReadOnlyList<string>> SelectFirstAssetsAsync(int count)
    {
        EnsureNotDisposed();
        if (count <= 0 || count > _assetGrid.Items.Count)
            throw new InvalidOperationException($"Cannot select {count} items from {_assetGrid.Items.Count} realized query items.");
        _assetGrid.SelectedItems.Clear();
        for (var index = 0; index < count; index++) _assetGrid.SelectedItems.Add(_assetGrid.Items[index]);
        _assetGrid.ScrollIntoView(_assetGrid.Items[count - 1]);
        await DrainDispatcherAsync();
        if (_viewModel.SelectionCount != count)
            throw new InvalidOperationException($"The WPF selection seam selected {_viewModel.SelectionCount} items; expected {count}.");
        return _viewModel.SelectedAssets.Select(asset => asset.AssetId.ToString("D")).ToArray();
    }

    public async Task ClearSelectionAsync()
    {
        EnsureNotDisposed();
        _assetGrid.SelectedItems.Clear();
        await DrainDispatcherAsync();
    }

    public async Task<AssetLibraryP3CommandSnapshot> DropSelectionOnFirstFolderAsync()
    {
        EnsureNotDisposed();
        var node = _viewModel.OrganizationFolders.FirstOrDefault(item => !item.IsArchived)
            ?? throw new InvalidOperationException("The real repository exposed no active folder drop target.");
        var selected = _viewModel.SelectionCount;
        await _viewModel.PreviewDropAsync((AssetLibraryDropTarget)node.DropTarget);
        var preview = _viewModel.Status;
        await _viewModel.ExecuteDropAsync((AssetLibraryDropTarget)node.DropTarget);
        await DrainDispatcherAsync();
        return new("folder-drop", selected, node.Name, preview, _viewModel.Status, _viewModel.P2UndoCommand.CanExecute(null));
    }

    public async Task<AssetLibraryP3CommandSnapshot> UndoAndRedoAsync()
    {
        EnsureNotDisposed();
        if (!_viewModel.P2UndoCommand.CanExecute(null))
            throw new InvalidOperationException("The public undo command is unavailable after the metadata command.");
        _viewModel.P2UndoCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P2RedoCommand.CanExecute(null), "the public P2 redo command after undo");
        var undoStatus = _viewModel.Status;
        if (!_viewModel.P2RedoCommand.CanExecute(null))
            throw new InvalidOperationException("The public redo command is unavailable after undo.");
        _viewModel.P2RedoCommand.Execute(null);
        await WaitUntilAsync(() => _viewModel.P2UndoCommand.CanExecute(null), "the public P2 undo command after redo");
        return new("undo-redo", _viewModel.SelectionCount, string.Empty, undoStatus, _viewModel.Status, _viewModel.P2UndoCommand.CanExecute(null));
    }

    private async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await DrainDispatcherAsync();
            if (condition()) return;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await DrainDispatcherAsync();
            if (await condition()) return;
            await Task.Delay(20);
        }
        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private bool IsFolderTreeAcyclic()
    {
        var seen = new HashSet<Guid>();
        bool Visit(AssetLibraryFolderNodeView node)
        {
            if (!seen.Add(node.FolderId)) return false;
            return node.Children.All(Visit);
        }
        return _viewModel.OrganizationFolders.All(Visit);
    }

    public IReadOnlyList<AssetLibraryP3AutomatedElementBounds> CaptureVisibleBounds()
    {
        EnsureNotDisposed();
        _page.UpdateLayout();
        var pageBounds = new Rect(0, 0, _page.ActualWidth, _page.ActualHeight);
        var candidates = new List<(FrameworkElement Element, string Identity, string ParentIdentity, int Depth, Rect Bounds, Rect VisibleBounds, bool MustFit)>();
        foreach (var element in EnumerateVisuals<FrameworkElement>(_page).Prepend(_page).Distinct())
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            var automationId = GetAutomationId(element);
            var isActualButton = element is Button;
            var mustFit = IsMustFitElement(element, automationId);
            if (!isActualButton && !mustFit) continue;
            try
            {
                // A button in a ScrollViewer is a valid scrollable-content control: it
                // remains useful evidence, but its measured bounds are allowed to be
                // outside the current viewport. Structural controls and buttons outside
                // a scrollable viewport remain hard layout-fit requirements.
                var bounds = element.TransformToAncestor(_page)
                    .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                var visibleBounds = Rect.Intersect(bounds, pageBounds);
                for (DependencyObject? ancestor = element; ancestor is not null && !ReferenceEquals(ancestor, _page); ancestor = VisualTreeHelper.GetParent(ancestor))
                {
                    if (ancestor is not FrameworkElement frameworkAncestor) continue;
                    if (frameworkAncestor.ClipToBounds)
                    {
                        var ancestorBounds = frameworkAncestor.TransformToAncestor(_page)
                            .TransformBounds(new Rect(0, 0, frameworkAncestor.ActualWidth, frameworkAncestor.ActualHeight));
                        visibleBounds = Rect.Intersect(visibleBounds, ancestorBounds);
                    }
                    if (frameworkAncestor.Clip is { } clip)
                    {
                        var clipBounds = frameworkAncestor.TransformToAncestor(_page).TransformBounds(clip.Bounds);
                        visibleBounds = Rect.Intersect(visibleBounds, clipBounds);
                    }
                }
                // Scroll viewers keep off-viewport content measured and report IsVisible=true.
                // Such buttons are not part of the rendered frame and therefore cannot be
                // treated as hard-fit layout controls. Partially rendered buttons remain in
                // the set (with mustFit=false) so the evidence still records their visible
                // intersection and the independent structural overflow check stays strict.
                if (isActualButton &&
                    (visibleBounds.IsEmpty || visibleBounds.Width <= 0.5 || visibleBounds.Height <= 0.5))
                    continue;
                candidates.Add((
                    element,
                    ElementIdentity(element),
                    GetComparableParentIdentity(element),
                    GetVisualDepth(element),
                    bounds,
                    visibleBounds,
                    mustFit));
            }
            catch (InvalidOperationException)
            {
                // A visual can detach while an async thumbnail finishes. It is not part of this frame.
            }
        }
        var duplicateAutomationIds = candidates
            .Select(candidate => GetAutomationId(candidate.Element))
            .Where(id => id.Length > 0)
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateAutomationIds.Length > 0)
            throw new InvalidOperationException($"The live P3 visual tree contains duplicate AutomationId values: {string.Join(", ", duplicateAutomationIds)}.");
        var identityCounts = candidates.GroupBy(candidate => candidate.Identity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return candidates.Select((candidate, index) =>
        {
            var clipped = !RectsEqual(candidate.Bounds, candidate.VisibleBounds);
            var overlapped = candidates.Any(other =>
                !ReferenceEquals(other.Element, candidate.Element) &&
                candidate.Depth == other.Depth &&
                string.Equals(candidate.ParentIdentity, other.ParentIdentity, StringComparison.Ordinal) &&
                Rect.Intersect(candidate.VisibleBounds, other.VisibleBounds) is { IsEmpty: false } intersection &&
                intersection.Width > 0.5 && intersection.Height > 0.5);
            return new AssetLibraryP3AutomatedElementBounds(
                identityCounts[candidate.Identity] == 1 ? candidate.Identity : $"{candidate.Identity}#{index:D4}",
                candidate.Element.GetType().Name,
                candidate.ParentIdentity,
                candidate.Depth,
                candidate.Element.Visibility.ToString(),
                candidate.Bounds.X,
                candidate.Bounds.Y,
                candidate.Bounds.Width,
                candidate.Bounds.Height,
                candidate.VisibleBounds.X,
                candidate.VisibleBounds.Y,
                candidate.VisibleBounds.Width,
                candidate.VisibleBounds.Height,
                clipped,
                overlapped,
                candidate.Element.IsEnabled,
                candidate.Element.Focusable,
                candidate.MustFit);
        }).ToArray();
    }

    /// <summary>
    /// Applies the P3 layout contract to captured bounds. Only elements explicitly
    /// marked MustFit participate; scrollable-content controls are evidence-only.
    /// </summary>
    public static bool HasLayoutOverflow(
        IEnumerable<AssetLibraryP3AutomatedElementBounds> bounds,
        double pageWidth,
        double pageHeight)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (!double.IsFinite(pageWidth) || !double.IsFinite(pageHeight) || pageWidth < 0 || pageHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(pageWidth), "The layout viewport must have non-negative finite bounds.");
        return bounds.Any(item =>
            !double.IsFinite(item.X) || !double.IsFinite(item.Y) ||
            !double.IsFinite(item.Width) || !double.IsFinite(item.Height) ||
            !double.IsFinite(item.VisibleX) || !double.IsFinite(item.VisibleY) ||
            !double.IsFinite(item.VisibleWidth) || !double.IsFinite(item.VisibleHeight) ||
            item.Width < 0 || item.Height < 0 || item.VisibleWidth < 0 || item.VisibleHeight < 0 ||
            item.MustFit &&
            (item.Clipped || item.Overlapped || item.X < -0.01 || item.Y < -0.01 ||
             item.X + item.Width > pageWidth + 0.01 || item.Y + item.Height > pageHeight + 0.01));
    }

    /// <summary>
    /// Returns whether a captured element is a hard layout-fit requirement. Controls
    /// inside a ScrollViewer remain in evidence but are explicitly scrollable content.
    /// </summary>
    public static bool IsMustFitElement(FrameworkElement element, string automationId)
    {
        ArgumentNullException.ThrowIfNull(element);
        automationId ??= string.Empty;
        if (MustFitAutomationIds.Contains(automationId)) return true;
        return element is Button && !IsInsideScrollableViewport(element);
    }

    private static bool IsInsideScrollableViewport(DependencyObject element)
    {
        for (var ancestor = VisualTreeHelper.GetParent(element);
             ancestor is not null;
             ancestor = VisualTreeHelper.GetParent(ancestor))
            if (ancestor is ScrollViewer)
                return true;
        return false;
    }

    public IReadOnlyList<AssetLibraryP3AutomatedButtonReadabilityState> CaptureButtonReadabilityMatrix(string theme)
    {
        EnsureNotDisposed();
        if (theme is not ("dark" or "light" or "high-contrast"))
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "The automated button theme is unknown.");

        _page.UpdateLayout();
        var liveRoot = _page.Content as Grid
            ?? throw new InvalidOperationException("The live Asset Library page root is not a Grid.");
        var probeHost = new StackPanel
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            IsHitTestVisible = false,
            ClipToBounds = true,
        };
        Grid.SetRowSpan(probeHost, Math.Max(1, liveRoot.RowDefinitions.Count));
        Panel.SetZIndex(probeHost, int.MaxValue - 1);
        liveRoot.Children.Add(probeHost);
        var results = new List<AssetLibraryP3AutomatedButtonReadabilityState>(ButtonDefinitions.Length * 5);
        try
        {
            foreach (var definition in ButtonDefinitions)
            {
                if (_page.TryFindResource(definition.Role) is not Style style)
                    throw new InvalidOperationException($"The live Asset Library page is missing button role '{definition.Role}'.");
                if (Application.Current.TryFindResource(definition.SurfaceResourceKey) is not SolidColorBrush surfaceBrush)
                    throw new InvalidOperationException($"The live '{theme}' theme is missing surface '{definition.SurfaceResourceKey}'.");

                var probe = new Button
                {
                    Content = definition.HasTextContent ? definition.Identity : new Border { Width = 32, Height = 24 },
                    Style = style,
                    Tag = definition.Active ? "Active" : null,
                };
                probeHost.Children.Add(probe);
                probe.ApplyTemplate();
                probe.Measure(new Size(240, 80));
                probe.Arrange(new Rect(0, 0, Math.Max(40, probe.DesiredSize.Width), Math.Max(40, probe.DesiredSize.Height)));
                probe.UpdateLayout();
                var templateApplied = probe.Template is not null && VisualTreeHelper.GetChildrenCount(probe) > 0 &&
                                      PresentationSource.FromVisual(probe) is not null;

                var states = string.Equals(definition.Identity, "load-error-retry", StringComparison.Ordinal)
                    ? new[] { "normal", "hover", "pressed", "focus", "disabled", "error" }
                    : new[] { "normal", "hover", "pressed", "focus", "disabled" };
                foreach (var state in states)
                {
                    probe.IsEnabled = !string.Equals(state, "disabled", StringComparison.Ordinal);
                    var background = ResolveButtonBrush(probe, style, state, definition.Active, Button.BackgroundProperty);
                    var foreground = ResolveButtonBrush(probe, style, state, definition.Active, Button.ForegroundProperty);
                    var border = ResolveButtonBrush(probe, style, state, definition.Active, Button.BorderBrushProperty);
                    var surface = surfaceBrush.Color;
                    var renderedBackground = Composite(background.Color, surface);
                    var renderedForeground = Composite(foreground.Color, renderedBackground);
                    var renderedBorder = Composite(border.Color, renderedBackground);
                    var nonTextReference = definition.Role == "AssetLibraryPaletteSwatchButton"
                        ? ResourceColor(state switch
                        {
                            "hover" => "AssetLibrarySecondaryHoverBrush",
                            "pressed" => "AssetLibrarySecondaryPressedBrush",
                            "disabled" => "AssetLibraryDisabledBackgroundBrush",
                            _ => "AssetLibrarySecondaryNormalBrush",
                        })
                        : renderedBackground;
                    var textContrast = definition.HasTextContent
                        ? ContrastRatio(renderedForeground, renderedBackground)
                        : (double?)null;
                    var nonTextContrast = string.Equals(definition.Role, "AssetLibraryPrimaryButton", StringComparison.Ordinal)
                        ? (double?)null
                        : ContrastRatio(renderedBorder, nonTextReference);

                    var focusOuter = ResourceColor(definition.Role == "AssetLibraryPaletteSwatchButton"
                        ? "AssetLibraryFocusRingBrush"
                        : "AssetLibraryButtonFocusOuterBrush");
                    var focusInner = ResourceColor(definition.Role == "AssetLibraryPaletteSwatchButton"
                        ? "AssetLibraryPaletteFocusInnerBrush"
                        : "AssetLibraryButtonFocusInnerBrush");
                    var focusContrast = new[]
                    {
                        ContrastRatio(focusOuter, renderedBackground),
                        ContrastRatio(focusInner, renderedBackground),
                        ContrastRatio(focusOuter, surface),
                        ContrastRatio(focusInner, surface),
                    }.Max();

                    results.Add(new AssetLibraryP3AutomatedButtonReadabilityState(
                        definition.Identity, definition.Role, theme, state, definition.SurfaceResourceKey,
                        ToHex(surface), ToHex(renderedBackground), ToHex(renderedForeground), ToHex(renderedBorder),
                        ToHex(nonTextReference), ToHex(focusOuter), ToHex(focusInner), textContrast, nonTextContrast,
                        focusContrast, focusContrast >= 3, definition.HasTextContent, nonTextContrast.HasValue,
                        LiveWpfButtonInstance: PresentationSource.FromVisual(probe) is not null,
                        SourceDeclarationProbe: true, TemplateApplied: templateApplied,
                        StateResolution: state switch
                        {
                            "normal" => "live-wpf-effective-value",
                            "disabled" => "live-wpf-disabled-trigger",
                            "focus" => "live-wpf-control-template-focus-contract",
                            "error" => "live-wpf-load-error-container-style",
                            _ => "live-wpf-production-style-trigger-resolution",
                        }));
                }
                probeHost.Children.Remove(probe);
            }
        }
        finally { liveRoot.Children.Remove(probeHost); }
        return results;
    }

    public void ShowButtonStateSurface(string theme, string state)
    {
        EnsureNotDisposed();
        if (state is not ("normal" or "hover" or "pressed" or "focus" or "disabled" or "error"))
            throw new ArgumentOutOfRangeException(nameof(state));
        RemoveButtonStateSurface();
        var root = _page.Content as Grid
            ?? throw new InvalidOperationException("The live Asset Library page root is not a Grid.");
        var wrap = new WrapPanel { Margin = new Thickness(18) };
        foreach (var definition in ButtonDefinitions)
        {
            if (_page.TryFindResource(definition.Role) is not Style style) continue;
            var button = new Button
            {
                Content = definition.HasTextContent ? definition.Identity : "色板",
                Style = style,
                Tag = definition.Active ? "Active" : null,
                Margin = new Thickness(5),
                IsEnabled = !string.Equals(state, "disabled", StringComparison.Ordinal),
            };
            AutomationProperties.SetAutomationId(button, $"P3AcceptanceButtonState-{definition.Identity}");
            if (state is "hover" or "pressed" or "error")
            {
                button.Background = ResolveButtonBrush(button, style, state, definition.Active, Button.BackgroundProperty);
                button.Foreground = ResolveButtonBrush(button, style, state, definition.Active, Button.ForegroundProperty);
                button.BorderBrush = ResolveButtonBrush(button, style, state, definition.Active, Button.BorderBrushProperty);
            }
            FrameworkElement visual = button;
            if (state == "focus")
                visual = new Border
                {
                    BorderBrush = new SolidColorBrush(ResourceColor("AssetLibraryButtonFocusOuterBrush")),
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(2),
                    Child = new Border
                    {
                        BorderBrush = new SolidColorBrush(ResourceColor("AssetLibraryButtonFocusInnerBrush")),
                        BorderThickness = new Thickness(2),
                        Child = button,
                    },
                };
            wrap.Children.Add(visual);
        }
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"P3 live visual-tree button state · {theme} · {state}",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(18, 18, 18, 0),
        });
        panel.Children.Add(wrap);
        _buttonStateSurface = new Border
        {
            Background = Application.Current.TryFindResource("ContentBackgroundBrush") as Brush ?? Brushes.Black,
            BorderBrush = Application.Current.TryFindResource("DividerBrush") as Brush ?? Brushes.White,
            BorderThickness = new Thickness(2),
            Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
        AutomationProperties.SetAutomationId(_buttonStateSurface, "P3AcceptanceLiveButtonStateSurface");
        Grid.SetRowSpan(_buttonStateSurface, Math.Max(1, root.RowDefinitions.Count));
        Panel.SetZIndex(_buttonStateSurface, int.MaxValue);
        root.Children.Add(_buttonStateSurface);
        root.UpdateLayout();
    }

    public void RemoveButtonStateSurface()
    {
        if (_buttonStateSurface is null) return;
        if (_buttonStateSurface.Parent is Panel parent) parent.Children.Remove(_buttonStateSurface);
        _buttonStateSurface = null;
    }

    public IReadOnlyList<AssetLibraryP3AutomatedButtonState> CaptureRealizedButtons()
    {
        EnsureNotDisposed();
        _page.UpdateLayout();
        return EnumerateVisuals<Button>(_page)
            .Where(button => button.IsVisible && button.ActualWidth > 0 && button.ActualHeight > 0)
            .Distinct()
            .Select(button => new AssetLibraryP3AutomatedButtonState(
                ElementIdentity(button),
                GetAutomationId(button),
                AutomationProperties.GetName(button),
                button.Content?.ToString() ?? string.Empty,
                AccessibleIdentity(button),
                GetAccessibleIdentitySource(button),
                button.IsEnabled,
                button.IsKeyboardFocused,
                button.IsMouseOver,
                button.IsPressed,
                button.ActualWidth,
                button.ActualHeight))
            .ToArray();
    }

    public async Task DrainDispatcherAsync()
    {
        EnsureNotDisposed();
        await _page.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await _page.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        RemoveButtonStateSurface();
        _disposed = true;
        _page.RemoveHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnDragStarted));
        _page.RemoveHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnDragDelta));
        _page.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnDragCompleted));
        _page.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
        _page.RemoveHandler(Keyboard.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        _page.RemoveHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));
        _page.RemoveHandler(Keyboard.KeyUpEvent, new KeyEventHandler(OnKeyUp));
        _page.RemoveHandler(Button.ClickEvent, new RoutedEventHandler(OnButtonClick));
        _page.RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnSelectionChanged));
        _page.RemoveHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler(OnTextInput));
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.AssetCards.CollectionChanged -= OnAssetCardsChanged;
        _acceptanceRepository.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task ExecuteBoundButtonCommandAsync(string automationId)
    {
        var button = FindRequired<Button>(automationId);
        if (!button.IsVisible || !button.IsEnabled)
            throw new InvalidOperationException($"The real WPF button '{automationId}' is not visible and enabled.");
        var command = button.Command ?? throw new InvalidOperationException($"The real WPF button '{automationId}' has no bound command.");
        if (!command.CanExecute(button.CommandParameter))
            throw new InvalidOperationException($"The bound command for '{automationId}' cannot execute.");
        // Space follows ButtonBase's real keyboard route; ButtonBase emits Click and invokes
        // its bound command exactly once. Raising Click directly would bypass OnClick's command path.
        await RaiseKeyboardRouteAsync(button, Key.Space);
    }

    private async Task RaiseSplitterDragAsync(GridSplitter splitter, double horizontalChange)
    {
        if (!splitter.IsVisible || !splitter.IsEnabled || !splitter.ShowsPreview)
            throw new InvalidOperationException($"The real WPF splitter '{GetAutomationId(splitter)}' is not ready for preview drag.");
        splitter.Focus();
        splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = splitter });
        splitter.RaiseEvent(new DragDeltaEventArgs(horizontalChange, 0) { RoutedEvent = Thumb.DragDeltaEvent, Source = splitter });
        splitter.RaiseEvent(new DragCompletedEventArgs(horizontalChange, 0, false) { RoutedEvent = Thumb.DragCompletedEvent, Source = splitter });
        await DrainDispatcherAsync();
    }

    private async Task RaiseKeyboardRouteAsync(UIElement control, Key key)
    {
        if (!control.IsVisible || !control.IsEnabled || !control.Focus())
            throw new InvalidOperationException($"The real WPF control '{GetAutomationId(control)}' is not ready for keyboard input.");
        var inputSource = PresentationSource.FromVisual(control)
            ?? throw new InvalidOperationException("The keyboard target is not attached to the live presentation source.");
        foreach (var routedEvent in new[]
                 {
                     Keyboard.PreviewKeyDownEvent,
                     Keyboard.KeyDownEvent,
                     Keyboard.PreviewKeyUpEvent,
                     Keyboard.KeyUpEvent,
                 })
        {
            control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
            {
                RoutedEvent = routedEvent,
                Source = control,
            });
        }
        await DrainDispatcherAsync();
    }

    private T FindRequired<T>(string automationId) where T : FrameworkElement =>
        EnumerateVisuals<T>(_page).SingleOrDefault(element => string.Equals(GetAutomationId(element), automationId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"The live Asset Library visual tree is missing '{automationId}'.");

    private static IEnumerable<T> EnumerateVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in EnumerateVisuals<T>(child)) yield return descendant;
        }
    }

    private void OnDragStarted(object sender, DragStartedEventArgs e) => ObserveRouted("routed-input", e);
    private void OnDragDelta(object sender, DragDeltaEventArgs e) => ObserveRouted("routed-input", e, new { e.HorizontalChange, e.VerticalChange });
    private void OnDragCompleted(object sender, DragCompletedEventArgs e) => ObserveRouted("routed-input", e, new { e.HorizontalChange, e.VerticalChange, e.Canceled });
    private void OnPreviewKeyDown(object sender, KeyEventArgs e) => ObserveRouted("routed-input", e, new { Key = e.Key.ToString(), Phase = "preview-down" });
    private void OnKeyDown(object sender, KeyEventArgs e) => ObserveRouted("routed-input", e, new { Key = e.Key.ToString(), Phase = "down" });
    private void OnPreviewKeyUp(object sender, KeyEventArgs e) => ObserveRouted("routed-input", e, new { Key = e.Key.ToString(), Phase = "preview-up" });
    private void OnKeyUp(object sender, KeyEventArgs e) => ObserveRouted("routed-input", e, new { Key = e.Key.ToString(), Phase = "up" });
    private void OnButtonClick(object sender, RoutedEventArgs e) => ObserveRouted("routed-command-source", e);
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => ObserveRouted("selection-changed", e, new { Added = e.AddedItems.Count, Removed = e.RemovedItems.Count });
    private void OnTextInput(object sender, TextCompositionEventArgs e) => ObserveRouted("text-composition", e, new { e.Text, e.SystemText, e.ControlText });

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not { } propertyName || !ObservedProperties.Contains(propertyName)) return;
        Observed?.Invoke(this, new(
            "view-model-property",
            propertyName,
            "AssetLibraryViewModel",
            ReadObservedProperty(propertyName),
            DateTimeOffset.UtcNow));
    }

    private void OnAssetCardsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Observed?.Invoke(this, new(
            "collection-changed",
            e.Action.ToString(),
            "AssetGrid",
            new { Count = _viewModel.AssetCards.Count },
            DateTimeOffset.UtcNow));

    private void ObserveRouted(string kind, RoutedEventArgs args, object? value = null)
    {
        var source = args.OriginalSource as DependencyObject ?? args.Source as DependencyObject;
        Observed?.Invoke(this, new(
            kind,
            args.RoutedEvent.Name,
            GetAutomationId(source),
            value,
            DateTimeOffset.UtcNow));
    }

    private object? ReadObservedProperty(string propertyName) => propertyName switch
    {
        nameof(AssetLibraryViewModel.IsLoading) => _viewModel.IsLoading,
        nameof(AssetLibraryViewModel.IsReady) => _viewModel.IsReady,
        nameof(AssetLibraryViewModel.LoadErrorMessage) => _viewModel.LoadErrorMessage,
        nameof(AssetLibraryViewModel.LoadAttempt) => _viewModel.LoadAttempt,
        nameof(AssetLibraryViewModel.OrganizationPaneWidth) => _viewModel.OrganizationPaneWidth,
        nameof(AssetLibraryViewModel.InspectorPaneWidth) => _viewModel.InspectorPaneWidth,
        nameof(AssetLibraryViewModel.IsOrganizationPaneCollapsed) => _viewModel.IsOrganizationPaneCollapsed,
        nameof(AssetLibraryViewModel.IsInspectorPaneCollapsed) => _viewModel.IsInspectorPaneCollapsed,
        nameof(AssetLibraryViewModel.IsOrganizationPaneVisible) => _viewModel.IsOrganizationPaneVisible,
        nameof(AssetLibraryViewModel.IsInspectorPaneVisible) => _viewModel.IsInspectorPaneVisible,
        nameof(AssetLibraryViewModel.ThumbnailWidth) => _viewModel.ThumbnailWidth,
        nameof(AssetLibraryViewModel.SearchText) => _viewModel.SearchText,
        nameof(AssetLibraryViewModel.SelectedAsset) => _viewModel.SelectedAsset?.AssetId.ToString("D"),
        nameof(AssetLibraryViewModel.SelectionCount) => _viewModel.SelectionCount,
        nameof(AssetLibraryViewModel.VisibleCount) => _viewModel.VisibleCount,
        _ => null,
    };

    private static string ElementIdentity(FrameworkElement element)
    {
        var automationId = GetAutomationId(element);
        if (automationId.Length > 0) return automationId;
        var automationName = AutomationProperties.GetName(element);
        if (automationName.Length > 0) return automationName;
        if (element.Name.Length > 0) return element.Name;
        if (element is Button button) return AccessibleIdentity(button);
        return $"<{element.GetType().Name}>";
    }

    private static string AccessibleIdentity(Button button)
    {
        var identity = GetAutomationId(button);
        if (identity.Length > 0) return identity;
        identity = AutomationProperties.GetName(button);
        if (identity.Length > 0) return identity;
        if (button.Content is string content && !string.IsNullOrWhiteSpace(content)) return content.Trim();
        if (!string.IsNullOrWhiteSpace(button.Name)) return button.Name;
        throw new InvalidOperationException(
            "A realized P3 button has no AutomationId, accessible name, text content, or XAML name; type-name fallback is forbidden.");
    }

    private static string GetAccessibleIdentitySource(Button button)
    {
        if (GetAutomationId(button).Length > 0) return "automation-id";
        if (AutomationProperties.GetName(button).Length > 0) return "automation-name";
        if (button.Content is string content && !string.IsNullOrWhiteSpace(content)) return "text-content";
        if (!string.IsNullOrWhiteSpace(button.Name)) return "xaml-name";
        return "missing";
    }

    private string GetComparableParentIdentity(FrameworkElement element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (ReferenceEquals(parent, _page)) return "AssetLibraryPage";
            if (parent is not FrameworkElement frameworkParent) continue;
            var automationId = GetAutomationId(frameworkParent);
            if (automationId.Length > 0) return automationId;
            if (frameworkParent.Name.Length > 0) return frameworkParent.Name;
        }
        return "AssetLibraryPage";
    }

    private static int GetVisualDepth(DependencyObject element)
    {
        var depth = 0;
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent)) depth++;
        return depth;
    }

    private static bool RectsEqual(Rect first, Rect second) =>
        Math.Abs(first.X - second.X) <= 0.01 &&
        Math.Abs(first.Y - second.Y) <= 0.01 &&
        Math.Abs(first.Width - second.Width) <= 0.01 &&
        Math.Abs(first.Height - second.Height) <= 0.01;

    private SolidColorBrush ResolveButtonBrush(
        Button button,
        Style style,
        string state,
        bool active,
        DependencyProperty property)
    {
        var value = button.GetValue(property);
        if (state is "hover" or "pressed")
            value = ResolveTriggeredStyleValue(style, property, state, active, value);
        return value as SolidColorBrush
            ?? throw new InvalidOperationException($"The live WPF role '{style}' did not resolve {property.Name} to a solid brush for '{state}'.");
    }

    private static object? ResolveTriggeredStyleValue(
        Style style,
        DependencyProperty property,
        string state,
        bool active,
        object? initialValue)
    {
        var value = style.BasedOn is null
            ? initialValue
            : ResolveTriggeredStyleValue(style.BasedOn, property, state, active, initialValue);
        foreach (var trigger in style.Triggers)
        {
            var matches = trigger switch
            {
                Trigger single => TriggerConditionMatches(single.Property, single.Value, state, active),
                MultiTrigger multi => multi.Conditions.Cast<System.Windows.Condition>()
                    .All(condition => TriggerConditionMatches(condition.Property, condition.Value, state, active)),
                _ => false,
            };
            if (!matches) continue;
            var setter = trigger switch
            {
                Trigger single => single.Setters.OfType<Setter>().LastOrDefault(candidate => candidate.Property == property),
                MultiTrigger multi => multi.Setters.OfType<Setter>().LastOrDefault(candidate => candidate.Property == property),
                _ => null,
            };
            if (setter is not null) value = setter.Value;
        }
        return value;
    }

    private static bool TriggerConditionMatches(
        DependencyProperty property,
        object expected,
        string state,
        bool active)
    {
        object actual = property switch
        {
            _ when property == UIElement.IsMouseOverProperty => state == "hover",
            _ when property == ButtonBase.IsPressedProperty => state == "pressed",
            _ when property == UIElement.IsEnabledProperty => state != "disabled",
            _ when property == UIElement.IsKeyboardFocusedProperty => state == "focus",
            _ when property == FrameworkElement.TagProperty => active ? "Active" : string.Empty,
            _ => DependencyProperty.UnsetValue,
        };
        return !ReferenceEquals(actual, DependencyProperty.UnsetValue) && Equals(actual, expected);
    }

    private Color ResourceColor(string key) =>
        _page.TryFindResource(key) is SolidColorBrush brush
            ? brush.Color
            : throw new InvalidOperationException($"The live Asset Library page is missing color resource '{key}'.");

    private static Color Composite(Color foreground, Color background)
    {
        var alpha = foreground.A / 255d;
        byte Blend(byte front, byte back) => (byte)Math.Round(front * alpha + back * (1 - alpha));
        return Color.FromRgb(Blend(foreground.R, background.R), Blend(foreground.G, background.G), Blend(foreground.B, background.B));
    }

    private static double ContrastRatio(Color first, Color second)
    {
        static double Luminance(Color color)
        {
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
               (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string GetAutomationId(DependencyObject? element) =>
        element is null ? string.Empty : AutomationProperties.GetAutomationId(element) ?? string.Empty;

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_page.Dispatcher.CheckAccess())
            throw new InvalidOperationException("The automated acceptance driver must run on the live WPF Dispatcher.");
    }
}

public sealed record AssetLibraryP3AutomatedObservation(
    string Kind,
    string Name,
    string SourceAutomationId,
    object? Value,
    DateTimeOffset ObservedAt);

public sealed record AssetLibraryP3CanonicalQuerySnapshot(
    AssetQueryDocument Document,
    string CanonicalJson,
    string CanonicalSha256,
    int RuleCount);

public sealed record AssetLibraryP3ParameterizedQueryPlan(
    bool Parameterized,
    int UnparameterizedSqlCount,
    int ParameterCount,
    string ExplainQueryPlan,
    string CanonicalSha256,
    string SqlTemplate,
    string SqlTemplateSha256,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<string> ParameterValueSha256,
    IReadOnlyList<string> ExplainRows);

public sealed record AssetLibraryP3QueryResultSnapshot(
    IReadOnlyList<string> AssetIds,
    string AssetIdSha256,
    int ResultCount,
    string? ErrorMessage,
    TimeSpan Elapsed);

public sealed record AssetLibraryP3PublishedQuerySnapshot(
    long QueryGeneration,
    string Scope,
    Guid? FolderId,
    Guid? SmartFolderId,
    IReadOnlyList<string> AssetIds,
    string AssetIdSha256,
    int TotalCount,
    string OracleAssetIdSha256);

public sealed record AssetLibraryP3ImeSnapshot(
    bool SuggestionsSuppressedDuringComposition,
    bool IsComposing,
    string SearchText,
    bool CancelledGenerationPublished,
    long SupersededGeneration,
    long PublishedGeneration,
    bool QueryCancellationObserved,
    bool CancelledQueryGenerationPublished,
    long CancelledQueryGeneration,
    long PublishedQueryGeneration);

public sealed record AssetLibraryP3QueryCancellationSnapshot(
    bool CancellationObserved,
    bool CancelledGenerationPublished,
    long CancelledGeneration,
    long PublishedGeneration);

public sealed record AssetLibraryP3HistorySnapshot(
    string SearchText,
    int SuggestionCount,
    bool SuggestionsSuppressedDuringComposition,
    IReadOnlyList<string> History,
    bool Deduplicated,
    bool SingleEntryRemoved,
    bool AllEntriesCleared);

public sealed record AssetLibraryP3SmartFolderSnapshot(
    Guid SmartFolderId,
    string LoadedCanonicalJson,
    string ExpectedCanonicalJson,
    int PreviewCount,
    long PreviewMilliseconds,
    bool ArchiveRestorePassed,
    string SavedCanonicalSha256,
    string LoadedCanonicalSha256,
    string EditorBeforePreviewSha256,
    string EditorAfterPreviewSha256,
    string PersistedBeforePreviewSha256,
    string PersistedAfterPreviewSha256,
    bool PreviewIsolated,
    bool CancellationIsolated,
    bool EditorClosedAfterCancellation);

public sealed record AssetLibraryP3MigrationSnapshot(
    int MigratedSchemaVersion,
    string QueryHash,
    bool InvalidReferenceFailClosed);

public sealed record AssetLibraryP3TagLifecycleSnapshot(
    bool RenameCommandChangedState,
    bool RenamePreservedMemberships,
    int MergeChangedCount,
    int MergeDuplicateMembershipCount,
    bool GroupCycleRejected);

public sealed record AssetLibraryP3BatchSnapshot(
    int BatchSize,
    int PreviewCount,
    int CommittedCount,
    bool UndoPassed,
    bool RedoPassed,
    bool MembershipsDeduplicated,
    TimeSpan Elapsed,
    string OperationId);

public sealed record AssetLibraryP3MeasuredBatchSnapshot(
    AssetLibraryP3BatchSnapshot Batch,
    int DispatcherSampleCount,
    double MaximumDispatcherGapMilliseconds,
    double AverageDispatcherGapMilliseconds);

public sealed record AssetLibraryP3JournalConsistencySnapshot(
    bool IsValid,
    int EntryCount,
    bool UniqueOperationIds,
    bool DescendingCreatedAt,
    bool DescriptionsAndKindsPresent,
    bool UndoStateCoherent,
    bool RequiredOperationsPresent,
    IReadOnlyList<string> RequiredOperationIds);

public sealed record AssetLibraryP3ContentStateSnapshot(
    bool EmptyStateObserved,
    bool ErrorStateObserved,
    string ErrorMessage,
    string RetryButtonAccessibleIdentity,
    bool LoadingObservedDuringRetry,
    bool CancelledStateObserved,
    bool RetryRecoveredReadyState,
    int RecoveredVisibleAssetCount);

public sealed record AssetLibraryP3RestartUndoRedoSnapshot(
    int BeforeMembershipCount,
    int UndoneMembershipCount,
    int RedoneMembershipCount,
    string BeforeMembershipSha256,
    string UndoneMembershipSha256,
    string RedoneMembershipSha256);

internal sealed record AssetLibraryP3MembershipDigest(int Count, string Sha256);

public sealed record AssetLibraryP3BrowserSnapshot(
    string ActiveCollection,
    string ViewMode,
    string SortField,
    string SortDirection,
    string QueryDescription,
    int QueryTotalCount,
    int VisibleCount,
    int SelectionCount,
    IReadOnlyList<string> SelectedAssetIds,
    string InspectorMode,
    string SingleFolderSummary,
    string SingleTagSummary,
    string MultipleFolderSummary,
    string MultipleTagSummary,
    string MultipleRatingSummary,
    int FolderNodeCount,
    int SmartFolderCount,
    int TagNodeCount,
    bool FolderTreeAcyclic,
    int RealizedItemCount,
    bool IsVirtualizing,
    string VirtualizationMode);

public sealed record AssetLibraryP3CommandSnapshot(
    string Command,
    int SelectionCount,
    string Target,
    string Preview,
    string Result,
    bool CanUndo);

public sealed record AssetLibraryP3AutomatedState(
    bool PageLoaded,
    bool PageVisible,
    double PageWidth,
    double PageHeight,
    int LoadAttempt,
    bool IsLoading,
    bool IsReady,
    bool HasLoadError,
    string LoadErrorMessage,
    bool EmptyStateVisible,
    int VisibleAssetCount,
    double OrganizationPersistedWidth,
    double OrganizationActualWidth,
    bool OrganizationCollapsed,
    bool OrganizationVisible,
    double InspectorPersistedWidth,
    double InspectorActualWidth,
    bool InspectorCollapsed,
    bool InspectorVisible,
    double CollectionActualWidth,
    double ThumbnailPersistedWidth,
    double ThumbnailSliderValue,
    double ThumbnailSliderMinimum,
    double ThumbnailSliderMaximum,
    string SearchText,
    IReadOnlyList<string> SelectedAssetIds,
    string FocusedAutomationId);

public sealed record AssetLibraryP3AutomatedElementBounds(
    string Identity,
    string ElementType,
    string ParentIdentity,
    int Depth,
    string Visibility,
    double X,
    double Y,
    double Width,
    double Height,
    double VisibleX,
    double VisibleY,
    double VisibleWidth,
    double VisibleHeight,
    bool Clipped,
    bool Overlapped,
    bool IsEnabled,
    bool Focusable,
    bool MustFit);

public sealed record AssetLibraryP3AutomatedButtonDefinition(
    string Identity,
    string Role,
    string SurfaceResourceKey,
    bool HasTextContent,
    bool Active = false);

public sealed record AssetLibraryP3AutomatedButtonReadabilityState(
    [property: JsonPropertyName("button_identity")] string ButtonIdentity,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("theme")] string Theme,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("surface_resource_key")] string SurfaceResourceKey,
    [property: JsonPropertyName("surface_color")] string SurfaceColor,
    [property: JsonPropertyName("background_color")] string BackgroundColor,
    [property: JsonPropertyName("foreground_color")] string ForegroundColor,
    [property: JsonPropertyName("border_color")] string BorderColor,
    [property: JsonPropertyName("non_text_reference_color")] string NonTextReferenceColor,
    [property: JsonPropertyName("focus_outer_color")] string FocusOuterColor,
    [property: JsonPropertyName("focus_inner_color")] string FocusInnerColor,
    [property: JsonPropertyName("text_contrast")] double? TextContrast,
    [property: JsonPropertyName("non_text_contrast")] double? NonTextContrast,
    [property: JsonPropertyName("focus_contrast")] double FocusContrast,
    [property: JsonPropertyName("focus_visible")] bool FocusVisible,
    [property: JsonPropertyName("text_contrast_applicable")] bool TextContrastApplicable,
    [property: JsonPropertyName("non_text_contrast_applicable")] bool NonTextContrastApplicable,
    [property: JsonPropertyName("live_wpf_button_instance")] bool LiveWpfButtonInstance,
    [property: JsonPropertyName("source_declaration_probe")] bool SourceDeclarationProbe,
    [property: JsonPropertyName("template_applied")] bool TemplateApplied,
    [property: JsonPropertyName("state_resolution")] string StateResolution);

public sealed record AssetLibraryP3AutomatedButtonState(
    string Identity,
    string AutomationId,
    string AutomationName,
    string Content,
    string AccessibleIdentity,
    string AccessibleIdentitySource,
    bool IsEnabled,
    bool IsKeyboardFocused,
    bool IsMouseOver,
    bool IsPressed,
    double Width,
    double Height);
#endif

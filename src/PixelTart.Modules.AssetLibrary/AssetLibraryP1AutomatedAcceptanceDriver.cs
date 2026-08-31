#if ASSET_LIBRARY_P1_AUTOMATED_ACCEPTANCE
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PixelTart.Modules.AssetLibrary;

/// <summary>
/// Drives the public WPF surface used by the P1 automated acceptance build.
/// It deliberately owns no evidence files and makes no pass/fail decision.
/// </summary>
public sealed class AssetLibraryP1AutomatedAcceptanceDriver : IDisposable
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

    private static readonly AssetLibraryP1AutomatedButtonDefinition[] ButtonDefinitions =
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
    private bool _disposed;

    public AssetLibraryP1AutomatedAcceptanceDriver(AssetLibraryPage page)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _viewModel = page.ViewModel;
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

    public event EventHandler<AssetLibraryP1AutomatedObservation>? Observed;

    public AssetLibraryP1AutomatedState CaptureState()
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
        TextCompositionManager.CompleteComposition(composition);
        await DrainDispatcherAsync();
        if (!string.Equals(_viewModel.SearchText, text, StringComparison.Ordinal))
            throw new InvalidOperationException("The WPF text-composition route did not update the bound Asset Library search query.");
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

    public IReadOnlyList<AssetLibraryP1AutomatedElementBounds> CaptureVisibleBounds()
    {
        EnsureNotDisposed();
        _page.UpdateLayout();
        var pageBounds = new Rect(0, 0, _page.ActualWidth, _page.ActualHeight);
        var candidates = new List<(FrameworkElement Element, string Identity, string ParentIdentity, int Depth, Rect Bounds, Rect VisibleBounds)>();
        foreach (var element in EnumerateVisuals<FrameworkElement>(_page).Prepend(_page).Distinct())
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            var automationId = GetAutomationId(element);
            var isActualButton = element is Button;
            if (!isActualButton && !MustFitAutomationIds.Contains(automationId)) continue;
            try
            {
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
                // treated as clipped layout controls. Partially rendered buttons remain in
                // the set so the independent overflow check can still reject real clipping.
                if (isActualButton &&
                    (visibleBounds.IsEmpty || visibleBounds.Width <= 0.5 || visibleBounds.Height <= 0.5))
                    continue;
                candidates.Add((
                    element,
                    ElementIdentity(element),
                    GetComparableParentIdentity(element),
                    GetVisualDepth(element),
                    bounds,
                    visibleBounds));
            }
            catch (InvalidOperationException)
            {
                // A visual can detach while an async thumbnail finishes. It is not part of this frame.
            }
        }
        return candidates.Select(candidate =>
        {
            var clipped = !RectsEqual(candidate.Bounds, candidate.VisibleBounds);
            var overlapped = candidates.Any(other =>
                !ReferenceEquals(other.Element, candidate.Element) &&
                candidate.Depth == other.Depth &&
                string.Equals(candidate.ParentIdentity, other.ParentIdentity, StringComparison.Ordinal) &&
                Rect.Intersect(candidate.VisibleBounds, other.VisibleBounds) is { IsEmpty: false } intersection &&
                intersection.Width > 0.5 && intersection.Height > 0.5);
            return new AssetLibraryP1AutomatedElementBounds(
                candidate.Identity,
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
                true);
        }).ToArray();
    }

    public IReadOnlyList<AssetLibraryP1AutomatedButtonReadabilityState> CaptureButtonReadabilityMatrix(string theme)
    {
        EnsureNotDisposed();
        if (theme is not ("dark" or "light" or "high-contrast"))
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "The automated button theme is unknown.");

        _page.UpdateLayout();
        var results = new List<AssetLibraryP1AutomatedButtonReadabilityState>(ButtonDefinitions.Length * 5);
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
            probe.Measure(new Size(240, 80));
            probe.Arrange(new Rect(0, 0, Math.Max(40, probe.DesiredSize.Width), Math.Max(40, probe.DesiredSize.Height)));
            var templateApplied = probe.ApplyTemplate();

            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
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

                results.Add(new AssetLibraryP1AutomatedButtonReadabilityState(
                    definition.Identity,
                    definition.Role,
                    theme,
                    state,
                    definition.SurfaceResourceKey,
                    ToHex(surface),
                    ToHex(renderedBackground),
                    ToHex(renderedForeground),
                    ToHex(renderedBorder),
                    ToHex(nonTextReference),
                    ToHex(focusOuter),
                    ToHex(focusInner),
                    textContrast,
                    nonTextContrast,
                    focusContrast,
                    focusContrast >= 3,
                    definition.HasTextContent,
                    nonTextContrast.HasValue,
                    probe is Button,
                    true,
                    templateApplied,
                    state switch
                    {
                        "normal" => "wpf-effective-value",
                        "disabled" => "wpf-effective-disabled-trigger",
                        "focus" => "wpf-control-template-focus-trigger",
                        _ => "wpf-style-trigger-resolution",
                    }));
            }
        }
        return results;
    }

    public IReadOnlyList<AssetLibraryP1AutomatedButtonState> CaptureRealizedButtons()
    {
        EnsureNotDisposed();
        _page.UpdateLayout();
        return EnumerateVisuals<Button>(_page)
            .Where(button => button.IsVisible && button.ActualWidth > 0 && button.ActualHeight > 0)
            .Distinct()
            .Select(button => new AssetLibraryP1AutomatedButtonState(
                ElementIdentity(button),
                GetAutomationId(button),
                AutomationProperties.GetName(button),
                button.Content?.ToString() ?? string.Empty,
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
        return $"<{element.GetType().Name}>";
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

public sealed record AssetLibraryP1AutomatedObservation(
    string Kind,
    string Name,
    string SourceAutomationId,
    object? Value,
    DateTimeOffset ObservedAt);

public sealed record AssetLibraryP1AutomatedState(
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

public sealed record AssetLibraryP1AutomatedElementBounds(
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

public sealed record AssetLibraryP1AutomatedButtonDefinition(
    string Identity,
    string Role,
    string SurfaceResourceKey,
    bool HasTextContent,
    bool Active = false);

public sealed record AssetLibraryP1AutomatedButtonReadabilityState(
    string ButtonIdentity,
    string Role,
    string Theme,
    string State,
    string SurfaceResourceKey,
    string SurfaceColor,
    string BackgroundColor,
    string ForegroundColor,
    string BorderColor,
    string NonTextReferenceColor,
    string FocusOuterColor,
    string FocusInnerColor,
    double? TextContrast,
    double? NonTextContrast,
    double FocusContrast,
    bool FocusVisible,
    bool TextContrastApplicable,
    bool NonTextContrastApplicable,
    bool LiveWpfButtonInstance,
    bool SourceDeclarationProbe,
    bool TemplateApplied,
    string StateResolution);

public sealed record AssetLibraryP1AutomatedButtonState(
    string Identity,
    string AutomationId,
    string AutomationName,
    string Content,
    bool IsEnabled,
    bool IsKeyboardFocused,
    bool IsMouseOver,
    bool IsPressed,
    double Width,
    double Height);
#endif

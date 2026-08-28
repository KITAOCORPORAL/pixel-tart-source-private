using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelTart.Kernel;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Tasks;
using SmartFolderField = RAWSelectionAssistant.Core.Models.SmartFolderField;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class EmbeddedAssetLibraryWpfTests
{
    [TestMethod]
    public void LocalPixelProviderExecutesDeterministicPixelFixture()
    {
        var pixels = Enumerable.Repeat(new byte[] { 220, 30, 40 }, 64).SelectMany(value => value).ToArray();
        var buffer = new VisualPixelBuffer(8, 8, pixels);
        var request = new AssetVisualAnalysisRequest(Guid.NewGuid(), VisualAnalysisFingerprint.Compute(buffer), buffer);
        var registry = new PixelTartModuleRegistry();
        foreach (var capability in new[] { "core.navigation", "core.task-center", "core.settings", "core.file-safety" })
            registry.Capabilities.Register(new(capability, "pixel-tart.kernel", "kernel/v1"));
        registry.Register(new AssetLibraryModule());
        Assert.IsTrue(registry.Providers.TryGet("visual-analysis.local-pixel", out var descriptor));
        var provider = (LocalPixelVisualAnalysisProvider)descriptor.Provider;

        var result = provider.Analyze(request);

        Assert.AreEqual(AssetVisualFeatureContract.AnalysisVersion, provider.AnalysisVersion);
        Assert.HasCount(256, result.HistogramLuma);
        Assert.AreEqual(64u, result.HistogramR.Aggregate(0u, (sum, value) => sum + value));
        Assert.IsGreaterThan(0, result.Palette.Count);
    }

    [TestMethod]
    public async Task EmbeddedPageImportsReferenceWithoutChangingSourceAndPopulatesGridCollection()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedAsset", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "synthetic-reference.jpg");
            await RunSta(() =>
            {
                WriteSyntheticJpeg(source);
                var before = SHA256.HashData(File.ReadAllBytes(source));
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [new("AssetLibraryModuleDiagnostic", "asset")]);

                page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                page.ViewModel.ImportDemoDirectoryAsync(root).GetAwaiter().GetResult();

                Assert.IsFalse(page.ViewModel.IsPreviewDiagnosticsEnabled);
                Assert.HasCount(0, page.ViewModel.ModuleDiagnostics);
                page.Measure(new Size(1600, 900));
                page.Arrange(new Rect(0, 0, 1600, 900));
                page.UpdateLayout();
                var hiddenDiagnostics = FindVisualByAutomationId<Expander>(page, "ModuleDiagnostics");
                Assert.IsFalse(hiddenDiagnostics.IsExpanded);
                Assert.AreEqual(Visibility.Collapsed, hiddenDiagnostics.Visibility);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => AsyncThumbnail.PendingRequestCount == 0,
                    TimeSpan.FromSeconds(10)),
                    $"Async thumbnails did not drain; pending={AsyncThumbnail.PendingRequestCount}.");
                Assert.HasCount(0, page.ViewModel.Folders);
                Assert.HasCount(1, page.ViewModel.AssetCards);
                Assert.AreEqual("synthetic-reference.jpg", page.ViewModel.AssetCards[0].Asset.DisplayName);
                CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(source)));
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task PersistedSingleAssetSelectionRestoresAndInvalidAssetFallsBackToNoSelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedSelectionRestore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "selection-restore.jpg");
            await RunSta(() =>
            {
                WriteSyntheticJpeg(source);
                var databasePath = Path.Combine(root, "asset-library.db");
                var state = new RAWSelectionAssistant.Core.Models.AssetLibraryWorkspaceSettings();
                var firstPage = new AssetLibraryPage(
                    databasePath,
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                firstPage.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                firstPage.ViewModel.ImportDemoDirectoryAsync(root).GetAwaiter().GetResult();
                var asset = firstPage.ViewModel.AssetCards.Single().Asset;
                firstPage.ViewModel.SyncSelection([asset]);
                Assert.AreEqual(asset.AssetId, state.SelectedAssetId);
                firstPage.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();

                var restoredPage = new AssetLibraryPage(
                    databasePath,
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                restoredPage.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                ArrangePage(restoredPage, 1600, 900);
                var restoredGrid = FindVisualByAutomationId<ListBox>(restoredPage, "AssetGrid");
                Assert.HasCount(1, restoredPage.ViewModel.SelectedAssets);
                Assert.AreEqual(asset.AssetId, restoredPage.ViewModel.SelectedAssets[0].AssetId);
                Assert.HasCount(1, restoredGrid.SelectedItems.Cast<object>().ToArray());
                Assert.AreEqual(asset.AssetId, ((AssetVisualMatchView)restoredGrid.SelectedItem).Asset.AssetId);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => AsyncThumbnail.PendingRequestCount == 0,
                    TimeSpan.FromSeconds(10)),
                    $"Restored selection thumbnails did not drain; pending={AsyncThumbnail.PendingRequestCount}.");
                restoredPage.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();

                state.SelectedAssetId = Guid.NewGuid();
                var fallbackPage = new AssetLibraryPage(
                    databasePath,
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                fallbackPage.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                ArrangePage(fallbackPage, 1600, 900);
                var fallbackGrid = FindVisualByAutomationId<ListBox>(fallbackPage, "AssetGrid");
                Assert.IsNull(state.SelectedAssetId);
                Assert.HasCount(0, fallbackPage.ViewModel.SelectedAssets);
                Assert.HasCount(0, fallbackGrid.SelectedItems.Cast<object>().ToArray());
                Assert.IsTrue(PumpDispatcherUntil(
                    () => AsyncThumbnail.PendingRequestCount == 0,
                    TimeSpan.FromSeconds(10)),
                    $"Fallback selection thumbnails did not drain; pending={AsyncThumbnail.PendingRequestCount}.");
                fallbackPage.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task EmbeddedCommandsRefreshAfterAssetLoadAndVisualAnalysis()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedCommands", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "command-state.jpg");
            await RunSta(() =>
            {
                WriteSyntheticJpeg(source);
                var databasePath = Path.Combine(root, "asset-library.db");
                var page = new AssetLibraryPage(
                    databasePath,
                    new TaskOperationBridge(),
                    [
                        new("AssetLibraryModuleDiagnostic", "asset"),
                        new("RawToolModuleDiagnostic", "raw"),
                        new("OnlineSelectionModuleDiagnostic", "online")
                    ],
                    enablePreviewFeatures: true);

                page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                Assert.IsTrue(page.ViewModel.IsPreviewDiagnosticsEnabled);
                Assert.HasCount(3, page.ViewModel.ModuleDiagnostics);
                Assert.IsNotEmpty(page.ViewModel.Folders);
                Assert.HasCount(0, page.ViewModel.SmartFolders);
                Assert.IsTrue(page.ViewModel.SaveSmartFolderCommand.CanExecute(null));

                var commandCompleted = false;
                var commandTimedOut = false;
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var previousContext = SynchronizationContext.Current;
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timeout = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromSeconds(10),
                    System.Windows.Threading.DispatcherPriority.Send,
                    (_, _) =>
                    {
                        commandTimedOut = true;
                        frame.Continue = false;
                    },
                    dispatcher);
                EventHandler? commandStateChanged = null;
                commandStateChanged = (_, _) =>
                {
                    if (!page.ViewModel.SaveSmartFolderCommand.CanExecute(null)) return;
                    commandCompleted = true;
                    frame.Continue = false;
                };
                page.ViewModel.SaveSmartFolderCommand.CanExecuteChanged += commandStateChanged;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                    timeout.Start();
                    page.ViewModel.SaveSmartFolderCommand.Execute(null);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                }
                finally
                {
                    timeout.Stop();
                    page.ViewModel.SaveSmartFolderCommand.CanExecuteChanged -= commandStateChanged;
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }

                Assert.IsFalse(commandTimedOut, "SaveSmartFolderCommand did not finish within ten seconds.");
                Assert.IsTrue(commandCompleted, "SaveSmartFolderCommand did not return to an executable state.");
                Assert.HasCount(1, page.ViewModel.SmartFolders);
                var savedSmartFolder = page.ViewModel.SmartFolders.Single();
                var verificationRepository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(databasePath));
                var savedRules = verificationRepository.ListSmartFolderRulesAsync(savedSmartFolder.SmartFolderId).GetAwaiter().GetResult();
                Assert.HasCount(6, savedRules);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        SmartFolderField.Tag,
                        SmartFolderField.VisualToneKey,
                        SmartFolderField.VisualAnalysisStatus,
                        SmartFolderField.VisualAverageSaturation,
                        SmartFolderField.VisualDominantHue,
                        SmartFolderField.Rating
                    },
                    savedRules.Select(rule => rule.Field).ToArray());
                StringAssert.Contains(page.ViewModel.Status, "已保存智能文件夹：精选参考");
                StringAssert.Contains(page.ViewModel.Status, "视觉状态=Analyzed");
                verificationRepository.DisposeAsync().AsTask().GetAwaiter().GetResult();

                Assert.IsFalse(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                var visibleStateChanges = 0;
                page.ViewModel.AnalyzeVisibleCommand.CanExecuteChanged += (_, _) => Interlocked.Increment(ref visibleStateChanges);

                page.ViewModel.ImportDemoDirectoryAsync(root).GetAwaiter().GetResult();

                Assert.IsGreaterThan(0, visibleStateChanges);
                Assert.HasCount(0, page.ViewModel.SelectedAssets);
                page.ViewModel.BatchScope = nameof(VisualBatchScope.Current);
                Assert.IsTrue(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                page.ViewModel.BatchScope = nameof(VisualBatchScope.Selected);
                Assert.IsFalse(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                StringAssert.Contains(page.ViewModel.BatchStatus, "请先选择素材");
                page.ViewModel.BatchScope = nameof(VisualBatchScope.Folder);
                Assert.IsFalse(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                StringAssert.Contains(page.ViewModel.BatchStatus, "请先选择文件夹");
                page.ViewModel.BatchScope = nameof(VisualBatchScope.Current);

                var paletteStateChanges = 0;
                page.ViewModel.FindPaletteSimilarCommand.CanExecuteChanged += (_, _) => paletteStateChanges++;
                Assert.IsFalse(page.ViewModel.FindPaletteSimilarCommand.CanExecute(null));

                page.ViewModel.ExecuteVisualContextActionAsync(
                    page.ViewModel.AssetCards[0].Asset,
                    VisualContextAction.Analyze).GetAwaiter().GetResult();

                Assert.IsGreaterThan(0, paletteStateChanges);
                Assert.IsNotNull(page.ViewModel.Analysis);
                Assert.IsTrue(page.ViewModel.FindPaletteSimilarCommand.CanExecute(null));

                var folder = page.ViewModel.Folders[0];
                page.ViewModel.ApplyFoldersAsync([folder.FolderId]).GetAwaiter().GetResult();
                page.ViewModel.SyncSelection([]);
                var folderRefreshStateChanges = Volatile.Read(ref visibleStateChanges);
                page.ViewModel.BatchScope = nameof(VisualBatchScope.Folder);
                Assert.IsFalse(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                page.ViewModel.SelectedFolder = folder;
                Assert.IsTrue(SpinWait.SpinUntil(
                    () => Volatile.Read(ref visibleStateChanges) > folderRefreshStateChanges,
                    TimeSpan.FromSeconds(5)));
                Assert.HasCount(0, page.ViewModel.SelectedAssets);
                Assert.HasCount(1, page.ViewModel.AssetCards);
                Assert.IsTrue(page.ViewModel.AnalyzeVisibleCommand.CanExecute(null));
                page.ViewModel.ExecuteVisualContextActionAsync(
                    page.ViewModel.AssetCards[0].Asset,
                    VisualContextAction.Analyze).GetAwaiter().GetResult();
                Assert.IsNotNull(page.ViewModel.Analysis);

                page.Measure(new Size(1600, 900));
                page.Arrange(new Rect(0, 0, 1600, 900));
                page.UpdateLayout();
                var diagnostics = FindVisualByAutomationId<Expander>(page, "ModuleDiagnostics");
                Assert.IsTrue(diagnostics.IsExpanded);
                Assert.AreEqual(Visibility.Visible, diagnostics.Visibility);
                foreach (var automationId in new[] { "AssetLibraryModuleDiagnostic", "RawToolModuleDiagnostic", "OnlineSelectionModuleDiagnostic" })
                {
                    var diagnostic = FindVisualByAutomationId<TextBlock>(page, automationId);
                    Assert.AreEqual(Visibility.Visible, diagnostic.Visibility);
                    Assert.IsGreaterThan(0d, diagnostic.ActualHeight);
                }

                var smartBuilder = FindVisualByAutomationId<Expander>(page, "VisualSmartFolderBuilder");
                var saveSmartFolder = FindVisualByAutomationId<Button>(page, "SaveVisualSmartFolder");
                var inspectorScroll = FindVisualByAutomationId<ScrollViewer>(page, "AssetInspectorScroll");
                Assert.IsTrue(smartBuilder.IsExpanded);
                Assert.IsTrue(saveSmartFolder.Focusable);
                Assert.IsTrue(saveSmartFolder.IsTabStop);
                Assert.IsGreaterThan(0d, saveSmartFolder.ActualHeight);
                var savePosition = saveSmartFolder.TranslatePoint(new Point(0, 0), inspectorScroll);
                Assert.IsGreaterThanOrEqualTo(0d, savePosition.Y);
                Assert.IsLessThanOrEqualTo(inspectorScroll.ActualHeight, savePosition.Y + saveSmartFolder.ActualHeight);
                Assert.AreSame(page.ViewModel.SaveSmartFolderCommand, saveSmartFolder.Command);

                page.Measure(new Size(2400, 1380));
                page.Arrange(new Rect(0, 0, 2400, 1380));
                page.UpdateLayout();
                Assert.IsTrue(PumpDispatcherUntil(
                    () => AsyncThumbnail.PendingRequestCount == 0,
                    TimeSpan.FromSeconds(10)),
                    $"Async thumbnails did not drain; pending={AsyncThumbnail.PendingRequestCount}.");
                var paletteSwatches = FindVisualChildren<Border>(page)
                    .Where(border => border.DataContext is DominantColor && border.Height == 34)
                    .ToArray();
                Assert.HasCount(page.ViewModel.Analysis.Palette.Count, paletteSwatches);
                foreach (var swatch in paletteSwatches)
                {
                    var color = (DominantColor)swatch.DataContext;
                    var expected = (Color)ColorConverter.ConvertFromString(color.Hex)!;
                    var brush = swatch.Background as SolidColorBrush;
                    Assert.IsGreaterThanOrEqualTo(40d, swatch.ActualWidth);
                    Assert.IsNotNull(brush);
                    Assert.AreEqual(expected, brush.Color);
                }
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task SwitchingSingleSelectionClearsOldInspectorAndPublishesOnlyTheNewAsset()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedSelection", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                WriteSyntheticJpeg(Path.Combine(root, "asset-a.jpg"));
                var assetBPath = Path.Combine(root, "asset-b.jpg");
                WriteSyntheticJpeg(assetBPath, 17, 1024, 1024);
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    []);
                page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                page.ViewModel.ImportDemoDirectoryAsync(root).GetAwaiter().GetResult();
                var assetA = page.ViewModel.AssetCards.Single(card => card.Asset.DisplayName == "asset-a.jpg").Asset;
                var assetB = page.ViewModel.AssetCards.Single(card => card.Asset.DisplayName == "asset-b.jpg").Asset;

                page.ViewModel.ExecuteVisualContextActionAsync(assetA, VisualContextAction.Analyze).GetAwaiter().GetResult();
                Assert.AreEqual(assetA.AssetId, page.ViewModel.Analysis?.AssetId);
                Assert.IsNotNull(page.ViewModel.SelectedFeatures);
                Assert.IsTrue(page.ViewModel.FindPaletteSimilarCommand.CanExecute(null));
                Assert.IsTrue(page.ViewModel.FindSimilarCommand.CanExecute(null));

                File.Delete(assetBPath);
                page.ViewModel.SyncSelection([assetB]);

                Assert.IsNull(page.ViewModel.Analysis);
                Assert.AreNotEqual(assetA.AssetId, page.ViewModel.SelectedFeatures?.AssetId);
                Assert.IsFalse(page.ViewModel.FindPaletteSimilarCommand.CanExecute(null));
                Assert.IsFalse(page.ViewModel.FindSimilarCommand.CanExecute(null));
                Assert.IsTrue(SpinWait.SpinUntil(() => !page.ViewModel.IsAnalyzing, TimeSpan.FromSeconds(5)));

                WriteSyntheticJpeg(assetBPath, 17, 1024, 1024);
                page.ViewModel.ExecuteVisualContextActionAsync(assetB, VisualContextAction.Analyze).GetAwaiter().GetResult();
                Assert.IsTrue(SpinWait.SpinUntil(
                    () => page.ViewModel.Analysis?.AssetId == assetB.AssetId && !page.ViewModel.IsAnalyzing,
                    TimeSpan.FromSeconds(10)));
                Assert.AreEqual(assetB.AssetId, page.ViewModel.Analysis?.AssetId);
                Assert.AreEqual(assetB.AssetId, page.ViewModel.SelectedFeatures?.AssetId);
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task WorkspaceLayoutRestoresClampsCollapsesAndRespondsToNarrowWidth()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedLayout", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var state = new RAWSelectionAssistant.Core.Models.AssetLibraryWorkspaceSettings
                {
                    OrganizationPaneWidth = 286,
                    InspectorPaneWidth = 414,
                    ThumbnailWidth = 232
                };
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                var viewModel = page.ViewModel;

                Assert.AreEqual(286d, viewModel.OrganizationPaneWidth);
                Assert.AreEqual(414d, viewModel.InspectorPaneWidth);
                Assert.AreEqual(232d, viewModel.ThumbnailWidth);

                viewModel.UpdateViewportWidth(1000);
                Assert.IsTrue(viewModel.IsOrganizationPaneVisible);
                Assert.IsFalse(viewModel.IsInspectorPaneVisible);
                Assert.AreEqual(new GridLength(0), viewModel.InspectorPaneColumnWidth);

                viewModel.UpdateViewportWidth(1280);
                Assert.IsTrue(viewModel.IsInspectorPaneVisible);
                viewModel.UpdatePaneWidths(12, 900);
                Assert.AreEqual(180d, state.OrganizationPaneWidth);
                Assert.AreEqual(520d, state.InspectorPaneWidth);

                viewModel.ToggleOrganizationPaneCommand.Execute(null);
                Assert.IsTrue(state.OrganizationPaneCollapsed);
                Assert.AreEqual(new GridLength(0), viewModel.OrganizationPaneColumnWidth);
                viewModel.ToggleOrganizationPaneCommand.Execute(null);
                Assert.IsFalse(state.OrganizationPaneCollapsed);

                viewModel.ToggleInspectorPinCommand.Execute(null);
                Assert.IsTrue(state.InspectorPinned);
                viewModel.UpdateViewportWidth(820);
                Assert.IsFalse(viewModel.IsInspectorPaneVisible);
                Assert.AreEqual("检查器（窗口过窄）", viewModel.InspectorPaneToggleLabel);
                viewModel.UpdateViewportWidth(900);
                Assert.IsTrue(viewModel.IsInspectorPaneVisible);
                Assert.IsFalse(viewModel.ToggleInspectorPaneCommand.CanExecute(null));

                viewModel.ThumbnailWidth = 248;
                Assert.AreEqual(248d, state.ThumbnailWidth);
                viewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task RealSplitterDragKeepsSideWidthBindingsAndCollapseRestoresTheDraggedWidths()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedSplitter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var state = new RAWSelectionAssistant.Core.Models.AssetLibraryWorkspaceSettings
                {
                    OrganizationPaneWidth = 286,
                    InspectorPaneWidth = 414
                };
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                using var presentation = AttachToPresentationSource(page, 1500, 820);
                ArrangePage(page, 1500, 820);

                var workspace = FindVisualByAutomationId<Grid>(page, "AssetLibraryThreePaneWorkspace");
                var organizationColumn = workspace.ColumnDefinitions[0];
                var collectionColumn = workspace.ColumnDefinitions[2];
                var inspectorColumn = workspace.ColumnDefinitions[4];
                var organizationSplitter = FindVisualByAutomationId<GridSplitter>(page, "AssetOrganizationSplitter");
                var inspectorSplitter = FindVisualByAutomationId<GridSplitter>(page, "AssetInspectorSplitter");
                var thumbnailSlider = FindVisualByAutomationId<Slider>(page, "AssetThumbnailSizeSlider");

                Assert.IsTrue(BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(BindingOperations.IsDataBound(inspectorColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(collectionColumn.Width.IsStar);

                RaiseSplitterDrag(organizationSplitter, horizontalChange: 36);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => state.OrganizationPaneWidth > 286 && BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty),
                    TimeSpan.FromSeconds(1)));
                var draggedOrganizationWidth = state.OrganizationPaneWidth;
                Assert.IsGreaterThan(286d, draggedOrganizationWidth);
                Assert.IsTrue(BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty),
                    "A real GridSplitter drag must not detach the responsive organization-column binding.");
                Assert.IsTrue(collectionColumn.Width.IsStar, "The elastic collection column must remain star-sized after dragging.");

                // Exercise WPF's real keyboard path: KeyDown changes the columns and PreviewKeyUp
                // schedules the same deferred persistence repair without a test-side layout pass.
                RaiseKeyboardAdjustment(organizationSplitter, Key.Right);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => Math.Abs(state.OrganizationPaneWidth - draggedOrganizationWidth) > .5 && BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty),
                    TimeSpan.FromSeconds(1)));
                var keyboardAdjustedOrganizationWidth = state.OrganizationPaneWidth;
                var retainedInspectorWidth = state.InspectorPaneWidth;
                Assert.AreNotEqual(draggedOrganizationWidth, keyboardAdjustedOrganizationWidth);
                Assert.IsTrue(BindingOperations.IsDataBound(inspectorColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(collectionColumn.Width.IsStar, "The elastic collection column must remain star-sized after both drags.");

                Assert.IsTrue(BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(BindingOperations.IsDataBound(inspectorColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(collectionColumn.Width.IsStar,
                    "Keyboard splitter adjustment must restore the elastic collection star column.");

                var thumbnailWidthBeforeKeyboard = state.ThumbnailWidth;
                Assert.IsTrue(thumbnailSlider.Focus(), "The attached thumbnail-size slider must accept keyboard focus.");
                RaiseKeyboardAdjustment(thumbnailSlider, Key.Right);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => state.ThumbnailWidth > thumbnailWidthBeforeKeyboard &&
                          Math.Abs(thumbnailSlider.Value - state.ThumbnailWidth) <= .01,
                    TimeSpan.FromSeconds(1)),
                    "WPF Right must increase the thumbnail size and write the new value back to workspace settings.");

                page.ViewModel.ToggleOrganizationPaneCommand.Execute(null);
                page.UpdateLayout();
                Assert.AreEqual(0d, organizationColumn.ActualWidth, .1);
                page.ViewModel.UpdateViewportWidth(1500);
                page.ViewModel.ToggleOrganizationPaneCommand.Execute(null);
                page.UpdateLayout();
                Assert.AreEqual(keyboardAdjustedOrganizationWidth, organizationColumn.ActualWidth, 1d);

                page.ViewModel.ToggleInspectorPaneCommand.Execute(null);
                page.UpdateLayout();
                Assert.AreEqual(0d, inspectorColumn.ActualWidth, .1);
                page.ViewModel.UpdateViewportWidth(1500);
                page.ViewModel.ToggleInspectorPaneCommand.Execute(null);
                page.UpdateLayout();
                Assert.AreEqual(retainedInspectorWidth, inspectorColumn.ActualWidth, 1d);
                Assert.IsTrue(BindingOperations.IsDataBound(organizationColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(BindingOperations.IsDataBound(inspectorColumn, ColumnDefinition.WidthProperty));
                Assert.IsTrue(collectionColumn.Width.IsStar);
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task MaximumPersistedPaneWidthsAndPinnedNarrowLayoutKeepTheCollectionInsideTheWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedPaneBounds", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var state = new RAWSelectionAssistant.Core.Models.AssetLibraryWorkspaceSettings
                {
                    OrganizationPaneWidth = 420,
                    InspectorPaneWidth = 520,
                    InspectorPinned = true
                };
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);

                foreach (var width in new[] { 1400d, 1280d, 900d, 820d })
                {
                    ArrangePage(page, width, 720);
                    var workspace = FindVisualByAutomationId<Grid>(page, "AssetLibraryThreePaneWorkspace");
                    var collection = FindVisualByAutomationId<Border>(page, "AssetCollectionPane");
                    var organization = FindVisualByAutomationId<Border>(page, "AssetOrganizationPane");
                    var inspector = FindVisualByAutomationId<Border>(page, "AssetInspectorPane");

                    Assert.IsGreaterThanOrEqualTo(360d, collection.ActualWidth,
                        $"The collection fell below its 360 DIP minimum at a {width} DIP viewport.");
                    AssertElementStaysInside(workspace, collection, width);
                    AssertElementStaysInside(workspace, organization, width);
                    AssertElementStaysInside(workspace, inspector, width);
                }

                Assert.AreEqual(420d, state.OrganizationPaneWidth,
                    "Responsive hiding or temporary fitting must not overwrite the persisted organization width.");
                Assert.AreEqual(520d, state.InspectorPaneWidth,
                    "Responsive hiding or temporary fitting must not overwrite the persisted inspector width.");
                Assert.IsTrue(state.InspectorPinned);
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task EmptyVisualResultClearConditionsReturnsToTheNormalCollection()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedClearAll", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var source = Path.Combine(root, "analyzed-reference.jpg");
                WriteSyntheticJpeg(source);
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    []);
                page.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                page.ViewModel.ImportDemoDirectoryAsync(root).GetAwaiter().GetResult();
                var asset = page.ViewModel.AssetCards.Single().Asset;
                page.ViewModel.ExecuteVisualContextActionAsync(asset, VisualContextAction.Analyze).GetAwaiter().GetResult();
                Assert.IsNotNull(page.ViewModel.SelectedFeatures);

                ArrangePage(page, 1280, 820);
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var previousContext = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                    page.ViewModel.VisualChipCommand.Execute("NotAnalyzed");
                    Assert.IsTrue(PumpDispatcherUntil(
                        () => page.ViewModel.VisualChipCommand.CanExecute("NotAnalyzed") &&
                              page.ViewModel.IsTemporaryVisualMode &&
                              !page.ViewModel.IsLoading &&
                              page.ViewModel.AssetCards.Count == 0,
                        TimeSpan.FromSeconds(10)),
                        "The deterministic visual filter did not reach an empty temporary result.");
                    page.UpdateLayout();
                    Assert.IsTrue(page.ViewModel.IsEmptyStateVisible);
                    Assert.AreEqual(Visibility.Visible,
                        FindVisualByAutomationId<Border>(page, "AssetLibraryEmptyState").Visibility);

                    var clearConditions = FindVisualChildren<Button>(page)
                        .Single(button => string.Equals(button.Content as string, "清除条件", StringComparison.Ordinal));
                    clearConditions.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.IsTrue(PumpDispatcherUntil(
                        () => !page.ViewModel.IsTemporaryVisualMode &&
                              !page.ViewModel.IsLoading &&
                              page.ViewModel.AssetCards.Count == 1,
                        TimeSpan.FromSeconds(10)),
                        "Clear conditions did not exit the temporary visual mode and restore the normal collection.");
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }

                Assert.HasCount(0, page.ViewModel.ActiveVisualChips);
                Assert.IsFalse(page.ViewModel.HasActiveQuery);
                Assert.AreEqual(asset.AssetId, page.ViewModel.AssetCards.Single().Asset.AssetId);
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    [DataRow(1366d, 768d, 1d)]
    [DataRow(1366d, 768d, 1.25d)]
    [DataRow(1366d, 768d, 1.5d)]
    [DataRow(1366d, 768d, 1.75d)]
    [DataRow(1920d, 1080d, 1d)]
    [DataRow(1920d, 1080d, 1.25d)]
    [DataRow(1920d, 1080d, 1.5d)]
    [DataRow(1920d, 1080d, 1.75d)]
    [DataRow(2560d, 1440d, 1d)]
    [DataRow(2560d, 1440d, 1.25d)]
    [DataRow(2560d, 1440d, 1.5d)]
    [DataRow(2560d, 1440d, 1.75d)]
    public async Task WorkspaceKeepsTheCollectionUsableAcrossRequiredDisplayScales(double physicalWidth, double physicalHeight, double scale)
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedDpi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var logicalShellWidth = physicalWidth / scale;
                var logicalShellHeight = physicalHeight / scale;
                var compactPrimaryNavigation = logicalShellWidth < 1100d;
                var navigationWidth = compactPrimaryNavigation ? 60d : 172d;
                var contentWidth = Math.Max(720d - navigationWidth, logicalShellWidth - navigationWidth);
                var contentHeight = Math.Max(400d, logicalShellHeight);
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    []);

                page.ViewModel.UpdateViewportWidth(contentWidth);
                page.Measure(new Size(contentWidth, contentHeight));
                page.Arrange(new Rect(0, 0, contentWidth, contentHeight));
                page.UpdateLayout();

                var workspace = FindVisualByAutomationId<Grid>(page, "AssetLibraryThreePaneWorkspace");
                var collection = FindVisualByAutomationId<Border>(page, "AssetCollectionPane");
                var search = FindVisualByAutomationId<TextBox>(page, "AssetLibrarySearch");
                Assert.AreEqual(contentWidth, workspace.ActualWidth, 1d);
                Assert.IsGreaterThanOrEqualTo(360d, collection.ActualWidth,
                    $"Collection became unusable at {physicalWidth}x{physicalHeight} / {scale:P0}.");
                Assert.IsGreaterThanOrEqualTo(120d, search.ActualWidth,
                    $"Search became unusable at {physicalWidth}x{physicalHeight} / {scale:P0}.");
                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task WorkspaceShowsLoadingThenFirstEmptyAndExplicitInitializationErrorStates()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedStates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var emptyPage = new AssetLibraryPage(
                    Path.Combine(root, "empty", "asset-library.db"),
                    new TaskOperationBridge(),
                    []);
                Assert.IsTrue(emptyPage.ViewModel.IsLoading);
                emptyPage.Measure(new Size(1280, 820));
                emptyPage.Arrange(new Rect(0, 0, 1280, 820));
                emptyPage.UpdateLayout();
                Assert.AreEqual(Visibility.Visible, FindVisualByAutomationId<Border>(emptyPage, "AssetLibraryLoadingState").Visibility);

                emptyPage.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                emptyPage.UpdateLayout();
                Assert.IsFalse(emptyPage.ViewModel.IsLoading);
                Assert.IsFalse(emptyPage.ViewModel.HasLoadError);
                Assert.IsTrue(emptyPage.ViewModel.IsEmptyStateVisible);
                Assert.AreEqual(Visibility.Visible, FindVisualByAutomationId<Border>(emptyPage, "AssetLibraryEmptyState").Visibility);
                emptyPage.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();

                var blockedParent = Path.Combine(root, "blocked-parent");
                File.WriteAllText(blockedParent, "not a directory");
                var errorPage = new AssetLibraryPage(
                    Path.Combine(blockedParent, "asset-library.db"),
                    new TaskOperationBridge(),
                    []);
                errorPage.ViewModel.InitializeAsync().GetAwaiter().GetResult();
                errorPage.Measure(new Size(1280, 820));
                errorPage.Arrange(new Rect(0, 0, 1280, 820));
                errorPage.UpdateLayout();
                Assert.IsFalse(errorPage.ViewModel.IsLoading);
                Assert.IsTrue(errorPage.ViewModel.HasLoadError);
                Assert.IsFalse(errorPage.ViewModel.IsEmptyStateVisible);
                Assert.AreEqual(Visibility.Visible, FindVisualByAutomationId<Border>(errorPage, "AssetLibraryErrorState").Visibility);
                Assert.IsNotNull(FindVisualByAutomationId<Button>(errorPage, "RetryAssetLibraryLoad").Command);
                errorPage.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task RecoverableErrorRetryIsReachableByForwardKeyboardTraversalWithoutStartingAttemptTwo()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedRetryFocus", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var controller = new RecoverableKeyboardTraversalLoadStateController();
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [],
                    loadStateController: controller);
                using var presentation = AttachToPresentationSource(page, 1280, 820);
                ArrangePage(page, 1280, 820);

                Assert.IsTrue(PumpDispatcherUntil(
                    () => page.ViewModel.HasLoadError &&
                          FindVisualByAutomationId<Border>(page, "AssetLibraryErrorState").Visibility == Visibility.Visible,
                    TimeSpan.FromSeconds(10)),
                    "The recoverable load-state seam did not produce the visible error surface.");

                var retry = FindVisualByAutomationId<Button>(page, "RetryAssetLibraryLoad");
                Assert.IsTrue(page.Focus(), "The attached Asset Library page root must accept the initial keyboard focus.");
                Assert.AreSame(page, Keyboard.FocusedElement);

                const int maximumForwardMoves = 12;
                var visitedAutomationIds = new List<string>();
                for (var move = 0; move < maximumForwardMoves && !ReferenceEquals(Keyboard.FocusedElement, retry); move++)
                {
                    var focused = Keyboard.FocusedElement as UIElement;
                    Assert.IsNotNull(focused, $"Keyboard focus left the WPF visual tree after: {string.Join(" -> ", visitedAutomationIds)}");
                    Assert.IsTrue(
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)),
                        $"Forward focus traversal stopped before Retry after: {string.Join(" -> ", visitedAutomationIds)}");
                    page.UpdateLayout();
                    var current = Keyboard.FocusedElement as DependencyObject;
                    visitedAutomationIds.Add(current is null
                        ? "<none>"
                        : AutomationProperties.GetAutomationId(current) is { Length: > 0 } automationId
                            ? automationId
                            : $"<{current.GetType().Name}>");
                }

                Assert.AreSame(retry, Keyboard.FocusedElement,
                    $"Retry was not reachable within {maximumForwardMoves} forward moves. Visited: {string.Join(" -> ", visitedAutomationIds)}");
                Assert.AreEqual("RetryAssetLibraryLoad", AutomationProperties.GetAutomationId(retry));
                Assert.AreEqual(1, page.ViewModel.LoadAttempt, "Moving focus must not execute Retry or start attempt 2.");
                CollectionAssert.AreEqual(new[] { 1 }, controller.InitialQueryAttempts.ToArray(),
                    "Moving focus must not invoke a second repository query.");
                Assert.IsTrue(page.ViewModel.HasLoadError);
                Assert.IsFalse(page.ViewModel.IsLoading);
                Assert.IsFalse(page.ViewModel.IsReady);

                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public async Task AcceptanceWorkspaceControlsAreReachableInNaturalForwardAndReverseTabOrderWithoutChangingState()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart-EmbeddedWorkspaceFocus", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await RunSta(() =>
            {
                var state = new RAWSelectionAssistant.Core.Models.AssetLibraryWorkspaceSettings
                {
                    OrganizationPaneWidth = 286,
                    InspectorPaneWidth = 414,
                    ThumbnailWidth = 196,
                    InspectorPinned = false
                };
                var page = new AssetLibraryPage(
                    Path.Combine(root, "asset-library.db"),
                    new TaskOperationBridge(),
                    [],
                    workspaceSettings: state);
                // HwndSource dimensions are device pixels; this environment runs at 150% DPI,
                // so 2400x1230 provides a 1600x820 DIP workspace where both panes fit.
                using var presentation = AttachToPresentationSource(page, 2400, 1230);
                ArrangePage(page, 1600, 820);
                Assert.IsTrue(PumpDispatcherUntil(
                    () => page.ViewModel.IsReady,
                    TimeSpan.FromSeconds(10)),
                    "The embedded page must finish its isolated load before keyboard traversal is measured.");
                ArrangePage(page, 1600, 820);
                // Keep the focus walk on the page's normal controls. State overlays are
                // independently covered by load-state tests and otherwise create a nested
                // focus scope that intentionally terminates WPF traversal.
                FindVisualByAutomationId<Border>(page, "AssetLibraryLoadingState").Visibility = Visibility.Collapsed;
                FindVisualByAutomationId<Border>(page, "AssetLibraryErrorState").Visibility = Visibility.Collapsed;
                FindVisualByAutomationId<Border>(page, "AssetLibraryEmptyState").Visibility = Visibility.Collapsed;
                page.UpdateLayout();

                Control[] targets =
                [
                    FindVisualByAutomationId<Button>(page, "ToggleAssetOrganizationPane"),
                    FindVisualByAutomationId<Button>(page, "ToggleAssetInspectorPane"),
                    FindVisualByAutomationId<GridSplitter>(page, "AssetOrganizationSplitter"),
                    FindVisualByAutomationId<Slider>(page, "AssetThumbnailSizeSlider"),
                    FindVisualByAutomationId<GridSplitter>(page, "AssetInspectorSplitter"),
                ];
                var expectedNames = new[]
                {
                    page.ViewModel.OrganizationPaneToggleLabel,
                    page.ViewModel.InspectorPaneToggleLabel,
                    "调整组织栏宽度",
                    "缩略图大小",
                    "调整检查器宽度",
                };
                for (var index = 0; index < targets.Length; index++)
                {
                    Assert.IsTrue(targets[index].IsEnabled,
                        $"{AutomationProperties.GetAutomationId(targets[index])} must remain enabled for keyboard acceptance; " +
                        $"organizationVisible={page.ViewModel.IsOrganizationPaneVisible}, inspectorVisible={page.ViewModel.IsInspectorPaneVisible}, " +
                        $"inspectorPinned={page.ViewModel.IsInspectorPinned}, viewport={page.ActualWidth:F1}.");
                    Assert.IsTrue(targets[index].Focusable, AutomationProperties.GetAutomationId(targets[index]));
                    Assert.IsTrue(targets[index].IsTabStop, AutomationProperties.GetAutomationId(targets[index]));
                    Assert.AreEqual(expectedNames[index], AutomationProperties.GetName(targets[index]),
                        AutomationProperties.GetAutomationId(targets[index]));
                }

                var forward = TraverseKeyboardFocus(page, FocusNavigationDirection.Next, maximumMoves: 96);
                AssertFocusOrder(forward, targets, "forward");

                var reverse = TraverseKeyboardFocus(targets[^1], FocusNavigationDirection.Previous, maximumMoves: 96);
                AssertFocusOrder(reverse, targets.Reverse().ToArray(), "reverse");

                Assert.AreEqual(286d, state.OrganizationPaneWidth, .01, "Tab traversal must not resize the organization pane.");
                Assert.AreEqual(414d, state.InspectorPaneWidth, .01, "Tab traversal must not resize the inspector pane.");
                Assert.AreEqual(196d, state.ThumbnailWidth, .01, "Tab traversal must not change thumbnail size.");
                Assert.IsFalse(state.OrganizationPaneCollapsed, "Tab traversal must not collapse the organization pane.");
                Assert.IsFalse(state.InspectorPaneCollapsed, "Tab traversal must not collapse the inspector pane.");

                page.ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
            });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void EmbeddedPageExposesForegroundAndDiagnosticsAutomationSeamsWithoutStandaloneProcess()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var page = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryViewModel.cs"));
        var app = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "App.xaml.cs"));
        var mainWindow = File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "MainWindow.xaml"));
        var thumbnail = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "AsyncThumbnail.cs"));
        var hexBrushConverter = File.ReadAllText(Path.Combine(root, "src", "PixelTart.Modules.AssetLibrary", "HexToBrushConverter.cs"));
        foreach (var id in new[]
        {
            "AssetLibraryPage", "AssetLibraryImport", "AssetLibraryThreePaneWorkspace", "AssetOrganizationPane",
            "AssetOrganizationSplitter", "AssetCollectionPane", "AssetInspectorSplitter", "AssetInspectorPane",
            "ToggleAssetOrganizationPane", "ToggleAssetInspectorPane", "PinAssetInspectorPane",
            "AssetLibraryLoadingState", "AssetLibraryEmptyState", "AssetLibraryErrorState", "RetryAssetLibraryLoad",
            "AssetGrid", "VisualFilterChips",
            "VisualPaletteTab", "VisualHistogramTab", "VisualToneTab", "SearchByColor", "FindSimilarPalette",
            "FindSimilarAssets", "VisualSmartFolderBuilder", "AnalyzeVisibleAssets", "ModuleDiagnostics"
        })
            StringAssert.Contains(xaml, $"AutomationProperties.AutomationId=\"{id}\"");
        Assert.DoesNotContain("AutomationProperties.AutomationId=\"ReturnToWorkbench\"", xaml, StringComparison.Ordinal);
        foreach (var token in new[] { "public void FocusSearch()", "AssetLibrarySearchBox.Focus()", "AssetLibrarySearchBox.SelectAll()" })
            StringAssert.Contains(page, token);
        foreach (var token in new[] { "AssetLibraryShellMinWidth = 720d", "AssetLibraryShellMinHeight = 400d", "ApplySurfaceMinimumSize(e.CurrentPage)" })
            StringAssert.Contains(File.ReadAllText(Path.Combine(root, "src", "RAWSelectionAssistant", "MainWindow.xaml.cs")), token);
        StringAssert.Contains(viewModel, "_taskOperationBridge.RunAsync(");
        Assert.DoesNotContain("Process.Start", page, StringComparison.Ordinal);
        Assert.DoesNotContain("new Window", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PIXEL_TART_ASSET_LIBRARY_DEMO_DIR", page, StringComparison.Ordinal);
        StringAssert.Contains(app, "#if MODULAR_HARNESS_DEV_PREVIEW");
        StringAssert.Contains(app, "Environment.GetEnvironmentVariable(\"PIXEL_TART_ASSET_LIBRARY_DEMO_DIR\")");
        StringAssert.Contains(app, "enableAssetLibraryPreview ? BuildModuleDiagnostics(registry) : []");
        StringAssert.Contains(viewModel, "if (_enablePreviewFeatures && Folders.Count == 0)");
        StringAssert.Contains(viewModel, "if (IsCurrentAnalysis(asset, generation)) { IsAnalyzing = false; RaiseVisualActions(); }");
        var assetHostStart = mainWindow.IndexOf("<views:ModuleWorkspaceHost x:Name=\"AssetLibraryWorkspace\"", StringComparison.Ordinal);
        var assetHostEnd = mainWindow.IndexOf("/>", assetHostStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, assetHostStart);
        Assert.IsGreaterThan(assetHostStart, assetHostEnd);
        var assetHost = mainWindow[assetHostStart..assetHostEnd];
        StringAssert.Contains(assetHost, "HorizontalContentAlignment=\"Stretch\"");
        StringAssert.Contains(assetHost, "VerticalContentAlignment=\"Stretch\"");
        StringAssert.Contains(thumbnail, "var cancellationToken = cancellation.Token;");
        Assert.DoesNotContain("async void OnSourceChanged", thumbnail, StringComparison.Ordinal);
        StringAssert.Contains(thumbnail, "dispatcher.InvokeAsync(");
        StringAssert.Contains(xaml, "<local:HexToBrushConverter x:Key=\"HexToBrushConverter\" />");
        StringAssert.Contains(xaml, "Background=\"{Binding Hex, Converter={StaticResource HexToBrushConverter}}\"");
        StringAssert.Contains(hexBrushConverter, "ColorConverter.ConvertFromString(hex)");
        StringAssert.Contains(hexBrushConverter, "new SolidColorBrush(color)");
        Assert.DoesNotContain("previous.Cancel(); previous.Dispose();", thumbnail, StringComparison.Ordinal);
        Assert.DoesNotContain("cancellation.Cancel(); cancellation.Dispose();", thumbnail, StringComparison.Ordinal);
    }

    private static void WriteSyntheticJpeg(string path, int colorOffset = 0, int width = 24, int height = 16)
    {
        var pixels = new byte[width * height * 3];
        for (var index = 0; index < width * height; index++)
        {
            pixels[index * 3] = (byte)(20 + (index + colorOffset) % width * 5);
            pixels[index * 3 + 1] = (byte)(100 + colorOffset);
            pixels[index * 3 + 2] = (byte)(180 - colorOffset);
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Rgb24, null, pixels, width * 3);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject root)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static T FindVisualByAutomationId<T>(DependencyObject root, string automationId)
        where T : FrameworkElement =>
        FindVisualChildren<T>(root).Single(element => AutomationProperties.GetAutomationId(element) == automationId);

    private static void ArrangePage(AssetLibraryPage page, double width, double height)
    {
        page.ViewModel.UpdateViewportWidth(width);
        page.Measure(new Size(width, height));
        page.Arrange(new Rect(0, 0, width, height));
        page.UpdateLayout();
    }

    private static HwndSource AttachToPresentationSource(FrameworkElement content, int width, int height)
    {
        var source = new HwndSource(new HwndSourceParameters("PixelTartAssetLibrarySplitterTest")
        {
            Width = width,
            Height = height,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = unchecked((int)0x80000000)
        });
        source.RootVisual = new AdornerDecorator { Child = content };
        return source;
    }

    private static void RaiseSplitterDrag(GridSplitter splitter, double horizontalChange)
    {
        Assert.IsTrue(splitter.ShowsPreview, "The regression must exercise the production preview-drag path.");
        splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        splitter.RaiseEvent(new DragDeltaEventArgs(horizontalChange, 0) { RoutedEvent = Thumb.DragDeltaEvent });
        splitter.RaiseEvent(new DragCompletedEventArgs(horizontalChange, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
    }

    private static void RaiseKeyboardAdjustment(UIElement control, Key key)
    {
        var inputSource = PresentationSource.FromVisual(control)
            ?? throw new InvalidOperationException("The keyboard target must be attached to a presentation source.");
        foreach (var routedEvent in new[]
                 {
                     Keyboard.PreviewKeyDownEvent,
                     Keyboard.KeyDownEvent,
                     Keyboard.PreviewKeyUpEvent,
                     Keyboard.KeyUpEvent,
                 })
            control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
            {
                RoutedEvent = routedEvent
            });
    }

    private static List<DependencyObject> TraverseKeyboardFocus(
        UIElement initial,
        FocusNavigationDirection direction,
        int maximumMoves)
    {
        Assert.IsTrue(initial.Focus(), $"{initial.GetType().Name} must accept initial keyboard focus.");
        var visited = new List<DependencyObject>();
        if (Keyboard.FocusedElement is DependencyObject first) visited.Add(first);

        for (var move = 0; move < maximumMoves; move++)
        {
            if (Keyboard.FocusedElement is not UIElement focused ||
                !focused.MoveFocus(new TraversalRequest(direction)) ||
                Keyboard.FocusedElement is not DependencyObject next ||
                visited.Any(item => ReferenceEquals(item, next)))
                break;
            visited.Add(next);
        }
        return visited;
    }

    private static void AssertFocusOrder(
        IReadOnlyList<DependencyObject> visited,
        IReadOnlyList<Control> expected,
        string direction)
    {
        var previousIndex = -1;
        foreach (var target in expected)
        {
            var index = -1;
            for (var candidate = 0; candidate < visited.Count; candidate++)
                if (ReferenceEquals(visited[candidate], target))
                {
                    index = candidate;
                    break;
                }
            Assert.IsGreaterThan(previousIndex, index,
                $"{direction} Tab traversal did not reach {AutomationProperties.GetAutomationId(target)} in order. " +
                $"Visited: {string.Join(" -> ", visited.Select(item => AutomationProperties.GetAutomationId(item) is { Length: > 0 } id ? id : $"<{item.GetType().Name}>"))}");
            previousIndex = index;
        }
    }

    private static void AssertElementStaysInside(FrameworkElement workspace, FrameworkElement element, double viewportWidth)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0) return;
        var origin = element.TranslatePoint(new Point(0, 0), workspace);
        Assert.IsGreaterThanOrEqualTo(-1d, origin.X,
            $"{AutomationProperties.GetAutomationId(element)} started outside a {viewportWidth} DIP workspace.");
        Assert.IsLessThanOrEqualTo(workspace.ActualWidth + 1d, origin.X + element.ActualWidth,
            $"{AutomationProperties.GetAutomationId(element)} overflowed a {viewportWidth} DIP workspace.");
    }

    private sealed class RecoverableKeyboardTraversalLoadStateController : IAssetLibraryLoadStateController
    {
        public bool DisablePreviewFixtures => true;
        public System.Collections.Concurrent.ConcurrentQueue<int> InitialQueryAttempts { get; } = new();

        public Task BeforeRepositoryInitializationAsync(int attempt, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RAWSelectionAssistant.Core.Models.AssetLibraryPage> ExecuteInitialQueryAsync(
            int attempt,
            Func<CancellationToken, Task<RAWSelectionAssistant.Core.Models.AssetLibraryPage>> realQuery,
            CancellationToken cancellationToken)
        {
            InitialQueryAttempts.Enqueue(attempt);
            if (attempt != 1) return realQuery(cancellationToken);

            var exception = new IOException("Known recoverable query failure for retry keyboard traversal.");
            exception.Data[AssetLibraryLoadStateExceptionMetadata.InjectionIdDataKey] = "asset-library-retry-keyboard-traversal-io-once/v1";
            return Task.FromException<RAWSelectionAssistant.Core.Models.AssetLibraryPage>(exception);
        }

        public void RecordState(AssetLibraryLoadStateSnapshot snapshot)
        {
        }
    }

    private static bool PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        if (condition()) return true;
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timedOut = false;
        var timer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(10),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) =>
            {
                if (!condition()) return;
                frame.Continue = false;
            },
            dispatcher);
        var timeoutTimer = new System.Windows.Threading.DispatcherTimer(
            timeout,
            System.Windows.Threading.DispatcherPriority.Send,
            (_, _) =>
            {
                timedOut = true;
                frame.Continue = false;
            },
            dispatcher);
        try
        {
            timer.Start();
            timeoutTimer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            return !timedOut && condition();
        }
        finally
        {
            timer.Stop();
            timeoutTimer.Stop();
        }
    }

    private static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

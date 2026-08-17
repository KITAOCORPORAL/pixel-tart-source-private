using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
            "AssetLibraryPage", "ReturnToWorkbench", "AssetLibraryImport", "AssetGrid", "VisualFilterChips",
            "VisualPaletteTab", "VisualHistogramTab", "VisualToneTab", "SearchByColor", "FindSimilarPalette",
            "FindSimilarAssets", "VisualSmartFolderBuilder", "AnalyzeVisibleAssets", "ModuleDiagnostics"
        })
            StringAssert.Contains(xaml, $"AutomationProperties.AutomationId=\"{id}\"");
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

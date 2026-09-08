using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.Tasks;
using AssetLibraryWpfPage = PixelTart.Modules.AssetLibrary.AssetLibraryPage;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryP35FocusAcceptanceTests
{
    [TestMethod]
    public async Task FocusedWorkspaceLayoutMatrix_CapturesFourScaledViewsWithImagePriority()
    {
        var root = Environment.GetEnvironmentVariable("PIXEL_TART_P35_ACCEPTANCE_ROOT") ?? Path.Combine(Path.GetTempPath(), "PixelTart-P35-Wpf", Guid.NewGuid().ToString("N"));
        var screenshots = Path.Combine(root, "screenshots"); Directory.CreateDirectory(screenshots);
        await RunSta(async () =>
        {
            var database = Path.Combine(root, "focus-fixture.db");
            await using (var repository = new SqliteAssetLibraryRepository(new AssetLibraryDatabase(database)))
            {
                await repository.InitializeAsync();
                for (var index = 0; index < 12; index++)
                {
                    var image = Path.Combine(root, $"synthetic-{index:00}.png");
                    WriteSyntheticPng(image, Color.FromRgb((byte)(25 + index * 13), (byte)(70 + index * 7), (byte)(155 - index * 5)));
                    await repository.ImportAsync([new AssetImportRequest(image, ComputeContentHash: true)]);
                }
            }
            var settings = new AssetLibraryWorkspaceSettings { InspectorPaneCollapsed = true, OrganizationPaneCollapsed = false };
            var page = new AssetLibraryWpfPage(database, new TaskOperationBridge(), [], workspaceSettings: settings, focusedChrome: true);
            try
            {
                await page.InitializeForSessionAsync();
                var matrix = new[] { (1366, 768, 100), (1920, 1080, 125), (1920, 1080, 150), (2560, 1440, 200) };
                foreach (var (pixelWidth, pixelHeight, scale) in matrix)
                {
                    foreach (var mode in Enum.GetValues<AssetLibraryViewMode>())
                    {
                        if (page.ViewModel.ViewMode != mode)
                        {
                            page.ViewModel.SwitchViewCommand.Execute(mode.ToString());
                            await Task.Delay(10);
                        }
                        var width = pixelWidth * 100d / scale;
                        var height = pixelHeight * 100d / scale;
                        page.Measure(new Size(width, height)); page.Arrange(new Rect(0, 0, width, height)); page.UpdateLayout();
                        var workspace = FindByAutomationId<FrameworkElement>(page, "AssetLibraryThreePaneWorkspace");
                        Assert.IsGreaterThan(height * 0.72, workspace.ActualHeight, $"{pixelWidth}x{pixelHeight}/{scale}%/{mode} central image workspace height");
                        Assert.IsGreaterThan(width * 0.50, FindByAutomationId<FrameworkElement>(page, "AssetCollectionPane").ActualWidth);
                        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height)), 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(page);
                        var path = Path.Combine(screenshots, $"focus-{pixelWidth}x{pixelHeight}-{scale}-{mode}.png");
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
                        Assert.IsGreaterThan(1024L, new FileInfo(path).Length);
                    }
                }
            }
            finally { await page.DisposeAsync(); }
        });
    }

    [TestMethod]
    public void FocusModeContract_PersistsCrashRecoveryAndKeepsAdvancedToolsOffThePrimaryRow()
    {
        var repo = FindRepositoryRoot();
        var window = File.ReadAllText(Path.Combine(repo, "src", "RAWSelectionAssistant", "MainWindow.xaml.cs"));
        var page = File.ReadAllText(Path.Combine(repo, "src", "PixelTart.Modules.AssetLibrary", "AssetLibraryPage.xaml"));
        var portable = File.ReadAllText(Path.Combine(repo, "src", "RAWSelectionAssistant.Core", "Models", "AssetLibraryPortableSettings.cs"));
        StringAssert.Contains(window, "WindowState = WindowState.Maximized");
        StringAssert.Contains(window, "SetAssetLibraryShellChrome(focused: true)");
        StringAssert.Contains(window, "RestoreAssetLibraryFocus");
        StringAssert.Contains(portable, "FocusRestorePending");
        Assert.DoesNotContain("<local:AssetSmartFolderEditorView Grid.Row=\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<local:AssetTagManagerView Grid.Row=\"", page, StringComparison.Ordinal);
        StringAssert.Contains(page, "Content=\"更多\"");
    }

    private static T FindByAutomationId<T>(DependencyObject root, string id) where T : DependencyObject
    {
        if (root is T match && System.Windows.Automation.AutomationProperties.GetAutomationId(root) == id) return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            try { return FindByAutomationId<T>(VisualTreeHelper.GetChild(root, index), id); } catch (InvalidOperationException) { }
        throw new InvalidOperationException($"Missing automation element: {id}");
    }

    private static void WriteSyntheticPng(string path, Color color)
    {
        const int width = 96, height = 72; var stride = width * 4; var pixels = new byte[stride * height];
        for (var index = 0; index < pixels.Length; index += 4) { pixels[index] = color.B; pixels[index + 1] = color.G; pixels[index + 2] = color.R; pixels[index + 3] = 255; }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
    }

    private static Task RunSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var operation = action();
                _ = operation.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                Dispatcher.Run(); operation.GetAwaiter().GetResult(); completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}

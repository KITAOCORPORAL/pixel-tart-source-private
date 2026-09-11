using System.IO;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PixelTart.Modules.AssetLibrary;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Tasks;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class AssetLibraryVisualPhase2Tests
{
    [TestMethod]
    public async Task ThemeImportBrowseAndResolutionMatrix()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            async Task Run()
            {
                // Load the actual application resources without invoking application startup/services.
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
                while (repositoryRoot is not null && !Directory.Exists(Path.Combine(repositoryRoot.FullName, "src")))
                    repositoryRoot = repositoryRoot.Parent;
                var sourceRoot = Path.Combine(repositoryRoot!.FullName, "src", "RAWSelectionAssistant");
                // Testhost owns ResourceAssembly. Expand the actual aggregate entries and load each
                // leaf from the product assembly so root-relative URIs retain production meaning.
                void LoadResources(string relative)
                {
                    var document = System.Xml.Linq.XDocument.Load(Path.Combine(sourceRoot, relative));
                    if (relative.EndsWith("App.xaml") || relative.EndsWith("PixelTart.Theme.xaml") || relative.EndsWith("PixelTart.Components.xaml"))
                    {
                        foreach (var source in document.Descendants().Attributes("Source"))
                            LoadResources(source.Value.TrimStart('/'));
                    }
                    else
                        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
                            new Uri("/KitaoPhotoSelector;component/" + relative, UriKind.Relative)));
                }
                LoadResources("App.xaml");
                var root = Path.Combine(Path.GetTempPath(), "PixelTart-VisualPhase2", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                var sources = Path.Combine(root, "sources");
                Directory.CreateDirectory(sources);
                var output = Environment.GetEnvironmentVariable("PIXEL_TART_PHASE2_EVIDENCE") ?? Path.Combine(root, "evidence");
                Directory.CreateDirectory(output);
                var fixture = Environment.GetEnvironmentVariable("PIXEL_TART_PHASE2_PHOTOS");
                if (fixture is not null)
                {
                    foreach (var path in Directory.EnumerateFiles(fixture).Where(p => Path.GetExtension(p).Equals(".jpg", StringComparison.OrdinalIgnoreCase)).Take(12))
                        File.Copy(path, Path.Combine(sources, Path.GetFileName(path)));
                }
                if (!Directory.EnumerateFiles(sources).Any())
                {
                    for (var index = 0; index < 12; index++)
                    {
                        var width = index % 2 == 0 ? 600 : 400;
                        var height = index % 2 == 0 ? 400 : 600;
                        var visual = new DrawingVisual();
                        using (var dc = visual.RenderOpen())
                        {
                            var background = new SolidColorBrush(Color.FromRgb(
                                (byte)(32 + index * 11),
                                (byte)(48 + index * 7),
                                (byte)(64 + index * 5)));
                            var subject = new SolidColorBrush(Color.FromRgb(
                                (byte)(224 - index * 5),
                                (byte)(184 - index * 3),
                                (byte)(96 + index * 9)));
                            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
                            dc.DrawEllipse(subject, null,
                                new Point(width * (.28 + index * .025), height * (.36 + index * .018)),
                                width * (.12 + index * .004), height * (.18 + index * .003));
                            dc.DrawRectangle(Brushes.Black, null,
                                new Rect(width * .58, height * (.18 + index * .012), width * .22, height * .56));
                        }
                        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(visual);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(sources, $"fixture-{index}.png"));
                        encoder.Save(stream);
                    }
                }
                var hashes = Directory.EnumerateFiles(sources).ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
                await using var page = new PixelTart.Modules.AssetLibrary.AssetLibraryPage(Path.Combine(root, "library.db"), new TaskOperationBridge(), [],
                    workspaceSettings: new AssetLibraryWorkspaceSettings { OrganizationPaneWidth = 208, InspectorPaneWidth = 320, InspectorPaneCollapsed = false },
                    focusedChrome: true);
                await page.InitializeForSessionAsync();
                await page.ViewModel.ImportDemoDirectoryAsync(sources);
                File.WriteAllText(Path.Combine(root, "debug.txt"), $"status={page.ViewModel.Status}; cards={page.ViewModel.AssetCards.Count}; hashes={hashes.Count}; sources={string.Join('|', Directory.EnumerateFiles(sources))}");
                Assert.HasCount(hashes.Count, page.ViewModel.AssetCards);
                var grid = (ListBox)page.FindName("AssetGrid");
                var records = new List<object>();
                foreach (var (pw, ph, scale) in new[] { (1920, 1080, 1d), (2560, 1440, 1d), (3840, 2160, 1d), (3840, 2160, 2d) })
                {
                    var width = pw / scale;
                    var height = ph / scale;
                    foreach (var mode in Enum.GetValues<AssetLibraryViewMode>())
                    {
                        page.ViewModel.SwitchViewCommand.Execute(mode.ToString());
                        await Task.Delay(40);
                        page.Measure(new Size(width, height));
                        page.Arrange(new Rect(0, 0, width, height));
                        page.UpdateLayout();
                        grid.SelectedIndex = 0;
                        await Task.Delay(100);
                        for (var wait = 0; wait < 100 && AsyncThumbnail.PendingRequestCount > 0; wait++) await Task.Delay(50);
                        page.UpdateLayout();
                        var images = Descendants<Image>(page).Where(i => AsyncThumbnail.GetSourcePath(i) is not null).ToArray();
                        Assert.IsTrue(images.Any(i => i.Source is not null), "No decoded thumbnail.");
                        Assert.IsTrue(images.All(i => i.Stretch == Stretch.Uniform), "Unexpected image crop.");
                        Assert.IsTrue(images.All(i => !AsyncThumbnail.GetHasFailure(i)), "Thumbnail failed.");
                        var collection = Descendants<FrameworkElement>(page).First(e => AutomationProperties.GetAutomationId(e) == "AssetCollectionPane");
                        Assert.IsGreaterThanOrEqualTo(.65, collection.ActualWidth / width, "Gallery width budget.");
                        var import = Descendants<Button>(page).First(e => AutomationProperties.GetAutomationId(e) == "AssetLibraryImport");
                        Assert.AreSame(app.FindResource("PixelTart.Button.Primary"), import.Style);
                        var screenshot = new RenderTargetBitmap(pw, ph, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                        screenshot.Render(page);
                        var filename = $"asset-library-{pw}x{ph}-{scale * 100:0}-{mode}.png";
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(screenshot));
                        using (var stream = File.Create(Path.Combine(output, filename))) encoder.Save(stream);
                        records.Add(new { filename, galleryWidthRatio = collection.ActualWidth / width, decoded = images.Count(i => i.Source is not null) });
                    }
                }
                foreach (var pair in hashes) CollectionAssert.AreEqual(pair.Value, SHA256.HashData(File.ReadAllBytes(pair.Key)));
                File.WriteAllText(Path.Combine(output, "results.json"), System.Text.Json.JsonSerializer.Serialize(records, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            var task = Run();
            _ = task.ContinueWith(_ => dispatcher.BeginInvokeShutdown(DispatcherPriority.Background));
            Dispatcher.Run();
            try { task.GetAwaiter().GetResult(); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}

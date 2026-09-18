using System.IO;
using System.Windows;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Utilities;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Projects;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
[DoNotParallelize]
public sealed class RealAppStartupIntegrationTests
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    // Run in a dedicated testhost: WPF permits one Application per AppDomain.
    // This executes App.OnStartup, not a replacement composition or shell mock.
    [TestMethod]
    public void ProductionCompositionAndToolCatalog_RealAppLoadedAndNavigated()
    {
        Exception? failure = null;
        var completed = false;
        var thread = new Thread(() =>
        {
            try
            {
                Require(Environment.GetEnvironmentVariable("PIXEL_TART_HUMAN_ACCEPTANCE") == "1",
                    "Run with an isolated data root, never user data.");
                var app = new App();
                app.InitializeComponent();
                var started = DateTime.UtcNow;
                var busy = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += async (_, _) =>
                {
                    if (busy) return;
                    if (DateTime.UtcNow - started > TimeSpan.FromSeconds(45))
                    {
                        failure = new TimeoutException("Production startup did not load MainWindow.");
                        timer.Stop(); app.Shutdown(-1); return;
                    }
                    if (app.MainWindow is not MainWindow { IsLoaded: true, IsVisible: true, DataContext: MainViewModel vm } window) return;
                    busy = true;
                    try
                    {
                        vm.ForceExitTutorial();
                        await Task.Delay(500);
                        Require(app.ModuleRegistry is not null, "Production modules missing");
                        Require(vm.ReferenceColorPage.Editor is not null, "Reference editor missing");
                        Require(vm.TetherPage is not null, "Tether missing");
                        Require(vm.PlanningPage is not null, "Planning missing");
                        var canvas = new FreeCanvasView(new CanvasEditor(new CanvasDocument()), new WpfAssetThumbnailProvider(),
                            new CanvasDocumentStore(Path.Combine(AppDataPaths.DataDirectory, "FreeCanvas")));
                        canvas.Measure(new Size(1000, 700)); canvas.Arrange(new Rect(0, 0, 1000, 700)); canvas.UpdateLayout();
                        for (var repeat = 0; repeat < 5; repeat++)
                            foreach (var kind in new ShotReferenceKind?[] { null, ShotReferenceKind.Lighting, ShotReferenceKind.Pose, ShotReferenceKind.Storyboard, ShotReferenceKind.Styling })
                            {
                                vm.PlanningPage!.ReferenceFilter = kind;
                                window.UpdateLayout();
                                Require(vm.PlanningPage.ReferencesView.Cast<ProjectShotReference>().All(reference => kind is null || reference.Kind == kind), "Planning filter did not apply");
                            }
                        var routes = new[] { "AssetLibrary", "Planning", "Tether" }
                            .Concat(ProductToolboxPolicy.ProductionCatalog.Select(tool => tool.TargetPageKey)).Append("Workbench");
                        foreach (var route in routes)
                        {
                            vm.NavigateCommand.Execute(route);
                            await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                            await Task.Delay(300);
                            Require(route == vm.CurrentPage, "Production tool must actually navigate: " + route + "; actual=" + vm.CurrentPage);
                            Require(window.IsLoaded && window.IsVisible, "Main window disappeared");
                        }
                        await Task.Delay(1000);
                        // Existing demo edits must survive subsequent launches/seeding.
                        var canvasPath = Path.Combine(AppDataPaths.DataDirectory, "FreeCanvas");
                        var savedCanvas = (await new CanvasDocumentStore(canvasPath).ListAsync()).First();
                        await new CanvasDocumentStore(canvasPath).SaveAsync(savedCanvas with { Name = "用户修改应保留" });
                        await RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.SeedAsync(new RAWSelectionAssistant.Core.Services.SettingsService(new RAWSelectionAssistant.Core.Services.FileLogService()));
                        Require((await new CanvasDocumentStore(canvasPath).ListAsync()).Any(item => item.Name == "用户修改应保留"), "Demo seeder overwrote user edits");
                        var logs = string.Join("\n", Directory.GetFiles(AppDataPaths.LogDirectory, "app-*.log").Select(File.ReadAllText));
                        Require(logs.Contains("STARTUP_OK", StringComparison.Ordinal), logs);
                        Require(!logs.Contains("[ERROR]", StringComparison.Ordinal), logs);
                        completed = true;
                    }
                    catch (Exception exception) { failure = exception; }
                    finally { timer.Stop(); window.Close(); }
                };
                timer.Start();
                app.Run();
            }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(75)), "Production startup/navigation/clean shutdown timed out.");
        if (failure is not null) throw new AssertFailedException("Production runtime failure: " + failure, failure);
        Assert.IsTrue(completed, "Full startup gate must complete.");
    }
}

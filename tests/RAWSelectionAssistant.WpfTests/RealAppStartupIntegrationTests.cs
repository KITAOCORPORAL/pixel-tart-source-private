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
                Require(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIXEL_TART_ACCEPTANCE_ROOT")),
                    "Explicit isolated root required for synthetic fixtures.");
                var database = new RAWSelectionAssistant.Core.Services.Database.PixelTartDatabase();
                var migration = new RAWSelectionAssistant.Core.Services.Database.DatabaseMigrator(database,
                    new RAWSelectionAssistant.Core.Services.Database.DatabaseBackupService(database, AppDataPaths.MigrationBackupDirectory)).MigrateAsync().GetAwaiter().GetResult();
                Require(migration.Success, "Synthetic fixture database initialization failed");
                RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.SeedAsync(
                    new RAWSelectionAssistant.Core.Services.SettingsService(new RAWSelectionAssistant.Core.Services.FileLogService())).GetAwaiter().GetResult();
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
                        vm.NavigateCommand.Execute("Planning");
                        await vm.PlanningPage!.ShowOverviewAsync();
                        Require(vm.PlanningPage.IsOverview && vm.PlanningPage.PlanningProjects.Count > 0, "Planning overview is empty");
                        var demoId = RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.ProjectId;
                        vm.NavigateCommand.Execute("WorkCalendar");
                        await ((RAWSelectionAssistant.Utilities.AsyncRelayCommand)vm.WorkCalendarPage.OpenFullPlanningCommand).ExecuteAsync(RAWSelectionAssistant.Services.PlanningHumanAcceptanceDemoSeeder.BookingId);
                        for (var wait = 0; wait < 50 && !vm.IsPlanningPage; wait++) await Task.Delay(100);
                        Require(vm.IsPlanningPage && vm.PlanningPage.ProjectId == demoId, "Calendar planning deep link failed");
                        await vm.OpenPlanningAsync(demoId);
                        Require(vm.IsPlanningPage && !vm.PlanningPage.IsOverview, "Project deep link failed");
                        var shotId = vm.PlanningPage.Shots.Last().ShotId;
                        await vm.OpenPlanningAsync(demoId, shotId: shotId);
                        Require(vm.PlanningPage.SelectedShot?.ShotId == shotId, "Shot deep link failed");
                        Require(vm.Settings.LastPlanningProjectId == demoId && vm.Settings.LastPlanningShotId == shotId, "Planning context memory failed");
                        await vm.PlanningPage.EnterTetherCommand.ExecuteAsync(null);
                        for (var wait = 0; wait < 50 && !vm.IsTetherPage; wait++) await Task.Delay(100);
                        Require(vm.IsTetherPage && vm.TetherPage!.ShotExecution.Current?.ShotId == shotId, "Planning to tether context lost");
                        await vm.ReturnToPlanningCommand.ExecuteAsync(null);
                        Require(vm.IsPlanningPage && vm.PlanningPage.SelectedShot?.ShotId == shotId, "Tether return context lost");
                        await vm.OpenProjectPlanningCommand.ExecuteAsync(vm.ProjectHistory.First(item => item.Id == demoId));
                        Require(vm.IsPlanningPage && vm.PlanningPage.ProjectId == demoId, "History project deep link failed");
                        window.WindowState = WindowState.Normal;
                        window.Width = 1920; window.Height = 1040;
                        await app.Dispatcher.InvokeAsync(window.UpdateLayout, DispatcherPriority.ApplicationIdle);
                        await Task.Delay(1000);
                        var nav = (System.Windows.Controls.ScrollViewer)window.FindName("SidebarNavigationScroll");
                        Require(nav.ScrollableHeight < 1, "1080p navigation needs scrolling");
                        var screenshotRoot = Environment.GetEnvironmentVariable("PIXEL_TART_NAVIGATION_EVIDENCE");
                        if (!string.IsNullOrWhiteSpace(screenshotRoot))
                        {
                            Directory.CreateDirectory(screenshotRoot);
                            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                            bitmap.Render(window);
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                            using var stream = File.Create(Path.Combine(screenshotRoot, "left-navigation-workflow.png"));
                            encoder.Save(stream);
                        }
                        await PlanningProposalAcceptance.RunAsync(window, vm);
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
                        var routes = new[] { "AssetLibrary", "Planning", "Tether", "OnlineSelection" }
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
                        await vm.OpenPlanningAsync(Guid.NewGuid());
                        Require(vm.IsPlanningPage && vm.PlanningPage.IsOverview, "Missing project must fall back to overview");
                        await vm.PlanningPage.OpenProjectSafelyAsync(demoId, shotId: Guid.NewGuid());
                        Require(vm.PlanningPage.SelectedShot is not null, "Missing shot must recover safely");
                        vm.PlanningPage.NewPlanningName = "导航验收新建策划";
                        await vm.PlanningPage.CreatePlanningCommand.ExecuteAsync(null);
                        Require(vm.PlanningPage.ProjectName == "导航验收新建策划" && !vm.PlanningPage.IsOverview, "New planning failed");
                        await vm.PlanningPage.ShowOverviewAsync();
                        vm.PlanningPage.ProjectSearch = "导航验收新建策划";
                        Require(vm.PlanningPage.PlanningProjectsView.Cast<PlanningProjectCard>().Count() == 1, "Overview search failed");
                        var corruptPath = Path.Combine(AppDataPaths.DataDirectory, "ProjectPlanning", $"{demoId:N}.planning.json");
                        var original = await File.ReadAllTextAsync(corruptPath);
                        try
                        {
                            await File.WriteAllTextAsync(corruptPath, "{invalid");
                            await vm.PlanningPage.OpenProjectSafelyAsync(demoId);
                            Require(vm.PlanningPage.IsOverview && await File.ReadAllTextAsync(corruptPath) == "{invalid", "Corrupt project must fall back without overwriting data");
                        }
                        finally { await File.WriteAllTextAsync(corruptPath, original); }
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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(180)), "Production startup/navigation/clean shutdown timed out.");
        if (failure is not null) throw new AssertFailedException("Production runtime failure: " + failure, failure);
        Assert.IsTrue(completed, "Full startup gate must complete.");
    }
}

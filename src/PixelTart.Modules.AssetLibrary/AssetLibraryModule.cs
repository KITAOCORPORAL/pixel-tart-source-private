using PixelTart.Kernel;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace PixelTart.Modules.AssetLibrary;

public sealed class AssetLibraryModule : PixelTartModuleBase
{
    public const string ModuleId = "pixel-tart.asset-library";
    public const string Route = "asset-library";
    public const string Version = "1.6.0-dev";

    private readonly Func<object> _viewFactory;

    public AssetLibraryModule()
        : this(static () => new AssetLibraryPage())
    {
    }

    public AssetLibraryModule(Func<object> viewFactory)
        : base(new PixelTartModuleManifest(
            ModuleId,
            "素材库",
            Version,
            ModuleType.WorkspaceModule,
            Route,
            "primary",
            20,
            ["asset.query", "asset.pick", "asset.import", "asset.folder", "asset.tag", "asset.smart-folder", "asset.visual-analysis", "asset.visual-search"],
            ["core.navigation", "core.task-center", "core.settings", "core.file-safety"],
            ["selection.create-from-assets"],
            []))
    {
        _viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
    }

    public override void RegisterCapabilities(ModuleRegistrationContext context)
    {
        foreach (var capability in Manifest.Provides)
            context.Capabilities.Register(new(capability, Manifest.ModuleId, "asset-library/v1"));
    }

    public override void RegisterProviders(ModuleRegistrationContext context) =>
        context.Providers.Register(new("visual-analysis.local-pixel", Manifest.ModuleId, "visual-analysis/v1", new LocalPixelVisualAnalysisProvider()));

    public override void RegisterRoutes(ModuleRegistrationContext context) =>
        context.Routes.Register(new(Route, "素材库", "primary", Manifest.NavigationOrder, Manifest.ModuleId, _viewFactory));

    public override void RegisterNavigation(ModuleRegistrationContext context)
    {
        if (!context.Routes.TryGet(Route, out var route)) throw new InvalidOperationException("Asset Library route must be registered before navigation.");
        context.Navigation.Register(route);
    }

    public override void RegisterTasks(ModuleRegistrationContext context)
    {
        foreach (var task in new[] { "asset.reference-import", "asset.thumbnail-generation", "asset.visual-analysis", "asset.relink" })
            context.Tasks.Register(new(task, Manifest.ModuleId, task));
    }

    public override void RegisterSettings(ModuleRegistrationContext context)
    {
        foreach (var setting in new[] { "asset-library.import-mode", "asset-library.thumbnail-cache", "asset-library.background-analysis", "asset-library.analysis-cache" })
            context.Settings.Register(new(setting, Manifest.ModuleId, setting));
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class LocalPixelVisualAnalysisProvider
{
    public string ProviderId => "visual-analysis.local-pixel";
    public string AnalysisVersion => AssetVisualFeatureContract.AnalysisVersion;
    public AssetVisualAnalysisResult Analyze(AssetVisualAnalysisRequest request, CancellationToken cancellationToken = default) =>
        VisualAnalysisEngine.Analyze(request, cancellationToken);
    public AssetVisualAnalysisService CreateCachedService(IAssetVisualAnalysisCache cache) => new(cache);
}

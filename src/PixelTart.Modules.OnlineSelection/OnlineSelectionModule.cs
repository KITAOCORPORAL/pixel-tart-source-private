using PixelTart.Kernel;

namespace PixelTart.Modules.OnlineSelection;

public sealed class OnlineSelectionModule : PixelTartModuleBase
{
    public const string ModuleId = "pixel-tart.online-selection";

    public OnlineSelectionModule()
        : base(new PixelTartModuleManifest(ModuleId, "Online Selection", "1.5.0-contract", ModuleType.WorkspaceModule, "online-selection", "workspace", 50, ["selection.create-from-assets"], ["core.navigation", "core.task-center"], ["asset.pick"], []))
    {
    }

    public override void RegisterRoutes(ModuleRegistrationContext context) =>
        context.Routes.Register(new("online-selection", "Online Selection", "workspace", 50, Manifest.ModuleId, static () => new OnlineSelectionPagePlaceholder(), false));
}

public sealed class OnlineSelectionPagePlaceholder
{
    public string Route => "online-selection";
}

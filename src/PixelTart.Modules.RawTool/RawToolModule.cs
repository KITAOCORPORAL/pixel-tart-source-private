using PixelTart.Kernel;

namespace PixelTart.Modules.RawTool;

public sealed class RawToolModule : PixelTartModuleBase
{
    public const string ModuleId = "pixel-tart.raw-to-jpeg";

    public RawToolModule()
        : base(new PixelTartModuleManifest(ModuleId, "RAW to JPG", "1.0.0", ModuleType.ToolModule, null, "toolbox", 40, ["raw.decode"], ["core.navigation", "core.task-center", "core.file-safety"], [], []))
    {
    }

    public override void RegisterCapabilities(ModuleRegistrationContext context) => context.Capabilities.Register(new("raw.decode", Manifest.ModuleId, "raw-decoder/v1"));

    public override void RegisterTasks(ModuleRegistrationContext context) => context.Tasks.Register(new("raw-to-jpeg", Manifest.ModuleId, "RAW to JPG"));
}

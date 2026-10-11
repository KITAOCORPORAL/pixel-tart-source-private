
namespace RAWSelectionAssistant.Core.Services.Projects;

public static class ProfessionalRawResourcePolicy
{
    public const long MasterBudgetBytes=512L*1024*1024;
    public const long PreviewBudgetBytes=64L*1024*1024;
    public const int MaximumTileWidth=4096;
    public const int MaximumTilePixels=4*1024*1024;
    // Native decoder/large encoder work across workspaces is serialized, not just within one export batch.
    public static SemaphoreSlim HeavyWork {get;}=new(1,1);
}

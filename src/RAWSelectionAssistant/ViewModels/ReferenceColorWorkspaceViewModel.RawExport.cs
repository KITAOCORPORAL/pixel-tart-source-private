using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private async Task ExportFrozenRawAsync(FrozenRawMaster master, string output, ReferenceLook? look,
        ColorAdjustmentStack? stack, PixelTartFilmSettings? film, ColorStudioMatchEngine engine,
        MatchV4ExecutionMode mode, CancellationToken token)
    {
        if (engine == ColorStudioMatchEngine.MatchV4Beta)
        {
            if (_matchV4Executor is null || look is null || !TetherReferenceModeViewModel.SupportsExperimentalV4Stack(stack, film))
                throw new NotSupportedException("V4 Beta 尚不支持此调整组合；使用稳定 V3 处理完整调整栈。");
            var effectiveLook = TetherReferenceModeViewModel.ResolveExperimentalV4Look(look, stack);
            if (effectiveLook is null) await _rawPipeline.ExportAsync(master, output, null, stack, token, film, RAWSelectionAssistant.Services.StudioQuickExport.SrgbProfileBytes());
            else await Editor.ExportRawV4Async(master, output, effectiveLook, token, mode);
        }
        else await _rawPipeline.ExportAsync(master, output, look, stack, token, film, RAWSelectionAssistant.Services.StudioQuickExport.SrgbProfileBytes());
    }
}

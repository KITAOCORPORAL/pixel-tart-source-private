using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private async Task<FrozenRawMaster> DecodeStudioRawAsync(string path, CancellationToken token)
    {
        var master = await _rawPipeline.DecodeFrozenMasterAsync(path, token);
        var upright = await Task.Run(() => RawImageOrientation.NormalizePixels(master.Image, token), token);
        return ReferenceEquals(upright, master.Image) ? master : new(master.SourcePath, master.SourceSha256, master.DecodeGenerationId, upright);
    }
}

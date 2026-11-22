using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.ViewModels;

public sealed partial class ReferenceColorWorkspaceViewModel
{
    private Task<FrozenRawMaster> DecodeStudioRawAsync(string path, CancellationToken token) =>
        _rawPipeline.DecodeUprightFrozenMasterAsync(path,token);
}

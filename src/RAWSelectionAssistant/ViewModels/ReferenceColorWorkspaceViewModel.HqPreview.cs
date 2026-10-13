using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Text.Json;

namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly Guid _hqOwner=Guid.NewGuid();
    private readonly Dictionary<string,HighBitDepthImageBuffer> _hqPreview=[];
    private readonly object _hqGate=new();
    private void ClearHqPreview(){lock(_hqGate)_hqPreview.Clear();StudioGlobalResources.Release(_hqOwner,"Detail");}
    private async Task<HighBitDepthImageBuffer> RenderProfessionalPreviewAsync(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int edge,CancellationToken token)
    {
        var key=JsonSerializer.Serialize(new{master.Contract.SourceSha256,master.Contract.DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=definition,Edge=edge,Quality="DisplayLinearBoxBandsV1"});
        lock(_hqGate)if(_hqPreview.TryGetValue(key,out var cached))return cached;
        using var retainedMaster=StudioGlobalResources.Pin(Targets.FirstOrDefault(x=>ReferenceEquals(x.LinearRawMaster,master))?.Id??Guid.Empty,"Master");
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();lock(_hqGate)if(_hqPreview.TryGetValue(key,out var cached))return cached;
            using var reservation=StudioGlobalResources.ReserveWork((long)master.Image.Rgb32.Length*4+128L*1024*1024);
            var rendered=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,edge,token),token);
            token.ThrowIfCancellationRequested();
            long retained;
            lock(_hqGate)
            {
                if(_hqPreview.Count>=2)_hqPreview.Clear();
                _hqPreview[key]=rendered;
                retained=_hqPreview.Values.Sum(x=>(long)x.Rgb32.Length*4);
            }
            var weak=new WeakReference<ReferenceColorWorkspaceViewModel>(this);
            StudioGlobalResources.Store(_hqOwner,"Detail",retained,()=>{if(weak.TryGetTarget(out var owner))owner.ClearHqPreview();});
            return rendered;
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }
}

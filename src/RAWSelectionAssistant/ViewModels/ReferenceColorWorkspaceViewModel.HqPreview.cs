using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using System.Text.Json;

namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    private readonly Guid _hqOwner=Guid.NewGuid();
    private readonly Dictionary<string,HighBitDepthImageBuffer> _hqPreview=[];
    private readonly object _hqGate=new();private long _hqEpoch;
    private static HighBitDepthImageBuffer OwnedPreviewClone(HighBitDepthImageBuffer source){var copy=source.Clone();try{copy.TakeReservation(StudioGlobalResources.ReserveWork((long)copy.Rgb32.Length*4));return copy;}catch{copy.Dispose();throw;}}
    private void ClearHqPreview(){lock(_hqGate){_hqEpoch++;foreach(var image in _hqPreview.Values)image.Dispose();_hqPreview.Clear();}StudioGlobalResources.Release(_hqOwner,"Detail");}
    private async Task<HighBitDepthImageBuffer> RenderProfessionalPreviewAsync(ProfessionalRawMaster master,ProfessionalRawDefinition definition,int edge,CancellationToken token,string role="EffectPreview")
    {
        using var measurement=StudioPreviewDiagnostics.Measure(role);
        if(Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition))
        {
            await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
            try{return await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,edge,token),token);}
            finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
        }
        var key=JsonSerializer.Serialize(new{master.Contract.SourceSha256,master.Contract.DecodeKey,master.Image.Width,master.Image.Height,master.Image.Orientation,Definition=definition,Edge=edge,Quality="DisplayLinearBoxBandsV1"});
        long epoch;lock(_hqGate){epoch=_hqEpoch;if(_hqPreview.TryGetValue(key,out var cached))return OwnedPreviewClone(cached);}
        using var retainedMaster=StudioGlobalResources.Pin(Targets.FirstOrDefault(x=>ReferenceEquals(x.LinearRawMaster,master))?.MasterResourceOwner??Guid.Empty,"Master");
        await ProfessionalRawResourcePolicy.HeavyWork.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();lock(_hqGate)if(_hqPreview.TryGetValue(key,out var cached))return OwnedPreviewClone(cached);
            using var reservation=StudioGlobalResources.ReserveWork((long)master.Image.Rgb32.Length*4+128L*1024*1024);
            var rendered=await Task.Run(()=>ProfessionalRawPipeline.RenderPreview(master,definition,edge,token),token);
            if(token.IsCancellationRequested){rendered.Dispose();token.ThrowIfCancellationRequested();}
            long retained;HighBitDepthImageBuffer ownedResult;
            lock(_hqGate)
            {
                if(_disposed||epoch!=_hqEpoch||token.IsCancellationRequested){rendered.Dispose();throw new OperationCanceledException("Preview context retired",token);}
                if(_hqPreview.Count>=4||_hqPreview.Values.Sum(x=>(long)x.Rgb32.Length*4)+(long)rendered.Rgb32.Length*4>96L*1024*1024){foreach(var image in _hqPreview.Values)image.Dispose();_hqPreview.Clear();}
                _hqPreview[key]=rendered;
                ownedResult=OwnedPreviewClone(rendered);
                retained=_hqPreview.Values.Where(x=>!Kitao.InstantFilm.Research.InstantV3ProductPipeline.IsV3(definition)).Sum(x=>(long)x.Rgb32.Length*4);
            }
            var weak=new WeakReference<ReferenceColorWorkspaceViewModel>(this);
            StudioGlobalResources.Store(_hqOwner,"Detail",retained,()=>{if(weak.TryGetTarget(out var owner))owner.ClearHqPreview();});
            return ownedResult;
        }
        finally{ProfessionalRawResourcePolicy.HeavyWork.Release();}
    }
}

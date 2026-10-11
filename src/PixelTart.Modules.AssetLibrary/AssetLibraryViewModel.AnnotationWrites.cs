using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
namespace PixelTart.Modules.AssetLibrary;
public sealed partial class AssetLibraryViewModel
{
    public IReadOnlyList<AnnotationBatchItemResult> LastAnnotationResults { get; private set; }=[];
    private async Task ApplyUserAnnotationsAsync(IReadOnlyList<AssetItem> items,int? rating=null,string? color=null,string? note=null)
    {
        var expected=items.Select(a=>new PhotographerAnnotation(a.AssetId,a.Rating,a.ColorLabel,a.Comment,a.AnnotationRevision)).ToArray();
        LastAnnotationResults=await new PhotographerAnnotationStore(_database).ApplyBatchAsync(expected,rating,color,note,_lifetimeCancellation.Token);
        await RefreshAsync();
        Status=$"标注：成功 {LastAnnotationResults.Count(x=>x.Status=="Success")}，冲突 {LastAnnotationResults.Count(x=>x.Status=="Conflict")}，失败 {LastAnnotationResults.Count(x=>x.Status=="Failed")}；已刷新权威状态";
        OnPropertyChanged(nameof(LastAnnotationResults));
    }
}

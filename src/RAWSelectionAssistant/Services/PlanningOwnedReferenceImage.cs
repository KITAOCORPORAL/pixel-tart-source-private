using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
namespace RAWSelectionAssistant.Services;
/// <summary>Compatibility facade. Planning and canvas share the unchanged company bounded decoder.</summary>
public static class PlanningOwnedReferenceImage
{
    public const int MaximumSide=OwnedPlanningReferenceImage.MaximumSide;
    public const long MaximumPixels=OwnedPlanningReferenceImage.MaximumPixels;
    public const int ThumbnailEdge=OwnedPlanningReferenceImage.ThumbnailEdge,SummaryEdge=OwnedPlanningReferenceImage.SummaryEdge,LargeViewEdge=OwnedPlanningReferenceImage.LargeViewEdge;
    public static BitmapSource Read(PlanningProjectCopy copy,Guid project,Guid document,Guid reference)=>OwnedPlanningReferenceImage.Read(copy,project,document,reference);
    public static BitmapSource ReadBounded(PlanningProjectCopy copy,Guid project,Guid document,Guid reference,int maximumEdge,CancellationToken token=default)=>OwnedPlanningReferenceImage.ReadBounded(copy,project,document,reference,maximumEdge,token);
    public sealed record Thumbnail(string Path,BitmapSource Image,bool CacheHit);
    public static Thumbnail ReadThumbnail(PlanningProjectCopy copy,Guid project,Guid document,Guid reference,string cacheRoot,CancellationToken token=default)
    {var value=OwnedPlanningReferenceImage.ReadThumbnail(copy,project,document,reference,cacheRoot,token);return new(value.Path,value.Image,value.CacheHit);}
    public static BitmapSource Decode(byte[] data,string extension)=>OwnedPlanningReferenceImage.Decode(data,extension);
}

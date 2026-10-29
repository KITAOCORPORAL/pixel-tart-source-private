using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class ReferenceColorWorkspaceViewModel
{
    public IReadOnlyDictionary<string,BitmapSource> FramePhotoPreviews {get;private set;}=new Dictionary<string,BitmapSource>();
    private CancellationTokenSource? framePhotoCancellation;private long framePhotoRevision;
    internal Task FramePhotoPreviewWork{get;private set;}=Task.CompletedTask;
    internal void QueueFramePhotoPreviews()
    {
        framePhotoCancellation?.Cancel();var revision=++framePhotoRevision;var target=ActiveTarget;var version=target?.ActiveEditVersionId;var source=Editor.MatchedImage??Editor.SourceImage;
        FramePhotoPreviews=new Dictionary<string,BitmapSource>();OnPropertyChanged(nameof(FramePhotoPreviews));
        if(_disposed||target is null||source is null||IsLoading||IsExporting||IsInstantGenerating)return;
        var cts=new CancellationTokenSource();framePhotoCancellation=cts;FramePhotoPreviewWork=Build();
        async Task Build()
        {
            try
            {
                await Task.Delay(180,cts.Token);var small=new TransformedBitmap(source,new System.Windows.Media.ScaleTransform(Math.Min(1,144d/Math.Max(source.PixelWidth,source.PixelHeight)),Math.Min(1,144d/Math.Max(source.PixelWidth,source.PixelHeight))));small.Freeze();
                // A visible V3 result is already coloured; extract its actual photo window and
                // render only the frame component around it (Amount0), never duplicate the old tone.
                if(_lightPrintBeforeDraft is not null||IsLightPrintLocked)
                {
                    var frozen=IsLightPrintLocked?target.RawDefinition?.InstantFilm?.V3:ApplyLightPrintFrame(BuildInstantDefinition(target,target.RawDefinition,version,false).InstantFilm!).V3;
                    if(frozen is not null)
                    {
                        double x0,y0,x1,y1;if(frozen.FrameArtwork is{} art){x0=art.X0;y0=art.Y0;x1=art.X1;y1=art.Y1;}
                        else{var g=frozen.Geometry;double w=1+2*g.Side,h=1/g.WindowAspect+g.Top+g.Bottom;x0=g.Side/w;x1=(g.Side+1)/w;y0=g.Top/h;y1=(g.Top+1/g.WindowAspect)/h;}
                        var crop=new System.Windows.Int32Rect((int)(small.PixelWidth*x0),(int)(small.PixelHeight*y0),Math.Max(1,(int)(small.PixelWidth*(x1-x0))),Math.Max(1,(int)(small.PixelHeight*(y1-y0))));var picture=new CroppedBitmap(small,crop);picture.Freeze();small=new TransformedBitmap(picture,System.Windows.Media.Transform.Identity);small.Freeze();
                    }
                }
                var pixels=BitmapPixels(small);using var owner=pixels;using var image=Kitao.InstantFilm.Research.CandidateImageLease.Adopt(pixels);var result=new Dictionary<string,BitmapSource>();
                foreach(var frame in LightPrintFrames.Available)
                {
                    cts.Token.ThrowIfCancellationRequested();var art=frame.Id.StartsWith("orig.",StringComparison.Ordinal)?OriginalFrameLibrary.Find(frame.Id):null;var aspect=art is null?1:(art.X1-art.X0)*art.Width/((art.Y1-art.Y0)*art.Height);
                    var r=Kitao.InstantFilm.Research.CandidateRecipe.Create("modern-itype-v3") with{Amount=0,FrameArtwork=art,Geometry=new(WindowAspect:aspect),Optics=new(DetailMix:0,HighlightDiffusion:0),Emulsion=new(Microstructure:0,DevelopmentUnevenness:0),Surface=new(Frame:true,BaseColor:frame.Color,PaperTexture:0),Paper=new(Version:3,Pattern:frame.Pattern,Window:frame.Window)};
                    using var session=new Kitao.InstantFilm.Research.CandidateRenderSession(image,r);using var output=await Task.Run(()=>session.RenderPreview(100,cts.Token),cts.Token);using var borrow=output.Borrow();result[frame.Id]=RawDisplayBitmapAdapter.ToBitmap(borrow.Image.ToVisualRgb24(cts.Token));
                }
                if(_disposed||cts.IsCancellationRequested||revision!=framePhotoRevision||!ReferenceEquals(target,ActiveTarget)||version!=target.ActiveEditVersionId)return;
                FramePhotoPreviews=result;OnPropertyChanged(nameof(FramePhotoPreviews));
            }
            catch(OperationCanceledException){}
            catch(Exception error)when(error is IOException or InvalidOperationException or ArgumentException or NotSupportedException){if(revision==framePhotoRevision)LightPrintStatus="相框缩略未完成："+error.Message;}
            finally{if(ReferenceEquals(framePhotoCancellation,cts))framePhotoCancellation=null;cts.Dispose();}
        }
    }
    internal void CancelFramePhotoPreviews(){framePhotoRevision++;var c=framePhotoCancellation;framePhotoCancellation=null;c?.Cancel();}
}

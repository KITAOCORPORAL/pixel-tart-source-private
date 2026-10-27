using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
/// <summary>Bounded real selected-image/node thumbnail; no extra full-size source decode.</summary>
public sealed class StudioLayerThumbnail:FrameworkElement
{
 public static readonly DependencyProperty NodeProperty=DependencyProperty.Register(nameof(Node),typeof(ColorAdjustmentStackNode),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(null,Changed));
 public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(BitmapSource),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(null,Changed));
 public static readonly DependencyProperty MaskProperty=DependencyProperty.Register(nameof(Mask),typeof(bool),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(false,Changed));
 public ColorAdjustmentStackNode? Node{get=>(ColorAdjustmentStackNode?)GetValue(NodeProperty);set=>SetValue(NodeProperty,value);}public BitmapSource? Source{get=>(BitmapSource?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}public bool Mask{get=>(bool)GetValue(MaskProperty);set=>SetValue(MaskProperty,value);}
 internal BitmapSource? RenderedImage=>image;
 public StudioLayerThumbnail(){Unloaded+=(_,_)=>queue?.Invalidate();}
 private BitmapSource? image;private LatestPreviewQueue<ThumbSnapshot,BitmapSource?>? queue;
 private sealed record ThumbSnapshot(ColorAdjustmentStackNode Node,BitmapSource? Source,bool Mask);
 private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)=>((StudioLayerThumbnail)o).Refresh();
 private void Refresh(){var node=Node?.Normalize();if(node is null){image=null;InvalidateVisual();return;}queue??=new LatestPreviewQueue<ThumbSnapshot,BitmapSource?>(async(s,token)=>await Task.Run(()=>{
  if(s.Mask){var aspect=s.Source is null?1:(double)s.Source.PixelWidth/s.Source.PixelHeight;var sample=s.Node.LocalMask?.Compile(aspect,token);var data=new byte[48*48];for(int y=0;y<48;y++){token.ThrowIfCancellationRequested();for(int x=0;x<48;x++)data[y*48+x]=(byte)Math.Round((sample?.Invoke((x+.5)/48,(y+.5)/48)??1)*255);}var b=BitmapSource.Create(48,48,96,96,PixelFormats.Gray8,null,data,48);b.Freeze();return b;}
  if(s.Source is null)return null;var scale=Math.Min(1,64d/Math.Max(s.Source.PixelWidth,s.Source.PixelHeight));var small=new TransformedBitmap(s.Source,new ScaleTransform(scale,scale));small.Freeze();return ProfessionalRawLocalRenderer.Supports(s.Node.Type)?ColorStudioBitmapRenderer.Render(small,new([s.Node],ProcessingVersion:2),null,token):small;
 },token),(_,s,result)=>{if(Node?.Id!=s.Node.Id)return;image=result;InvalidateVisual();});queue.Submit(new(node,Source,Mask));}
 protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.DimGray,null,new Rect(RenderSize));if(image is not null)dc.DrawImage(image,new Rect(RenderSize));}
}

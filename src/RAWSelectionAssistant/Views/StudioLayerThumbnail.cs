using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
/// <summary>Displays shared low-resolution stack-prefix output, not a single node applied to final RAW.</summary>
public sealed class StudioLayerThumbnail:FrameworkElement
{
 public static readonly DependencyProperty NodeProperty=DependencyProperty.Register(nameof(Node),typeof(ColorAdjustmentStackNode),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(null,Changed));
 public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(BitmapSource),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(null,Changed));
 public static readonly DependencyProperty ContextProperty=DependencyProperty.Register(nameof(Context),typeof(StudioLayerThumbnailContext),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(null,Changed));
 public static readonly DependencyProperty MaskProperty=DependencyProperty.Register(nameof(Mask),typeof(bool),typeof(StudioLayerThumbnail),new FrameworkPropertyMetadata(false,Changed));
 public ColorAdjustmentStackNode? Node{get=>(ColorAdjustmentStackNode?)GetValue(NodeProperty);set=>SetValue(NodeProperty,value);}
 public BitmapSource? Source{get=>(BitmapSource?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}
 public StudioLayerThumbnailContext? Context{get=>(StudioLayerThumbnailContext?)GetValue(ContextProperty);set=>SetValue(ContextProperty,value);}
 public bool Mask{get=>(bool)GetValue(MaskProperty);set=>SetValue(MaskProperty,value);}
 internal BitmapSource? RenderedImage=>image;private BitmapSource? image;
 public StudioLayerThumbnail(){Unloaded+=(_,_)=>{image=null;InvalidateVisual();};Loaded+=(_,_)=>Refresh();}
 private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)=>((StudioLayerThumbnail)o).Refresh();
 private void Refresh(){image=null;if(Node is{} node&&Context is{} context)(Mask?context.Masks:context.Outputs).TryGetValue(node.Id,out image);InvalidateVisual();}
 protected override void OnRender(DrawingContext dc){dc.DrawRectangle(Brushes.DimGray,null,new Rect(RenderSize));if(image is null)return;double scale=Math.Min(ActualWidth/image.PixelWidth,ActualHeight/image.PixelHeight);double w=image.PixelWidth*scale,h=image.PixelHeight*scale;dc.DrawImage(image,new Rect((ActualWidth-w)/2,(ActualHeight-h)/2,w,h));}
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RAWSelectionAssistant.Views;
/// <summary>Bounded analysis of the currently displayed real frame; diagnostic display-sRGB scopes.</summary>
public sealed class StudioScopeView:FrameworkElement
{
 private BitmapSource? cachedSource;private byte[]? cachedPixels;private int cachedWidth,cachedHeight;
 public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(BitmapSource),typeof(StudioScopeView),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty ModeProperty=DependencyProperty.Register(nameof(Mode),typeof(string),typeof(StudioScopeView),new FrameworkPropertyMetadata("波形",FrameworkPropertyMetadataOptions.AffectsRender));
 public BitmapSource? Source{get=>(BitmapSource?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}public string Mode{get=>(string)GetValue(ModeProperty);set=>SetValue(ModeProperty,value);}
 protected override void OnRender(DrawingContext dc){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18,18,20)),null,new Rect(RenderSize));if(Source is not{} source||ActualWidth<1||ActualHeight<1)return;if(!ReferenceEquals(cachedSource,source)){var scale=Math.Min(1,256d/Math.Max(source.PixelWidth,source.PixelHeight));var small=new TransformedBitmap(source,new ScaleTransform(scale,scale));var rgb=new FormatConvertedBitmap(small,PixelFormats.Rgb24,null,0);cachedWidth=rgb.PixelWidth;cachedHeight=rgb.PixelHeight;cachedPixels=new byte[cachedWidth*cachedHeight*3];rgb.CopyPixels(cachedPixels,cachedWidth*3,0);cachedSource=source;}var data=cachedPixels!;var pen=new Pen(new SolidColorBrush(Color.FromArgb(55,110,195,170)),1);var channels=new Brush[]{new SolidColorBrush(Color.FromArgb(65,255,80,80)),new SolidColorBrush(Color.FromArgb(65,80,255,80)),new SolidColorBrush(Color.FromArgb(65,80,130,255))};
  for(int y=0;y<cachedHeight;y+=2)for(int x=0;x<cachedWidth;x+=2){int i=(y*cachedWidth+x)*3;double r=data[i]/255d,g=data[i+1]/255d,b=data[i+2]/255d;if(Mode=="矢量"){double u=-.147*r-.289*g+.436*b,v=.615*r-.515*g-.100*b;dc.DrawEllipse(pen.Brush,null,new Point(ActualWidth*(.5+u),ActualHeight*(.5-v)),.6,.6);}else if(Mode=="RGB分量"){for(int c=0;c<3;c++){double value=c==0?r:c==1?g:b;dc.DrawEllipse(channels[c],null,new Point((c+x/(double)cachedWidth)*ActualWidth/3,(1-value)*ActualHeight),.5,.5);}}else{double l=.2126*r+.7152*g+.0722*b;dc.DrawEllipse(pen.Brush,null,new Point(x/(double)cachedWidth*ActualWidth,(1-l)*ActualHeight),.5,.5);}}
 }
}

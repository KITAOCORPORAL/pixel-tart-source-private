using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RAWSelectionAssistant.Views;
/// <summary>Bounded, cancellable analysis of a frozen displayed bitmap; stale generations never publish.</summary>
public sealed class StudioScopeView:FrameworkElement
{
 private CancellationTokenSource? cancellation;private long generation;private Task previousWork=Task.CompletedTask;private Point[] points=[];private byte[] channels=[];internal int PointCount=>points.Length;internal bool HasPendingAnalysis=>cancellation is not null&&!AnalysisWork.IsCompleted;
 public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(BitmapSource),typeof(StudioScopeView),new FrameworkPropertyMetadata(null,OnInputChanged));
 public static readonly DependencyProperty ModeProperty=DependencyProperty.Register(nameof(Mode),typeof(string),typeof(StudioScopeView),new FrameworkPropertyMetadata("波形",OnInputChanged));
 public BitmapSource? Source{get=>(BitmapSource?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}public string Mode{get=>(string)GetValue(ModeProperty);set=>SetValue(ModeProperty,value);}
 internal Task AnalysisWork{get;private set;}=Task.CompletedTask;
 public StudioScopeView(){Loaded+=(_,_)=>Queue();Unloaded+=(_,_)=>Cancel();IsVisibleChanged+=(_,_)=>{if(IsVisible)Queue();else Cancel();};}
 private static void OnInputChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)=>((StudioScopeView)d).Queue();
 private void Cancel(){Interlocked.Increment(ref generation);var c=cancellation;cancellation=null;c?.Cancel();c?.Dispose();points=[];channels=[];InvalidateVisual();}
 private void Queue()
 {
  Cancel();if(!IsVisible||Source is not{} image)return;
  var source=image.IsFrozen?image:image.CloneCurrentValue();if(!source.IsFrozen)source.Freeze();var mode=Mode;var id=Volatile.Read(ref generation);var c=new CancellationTokenSource();cancellation=c;
  var previous=previousWork;AnalysisWork=AnalyzeAsync(previous,source,mode,id,c.Token);previousWork=AnalysisWork;
 }
 private async Task AnalyzeAsync(Task previous,BitmapSource source,string mode,long id,CancellationToken token)
 {
  try{
   await previous;token.ThrowIfCancellationRequested();
   var result=await Task.Run(()=>Analyze(source,mode,token),token);
   if(token.IsCancellationRequested||id!=Volatile.Read(ref generation)||!IsVisible)return;
   points=result.Points;channels=result.Channels;InvalidateVisual();
  }catch(OperationCanceledException)when(token.IsCancellationRequested){}
  catch(Exception error)when(error is InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException){if(id==Volatile.Read(ref generation))ToolTip="示波分析未完成："+error.Message;}
 }
 private static (Point[] Points,byte[] Channels) Analyze(BitmapSource source,string mode,CancellationToken token)
 {
  var scale=Math.Min(1,256d/Math.Max(source.PixelWidth,source.PixelHeight));var small=new TransformedBitmap(source,new ScaleTransform(scale,scale));var rgb=new FormatConvertedBitmap(small,PixelFormats.Rgb24,null,0);int w=rgb.PixelWidth,h=rgb.PixelHeight;var data=new byte[w*h*3];rgb.CopyPixels(data,w*3,0);var result=new List<Point>();var c=new List<byte>();
  for(int y=0;y<h;y+=2){token.ThrowIfCancellationRequested();for(int x=0;x<w;x+=2){int i=(y*w+x)*3;double r=data[i]/255d,g=data[i+1]/255d,b=data[i+2]/255d;if(mode=="矢量"){double u=-.147*r-.289*g+.436*b,v=.615*r-.515*g-.100*b;result.Add(new(.5+u,.5-v));c.Add(3);}else if(mode=="RGB分量"){for(byte ch=0;ch<3;ch++){double value=ch==0?r:ch==1?g:b;result.Add(new((ch+x/(double)w)/3,1-value));c.Add(ch);}}else{result.Add(new(x/(double)w,1-(.2126*r+.7152*g+.0722*b)));c.Add(3);}}}
  return(result.ToArray(),c.ToArray());
 }
 protected override void OnRender(DrawingContext dc)
 {
  dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18,18,20)),null,new Rect(RenderSize));var brushes=new Brush[]{new SolidColorBrush(Color.FromArgb(65,255,80,80)),new SolidColorBrush(Color.FromArgb(65,80,255,80)),new SolidColorBrush(Color.FromArgb(65,80,130,255)),new SolidColorBrush(Color.FromArgb(55,110,195,170))};
  for(int i=0;i<points.Length;i++)dc.DrawEllipse(brushes[channels[i]],null,new Point(points[i].X*ActualWidth,points[i].Y*ActualHeight),.6,.6);
 }
}

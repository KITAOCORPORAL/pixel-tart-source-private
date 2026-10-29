using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.Views;
/// <summary>One presentation host for the existing R53CompareFrames, variants and stills alike.</summary>
public sealed class StudioSharedComparisonView:DockPanel
{
 private readonly Grid grid=new();private readonly List<ColorStudioImageViewport> views=[];private readonly ComboBox mode=new(){ItemsSource=new[]{"同步并排","划像"},SelectedIndex=0,Width=100};private readonly Slider split=new(){Minimum=0,Maximum=1,Value=.5,Width=160};private IReadOnlyList<R53VariantFrame> frames=[];private bool syncing;private Point? pan;
 public IReadOnlyList<ColorStudioImageViewport> Viewports=>views;
 public StudioSharedComparisonView(){var bar=new StackPanel{Orientation=Orientation.Horizontal};bar.Children.Add(mode);bar.Children.Add(split);var fit=new Button{Content="适应"};fit.Click+=(_,_)=>{foreach(var view in views)view.State.Fit();};bar.Children.Add(fit);SetDock(bar,Dock.Top);Children.Add(bar);Children.Add(grid);mode.SelectionChanged+=(_,_)=>Rebuild();split.ValueChanged+=(_,_)=>{foreach(var v in views)v.Split=split.Value;};}
 public void SetFrames(IReadOnlyList<R53VariantFrame> values){if(values.Count==frames.Count&&values.Zip(frames,(a,b)=>a.Id==b.Id&&ReferenceEquals(a.Image,b.Image)).All(x=>x))return;frames=values;Rebuild();}
 public void SetWipe(bool value){mode.SelectedIndex=value?1:0;}
 private void Rebuild()
 {
  var previous=views.FirstOrDefault()?.State;
  grid.Children.Clear();grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();views.Clear();if(frames.Count==0)return;bool wipe=mode.SelectedIndex==1&&frames.Count>=2;
  grid.ColumnDefinitions.Add(new());if(!wipe)grid.ColumnDefinitions.Add(new());grid.RowDefinitions.Add(new());if(!wipe&&frames.Count>2)grid.RowDefinitions.Add(new());
  int count=wipe?1:frames.Count;for(int i=0;i<count;i++)
  {
   var frame=frames[i];var root=new DockPanel();var label=new TextBlock{Text=wipe?frames[0].Name+" ↔ "+frames[1].Name:frame.Name,ToolTip=$"{frame.Kind} / target {frame.SourceTargetId} / version {frame.SourceVersionId} / hash {frame.SourceHash}",TextTrimming=TextTrimming.CharacterEllipsis};SetDock(label,Dock.Top);root.Children.Add(label);
   var view=new ColorStudioImageViewport{Original=frame.Image,Matched=wipe?frames[1].Image:null,Mode=wipe?"左右对比":"原片",Split=split.Value};root.Children.Add(view);views.Add(view);Grid.SetColumn(root,wipe?0:i%2);Grid.SetRow(root,wipe?0:i/2);grid.Children.Add(root);
   view.State.Changed+=(_,_)=>{if(syncing)return;syncing=true;try{foreach(var other in views.Where(v=>v!=view))FollowNormalized(other.State,view.State);}finally{syncing=false;}};
   view.MouseWheel+=(_,e)=>{view.State.ZoomAbout(e.GetPosition(view),e.Delta>0?1.15:1/1.15);e.Handled=true;};view.MouseLeftButtonDown+=(_,e)=>{pan=e.GetPosition(view);view.CaptureMouse();if(wipe)view.Split=Math.Clamp(pan.Value.X/Math.Max(1,view.ActualWidth),0,1);};view.MouseMove+=(_,e)=>{if(pan is{} start&&e.LeftButton==MouseButtonState.Pressed){var next=e.GetPosition(view);if(wipe){split.Value=Math.Clamp(next.X/Math.Max(1,view.ActualWidth),0,1);}else view.State.PanBy(next-start);pan=next;}};view.MouseLeftButtonUp+=(_,_)=>{pan=null;view.ReleaseMouseCapture();};if(previous is not null)Dispatcher.BeginInvoke(new Action(()=>{if(views.Contains(view))FollowNormalized(view.State,previous);}));
  }
 }
 public static void FollowNormalized(ColorStudioZoomPanState target,ColorStudioZoomPanState source)
 {
  if(source.IsFit){target.Fit();return;}double sourceFit=Math.Min(source.ViewportSize.Width/Math.Max(1,source.ImageSize.Width),source.ViewportSize.Height/Math.Max(1,source.ImageSize.Height));double targetFit=Math.Min(target.ViewportSize.Width/Math.Max(1,target.ImageSize.Width),target.ViewportSize.Height/Math.Max(1,target.ImageSize.Height));target.SetZoom(source.Zoom/Math.Max(1e-9,sourceFit)*targetFit);target.PanBy(new Vector(source.PanX/Math.Max(1,source.ImageSize.Width*source.Zoom)*target.ImageSize.Width*target.Zoom-target.PanX,source.PanY/Math.Max(1,source.ImageSize.Height*source.Zoom)*target.ImageSize.Height*target.Zoom-target.PanY));
 }
}

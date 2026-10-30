using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Imaging;using RAWSelectionAssistant.ViewModels;using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Views;
/// <summary>WPF viewing-state host. Shares target/variant identities; not a collage or second library.</summary>
public sealed partial class StudioViewingTableView:DockPanel
{
 private readonly ReferenceColorWorkspaceViewModel workspace;private readonly Canvas canvas=new(){Width=2400,Height=3600,Background=new SolidColorBrush(Color.FromRgb(28,29,30))};private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Gainsboro,Margin=new Thickness(6)};private ViewingFrame? selected;private Point? drag;private FrameworkElement? dragged;private readonly System.Windows.Threading.DispatcherTimer slide=new(){Interval=TimeSpan.FromSeconds(3)};private int slideIndex;private readonly ContentControl detail=new(){Visibility=Visibility.Collapsed,Height=260};private long detailRevision;private CancellationTokenSource? detailCancellation;private bool closed;
 internal Func<ViewingTableItem,int,CancellationToken,Task<BitmapSource>>? DetailRenderer;
 internal Func<CancellationToken,Task>? RefreshLoader;
 internal object? DetailContent=>detail.Content;internal string VisibleStatus=>status.Text;
 internal bool IsClosed=>closed;
 public event Action? ReturnRequested;
 public StudioViewingTableView(ReferenceColorWorkspaceViewModel model)
 {
  workspace=model;InitializeR55();
 }
 private void Run(Func<Task> action)=>RunAsync(action);
 private async void RunAsync(Func<Task> action){var before=detailRevision;try{await action();}catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException){if(!closed&&before==detailRevision)status.Text=e is OperationCanceledException?"操作已取消":e.Message;}}
 private void InvalidateRequests(){detailRevision++;var old=detailCancellation;detailCancellation=null;old?.Cancel();workspace.InvalidateViewingRequests();}
 private (long Revision,CancellationTokenSource Cancellation) BeginRequest(){InvalidateRequests();var c=new CancellationTokenSource();detailCancellation=c;return(detailRevision,c);}
 internal void StopSlideshow(){slideshowRevision++;slide.Stop();slideshow=false;paused=false;slideshowControls.Visibility=Visibility.Collapsed;InvalidateRequests();}
 internal void SelectFrame(ViewingFrame frame){selected=frame;selection.Clear();selection.Add(frame.Item.VariantId);UpdateR55Chrome();}
 private void UpdateLight()=>UpdateR55Light();
 private readonly ScrollViewer scroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
 private readonly Dictionary<Guid,Border> realized=[];private bool updatingViewport;private Task viewportWork=Task.CompletedTask;
 internal int RealizedCardCount=>realized.Count;internal Task ViewportWork=>viewportWork;
 internal async Task RefreshForViewportAsync(){if(closed)return;await RefreshAsync();}
 private void UpdateCards()
 {
  if(closed||updatingViewport)return;updatingViewport=true;try{
   var viewport=workspace.ViewingTable.Viewport;var scale=viewport.Scale;var width=Math.Max(600,scroll.ViewportWidth)/scale;var height=Math.Max(400,scroll.ViewportHeight)/scale;var visible=new Rect((scroll.HorizontalOffset-viewport.X)/scale-240,(scroll.VerticalOffset-viewport.Y)/scale-180,width+480,height+360);
   var items=workspace.ViewingTable.Items.Where(i=>IntersectsItem(i,visible)).ToArray();var wanted=items.Select(i=>i.VariantId).ToHashSet();
   foreach(var id in realized.Keys.Where(id=>!wanted.Contains(id)).ToArray()){canvas.Children.Remove(realized[id]);realized[id].Child=null;realized.Remove(id);}
   foreach(var item in items.OrderBy(i=>i.Z)){var frame=workspace.ViewingFrames.FirstOrDefault(f=>f.Item.TargetId==item.TargetId&&f.Item.VariantId==item.VariantId);if(!realized.TryGetValue(item.VariantId,out var card)){card=CreateCard(item);realized[item.VariantId]=card;canvas.Children.Add(card);}UpdateCard(card,item,frame);}
  }finally{updatingViewport=false;}
 }
 private Border CreateCard(ViewingTableItem item)=>CreateR55Card(item);
 private void UpdateCard(Border card,ViewingTableItem item,ViewingFrame? frame)=>UpdateR55Card(card,item,frame);
 private async Task RetryAsync(ViewingTableItem item){if(closed)return;slide.Stop();var request=BeginRequest();try{await workspace.RetryViewingItemAsync(item,request.Cancellation.Token);if(Current(request)){UpdateCards();status.Text=workspace.ViewingStatus;}}finally{Finish(request);}}
 internal void ScrollForTest(double y)=>scroll.ScrollToVerticalOffset(y);
 public Task RefreshAsync()=>RefreshAsync(false);
 private async Task RefreshAsync(bool visibleOnly)
 {
  if(closed)return;StopSlideshow();loupeMode=false;loupe.Visibility=Visibility.Collapsed;var request=BeginRequest();detail.Content=null;detail.Visibility=Visibility.Collapsed;UpdateCards();status.Text="准备固定变体代理…";var state=workspace.ViewingTable;var priority=realized.Keys.ToArray();
  System.ComponentModel.PropertyChangedEventHandler changed=(_,e)=>{if(!Current(request))return;if(e.PropertyName is nameof(workspace.ViewingFrames) or nameof(workspace.ViewingFailures)){UpdateCards();status.Text=workspace.ViewingStatus;}};workspace.PropertyChanged+=changed;
  loadingBar.Visibility=Visibility.Visible;UpdateR55Chrome();try{await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);if(!Current(request))return;if(RefreshLoader is{} load)await load(request.Cancellation.Token);else await workspace.PrepareViewingTableAsync(request.Cancellation.Token,priority,visibleOnly);if(!Current(request)||!ReferenceEquals(state,workspace.ViewingTable))return;UpdateCards();selected=workspace.ViewingFrames.FirstOrDefault(f=>selection.Contains(f.Item.VariantId));UpdateR55Chrome();status.Text=workspace.ViewingStatus;}
  catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException){if(Current(request))status.Text=e is OperationCanceledException?"操作已取消":e.Message;}
  finally{workspace.PropertyChanged-=changed;if(Current(request)){loadingBar.Visibility=Visibility.Collapsed;UpdateR55Chrome();}Finish(request);}
 }
 private bool Current((long Revision,CancellationTokenSource Cancellation) request)=>!closed&&request.Revision==detailRevision&&!request.Cancellation.IsCancellationRequested;
 private void Finish((long Revision,CancellationTokenSource Cancellation) request){if(ReferenceEquals(detailCancellation,request.Cancellation))detailCancellation=null;request.Cancellation.Dispose();}
 private ViewingTableItem CurrentItem(ViewingTableItem item)=>workspace.ViewingTable.Items.Single(i=>i.TargetId==item.TargetId&&i.VariantId==item.VariantId);
 private static void ApplyTransform(FrameworkElement element,ViewingTableItem item){var transform=new TransformGroup();transform.Children.Add(new ScaleTransform(item.Scale,item.Scale));transform.Children.Add(new RotateTransform(item.Rotation));element.RenderTransform=transform;element.RenderTransformOrigin=new Point(.5,.5);}
 private void TransformSelected(double scale,double rotate){if(selected is null)return;var item=CurrentItem(selected.Item);var next=item with{Scale=Math.Clamp(item.Scale*scale,.1,8),Rotation=(item.Rotation+rotate)%360};workspace.SetViewingPlacement(item.TargetId,item.VariantId,next.X,next.Y,next.Scale,next.Rotation);foreach(var card in canvas.Children.OfType<FrameworkElement>())if(card.Tag is ViewingTableItem frame&&frame.TargetId==item.TargetId&&frame.VariantId==item.VariantId)ApplyTransform(card,next);UpdateCards();UpdateR55Chrome();}
 internal async Task ShowDetailAsync(){if(closed||selected is null)return;loupeMode=false;loupe.Visibility=Visibility.Collapsed;var request=BeginRequest();var frame=selected;try{var image=await (DetailRenderer??workspace.RenderViewingItemAsync)(frame.Item,1600,request.Cancellation.Token);if(!Current(request))return;var viewport=new ColorStudioImageViewport{Original=image,Mode="原片",SourcePixelSize=new Size(image.PixelWidth,image.PixelHeight)};viewport.MouseWheel+=(_,e)=>{viewport.State.ZoomAbout(e.GetPosition(viewport),e.Delta>0?1.2:1/1.2);e.Handled=true;};Point? detailPan=null;viewport.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2){viewport.State.Fit();e.Handled=true;return;}detailPan=e.GetPosition(viewport);viewport.CaptureMouse();e.Handled=true;};viewport.MouseMove+=(_,e)=>{if(detailPan is{} start&&e.LeftButton==MouseButtonState.Pressed){var next=e.GetPosition(viewport);viewport.State.PanBy(next-start);detailPan=next;}};viewport.MouseLeftButtonUp+=(_,_)=>{detailPan=null;viewport.ReleaseMouseCapture();};viewport.LostMouseCapture+=(_,_)=>detailPan=null;var root=new DockPanel();var bar=new StackPanel{Orientation=Orientation.Horizontal};var label=new TextBlock{Text=$"代理 · {image.PixelWidth}×{image.PixelHeight}",Foreground=Brushes.Gainsboro};bar.Children.Add(label);var actual=new Button{Content="代理 1:1",Margin=new Thickness(8,0,0,0)};actual.Click+=(_,_)=>viewport.State.SetZoom(1);bar.Children.Add(actual);var fit=new Button{Content="适应"};fit.Click+=(_,_)=>viewport.State.Fit();bar.Children.Add(fit);SetDock(bar,Dock.Top);root.Children.Add(bar);root.Children.Add(viewport);detail.Content=root;detail.Visibility=Visibility.Visible;}catch(Exception e)when(e is IOException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException){if(Current(request))status.Text=e is OperationCanceledException?"操作已取消":e.Message;}finally{Finish(request);}}
 internal Task CompareAsync(){if(closed)return Task.CompletedTask;var frames=workspace.ViewingFrames;StopSlideshow();loupeMode=false;loupe.Visibility=Visibility.Collapsed;if(selected is null||frames.Count<2)return Task.CompletedTask;var index=frames.ToList().IndexOf(selected);var other=frames[(index+1)%frames.Count];var compare=new StudioSharedComparisonView();compare.SetFrames(new[]{selected,other}.Select(f=>new R53VariantFrame(f.Item.VariantId,f.Item.FrozenVariant.Name,f.Image){SourceTargetId=f.Item.TargetId,SourceVersionId=f.Item.VariantId,SourceHash=f.Item.SourceHash}).ToArray());var host=new DockPanel();var bar=new StackPanel{Orientation=Orientation.Horizontal};var swap=new Button{Content="交换"};bool swapped=false;var pair=new[]{selected,other};swap.Click+=(_,_)=>{swapped=!swapped;compare.SetFrames((swapped?pair.Reverse():pair).Select(f=>new R53VariantFrame(f.Item.VariantId,f.Item.FrozenVariant.Name,f.Image){SourceTargetId=f.Item.TargetId,SourceVersionId=f.Item.VariantId,SourceHash=f.Item.SourceHash}).ToArray());};bar.Children.Add(swap);SetDock(bar,Dock.Top);host.Children.Add(bar);host.Children.Add(compare);detail.Content=host;detail.Visibility=Visibility.Visible;return Task.CompletedTask;}
 private async Task ReturnToDarkroomAsync(){if(closed)return;slide.Stop();var request=BeginRequest();var frame=selected;try{if(frame is not null)await workspace.ReturnViewingToDarkroomAsync(frame.Item,request.Cancellation.Token);if(!Current(request))return;Close();ReturnRequested?.Invoke();}finally{Finish(request);}}
 public void Close(){if(closed)return;closed=true;slide.Stop();InvalidateRequests();workspace.CancelFilmBackendTask();workspace.ReleaseViewing();StopR55();dragged?.ReleaseMouseCapture();dragged=null;drag=null;selected=null;canvas.Children.Clear();realized.Clear();detail.Content=null;}
}

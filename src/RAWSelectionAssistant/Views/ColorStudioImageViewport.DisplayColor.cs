using System.Windows;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
namespace RAWSelectionAssistant.Views;
public sealed partial class ColorStudioImageViewport
{
 public static readonly DependencyProperty DisplayColorEnabledProperty=DependencyProperty.Register(nameof(DisplayColorEnabled),typeof(bool),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(false,DisplaySettingChanged));
 public static readonly DependencyProperty ManualDisplayProfileProperty=DependencyProperty.Register(nameof(ManualDisplayProfile),typeof(RgbIccIdentityV1),typeof(ColorStudioImageViewport),new FrameworkPropertyMetadata(null,DisplaySettingChanged));
 public bool DisplayColorEnabled{get=>(bool)GetValue(DisplayColorEnabledProperty);set=>SetValue(DisplayColorEnabledProperty,value);}
 public RgbIccIdentityV1? ManualDisplayProfile{get=>(RgbIccIdentityV1?)GetValue(ManualDisplayProfileProperty);set=>SetValue(ManualDisplayProfileProperty,value);}
 internal Task DisplayColorWork=>displayQueue?.Work??Task.CompletedTask;
 internal BitmapSource PresentedDisplayPixels(BitmapSource source)=>DisplayPixels(source);
 public string DisplayConversionStatus{get;private set;}="LegacySrgb_NotConverted";
 private RgbIccIdentityV1? displayProfile;private string? displayKey;private long displayContext;private bool displayDetached;
 private readonly Dictionary<BitmapSource,IDisposable> monitorReservations=new(ReferenceEqualityComparer.Instance);
 private void ClearMonitorImages(){monitorImages.Clear();foreach(var held in monitorReservations.Values)held.Dispose();monitorReservations.Clear();}
 private void RemoveMonitorImage(BitmapSource source){monitorImages.Remove(source);if(monitorReservations.Remove(source,out var reservation))reservation.Dispose();}
 private readonly Dictionary<BitmapSource,BitmapSource> monitorImages=new(ReferenceEqualityComparer.Instance);
 private LatestPreviewQueue<DisplayRequest,BitmapSource>? displayQueue;
 private sealed record DisplayRequest(BitmapSource Source,RgbIccIdentityV1 Profile,long Context);
 private readonly System.Windows.Threading.DispatcherTimer monitorRefresh=new(){Interval=TimeSpan.FromSeconds(3)};
 private static void DisplaySettingChanged(DependencyObject sender,DependencyPropertyChangedEventArgs e)=>((ColorStudioImageViewport)sender).RefreshDisplayColorContext();
 private void RefreshDisplayColorContext()
 {
  displayDetached=false;var resolved=DisplayColorEnabled?MonitorRgbDisplayV1.Resolve(DisplayProfileObservation,ManualDisplayProfile):(null,"LegacySrgb_NotConverted");var key=DisplayColorEnabled+":"+resolved.Item1?.Sha256+":"+resolved.Item2;
  if(key!=displayKey){displayKey=key;displayProfile=resolved.Item1;displayContext++;displayQueue?.Invalidate();ClearMonitorImages();}
  DisplayConversionStatus=resolved.Item2;ToolTip=DescribeProfileAssociation(DisplayProfileObservation)+"\nExplicit display path: "+DisplayConversionStatus;QueueDisplayImages();InvalidateVisual();
 }
 private void QueueDisplayImages()
 {
  if(displayDetached||!DisplayColorEnabled||displayProfile is null)return;
  displayQueue??=new(async(s,token)=>await Task.Run(()=>MonitorRgbDisplayV1.Convert(s.Source,s.Profile,token),token),(_,s,result)=>{if(displayDetached||s.Context!=displayContext||displayProfile?.Sha256!=s.Profile.Sha256)return;if(!ReferenceEquals(s.Source,Original)&&!ReferenceEquals(s.Source,Matched)&&!ReferenceEquals(s.Source,DetailOriginal)&&!ReferenceEquals(s.Source,DetailMatched))return;long bytes=checked((long)result.PixelWidth*result.PixelHeight*4);if(bytes>96L*1024*1024){DisplayConversionStatus="FallbackSrgb_DisplayPreviewOver96MiB";return;}if(monitorImages.Count>=4||monitorImages.Values.Sum(x=>(long)x.PixelWidth*x.PixelHeight*4)+bytes>96L*1024*1024)ClearMonitorImages();RemoveMonitorImage(s.Source);monitorReservations[s.Source]=StudioGlobalResources.ReserveWork(bytes);monitorImages[s.Source]=result;QueueDisplayImages();InvalidateVisual();},error=>{DisplayConversionStatus="FallbackSrgb_TransformFailed_"+error.GetType().Name;displayProfile=null;ClearMonitorImages();InvalidateVisual();});
  var visible=new List<BitmapSource>();if(Mode is "仿色" or "仿色结果"){if(Matched is{} m)visible.Add(m);}else if(Mode=="原片"){if(Original is{} o)visible.Add(o);}else{if(Original is{} o)visible.Add(o);if(Matched is{} m)visible.Add(m);}
  if(DetailMatched is{} dm)visible.Add(dm);if(DetailOriginal is{} originalDetail)visible.Add(originalDetail);
  foreach(var old in monitorImages.Keys.Where(x=>!visible.Contains(x)).ToArray())RemoveMonitorImage(old);
  if(visible.FirstOrDefault(x=>!monitorImages.ContainsKey(x)) is{} missing)displayQueue.Submit(new(missing,displayProfile,displayContext));
 }
 private bool CanPresentDetail(BitmapSource image)=>!DisplayColorEnabled||displayProfile is null||monitorImages.ContainsKey(image);
 private BitmapSource DisplayPixels(BitmapSource image)=>DisplayColorEnabled&&monitorImages.TryGetValue(image,out var result)?result:image;
 private void DetachDisplayColor(){monitorRefresh.Stop();displayDetached=true;displayContext++;displayQueue?.Invalidate();ClearMonitorImages();}
}

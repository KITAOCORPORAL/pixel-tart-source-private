using System.Xml.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
static class Program
{
 static string Root="",Payload="",Expected="",Icc="",Mode="",Scenario="",Phase="startup";static int Width,Height;static ushort Orientation;
 static ReferenceColorWorkspaceViewModel? Vm;static Window? Window;static PhotoGradePageView? Page;static Replay? Decoder;
 static readonly List<(string Role,WeakReference Owner)> Owners=[];static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 static object? Field(object? value,string name)=>value?.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(value);
 static Dictionary<string,Task> KnownTasks(ReferenceColorWorkspaceViewModel vm)
 {
   var result=new Dictionary<string,Task>{{"analysis",vm.AnalysisWork},{"detail",vm.Editor.DetailPreviewWork},{"lightPrint",vm.LightPrintPreviewWork}};
   if(typeof(ReferenceColorWorkspaceViewModel).GetProperty("FramePhotoPreviewWork",BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(vm) is Task frame)result["framePhotos"]=frame;
   if(Field(vm,"_thumbnailJobs") is System.Collections.IEnumerable jobs)foreach(var entry in jobs)
   {
      var job=entry!.GetType().GetProperty("Value")?.GetValue(entry);
      if(job?.GetType().GetField("Completion")?.GetValue(job) is TaskCompletionSource completion)result["thumbnail-"+result.Count]=completion.Task;
   }
   return result;
 }
 static object Describe(object? value)
 {
  if(value is FrozenRawMaster m)value=m.Image;
  if(value is not null&&!Owners.Any(x=>ReferenceEquals(x.Owner.Target,value)))Owners.Add((value.GetType().Name,new(value)));
  return value switch{HighBitDepthImageBuffer b=>new{type="HighBitDepth",id=RuntimeHelpers.GetHashCode(b),width=b.Width,height=b.Height,bytes=b.StorageBytes,floatMaterialized=b.IsFloatMaterialized,logicalFloatBytes=(long)b.SampleCount*4},BitmapSource b=>new{type=b.GetType().Name,id=RuntimeHelpers.GetHashCode(b),width=b.PixelWidth,height=b.PixelHeight,bits=b.Format.BitsPerPixel,minBytes=(long)b.PixelWidth*b.PixelHeight*b.Format.BitsPerPixel/8},null=>new{type="null"},_=>new{type=value.GetType().Name,id=RuntimeHelpers.GetHashCode(value)}};
 }
 static void Snap(string stage)
 {
  Phase=stage;File.WriteAllText(Path.Combine(Root,"phase.txt"),stage);using var p=Process.GetCurrentProcess();p.Refresh();var holders=new Dictionary<string,object>();
  if(Vm is not null){holders["tasks"]=KnownTasks(Vm).Select(x=>new{name=x.Key,status=x.Value.Status.ToString(),x.Value.IsCompleted}).ToArray();holders["editor.mode"]=new{enabled=Vm.Editor.Enabled,mode=Vm.Editor.EffectiveViewMode,detail=Vm.Editor.IsDetailPreviewRequested};holders["target.RawMaster"]=Describe(Vm.ActiveTarget?.RawMaster);holders["target.Image"]=Describe(Vm.TargetImage);foreach(var name in new[]{"_source","_interactiveSource","_matchedImage","_rawPreviewMaster","_interactiveRawMaster","_frozenRawMaster","_fullRawDisplay"})holders["Editor."+name]=Describe(Field(Vm.Editor,name));}
  if(Page is not null&&Page.Dispatcher.CheckAccess()&&Page.FindName("PhotoViewport") is ColorStudioImageViewport viewport)
      holders["viewport"]=new{zoom=viewport.State.Zoom,viewport.Mode,viewport.IsVisible,viewport.IsLoaded,width=viewport.ActualWidth,height=viewport.ActualHeight,original=Describe(viewport.Original),matched=Describe(viewport.Matched),viewport.DisplayColorEnabled};
  var modules=p.Modules.Cast<ProcessModule>().Select(x=>x.ModuleName).ToArray();if(modules.Any(x=>x.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase)||x.Equals("libraw.dll",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("RAW native module loaded");
  File.AppendAllText(Path.Combine(Root,"events.jsonl"),JsonSerializer.Serialize(new{stage,pid=p.Id,atUtc=DateTimeOffset.UtcNow,privateBytes=p.PrivateMemorySize64,workingSetBytes=p.WorkingSet64,managedEstimate=GC.GetTotalMemory(false),gcHeapBytes=GC.GetGCMemoryInfo().HeapSizeBytes,gcCommittedBytes=GC.GetGCMemoryInfo().TotalCommittedBytes,gcFragmentedBytes=GC.GetGCMemoryInfo().FragmentedBytes,allocatedBytes=GC.GetTotalAllocatedBytes(true),gcCollections=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},holders,weakOwners=Owners.Select(x=>new{x.Role,alive=x.Owner.IsAlive}),replayReads=Decoder?.Calls??0,nativeRawLoaded=false})+"\n");
 }
 static async Task Settle(){if(Vm is not null){await Vm.AnalysisWork;await Vm.Editor.DetailPreviewWork;}await Task.Delay(700);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);}
 static ColorAdjustmentStack? Stack()=>Scenario=="neutral"?null:new(new[]{new ColorAdjustmentStackNode(Guid.Parse("10101010-0000-0000-0000-000000000001"),ColorStudioNodeType.WhiteBalance,"WB",NumericParameters:new Dictionary<string,double>{{"temperature",9},{"tint",-4}}),new ColorAdjustmentStackNode(Guid.Parse("10101010-0000-0000-0000-000000000002"),ColorStudioNodeType.BasicTone,"Tone",NumericParameters:new Dictionary<string,double>{{"exposure",.5},{"highlights",-20},{"saturation",7}})},ProcessingVersion:2);
 static HighBitDepthImageBuffer ReadFloat()
 {
  var output=new float[Width*Height*3];var row=new byte[Width*6];using var file=File.OpenRead(Payload);
  for(int y=0;y<Height;y++){file.ReadExactly(row);for(int i=0;i<Width*3;i++)output[y*Width*3+i]=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(i*2,2))/65535f;}
  return new(Width,Height,output,"16","sRGB",Orientation);
 }
 static async Task Oracle()
 {
  var decoded=ReadFloat();using var master=RawImageOrientation.NormalizePixels(decoded);if(!ReferenceEquals(decoded,master))decoded.Dispose();Snap("oracle-source");var pipeline=new RawMatchTiff16ProductPipeline(new Replay());
  var rendered=pipeline.Render(master,null,Stack());using var pixels=rendered.ProcessingPixels!;Snap("oracle-rendered");
  var file=Path.Combine(Root,"oracle.tif");await AtomicTiffWriter.WriteRgb48Async(file,pixels,new(TiffBitDepth.Sixteen,IccProfile:File.ReadAllBytes(Icc),Orientation:master.Orientation));Snap("oracle-written");
  File.WriteAllText(Path.Combine(Root,"RESULT.json"),JsonSerializer.Serialize(new{status="PASS_ORIGINAL_CORE_ORACLE",file,newRawDecodes=0,outputFloatSha256=FloatHash(pixels),outputRgb24Sha256=Convert.ToHexString(SHA256.HashData(rendered.Pixels.Rgb24.Span))},Json));
 }
 static string FloatHash(HighBitDepthImageBuffer image){using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var row=new float[image.Width*3];for(int y=0;y<image.Height;y++){image.CopyRgb32(y*row.Length,row);hash.AppendData(MemoryMarshal.AsBytes(row.AsSpan()));}return Convert.ToHexString(hash.GetHashAndReset());}
 static string BitmapHash(BitmapSource image)
 {
  if(image.Format!=System.Windows.Media.PixelFormats.Rgb24)throw new InvalidOperationException("Expected legacy RGB24 display adapter");
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var row=new byte[image.PixelWidth*3];
  for(int y=0;y<image.PixelHeight;y++){image.CopyPixels(new Int32Rect(0,y,image.PixelWidth,1),row,row.Length,0);hash.AppendData(row);}
  return Convert.ToHexString(hash.GetHashAndReset());
 }
 static void Show()
 {
  var hidden=new ReferenceColorWorkspaceView{Visibility=Visibility.Collapsed,DataContext=Vm};Page=new PhotoGradePageView{DataContext=Vm};var grid=new System.Windows.Controls.Grid();grid.Children.Add(hidden);grid.Children.Add(Page);
  Window=new(){Content=grid,Width=1280,Height=850,Left=-10000,Top=0,ShowInTaskbar=false,ShowActivated=false};Window.Show();
 }
 static async Task View100()
 {
  if(Scenario!="neutral"&&!Mode.StartsWith("processed",StringComparison.Ordinal))Vm!.Editor.Enabled=false;
  if(Mode.StartsWith("processed",StringComparison.Ordinal)){Vm!.Editor.Enabled=true;Vm.Editor.ViewMode="仿色结果";}
  Show();await Settle();var viewport=(ColorStudioImageViewport)Page!.FindName("PhotoViewport");viewport.State.SetZoom(1);Vm!.Editor.RequestPreviewZoom(1);await Settle();
  var wait=Stopwatch.StartNew();
  while(Field(Vm.Editor,"_detailRequest") is not null || Vm.Editor.IsBusy ||
      (Mode.StartsWith("processed",StringComparison.Ordinal)&&Scenario!="neutral"&&Vm.Editor.MatchedImage is null))
  {
      if(Vm.Editor.HasError)throw new InvalidOperationException("Preview failed: "+Vm.Editor.StatusText);
      if(wait.Elapsed.TotalSeconds>200)throw new TimeoutException("Pending preview did not settle");
      await Task.Delay(50);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
  }
  await Vm.AnalysisWork;
  if(Math.Abs(viewport.State.Zoom-1)>1e-9)throw new InvalidOperationException("Actual viewport zoom is not 100%");
  if(!viewport.IsVisible||!viewport.IsLoaded||viewport.ActualWidth<=0||Vm.Editor.SourceImage is not{} image||image.PixelWidth!=Vm.ActiveTarget!.RawMaster!.Width||image.PixelHeight!=Vm.ActiveTarget.RawMaster.Height)throw new InvalidOperationException("Visible 100% detail was not established");
  if(Mode.StartsWith("processed",StringComparison.Ordinal)&&Scenario!="neutral"&&(Vm.Editor.MatchedImage is not{} matched||matched.PixelWidth!=image.PixelWidth||matched.PixelHeight!=image.PixelHeight))throw new InvalidOperationException("Processed full-size preview missing");
  Snap("visible-100");
 }
 static async Task Release()
 {
  if(Window?.Content is System.Windows.Controls.Grid grid)foreach(FrameworkElement child in grid.Children)child.DataContext=null;
  if(Window is not null){Window.Content=null;Window.Close();Window=null;}Page=null;var closing=Vm!;var tasks=KnownTasks(closing).Values.ToArray();Snap("release-tasks-observed");closing.Dispose();Vm=null;
  try{await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));}catch(OperationCanceledException){}
  await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Snap("released-natural-idle");
 }
 static async Task Cycle()
 {
  Decoder=new();var marker=Path.Combine(Root,"synthetic-replay-marker.raf");await File.WriteAllTextAsync(marker,"ZERO RAW substitute marker; not camera RAW. "+Expected);var dialogs=new Dialogs(Path.Combine(Root,"export-first"));
  Vm=new(dialogs,rawDecoder:Decoder);await Vm.LoadTargetAsync(marker);await Settle();if(Vm.ActiveTarget?.RawMaster is null)throw new InvalidOperationException("Legacy frozen master missing");
  if(Scenario!="neutral"){Vm.ActiveTarget.ColorAdjustmentStackSnapshot=Stack();Vm.Editor.ApplyTargetSnapshot(null,null,Vm.ActiveTarget.ColorAdjustmentStackSnapshot,render:false);}
  Snap("master-loaded");if(Mode!="proxy-cycle")await View100();
  var beforeHash=FloatHash(Vm.ActiveTarget.RawMaster.Image);var session=Path.Combine(Root,"saved.ptstudio.json");await Vm.SaveSessionAsync(session);Snap("saved");
  Vm.QuickExportFormat=StudioExportFormat.Tiff;Snap("export-start");await Vm.ExportSelectedCommand.ExecuteAsync(null);await Settle();
  if(Vm.ActiveTarget.ExportStatus!=ReferenceExportStatus.Succeeded)throw new InvalidOperationException("First export failed: "+Vm.ActiveTarget.Error+" "+Vm.StatusText);
  var first=Vm.ActiveTarget.OutputPath;var firstPreviewSha=BitmapHash(Vm.Editor.MatchedImage??Vm.Editor.SourceImage!);Snap("export-finished");if(beforeHash!=FloatHash(Vm.ActiveTarget.RawMaster!.Image))throw new InvalidOperationException("Shared master was disposed or mutated");
  if(Mode=="processed-once")
  {
      await Release();
      File.WriteAllText(Path.Combine(Root,"RESULT.json"),JsonSerializer.Serialize(new{status="PASS_PROCESSED100_FIRST_EXPORT",first,firstPreviewSha,second=(string?)null,sameProcessReopenVerified=false,newRawDecodes=0,replayReads=Decoder.Calls,pointProcessedPreviewExcluded=false,source=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion},Json));
      return;
  }
  await Release();dialogs=new Dialogs(Path.Combine(Root,"export-reopen"));Vm=new(dialogs,rawDecoder:Decoder);Snap("same-process-reopen-start");await Vm.LoadSessionAsync(session);await Settle();
  if(Vm.ActiveTarget?.RawMaster is null)throw new InvalidOperationException("Reopened frozen master missing");Snap("reopened");if(Mode!="proxy-cycle")await View100();Vm.QuickExportFormat=StudioExportFormat.Tiff;
  Snap("second-export-start");await Vm.ExportSelectedCommand.ExecuteAsync(null);await Settle();if(Vm.ActiveTarget.ExportStatus!=ReferenceExportStatus.Succeeded)throw new InvalidOperationException("Second export failed");
  var second=Vm.ActiveTarget.OutputPath;var secondPreviewSha=BitmapHash(Vm.Editor.MatchedImage??Vm.Editor.SourceImage!);Snap("second-export-finished");if(beforeHash!=FloatHash(Vm.ActiveTarget.RawMaster!.Image))throw new InvalidOperationException("Reopened master altered");await Release();
  string? third=null,thirdPreviewSha=null;
  if(Mode=="processed-three-cycle")
  {
    Vm=new(new Dialogs(Path.Combine(Root,"export-third")),rawDecoder:Decoder);Snap("third-reopen-start");await Vm.LoadSessionAsync(session);await Settle();await View100();
    Vm.QuickExportFormat=StudioExportFormat.Tiff;Snap("third-export-start");await Vm.ExportSelectedCommand.ExecuteAsync(null);await Settle();
    if(Vm.ActiveTarget?.ExportStatus!=ReferenceExportStatus.Succeeded)throw new InvalidOperationException("Third export failed");
    third=Vm.ActiveTarget.OutputPath;thirdPreviewSha=BitmapHash(Vm.Editor.MatchedImage??Vm.Editor.SourceImage!);if(beforeHash!=FloatHash(Vm.ActiveTarget.RawMaster!.Image))throw new InvalidOperationException("Third master altered");Snap("third-export-finished");await Release();
  }

  File.WriteAllText(Path.Combine(Root,"RESULT.json"),JsonSerializer.Serialize(new{status="PASS_ZERO_RAW_SAME_PROCESS_CYCLE",first,second,session,pid=Environment.ProcessId,newRawDecodes=0,replayReads=Decoder.Calls,forcedGc=false,display=Mode,scenario=Scenario,firstPreviewSha,secondPreviewSha,third,thirdPreviewSha,pointProcessedPreviewExcluded=Scenario!="neutral"&&!Mode.StartsWith("processed",StringComparison.Ordinal),source=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion},Json));
 }
 [STAThread]static int Main(string[] args)
 {
  AssemblyLoadContext.Default.Resolving+=(_,name)=>{var f=Path.Combine(AppContext.BaseDirectory,name.Name+".dll");return File.Exists(f)?AssemblyLoadContext.Default.LoadFromAssemblyPath(f):null;};
  using var input=JsonDocument.Parse(File.ReadAllText(args[0]));var r=input.RootElement;Payload=r.GetProperty("payload").GetString()!;Expected=r.GetProperty("pixelSha256").GetString()!;Icc=r.GetProperty("icc").GetString()!;Width=r.GetProperty("width").GetInt32();Height=r.GetProperty("height").GetInt32();Orientation=r.GetProperty("orientation").GetUInt16();
  Root=Path.GetFullPath(args[1]);Mode=args[2];Scenario=args[3];Directory.CreateDirectory(Root);using(var stream=File.OpenRead(Payload))if(Convert.ToHexString(SHA256.HashData(stream))!=Expected)throw new InvalidDataException("Frozen payload SHA mismatch");
  Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME","1");Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT",Path.Combine(Root,"runtime"));
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int exit=2;app.Startup+=async(_,_)=>{try{var resources=System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory,"ProductAppResources.xaml"));foreach(var src in resources.Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+src.Value,UriKind.Relative)));Snap("startup");if(Mode=="oracle")await Oracle();else await Cycle();exit=0;}catch(Exception error){File.WriteAllText(Path.Combine(Root,"FAILURE.json"),JsonSerializer.Serialize(new{phase=Phase,error=error.ToString()},Json));}finally{app.Shutdown(exit);}};app.Run();return exit;
 }
 sealed class Replay:IRawDecoder
 {
  public int Calls;
  public RawDecoderCapability GetCapability()=>new(true,"FrozenRenderedRgb48Replay",Expected,[".raf"],[".raf"]);
  public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>Task.Run(()=>{Calls++;Snap("before-ushort-allocation-"+Calls);
   if(Calls==2&&Mode=="processed-diagnostic")
   {
    File.WriteAllText(Path.Combine(Root,"DIAGNOSTIC_READY.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,phase=Phase,gcCounts=new[]{GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)},diagnosticOnly=true}));
    var timer=Stopwatch.StartNew();while(!File.Exists(Path.Combine(Root,"DIAGNOSTIC_CONTINUE"))){if(timer.Elapsed.TotalSeconds>180)throw new TimeoutException("Diagnostic boundary not released");Thread.Sleep(50);}
    Snap("after-diagnostic-boundary");
   }
   var values=new ushort[Width*Height*3];var block=new byte[1048576];using var f=File.OpenRead(Payload);for(int at=0;at<values.Length*2;){token.ThrowIfCancellationRequested();int n=Math.Min(block.Length,values.Length*2-at);f.ReadExactly(block.AsSpan(0,n));Buffer.BlockCopy(block,0,values,at,n);at+=n;}Snap("ushort-read-complete-"+Calls);return RawDecodedImage.FromRgb48(Width,Height,Width*6,values,new RawImageMetadata("FrozenRendered","ZeroRawSubstitute",null,Orientation,"sRGB"));},token);
 }
 sealed class Dialogs(string directory):IDialogService
 {
  public string? ChooseFolder(string title,string? initialDirectory=null){Directory.CreateDirectory(directory);return directory;}
  public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> x)=>null;public void ShowInfo(string s){}public void ShowError(string s){}public bool Confirm(string a,string b)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry>x)=>null;public bool ShowMediaDetails(MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}
 }
}

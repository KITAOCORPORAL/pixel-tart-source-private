using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using PixelTart.Modules.AssetLibrary;
using PixelTart.Modules.AssetLibrary.FreeCanvas;
using RAWSelectionAssistant.Core.Services.FreeCanvas;
using RAWSelectionAssistant.Core.Services.Tasks;
using RAWSelectionAssistant.Core.Services.Database;
internal static class Program
{
 static string root="",scenario="";
 static readonly List<object> observations=[];
 static readonly List<string> dispatcherErrors=[];
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 static object? Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o);
 static void Set(object o,string n,object? v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,v);
 static object? Call(object o,string n,params object?[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(o,args);
 static void Check(bool v,string reason){if(!v)throw new InvalidOperationException("Probe expectation failed: "+reason);}
 static void Record(string k,object v)=>observations.Add(new{kind=k,value=v,utc=DateTimeOffset.UtcNow});
 static void Memory(string point){using var p=Process.GetCurrentProcess();p.Refresh();Record("NaturalMemory",new{point,privateBytes=p.PrivateMemorySize64,workingSetBytes=p.WorkingSet64,osPeakWorkingSetBytes=p.PeakWorkingSet64,managedBytesEstimate=GC.GetTotalMemory(false),gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2),forcedCollection=false});}
 [STAThread] static int Main(string[] args)
 {
  scenario=args[0];root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(var x in XDocument.Load(Path.Combine(AppContext.BaseDirectory,"ProductAppResources.xaml")).Descendants().Attributes("Source"))app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+x.Value,UriKind.Relative)));
  app.DispatcherUnhandledException+=(_,e)=>{dispatcherErrors.Add(e.Exception.ToString());e.Handled=true;Record("DispatcherUnhandledException",new{type=e.Exception.GetType().FullName,e.Exception.Message});};
  int code=0;app.Startup+=async(_,_)=>{
   try{Memory("before");await Run();await Task.Delay(350);Memory("after-natural-idle");}
   catch(Exception error){code=3;Record("ProbeFailure",new{error=error.ToString()});}
   finally{File.WriteAllText(Path.Combine(root,"RESULT.json"),JsonSerializer.Serialize(new{scenario,pid=Environment.ProcessId,source="B1_WORKTREE",basis="fdf01831a65065b574e6a320aba0cb5a4425148f",authority="Controlled synthetic WPF host, unmodified production classes; reflection only for explicit state gates/prompt delegates. Not full Shell/physical UI acceptance",nativeRawDecodes=0,forcedGc=false,exitCode=code,observations,dispatcherErrors},json));app.Shutdown(code);}
  };app.Run();return code;
 }
 sealed class Provider:IAssetPreviewProvider
 {
  public readonly TaskCompletionSource<AssetPreviewResult> Completion=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public int Calls;public bool AnyCancelable;
  public Task<AssetPreviewResult> GetAsync(AssetPreviewRequest r,CancellationToken t=default){Calls++;AnyCancelable|=t.CanBeCanceled;return Completion.Task;}
  public void Complete(int w=1024){var image=BitmapSource.Create(w,w,96,96,PixelFormats.Bgra32,null,new byte[w*w*4],w*4);image.Freeze();Completion.TrySetResult(new(AssetThumbnailState.Available,AssetPreviewPurpose.Canvas,AssetPreviewQuality.High,image));}
 }
 sealed class Fixture
 {
  public required AssetLibraryPage Page;public required Window Window;public required CanvasDocumentStore Store;public bool Closed;
  public static async Task<Fixture> Create()
  {
   var product=new PixelTartDatabase(Path.Combine(root,"product.db"));Check((await new DatabaseMigrator(product,new DatabaseBackupService(product,Path.Combine(root,"backups"))).MigrateAsync()).Success,"synthetic schema migration");
   var page=new AssetLibraryPage(Path.Combine(root,"assets.db"),new TaskOperationBridge(),[],productDatabasePath:Path.Combine(root,"product.db"),onlineSelectionWorkspaceFile:Path.Combine(root,"online.json"),inspirationTrayDatabasePath:Path.Combine(root,"tray.db"));
   var window=new RAWSelectionAssistant.MainWindow{Title="Close lifecycle synthetic diagnostic",Width=1024,Height=768,Left=-20000,Top=-20000,ShowInTaskbar=false};
   ((System.Windows.Controls.ContentControl)window.FindName("AssetLibraryWorkspace")).Content=page;
   var f=new Fixture{Page=page,Window=window,Store=new CanvasDocumentStore(Path.Combine(root,"canvas"))};window.Closed+=(_,_)=>f.Closed=true;
   window.Show();await page.InitializeForSessionAsync();await page.ShowCanvasAsync(new CanvasDocument{StorageRoot=f.Store.DirectoryPath,Name="Synthetic saved canvas",PixelWidth=160,PixelHeight=120});await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
   while(((HashSet<Task>)Field(page.ActiveCanvas!,"_viewTasks")!).Count>0)await Task.Delay(10);
   await page.ActiveCanvas!.FlushAsync();return f;
  }
  public async Task Cleanup(){await Page.DisposeAsync();if(!Closed)Window.Close();}
 }
 static async Task<bool> Prepare(FreeCanvasView view,MessageBoxResult choice)=>await (Task<bool>)typeof(FreeCanvasView).GetMethod("PrepareDocumentChangeAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,[new Func<MessageBoxResult>(()=>choice)])!;
 static CanvasDocument ImageDocument(string p){var id=Guid.NewGuid();return new(){CanvasId=id,StorageRoot=Path.Combine(root,"canvas"),PixelWidth=160,PixelHeight=120,Objects=[new CanvasObject{CanvasId=id,ObjectId=Guid.NewGuid(),SourcePath=p,AssetId=null,LibraryId=Guid.Empty,Width=128,Height=128}]};}
 static string SyntheticImage(){var p=Path.Combine(root,"owned.png");var b=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[16],8);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(p))e.Save(f);return p;}
 static async Task Run()
 {
  if(scenario.StartsWith("preview-")){await Preview();return;}
  var f=await Fixture.Create();var canvas=f.Page.ActiveCanvas!;
  try
  {
   if(scenario is "close-saved" or "close-nested-frame" or "close-cancel-other-handler" or "close-repeated" or "close-save-pending" or "close-hide-pending" or "close-workspace-first" or "save-during-edit")
   {
    int closeEvents=0,frameDepth=0;bool cancelOnce=true;f.Window.Closing+=(_,e)=>{closeEvents++;Record("ClosingObserver",new{closeEvents,cancel=e.Cancel,frameDepth});
     if(scenario=="close-cancel-other-handler"&&cancelOnce){cancelOnce=false;e.Cancel=true;Record("IndependentCancel",new{canceled=true});}
     if(scenario=="close-nested-frame"&&frameDepth==0){frameDepth++;var frame=new DispatcherFrame();f.Window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));Record("NestedFrameEnter",new{depth=frameDepth});Dispatcher.PushFrame(frame);Record("NestedFrameExit",new{errors=dispatcherErrors.Count});frameDepth--;}
    };
    if(scenario=="close-workspace-first")await f.Page.DisposeAsync();
    SemaphoreSlim? gate=null;if(scenario=="close-save-pending"||scenario=="save-during-edit"){gate=(SemaphoreSlim)Field(canvas,"_saveGate")!;await gate.WaitAsync();}
    if(scenario=="save-during-edit"){canvas.Editor.AddText(1,2,"revision1");typeof(FreeCanvasView).GetProperty("DocumentClosePrompt",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,new Func<MessageBoxResult>(()=>MessageBoxResult.Yes));}
    f.Window.Close();if(scenario=="close-repeated"||gate is not null)f.Window.Close();
    if(scenario=="close-hide-pending")_ = (Task)Call(f.Page,"HideCanvasAsync")!;
    if(gate is not null){await Task.Delay(100);Check(!f.Closed,"pending save does not close window");Record("CloseWhileSaveHeld",new{closed=f.Closed,closeEvents,pending=((RAWSelectionAssistant.MainWindow)f.Window).PendingClose?.IsCompleted==false});if(scenario=="save-during-edit")canvas.Editor.AddText(3,4,"revision2");gate.Release();}
    await Task.Delay(100);
    var until=DateTime.UtcNow.AddSeconds(6);while(((RAWSelectionAssistant.MainWindow)f.Window).PendingClose is {IsCompleted:false}&&DateTime.UtcNow<until)await Task.Delay(20);
    Record("CloseOutcome",new{closed=f.Closed,closeEvents,dispatcherErrors=dispatcherErrors.Count,pending=((RAWSelectionAssistant.MainWindow)f.Window).PendingClose?.IsCompleted==false,phase=((RAWSelectionAssistant.MainWindow)f.Window).ClosePhase,detail=((RAWSelectionAssistant.MainWindow)f.Window).CloseDetail});
    if(scenario=="close-nested-frame")Check(f.Closed&&dispatcherErrors.Count==0,"nested frame no longer executes a close continuation from Closing");
    else if(scenario=="close-hide-pending")Check(!f.Closed,"changed close target aborts explicitly without closing a replacement");
    else if(scenario=="close-cancel-other-handler")Check(!f.Closed&&f.Window.IsEnabled,"other participant cancellation is not overridden by an independent Close");
    else Check(f.Closed&&dispatcherErrors.Count==0,"normal controlled close completes");
    if(scenario=="save-during-edit"){var persisted=await f.Store.LoadAsync(canvas.Editor.Document.CanvasId);Check(persisted!.Objects.Any(o=>o.Text=="revision2"),"later revision saved before close");Record("LaterRevisionSaved",new{objects=persisted.Objects.Count});}
    return;
   }
   if(scenario=="save-cancel")
   {
    canvas.Editor.AddText(1,2,"unsaved synthetic text");var dirty=canvas.Editor.Document;typeof(FreeCanvasView).GetProperty("DocumentClosePrompt",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,new Func<MessageBoxResult>(()=>MessageBoxResult.Cancel));
    f.Window.Close();await Task.Delay(150);Check(!f.Closed&&f.Window.IsEnabled&&((RAWSelectionAssistant.MainWindow)f.Window).ClosePhase=="Open","root cancel keeps editable window");var result=await Prepare(canvas,MessageBoxResult.Cancel);
    Check(!result&&ReferenceEquals(dirty,canvas.Editor.Document)&&!ReferenceEquals(Field(canvas,"_saved"),dirty),"cancel retains dirty editor");Record("Cancel",new{accepted=result,dirtyPreserved=true,autosaveResumed=((DispatcherTimer)Field(canvas,"_saveTimer")!).IsEnabled});await canvas.FlushAsync();return;
   }
   if(scenario=="save-failure-retry")
   {
    var path=Path.Combine(f.Store.DirectoryPath,canvas.Editor.Document.CanvasId.ToString("N")+".json");var previous=File.ReadAllBytes(path);canvas.Editor.AddText(1,2,"must survive save failure");var dirty=canvas.Editor.Document;
    using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
    {
     var saved=await Prepare(canvas,MessageBoxResult.Yes);Check(!saved&&ReferenceEquals(dirty,canvas.Editor.Document)&&File.ReadAllBytes(path).SequenceEqual(previous),"failed save leaves prior file and dirty revision intact");bool failed=false;try{await f.Page.DisposeAsync();}catch(IOException){failed=true;}Check(failed&&!((bool)Field(f.Page,"_disposed")!),"save failure stays before irreversible teardown");Record("SaveFailure",new{prepared=saved,dirtyRetained=true,oldFileRetained=true,disposeFailed=failed,startedDisposal=((bool)Field(f.Page,"_disposed")!)});
    }
    Check(await Prepare(canvas,MessageBoxResult.Yes),"save recovery succeeds");await f.Page.DisposeAsync();Record("SaveRetry",new{newFileReadable=(await f.Store.LoadAsync(dirty.CanvasId))!.Objects.Single().Text=="must survive save failure",disposalRetrySucceeded=true});return;
   }
   if(scenario=="save-undo-reopen-export")
   {
    canvas.Editor.AddText(1,2,"synthetic before close");var edited=canvas.Editor.Document;canvas.Editor.Undo();Check(canvas.Editor.Document.Objects.Count==0,"undo");canvas.Editor.Redo();Check(canvas.Editor.Document.Objects.Count==1,"redo");Check(await canvas.FlushAsync(),"save");var document=(await f.Store.LoadAsync(edited.CanvasId))!;Check(document.Objects.Single().Text=="synthetic before close","saved reopen");var output=Path.Combine(root,"synthetic-export.png");canvas.ExportTransparentTo(output);var decoder=BitmapDecoder.Create(new Uri(output),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Check(decoder.Frames[0].PixelWidth==160&&decoder.Frames[0].PixelHeight==120,"real encoder dimensions");Record("SaveUndoRedoReopenExport",new{undo=true,redo=true,reopen=true,export=true,exportSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))),pixelMathModified=false});return;
   }
   throw new ArgumentException("Unknown scenario "+scenario);
  }
  finally{await f.Cleanup();}
 }
 static async Task Preview()
 {
  var provider=new Provider();var editor=new CanvasEditor(ImageDocument(SyntheticImage()));var surface=new FreeCanvasSurface(editor,provider);
  if(scenario=="preview-after-unload")
  {
   var work=surface.LoadPreviewsAsync();Check(!work.IsCompleted,"provider is controlled pending");surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));bool canceled=((CancellationTokenSource)Field(surface,"_previewCancellation")!).IsCancellationRequested;provider.Complete();await work;Check(canceled&&provider.AnyCancelable&&surface.LoadedPreviews.Count==0,"unload refuses late publication even when provider ignores token");Record("LatePreviewAfterUnload",new{canceled,providerReceivedCancelableToken=provider.AnyCancelable,imagesAfterUnloaded=surface.LoadedPreviews.Count,heldBytesLowerBound=surface.LoadedPreviews.Values.Sum(b=>(long)b.PixelWidth*b.PixelHeight*4)});return;
  }
  if(scenario=="preview-editor-change-after-unload")
  {
   surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));var before=surface.PreviewRequestCount;editor.AddText(1,2,"late editor change");await Task.Delay(130);Check(provider.Calls==0&&surface.PreviewRequestCount==before,"unloaded surface does not restart work");surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));await Task.Delay(130);Check(provider.Calls>0,"reloaded surface resumes previews");provider.Complete();await Task.Delay(100);Record("EditorSubscriptionAfterUnload",new{requestsBefore=before,requestsAfter=surface.PreviewRequestCount,provider.Calls,imagesAfterUnload=surface.LoadedPreviews.Count});return;
  }
  if(scenario=="preview-workspace-dispose")
  {
   var f=await Fixture.Create();Set(f.Page,"_previewProvider",provider);var show=f.Page.ShowCanvasAsync(editor.Document);await Task.Delay(80);Check(provider.Calls>0&&!show.IsCompleted,"actual page canvas preview pending");var view=f.Page.ActiveCanvas!;var disposal=f.Page.DisposeAsync().AsTask();await Task.Delay(60);bool disposedBeforePreview=disposal.IsCompleted;Check(!disposedBeforePreview,"page disposal waits for ignored-cancel preview");provider.Complete();await disposal;await show;await Task.Delay(100);Record("WorkspaceDisposeWhilePreviewPending",new{disposedBeforePreview,imagesAfterDispose=view.Surface.LoadedPreviews.Count,providerReceivedCancelableToken=provider.AnyCancelable,pageStartedDisposal=((bool)Field(f.Page,"_disposed")!)});Check(view.Surface.LoadedPreviews.Count==0&&view.Surface.PendingPreviewCount==0,"no preview or owned work survives terminal disposal");var requests=view.Surface.PreviewRequestCount;view.Editor.AddText(1,2,"late mutation");await view.Surface.LoadPreviewsAsync();Check(view.Surface.PreviewRequestCount==requests,"released surface cannot restart");await f.Cleanup();return;
  }
  if(scenario=="preview-loaded-unload-retains")
  {
   var work=surface.LoadPreviewsAsync();provider.Complete(2048);await work;Memory("preview-loaded");surface.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));await Task.Delay(1200);Check(surface.LoadedPreviews.Count==1,"unload retains already-published preview while surface is retained");Record("UnloadedSurfaceOwners",new{images=surface.LoadedPreviews.Count,retainedBytesLowerBound=surface.LoadedPreviews.Values.Sum(b=>(long)b.PixelWidth*b.PixelHeight*4),interpretation="Proves retained owner references, not an unbounded GC leak or immediate OS decommit requirement"});GC.KeepAlive(surface);return;
  }
  throw new ArgumentException("Unknown preview scenario");
 }
}

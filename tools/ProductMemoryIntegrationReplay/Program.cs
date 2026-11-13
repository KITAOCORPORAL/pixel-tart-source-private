using System.Xml.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;

static class Program
{
    static string output="",phase="startup";static readonly Stopwatch clock=Stopwatch.StartNew();static ReferenceColorWorkspaceViewModel? workspace;static ReplayDecoder? decoder;static Window? window;static ColorStudioImageViewport? viewport;static FrameworkElement? pageHost;static readonly List<WeakReference> owners=[];
    static readonly JsonSerializerOptions json=new(){WriteIndented=true};
    [DllImport("user32.dll")]static extern uint GetGuiResources(IntPtr process,uint flags);
    [DllImport("kernel32.dll")]static extern nuint VirtualQuery(IntPtr address,out MemoryInfo info,nuint size);
    [StructLayout(LayoutKind.Sequential)]struct MemoryInfo{public IntPtr BaseAddress,AllocationBase;public uint AllocationProtect;public ushort PartitionId;public nuint RegionSize;public uint State,Protect,Type;}
    static object? Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(o);
    static void Write(string name,object value)=>File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(value,json));
    static void Event(string kind,object value)=>File.AppendAllText(Path.Combine(output,"events.jsonl"),JsonSerializer.Serialize(new{atUtc=DateTimeOffset.UtcNow,elapsedMs=clock.Elapsed.TotalMilliseconds,phase,kind,value})+"\n");
    static object Describe(object? value)=>value switch
    {
        BitmapSource b=>new{type=b.GetType().Name,id=RuntimeHelpers.GetHashCode(b),width=b.PixelWidth,height=b.PixelHeight,bits=b.Format.BitsPerPixel,frozen=b.IsFrozen,minimumPixelBytes=(long)b.PixelWidth*b.PixelHeight*b.Format.BitsPerPixel/8},
        HighBitDepthImageBuffer b=>new{type="HighBitDepthImageBuffer",id=RuntimeHelpers.GetHashCode(b),width=b.Width,height=b.Height,floatBytes=(long)b.Rgb32.Length*4},
        FrozenRawMaster m=>Describe(m.Image),
        null=>new{type="null"},
        _=>new{type=value.GetType().FullName,id=RuntimeHelpers.GetHashCode(value)}
    };
    static Dictionary<string,object> Holders()
    {
        var found=new Dictionary<string,object>();if(workspace is not{} vm)return found;
        foreach(var f in new[]{"_source","_interactiveSource","_matchedImage","_rawPreviewMaster","_interactiveRawMaster","_frozenRawMaster","_fullRawDisplay"})
        {var value=Field(vm.Editor,f);found["Editor."+f]=Describe(value);if(value is not null&&!owners.Any(w=>ReferenceEquals(w.Target,value)))owners.Add(new(value));}
        found["Target.RawMaster"]=Describe(vm.ActiveTarget?.RawMaster);found["Workspace.analysisImage"]=Describe(Field(vm,"_analysisImage"));found["Editor.Enabled"]=vm.Editor.Enabled;found["Editor.StackNodes"]=vm.Editor.AdjustmentStack.Nodes.Select(n=>new{n.Name,n.Type,n.Enabled}).ToArray();
        var cache=Field(vm.Editor,"_previewCache") as System.Collections.IEnumerable;
        if(cache is not null){var entries=new List<object>();foreach(var entry in cache){var type=entry!.GetType();entries.Add(new{source=Describe(type.GetField("Item1")?.GetValue(entry)),result=Describe(type.GetField("Item3")?.GetValue(entry))});}found["Editor.previewCache"]=entries;}
        if(viewport is not null){found["Viewport.Original"]=Describe(viewport.Original);found["Viewport.Matched"]=Describe(viewport.Matched);found["Viewport.DisplayColorEnabled"]=viewport.DisplayColorEnabled;}
        return found;
    }
    static object VirtualMemory()
    {
        ulong at=0,privateBytes=0,mapped=0,image=0;int regions=0;var size=(nuint)Marshal.SizeOf<MemoryInfo>();
        while(at<0x00007fffffff0000&&VirtualQuery((IntPtr)at,out var m,size)!=0){ulong length=(ulong)m.RegionSize;if(length==0)break;if(m.State==0x1000){if(m.Type==0x20000)privateBytes+=length;else if(m.Type==0x40000)mapped+=length;else if(m.Type==0x1000000)image+=length;regions++;}var next=(ulong)m.BaseAddress+length;if(next<=at)break;at=next;}
        return new{privateCommittedVirtualBytes=privateBytes,mappedCommittedVirtualBytes=mapped,imageCommittedVirtualBytes=image,regions,note="VirtualQuery classifications include GC and native allocations; they are not a native-heap attribution."};
    }
    static void Snapshot(string name)
    {
        using var process=Process.GetCurrentProcess();process.Refresh();var gc=GC.GetGCMemoryInfo();var objects=Holders();
        Event("MemorySnapshot",new{name,pid=process.Id,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,osPeakWorkingSetBytes=process.PeakWorkingSet64,managedEstimatedLiveBytes=GC.GetTotalMemory(false),totalAllocatedBytes=GC.GetTotalAllocatedBytes(true),gcLastHeapSizeBytes=gc.HeapSizeBytes,gcCommittedBytes=gc.TotalCommittedBytes,gcFragmentedBytes=gc.FragmentedBytes,gcIndex=gc.Index,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2),handles=process.HandleCount,gdiObjects=GetGuiResources(process.Handle,0),userObjects=GetGuiResources(process.Handle,1),virtualMemory=VirtualMemory(),holders=objects,weakOwnersAlive=owners.Count(w=>w.IsAlive),weakOwnerIdentities=owners.Where(w=>w.IsAlive).Select(w=>new{type=w.Target?.GetType().FullName,id=w.Target is{} target?RuntimeHelpers.GetHashCode(target):0}).ToArray(),nativeRawLoaded=process.Modules.Cast<ProcessModule>().Any(m=>m.ModuleName.Equals("raw_r.dll",StringComparison.OrdinalIgnoreCase))});
    }
    static string ExportInputFingerprint(HighBitDepthImageBuffer image)
    {
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var row=new byte[image.Width*6];var values=image.Rgb32.Span;
        for(int y=0;y<image.Height;y++){for(int x=0;x<image.Width*3;x++){var v=(ushort)Math.Clamp(Math.Round(values[y*image.Width*3+x]*65535),0,65535);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(x*2,2),v);}hash.AppendData(row);}
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    static void ForceGc(){GC.Collect(2,GCCollectionMode.Forced,true,true);GC.WaitForPendingFinalizers();GC.Collect(2,GCCollectionMode.Forced,true,true);}
    static async Task Idle(string label,int ms=1500){phase=label;await Task.Delay(ms);await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);Snapshot(label);}
    [STAThread]static int Main(string[] args)
    {
        if(args.Length<3)return 2;var input=Path.GetFullPath(args[0]);output=Path.GetFullPath(args[1]);var mode=args[2];if(Directory.Exists(output))throw new IOException("Preserve replay evidence");Directory.CreateDirectory(output);
        foreach(var name in new[]{"PIXEL_TART_XTRANS_STABLE_V1","PIXEL_TART_XTRANS_PLAN","PIXEL_TART_XTRANS_STEP","PIXEL_TART_HUMAN_ACCEPTANCE"})Environment.SetEnvironmentVariable(name,null);
        Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME","1");Environment.SetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT",Path.Combine(output,"runtime"));
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        int result=1;app.Dispatcher.BeginInvoke(async()=>{try{await Run(input,mode);result=0;}catch(Exception e){Write("ERROR",new{error=e.ToString(),phase});result=1;}finally{app.Shutdown(result);}});app.Run();return result;
    }

    static void ProbeTiff(string path)
    {
        using var file=File.OpenRead(path);
        var frame=BitmapFrame.Create(file,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        BitmapSource pixels=frame.Format==PixelFormats.Rgb48?frame:new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var stride=checked(pixels.PixelWidth*6);var row=new byte[stride];
        for(int y=0;y<pixels.PixelHeight;y++){pixels.CopyPixels(new Int32Rect(0,y,pixels.PixelWidth,1),row,stride,0);hash.AppendData(row);}
        var profiles=new List<object>();
        if(frame.ColorContexts is {} contexts)foreach(var context in contexts){using var profile=context.OpenProfileStream();if(profile is not null){using var bytes=new MemoryStream();profile.CopyTo(bytes);var raw=bytes.ToArray();profiles.Add(new{bytes=raw.Length,sha256=Convert.ToHexString(SHA256.HashData(raw))});}}
        Write("TIFF_PROBE",new{path,width=frame.PixelWidth,height=frame.PixelHeight,storedPixelFormat=frame.Format.ToString(),bitsPerPixel=frame.Format.BitsPerPixel,rgb48PixelSha256=Convert.ToHexString(hash.GetHashAndReset()),iccProfiles=profiles,rawDecodeCalls=0,scope="WIC stored-sample readback normalized only to RGB48 channel layout; no camera RAW decoder"});
    }
    static async Task RenderedTiff(string input,string mode)
    {
        if(Path.GetExtension(input).ToLowerInvariant() is not (".tif" or ".tiff"))throw new InvalidDataException("Only an already-rendered TIFF is admitted");
        var modelOnly=mode.Contains("model");var reopening=mode.Contains("reopen");var session=Environment.GetEnvironmentVariable("KITAO_TIFF_REOPEN_SESSION");
        var dialogs=new RenderedDialogs(input,output,session);decoder=new ReplayDecoder(input,Event);
        workspace=new(dialogs,rawDecoder:decoder);
        if(!modelOnly)
        {
        var resources=XDocument.Load(Path.Combine(AppContext.BaseDirectory,"ProductAppResources.xaml"));
        foreach(var entry in resources.Descendants().Attributes("Source"))Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+entry.Value,UriKind.Relative)));
        var grid=new System.Windows.Controls.Grid();grid.Children.Add(new ReferenceColorWorkspaceView{Visibility=Visibility.Collapsed,DataContext=workspace});var page=new PhotoGradePageView{DataContext=workspace};grid.Children.Add(page);pageHost=grid;viewport=(ColorStudioImageViewport)page.FindName("PhotoViewport");
        workspace.Editor.PropertyChanged+=(_,e)=>{if(workspace is not null && (e.PropertyName is nameof(TetherReferenceModeViewModel.SourceImage) or nameof(TetherReferenceModeViewModel.MatchedImage))){viewport.Original=workspace.Editor.SourceImage;viewport.Matched=workspace.Editor.MatchedImage;viewport.SourcePixelSize=workspace.Editor.SourcePixelSize;}};
        window=new Window{Title="Controlled rendered TIFF product contract",Content=grid,Width=1600,Height=920,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-4000,Top=0};window.Show();
        }
        phase=reopening?"rendered-session-open":"rendered-tiff-import";
        if(reopening){if(string.IsNullOrWhiteSpace(session)||!File.Exists(session))throw new IOException("Explicit saved session required");await workspace.OpenSessionCommand.ExecuteAsync(null);}else await workspace.ChooseTargetCommand.ExecuteAsync(null);
        await workspace.AnalysisWork;
        if(workspace.ActiveTarget is null||workspace.Editor.SourceImage is null||decoder.CacheReads!=0)throw new InvalidDataException("Rendered TIFF did not load through the non-RAW production path: "+workspace.StatusText);
        if(viewport is not null)
        {
        await Idle("rendered-fit-idle");
        viewport.State.SetZoom(1);await workspace.Editor.DetailPreviewWork;await workspace.AnalysisWork;await Idle("rendered-100-idle");
        viewport.State.SetZoom(.2);await workspace.Editor.DetailPreviewWork;await workspace.AnalysisWork;await Idle("rendered-20-idle");
        viewport.State.SetZoom(1);await workspace.Editor.DetailPreviewWork;await workspace.AnalysisWork;await Idle("rendered-100-return-idle");
        }
        else await Idle("model-source-idle");
        workspace.QuickExportFormat=StudioExportFormat.Tiff;dialogs.ExportDirectory=Path.Combine(output,"export");Directory.CreateDirectory(dialogs.ExportDirectory);
        var cacheResolver=typeof(ReferenceColorWorkspaceViewModel).GetMethod("FrozenRasterExportSource",BindingFlags.Instance|BindingFlags.NonPublic);
        Event("RasterExportSourceBinding",new{bound=cacheResolver?.Invoke(workspace,[workspace.ActiveTarget.Path,workspace.ActiveTarget.SourceHash]) is BitmapSource});
        phase="rendered-real-export";await workspace.ExportSelectedCommand.ExecuteAsync(null);
        var target=workspace.ActiveTarget;
        if(target.ExportStatus!=ReferenceExportStatus.Succeeded||target.OutputPath is null||!File.Exists(target.OutputPath))throw new IOException("Production TIFF export failed: "+target.Error+" / "+workspace.ExportFailureSummary);
        var exported=target.OutputPath;
        if(!reopening){phase="rendered-session-save";await workspace.SaveSessionCommand.ExecuteAsync(null);if(!File.Exists(dialogs.SessionFile))throw new IOException("Production session was not saved");}
        await Idle("rendered-export-idle");if(window is not null){window.WindowState=WindowState.Minimized;await Idle("rendered-minimized-idle",5000);window.WindowState=WindowState.Normal;await Idle("rendered-restored-idle");}
        Write("RENDERED_TIFF_RESULT",new{status="PASS_CONTROLLED_REAL_TIFF_IO",processId=Environment.ProcessId,productVersion=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,input,output=exported,session=reopening?session:dialogs.SessionFile,reopenedInIndependentProcess=reopening,visualHost=!modelOnly,completeViewportFlow=!modelOnly,rawDecoderRequests=decoder.CacheReads,rawDecodeCalls=0,authority="Controlled product ViewModel and visible-view classes with fixed file dialogs; not physical user-entry acceptance",source=Describe(workspace.Editor.SourceImage)});
        phase="rendered-close";workspace.Dispose();workspace=null;if(window is not null){window.Content=null;window.Close();}await Idle("rendered-close-natural-idle",2500);
    }

    static async Task Run(string input,string mode)
    {
        if(mode.StartsWith("rendered-tiff")){await RenderedTiff(input,mode);return;}
        if(mode=="probe-tiff"){ProbeTiff(input);return;}
        phase="startup";Snapshot("startup");var source=Path.Combine(output,"cache-fixture.raf");File.WriteAllText(source,"Synthetic header only; the injected replay decoder reads only the owned RGB48 cache copy.");
        decoder=new ReplayDecoder(input,Event);workspace=new(new Dialogs(source),rawDecoder:decoder);workspace.Editor.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(TetherReferenceModeViewModel.IsDetailPreviewRequested))Event("DetailRequestChanged",new{requested=workspace?.Editor.IsDetailPreviewRequested,stack=Environment.StackTrace});if(e.PropertyName is nameof(TetherReferenceModeViewModel.SourceImage) or nameof(TetherReferenceModeViewModel.MatchedImage)){Event("ImageChanged",new{property=e.PropertyName,source=Describe(workspace?.Editor.SourceImage),matched=Describe(workspace?.Editor.MatchedImage)});if(viewport is not null&&workspace is not null){viewport.Original=workspace.Editor.SourceImage;viewport.Matched=workspace.Editor.MatchedImage;viewport.SourcePixelSize=workspace.Editor.SourcePixelSize;}}};
        if(mode.Contains("window")||mode.Contains("shell"))
        {
            var resources=System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory,"ProductAppResources.xaml"));
            foreach(var entry in resources.Descendants().Attributes("Source"))Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/KitaoPhotoSelector;component/"+entry.Value,UriKind.Relative)));
            viewport=new ColorStudioImageViewport{Mode="原片",PreferDisplayedDimensions=true};FrameworkElement content=viewport;
            if(mode.Contains("shell")&&!mode.Contains("main")){var grid=new System.Windows.Controls.Grid();var legacy=new ReferenceColorWorkspaceView{Visibility=Visibility.Collapsed,DataContext=workspace};var page=new PhotoGradePageView{DataContext=workspace};grid.Children.Add(legacy);grid.Children.Add(page);pageHost=grid;viewport=(ColorStudioImageViewport)page.FindName("PhotoViewport");content=grid;Event("HostCreated",new{hiddenLegacy=true,visiblePhotoPage=true});}
            window=mode.Contains("main")?new RAWSelectionAssistant.MainWindow{DataContext=new ShellContext(workspace),Width=1600,Height=920,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-4000,Top=0}:new Window{Title="Isolated no-RAW memory replay",Content=content,Width=1600,Height=920,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-4000,Top=0};window.Show();
        }
        phase="import-replay";Event("PhaseBegin",new{mode,rawDecodes=0});await workspace.ChooseTargetCommand.ExecuteAsync(null);await workspace.AnalysisWork;Snapshot("after-import");if(workspace.ActiveTarget?.RawMaster is null)throw new InvalidDataException("No cached fixture master");
        await Idle("import-idle");
        if(mode.Contains("soak"))
        {
            phase="minimize";if(window is not null)window.WindowState=WindowState.Minimized;Snapshot("window-minimized");
            for(int interval=1;interval<=14;interval++)await Idle("minimized-idle-"+(interval*15)+"s",15000);
            phase="restore";if(window is not null)window.WindowState=WindowState.Normal;await Idle("window-restored",3000);
        }
        if(!mode.Contains("natural")){ForceGc();Snapshot("import-after-diagnostic-gc");}
        if(mode.Contains("neutral")){workspace.Editor.ApplyTargetSnapshot(null,null,new([new(Guid.NewGuid(),ColorStudioNodeType.Preset,"Neutral")],ProcessingVersion:2),false);Event("SetNeutralStack",new{});}
        phase="detail-zoom";Snapshot("before-detail");if(!mode.Contains("auto")){if(mode.Contains("cycle")&&viewport is not null){viewport.State.SetZoom(1);await workspace.Editor.DetailPreviewWork;}else await workspace.Editor.SetPreviewZoomAsync(1.5);}Snapshot("after-detail");await workspace.AnalysisWork;await Idle("detail-idle");if(!mode.Contains("natural")){ForceGc();Snapshot("detail-after-diagnostic-gc");}
        if(mode.Contains("cycle")&&viewport is not null)
        {
            foreach(var zoom in new[]{2d,.1,1d,.15,1d}){phase="zoom-switch-"+zoom;viewport.State.SetZoom(zoom);await workspace.Editor.DetailPreviewWork;await Idle(phase,750);}
            phase="save-project";await workspace.SaveSessionCommand.ExecuteAsync(null);if(!File.Exists(Path.Combine(output,"replay.ptstudio.json")))throw new IOException("Cache replay project did not save");Snapshot("after-project-save");
        }
        phase="pixel-check";Write("EXPORT_INPUT_BEFORE",new{pixelSha256=ExportInputFingerprint(workspace.ActiveTarget!.RawMaster!.Image),rawDecodeCalls=0});var image=workspace.Editor.SourceImage??throw new InvalidDataException("No source image");if(!mode.Contains("auto")&&(image.PixelWidth!=7752||image.PixelHeight!=5184))throw new InvalidDataException("Not full detail");
        var pixelBytes=new byte[image.PixelWidth*3];using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);for(int y=0;y<image.PixelHeight;y++){image.CopyPixels(new Int32Rect(0,y,image.PixelWidth,1),pixelBytes,pixelBytes.Length,0);hash.AppendData(pixelBytes);}Write("PIXEL_RESULT",new{fullDisplayRgb24Sha=Convert.ToHexString(hash.GetHashAndReset()),image.PixelWidth,image.PixelHeight,cacheReadCalls=decoder.CacheReads,rawDecodeCalls=0});image=null;
        await workspace.Editor.SetPreviewZoomAsync(.1);await workspace.AnalysisWork;await Idle("proxy-return");if(!mode.Contains("natural")){ForceGc();Snapshot("proxy-after-diagnostic-gc");}
        if(mode.Contains("cycle"))
        {
            phase="close-workspace";if(pageHost is System.Windows.Controls.Grid grid)foreach(var child in grid.Children.OfType<FrameworkElement>())child.DataContext=null;
            if(viewport is not null){viewport.Original=null;viewport.Matched=null;}
            workspace.Dispose();workspace=null;await Idle("closed-natural-idle",2500);ForceGc();Snapshot("closed-diagnostic-gc");
            phase="reopen-project";workspace=new(new Dialogs(source),rawDecoder:decoder);await workspace.OpenSessionCommand.ExecuteAsync(null);await workspace.AnalysisWork;
            if(workspace.ActiveTarget?.RawMaster is null)throw new InvalidDataException("Saved project cache replay did not reopen");
            if(pageHost is System.Windows.Controls.Grid reopened)foreach(var child in reopened.Children.OfType<FrameworkElement>())child.DataContext=workspace;
            await Idle("reopened-natural-idle",3000);Write("EXPORT_INPUT_REOPEN",new{pixelSha256=ExportInputFingerprint(workspace.ActiveTarget!.RawMaster!.Image),rawDecodeCalls=0});Write("REOPEN_RESULT",new{status="completed",cacheReads=decoder!.CacheReads,rawDecodeCalls=0,source=Describe(workspace.Editor.SourceImage),projectSchema=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"replay.ptstudio.json"))).RootElement.GetProperty("version").GetInt32()});
        }
        phase="release";if(window is not null)window.DataContext=null;if(pageHost is not null){foreach(var child in ((System.Windows.Controls.Grid)pageHost).Children.OfType<FrameworkElement>())child.DataContext=null;}pageHost=null;if(viewport is not null){viewport.Original=null;viewport.Matched=null;}workspace.Dispose();workspace=null;decoder=null;window?.Close();viewport=null;window=null;await Idle("release-before-diagnostic-gc");ForceGc();await Idle("release-after-diagnostic-gc");Write("RESULT",new{status="completed",rawDecodeCalls=0,weakOwnersAlive=owners.Count(w=>w.IsAlive),mode});
    }
}
sealed class ReplayDecoder(string input,Action<string,object> trace):IRawDecoder
{
    public int CacheReads;public RawDecoderCapability GetCapability()=>new(true,"FrozenCacheReplay_NoRaw","1",[],[]);
    public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>Task.Run(()=>
    {
        Interlocked.Increment(ref CacheReads);var pixels=new ushort[7752*5184*3];using(var f=File.OpenRead(input)){if(f.Length!=pixels.LongLength*2)throw new InvalidDataException("Cached RGB48 size");var block=new byte[1048576];for(int at=0;at<pixels.Length*2;){token.ThrowIfCancellationRequested();int n=Math.Min(block.Length,pixels.Length*2-at);f.ReadExactly(block.AsSpan(0,n));Buffer.BlockCopy(block,0,pixels,at,n);at+=n;}}
        trace("CacheReplayRead",new{bytes=pixels.LongLength*2,rawDecoderCalls=0});return RawDecodedImage.FromRgb48(7752,5184,7752*6,pixels,new("SyntheticReplay","FrozenXTransCache",null,1,"sRGB"));
    },token);
}
sealed class Dialogs(string source):IDialogService
{
    public string? ChooseFolder(string title,string? initialDirectory=null)=>null;public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[filter.Contains("ptstudio")?Path.Combine(Path.GetDirectoryName(source)!,"replay.ptstudio.json"):source];public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>Path.Combine(Path.GetDirectoryName(source)!,"replay.ptstudio.json");public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;public void ShowInfo(string text){}public void ShowError(string text)=>throw new InvalidDataException(text);public bool Confirm(string text,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>null;public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
}

sealed class ShellContext(ReferenceColorWorkspaceViewModel workspace)
{
 public ReferenceColorWorkspaceViewModel ReferenceColorPage=>workspace;public bool IsReferenceColorPage=>true;public string CurrentPage=>"ReferenceColor";public double SidebarWidth=>40;public bool IsSidebarExpanded=>false;public bool IsSidebarCollapsed=>true;public string CurrentWorkspaceLabel=>"Isolated cached-RGB16 memory replay";
}

sealed class RenderedDialogs(string source,string root,string? reopen):IDialogService
{
    public string ExportDirectory {get;set;}=Path.Combine(root,"export");
    public string SessionFile=>Path.Combine(root,"rendered.ptstudio.json");
    public string? ChooseFolder(string title,string? initialDirectory=null)=>ExportDirectory;
    public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect=true)=>[filter.Contains("ptstudio")?reopen??SessionFile:source];
    public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>SessionFile;
    public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>null;
    public void ShowInfo(string text){}public void ShowError(string text)=>throw new IOException(text);
    public bool Confirm(string text,string title)=>true;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}
    public RawFileEntry? ChooseRawCandidate(IReadOnlyList<RawFileEntry> candidates)=>throw new InvalidOperationException("New RAW admission is zero");
    public bool ShowMediaDetails(MediaSelectionItem item,bool advanced)=>false;public void RevealFile(string path){}
}

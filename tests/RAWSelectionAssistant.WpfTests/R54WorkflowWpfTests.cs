using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]
public class R54WorkflowWpfTests
{
    static async Task Settle(TetherReferenceModeViewModel editor){var until=DateTime.UtcNow.AddSeconds(20);while(!editor.IsSettled&&DateTime.UtcNow<until)await Task.Delay(20);Assert.IsTrue(editor.IsSettled,editor.StatusText);}
    static string Pixels(BitmapSource b){var x=new FormatConvertedBitmap(b,PixelFormats.Rgba64,null,0);var a=new byte[x.PixelWidth*x.PixelHeight*8];x.CopyPixels(a,x.PixelWidth*8,0);return Convert.ToHexString(SHA256.HashData(a));}
    static string Image(string root){var a=new byte[64*48*3];for(int i=0;i<a.Length;i++)a[i]=(byte)(i%241);var bitmap=BitmapSource.Create(64,48,96,96,PixelFormats.Rgb24,null,a,64*3);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));var p=Path.Combine(root,"synthetic.png");using var stream=File.Create(p);png.Save(stream);return p;}
    [TestMethod]public Task GraphPreviewSaveReopenExportAndStillsUseActualRoute()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r54-workflow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var path=Image(root);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(path);await vm.CreateR53VariantAsync(true);vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");Assert.IsTrue(vm.Editor.AddParallelGraphBranch(),vm.Editor.StatusText);
            var parameter=ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure");vm.Editor.SetToolParameter(parameter,.8);await Settle(vm.Editor);var route=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);var actual=Pixels(vm.Editor.MatchedImage!);var export=await vm.Editor.ProcessForExportAsync(path,null,null,CancellationToken.None,vm.Editor.AdjustmentStack);Assert.AreEqual(actual,Pixels(export));var exported=Path.Combine(root,"route.png");StudioQuickExport.Encode(export,path,exported,StudioExportFormat.Png);Assert.AreEqual(actual,Pixels(StudioQuickExport.Load(exported)));
            Assert.IsTrue(vm.CaptureR54Still(),vm.StatusText);Assert.AreEqual(1,vm.R54Stills.Count);var session=Path.Combine(root,"graph.ptstudio.json");await vm.SaveSessionAsync(session);
            using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs());await reopened.LoadSessionAsync(session);await Settle(reopened.Editor);Assert.AreEqual(route,JsonSerializer.Serialize(reopened.Editor.AdjustmentStack));Assert.AreEqual(actual,Pixels(reopened.Editor.MatchedImage!));Assert.AreEqual(1,reopened.R54Stills.Count);
            var visible=vm.Editor.AdjustmentStack.Graph!.GroupEnabled;vm.Editor.ProfessionalGroupEnabled=!visible;await Settle(vm.Editor);vm.Editor.UndoAdjustmentCommand.Execute(null);await Settle(vm.Editor);Assert.AreEqual(visible,vm.Editor.AdjustmentStack.Graph!.GroupEnabled);
        }
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task OriginalFrameRealPreviewDevelopReopenAndHistogram()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r54-frame-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(Image(root));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;vm.SelectedLightPrintFrameId="orig.01";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;Assert.IsTrue(vm.LightPrintStatus.Contains("就绪"),vm.LightPrintStatus);var before=Pixels(vm.Editor.MatchedImage!);await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.AreEqual("orig.01",vm.ActiveTarget!.RawDefinition!.InstantFilm!.V3!.FrameArtwork!.Id);Assert.AreEqual(before,Pixels(vm.Editor.MatchedImage!));
            var write=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetType("RAWSelectionAssistant.Services.InstantV3DirectExport")!.GetMethod("Write",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!;
            foreach(var format in new[]{StudioExportFormat.Png,StudioExportFormat.Tiff,StudioExportFormat.Jpeg})
            {var output=Path.Combine(root,"frame-"+format+(format==StudioExportFormat.Png?".png":format==StudioExportFormat.Tiff?".tif":".jpg"));var task=(Task)write.Invoke(null,[vm.ActiveTarget.LinearRawMaster!,vm.ActiveTarget.RawDefinition,output,format,CancellationToken.None,null])!;await task;Assert.IsTrue(File.Exists(output));}
            await vm.RefreshPreviewAnalysisAsync(inspectedImage:vm.Editor.MatchedImage);Assert.IsNotNull(vm.PreviewHistogram);
            var session=Path.Combine(root,"frame.ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs());await reopened.LoadSessionAsync(session);Assert.AreEqual(before,Pixels(reopened.Editor.MatchedImage!));Assert.AreEqual(vm.ActiveTarget.RawDefinition.InstantFilm.V3.FrameArtwork,reopened.ActiveTarget!.RawDefinition!.InstantFilm!.V3!.FrameArtwork);
        }
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task EveryOriginalFramePreviewDevelopAndPngExportUsesFrozenArtwork()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r54-all-frames-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(Image(root));vm.EnableLightPrintWorkspace();vm.InstantCameraAnimation=false;
            var write=typeof(ReferenceColorWorkspaceViewModel).Assembly.GetType("RAWSelectionAssistant.Services.InstantV3DirectExport")!.GetMethod("Write",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)!;
            foreach(var frame in OriginalFrameLibrary.Frames)
            {if(vm.IsLightPrintLocked)vm.ReshootLightPrintCommand.Execute(null);vm.SelectedLightPrintFrameId=frame.Id;vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;Assert.IsTrue(vm.LightPrintStatus.Contains("就绪"),frame.Id+vm.LightPrintStatus);var preview=Pixels(vm.Editor.MatchedImage!);await vm.DevelopLightPrintCommand.ExecuteAsync(null);Assert.AreEqual(preview,Pixels(vm.Editor.MatchedImage!),frame.Id);var d=vm.ActiveTarget!.RawDefinition!;Assert.AreEqual(OriginalFrameLibrary.Find(frame.Id),d.InstantFilm!.V3!.FrameArtwork);await (Task)write.Invoke(null,[vm.ActiveTarget.LinearRawMaster!,d,Path.Combine(root,frame.Id+".png"),StudioExportFormat.Png,CancellationToken.None,null])!;}
        }finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task ActualProductionControlFourStatesOffscreen()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_R54_VISUAL_ROOT");if(string.IsNullOrEmpty(root)){Assert.Inconclusive("Explicit isolated evidence root required");return;}Directory.CreateDirectory(root);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(Image(root));await vm.CreateR53VariantAsync(true);vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");Assert.IsTrue(vm.Editor.AddParallelGraphBranch());await Settle(vm.Editor);var graphVersion=vm.ActiveTarget!.ActiveEditVersionId!.Value;var page=new PhotoGradePageView{DataContext=vm};
        void Invoke(string name){var m=page.GetType().GetMethod(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;m.Invoke(page,m.GetParameters().Length==0?null:[page,new RoutedEventArgs()]);}
        void Capture(string name,int width){page.Measure(new Size(width,width==1366?768:1080));page.Arrange(new Rect(0,0,width,width==1366?768:1080));page.ApplyR53Layout();page.UpdateLayout();if(vm.R53Professional){var g=(StudioNodeGraphView)((Grid)page.FindName("R53NodeContent")).Children[0];g.Fit();page.UpdateLayout();}var image=new RenderTargetBitmap(width,width==1366?768:1080,96,96,PixelFormats.Pbgra32);image.Render(page);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(root,$"OFFSCREEN-{name}-{width}.png"));encoder.Save(stream);}
        foreach(var width in new[]{1920,1366})
        {
            await vm.SwitchEditVersionAsync(graphVersion);await Settle(vm.Editor);Invoke("R53PresetTab");Invoke("OpenR53Panel");vm.R53Professional=false;((TabControl)page.FindName("EditModes")).SelectedIndex=0;Capture("simple",width);
            vm.R53Professional=true;vm.Layout=vm.Layout with{R54GraphCollapsed=false};page.DataContext=null;page.DataContext=vm;Invoke("OpenR53Panel");Capture("pro",width);
            vm.R53Professional=false;((TabControl)page.FindName("EditModes")).SelectedIndex=1;vm.ReshootLightPrintCommand.Execute(null);vm.SelectedLightPrintFrameId="orig.01";vm.SelectedPhotographerPaperId="itype-color";await vm.LightPrintPreviewWork;Capture("instant",width);
            vm.InstantCameraAnimation=false;await vm.DevelopLightPrintCommand.ExecuteAsync(null);Capture("developed",width);vm.ReshootLightPrintCommand.Execute(null);vm.CancelLightPrintPreviewCommand.Execute(null);await vm.LightPrintPreviewWork;
        }
        page.DataContext=null;
    });
    [TestMethod]public Task IndependentGraphCreate()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_R54_PROCESS_ROOT");if(string.IsNullOrEmpty(root)){Assert.Inconclusive("Explicit isolated process fixture root required");return;}Directory.CreateDirectory(root);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadTargetAsync(Image(root));await vm.CreateR53VariantAsync(true);vm.Editor.AddAdjustmentNodeCommand.Execute("BasicTone");vm.Editor.AddAdjustmentNodeCommand.Execute("Curve");Assert.IsTrue(vm.Editor.AddParallelGraphBranch());vm.Editor.SetToolParameter(ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure"),.6);await Settle(vm.Editor);await vm.SaveSessionAsync(Path.Combine(root,"independent.ptstudio.json"));await File.WriteAllTextAsync(Path.Combine(root,"expected.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,stack=JsonSerializer.Serialize(vm.Editor.AdjustmentStack),pixels=Pixels(vm.Editor.MatchedImage!)}));
    });
    [TestMethod]public Task IndependentGraphReopenAndExport()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Environment.GetEnvironmentVariable("KITAO_R54_PROCESS_ROOT");if(string.IsNullOrEmpty(root)||!File.Exists(Path.Combine(root,"expected.json"))){Assert.Inconclusive("Create in prior isolated testhost first");return;}var expected=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"expected.json"))).RootElement;Assert.AreNotEqual(expected.GetProperty("pid").GetInt32(),Environment.ProcessId);using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs());await vm.LoadSessionAsync(Path.Combine(root,"independent.ptstudio.json"));await Settle(vm.Editor);Assert.AreEqual(expected.GetProperty("stack").GetString(),JsonSerializer.Serialize(vm.Editor.AdjustmentStack));var export=await vm.Editor.ProcessForExportAsync(vm.ActiveTarget!.Path,null,null,CancellationToken.None,vm.Editor.AdjustmentStack);Assert.AreEqual(expected.GetProperty("pixels").GetString(),Pixels(export));await File.WriteAllTextAsync(Path.Combine(root,"readback.json"),JsonSerializer.Serialize(new{pid=Environment.ProcessId,sourcePid=expected.GetProperty("pid").GetInt32(),stackExact=true,exportPixelsExact=true,scope="independent testhost, not physical mouse/DPI"}));
    });
    sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string t,string f,bool m)=>[];public string? ChooseFolder(string t,string? initialDirectory=null)=>null;public string? ChooseSaveFile(string t,string f,string e,string? suggestedFileName=null)=>null;public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public void ShowInfo(string m){}public void ShowError(string m){}public bool Confirm(string m,string t)=>false;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> c)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem i,bool b)=>false;public void RevealFile(string p){}
    }
}

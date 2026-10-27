using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class R53CompletionTests
{
    static ColorStudioToolParameter Exposure=>ColorStudioToolCatalog.GetParameters(ColorStudioNodeType.BasicTone).Single(p=>p.Key=="exposure");
    static async Task Render(ReferenceColorWorkspaceViewModel vm){await vm.Editor.ApplyCommand.ExecuteAsync(null);await vm.AnalysisWork;Assert.IsNotNull(vm.Editor.MatchedImage,vm.Editor.StatusText+" / "+vm.Editor.LastRenderFailure);}
    static byte[] Pixels(BitmapSource image){var rgb=new FormatConvertedBitmap(image,PixelFormats.Rgb24,null,0);var b=new byte[rgb.PixelWidth*rgb.PixelHeight*3];rgb.CopyPixels(b,rgb.PixelWidth*3,0);return b;}
    static string Input(string root){var file=Path.Combine(root,"synthetic.png");StudioQuickExport.Encode(BitmapSource.Create(64,48,96,96,PixelFormats.Rgb24,null,Enumerable.Repeat((byte)80,64*48*3).ToArray(),64*3),file,file,StudioExportFormat.Png);return file;}
    [TestMethod]public Task MaskOpacityUndoLockVariantIsolationSaveReopenAndRealExport()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-complete-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Input(root));vm.Editor.SetToolParameter(Exposure,1);Assert.AreEqual(ColorStudioNodeType.BasicTone,vm.Editor.SelectedAdjustmentNode?.Type);vm.Editor.SelectedLayerOpacity=.5;vm.Editor.AddMaskStroke(new([new(.25,.5)],.2,.5,1,1));await Render(vm);Assert.IsNotNull(vm.Editor.SelectedAdjustmentNode!.LocalMask);var first=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);var pixels=Pixels(vm.Editor.MatchedImage!);Assert.IsTrue(pixels.Distinct().Count()>1);vm.Editor.InvertLocalMask();vm.Editor.UndoAdjustmentCommand.Execute(null);await Render(vm);Assert.AreEqual(first,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));vm.Editor.SelectedNodeLocked=true;var locked=JsonSerializer.Serialize(vm.Editor.AdjustmentStack);vm.Editor.ClearLocalMask();vm.Editor.SelectedLayerOpacity=0;Assert.AreEqual(locked,JsonSerializer.Serialize(vm.Editor.AdjustmentStack));vm.Editor.SelectedNodeLocked=false;var original=vm.ActiveTarget!.ActiveEditVersionId;await vm.CreateR53VariantAsync(false);vm.Editor.InvertLocalMask();await vm.SwitchEditVersionAsync(original!.Value);await Render(vm);CollectionAssert.AreEqual(pixels,Pixels(vm.Editor.MatchedImage!));var path=Path.Combine(root,"mask.ptstudio.json");await vm.SaveSessionAsync(path);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await reopened.LoadSessionAsync(path);await Render(reopened);CollectionAssert.AreEqual(pixels,Pixels(reopened.Editor.MatchedImage!));reopened.ActiveTarget!.IsSelected=true;await reopened.ExportSelectedCommand.ExecuteAsync(null);var exported=Directory.GetFiles(root,"*_仿色.png").Single();CollectionAssert.AreEqual(pixels,Pixels(StudioQuickExport.Load(exported)));var view=new PhotoGradePageView{DataContext=reopened};view.Measure(new Size(1100,800));view.Arrange(new Rect(0,0,1100,800));view.UpdateLayout();Assert.IsNotNull(view.FindName("MaskTools"));view.DataContext=null;}
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task VariantComparisonDoesNotMutateMainAndLatestRequestWins()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-compare-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Input(root));vm.Editor.SetToolParameter(Exposure,.3);await vm.CreateR53VariantAsync(false);vm.Editor.SetToolParameter(Exposure,.8);await vm.CreateR53VariantAsync(false);vm.Editor.SetToolParameter(Exposure,-.3);var ids=vm.R53Variants.Take(4).Select(v=>v.Id).ToArray();var main=vm.R53MainVariantId;var selected=vm.ActiveTarget!.ActiveEditVersionId;await vm.PrepareR53VariantComparisonAsync(ids);Assert.AreEqual(ids.Length,vm.R53CompareFrames.Count);Assert.IsTrue(vm.R53CompareFrames.Select(f=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Pixels(f.Image)))).Distinct().Count()>1,"Each variant must render its own definition.");Assert.AreEqual(main,vm.R53MainVariantId);Assert.AreEqual(selected,vm.ActiveTarget.ActiveEditVersionId);Assert.IsTrue(vm.R53CompareFrames.All(f=>Math.Max(f.Image.PixelWidth,f.Image.PixelHeight)<=1200));var a=vm.PrepareR53VariantComparisonAsync(ids);var b=vm.PrepareR53VariantComparisonAsync(ids.Take(2).ToArray());await Task.WhenAll(a,b);Assert.AreEqual(2,vm.R53CompareFrames.Count);vm.CloseR53VariantComparison();Assert.IsEmpty(vm.R53CompareFrames);}
        finally{Directory.Delete(root,true);}
    });
    [TestMethod]public Task ResourceFavoritesRecentsScopeToggleAndSharedLayerControlsPersist()=>RunSta(async()=>
    {
        EnsureTestApplication();var root=Path.Combine(Path.GetTempPath(),"r53-resources-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using var vm=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await vm.LoadTargetAsync(Input(root));vm.Editor.SetToolParameter(Exposure,.3);vm.RecordR53Resource("基础调色");vm.RecordR53Resource("曲线");vm.RecordR53Resource("基础调色");vm.ToggleR53Favorite("曲线");
            CollectionAssert.AreEqual(new[]{"基础调色","曲线"},vm.R53RecentResources.ToArray());Assert.IsTrue(vm.R53FavoriteResources.Contains("曲线"));
            var view=new PhotoGradePageView{DataContext=vm};view.Measure(new Size(1100,800));view.Arrange(new Rect(0,0,1100,800));view.UpdateLayout();
            var search=(System.Windows.Controls.TextBox)view.FindName("R53ResourceSearch");
            Assert.IsNotNull(search);search.Text="基础";var resource=(System.Windows.Controls.StackPanel)view.FindName("R53ResourceContent");Assert.IsTrue(resource.Children.Count>0);Assert.IsTrue(resource.Children.OfType<System.Windows.Controls.DockPanel>().SelectMany(x=>x.Children.OfType<System.Windows.Controls.Button>()).Where(x=>!Equals(x.Content,"☆")&&!Equals(x.Content,"★")).All(x=>x.Content.ToString()!.Contains("基础")));
            var method=typeof(PhotoGradePageView).GetMethod("R53ToggleScopes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;method.Invoke(view,new object[]{view,new RoutedEventArgs()});
            Assert.AreEqual(Visibility.Collapsed,((FrameworkElement)view.FindName("HistogramPanel")).Visibility);
            var tabs=(System.Windows.Controls.TabControl)view.FindName("EditModes");tabs.SelectedIndex=2;tabs.SelectedIndex=0;Assert.AreEqual(Visibility.Collapsed,((FrameworkElement)view.FindName("HistogramPanel")).Visibility);
            vm.R53Professional=true;vm.Editor.SelectedLayerBlend=StudioLayerBlend.Multiply;vm.Editor.SelectedLayerOpacity=.4;vm.R53Professional=false;Assert.AreEqual(.4,vm.Editor.SelectedLayerOpacity);Assert.AreEqual(StudioLayerBlend.Multiply,vm.Editor.SelectedLayerBlend);
            var session=Path.Combine(root,"resources.ptstudio.json");await vm.SaveSessionAsync(session);using var reopened=new ReferenceColorWorkspaceViewModel(new Dialogs(root));await reopened.LoadSessionAsync(session);CollectionAssert.AreEqual(vm.R53RecentResources.ToArray(),reopened.R53RecentResources.ToArray());CollectionAssert.AreEqual(vm.R53FavoriteResources.ToArray(),reopened.R53FavoriteResources.ToArray());view.DataContext=null;
        }finally{Directory.Delete(root,true);}
    });
    sealed class Dialogs(string root):IDialogService{public IReadOnlyList<string> ChooseFiles(string a,string b,bool c)=>[];public string? ChooseFolder(string a,string? b=null)=>root;public string? ChooseSaveFile(string a,string b,string c,string? d=null)=>null;public bool Confirm(string a,string b)=>true;public void ShowInfo(string a){}public void ShowError(string a){}public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> ids)=>ids;public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> x)=>null;public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem x,bool y)=>false;public void RevealFile(string p){}}
}

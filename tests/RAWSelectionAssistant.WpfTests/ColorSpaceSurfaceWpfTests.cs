using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.ViewModels;
using RAWSelectionAssistant.Views;
using RAWSelectionAssistant.Services;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class ColorSpaceSurfaceWpfTests
{
    [TestMethod]
    public Task EmptySphereRendersContinuousColorsAndRotationsActuallyChangeSurface() => RunSta(()=>
    {
        var view=new ColorSpace3DViewport { Width=360,Height=360,ViewSettings=new() { ShowGrid=false,ShowAxes=false,SurfaceOpacity=1 } };
        var first=Render(view);Assert.IsFalse(view.IsAvailable,"Empty reference sphere must not invent image samples.");
        var distinct=new HashSet<int>();for(var y=50;y<300;y++)for(var x=50;x<300;x++){var p=(y*360+x)*4;distinct.Add(first[p]|first[p+1]<<8|first[p+2]<<16);}
        Assert.IsGreaterThan(10000,distinct.Count,"A few colored patches are not a continuous sphere surface.");
        view.ViewSettings=view.ViewSettings with{RotationZ=70,RotationY=80};var rotated=Render(view);
        Assert.IsGreaterThan(10000,first.Zip(rotated).Count(p=>p.First!=p.Second));
        view.ViewSettings=view.ViewSettings with{SurfaceOpacity=0};var hidden=Render(view);
        Assert.AreNotEqual(BitConverter.ToInt32(rotated,180*360*4+180*4),BitConverter.ToInt32(hidden,180*360*4+180*4));
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task ExactRareImageColorMapsEvenWhenGridDidNotSampleItAndAlphaIsRespected() => RunSta(async()=>
    {
        using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs());
        var pixels=new byte[64*64*4];for(var p=0;p<4096;p++){pixels[p*4]=255;pixels[p*4+3]=255;}
        pixels[0]=0;pixels[2]=255;pixels[4]=0;pixels[6]=255;pixels[7]=0;
        var image=BitmapSource.Create(64,64,96,96,PixelFormats.Bgra32,null,pixels,256);image.Freeze();
        workspace.SamplingTier=ColorSpaceSamplingTier.Preview;await workspace.Editor.SetSourceAsync(null,image);await workspace.RefreshPreviewAnalysisAsync();
        var stack=workspace.Editor.AdjustmentStack;workspace.HighlightImageSample(new(255,0,0));
        CollectionAssert.AreEqual(new[]{0},workspace.HighlightedPixels.ToArray());Assert.AreEqual((byte)255,workspace.HighlightWeights[0]);Assert.AreEqual((byte)0,workspace.HighlightWeights[1]);
        Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
        workspace.SelectionTolerance=.08;workspace.SelectionSoftness=.8;
        CollectionAssert.AreEqual(new[]{0},workspace.HighlightedPixels.ToArray(),"Range/softness recompute the exact picked color.");
        workspace.CloudRotationX=100;workspace.CloudSurfaceOpacity=.2;workspace.CloudChromaMin=.2;Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
        await workspace.Editor.SetSourceAsync(null,null);await workspace.AnalysisWork;Assert.IsEmpty(workspace.HighlightWeights);Assert.IsNull(workspace.PreviewHistogram);
    });
    [TestMethod]
    public Task HistogramReadoutReflectsIndependentRgbAndLinearLumaChannels() => RunSta(()=>
    {
        var histogram=VisualAnalysisEngine.AnalyzeHistogram(new(2,1,new byte[]{255,0,0,0,255,0}));
        var rgb=new StudioHistogramView{Histogram=histogram};var luma=new StudioHistogramView{Histogram=histogram,Channel="亮度"};
        rgb.UpdateReadout(255);StringAssert.Contains(rgb.Readout,"R 1");StringAssert.Contains(rgb.Readout,"G 1");
        luma.UpdateReadout(54);StringAssert.Contains(luma.Readout,"Y 0.212: 1");
        rgb.Channel="B";rgb.UpdateReadout(0);StringAssert.Contains(rgb.Readout,"B 0: 2");
        return Task.CompletedTask;
    });
    [TestMethod]
    public Task InspectionViewSettingsDoNotChangeStackAndStaySynchronizedInBothPanels() => RunSta(async()=>
    {
        EnsureTestApplication();using var workspace=new ReferenceColorWorkspaceViewModel(new Dialogs());
        var controls=new StudioSpaceControls{DataContext=workspace};var appearance=new StudioSpaceAppearanceControls{DataContext=workspace};
        var root=new System.Windows.Controls.StackPanel();root.Children.Add(controls);root.Children.Add(appearance);
        root.Measure(new(350,1500));root.Arrange(new(0,0,350,1500));await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);
        var stack=workspace.Editor.AdjustmentStack;
        var slider=controls.Children.OfType<System.Windows.Controls.Slider>().Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Z 旋转 · 度");slider.Value=45;
        Assert.AreEqual(45,workspace.CloudSettings.RotationZ);
        appearance.Children.OfType<System.Windows.Controls.CheckBox>().Single(x=>x.Content?.ToString()=="显示网格").IsChecked=false;
        Assert.IsFalse(workspace.CloudSettings.ShowGrid);Assert.AreSame(stack,workspace.Editor.AdjustmentStack);
        var view=new ColorSpace3DViewport{DataContext=workspace};view.SetBinding(ColorSpace3DViewport.ViewSettingsProperty,new System.Windows.Data.Binding("CloudSettings"));
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.DataBind);Assert.AreEqual(45,view.ViewSettings.RotationZ);
        workspace.CloudRotationZ=-33;Assert.AreEqual(-33,view.ViewSettings.RotationZ);
    });
    private static byte[] Render(FrameworkElement view)
    {
        view.Measure(new(360,360));view.Arrange(new(0,0,360,360));view.UpdateLayout();
        var bmp=new RenderTargetBitmap(360,360,96,96,PixelFormats.Pbgra32);bmp.Render(view);var pixels=new byte[360*360*4];bmp.CopyPixels(pixels,1440,0);return pixels;
    }
    private sealed class Dialogs:IDialogService
    {
        public IReadOnlyList<string> ChooseFiles(string title,string filter,bool multiselect)=>[];
        public string? ChooseFolder(string title,string? initialDirectory=null)=>null;
        public string? ChooseSaveFile(string title,string filter,string defaultExtension,string? suggestedFileName=null)=>null;
        public IReadOnlyList<string>? ManageQuickTools(IReadOnlyList<string> currentToolIds)=>currentToolIds;
        public void ShowInfo(string message){}public void ShowError(string message){}public bool Confirm(string message,string title)=>false;
        public HelpAction ShowHelp()=>HelpAction.None;public void ShowFeedback(){}
        public RAWSelectionAssistant.Core.Models.RawFileEntry? ChooseRawCandidate(IReadOnlyList<RAWSelectionAssistant.Core.Models.RawFileEntry> candidates)=>null;
        public bool ShowMediaDetails(RAWSelectionAssistant.Core.Models.MediaSelectionItem item,bool showAdvancedDetails)=>false;
        public void RevealFile(string path){}
    }
}

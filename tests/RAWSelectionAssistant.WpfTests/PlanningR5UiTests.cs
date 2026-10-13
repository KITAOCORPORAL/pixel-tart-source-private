using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class PlanningR5UiTests
{
    private static void Arrange(FrameworkElement view,double width,double height)
    {view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();}
    private static IEnumerable<T> Walk<T>(DependencyObject root) where T:DependencyObject
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var nested in Walk<T>(child))yield return nested;}}
    private static void SaveState(FrameworkElement view,string name)
    {
        var output=Environment.GetEnvironmentVariable("KITAO_R5_SCREENSHOTS");if(output is null)return;Directory.CreateDirectory(output);
        var bmp=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(Path.Combine(output,name+".png"));png.Save(stream);
    }
    [TestMethod]
    public Task WidthMemoryDrawerRestoreAndInspectorWritesUseRealSaveGate()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);await f.Vm.NewShotCommand.ExecuteAsync(null);EnsureTestApplication();
        Assert.IsTrue(await f.Vm.SaveUiLayoutAsync(new(315,355,false,["拍摄信息"])));await f.Vm.LoadAsync(f.Id);Assert.AreEqual(315d,f.Vm.UiLayout.ListWidth);Assert.AreEqual(355d,f.Vm.UiLayout.InspectorWidth);Assert.IsFalse(f.Vm.UiLayout.InspectorOpen);
        Assert.IsTrue(await f.Vm.SaveUiLayoutAsync(f.Vm.UiLayout with{InspectorOpen=true}));
        var view=new PlanningCenterView{DataContext=f.Vm};await f.Vm.SelectContentSafelyAsync("镜头清单");view.Render();Arrange(view,1720,1000);
        Assert.AreEqual(355d,((ColumnDefinition)view.FindName("PlanningInspectorColumn")).Width.Value);
        var inspector=(StackPanel)view.FindName("PlanningInspectorContent");var name=Walk<TextBox>(inspector).Single(b=>AutomationProperties.GetName(b)=="镜头名称");name.Text="检查器实际修改";
        Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual("检查器实际修改",(await f.Shots.LoadAsync(f.Id)).Shots.Single().Name);
        Arrange(view,760,600);Assert.IsTrue(view.InspectorIsDrawer);Assert.AreEqual(0d,((ColumnDefinition)view.FindName("SectionOutlineColumn")).Width.Value);
        var pane=(FrameworkElement)view.FindName("PlanningInspectorPane");view.OpenPlanningDock(pane,true);
        Assert.AreEqual(Visibility.Visible,((Grid)view.FindName("PlanningDockOverlay")).Visibility);Assert.IsFalse(((Grid)view.FindName("WorkspaceLayout")).IsEnabled);Arrange(view,760,600);SaveState(view,"state-inspector-drawer");
        view.ClosePlanningDock();Assert.AreEqual(2,Grid.GetColumn(pane));Assert.IsTrue(((Grid)view.FindName("WorkspaceLayout")).IsEnabled);
        view.OpenPlanningDock((FrameworkElement)view.FindName("ContentNavigationBorder"),false);Arrange(view,760,600);SaveState(view,"state-section-drawer");view.ClosePlanningDock();
        Arrange(view,1720,1000);Assert.AreEqual(355d,((ColumnDefinition)view.FindName("PlanningInspectorColumn")).Width.Value);
        var id=f.Vm.Shots[0].ShotId;f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name=""});Assert.IsFalse(await f.Vm.SaveUiLayoutAsync(new(250,280)));Assert.AreEqual(315d,f.Vm.UiLayout.ListWidth);
        f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name="修复"});Assert.IsTrue(await f.Vm.FlushAsync());view.DataContext=null;
    });
    [TestMethod]
    public Task ResourceDialogCancelFailureAndRetryUseRealDataWithoutFakeEntries()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);EnsureTestApplication();
        await f.Vm.SaveResourceAsync(new PlanningGearItem{Name="真实手动设备",Model="相机A"});
        var view=new PlanningCenterView{DataContext=f.Vm};await f.Vm.SelectContentSafelyAsync("设备");view.Render();Arrange(view,1166,712);
        var content=(StackPanel)view.FindName("DocumentContent");
        var buttons=Walk<Button>(content).ToArray();Assert.IsFalse(buttons.Any(b=>Convert.ToString(b.Content)?.Contains("工具箱导入")==true));
        buttons.Single(b=>Convert.ToString(b.Content)=="新增").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Arrange(view,1166,712);var modal=(StackPanel)view.FindName("ModalContent");var name=Walk<TextBox>(modal).Single(b=>AutomationProperties.GetName(b)=="名称");name.Text="";
        Walk<Button>(modal).Single(b=>Convert.ToString(b.Content)=="保存").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(Visibility.Visible,((FrameworkElement)view.FindName("ModalOverlay")).Visibility);Assert.HasCount(1,f.Vm.ResourceCatalog.Items);Arrange(view,1166,712);SaveState(view,"state-resource-validation-error");
        Walk<Button>(modal).Single(b=>Convert.ToString(b.Content)=="取消").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.HasCount(1,f.Vm.ResourceCatalog.Items);
        var plan=Path.Combine(f.Root,"planning",$"{f.Id:N}.planning.json");
        using(var held=new FileStream(plan,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){f.Vm.DocumentBody="待恢复内容";Assert.IsFalse(await f.Vm.FlushAsync());Assert.AreEqual(Visibility.Visible,((Button)view.FindName("PlanningSaveRetry")).Visibility);Arrange(view,1166,712);SaveState(view,"state-save-failure");}
        Assert.IsTrue(await f.Vm.FlushAsync());Assert.AreEqual(Visibility.Collapsed,((Button)view.FindName("PlanningSaveRetry")).Visibility);view.DataContext=null;
    });
    [TestMethod]
    public Task AllSectionsOffscreenMatrixAndSyntheticSnapshots()=>RunSta(async()=>
    {
        using var f=await PlanningFoundationWpfTests.Fixture.Create();await f.Vm.LoadAsync(f.Id);EnsureTestApplication();
        f.Vm.DocumentTitle="夏日胶片人像 · 合成验收";f.Vm.DocumentLocation="合成海边场地";f.Vm.DocumentPeople="合成团队";f.Vm.ShootGoal="柔和逆光与夏季人像";f.Vm.Keywords="胶片 / 海风 / 留白";f.Vm.DocumentBody="这是合成摄影提案。所有人物与地点均为测试数据。";f.Vm.DocumentCustomBody="拍摄注意事项与补充文档内容";
        await f.Vm.NewShotCommand.ExecuteAsync(null);var id=f.Vm.SelectedShot!.ShotId;f.Vm.StageShot(f.Vm.ShotDraft(id) with{Name="草帽逆光半身",Scene="草坡",ShotSize="中景",Action="回望",CameraAndLens="85mm / f2"});Assert.IsTrue(await f.Vm.FlushAsync());
        PlanningResource[] resources=[new PlanningProp{Name="草帽",Preparation="已备齐"},new PlanningGearItem{Name="相机 A",Model="合成设备"},new PlanningCrewMember{Name="人员 A",Role="摄影",ArrivalLocal="09:00"},new PlanningVenue{Name="合成场地",Address="测试地址",SunsetLocal="18:00",ObservationSource="合成手动值"},new PlanningScheduleSlot{Name="集合",Owner="摄影"},new PlanningBudgetLine{Name="道具预算",Planned=300},new PlanningLight{Name="主灯",X=.2,Y=.35,DirectionDegrees=35}];
        foreach(var resource in resources)Assert.IsTrue(await f.Vm.SaveResourceAsync(resource with{ShotIds=[id]}));
        var fixtureImage=Path.Combine(f.Root,"synthetic-reference.png");
        var bitmap=BitmapSource.Create(120,80,96,96,PixelFormats.Bgra32,null,Enumerable.Range(0,120*80).SelectMany(i=>new byte[]{(byte)(90+i%120),(byte)140,(byte)180,(byte)255}).ToArray(),120*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(fixtureImage))encoder.Save(file);
        await f.Vm.AddDocumentReferencesAsync([new ProjectShotReference(Guid.NewGuid(),ShotReferenceKind.General,ExternalReference:fixtureImage,Title:"合成参考色块")]);
        await f.Vm.RefreshDocumentReferencesAsync();if(f.Vm.AllProjectReferences.FirstOrDefault() is {} reference)await f.Vm.UpdateReferenceAsync(reference,"情绪板");
        var attachment=Path.Combine(f.Root,"synthetic-brief.txt");await File.WriteAllTextAsync(attachment,"合成拍摄说明，不含客户资料");await f.Vm.AddAttachmentsAsync([attachment]);
        var view=new PlanningCenterView{DataContext=f.Vm};var output=Environment.GetEnvironmentVariable("KITAO_R5_SCREENSHOTS");if(output is not null)Directory.CreateDirectory(output);
        var matrix=new List<object>();var sections=RAWSelectionAssistant.ViewModels.PlanningCenterViewModel.ContentPages.ToArray();
        foreach(var size in new[]{new Size(1920,1080),new Size(1366,768)})foreach(var scale in new[]{1d,1.25,1.5,1.75,2d})
        {
            // Approximate unchanged shell allocation. This is logical DIP simulation, NOT OS DPI approval.
            var shell=(size.Width/scale>=1200?200:56);var width=size.Width/scale-shell;var height=size.Height/scale-56;
            for(var i=0;i<=sections.Length;i++)
            {
                var advanced=i==sections.Length;Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(advanced));if(!advanced)Assert.IsTrue(await f.Vm.SelectContentSafelyAsync(sections[i]));view.Render();Arrange(view,width,height);
                var content=(FrameworkElement)view.FindName("DocumentScroll");Assert.IsTrue(content.ActualWidth>150,$"{i}/{size}/{scale}: {content.ActualWidth}");
                Assert.IsTrue(view.DesiredSize.Width<=width+.5);Assert.IsTrue(view.DesiredSize.Height<=height+.5);
                var tag=$"{(advanced?"advanced":$"section-{i:00}")}_{size.Width:0}x{size.Height:0}_{scale*100:0}";
                if(output is not null)
                {
                    var image=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);image.Render(view);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(output,tag+".png"));png.Save(stream);
                }
                matrix.Add(new{section=advanced?"进阶版":sections[i],physicalWidth=size.Width,physicalHeight=size.Height,simulatedScale=scale,width,height,contentWidth=content.ActualWidth,inspectorDrawer=view.InspectorIsDrawer,screenshot=tag+".png",nativeDpi="NOT_RUN"});
            }
        }
        if(output is not null)await File.WriteAllTextAsync(Path.Combine(output,"matrix.json"),JsonSerializer.Serialize(matrix,new JsonSerializerOptions{WriteIndented=true}));
        Assert.IsTrue(await f.Vm.SwitchPlanningModeAsync(false));view.DataContext=null;
    });
}

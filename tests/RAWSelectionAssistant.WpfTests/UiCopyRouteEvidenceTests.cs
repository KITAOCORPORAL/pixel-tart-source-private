using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.ViewModels;
namespace RAWSelectionAssistant.WpfTests;
[TestClass][DoNotParallelize]public sealed class UiCopyRouteEvidenceTests
{
    [TestMethod]public void CurrentProductionRoutesCaptureSyntheticStatesWithoutNativeClaim()
    {
        var output=Environment.GetEnvironmentVariable("KITAO_COPY_ROUTE_EVIDENCE");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Explicit isolated route evidence required");return;}
        Exception? failure=null;var complete=false;var thread=new Thread(()=>
        {
            try
            {
                var app=new App();app.InitializeComponent();bool running=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=async(_,_)=>
                {
                    if(running||app.MainWindow is not MainWindow{IsLoaded:true,DataContext:MainViewModel vm} window)return;running=true;
                    try
                    {
                        vm.ForceExitTutorial();window.WindowState=WindowState.Normal;Directory.CreateDirectory(output);var rows=new List<object>();var selected=Environment.GetEnvironmentVariable("KITAO_COPY_ROUTES")?.Split(',')??["Workbench","Workflow","LocalSplit","WorkCalendar","Planning","AssetLibrary","ReferenceColor","Tether","OnlineSelection","Finance","History","Toolbox","Publishing","RawToJpeg","PhotoGrouping","Collage","BatchCompress","Settings","Help","Activation"];
                        foreach(var route in selected)
                        {
                            vm.NavigateCommand.Execute(route);await Task.Delay(350);if(route!="Settings")Assert.AreEqual(route,vm.CurrentPage,route);
                            foreach(var width in route is "Planning" or "Settings" or "Tether" or "Toolbox"?new[]{1920,1180}:new[]{1600})
                            {
                                window.Width=width;window.Height=920;window.UpdateLayout();await Task.Delay(120);var root=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var path=Path.Combine(output,route+"-"+width+".png");var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);rows.Add(new{route,width,path,scope="IN_PROCESS_WPF_RENDER, not desktop screenshot"});
                            }
                            if(route=="Planning"&&vm.PlanningPage is {} planning)
                            {
                                planning.NewPlanningName="合成策划 A";await planning.CreatePlanningCommand.ExecuteAsync(null);window.Width=1920;window.UpdateLayout();await Task.Delay(150);var root=(FrameworkElement)window.Content;var image=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(Path.Combine(output,"Planning-project-1920.png"));encoder.Save(file);
                            }
                            if(route=="Settings")vm.CloseSettingsCommand.Execute(null);
                        }
                        await File.WriteAllTextAsync(Path.Combine(output,"ROUTES.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));complete=true;
                    }
                    catch(Exception e){failure=e;}finally{timer.Stop();app.Shutdown();}
                };timer.Start();app.Run();
            }
            catch(Exception e){failure=e;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(4)));if(failure is not null)throw failure;Assert.IsTrue(complete);
    }
}

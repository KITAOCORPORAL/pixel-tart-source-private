using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Views;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class BatchTenCameraKeyframesTests
{
    [TestMethod]
    public void OffscreenSyntheticKeyframesNeverLaunchDesktop()
    {
        var output=Environment.GetEnvironmentVariable("KITAO_BATCH10_FRAMES");if(string.IsNullOrWhiteSpace(output)){Assert.Inconclusive("Offscreen artifact output opt-in required.");return;}
        Exception? error=null;var thread=new Thread(()=>{try
        {
            Directory.CreateDirectory(output);var pixels=new byte[460*530*3];
            for(var y=0;y<530;y++)for(var x=0;x<460;x++){var i=(y*460+x)*3;var image=x>=30&&x<430&&y>=30&&y<430;pixels[i]=image?(byte)(40+x/3):(byte)240;pixels[i+1]=image?(byte)(40+y/3):(byte)235;pixels[i+2]=image?(byte)110:(byte)218;}
            var bitmap=BitmapSource.Create(460,530,96,96,PixelFormats.Rgb24,null,pixels,460*3);bitmap.Freeze();
            foreach(var model in new[]{"SX-70","i-2"})
            {
                Save(new InstantCameraArtwork{Model=model},Path.Combine(output,model+"-entry.png"),460,320);
                foreach(var state in new[]{("half",.75),("full",1.25),("central-start",1.35),("central-mid",3.35),("complete",5.35)})
                {
                    var frame=InstantDevelopmentTimeline.Sample(state.Item2,true);Assert.AreEqual(state.Item2<1.35,frame.CameraVisible);
                    Save(new InstantDevelopmentView{Camera=model,Image=bitmap,ImageRegion=new Rect(30d/460,30d/530,400d/460,400d/530),Frame=frame},Path.Combine(output,model+"-"+state.Item1+".png"),900,760);
                }
            }
        }catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)throw error;
    }
    private static void Save(FrameworkElement view,string path,int width,int height)
    {
        view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(path);encoder.Save(stream);
    }
}

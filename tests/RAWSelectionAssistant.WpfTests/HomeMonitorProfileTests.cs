using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RAWSelectionAssistant.Services;
using RAWSelectionAssistant.Views;
using static RAWSelectionAssistant.WpfTests.RuntimeCorrectionWpfTests;
namespace RAWSelectionAssistant.WpfTests;
[TestClass]
public sealed class HomeMonitorProfileTests
{
 [TestMethod] public void NoWindowDoesNotPretendMonitorCalibrationOrConversion()
 {var value=WindowsMonitorProfileProbe.ObserveWindow(IntPtr.Zero);Assert.AreEqual("NoWindow",value.State);Assert.AreEqual("NotAppliedByColorStudio",value.PreviewConversion);StringAssert.Contains(ColorStudioImageViewport.DescribeProfileAssociation(value),"not applied");Assert.IsNull(value.ProfileSha256);}
 [TestMethod] public Task AssociatedProfileCorruptionAndMissingAreExplicitAndDoNotTouchPixels()=>RunSta(()=>
 {
  var root=Directory.CreateTempSubdirectory("home-monitor-profile-").FullName;
  try
  {
   var missing=WindowsMonitorProfileProbe.InspectAssociation("synthetic",Path.Combine(root,"missing.icc"));Assert.AreEqual("ProfileUnreadable",missing.State);
   var path=Path.Combine(root,"sRGB.icc");File.WriteAllBytes(path,StudioQuickExport.SrgbProfileBytes());var good=WindowsMonitorProfileProbe.InspectAssociation("synthetic",path);Assert.AreEqual("AssociatedRgbProfileHeaderVerified",good.State);Assert.IsNotNull(good.ProfileSha256);
   File.WriteAllBytes(path,new byte[160]);Assert.AreEqual("InvalidProfile",WindowsMonitorProfileProbe.InspectAssociation("synthetic",path).State);
   var image=BitmapSource.Create(2,1,96,96,PixelFormats.Rgb24,null,new byte[]{0,127,255,255,80,10},6);image.Freeze();var view=new ColorStudioImageViewport{Original=image,Matched=image};Assert.AreSame(image,view.Original);Assert.AreSame(image,view.Matched);Assert.AreEqual("NotObserved",view.DisplayProfileObservation.State);
  }finally{Directory.Delete(root,true);}return Task.CompletedTask;
 });
}

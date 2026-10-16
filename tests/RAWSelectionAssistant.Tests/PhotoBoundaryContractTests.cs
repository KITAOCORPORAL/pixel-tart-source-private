using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class PhotoBoundaryContractTests
{
 [TestMethod] public void ProcessedRgbIsNotSensorAndProPhotoConventionIsNotIccD50()
 {
  var contract=new PhotoBoundaryContract("PhotoRaw0222Research1",PhotoBoundaryStage.RenderedWorking,"LibRaw Linear ProPhoto D65",PhotoTransfer.Linear,PhotoWhitePoint.D65,16,true,true,true,true,0,"Pinned source/native identity required");
  Assert.AreSame(contract,contract.Validate());contract.RequireExportSource();
  Assert.Throws<InvalidDataException>(()=>(contract with{Stage=PhotoBoundaryStage.SensorUnpacked}).Validate());
  Assert.Throws<InvalidDataException>(()=>(contract with{WhitePoint=PhotoWhitePoint.PcsD50}).Validate());
  Assert.Throws<InvalidDataException>(()=>contract.RequireWhiteBalanceNotAlreadyBaked());
 }
 [TestMethod] public void MonitorPixelsCannotReturnToExportAndAdaptationCannotRepeat()
 {
  var contract=new PhotoBoundaryContract("DisplaySrgb16ToMonitorV1",PhotoBoundaryStage.DisplayDevicePreview,"monitor",PhotoTransfer.DeviceProfile,PhotoWhitePoint.DeviceProfile,16,true,true,true,true,1,"Explicit monitor profile hash");
  contract.Validate();Assert.Throws<InvalidDataException>(()=>contract.RequireExportSource());
  Assert.Throws<InvalidDataException>(()=>contract.RequireNoPriorChromaticAdaptation());
  Assert.Throws<InvalidDataException>(()=>(contract with{ChromaticAdaptations=2}).Validate());
 }
}

using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorScience;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class NightColorBaselineTests
{
 [TestMethod] public void DiagnosticsRecordNegativeExtendedFiniteAndRejectUnknownIdentity()
 {
  var source=new float[]{-.2f,.18f,1.3f,0,1,2};var s=PixelRangeStatistics.Measure(source);CollectionAssert.AreEqual(new long[]{1,0,0},s.Negative);CollectionAssert.AreEqual(new long[]{0,0,2},s.AboveOne);Assert.AreEqual(2,s.Pixels);
  var d=new ProfessionalRawDefinition();var m=new ProfessionalRawMaster(new(2,1,source,"32f",ProfessionalRawPipeline.WorkingSpace),new("Synthetic","None","None",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));
  var result=PixelPipelineDiagnostics.Explain(m,d,new string('b',64));Assert.IsFalse(result.CalibrationEligible);Assert.AreEqual(2,result.Stages.Count);Assert.AreEqual("Linear",result.Stages[0].Transfer);Assert.AreEqual(0L,result.Stages[1].Range.NonFinite);Assert.Throws<InvalidDataException>(()=>PixelPipelineDiagnostics.Explain(m,d,"unknown"));
  var probe=PixelPipelineDiagnostics.ProbeTransform(source,new(1,0,0,0,1,0,0,0,1),"before-clipping");Assert.AreEqual(s.Maximum,probe.Range.Maximum);StringAssert.Contains(probe.Limitations,"Not an observation");
 }
 [TestMethod] public void SensorProbePreservesExtendedValuesAndCountsActualChannelSaturation()
 {
  var result=LinearInputProbe.Measure(new float[]{50,150,1000,1200,250,100},[100,100,100],1000,new(2,1,3),new(1,0,0,0,1,0,0,0,1));
  CollectionAssert.AreEqual(new long[]{1,0,1},result.AtSensorWhite);Assert.IsTrue(result.AfterBlack.Minimum<0);Assert.IsTrue(result.AfterWhiteBalance.Maximum>1);Assert.AreEqual(result.AfterWhiteBalance.Maximum,result.AfterMatrix.Maximum);Assert.Throws<ArgumentException>(()=>LinearInputProbe.Measure([1,2,3],[10,10,10],5,new(1,1,1),new()));
 }
 [TestMethod] public void AcquisitionProtocolNeverAdmitsCalibrationFromMetadataAlone()
 {
  var sample=new CameraChartCaptureV1("scene-1",new string('a',64),"A7M4","Uncompressed","50mm","D65","Measured SPD",0,"Measured chart revision",new string('b',64),"D50 measured reference",2,"Exclude reflections/shadows","Holdout","CIEDE2000 D50 2deg","Uncertainty pending physical review");
  var good=CameraChartCaptureValidator.Validate(sample);Assert.IsTrue(good.MetadataComplete);Assert.IsFalse(good.CalibrationEligible);Assert.IsFalse(CameraChartCaptureValidator.Validate(sample with{ReferenceWhitePoint=""}).MetadataComplete);
 }
}

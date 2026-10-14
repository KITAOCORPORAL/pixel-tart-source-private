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
}

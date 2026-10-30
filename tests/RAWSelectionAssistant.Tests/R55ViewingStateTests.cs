using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Core.Services.Projects.LightTablePort;
namespace RAWSelectionAssistant.Tests;
[TestClass]public class R55ViewingStateTests
{
 [TestMethod]public void LegacyViewingMigratesExplicitlyAndUnknownVersionRejects(){var legacy=new ViewingTableState(1,[]);Assert.AreEqual(2,legacy.Validate().Version);Assert.ThrowsExactly<InvalidDataException>(()=>new ViewingTableState(3,[]).Validate());Assert.ThrowsExactly<InvalidDataException>(()=>(ViewingTableState.Empty with{Viewport=new(double.NaN,0,0)}).Validate());}
 [TestMethod]public void RotatedPlacementAndInverseHitAreConsistent(){var p=new Placement(new(100,100),200,100,90);Assert.IsTrue(p.Contains(new(100,195)));Assert.IsFalse(p.Contains(new(195,100)));Assert.IsTrue(p.Intersects(new(80,185,30,25)));Assert.IsFalse(p.Intersects(new(180,70,25,25)));}
 [TestMethod]public void LampCurveKeepsReferenceEndpointsAndReducedMotion(){Assert.AreEqual(new LightSample(0,0,0),LightResponse.Sample(.2,false));Assert.AreEqual(new LightSample(1,0,.5),LightResponse.Sample(2,true));Assert.AreEqual(new LightSample(1,0,.5),LightResponse.Sample(.1,true,true));for(int i=0;i<=200;i++){var s=LightResponse.Sample(i/100d,true);Assert.IsTrue(s.Transmission is >=0 and <=1);Assert.IsTrue(s.Flare is >=0 and <=1);}Assert.AreEqual(.1,LightResponse.Power(0));Assert.AreEqual(1.6,LightResponse.Power(2));}
}

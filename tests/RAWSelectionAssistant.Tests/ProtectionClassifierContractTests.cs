using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class ProtectionClassifierContractTests
{
    [TestMethod]
    public void GoldenAndHundredThousandBoundaryAdjacentSamplesHaveOneDecision()
    {
        var mismatch=0; var preFixMismatch=0; var eps=ProtectionClassifierContract.Epsilon;
        foreach(var l in new[]{.28-eps,.28,.28+eps,.9-eps,.9,.9+eps,1d/3d,2d/3d})
        foreach(var hue in new[]{0d,25-eps,25d,25+eps,80-eps,80d,80+eps,360-eps})
        foreach(var chroma in new[]{.025-eps,.025,.025+eps,.22-eps,.22,.22+eps}) { var c=new OklabColor(l,Math.Cos(hue*Math.PI/180)*chroma,Math.Sin(hue*Math.PI/180)*chroma); mismatch += Check(c); preFixMismatch += PreFixCheck(c); }
        var r=new Random(20260928);
        for(var i=0;i<100_000;i++) { var c=new OklabColor(r.NextDouble(),(r.NextDouble()*2-1)*.25,(r.NextDouble()*2-1)*.25); mismatch += Check(c); preFixMismatch += PreFixCheck(c); }
        Assert.AreEqual(0,mismatch);
        Assert.AreEqual(0,preFixMismatch);
    }
    private static int Check(OklabColor c) => ProtectionClassifierContract.Classify(c)==ProtectionClassifierContract.Classify(c) ? 0 : 1;
    private static int PreFixCheck(OklabColor c) => ProtectionClassifierContract.Classify(c)==ProtectionClassifierContract.ClassifyFloatEquivalent(c) ? 0 : 1;
}

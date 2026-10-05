using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceSurfaceTests
{
    [TestMethod]
    public void DisplayRoundtripKeepsTrueOklabAndDoesNotProjectSamplesToShell()
    {
        foreach(var rgb in new[]{new VisualRgb24(0,0,0),new(255,255,255),new(128,128,128),new(1,2,3),new(250,249,248),new(210,70,30),new(50,170,100),new(70,130,210),new(255,0,0),new(0,0,255)})
        {
            var lab=OklabColorSpace.FromSrgb(rgb);var point=ColorSpaceSurface.ToSphere(lab);var restored=ColorSpaceSurface.FromSphere(point);
            Assert.AreEqual(lab.L,restored.L,1e-7);Assert.AreEqual(lab.A,restored.A,2e-6);Assert.AreEqual(lab.B,restored.B,2e-6);
            Assert.IsTrue(double.IsFinite(point.X)&&double.IsFinite(point.Y)&&double.IsFinite(point.Z));
        }
        var gray=ColorSpaceSurface.ToSphere(OklabColorSpace.FromSrgb(new(128,128,128)));
        Assert.IsLessThan(.9, gray.X*gray.X+gray.Y*gray.Y+gray.Z*gray.Z,"Neutral gray remains inside sphere.");
    }
    [TestMethod]
    public void SurfaceRayAndProjectionUseSameRotationPanZoomAndGamut()
    {
        var settings=new ColorSpaceViewSettings { RotationX=21,RotationY=-32,RotationZ=63 };
        var camera=new ColorSpaceCamera(33,-17,2.7,.2,-.1);
        foreach(var offset in new[]{(0d,0d),(.2,.3),(-.3,.1)})
        {
            var scale=ColorSpaceSurface.Scale(camera,480,360);var x=240+(offset.Item1+camera.PanX)*scale;var y=180-(offset.Item2+camera.PanY)*scale;
            var lab=ColorSpaceSurface.PickSurface(x,y,480,360,camera,settings);Assert.IsNotNull(lab);
            var projected=ColorSpaceSurface.Project(lab.Value,new(0,0,0),0,camera,settings,480,360);
            Assert.AreEqual(x,projected.X,.01);Assert.AreEqual(y,projected.Y,.01);
            var rgb=OklabColorSpace.ToLinear(lab.Value);Assert.IsTrue(rgb.R>=-1e-6&&rgb.R<=1.000001&&rgb.G>=-1e-6&&rgb.G<=1.000001&&rgb.B>=-1e-6&&rgb.B<=1.000001);
        }
        Assert.IsNull(ColorSpaceSurface.PickSurface(-1000,-1000,480,360,camera,settings));
    }
    [TestMethod]
    public void SoftSelectionPreservesExactCenterExcludesTransparencyAndHasSmoothBoundary()
    {
        var center=new OklabColor(.5,.1,.05);
        Assert.AreEqual(1,ColorSpaceSurface.SelectionWeight(center,center,.1,.5));
        var last=1d;for(var i=0;i<=100;i++){var weight=ColorSpaceSurface.SelectionWeight(center with { L=.5+i*.001 },center,.1,.5);Assert.IsLessThanOrEqualTo(last+1e-12,weight);last=weight;}
        Assert.AreEqual(0,last,1e-10);Assert.AreEqual(0,ColorSpaceSurface.SelectionWeight(center,center,double.NaN,.5));
        var source=new VisualPixelBuffer(3,1,new byte[]{255,0,0,255,0,0,255,0,0},new byte[]{255,128,0});
        var mask=ColorSpaceSurface.SelectionMask(source,OklabColorSpace.FromSrgb(new(255,0,0)),.01,.4);
        CollectionAssert.AreEqual(new byte[]{255,128,0},mask);
        Assert.ThrowsExactly<OperationCanceledException>(()=>ColorSpaceSurface.SelectionMask(source,center,.1,.4,new(true)));
    }
    [TestMethod]
    public void TransparentPixelsCannotPolluteStatisticsCloudOrToneMembership()
    {
        var source=new VisualPixelBuffer(2,1,new byte[]{255,0,0,0,0,255},new byte[]{255,0});
        var histogram=VisualAnalysisEngine.AnalyzeHistogram(source);Assert.AreEqual(1u,histogram.R[255]);Assert.AreEqual(0u,histogram.B[255]);
        var cloud=ColorSpaceProxyBuilder.Build(source,new(16));Assert.AreEqual(1,cloud.Count);Assert.AreEqual(0,cloud.Points[0].SourceX);
        var members=Enumerable.Range(0,11).SelectMany(zone=>VisualAnalysisEngine.ToneZoneMembers(source,zone)).ToArray();CollectionAssert.AreEqual(new[]{0},members);
    }
    [TestMethod]
    public void SliceAndChromaFiltersHaveExplicitFiniteBoundaries()
    {
        var settings=(new ColorSpaceViewSettings { SliceEnabled=true,SliceCenter=.5,SliceThickness=.2,ChromaMin=double.NaN,ChromaMax=double.PositiveInfinity }).Normalize();
        Assert.IsTrue(settings.Includes(new(.5,0,0)));Assert.IsFalse(settings.Includes(new(.39,0,0)));Assert.IsFalse(settings.Includes(new(.61,0,0)));Assert.IsFalse(settings.Includes(new(double.NaN,0,0)));
        var outOfGamut=ColorSpaceSurface.ToSphere(new(.5,.6,0));Assert.IsGreaterThan(1,outOfGamut.X,"Display preserves an explicit out-of-gamut radial position.");
    }
}

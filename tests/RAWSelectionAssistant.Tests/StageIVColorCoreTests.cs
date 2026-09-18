using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class ColorSpaceRoundTripTests
{
    [TestMethod]
    public void OklabKnownVectorsAndRoundTripStayWithinOneCodeValue()
    {
        var red = OklabColorSpace.FromSrgb(new(255, 0, 0));
        Assert.AreEqual(.627955, red.L, 2e-5); Assert.AreEqual(.224863, red.A, 2e-5); Assert.AreEqual(.125846, red.B, 2e-5);
        foreach (var rgb in new[] { new VisualRgb24(0,0,0), new(255,255,255), new(127,127,127), new(255,0,0), new(0,255,0), new(0,0,255), new(12,93,201) })
        {
            var roundTrip = OklabColorSpace.ToSrgb(OklabColorSpace.FromSrgb(rgb));
            Assert.IsLessThanOrEqualTo(1, Math.Abs(roundTrip.R-rgb.R)); Assert.IsLessThanOrEqualTo(1, Math.Abs(roundTrip.G-rgb.G)); Assert.IsLessThanOrEqualTo(1, Math.Abs(roundTrip.B-rgb.B));
        }
    }

    [TestMethod]
    public void NeutralAxisHasNearZeroChroma()
    {
        foreach (var value in new byte[] { 0, 32, 128, 220, 255 }) Assert.IsLessThan(2e-7, OklabColorSpace.FromSrgb(new(value,value,value)).Chroma);
    }
}

[TestClass]
public sealed class ToneQuantileMappingTests
{
    [TestMethod]
    public void CurveIsMonotonicBoundedAndShiftLimited()
    {
        var source = new uint[256]; var target = new double[256]; for(var i=0;i<256;i++){source[i]=(uint)(i+1);target[255-i]=i+1;}
        var curve=ReferenceToneMapper.BuildMonotonicQuantileCurve(source,target);
        Assert.HasCount(256,curve); Assert.IsTrue(curve.All(value=>value is >=0 and <=1));
        for(var i=1;i<256;i++)Assert.IsGreaterThanOrEqualTo(curve[i-1],curve[i]);
        for(var i=0;i<256;i++)Assert.IsLessThanOrEqualTo(33,Math.Abs(curve[i]*255-i));
    }
}

[TestClass]
public sealed class SmoothZoneAndProtectionTests
{
    [TestMethod]
    public void SmoothZonesAreContinuousNormalizedAndLowSampleConfidenceDrops()
    {
        double[]? previous=null; for(var step=0;step<=100;step++){var current=ReferenceColorTargetBuilder.SmoothZoneWeights(step/100d);Assert.AreEqual(1,current.Sum(),1e-12);if(previous is not null)Assert.IsLessThan(.08,current.Zip(previous,(a,b)=>Math.Abs(a-b)).Max());previous=current;}
        var one = new VisualPixelBuffer(1,1,new byte[]{128,128,128}); var statistic=ReferenceColorTargetBuilder.FromPixels(one);
        Assert.IsLessThan(.1,statistic.Global.Confidence);
    }

    [TestMethod]
    public void NeutralProtectionAndKeepOriginalToneHaveIndependentEffects()
    {
        var (look, source, analysis)=ColorFixtures.Look(); var matcher=new ReferenceLookMatcher();
        var noNeutral=matcher.Match(source,analysis,look with{Parameters=look.Parameters with{NeutralProtection=0,ToneStrength=0}}).Preview.Rgb24.ToArray();
        var protectedNeutral=matcher.Match(source,analysis,look with{Parameters=look.Parameters with{NeutralProtection=100,ToneStrength=0}}).Preview.Rgb24.ToArray();
        var sourceBytes=source.Rgb24.ToArray(); Assert.IsLessThanOrEqualTo(Distance(noNeutral,sourceBytes),Distance(protectedNeutral,sourceBytes));
        var keep=matcher.Match(source,analysis,look with{Parameters=look.Parameters with{KeepOriginalTone=true,ColorStrength=0}}).Preview.Rgb24.ToArray();
        var toneZero=matcher.Match(source,analysis,look with{Parameters=look.Parameters with{ToneStrength=0,ColorStrength=0}}).Preview.Rgb24.ToArray();
        CollectionAssert.AreEqual(toneZero,keep);
    }
    private static long Distance(byte[] a,byte[] b)=>a.Zip(b,(x,y)=>(long)Math.Abs(x-y)).Sum();
}

[TestClass]
public sealed class GamutAndDifferenceTests
{
    [TestMethod]
    public void GamutMappingProducesValidFiniteOutputWithoutHueCollapse()
    {
        var mapped=OklabColorSpace.ToSrgbGamutMapped(new(.7,.7,.4));
        Assert.IsLessThanOrEqualTo((byte)255,mapped.R); Assert.IsLessThanOrEqualTo((byte)255,mapped.G); Assert.IsLessThanOrEqualTo((byte)255,mapped.B); Assert.IsTrue(mapped.R!=mapped.G||mapped.G!=mapped.B);
    }
    [TestMethod]
    public void DifferenceWarningsAreObjectiveAndUnscored()
    {
        var statistic=new ReferenceZoneStatistic(new(.5,0,0),0,20,1); var source=new ReferenceColorTarget(statistic,[statistic,statistic,statistic],.1);
        var color=new ReferenceZoneStatistic(new(.5,.2,.1),0,20,1); var target=new ReferenceColorTarget(color,[color,color,color],.7);
        var warning=ReferenceDifferenceAnalyzer.Compare(source,target); Assert.IsTrue(warning.ToneDifferenceLarge); Assert.DoesNotContain("%",warning.UserMessage!); StringAssert.Contains(warning.UserMessage!,"影调差异");
    }
}

[TestClass]
public sealed class CubeLutTests
{
    [TestMethod]
    public void PreviewCubeApproximatesDirectTransformAndZeroStrengthIsExactIdentity()
    {
        var (look, source, analysis)=ColorFixtures.Look(); var matcher=new ReferenceLookMatcher();
        var transform=matcher.BuildTransform(source,analysis,look); var lut=ReferenceCubeLutBuilder.Build(33,transform.Apply,transform.Pipeline);
        foreach(var rgb in new[]{new VisualRgb24(12,55,91),new(127,128,129),new(235,98,31)})
        {
            var direct=transform.Apply(rgb);var sampled=lut.Sample(rgb.R/255d,rgb.G/255d,rgb.B/255d);
            Assert.IsLessThanOrEqualTo(3,Math.Abs(direct.R-Math.Round(sampled.R*255)));
            Assert.IsLessThanOrEqualTo(3,Math.Abs(direct.G-Math.Round(sampled.G*255)));
            Assert.IsLessThanOrEqualTo(3,Math.Abs(direct.B-Math.Round(sampled.B*255)));
        }
        var identity=matcher.BuildTransform(source,analysis,look with{Parameters=look.Parameters with{MatchStrength=0}});
        var original=new VisualRgb24(17,89,203);Assert.AreEqual(original,identity.Apply(original));
    }

    [TestMethod]
    public void Identity33CubeUsesBlueMajorRedFastAxisAndTrilinearInterpolation()
    {
        var lut=ReferenceCubeLutBuilder.Build(33,rgb=>rgb); Assert.HasCount(35937,lut.Values);
        Assert.AreEqual(0,lut.Values[0].R); Assert.IsGreaterThan(0,lut.Values[1].R); Assert.AreEqual(0,lut.Values[1].G); Assert.AreEqual(0,lut.Values[1].B);
        var sample=lut.Sample(.21,.47,.83); Assert.AreEqual(.21,sample.R,.002); Assert.AreEqual(.47,sample.G,.002); Assert.AreEqual(.83,sample.B,.002);
    }
    [TestMethod]
    public async Task Cube65ExportsDeclaredSizeAndColorContract()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-StageIV",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"look.cube");
        try{var lut=ReferenceCubeLutBuilder.Build(65,rgb=>rgb);Assert.HasCount(274625,lut.Values);await ReferenceCubeLutBuilder.ExportAsync(lut,path,"项目色彩方案");var text=await File.ReadAllTextAsync(path);StringAssert.Contains(text,"LUT_3D_SIZE 65");StringAssert.Contains(text,"已转换到 sRGB");Assert.AreEqual(274625,text.Split('\n').Count(line=>line.Count(c=>c==' ')==2&&char.IsDigit(line[0])));}
        finally{Directory.Delete(root,true);}
    }

    [TestMethod]
    public async Task CubeExportDoesNotModifyReferencedSource()
    {
        var root=Path.Combine(Path.GetTempPath(),"PixelTart-StageIV",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var sourcePath=Path.Combine(root,"source.raw");var cubePath=Path.Combine(root,"look.cube");await File.WriteAllBytesAsync(sourcePath,[1,3,5,7,9]);
        try{var before=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(sourcePath));await ReferenceCubeLutBuilder.ExportAsync(ReferenceCubeLutBuilder.Build(33,rgb=>rgb),cubePath,"安全测试");var after=System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(sourcePath));CollectionAssert.AreEqual(before,after);}
        finally{Directory.Delete(root,true);}
    }
}

internal static class ColorFixtures
{
    public static (ReferenceLook,VisualPixelBuffer,AssetVisualAnalysisResult) Look()
    {
        var bytes=Enumerable.Range(0,256).SelectMany(i=>new[]{(byte)i,(byte)i,(byte)i}).ToArray();var source=new VisualPixelBuffer(16,16,bytes);var analysis=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"source",source));
        var targetBytes=Enumerable.Range(0,256).SelectMany(i=>new[]{(byte)Math.Min(255,i+30),(byte)Math.Max(0,i-20),(byte)i}).ToArray();var targetPixels=new VisualPixelBuffer(16,16,targetBytes);var target=VisualAnalysisEngine.Analyze(new(Guid.NewGuid(),"target",targetPixels));var now=DateTimeOffset.UtcNow;
        return(new(Guid.NewGuid(),"方案",Guid.NewGuid(),[new(Guid.NewGuid(),target.AssetId,"参考","managed://reference","hash",1,target)],new(),now,now),source,analysis);
    }
}

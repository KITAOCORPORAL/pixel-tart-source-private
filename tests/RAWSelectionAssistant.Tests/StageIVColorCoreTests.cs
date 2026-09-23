using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.Tethering;

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

[TestClass]
public sealed class StageIVReferenceResolutionTests
{
    [TestMethod]
    public void ShotThenProjectThenSessionPriorityIsExplicit()
    {
        var shot=Guid.NewGuid();var project=Guid.NewGuid();var session=Guid.NewGuid();
        Assert.AreEqual(shot,ReferenceLookResolver.Resolve(shot,project,session));
        Assert.AreEqual(project,ReferenceLookResolver.Resolve(null,project,session));
        Assert.AreEqual(session,ReferenceLookResolver.Resolve(null,null,session));
        Assert.IsNull(ReferenceLookResolver.Resolve(null,null,null));
    }
}

[TestClass]
public sealed class MatchV3MeasuredFixtureTests
{
    [TestMethod]
    public void TenColorAndToneFixturesRecordQuantilesDriftClippingAndParity()
    {
        var samples = new (string Name, VisualRgb24 Color)[]
        {
            ("P05", new(13, 13, 13)), ("P50", new(128, 128, 128)), ("P95", new(242, 242, 242)),
            ("Neutral", new(110, 110, 110)), ("Skin-like", new(204, 151, 123)),
            ("Highlight", new(250, 242, 223)), ("Shadow", new(22, 30, 40)),
            ("Warm", new(210, 129, 64)), ("Cool", new(55, 132, 209)),
            ("Strong Cast", new(38, 205, 72)), ("High Saturation", new(245, 29, 174))
        };
        var sourceBytes = Enumerable.Range(0, 16 * 16).SelectMany(index =>
        {
            var color = samples[index % samples.Length].Color; return new[] { color.R, color.G, color.B };
        }).ToArray();
        var source = new VisualPixelBuffer(16, 16, sourceBytes);
        var sourceAnalysis = VisualAnalysisEngine.Analyze(new(Guid.NewGuid(), "match-v3-source", source));
        var (baseLook, _, _) = ColorFixtures.Look();
        var look = baseLook with { Parameters = baseLook.Parameters with { NeutralProtection = 80, HighlightProtection = 80, SkinProtection = 70 } };
        var matcher = new ReferenceLookMatcher();
        var matched = matcher.Match(source, sourceAnalysis, look).Preview;
        var transform = matcher.BuildTransform(source, sourceAnalysis, look);
        var cube = ReferenceCubeLutBuilder.Build(33, transform.Apply, transform.Pipeline);
        var observed = new List<(string Name, double NeutralDrift, double HighlightChromaDrift, int ClippedChannels, int PreviewExportDelta)>();
        for (var index = 0; index < samples.Length; index++)
        {
            var (name, color) = samples[index];
            var offset = index * 3;
            var preview = new VisualRgb24(matched.Rgb24.Span[offset], matched.Rgb24.Span[offset + 1], matched.Rgb24.Span[offset + 2]);
            var encoded = cube.Sample(color.R / 255d, color.G / 255d, color.B / 255d);
            var delta = new[] { Math.Abs(preview.R - encoded.R * 255), Math.Abs(preview.G - encoded.G * 255), Math.Abs(preview.B - encoded.B * 255) }.Max();
            var neutralDrift = name == "Neutral" ? OklabColorSpace.FromSrgb(preview).Chroma : 0;
            var highlightDrift = name == "Highlight" ? Math.Abs(OklabColorSpace.FromSrgb(preview).Chroma - OklabColorSpace.FromSrgb(color).Chroma) : 0;
            observed.Add((name, neutralDrift, highlightDrift, new byte[] { preview.R, preview.G, preview.B }.Count(channel => channel is 0 or 255), (int)Math.Ceiling(delta)));
        }
        Assert.HasCount(samples.Length, observed);
        foreach (var row in observed) Assert.IsLessThanOrEqualTo(5, row.PreviewExportDelta, $"{row.Name}: delta {row.PreviewExportDelta}, clipping {row.ClippedChannels}, neutral {row.NeutralDrift:F4}, highlight {row.HighlightChromaDrift:F4}");
        Assert.IsLessThan(.04, observed.Single(row => row.Name == "Neutral").NeutralDrift);
        Assert.IsLessThan(.08, observed.Single(row => row.Name == "Highlight").HighlightChromaDrift);
        var clipped = observed.Where(row => row.Name is not "P05" and not "P95" && row.ClippedChannels > 0).ToArray();
        TestContext?.WriteLine(string.Join(Environment.NewLine, observed.Select(row => $"{row.Name}: neutral={row.NeutralDrift:F5}, highlight-chroma={row.HighlightChromaDrift:F5}, clipped={row.ClippedChannels}, preview-export={row.PreviewExportDelta}")));
        Assert.IsLessThanOrEqualTo(1, clipped.Sum(row => row.ClippedChannels), "Gamut boundary channels: " + string.Join(", ", clipped.Select(row => row.Name)));
    }
    public TestContext? TestContext { get; set; }
}

[TestClass]
public sealed class NextCaptureRuleTests
{
    [TestMethod]
    public void NamingUsesProjectDateCounterAndSafeCustomPrefix()
    {
        var rule=new NextCaptureRule("Kitao",true,true,"Hero/",125);
        Assert.AreEqual("Hero_Kitao_20260918_0125",rule.Example(new(2026,9,18)));
    }
    [TestMethod]
    public void NamingCanOmitProjectAndDateButAlwaysKeepsCounter()
    {
        var rule=new NextCaptureRule("Kitao",false,false,"",3);
        Assert.AreEqual("0003",rule.Example(new(2026,9,18)));
    }
}

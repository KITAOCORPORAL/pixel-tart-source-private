using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass] public sealed class R53ReferenceComparisonTests
{
    [TestMethod] public void TwoPinnedNativeReferenceKernelsRecordNumericalMatrix()
    {
        var path=Environment.GetEnvironmentVariable("KITAO_R53_REFERENCE_MATRIX");
        if(string.IsNullOrEmpty(path)){Assert.Inconclusive("Pinned reference execution matrix is explicitly supplied, never synthesized as a pass.");return;}
        var cases=JsonSerializer.Deserialize<List<ReferenceCase>>(File.ReadAllText(path))!;var rows=new List<object>();double maxDt=0,maxIm=0;int extendedDifferences=0;
        foreach(var c in cases)
        {
            using var a=new HighBitDepthImageBuffer(1,1,new[]{c.A,c.A,c.A});using var b=new HighBitDepthImageBuffer(1,1,new[]{c.B,c.B,c.B});
            var n=new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.BasicTone,"Reference"){CompositeVersion=1,Opacity=c.Opacity,Blend=(StudioLayerBlend)c.Mode};
            using var actual=StudioLayerComposite.Apply(a,b,n);var result=actual.Rgb32.Span[0];var dt=Math.Abs(result-c.Darktable);var im=Math.Abs(result-c.ImageMagick);bool bounded=c.A>=0&&c.A<=1&&c.B>=0&&c.B<=1;
            if(bounded||c.Mode==0){Assert.AreEqual(c.Darktable,result,3e-7);Assert.AreEqual(c.ImageMagick,result,3e-7);maxDt=Math.Max(maxDt,dt);maxIm=Math.Max(maxIm,im);}else if(dt>3e-7||im>3e-7)extendedDifferences++;
            rows.Add(new{c.A,c.B,c.Opacity,c.Mode,c.Darktable,c.ImageMagick,result,dt,im,bounded});
        }
        Assert.IsGreaterThan(1000,cases.Count);Assert.IsGreaterThan(0,extendedDifferences);
        var output=Environment.GetEnvironmentVariable("KITAO_R53_REFERENCE_OUTPUT");if(output is not null)File.WriteAllText(output,JsonSerializer.Serialize(new{cases=rows,maxDt,maxIm,extendedDifferences,domain="Nonlinear display-referred sRGB, straight RGB, alpha unchanged. Extended V1 deliberately not clamped; differences retained."},new JsonSerializerOptions{WriteIndented=true}));
    }
    public sealed record ReferenceCase(float A,float B,double Opacity,int Mode,double Darktable,double ImageMagick);
}

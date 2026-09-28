using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Export;
using RAWSelectionAssistant.Core.Services.Projects;
public sealed record PixelMetrics(bool Pass,double Mean,double P95,double P99,double Max,long[] Counts,int MaxX,int MaxY,int NonFinite);
public static class ClosureMetrics
{
    public static PixelMetrics Compare(HighBitDepthImageBuffer a,HighBitDepthImageBuffer b)
    {
        if(a.Width!=b.Width||a.Height!=b.Height)throw new InvalidDataException("dimensions");
        var errors=new float[a.Rgb32.Length]; var counts=new long[5];double sum=0,max=-1;int index=0,nonFinite=0;
        double[] thresholds=[1e-6,1e-5,1e-4,1e-3,1e-2];
        for(var p=0;p<a.PixelCount;p++)
        {
            double pm=0;
            for(var c=0;c<3;c++){var i=p*3+c;var e=Math.Abs(a.Rgb32.Span[i]-b.Rgb32.Span[i]);if(!float.IsFinite(e)){nonFinite++;e=float.PositiveInfinity;}errors[i]=e;sum+=e;pm=Math.Max(pm,e);}
            if(pm>max){max=pm;index=p;}for(var j=0;j<5;j++)if(pm>thresholds[j])counts[j]++;
        }
        Array.Sort(errors);var mean=sum/errors.Length;var p95=errors[(int)Math.Ceiling((errors.Length-1)*.95)];var p99=errors[(int)Math.Ceiling((errors.Length-1)*.99)];
        return new(nonFinite==0&&mean<=1e-5&&p95<=3e-5&&p99<=5e-5&&max<=1e-4,mean,p95,p99,max,counts,index%a.Width,index/a.Width,nonFinite);
    }
    public static object Tiff(TiffReadBackResult a,TiffReadBackResult b)
    {
        if(a.Width!=b.Width||a.Height!=b.Height||a.BitsPerSample!=16||b.BitsPerSample!=16)throw new InvalidDataException("TIFF precision/dimensions");
        long equal=0,one=0;int max=0;double sum=0,deSum=0,deMax=0;
        var aa=a.Rgb48Samples.Span;var bb=b.Rgb48Samples.Span;
        for(int p=0;p<a.Width*a.Height;p++)
        {
            var pm=0;for(var c=0;c<3;c++){var i=p*3+c;var d=Math.Abs(aa[i]-bb[i]);pm=Math.Max(pm,d);sum+=d;}if(pm==0)equal++;if(pm<=1)one++;max=Math.Max(max,pm);
            var offset=p*3; var x=OklabColorSpace.FromSrgb(aa[offset]/65535f,aa[offset+1]/65535f,aa[offset+2]/65535f);var y=OklabColorSpace.FromSrgb(bb[offset]/65535f,bb[offset+1]/65535f,bb[offset+2]/65535f);
            var de=Math.Sqrt(Math.Pow(x.L-y.L,2)+Math.Pow(x.A-y.A,2)+Math.Pow(x.B-y.B,2));deSum+=de;deMax=Math.Max(deMax,de);
        }
        return new { pass=max<=1, gate="max code delta <= 1; orientation and ICC identical",exactPixelRatio=equal/(double)(a.Width*a.Height),withinOneCodeRatio=one/(double)(a.Width*a.Height),maxCodeDelta=max,meanRgbCodeDelta=sum/aa.Length,meanOklabDeltaE=deSum/(a.Width*a.Height),maxOklabDeltaE=deMax,orientationEqual=a.Orientation==b.Orientation,iccEqual=a.IccProfile.Span.SequenceEqual(b.IccProfile.Span),a.BitsPerSample,a.SamplesPerPixel };
    }
}

using RAWSelectionAssistant.Core.Services.Color;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Photographic controls in linear sRGB. Neutral settings are an exact identity.
/// Tonal masks are smooth overlapping luminance weights; detail is a bounded luminance unsharp mask.</summary>
public static class ColorStudioDevelop
{
    public static HighBitDepthImageBuffer Apply(HighBitDepthImageBuffer source, ColorAdjustmentStackNode node, CancellationToken token = default)
    {
        double P(string key) => node.NumericParameters.TryGetValue(key, out var value) && double.IsFinite(value) ? Math.Clamp(value, key == "exposure" ? -3 : -100, key == "exposure" ? 3 : 100) : 0;
        string[] keys = ["exposure", "brightness", "highlights", "midtones", "shadows", "contrast", "whites", "blacks", "structure", "detail"];
        if (keys.All(key => P(key) == 0)) return source.Clone();
        var input = source.Rgb32.Span; var linear = new float[input.Length]; var luminance = new float[source.PixelCount];
        for (var i = 0; i < input.Length; i++) { if ((i & 8191) == 0) token.ThrowIfCancellationRequested(); linear[i] = (float)Decode(input[i]); }
        for (var p = 0; p < luminance.Length; p++) luminance[p] = (float)(.2126*linear[p*3]+.7152*linear[p*3+1]+.0722*linear[p*3+2]);
        var radius = Math.Max(1, (int)Math.Round(Math.Min(source.Width, source.Height) * .01));
        var structure = P("structure") == 0 ? luminance : Blur(luminance, source.Width, source.Height, radius, token);
        var detail = P("detail") == 0 ? luminance : Blur(luminance, source.Width, source.Height, 1, token);
        var exposure = P("exposure") + P("brightness") / 100;
        var shadows = P("shadows") / 100; var highlights = P("highlights") / 100; var midtones = P("midtones") / 100;
        var contrast = 1 + P("contrast") / 100; var whites = P("whites") / 100 * .25; var blacks = P("blacks") / 100 * .15;
        var structureAmount = P("structure") / 100; var detailAmount = P("detail") / 100;
        var output = new float[input.Length];
        for (var p = 0; p < luminance.Length; p++)
        {
            if ((p & 2047) == 0) token.ThrowIfCancellationRequested();
            var y = luminance[p];
            var shadow = 1-Smooth(.02,.45,y); var highlight=Smooth(.45,.95,y); var mid=4*y*(1-y);
            var ev = exposure + shadows*shadow + highlights*highlight + midtones*mid;
            var adjusted = y*Math.Pow(2,ev);
            adjusted = (adjusted-.18)*contrast+.18;
            adjusted += whites*Smooth(.6,1,y) + blacks*(1-Smooth(0,.2,y));
            adjusted += (y-structure[p])*structureAmount + (y-detail[p])*detailAmount;
            adjusted=Math.Clamp(adjusted,0,1);
            for(var c=0;c<3;c++) output[p*3+c]=(float)Encode(y>1e-8 ? Math.Clamp(linear[p*3+c]*adjusted/y,0,1) : adjusted);
        }
        return new(source.Width,source.Height,output,source.SourceBitDepth,source.WorkingColorSpace,source.Orientation,source.Metadata);
    }
    private static double Decode(double value) => value<=.04045 ? value/12.92 : Math.Pow((value+.055)/1.055,2.4);
    private static double Encode(double value) => value<=.0031308 ? value*12.92 : 1.055*Math.Pow(value,1/2.4)-.055;
    private static double Smooth(double lo,double hi,double value) {var t=Math.Clamp((value-lo)/(hi-lo),0,1);return t*t*(3-2*t);}
    private static float[] Blur(float[] source,int width,int height,int radius,CancellationToken token)
    {
        // Separable running sums: O(pixels), independent of the radius.
        var horizontal=new float[source.Length];var result=new float[source.Length];
        for(var y=0;y<height;y++) {token.ThrowIfCancellationRequested();double sum=0;var left=0;var right=-1;for(var x=0;x<width;x++){while(right<Math.Min(width-1,x+radius))sum+=source[y*width+(++right)];while(left<Math.Max(0,x-radius))sum-=source[y*width+(left++)];horizontal[y*width+x]=(float)(sum/(right-left+1));}}
        for(var x=0;x<width;x++){token.ThrowIfCancellationRequested();double sum=0;var top=0;var bottom=-1;for(var y=0;y<height;y++){while(bottom<Math.Min(height-1,y+radius))sum+=horizontal[(++bottom)*width+x];while(top<Math.Max(0,y-radius))sum-=horizontal[(top++)*width+x];result[y*width+x]=(float)(sum/(bottom-top+1));}}
        return result;
    }
}

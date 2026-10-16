using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research;

/// <summary>Independent creative research response; never a measured dye concentration or a fitted physical LUT.</summary>
public static class CandidateTone
{
    public static double Decode(double v)
    {
        if(!double.IsFinite(v))throw new InvalidDataException("Non-finite display input.");
        v=Math.Clamp(v,0,1);return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);
    }
    public static double Encode(double v){v=Math.Clamp(v,0,1);return v<=.0031308?v*12.92:1.055*Math.Pow(v,1/2.4)-.055;}
    public static ColorVector3 Decode(ColorVector3 v)=>new(Decode(v.X),Decode(v.Y),Decode(v.Z));
    public static ColorVector3 Encode(ColorVector3 v)=>new(Encode(v.X),Encode(v.Y),Encode(v.Z));
    public static double Luma(ColorVector3 v)=>.2126*v.X+.7152*v.Y+.0722*v.Z;
    public static ColorVector3 Mix(ColorVector3 a,ColorVector3 b,double t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
    public static ColorVector3 ApplyLinear(ColorVector3 display,CandidateRecipe recipe)
    {
        var c=Decode(display);var p=recipe.Response;var l=Luma(c);var e=l*Math.Pow(2,p.ExposureEv);
        var toe=e*e/(e+p.Toe+1e-12);var u=toe/(1+p.Shoulder*toe);
        var t=p.Black+(1-p.Black)*Math.Pow(Math.Clamp(u,0,1),p.ApparentDensity);
        ColorVector3 mapped;
        if(recipe.Profile.Palette=="NeutralBW")mapped=new(t,t,t);
        else if(recipe.Profile.Palette=="WhiteBlue")mapped=new(.015+.88*t,.07+.86*t,.32+.62*t);
        else if(recipe.Profile.Palette=="BlackBlue")mapped=new(.002+.035*t,.004+.19*t,.009+.68*t);
        else
        {
            var saturation=p.Saturation/(1+.25*Math.Max(Math.Max(c.X,c.Y),c.Z));
            var scale=l>1e-12?t/l:0;
            var r=t+(c.X-l)*scale*saturation;var g=t+(c.Y-l)*scale*saturation;var b=t+(c.Z-l)*scale*saturation;
            var warmth=recipe.ProfileId=="sx70-v3"?.012:.006;
            // Luminance-dependent creative bias; no arbitrary web-patch fit.
            mapped=new(r+warmth*t*t,g,b+(.004*(1-t)*(1-t)-warmth*t*t));
        }
        return new(Math.Clamp(mapped.X,0,1),Math.Clamp(mapped.Y,0,1),Math.Clamp(mapped.Z,0,1));
    }
}

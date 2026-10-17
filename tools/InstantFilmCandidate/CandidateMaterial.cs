using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
namespace Kitao.InstantFilm.Research;

/// <summary>Original procedural materials. No copied scanner dust or unverified network textures.</summary>
public static class CandidateMaterial
{
    private static ulong Hash(ulong seed,long x,long y,ulong stream)
    {
        unchecked {var z=seed^((ulong)x*0x9E3779B97F4A7C15UL)^((ulong)y*0xD1B54A32D192ED03UL)^stream;
        z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;z=(z^(z>>27))*0x94D049BB133111EBUL;return z^(z>>31);}
    }
    private static double Unit(ulong seed,long x,long y,ulong stream)=>(Hash(seed,x,y,stream)>>11)*(1d/9007199254740992d);
    public static double Noise(ulong seed,double x,double y,double scale,ulong stream)
    {
        var px=x*scale;var py=y*scale;var ix=(long)Math.Floor(px);var iy=(long)Math.Floor(py);
        var fx=px-ix;var fy=py-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        var a=Unit(seed,ix,iy,stream)*(1-fx)+Unit(seed,ix+1,iy,stream)*fx;
        var b=Unit(seed,ix,iy+1,stream)*(1-fx)+Unit(seed,ix+1,iy+1,stream)*fx;
        return 2*(a*(1-fy)+b*fy)-1;
    }
    public static ColorVector3 Emulsion(ColorVector3 linear,double x,double y,CandidateRecipe recipe)
        =>Emulsion(linear,x,y,recipe,recipe.Profile.Palette);
    internal static ColorVector3 Emulsion(ColorVector3 linear,double x,double y,CandidateRecipe recipe,string palette)
    {
        var seed=recipe.Random.RootSeed;var p=recipe.Emulsion;var l=CandidateTone.Luma(linear);
        var shared=p.Microstructure*(Noise(seed,x,y,1200,1)*.65+Noise(seed,x,y,430,2)*.35);
        var uneven=p.DevelopmentUnevenness*(Noise(seed,x,y,5,3)*.65+Noise(seed,x,y,17,4)*.35);
        var defect=0d;
        foreach(var d in recipe.Random.Geometry)
        {
            var dx=x-d.X;var dy=y-d.Y;
            defect+=p.DevelopmentDefects*d.Strength*Math.Exp(-(dx*dx+dy*dy)/(d.Radius*d.Radius))*.1;
        }
        var age=p.ExpiredAging*(.008+.025*Noise(seed,x,y,3,5));
        double Channel(double v,ulong stream,double aging)=>Math.Clamp(v+(shared+.2*p.Microstructure*Noise(seed,x,y,900,stream))*Math.Sqrt(Math.Max(.005,l)) +uneven*v +defect*(.8-v)+aging,0,1);
        var r=Channel(linear.X,11,age);var g=Channel(linear.Y,12,.2*age);var b=Channel(linear.Z,13,-.6*age);
        // A neutral B&W family stays neutral, even though color emulsions have correlated channel variation.
        return palette=="NeutralBW"?new(r,r,r):new(r,g,b);
    }
    public static ColorVector3 Paper(double x,double y,CandidateRecipe recipe)
    {
        var p=recipe.Surface;var seed=recipe.Random.RootSeed;
        var baseColor=p.BaseColor switch{"Neutral"=>new ColorVector3(.94,.94,.94),"Cool"=>new(.91,.94,.96),_=>new(.95,.935,.9)};
        var texture=p.PaperTexture*(.7*Noise(seed,x,y,1700,21)+.3*Noise(seed,x,y,260,22));
        var scratch=p.PaperScratches*Math.Pow(Math.Max(0,Noise(seed,x,y,300,23)),20)*.08;
        return new(Math.Clamp(baseColor.X+texture-scratch,0,1),Math.Clamp(baseColor.Y+texture-scratch,0,1),Math.Clamp(baseColor.Z+texture-scratch,0,1));
    }
    public static ColorVector3 Scan(ColorVector3 display,double x,double y,CandidateRecipe recipe)
    {
        var dust=recipe.Surface.ScanDust*Math.Pow(Math.Max(0,Noise(recipe.Random.RootSeed,x,y,1000,31)),30)*.4;
        return new(Math.Clamp(display.X-dust,0,1),Math.Clamp(display.Y-dust,0,1),Math.Clamp(display.Z-dust,0,1));
    }
}

public static class CandidateRenderer
{
    public static CandidateImageLease Render(CandidateImageLease source,CandidateRecipe recipe,CancellationToken token=default)
    {
        using var borrow=source.Borrow();return Render(borrow.Image,recipe,token);
    }
    public static CandidateImageLease Render(HighBitDepthImageBuffer orientedDisplay,CandidateRecipe recipe,CancellationToken token=default)
    {
        recipe=recipe.Freeze();token.ThrowIfCancellationRequested();
        var frame=new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:recipe.Surface.Frame){PaperGeometry=recipe.Geometry};
        var layout=InstantPaperLayout.Create(orientedDisplay.Width,orientedDisplay.Height,frame);
        var outputLease=StudioGlobalResources.ReserveWork(checked((long)layout.Width*layout.Height*3*sizeof(float)));
        try
        {
        var output=new float[checked(layout.Width*layout.Height*3)];
        // Bounded rows/strips. Source coordinates, not tile coordinates, drive all random material.
        for(var top=0;top<layout.Height;top+=64)
        {
            token.ThrowIfCancellationRequested();var bottom=Math.Min(top+64,layout.Height);
            var firstSy=int.MaxValue;var lastSy=-1;
            for(var y=top;y<bottom;y++)for(var x=0;x<layout.Width;x++)if(layout.TrySource(x,y,out _,out var sy)){firstSy=Math.Min(firstSy,sy);lastSy=Math.Max(lastSy,sy);}
            using var stripLease=lastSy>=firstSy?CandidateSpatial.RenderRegion(orientedDisplay,recipe,0,firstSy,orientedDisplay.Width,lastSy-firstSy+1,token):null;
            using var stripBorrow=stripLease?.Borrow();
            var strip=stripBorrow?.Image;
            for(var y=top;y<bottom;y++)
            {
                token.ThrowIfCancellationRequested();
                for(var x=0;x<layout.Width;x++)
                {
                    var nx=(x+.5)/layout.Width;var ny=(y+.5)/layout.Height;ColorVector3 v;
                    if(layout.TrySource(x,y,out var sx,out var sy))
                    {
                        var i=((sy-firstSy)*orientedDisplay.Width+sx)*3;var pixels=strip!.Rgb32.Span;v=new(pixels[i],pixels[i+1],pixels[i+2]);
                    }
                    else v=CandidateMaterial.Paper(nx,ny,recipe);
                    v=CandidateMaterial.Scan(v,nx,ny,recipe);var o=(y*layout.Width+x)*3;output[o]=(float)v.X;output[o+1]=(float)v.Y;output[o+2]=(float)v.Z;
                }
            }
        }
        return CandidateImageLease.Transfer(new(layout.Width,layout.Height,output,"16","sRGB",1),outputLease);
        }
        catch{outputLease.Dispose();throw;}
    }
}

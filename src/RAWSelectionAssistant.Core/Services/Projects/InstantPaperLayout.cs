namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Original design ratios; not a calibrated manufacturer paper specification.</summary>
public sealed record InstantPaperGeometry(int Version=1,string Fit="CropFill",double CropX=.5,double CropY=.5,
    double WindowAspect=1,double Side=.075,double Top=.075,double Bottom=.25)
{
    public void Validate()
    {
        if(Version!=1||Fit is not("CropFill" or "FitEntire")||
           new[]{CropX,CropY,WindowAspect,Side,Top,Bottom}.Any(x=>!double.IsFinite(x))||
           CropX is <0 or >1||CropY is <0 or >1||WindowAspect is <.25 or >4||
           Side is <0 or >.4||Top is <0 or >.4||Bottom is <0 or >.6)
            throw new InvalidDataException("Invalid paper geometry or crop position.");
    }
}

/// <summary>Maps output pixel centres to EXIF-oriented source pixels without anisotropic stretch.</summary>
public readonly record struct InstantPaperLayout(int Width,int Height,int Left,int Top,int WindowWidth,int WindowHeight,
    int SourceWidth,int SourceHeight,double SourceX,double SourceY,double Scale,int Bottom)
{
    public bool TrySource(int x,int y,out int sx,out int sy)
    {
        var u=x-Left;var v=y-Top;
        var sourceX=SourceX+(u+.5)/Scale;var sourceY=SourceY+(v+.5)/Scale;
        sx=(int)Math.Floor(sourceX);sy=(int)Math.Floor(sourceY);
        return u>=0&&v>=0&&u<WindowWidth&&v<WindowHeight&&sourceX>=0&&sourceY>=0&&sourceX<SourceWidth&&sourceY<SourceHeight;
    }
    public static InstantPaperLayout Create(int width,int height,InstantFilmDefinition? film)
    {
        if(width<=0||height<=0)throw new ArgumentOutOfRangeException(nameof(width));
        if(film?.Enabled!=true)return new(width,height,0,0,width,height,width,height,0,0,1,0);
        if(film.PaperGeometry is not {} geometry)
        {
            var edge=Math.Min(width,height);var side=film.PaperFrame?(int)Math.Round(edge*film.Border):0;
            var bottom=film.PaperFrame?Math.Max(side,(int)Math.Round(edge*film.BottomBorder)):0;
            return new(checked(width+side*2),checked(height+side+bottom),side,side,width,height,width,height,0,0,1,bottom);
        }
        geometry.Validate();
        var fill=geometry.Fit=="CropFill";
        var ww=Math.Max(1,(int)Math.Round(fill?Math.Min(width,height*geometry.WindowAspect):Math.Max(width,height*geometry.WindowAspect)));
        var wh=Math.Max(1,(int)Math.Round(ww/geometry.WindowAspect));
        var scale=fill?Math.Max(ww/(double)width,wh/(double)height):Math.Min(ww/(double)width,wh/(double)height);
        var sx=(width-ww/scale)*(fill?geometry.CropX:.5);var sy=(height-wh/scale)*(fill?geometry.CropY:.5);
        var left=film.PaperFrame?(int)Math.Round(ww*geometry.Side):0;
        var top=film.PaperFrame?(int)Math.Round(ww*geometry.Top):0;
        var lower=film.PaperFrame?(int)Math.Round(ww*geometry.Bottom):0;
        return new(checked(ww+2*left),checked(wh+top+lower),left,top,ww,wh,width,height,sx,sy,scale,lower);
    }
}


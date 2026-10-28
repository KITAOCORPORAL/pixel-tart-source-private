using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record InstantFrameArtwork(int Version,string Id,string Sha256,int Width,int Height,double X0,double Y0,double X1,double Y1)
{
    public InstantFrameArtwork Validate()
    {
        var known=OriginalFrameLibrary.Find(Id);
        if(Version!=1||this!=known)throw new InvalidDataException("原创相框身份/版本/hash不匹配，未回退其他相框。");return this;
    }
}
public static class OriginalFrameLibrary
{
    private static readonly System.Reflection.Assembly Assembly=typeof(OriginalFrameLibrary).Assembly;
    private static Stream Resource(string suffix)=>Assembly.GetManifestResourceStream("RAWSelectionAssistant.Core.Resources.OriginalFrames."+suffix)??throw new FileNotFoundException("缺少原创相框资源，无法显影/导出："+suffix);
    private static readonly JsonDocument Manifest=JsonDocument.Parse(Resource("manifest.json"));
    public static IReadOnlyList<LightPrintFrame> Frames {get;}=Manifest.RootElement.EnumerateArray().Select(f=>new LightPrintFrame(f.GetProperty("id").GetString()!,f.GetProperty("name").GetString()!,"光镜原创","Neutral","Plain","Rectangle",true,f.GetProperty("rights").GetString()!)).ToArray();
    public static InstantFrameArtwork Find(string id)
    {
        var f=Manifest.RootElement.EnumerateArray().SingleOrDefault(x=>x.GetProperty("id").GetString()==id);
        if(f.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("未知原创相框身份。");var w=f.GetProperty("window");
        return new(1,id,f.GetProperty("rgba_sha256").GetString()!,f.GetProperty("width").GetInt32(),f.GetProperty("height").GetInt32(),w.GetProperty("x0").GetDouble(),w.GetProperty("y0").GetDouble(),w.GetProperty("x1").GetDouble(),w.GetProperty("y1").GetDouble());
    }
    public static byte[] Load(InstantFrameArtwork artwork)
    {
        artwork.Validate();using var compressed=Resource(artwork.Id+".rgba.zlib");using var z=new ZLibStream(compressed,CompressionMode.Decompress);var bytes=new byte[checked(artwork.Width*artwork.Height*4)];z.ReadExactly(bytes);if(z.ReadByte()!=-1||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(artwork.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("原创相框资源hash验证失败，未替换。");return bytes;
    }
    public static InstantPaperLayout Layout(int width,int height,Kitao.InstantFilm.Research.CandidateRecipe recipe)
    {
        var basis=InstantPaperLayout.Create(width,height,new InstantFilmDefinition(true,AlgorithmVersion:2,PaperFrame:recipe.Surface.Frame){PaperGeometry=recipe.Geometry});
        if(recipe.FrameArtwork is not{} art||!recipe.Surface.Frame)return basis;
        art.Validate();int canvasWidth=Math.Max(1,(int)Math.Round(basis.WindowWidth/(art.X1-art.X0))),canvasHeight=Math.Max(1,(int)Math.Round(canvasWidth*(double)art.Height/art.Width));int left=(int)Math.Round(canvasWidth*art.X0),top=(int)Math.Round(canvasHeight*art.Y0);
        return basis with{Width=canvasWidth,Height=canvasHeight,Left=left,Top=top,Bottom=canvasHeight-top-basis.WindowHeight};
    }
    public static ColorVector3 Composite(ColorVector3 photo,int x,int y,InstantPaperLayout layout,InstantFrameArtwork art,byte[] rgba)
    {
        // Resource pixels are encoded sRGB straight RGBA. Deterministic nearest resource sampling;
        // full-result preview downsamples afterward, same as export. Alpha belongs only to frame art.
        int px=Math.Clamp((int)((x+.5)*art.Width/layout.Width),0,art.Width-1),py=Math.Clamp((int)((y+.5)*art.Height/layout.Height),0,art.Height-1),i=(py*art.Width+px)*4;double a=rgba[i+3]/255d;
        return new(photo.X+(rgba[i]/255d-photo.X)*a,photo.Y+(rgba[i+1]/255d-photo.Y)*a,photo.Z+(rgba[i+2]/255d-photo.Z)*a);
    }
}

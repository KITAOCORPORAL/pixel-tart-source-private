using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research;
public interface ICandidatePixelSource { int Width {get;} int Height {get;} ColorVector3 Read(int x,int y); }
internal sealed class BufferCandidateSource(HighBitDepthImageBuffer image):ICandidatePixelSource
{
 public int Width=>image.Width;public int Height=>image.Height;
 public ColorVector3 Read(int x,int y){var i=(y*Width+x)*3;var p=image.Rgb32.Span;return new(p[i],p[i+1],p[i+2]);}
}

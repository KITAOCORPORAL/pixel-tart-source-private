using System.Windows;using System.Windows.Media;
namespace RAWSelectionAssistant.Views;
/// <summary>Shared uniform-fit rectangle and affine pixel mapping. No image/color processing.</summary>
internal static class ViewingImageMapping
{
 internal static Rect UniformRect(Size area,Size pixels){if(area.Width<=0||area.Height<=0||pixels.Width<=0||pixels.Height<=0)return Rect.Empty;double scale=Math.Min(area.Width/pixels.Width,area.Height/pixels.Height);return new((area.Width-pixels.Width*scale)/2,(area.Height-pixels.Height*scale)/2,pixels.Width*scale,pixels.Height*scale);}
 internal static Matrix PixelToSurface(Size area,Size pixels,Matrix elementToSurface){var fit=UniformRect(area,pixels);var m=new Matrix(fit.Width/pixels.Width,0,0,fit.Height/pixels.Height,fit.X,fit.Y);m.Append(elementToSurface);return m;}
 internal static bool TryPixel(Point surface,Size pixels,Matrix pixelToSurface,out Point pixel){pixel=default;if(!pixelToSurface.HasInverse)return false;var inverse=pixelToSurface;inverse.Invert();pixel=inverse.Transform(surface);return pixel.X>=0&&pixel.Y>=0&&pixel.X<pixels.Width&&pixel.Y<pixels.Height;}
 internal static Matrix LoupeMatrix(Matrix pixelToSurface,Point pointer,double diameter,double requested,out double magnification){double scale=Math.Sqrt(pixelToSurface.M11*pixelToSurface.M11+pixelToSurface.M12*pixelToSurface.M12);magnification=Math.Min(requested,1/Math.Max(1e-9,scale));var m=pixelToSurface;m.Scale(magnification,magnification);m.Translate(diameter/2-pointer.X*magnification,diameter/2-pointer.Y*magnification);return m;}
}

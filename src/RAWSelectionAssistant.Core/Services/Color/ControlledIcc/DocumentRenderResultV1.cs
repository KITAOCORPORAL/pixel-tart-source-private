using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Core.Services.Color.ControlledIcc;
/// <summary>Owned document pixels before any monitor transform. Alpha is straight and independent.</summary>
public sealed class DocumentRenderResultV1 : IDisposable
{
 public HighBitDepthImageBuffer Pixels {get;}
 public RgbIccIdentityV1 Profile {get;}
 public ReadOnlyMemory<float> Alpha {get;}
 public Guid? EditVersionId {get;}
 public string GeometryIdentity {get;}
 public string PixelFormat => "RGB-float32";
 public int BitsPerChannel => 32;
 public string Precision => "IEEE754-float32";
 public string AlphaPolicy => Alpha.IsEmpty ? "Opaque" : "StraightIndependentV1";
 public DocumentRenderResultV1(HighBitDepthImageBuffer pixels,RgbIccIdentityV1 profile,Guid? version,string geometry,ReadOnlyMemory<float> alpha=default)
 {profile.Bytes();if(!alpha.IsEmpty&&(alpha.Length!=pixels.PixelCount||alpha.Span.ToArray().Any(x=>!float.IsFinite(x)||x<0||x>1)))throw new InvalidDataException("Invalid independent alpha");Pixels=pixels;Profile=profile;EditVersionId=version;GeometryIdentity=geometry;Alpha=alpha;}
 public void RequireOpaque(){if(!Alpha.IsEmpty&&Alpha.Span.ToArray().Any(x=>x<1))throw new NotSupportedException("透明像素需要明确背景或独立Alpha出口；未静默压白。");}
 public DocumentRenderResultV1 ConvertTo(RgbIccIdentityV1 target,CancellationToken token=default)
 {using var cms=new LcmsRgbEngine();using var t=cms.CreateTransform(Profile,target);return new(new(Pixels.Width,Pixels.Height,t.Convert(Pixels.Rgb32.Span,token),"32f",target.Label,Pixels.Orientation),target,EditVersionId,GeometryIdentity,Alpha.IsEmpty?default:Alpha.ToArray());}
 public DocumentRenderResultV1 NormalizeOrientation(CancellationToken token=default)
 {
  int sw=Pixels.Width,sh=Pixels.Height;ushort orientation=Pixels.Orientation;int w=orientation>=5?sh:sw,h=orientation>=5?sw:sh;var rgb=new float[w*h*3];var alpha=Alpha.IsEmpty?null:new float[w*h];
  for(int y=0;y<h;y++){token.ThrowIfCancellationRequested();for(int x=0;x<w;x++){var (sx,sy)=orientation switch{2=>(sw-1-x,y),3=>(sw-1-x,sh-1-y),4=>(x,sh-1-y),5=>(y,x),6=>(y,sh-1-x),7=>(sw-1-y,sh-1-x),8=>(sw-1-y,x),_=>(x,y)};int source=(sy*sw+sx)*3,target=(y*w+x)*3;Pixels.Rgb32.Span.Slice(source,3).CopyTo(rgb.AsSpan(target));if(alpha is not null)alpha[y*w+x]=Alpha.Span[sy*sw+sx];}}
  return new(new(w,h,rgb,"32f",Pixels.WorkingColorSpace,1),Profile,EditVersionId,GeometryIdentity+"/Upright",alpha is null?default:alpha);
 }
 public void Dispose()=>Pixels.Dispose();
}

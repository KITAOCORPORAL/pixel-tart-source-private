using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Explicit scene-linear source-normalized local EV before the existing PV4 display mapping.
/// Normal interpolation only; display-sRGB blend modes are never used in this domain.</summary>
public sealed record SceneExposureV1(double ExposureEv,StudioLocalMask? Mask=null,int Version=1)
{
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
 public bool Locked{get;init;}
 public SceneExposureV1 Validate(){if(Version!=1||!double.IsFinite(ExposureEv)||ExposureEv is < -10 or >10)throw new InvalidDataException("Invalid scene exposure v1");Mask?.Freeze();return this with{Mask=Mask?.Freeze()};}
 public ColorVector3 Apply(ColorVector3 linear,double weight){var gain=1+(Math.Pow(2,ExposureEv)-1)*Math.Clamp(weight,0,1);return new(linear.X*gain,linear.Y*gain,linear.Z*gain);}
}

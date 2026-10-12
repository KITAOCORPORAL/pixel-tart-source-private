namespace RAWSelectionAssistant.Core.Services.Projects;

public enum InstantDevelopmentStage { Computing, Camera, Ejecting, Developing, Completed, Cancelled, Failed }
public sealed record InstantDevelopmentFrame(InstantDevelopmentStage Stage,double Progress,bool CameraVisible,bool EditingAllowed);
/// <summary>Presentation only; duration starts AFTER final pixels are ready and ejection is complete.</summary>
public static class InstantDevelopmentTimeline
{
    public const double DefaultSeconds=4;
    public const double EjectionSeconds=1.35;
    public static InstantDevelopmentFrame Sample(double elapsed,bool camera,double duration=DefaultSeconds)
    {
        if(!double.IsFinite(duration)||duration is <3 or >5||!double.IsFinite(elapsed)||elapsed<0)throw new ArgumentOutOfRangeException(nameof(duration));
        if(camera&&elapsed<.3)return new(InstantDevelopmentStage.Camera,0,true,false);
        if(camera&&elapsed<EjectionSeconds)return new(InstantDevelopmentStage.Ejecting,Math.Clamp((elapsed-.3)/.9,0,1),true,false);
        var offset=camera?EjectionSeconds:0;
        var progress=elapsed>=offset+duration?1:Math.Clamp((elapsed-offset)/duration,0,1);
        return new(progress<1?InstantDevelopmentStage.Developing:InstantDevelopmentStage.Completed,progress,false,progress>=1);
    }
}

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Persisted full-resolution source recipe; never a preview bitmap or UI cache.</summary>
public sealed record InstantInputRecipe(int Version,string Mode,Guid? OriginVersionId,
    ReferenceLook? Look=null,ColorAdjustmentStack? Stack=null,PixelTartFilmSettings? Film=null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public LegacyRawEditInputContract? LegacyRaw {get;init;}
}

public sealed record LegacyRawEditInputContract(int Version,int ProcessingVersion,string SourceSha256,
    string DecoderContract="ProfessionalDecode-RGB16-sRGB-CameraWB-AutoRotateFalse-V1",
    string OrientationContract="NormalizePixelsBeforeEditsV1",string RenderContract="LegacyRawFullResolutionV1",
    ColorStudioMatchEngine Engine=ColorStudioMatchEngine.Stable,MatchV4ExecutionMode ExecutionMode=MatchV4ExecutionMode.Auto);



using RAWSelectionAssistant.Core.Services.Color;
namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>PV6: legacy RAW decode/edits once, then display-referred sRGB identity into instant film.</summary>
public static class LegacyRawInstantInput
{
    public static void ValidateRecipe(InstantInputRecipe recipe)
    {
        var input=recipe.LegacyRaw;
        if(recipe.Version!=2||recipe.Mode!="CurrentEdits"||recipe.OriginVersionId is null||input is null||input.Version!=1||
input.ProcessingVersion is not(1 or 2)||string.IsNullOrEmpty(input.SourceSha256)||input.SourceSha256.Length!=64||!input.SourceSha256.All(Uri.IsHexDigit)||
           input.DecoderContract!="ProfessionalDecode-RGB16-sRGB-CameraWB-AutoRotateFalse-V1"||input.OrientationContract!="NormalizePixelsBeforeEditsV1"||input.RenderContract is not("LegacyRawFullResolutionV1" or "LegacyRawSpatialBandsV2"))
            throw new InvalidDataException("Unknown legacy RAW edit input contract.");
        if(input.Engine!=ColorStudioMatchEngine.Stable)throw new NotSupportedException("旧RAW CurrentEdits 不支持 MatchV4Beta；原版本保留，请使用稳定处理引擎。");
        if(!Enum.IsDefined(input.ExecutionMode))throw new InvalidDataException("Unknown execution mode.");
        recipe.Look?.Normalize();recipe.Stack?.Normalize();recipe.Film?.Validate();
        if((recipe.Stack?.ProcessingVersion??1)!=input.ProcessingVersion)throw new InvalidDataException("Source processing version mismatch.");
        foreach(var node in recipe.Stack?.Nodes.Where(n=>n.Enabled)??[])
        {
            if(node.Type==ColorStudioNodeType.ReferenceMatch&&recipe.Look is null)throw new NotSupportedException($"工具 {node.Name} 缺少参考Look，不能静默跳过。");
            if(!Enum.IsDefined(node.Type))throw new NotSupportedException($"不支持旧RAW工具 {node.Type}。");
        }
    }
    public static ProfessionalRawDefinition CreateSpatial(InstantInputRecipe recipe) => Create(recipe with{LegacyRaw=(recipe.LegacyRaw??throw new InvalidDataException("Missing source contract.")) with{RenderContract=SpatialLegacyRawRenderer.Contract}});
    public static ProfessionalRawDefinition Create(InstantInputRecipe recipe)
    {
        ValidateRecipe(recipe);
        // Snapshot by value: no later UI dictionary mutation can alter this operation or decode key.
        var frozen=System.Text.Json.JsonSerializer.Deserialize<InstantInputRecipe>(System.Text.Json.JsonSerializer.Serialize(recipe))!;
        return new ProfessionalRawDefinition(6,"Rendered",InputConfiguration:"LegacyRawEditsSrgbFloatV1",DisplayTransform:"LegacyRawEditsIdentityV1"){InstantInput=frozen};
    }
    public static ProfessionalRawMaster Render(FrozenRawMaster source,ProfessionalRawDefinition definition,
        RawMatchTiff16ProductPipeline pipeline,CancellationToken token=default,Action<string>? stage=null)
    {
        definition.Validate();var recipe=definition.InstantInput!;ValidateRecipe(recipe);
        if(!string.Equals(source.SourceSha256,recipe.LegacyRaw!.SourceSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Legacy RAW source identity changed.");
        // Reserve before orientation allocates; retained source and scratch are distinct from physical allocation.
        var spatial=recipe.LegacyRaw!.RenderContract==SpatialLegacyRawRenderer.Contract;
        var memory=BoundedLegacyRawRenderer.Plan(source.Width,source.Height,recipe.Look,recipe.Stack,recipe.Film);
        using var work=spatial?StudioGlobalResources.ReserveWork(checked((long)source.Width*source.Height*12)):StudioGlobalResources.ReserveWork(memory.RequiredBytes);
        token.ThrowIfCancellationRequested();stage?.Invoke("OrientationStart");
        var upright=RawImageOrientation.NormalizePixels(source.Image,token);stage?.Invoke("OrientationComplete");
        stage?.Invoke("Plan:"+(memory.Bounded?"RowBandsHalo0":"FullImage")+":"+memory.RequiredBytes);
        token.ThrowIfCancellationRequested();stage?.Invoke("CurrentEditsStart");
        var rendered=spatial?SpatialLegacyRawRenderer.Render(upright,recipe.Look,recipe.Stack,recipe.Film,token,stage):memory.Bounded?BoundedLegacyRawRenderer.Render(upright,recipe.Look,recipe.Stack,recipe.Film,token,stage):
            pipeline.Render(upright,recipe.Look,recipe.Stack,maximumEdge:0,token:token,film:recipe.Film).ProcessingPixels
            ??throw new InvalidDataException("Legacy RAW renderer did not provide full precision pixels.");
        token.ThrowIfCancellationRequested();stage?.Invoke("CurrentEditsComplete");
        if(rendered.Width!=upright.Width||rendered.Height!=upright.Height||rendered.Orientation!=1||rendered.WorkingColorSpace!="sRGB"||rendered.SourceBitDepth!="16")throw new InvalidDataException("Legacy render size/domain contract changed.");
        var contract=new ProfessionalRawContract("LegacyRawFullResolutionV1","Legacy RAW","CurrentEdits",source.SourceSha256,definition.DecodeKey,[],[],[],65535,
            OutputSpace:"sRGB",OutputBits:16,WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB",Characterization:"Camera WB/display and legacy edits already baked; PV6 never applies them again");
        return new(rendered,contract);
    }
}

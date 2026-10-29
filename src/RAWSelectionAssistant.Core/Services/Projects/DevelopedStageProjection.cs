using System.Security.Cryptography;
using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Read-only projection of an existing committed LightPrint stage, not an executable stack node.</summary>
public sealed record DevelopedStageProjection(int ProjectionVersion,Guid VariantId,Guid? SourceVariantId,int FilmAlgorithmVersion,int RecipeVersion,string RecipeSha256,string FrameId,string FrozenRecipeJson)
{
 public static DevelopedStageProjection? FromCommitted(Guid? variantId,ProfessionalRawDefinition? definition,bool draft=false)
 {
  if(draft||variantId is not Guid id||id==Guid.Empty||definition?.LightPrint is not{} light||definition.InstantFilm?.V3 is not{} recipe)return null;
  definition.Validate();
  // WindowAdjustments are edited after film response; exclude them from frozen-stage identity.
  var payload=JsonSerializer.Serialize(new{definition.InstantFilm,definition.InstantInput,LightPrint=light with{WindowAdjustments=null}});
  var hash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
  return new(1,id,definition.InstantInput?.OriginVersionId,definition.InstantFilm.AlgorithmVersion,recipe.AlgorithmVersion,hash,light.FrameId,payload);
 }
}

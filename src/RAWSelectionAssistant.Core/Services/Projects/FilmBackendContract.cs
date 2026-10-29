using System.Security.Cryptography;using System.Text;using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record FilmColorDomainV1(string Space,string Transfer,string WhitePoint,int Bits,string Alpha,string? ProfileSha256=null);
/// <summary>Frozen product-side boundary. No third-party engine code or resources are linked here.</summary>
public sealed record FilmBackendBindingV1(int Version,Guid RequestId,Guid TargetId,Guid SourceVariantId,string SourceSha256,string SourceRecipeSha256,string EngineId,string EngineVersion,string ResourceVersion,string FilmId,string PaperId,string ParametersJson,int Seed,FilmColorDomainV1 Input,FilmColorDomainV1 Output)
{
 public FilmBackendBindingV1 Validate(){if(SourceSha256 is null||SourceRecipeSha256 is null||ParametersJson is null||Input is null||Output is null||Version!=1||RequestId==Guid.Empty||TargetId==Guid.Empty||SourceVariantId==Guid.Empty||SourceSha256.Length!=64||SourceRecipeSha256.Length!=64||!SourceSha256.All(Uri.IsHexDigit)||!SourceRecipeSha256.All(Uri.IsHexDigit)||Seed<0||EngineId!="guangjing-existing-film"||EngineVersion!="PixelTartFilmV1"||ResourceVersion!="BuiltInV1"||PaperId!="none"||!PixelTartFilmProfiles.All.Any(p=>p.Id==FilmId))throw new InvalidDataException("不支持的胶片后端配方或版本。");var settings=JsonSerializer.Deserialize<PixelTartFilmSettings>(ParametersJson)??throw new InvalidDataException("缺少胶片参数。");settings.Validate();if(settings.ProfileId!=FilmId||settings.Seed!=Seed||!settings.Enabled)throw new InvalidDataException("胶片参数身份不一致。");if(Input.WhitePoint!="D65"||Output.WhitePoint!="D65"||Input.Bits!=8||Input.Alpha!="preserve-source"||Input.ProfileSha256 is null||Input.ProfileSha256.Length!=64||!Input.ProfileSha256.All(Uri.IsHexDigit)||Output.ProfileSha256!=Input.ProfileSha256||Input.Space!="sRGB"||Input.Transfer!="sRGB"||Output.Space!="sRGB"||Output.Transfer!="sRGB"||Output.Bits!=8||Output.Alpha!="preserve-source")throw new InvalidDataException("不支持的胶片输入/输出域。");return this;}
 public static string HashRecipe(StudioEditVersion version)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(version)))).ToLowerInvariant();
}
public sealed record FilmBackendAvailability(string Id,bool Available,string Reason);
public static class FilmBackendRegistryV1
{
 public static IReadOnlyList<FilmBackendAvailability> Backends=>[new("guangjing-existing-film",true,"已有8位栅格创意胶片后端；不是实测负片/反转片"),new("spektralab",false,"研究引擎、色彩域及GPL/资源许可门禁未完成；未加载")];
}

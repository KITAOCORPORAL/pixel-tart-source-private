namespace RAWSelectionAssistant.Core.Services.Projects;
/// <summary>Viewing placement only; never applied to photo pixels or collage documents.</summary>
public sealed record ViewingTableItem(Guid TargetId,Guid VariantId,string SourceHash,string RecipeHash,StudioEditVersion FrozenVariant,double X,double Y,double Scale=1,double Rotation=0)
{
 public ViewingTableItem Validate(){if(FrozenVariant is null||SourceHash is null||RecipeHash is null||TargetId==Guid.Empty||VariantId==Guid.Empty||FrozenVariant.Id!=VariantId||SourceHash.Length!=64||RecipeHash.Length!=64||!SourceHash.All(Uri.IsHexDigit)||!RecipeHash.All(Uri.IsHexDigit)||!double.IsFinite(X+Y+Scale+Rotation)||Math.Abs(X)>100000||Math.Abs(Y)>100000||Scale is <.1 or >8||Math.Abs(Rotation)>36000)throw new InvalidDataException("灯箱摆放或照片身份无效。");FrozenVariant.Stack?.Normalize();FrozenVariant.RawDefinition?.Validate();FrozenVariant.Film?.Validate();if(!Enum.IsDefined(FrozenVariant.Engine)||!Enum.IsDefined(FrozenVariant.Mode))throw new InvalidDataException("未知的固定变体引擎");FrozenVariant.FilmBackend?.Validate();if(!string.Equals(FrozenVariant.SourceHash,SourceHash,StringComparison.OrdinalIgnoreCase)||FilmBackendBindingV1.HashRecipe(FrozenVariant)!=RecipeHash)throw new InvalidDataException("灯箱固定配方或来源hash错误。");return this;}
}
public sealed record ViewingTableState(int Version,IReadOnlyList<ViewingTableItem> Items,bool PresentationLight=false)
{
 public static ViewingTableState Empty=>new(1,[]);
 public ViewingTableState Validate(){if(Version!=1||Items is null||Items.Count>100||Items.Any(x=>x is null)||Items.Select(x=>(x.TargetId,x.VariantId)).Distinct().Count()!=Items.Count)throw new InvalidDataException("不支持的灯箱状态版本或照片数。");foreach(var x in Items)(x??throw new InvalidDataException("灯箱空项目")).Validate();return this;}
}

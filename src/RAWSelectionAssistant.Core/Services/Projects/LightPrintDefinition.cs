namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record LightPrintDefinition(int Version,string FrameId,string Camera,DateTimeOffset DevelopedAt)
{
 public ColorAdjustmentStack? WindowAdjustments {get;init;}
}
public sealed record LightPrintFrame(string Id,string Name,string Group,string Color,string Pattern,string Window,bool Shippable,string Rights);
public static class LightPrintFrames
{
 public static IReadOnlyList<LightPrintFrame> Available {get;}=[
  new("generic.white.square","经典暖白 · 方窗","经典","Warm","Plain","Rectangle",true,"Project procedural geometry; no external artwork"),
  new("generic.neutral.square","中性白 · 方窗","经典","Neutral","Plain","Rectangle",true,"Project procedural geometry"),
  new("generic.cool.square","冷白 · 方窗","经典","Cool","Plain","Rectangle",true,"Project procedural geometry"),
  new("generic.white.circle","暖白 · 圆窗","经典","Warm","Plain","Circle",true,"Project procedural circular coverage"),
  new("generic.dots.square","点纹 · 方窗","图案","Neutral","Dots","Rectangle",true,"Project original procedural dots"),
  new("generic.foil.circle","柔金属 · 圆窗","金属","Cool","Foil","Circle",true,"Project original procedural foil")];
}

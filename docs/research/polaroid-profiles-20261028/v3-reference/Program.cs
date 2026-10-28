using System.Text.Json;
using Kitao.InstantFilm.Research;
using RAWSelectionAssistant.Core.Services.Color;
var input=JsonSerializer.Deserialize<double[][]>(File.ReadAllText(args[0]))!;
var result=new Dictionary<string,double[][]>();
foreach(var (id,palette,sat) in new[]{("modern-color","Color",.9),("modern-bw","NeutralBW",0d)}){
 var recipe=new CandidateRecipe(id,new(0,.025,.3,.012,sat,1),new(palette));
 result[id]=input.Select(a=>{var v=CandidateTone.Encode(CandidateTone.ApplyLinear(new(a[0],a[1],a[2]),recipe));return new[]{v.X,v.Y,v.Z};}).ToArray();
}
File.WriteAllText(args[1],JsonSerializer.Serialize(result));
namespace RAWSelectionAssistant.Core.Services.Color {public readonly record struct ColorVector3(double X,double Y,double Z);}
namespace Kitao.InstantFilm.Research {
 public sealed record CandidateResponse(double ExposureEv,double Toe,double Shoulder,double Black,double Saturation,double ApparentDensity);
 public sealed record CandidateProfile(string Palette);
 public sealed record CandidateRecipe(string ProfileId,CandidateResponse Response,CandidateProfile Profile);
}

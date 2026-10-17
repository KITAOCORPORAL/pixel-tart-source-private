using System.IO;using System.Text.Json;using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;using RAWSelectionAssistant.Core.Services.Projects;using RAWSelectionAssistant.Services;
internal static class InstantProductVisual
{
 public static async Task Run(string output)
 {
  if(Directory.Exists(output))throw new IOException("Fresh synthetic visual output");Directory.CreateDirectory(output);const int w=360,h=240;var pixels=new float[w*h*3];for(int y=0;y<h;y++)for(int x=0;x<w;x++){var i=(y*w+x)*3;pixels[i]=x/(float)(w-1);pixels[i+1]=y/(float)(h-1);pixels[i+2]=(x/30+y/30)%2==0?.12f:.8f;}
  var basis=new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");var master=new ProfessionalRawMaster(new(w,h,pixels,"16","sRGB",1),new("SyntheticProductMaster","Synthetic","Original",new string('0',64),basis.DecodeKey,[],[],[],65535,OutputSpace:"sRGB",WhiteBalanceApplied:false,CameraMatrixApplied:false,Gamma:"sRGB"));var rows=new List<object>();
  foreach(var profile in CandidateProfiles.Available.Where(p=>p.Id!="modern-600-v3"))
  {
   var old=InstantFilmProfiles.Available.Single(p=>p.Id==profile.LegacyId);var previous=basis with{InstantFilm=new(true,old.Id,old.ChemicalGeneration,AlgorithmVersion:2){PaperGeometry=new()}};var r=CandidateRecipe.Create(profile.Id,20261017);var current=basis with{InstantFilm=new(true,old.Id,old.ChemicalGeneration,AlgorithmVersion:3){V3=r}};
   var oldPath=Path.Combine(output,profile.Id+"-v2.png");var currentPath=Path.Combine(output,profile.Id+"-v3.png");await ProfessionalRawExport.WriteAsync(master,previous,oldPath,StudioExportFormat.Png);var receipt=await ProfessionalRawExport.WriteAsync(master,current,currentPath,StudioExportFormat.Png);rows.Add(new{profile=profile.Id,seed=r.Random.RootSeed,currentPath,oldPath,receipt});
  }
  File.WriteAllText(Path.Combine(output,"SYNTHETIC_PRODUCT_VISUAL.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
}

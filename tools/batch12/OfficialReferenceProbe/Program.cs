using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.ColorValidation;
var before=File.ReadAllBytes(args[0]);var after=File.ReadAllBytes(args[1]);
var references=new List<object>();
foreach(var item in new[]{(before,ClassicEdition.BeforeNovember2014),(after,ClassicEdition.November2014AndNewer)})
{
 var hash=Convert.ToHexString(SHA256.HashData(item.Item1));
 var source=new DataProvenance("X-Rite ColorChecker Classic",item.Item2.ToString(),"https://www.xrite.com/service-support/new_color_specifications_for_colorchecker_sg_and_classic_charts","X-Rite terms; no redistribution consent",RedistributionPermission.NotEstablished,hash);
 var conditions=new ObservationConditions(null,null,null,null,item.Item2==ClassicEdition.November2014AndNewer?"M0, no filter":null,"Original source does not declare white point/observer; newer source reports i1Pro2 M0/no filter");
 var result=ClassicCgatsImporter.Parse(item.Item1,source,item.Item2,conditions);var catalog=new ReferenceDataCatalog();catalog.Add(result);
 catalog.Add(ClassicCgatsImporter.Parse(item.Item1,source,item.Item2,conditions));
 bool conversionRefused=false,redistributionRefused=false,editionConflictRefused=false;
 try{ReferenceColorValidation.ToXyz(result.Patches[0],conditions);}catch(InvalidOperationException){conversionRefused=true;}
 try{source.RequireRedistribution();}catch(InvalidOperationException){redistributionRefused=true;}
 try{ClassicCgatsImporter.Parse(item.Item1,source,item.Item2==ClassicEdition.BeforeNovember2014?ClassicEdition.November2014AndNewer:ClassicEdition.BeforeNovember2014,conditions);}catch(InvalidDataException){editionConflictRefused=true;}
 if(!conversionRefused||!redistributionRefused||!editionConflictRefused||result.Patches.Count!=24)throw new Exception("Reference gate failed.");
 references.Add(new{source,result.Originator,PatchCount=result.Patches.Count,Encoding=result.Patches[0].Encoding.ToString(),Conditions=conditions,ConversionRefused=conversionRefused,RedistributionRefused=redistributionRefused,EditionConflictRefused=editionConflictRefused});
}
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Parser=ClassicCgatsImporter.ParserVersion,References=references},new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine("2 official original files parsed; 48 patches, unknown-condition conversion/redistribution/version conflicts refused.");

using System.Text.Json;
using System.Security.Cryptography;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorScience;
var output=args[0];Directory.CreateDirectory(output);var samples=new Dictionary<string,float[]>{["neutral-toe-mid-white"]=[0,0,0,.0031308f,.0031308f,.0031308f,.18f,.18f,.18f,.75f,.75f,.75f,1,1,1],["primaries-and-skin"]=[1,0,0,0,1,0,0,0,1,.4f,.25f,.18f,.41f,.25f,.18f],["negative-extended"]=[-.1f,.2f,1.5f,2,2,2]};
var rows=new List<object>();foreach(var pair in samples)foreach(var ev in new[]{-2d,-1d,0d,1d,2d})
{
 var recipe=new Pv7Recipe(ExposureEv:ev);var result=Pv7ColorPrototype.Render(pair.Value,recipe);var sourceHash=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(pair.Value.AsSpan())));
 rows.Add(new{sampleId=pair.Key,originalSha256=sourceHash,inputType="AnalystSyntheticLinearSrgbD65Float32",decoder="No decoder synthetic V1",matrixSource="IdentityLinearSrgbD65V1",whitePoint="D65",transfer="Linear input; sRGB OETF output",whiteBalanceAlreadyApplied=true,renderVersion=7,recipe,recipeKey=recipe.CacheKey,outputProfile="SyntheticSrgbMatrixTrcV1 (descriptor only; no device profile)",stages=new[]{result.Input,result.WorkingLinear,result.Output},values=result.DisplayEncoded,export16=Pv7ColorPrototype.Export16(result),CalibrationEligible=false});
}
File.WriteAllText(Path.Combine(output,"sample-manifest.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"sensor-probe.json"),JsonSerializer.Serialize(LinearInputProbe.Measure([50,150,1000,1200,250,100],[100,100,100],1000,new(2,1,3),new(1,0,0,0,1,0,0,0,1)),new JsonSerializerOptions{WriteIndented=true}));

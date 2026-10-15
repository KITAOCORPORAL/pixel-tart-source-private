using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.ColorScience;
var path=args[0];var bytes=File.ReadAllBytes(path);var hash=Convert.ToHexString(SHA256.HashData(bytes));var points=JsonSerializer.Deserialize<double[][]>(File.ReadAllText(args[1]))!;var results=new List<object>();
try{var profile=SyntheticIccMatrixReader.Read(bytes,hash,SyntheticIccMatrixReader.FixtureId);foreach(var p in points){var v=SyntheticIccMatrixReader.ToLinearSrgb(new(p[0],p[1],p[2]),profile);results.Add(new{input=p,linearSrgb=new[]{v.X,v.Y,v.Z}});}File.WriteAllText(args[2],JsonSerializer.Serialize(new{status="ACCEPTED_DECLARED_SYNTHETIC_SUBSET",hash,profile,results},new JsonSerializerOptions{WriteIndented=true}));}
catch(Exception e){File.WriteAllText(args[2],JsonSerializer.Serialize(new{status="REFUSED",hash,error=e.GetType().Name,message=e.Message}));}

using System.Windows.Media;using System.Windows.Media.Imaging;using System.IO;using System.Text.Json;using System.Security.Cryptography;using Kitao.InstantFilm.Research;using RAWSelectionAssistant.Core.Services.Color;
if(args.Length is <1 or >3||File.Exists(args[0]))throw new ArgumentException("New matrix JSON path required");
var input=new HighBitDepthImageBuffer(47,39,Enumerable.Range(0,47*39*3).Select(i=>(i%233)/232f).ToArray(),"32f","sRGB",1);var rows=new List<object>();
if(args.Length==3)
{
 using var frozen=File.OpenRead(args[2]);var frame=BitmapDecoder.Create(frozen,BitmapCreateOptions.PreservePixelFormat|BitmapCreateOptions.IgnoreColorProfile,BitmapCacheOption.OnDemand).Frames[0];var rgb=new FormatConvertedBitmap(frame,PixelFormats.Rgb48,null,0);var raw=new ushort[47*39*3];rgb.CopyPixels(new System.Windows.Int32Rect(frame.PixelWidth/2,frame.PixelHeight/2,47,39),raw,47*6,0);input=new HighBitDepthImageBuffer(47,39,raw,"16","sRGB",1);
}
foreach(var stock in new[]{"Base","Womb01351.Stock0","Womb01351.Stock1","Womb01351.Stock2","Womb01351.Stock3","Womb01351.Stock4","Womb01351.Stock5"})
{
 var r=CandidateRecipe.Create("modern-itype-v3",42) with{Creative=new(Version:args.Length>=2?2:1,Stock:stock),Surface=new(Frame:false)};
 float[] Render(CandidateRecipe recipe){using var owner=CandidateSpatial.RenderRegion(input,recipe,0,0,input.Width,input.Height);using var borrowed=owner.Borrow();return borrowed.Image.Rgb32.ToArray();}
 var baseline=Render(r);var variants=new Dictionary<string,CandidateRecipe>{["Exposure"]=r with{Response=r.Response with{ExposureEv=.7}},["Toe"]=r with{Response=r.Response with{Toe=.15}},["Shoulder"]=r with{Response=r.Response with{Shoulder=1}},["Softness"]=r with{Optics=r.Optics with{DetailMix=.8}},["Diffusion"]=r with{Optics=r.Optics with{HighlightDiffusion=.8}},["Emulsion"]=r with{Emulsion=r.Emulsion with{Microstructure=.04}},["Expired"]=r with{Emulsion=r.Emulsion with{ExpiredAging=.8}},["Vignette"]=r with{Creative=r.Creative! with{LensVignette=.8}}};
 foreach(var pair in variants){var result=Render(pair.Value);var delta=baseline.Zip(result).Select(x=>Math.Abs(x.First-x.Second)).ToArray();rows.Add(new{stock,parameter=pair.Key,effective=delta.Any(x=>x>0),maximum=delta.Max(),mean=delta.Average(),inputSha256=Convert.ToHexString(SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(input.Rgb32.Span))),component=r.Creative!.Version});}
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{rows.Count} influences, {rows.Count(x=>!JsonSerializer.Serialize(x).Contains("\"effective\":true"))} inactive");

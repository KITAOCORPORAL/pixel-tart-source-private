using System.Text.Json;using RAWSelectionAssistant.Core.Services.Color.PhotoWideV1;using RAWSelectionAssistant.Core.Services.Color;
if(args.Length>=1&&args[0]=="linear-stages")
{
 var values=new[]{-.2,-.01,0,.0031308,Math.BitDecrement(.040449936),.040449936,Math.BitIncrement(.040449936),Math.BitDecrement(.04045),.04045,Math.BitIncrement(.04045),(double)MathF.BitDecrement(.04045f),(double).04045f,(double)MathF.BitIncrement(.04045f),.18,.5,1,1.5,4};
 var vectors=values.Select(x=>new ColorVector3(x,x,x)).Concat(new[]{new ColorVector3(-.1,.003,.8),new(.04045,.5,1),new(1.5,2,.2),new(1,0,0),new(0,1,0),new(0,0,1)}).ToList();
 if(args.Length==2){var extra=JsonSerializer.Deserialize<double[][]>(File.ReadAllText(args[1]))!;vectors.AddRange(extra.Select(x=>new ColorVector3(x[0],x[1],x[2])));}
 ColorVector3 Map(ColorVector3 v,Func<double,double> f)=>new(f(v.X),f(v.Y),f(v.Z));
 var result=from ev in new[]{-2d,0,.7,2} from input in vectors let decoded=Map(input,PhotoColorMathV1.DecodeSrgb) let exposed=Map(decoded,x=>x*Math.Pow(2,ev)) let encoded=Map(exposed,PhotoColorMathV1.EncodeSrgb) select new{input,ev,decoded,exposed,encoded,output=Map(encoded,x=>Math.Clamp(x,0,1)),productionOutput=new RenderedSrgbExposure(ExposureEv:ev).Apply(input)};
 Console.WriteLine(JsonSerializer.Serialize(result));return;
}
if(args.Length==1&&args[0]=="linear-chain")
{
 var vectors=new[]{new ColorVector3(-.1,.003,.8),new(0,0,0),new(.04045,.5,1),new(1.5,2,.2)};
 var results=from ev in new[]{-2d,0,.7,2} from rgb in vectors select new{input=rgb,ev,output=new ColorVector3(Math.Clamp(PhotoColorMathV1.EncodeSrgb(PhotoColorMathV1.DecodeSrgb(rgb.X)*Math.Pow(2,ev)),0,1),Math.Clamp(PhotoColorMathV1.EncodeSrgb(PhotoColorMathV1.DecodeSrgb(rgb.Y)*Math.Pow(2,ev)),0,1),Math.Clamp(PhotoColorMathV1.EncodeSrgb(PhotoColorMathV1.DecodeSrgb(rgb.Z)*Math.Pow(2,ev)),0,1))};
 Console.WriteLine(JsonSerializer.Serialize(results));return;
}
var samples=new[]{-.2,-.01,0,1e-8,.00262409-1e-10,.00262409,.00262409+1e-10,.0031308,.0078125-1e-10,.0078125,.0078125+1e-10,.018,.18,1,4,100};var rows=samples.Select(x=>new{x,di=PhotoColorMathV1.EncodeDi(x),acescct=PhotoColorMathV1.EncodeAcesCct(x),srgb=PhotoColorMathV1.EncodeSrgb(x),bt709=PhotoColorMathV1.Encode709(x),bt2020=PhotoColorMathV1.Encode2020(x)}).ToArray();var pq=new[]{0d,.001,.1,1,10,100,1000,10000}.Select(x=>new{x,encoded=PhotoColorMathV1.EncodePqNits(x),decoded=PhotoColorMathV1.DecodePqNits(PhotoColorMathV1.EncodePqNits(x))});var cdl=new[]{new ColorVector3(-.2,.18,1.5),new(.1,.4,.8),new(0,0,0),new(1,1,1)}.Select(x=>new{input=x,clamp=PhotoColorMathV1.Cdl(x,new(1.1,.9,1.2),new(.01,-.02,.03),new(.9,1.1,1.2),.8,CdlClampV1.ClampSopAndOutput),noClamp=PhotoColorMathV1.Cdl(x,new(1.1,.9,1.2),new(.01,-.02,.03),new(.9,1.1,1.2),.8,CdlClampV1.NoClampNonNegativePower)});
var inverseSamples=new[]{-.2,0,.02740668-1e-10,.02740668+1e-10,.155251141552511-1e-10,.155251141552511+1e-10,.5,1,1.5}.Select(x=>new{x,di=PhotoColorMathV1.DecodeDi(x),acescct=PhotoColorMathV1.DecodeAcesCct(x),srgb=PhotoColorMathV1.DecodeSrgb(x),bt709=PhotoColorMathV1.Decode709(x),bt2020=PhotoColorMathV1.Decode2020(x)});
Console.WriteLine(JsonSerializer.Serialize(new{rows,pq,cdl,inverseSamples,matrices=new{dwg=PhotoColorMathV1.DwgToXyz,xyzToDwg=PhotoColorMathV1.XyzToDwg,srgb=PhotoColorMathV1.SrgbToXyz,bt2020=PhotoColorMathV1.Bt2020ToXyz,ap1D60=PhotoColorMathV1.Ap1ToXyzD60}},new JsonSerializerOptions{WriteIndented=true}));

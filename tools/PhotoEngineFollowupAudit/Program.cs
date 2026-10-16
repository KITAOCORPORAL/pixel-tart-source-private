using System.Text.Json;
using Sdcb.LibRaw;
using Sdcb.LibRaw.Natives;
if(args.Length==5&&args[0]=="native-stages"){NativeRawAudit.Run(args[1],args[2],args[3],int.Parse(args[4]));return;}
if(args.Length==3&&args[0]=="wic-matrix"){WicMatrixAudit.Run(args[1],args[2]);return;}
if(args.Length==3&&args[0]=="sony-chain"){await SonyProductChain.Run(args[1],args[2]);return;}
if(args.Length==3&&args[0]=="sony-old"){await SonyProbe.Run(args[1],args[2]);return;}
if(args.Length==1&&args[0]=="api-fields")
{
 var types = new[]{typeof(RawContext),typeof(LibRawData),typeof(LibRawRawData),typeof(LibRawImageSizes),typeof(LibRawNative)};
 Console.WriteLine(JsonSerializer.Serialize(types.Select(t=>new{type=t.FullName,fields=t.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Select(f=>new{f.Name,fieldType=f.FieldType.ToString()}),properties=t.GetProperties(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Select(p=>new{p.Name,propertyType=p.PropertyType.ToString()}),methods=t.GetMethods().Where(m=>m.Name.Contains("Callback")||m.Name.Contains("Progress")||m.Name.Contains("Image")).Select(m=>m.ToString())}),new JsonSerializerOptions{WriteIndented=true}));
 return;
}
throw new ArgumentException("api-fields");

using System.Buffers.Binary;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
using RAWSelectionAssistant.Core.Services.Projects;
if(args.Length==4){
 if(args[2] is not("u16x4" or "f32"))throw new ArgumentException("Unknown input domain kind");
 var input=args[0];var output=args[1];var kind=args[2];var ev=double.Parse(args[3],System.Globalization.CultureInfo.InvariantCulture);using var src=File.OpenRead(input);using var dst=new FileStream(output,FileMode.CreateNew);var n=kind=="u16x4"?8:12;if(src.Length%n!=0)throw new InvalidDataException("pixel stride mismatch");var ib=new byte[n*8192];var ob=new byte[24*8192];long pixels=0;
 while(true){var count=src.ReadAtLeast(ib,n,throwOnEndOfStream:false);if(count==0)break;if(count%n!=0)throw new InvalidDataException("truncated row");int j=0;for(int i=0;i<count;i+=n){double R(int c)=>kind=="u16x4"?BinaryPrimitives.ReadUInt16LittleEndian(ib.AsSpan(i+c*2))/65535f:BinaryPrimitives.ReadSingleLittleEndian(ib.AsSpan(i+c*4));var v=ProfessionalRawPipeline.Display(ProfessionalRawPipeline.ApplyExposure(new ColorVector3(R(0),R(1),R(2)),ev));BinaryPrimitives.WriteDoubleLittleEndian(ob.AsSpan(j),v.X);BinaryPrimitives.WriteDoubleLittleEndian(ob.AsSpan(j+8),v.Y);BinaryPrimitives.WriteDoubleLittleEndian(ob.AsSpan(j+16),v.Z);j+=24;}dst.Write(ob,0,j);pixels+=count/n;}
 Console.WriteLine(JsonSerializer.Serialize(new{pixels,ev,kind,method="actual product Display/ApplyExposure"}));return 0;
}
while(Console.ReadLine() is string line){var v=JsonSerializer.Deserialize<double[]>(line)??throw new ArgumentException("null");if(v.Length!=4)throw new ArgumentException("expected four values");var p=ProfessionalRawPipeline.Display(ProfessionalRawPipeline.ApplyExposure(new ColorVector3(v[0],v[1],v[2]),v[3]));Console.WriteLine(JsonSerializer.Serialize(new[]{p.X,p.Y,p.Z}));}return 0;

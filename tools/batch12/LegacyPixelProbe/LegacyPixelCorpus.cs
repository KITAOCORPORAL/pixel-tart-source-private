using RAWSelectionAssistant.Core.Models;
using System.Security.Cryptography;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.RawToJpeg;
namespace RAWSelectionAssistant.Core.ValidationTools;
public static class LegacyPixelCorpus { public static Dictionary<string,string> Capture() {
var results = new Dictionary<string,string>();
for(ushort orientation=1;orientation<=8;orientation++)
for(var pv=4;pv<=6;pv++)
for(var film=0;film<=2;film++)
{
    var data=Enumerable.Range(0,8*6*3).Select(i=>new[]{0f,.0031308f,.04045f,.18f,.5f,1f,1.2f}[i%7]).ToArray();
    var d=pv==4?new ProfessionalRawDefinition(ExposureEv:.5):new ProfessionalRawDefinition(5,"Rendered",InputConfiguration:"RenderedSrgbFloatV1",DisplayTransform:"RenderedSrgbIdentityV1");
    ProfessionalRawMaster master;
    if(pv==6)
    {
        var recipe=new InstantInputRecipe(2,"CurrentEdits",Guid.Parse("00000000-0000-0000-0000-000000000001"),Stack:new([new(Guid.Parse("00000000-0000-0000-0000-000000000002"),ColorStudioNodeType.BasicTone,"Exposure",NumericParameters:new Dictionary<string,double>{{"exposure",1.25}})],ProcessingVersion:2),Film:new(true,GrainAmount:12,Seed:42)) {LegacyRaw=new(1,2,new string('a',64))};
        d=LegacyRawInstantInput.Create(recipe);
        master=LegacyRawInstantInput.Render(new("synthetic.arw",new string('a',64),Guid.Parse("00000000-0000-0000-0000-000000000003"),new(8,6,data,"16","sRGB",orientation)),d,new(new NeverDecoder()));
    }
    else master=new(new(8,6,data,"16",pv==4?ProfessionalRawPipeline.WorkingSpace:"sRGB",orientation),new("Synthetic","Test","Synthetic",new string('a',64),d.DecodeKey,[1,1,1,1],[1,1,1,1],[],65535));
    if(film>0)d=d with{InstantFilm=new(true,AlgorithmVersion:film,PaperFrame:false)};
    d=JsonSerializer.Deserialize<ProfessionalRawDefinition>(JsonSerializer.Serialize(d))!;
    var rendered=ProfessionalRawPipeline.RenderOutput(master,d);var bytes=new byte[rendered.Rgb32.Length*4];Buffer.BlockCopy(rendered.Rgb32.ToArray(),0,bytes,0,bytes.Length);
    results[$"PV{pv}-orientation{orientation}-film{film}"]=$"{rendered.Width}x{rendered.Height}:{rendered.Orientation}:"+Convert.ToHexString(SHA256.HashData(bytes));
}
return results;
}
sealed class NeverDecoder:IRawDecoder
{
 public RawDecoderCapability GetCapability()=>throw new NotSupportedException();
 public Task<RawDecodedImage> DecodeAsync(string path,RawToJpegOptions options,CancellationToken token=default)=>throw new NotSupportedException();
}


}

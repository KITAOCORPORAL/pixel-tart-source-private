using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Color;
namespace Kitao.InstantFilm.Research;
/// <summary>Explicit first-party STYLE_APPROXIMATION, never a measured camera/film model.</summary>
public sealed record FrozenInstantColor(int Version,string Id,string ModelSha256,string DataSha256,string ModelJson)
{
    public FrozenInstantColor Validate()
    {
        if(Version!=1||ModelJson is null||ModelJson.Length>16000||!Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ModelJson))).Equals(ModelSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("色彩组件内容/hash不匹配，未使用最新同名配置替代。");
        using var d=JsonDocument.Parse(ModelJson);var p=d.RootElement;
        if(p.GetProperty("stable_id").GetString()!=Id||p.GetProperty("evidence_grade").GetString()!="STYLE_APPROXIMATION"||p.GetProperty("calibration_eligible").GetBoolean()||p.GetProperty("physical_or_spectral_model").GetBoolean()||p.GetProperty("data_manifest_sha256").GetString()!=DataSha256)throw new InvalidDataException("色彩组件证据/输入域无效。");
        if(!InstantColorComponents.KnownHash(Id,ModelSha256,DataSha256))throw new InvalidDataException("未知色彩组件版本；保留旧定义，未静默回退。");return this;
    }
    public bool BlackAndWhite=>Id=="polaroid-modern-bw-style-smooth-1";
}
public static class InstantColorComponents
{
    private sealed record Entry(string id,string resource,string modelSha256,string dataSha256,string kind);
    private static System.Reflection.Assembly Assembly=>typeof(InstantColorComponents).Assembly;
    private static Stream Read(string file)=>Assembly.GetManifestResourceStream("RAWSelectionAssistant.Core.Resources.InstantColor."+file)??throw new FileNotFoundException("缺少冻结色彩组件资源："+file);
    private static readonly Entry[] Index=JsonSerializer.Deserialize<Entry[]>(Read("index.json"))!;
    public static bool KnownHash(string id,string hash,string dataHash)=>Index.Any(e=>e.id==id&&e.modelSha256.Equals(hash,StringComparison.OrdinalIgnoreCase)&&e.dataSha256==dataHash);
    public static FrozenInstantColor Freeze(string id)
    {
        var e=Index.Single(x=>x.id==id);using var stream=Read(e.resource);using var reader=new StreamReader(stream,Encoding.UTF8);return new FrozenInstantColor(1,id,e.modelSha256,e.dataSha256,reader.ReadToEnd()).Validate();
    }
    public static ColorVector3 ApplyLinear(ColorVector3 display,FrozenInstantColor model,double exposureEv)
    {
        var c=CandidateTone.Decode(display);double gain=Math.Pow(2,exposureEv);c=new(c.X*gain,c.Y*gain,c.Z*gain);var l=CandidateTone.Luma(c);var toe=l*l/(l+.025+1e-12);var u=toe/(1+.3*toe);var signal=.988*Math.Clamp(u,0,1);var t=.012+signal;
        if(model.BlackAndWhite)return new(t,t,t);
        var saturation=.9/(1+.25*Math.Max(Math.Max(c.X,c.Y),c.Z));var scale=l>1e-12?signal/l:0;
        return new(Math.Clamp(t+(c.X-l)*scale*saturation,0,1),Math.Clamp(t+(c.Y-l)*scale*saturation,0,1),Math.Clamp(t+(c.Z-l)*scale*saturation,0,1));
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using static PlanningReview.Rules;

namespace PlanningReview;

// Only structured content is persisted; no submitted HTML, URLs or file paths are interpreted.
internal static class WorkSchema
{
    static void Keys(JsonElement v,string names){Need(v.ValueKind==JsonValueKind.Object,"需要对象");var allowed=names.Split(' ').ToHashSet(StringComparer.Ordinal);var used=new HashSet<string>();foreach(var p in v.EnumerateObject())Need(allowed.Contains(p.Name)&&used.Add(p.Name),"无效或重复的字段："+p.Name);}
    static JsonElement Require(JsonElement v,string name){Need(v.TryGetProperty(name,out var value),"缺少字段："+name);return value;}
    static JsonElement[] Arr(JsonElement v,string key,int limit){var a=Require(v,key);Need(a.ValueKind==JsonValueKind.Array&&a.GetArrayLength()<=limit,"集合超限："+key);return a.EnumerateArray().ToArray();}
    static string Str(JsonElement v,int max){Need(v.ValueKind==JsonValueKind.String && v.GetString()!.Length<=max,"文本字段无效或过长");return v.GetString()!;}
    static void Choice(JsonElement o,string key,params string[] values){if(o.TryGetProperty(key,out var v))Need(values.Contains(Str(v,24)),"无效样式："+key);}
    static void Flag(JsonElement o,string key){if(o.TryGetProperty(key,out var v))Need(v.ValueKind is JsonValueKind.True or JsonValueKind.False,"无效标记："+key);}
    static Guid Id(JsonElement o,string k="id"){var v=Require(o,k);Guid id=Guid.Empty;Need(v.ValueKind==JsonValueKind.String&&v.TryGetGuid(out id)&&id!=Guid.Empty,"无效身份："+k);return id;}
    static double Num(JsonElement v,double min,double max){double n=0;Need(v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out n)&&double.IsFinite(n)&&n>=min&&n<=max,"数字超出范围");return n;}
    static void Range(JsonElement o,string k,double min,double max){if(o.TryGetProperty(k,out var v))Num(v,min,max);}
    static void Color(JsonElement o,string key){if(o.TryGetProperty(key,out var v))Need(Regex.IsMatch(Str(v,12),"^#[A-Fa-f0-9]{6}$"),"颜色须为 #RRGGBB");}
    static void Marks(JsonElement o,string text)
    {if(!o.TryGetProperty("marks",out _))return;int previous=0;foreach(var m in Arr(o,"marks",4096)){Keys(m,"start end bold italic underline color size highlight font spacing");var s=Require(m,"start");var e=Require(m,"end");int x=0,y=0;Need(s.ValueKind==JsonValueKind.Number&&s.TryGetInt32(out x)&&e.ValueKind==JsonValueKind.Number&&e.TryGetInt32(out y)&&x>=previous&&y>x&&y<=text.Length,"文字格式区间无效");previous=y;foreach(var k in new[]{"bold","italic","underline"})Flag(m,k);Color(m,"color");Color(m,"highlight");Range(m,"size",8,126);Choice(m,"font","sans","serif","mono");Range(m,"spacing",-1,12);}}
    static void Asset(Notebook book,Guid id,bool allowRemoved=false)=>Need(book.Blocks.Any(b=>b.Id==id&&b.Kind=="image"&&(allowRemoved||!b.Removed)),"参考图不是本项目的可用副本",409);
    public static void Page(Notebook book,string kind,JsonElement d,bool allowRemovedAssets=false)
    {
        Need(d.GetRawText().Length<=400000,"单页内容超限");var ids=new HashSet<Guid>();
        switch(kind)
        {
            case "doc": Keys(d,"paragraphs assetIds");if(d.TryGetProperty("assetIds",out _)){var refs=new HashSet<Guid>(); foreach(var a in Arr(d,"assetIds",100)){Guid id=Guid.Empty;Need(a.ValueKind==JsonValueKind.String && a.TryGetGuid(out id)&&refs.Add(id),"正文参考图身份无效");Asset(book,id,allowRemovedAssets);}}foreach(var p in Arr(d,"paragraphs",1000)){
                Keys(p,"id type style text checked align marks lineHeight");Need(ids.Add(Id(p)),"段落身份重复");var text=Str(Require(p,"text"),24000);Choice(p,"type","p","h1","h2","h3","li","check","ul","ol","quote","caption");Range(p,"lineHeight",1,3);Choice(p,"style","body","h1","h2","quote");Choice(p,"align","left","center","right");Flag(p,"checked");Marks(p,text);}break;
            case "table": Keys(d,"columns rows");var cols=new HashSet<string>(StringComparer.Ordinal);foreach(var c in Arr(d,"columns",40)){Keys(c,"key label");var key=Str(Require(c,"key"),64);Need(Regex.IsMatch(key,"^[A-Za-z0-9_-]+$")&&key is not ("__proto__" or "prototype" or "constructor")&&cols.Add(key),"列标识无效或重复");Short(Str(Require(c,"label"),100),100);} Need(cols.Count>0,"至少需要一列");
                foreach(var row in Arr(d,"rows",1000)){Keys(row,"id cells");Need(ids.Add(Id(row)),"行身份重复");var cells=Require(row,"cells");Need(cells.ValueKind==JsonValueKind.Object,"缺少表格单元");var seen=new HashSet<string>();foreach(var cell in cells.EnumerateObject()){Need(cols.Contains(cell.Name)&&seen.Add(cell.Name),"未知或重复的列");Str(cell.Value,12000);}}break;
            case "canvas": Keys(d,"width height objects");Range(d,"width",100,8000);Range(d,"height",100,8000);foreach(var o in Arr(d,"objects",600)){
                Keys(o,"id kind x y w h rotate rotation group groupId assetId text fill color size shape crop marks align stroke strokeWidth opacity lineHeight");Need(ids.Add(Id(o)),"对象身份重复");var k=Str(Require(o,"kind"),12);Need(k is "image" or "text" or "shape" or "line" or "arrow","未知画布对象");
                foreach(var c in new[]{"x","y"})Num(Require(o,c),-16000,16000);foreach(var c in new[]{"w","h"})Num(Require(o,c),0,16000);Range(o,"rotate",-3600,3600);Range(o,"rotation",-3600,3600);Range(o,"size",8,126);Range(o,"strokeWidth",0,100);Range(o,"opacity",0,1);if(o.TryGetProperty("lineHeight",out _)){Need(k=="text","行高只适用于文字对象");Range(o,"lineHeight",1,3);}Choice(o,"shape","rect","ellipse");Choice(o,"align","left","center","right");
                foreach(var g in new[]{"group","groupId"})if(o.TryGetProperty(g,out var group)&&group.ValueKind!=JsonValueKind.Null)Id(o,g);
                foreach(var c in new[]{"color","fill","stroke"})Color(o,c);
                if(k=="image")Asset(book,Id(o,"assetId"),allowRemovedAssets);else Need(!o.TryGetProperty("assetId",out var a)||a.ValueKind==JsonValueKind.Null,"仅图片可绑定素材");
                var text=o.TryGetProperty("text",out var t)?Str(t,24000):"";Marks(o,text);
                if(o.TryGetProperty("crop",out var crop)&&crop.ValueKind!=JsonValueKind.Null){Keys(crop,"x y w h");var x=Num(Require(crop,"x"),0,1);var y=Num(Require(crop,"y"),0,1);var w=Num(Require(crop,"w"),0,1);var h=Num(Require(crop,"h"),0,1);Need(k=="image"&&w>0&&h>0&&x+w<=1.000001&&y+h<=1.000001,"原图裁切越界");}
            }break;
            case "shots":Keys(d,"");break;
            default:throw new Problem(422,"未知页面类型");
        }
    }
    public static ShotDetail Shot(Notebook book,Guid id,JsonElement? data,int version,ShotDetail? current=null)
    {
        var shot=new ShotDetail{Id=id,Version=version,Requirements=current?.Requirements??"",AssetId=current?.AssetId,Fields=current?.Fields??[]};if(data is not {} d)return shot;
        Keys(d,"requirements assetId fields");if(d.TryGetProperty("requirements",out var text))shot.Requirements=Str(text,12000);
        if(d.TryGetProperty("assetId",out var a)){shot.AssetId=a.ValueKind==JsonValueKind.Null?null:Id(d,"assetId");if(shot.AssetId is {} asset)Asset(book,asset);}
        if(d.TryGetProperty("fields",out var f)){Need(f.ValueKind==JsonValueKind.Object,"分镜字段无效");var values=new Dictionary<string,string>();foreach(var p in f.EnumerateObject()){Need(values.Count<36&&p.Name.Length is >0 and <=64&&p.Name is not ("__proto__" or "constructor" or "prototype")&&values.TryAdd(p.Name,Str(p.Value,1000)),"分镜字段超限或重复");}shot.Fields=values;}
        return shot;
    }
}

using System.Text.Json;
using static PlanningReview.Rules;

namespace PlanningReview;

public sealed class WorkPage
{
    public Guid Id {get;set;}
    public string Title {get;set;}="";
    public string Section {get;set;}="";
    public string Kind {get;set;}="doc";
    public int Version {get;set;}=1;
    public JsonElement Data {get;set;}
    public bool Archived {get;set;}
    public List<RAWSelectionAssistant.Core.Services.Projects.PlanningShareSectionNotice> Notices {get;set;}=[];
}
public sealed class ShotDetail
{
    public Guid Id {get;set;}
    public int Version {get;set;}=1;
    public string Requirements {get;set;}="";
    public Guid? AssetId {get;set;}
    public Dictionary<string,string> Fields {get;set;}=[];
}
public sealed class Workbench
{
    public string PrivateTier {get;set;}="basic";
    public List<WorkPage> Pages {get;set;}=[];
    public int PageOrderVersion {get;set;}=1;
    public List<ShotDetail> ShotDetails {get;set;}=[];
    public object View(bool team)=>new {tier=team?"team":PrivateTier, pages=Pages.Where(p=>!p.Archived), pageOrderVersion=PageOrderVersion, shotDetails=ShotDetails, archivedPages=Pages.Where(p=>p.Archived).Select(p=>new {p.Id,p.Title,p.Version})};
    public static JsonElement Enc(object value)=>JsonSerializer.SerializeToElement(value,Json);
    static JsonElement Empty(string kind)=>kind switch {
        "doc"=>Enc(new {paragraphs=new[]{new {id=Guid.NewGuid(),type="p",text="",marks=Array.Empty<object>()}}}),
        "canvas"=>Enc(new {width=1000,height=670,objects=Array.Empty<object>()}),
        "table"=>Enc(new {columns=new[]{new {key="name",label="名称"}},rows=Array.Empty<object>()}),
        "shots"=>Enc(new {}), _=>throw new Problem(422,"未知页面类型")};
    public static Workbench Seed(Notebook book,List<ShotDetail> shots)
    {
        var wb=new Workbench {ShotDetails=shots};
        var rows=book.Blocks.Where(b=>b.Kind=="text"&&!b.Removed).Select(b=>new {id=Guid.NewGuid(),type="p",text=b.Text,marks=Array.Empty<object>()}).ToArray();
        foreach(var (kind,title) in new[]{("doc","策划正文"),("canvas","参考图板"),("shots","拍摄分镜")}) wb.Pages.Add(new(){Id=Guid.NewGuid(),Title=title,Section=kind=="doc"?"brief":kind=="canvas"?"reference":"shots",Kind=kind,Data=kind=="doc" && rows.Length>0?Enc(new {paragraphs=rows}):Empty(kind)});
        return wb;
    }
    static IEnumerable<JsonElement> Items(WorkPage p)
    {
        if(p.Kind=="canvas")return p.Data.GetProperty("objects").EnumerateArray();
        if(p.Kind!="doc")return [];
        return p.Data.GetProperty("paragraphs").EnumerateArray().SelectMany(WithChildren);
        static IEnumerable<JsonElement> WithChildren(JsonElement para)
        {
            yield return para;
            if(!para.TryGetProperty("nativeTable",out var table))yield break;
            foreach(var row in table.GetProperty("rows").EnumerateArray())foreach(var cell in row.GetProperty("cells").EnumerateArray())
                foreach(var child in cell.GetProperty("paragraphs").EnumerateArray())yield return child;
        }
    }
    static JsonElement? Item(WorkPage p,Guid id)
    {foreach(var a in Items(p))if(a.GetProperty("id").GetGuid()==id)return a;return null;}
    static string TextOf(JsonElement obj)=>obj.TryGetProperty("text",out var value)?value.GetString()??"":"";
    static bool PageHasAsset(WorkPage p,Guid id)=>(p.Data.TryGetProperty("assetIds",out var assets)&&assets.EnumerateArray().Any(a=>a.GetGuid()==id)) || Items(p).Any(i=>i.TryGetProperty("nativeImage",out var image)&&image.GetProperty("assetId").GetGuid()==id);
    static bool IsImage(JsonElement obj)=>obj.TryGetProperty("kind",out var k)&&k.GetString()=="image";
    static string Summary(WorkPage page)=>Enc(new {page.Title,page.Data}).GetRawText();
    static Guid? Asset(JsonElement obj)=>obj.TryGetProperty("assetId",out var value)&&value.ValueKind==JsonValueKind.String?value.GetGuid():null;
    public void PrepareAnchor(Notebook book,Anchor anchor,int version)
    {
        var page=Pages.Find(p=>p.Id==anchor.PageId)??throw new Problem(404,"页面不存在");
        anchor.Detached=page.Archived;
        if ((page.Kind=="doc" && anchor.Rect==null) || (page.Kind=="canvas" && (anchor.Quote?.Length??0)>0))
        {
            var node=Item(page,anchor.Target);Need(page.Kind=="doc"||anchor.ObjectId==anchor.Target,"文本框身份不匹配");
            Need(node==null||page.Kind=="doc"||node.Value.GetProperty("kind").GetString()=="text","目标不含文字");
            Need(anchor.Quote is {Length:>0 and <=4000} && anchor.Start>=0,"请选择文字");
            string text=node==null?"":TextOf(node.Value);
            anchor.Detached|=node==null||version!=page.Version||anchor.Start>text.Length-anchor.Quote!.Length||!text.AsSpan(anchor.Start,anchor.Quote!.Length).SequenceEqual(anchor.Quote);
            anchor.Hash=null;anchor.Rect=null;return;
        }
        var block=book.Blocks.Find(b=>b.Id==anchor.Target)??throw new Problem(404,"位置不存在");
        if(block.Kind=="image")
        {
            var r=anchor.Rect; Need(r is {Length:4} && r.All(double.IsFinite) && r[0]>=0 && r[1]>=0 && r[2]>0 && r[3]>0 && r[0]+r[2]<=1.000001 && r[1]+r[3]<=1.000001 && block.Hash==anchor.Hash,"原图摘要或框选区域无效");
            if(page.Kind=="doc")anchor.Detached|=!PageHasAsset(page,block.Id);
            if(anchor.ObjectId is {} placement){if(page.Kind=="shots")anchor.Detached|=ShotDetails.Find(s=>s.Id==placement)?.AssetId!=block.Id || !book.Order.Contains(placement);
                else if(page.Kind=="canvas"){var obj=Item(page,placement);anchor.Detached|=obj is null||!IsImage(obj.Value)||Asset(obj.Value)!=block.Id;}
                else if(page.Kind=="doc"){var obj=Item(page,placement);anchor.Detached|=obj is null||!obj.Value.TryGetProperty("nativeImage",out var inline)||inline.GetProperty("assetId").GetGuid()!=block.Id;}}
        }
        else {Need(block.Kind=="shot" && page.Kind=="shots","无效分镜锚点");anchor.Rect=null;anchor.Hash=null;}
        anchor.Quote="";anchor.Detached|=block.Removed;
    }
    static void TrackPage(Notebook book,WorkPage old,WorkPage updated)
    {
        foreach(var a in book.Threads.Select(t=>t.Anchor).Where(a=>a.PageId==old.Id&&!a.Detached))
        {
            if(updated.Archived){a.Detached=true;continue;}
            if(a.Quote.Length>0){var prev=Item(old,a.Target);var next=Item(updated,a.Target);if(prev==null||next==null || (updated.Kind=="canvas"&&next.Value.GetProperty("kind").GetString()!="text")){a.Detached=true;continue;}
                Rules.TrackAnchor(a,TextOf(prev.Value),TextOf(next.Value));}
            else if(updated.Kind=="doc" && a.Rect!=null && !PageHasAsset(updated,a.Target))a.Detached=true;
            else if(updated.Kind=="canvas" && a.ObjectId is {} placement){var obj=Item(updated,placement);if(obj==null||!IsImage(obj.Value)||Asset(obj.Value)!=a.Target)a.Detached=true;}
        }
    }
    public void Apply(Notebook book,Edit edit,Action<string,string> conflict)
    {
        var page=Pages.Find(p=>p.Id==edit.Target);
        switch(edit.Action)
        {
            case "wbPageAdd":
                Need(Pages.Count<200&&!Pages.Any(p=>p.Id==edit.Id),"页面数量超限或身份重复",409);
                var kind=edit.Kind??"doc"; var blank=Empty(kind);var data=edit.Data??blank;
                WorkSchema.Page(book,kind,data);Pages.Add(new(){Id=edit.Id,Kind=kind,Section=edit.Section==null?"":Short(edit.Section,40),Title=Short(edit.Text,120),Data=data});PageOrderVersion++;break;
            case "wbPageSet":case "wbPageDelete":case "wbPageRestore":
                Need(page!=null,"页面不存在",404);
                if(page!.Version!=edit.Base || page.Archived!=(edit.Action=="wbPageRestore")){conflict(Summary(page),Enc(new {edit.Text,edit.Data}).GetRawText());break;}
                var modified=new WorkPage{Id=page.Id,Kind=page.Kind,Section=edit.Section==null?page.Section:Short(edit.Section,40),Title=edit.Text.Length==0?page.Title:Short(edit.Text,120),Notices=page.Notices,Version=page.Version+1,Data=edit.Data??page.Data,Archived=edit.Action=="wbPageDelete"};
                if(edit.Action=="wbPageSet")WorkSchema.Page(book,page.Kind,modified.Data);
                TrackPage(book,page,modified);Pages[Pages.IndexOf(page)]=modified;if(edit.Action!="wbPageSet")PageOrderVersion++;break;
            case "wbPageMove":
                if(PageOrderVersion!=edit.Base||page==null||page.Archived||edit.Target==edit.After||edit.After!=Guid.Empty&&!Pages.Any(p=>p.Id==edit.After&&!p.Archived)) {conflict(string.Join(",",Pages.Where(p=>!p.Archived).Select(p=>p.Id)),edit.After.ToString());break;}
                Pages.Remove(page);Pages.Insert(edit.After==Guid.Empty?0:Pages.FindIndex(p=>p.Id==edit.After)+1,page);PageOrderVersion++;break;
            case "wbShotMove":
                if(book.OrderVersion!=edit.Base||!book.Order.Contains(edit.Target)||edit.After==edit.Target||edit.After!=Guid.Empty&&!book.Order.Contains(edit.After)){conflict(string.Join(",",book.Order),$"{edit.Target} → {edit.After}");break;}
                book.Order.Remove(edit.Target);book.Order.Insert(edit.After==Guid.Empty?0:book.Order.IndexOf(edit.After)+1,edit.Target);book.OrderVersion++;break;
            case "wbShotAdd":
                Need(book.Order.Count<2000&&!book.Blocks.Any(b=>b.Id==edit.Id),"分镜已存在或数量已达上限",409);
                Need(edit.After==Guid.Empty||book.Order.Contains(edit.After),"插入位置不存在",409);
                var added=WorkSchema.Shot(book,edit.Id,edit.Data,1);ShotDetails.Add(added);
                book.Blocks.Add(new(){Kind="shot",Id=edit.Id,Text=Short(edit.Text,180)});
                if(edit.After==Guid.Empty)book.Order.Add(edit.Id);else book.Order.Insert(book.Order.IndexOf(edit.After)+1,edit.Id);book.OrderVersion++;break;
            case "wbShotSet":case "wbShotArchive":
                var shot=ShotDetails.Find(s=>s.Id==edit.Target)??throw new Problem(404,"分镜不存在");var b=book.Blocks.Single(x=>x.Id==edit.Target&&x.Kind=="shot");
                if(shot.Version!=edit.Base||b.Removed){conflict(Enc(new {title=b.Text,detail=shot}).GetRawText(),Enc(new {edit.Text,edit.Data}).GetRawText());break;}
                if(edit.Action=="wbShotSet"){var replacement=WorkSchema.Shot(book,shot.Id,edit.Data,shot.Version+1,shot); if(!string.IsNullOrEmpty(edit.Text)){var title=Short(edit.Text,180);if(title!=b.Text)Rules.Track(book,b,title);} if(shot.AssetId!=replacement.AssetId)foreach(var a in book.Threads.Select(t=>t.Anchor).Where(a=>a.ObjectId==shot.Id && a.Target==shot.AssetId))a.Detached=true;
                    ShotDetails[ShotDetails.IndexOf(shot)]=replacement;}
                else {b.Removed=true;b.Version++;shot.Version++;book.Order.Remove(shot.Id);book.OrderVersion++;foreach(var a in book.Threads.Select(t=>t.Anchor).Where(a=>a.Target==shot.Id||a.ObjectId==shot.Id))a.Detached=true;}break;
        }
    }
}

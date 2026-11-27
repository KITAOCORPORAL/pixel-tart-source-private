using System.Text.Json;

namespace PlanningReview;

/// <summary>Historical selectors can guide reattachment; archived page/block payloads never replace current native content.</summary>
internal static class NativeShareHistory
{
    public static void Reattach(Notebook book,IReadOnlyList<Block> oldBlocks,IReadOnlyList<WorkPage> oldPages)
    {
        var workbench=book.Workbench!;
        foreach(var thread in book.Threads)
        {
            var anchor=thread.Anchor;
            if(anchor.Detached)continue;
            var oldBlock=oldBlocks.SingleOrDefault(b=>b.Id==anchor.Target);
            var oldPage=oldPages.SingleOrDefault(p=>p.Id==anchor.PageId);
            anchor.Detached=true;
            if(oldBlock?.Removed==true||anchor.PageId is not null&&(oldPage is null||oldPage.Archived))continue;
            var section=oldPage?.Section ?? (anchor.Rect is not null?"reference":anchor.Quote?.Length>0?"brief":"shots");
            var next=workbench.Pages.SingleOrDefault(p=>p.Section==section&&!p.Archived);
            if(next is null)continue;
            if(anchor.Rect is not null)
            {
                if(!ReattachImage(book,next,anchor))continue;
            }
            else if(anchor.Quote?.Length>0)
            {
                if(!ReattachText(oldBlock,oldPage,next,anchor))continue;
            }
            else if(next.Kind!="shots"||!book.Order.Contains(anchor.Target)||!book.Blocks.Any(b=>b.Id==anchor.Target&&b.Kind=="shot"&&!b.Removed))continue;
            anchor.PageId=next.Id;
            anchor.Detached=false;
        }
    }

    private static bool ReattachText(Block? oldBlock,WorkPage? oldPage,WorkPage page,Anchor anchor)
    {
        var oldText=oldPage is null?oldBlock?.Text:Text(Nodes(oldPage).FirstOrDefault(n=>Identity(n)==anchor.Target));
        if(oldText is null||anchor.Quote is not {Length:>0 and <=4000}||anchor.Start<0||anchor.Start>oldText.Length-anchor.Quote.Length||
            !oldText.AsSpan(anchor.Start,anchor.Quote.Length).SequenceEqual(anchor.Quote))return false;
        var matching=Nodes(page).Where(n=>Text(n)==oldText).ToArray();
        if(matching.Length!=1||Identity(matching[0]) is not Guid id)return false;
        anchor.Target=id;anchor.ObjectId=page.Kind=="canvas"?id:null;return true;
    }

    private static bool ReattachImage(Notebook book,WorkPage page,Anchor anchor)
    {
        var image=book.Blocks.SingleOrDefault(b=>b.Id==anchor.Target&&b.Kind=="image"&&!b.Removed);
        if(image is null||!string.Equals(anchor.Hash,image.Hash,StringComparison.OrdinalIgnoreCase)||anchor.Rect is not {Length:4} r||!r.All(double.IsFinite)||
            r[0]<0||r[1]<0||r[2]<=0||r[3]<=0||r[0]+r[2]>1.000001||r[1]+r[3]>1.000001)return false;
        if(page.Kind=="shots")
        {
            if(anchor.ObjectId is not Guid shot||!book.Order.Contains(shot)||!book.Workbench!.ShotDetails.Any(s=>s.Id==shot&&s.AssetId==image.Id))return false;
            anchor.Hash=image.Hash;return true;
        }
        var placements=Nodes(page).Where(n=>PlacedAsset(n)==image.Id).Select(n=>Identity(n)).OfType<Guid>().ToArray();
        if(anchor.ObjectId is Guid old&&placements.Contains(old)){anchor.Hash=image.Hash;return true;}
        if(placements.Length==1){anchor.ObjectId=placements[0];anchor.Hash=image.Hash;return true;}
        if(page.Kind=="doc"&&placements.Length==0&&page.Data.TryGetProperty("assetIds",out var assets)&&assets.ValueKind==JsonValueKind.Array&&assets.EnumerateArray().Any(a=>a.ValueKind==JsonValueKind.String&&a.TryGetGuid(out var id)&&id==image.Id))
        {anchor.ObjectId=null;anchor.Hash=image.Hash;return true;}
        return false;
    }
    private static IEnumerable<JsonElement> Nodes(WorkPage page)
    {
        if(page.Data.ValueKind!=JsonValueKind.Object)yield break;
        if(page.Kind=="canvas"&&page.Data.TryGetProperty("objects",out var objects)&&objects.ValueKind==JsonValueKind.Array)
        {foreach(var node in objects.EnumerateArray())yield return node;yield break;}
        if(page.Kind!="doc"||!page.Data.TryGetProperty("paragraphs",out var paragraphs)||paragraphs.ValueKind!=JsonValueKind.Array)yield break;
        foreach(var p in paragraphs.EnumerateArray())
        {
            yield return p;
            if(p.ValueKind!=JsonValueKind.Object||!p.TryGetProperty("nativeTable",out var table))continue;
            foreach(var row in Array(table,"rows"))foreach(var cell in Array(row,"cells"))foreach(var child in Array(cell,"paragraphs"))yield return child;
        }
    }
    private static IEnumerable<JsonElement> Array(JsonElement o,string key)=>o.ValueKind==JsonValueKind.Object&&o.TryGetProperty(key,out var a)&&a.ValueKind==JsonValueKind.Array?a.EnumerateArray():[];
    private static string? Text(JsonElement o)=>o.ValueKind==JsonValueKind.Object&&o.TryGetProperty("text",out var text)&&text.ValueKind==JsonValueKind.String?text.GetString():null;
    private static Guid? Identity(JsonElement o,string key="id")=>o.ValueKind==JsonValueKind.Object&&o.TryGetProperty(key,out var id)&&id.ValueKind==JsonValueKind.String&&id.TryGetGuid(out var value)?value:null;
    private static Guid? PlacedAsset(JsonElement o)=>o.ValueKind==JsonValueKind.Object&&o.TryGetProperty("nativeImage",out var image)?Identity(image,"assetId"):Identity(o,"assetId");
}

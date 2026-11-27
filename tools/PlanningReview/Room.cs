using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static PlanningReview.Rules;

namespace PlanningReview;

public sealed class Room
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<(Guid Person, Guid Device), (DateTimeOffset At, Guid? Target)> presence = [];
    readonly string root;
    readonly byte[] key;
    readonly NativePlan native;
    Notebook book;
    readonly Dictionary<Guid, DateTimeOffset> ownerTickets = [];
    Room(string root, byte[] key, NativePlan native, Notebook book) { this.root = root; this.key = key; this.native = native; this.book = book; }
    public static async Task<Room> Load(string root, Guid project, bool workbench=false, Guid? publishedDocument=null, bool existingOnly=false)
    {
        var keyPath = Path.Combine(root, "identity.key"); var path = Path.Combine(root, "notebook.json");
        if (existingOnly && (!File.Exists(path) || !File.Exists(keyPath))) throw new InvalidDataException("在线房间的已发布状态缺失，保留目录并停止。");
        if (!File.Exists(keyPath) && File.Exists(path)) throw new InvalidDataException("缺少原身份密钥，保留项目并停止");
        if (!File.Exists(keyPath)) NativePlan.Atomic(keyPath, RandomNumberGenerator.GetBytes(32));
        var native = new NativePlan(Path.Combine(root, "isolated"));
        var book = File.Exists(path) ? JsonSerializer.Deserialize<Notebook>(File.ReadAllBytes(path), Json)! : await native.Open(project,workbench,publishedDocument is not null);
        Need(book.Project == project && book.Schema == 1, "项目不匹配");
        Need((book.Workbench is not null)==workbench,"工作台必须使用独立数据目录；旧项目请通过副本导入。");
        if (publishedDocument is {} document)
        {
            Need(book.Document == document && book.People.Count(p => p.Owner && p.Active) == 1, "已发布策划文档或负责人身份不匹配");
            if (existingOnly) Need(book.Team, "在线房间不是团队状态");
            book.Team = true;
        }
        var room = new Room(root, File.ReadAllBytes(keyPath), native, book);
        if (!File.Exists(path)) room.Commit(book);
        return room;
    }
    void Commit(Notebook next) { NativePlan.Atomic(Path.Combine(root, "notebook.json"), JsonSerializer.SerializeToUtf8Bytes(next, Json)); book = next; }
    Notebook Copy() => JsonSerializer.Deserialize<Notebook>(JsonSerializer.SerializeToUtf8Bytes(book, Json), Json)!;
    string Code(string kind, Guid id)
    {
        var value = $"{kind}.{id:N}"; var mac = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(value));
        return value + "." + Convert.ToHexString(mac)[..40];
    }
    Guid Decode(string? code, string kind)
    {
        var parts = (code ?? "").Split('.');
        if (parts.Length != 3 || parts[0] != kind || !Guid.TryParseExact(parts[1], "N", out var id) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code!), Encoding.UTF8.GetBytes(Code(kind,id)))) throw new Problem(401, "身份凭据无效，请重新加入。");
        return id;
    }
    Person Member(string? code)
    {
        var id = Decode(code,"member"); return book.People.FirstOrDefault(p => p.Id == id && p.Active && (p.Owner || book.Team)) ?? throw new Problem(403, "项目访问权限已撤销。");
    }
    public string OwnerKey => Code("member",book.People.Single(p => p.Owner).Id);
    internal bool IsOwnerCredential(string? token) => token is {} value &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(OwnerKey));
    internal Task<string> IssueOwnerTicket() => Locked(() =>
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in ownerTickets.Where(t => t.Value <= now).Select(t => t.Key).ToArray()) ownerTickets.Remove(expired);
        // Polling also creates receipts; retain a bounded set of recently issued links for lost acknowledgements.
        if (ownerTickets.Count >= 128) ownerTickets.Remove(ownerTickets.MinBy(t => t.Value).Key);
        var id = Guid.NewGuid(); ownerTickets[id] = now.AddSeconds(90);
        return Task.FromResult(Code("owner-once", id));
    });
    async Task<T> Locked<T>(Func<Task<T>> run) { await gate.WaitAsync(); try { return await run(); } finally { gate.Release(); } }
    public Task<object> Join(string? code, bool published = false, bool loopback = false) => Locked<object>(() =>
    {
        Person person;
        if (code is null) throw new Problem(401, "身份凭据无效，请重新加入。");
        if (published && code.StartsWith("owner-once.", StringComparison.Ordinal))
        {
            Need(loopback, "负责人入口仅可在发布电脑上打开。", 403);
            var ticket = Decode(code, "owner-once");
            Need(ownerTickets.Remove(ticket, out var expires) && expires > DateTimeOffset.UtcNow, "负责人入口已使用或过期，请从桌面重新打开。", 401);
            person = book.People.Single(p => p.Owner && p.Active);
        }
        else if (code.StartsWith("member.", StringComparison.Ordinal))
        {
            person = Member(code);
            Need(!published || !person.Owner, "请从发布电脑的桌面打开负责人入口。", 403);
        }
        else
        {
            var id = Decode(code,"invite"); var next = Copy(); var index = next.Invitations.FindIndex(i => i.Id == id);
            Need(next.Team && index >= 0 && Pending(next.Invitations[index]), "邀请已使用、过期或撤销；换电脑请用本人迁移码。", 403);
            var invite = next.Invitations[index]; person = new() { Name = invite.Label }; next.People.Add(person);
            next.Invitations[index] = invite with { Claimed = person.Id }; next.Revision++; Commit(next);
        }
        return Task.FromResult<object>(new { token = Code("member",person.Id), person.Id, person.Name, book.Project });
    });
    public Task<object> View(string? token, string? memberUrl = null) => Locked<object>(() =>
    {
        var me = Member(token);
        var online = presence.Where(p => (DateTimeOffset.UtcNow - p.Value.At).TotalSeconds < 18 && book.People.Any(m => m.Id == p.Key.Person && m.Active)).GroupBy(p=>p.Key.Person).Select(g => { var item=g.MaxBy(p=>p.Value.At); return new { person=g.Key, target=item.Value.Target }; }).ToArray();
        var state = new { book.Project, book.Document, book.Title, book.Team, book.Revision, book.OrderVersion, book.Order, book.Blocks, book.Threads, book.Conflicts, people = book.People, me, online, memberSeats = MemberSeats, used = Used(book), invitations = me.Owner ? book.Invitations.Where(Pending).Select(i => new { i.Id, i.Label, i.Expires, code = Code("invite", i.Id) }).ToArray() : null };
        if (book.Workbench is not {} wb) return Task.FromResult<object>(state);
        var view=JsonSerializer.SerializeToElement(state,Json).EnumerateObject().ToDictionary(p=>p.Name,p=>(object)p.Value);
        view["workbench"]=wb.View(book.Team);
        if (memberUrl != null) view["memberUrl"] = memberUrl;
        return Task.FromResult<object>(view);
    });
    public Task<object> Pulse(string? token, Guid device, Guid? target) => Locked<object>(() =>
    {
        var actor = Member(token); Need(device != Guid.Empty, "缺少设备身份");
        foreach (var entry in presence.Where(p => (DateTimeOffset.UtcNow - p.Value.At).TotalSeconds > 30).Select(p=>p.Key).ToArray()) presence.Remove(entry);
        presence[(actor.Id, device)] = (DateTimeOffset.UtcNow, book.Blocks.Any(b => b.Id == target && !b.Removed) || book.Workbench?.Pages.Any(p=>p.Id==target)==true ? target : null);
        return Task.FromResult<object>(new { book.Revision });
    });
    public Task<(Stream stream,string mime)> Image(string? token, Guid id) => Locked(async () =>
    {
        Member(token); var block = book.Blocks.FirstOrDefault(b => b.Id == id && !b.Removed && b.Kind == "image") ?? throw new Problem(404, "参考图已移除");
        return (await native.Read(book, block),block.Mime!);
    });
    public Task<byte[]> Office(string? token) => Locked(() =>
    {
        Need(Member(token).Owner,"仅负责人可导出审阅稿",403);
        return Task.FromResult(WorkbenchOffice.Create(book));
    });
    public Task<string> Export(string? token) => Locked(async () =>
    {
        Need(Member(token).Owner, "仅负责人可导出", 403); var name = "review-" + Guid.NewGuid().ToString("N");
        await native.Export(book,Path.Combine(root,"exports",name)); return name;
    });
    public Task<object> Apply(string? token, Edit edit) => Locked<object>(async () =>
    {
        var actor = Member(token); Need(edit.Id != Guid.Empty && edit.Project == book.Project, "操作缺少身份或属于其他项目");
        var receiptKey = $"{actor.Id:N}:{edit.Id:N}"; string digest = Hash(JsonSerializer.SerializeToUtf8Bytes(edit,Json));
        if (book.Receipts.TryGetValue(receiptKey,out var previous))
        { Need(previous.Digest == digest, "同一操作编号不能提交不同内容", 409); return new { outcome=previous.Outcome, replay=true, book.Revision }; }
        var next = Copy(); var now = DateTimeOffset.UtcNow; string outcome="saved";
        var target = next.Blocks.Find(b=>b.Id==edit.Target); var thread = next.Threads.Find(t=>t.Id==edit.Target);
        void Owner() => Need(actor.Owner,"只有负责人可以管理席位和版本。",403);
        void Conflict(string current, string submitted)
        { next.Conflicts.Add(new(edit.Id,actor.Id,actor.Name,now,edit.Target,edit.Action,edit.Base,submitted,current)); outcome="conflict"; }
        if (next.Workbench?.PrivateTier=="basic" && !next.Team && edit.Action is ("comment" or "reply" or "status")) throw new Problem(403,"批注需要专业版或团队版。");
        var note = () => new Note(actor.Id,actor.Name,now,Short(edit.Text));
        switch (edit.Action)
        {
            case "upgrade": Owner(); next.Team=true; break;
            case "wbTier":
                Owner(); Need(next.Workbench!=null,"此实例未开启工作台",404); Need(edit.Text is "basic" or "pro","版本无效");
                Need(!next.Team,"团队项目不能降级；请导出新的私人副本。",409); next.Workbench!.PrivateTier=edit.Text; break;
            case "wbPageAdd": case "wbPageSet": case "wbPageDelete": case "wbPageRestore": case "wbPageMove": case "wbShotSet": case "wbShotAdd": case "wbShotArchive": case "wbShotMove":
                (next.Workbench??throw new Problem(404,"此实例未开启工作台")).Apply(next,edit,Conflict); break;
            case "invite":
                Owner(); Need(next.Team,"请先升级为团队项目"); Need(Used(next)<MemberSeats,"席位已满；请先移除成员或撤销待用邀请。",409);
                var label=Short(edit.Text,32); Need(!next.People.Any(p=>p.Active&&p.Name==label)&&!next.Invitations.Any(i=>Pending(i)&&i.Label==label),"署名已占用",409);
                next.Invitations.Add(new(edit.Id,label,now.AddDays(7))); break;
            case "cancelInvite":
                Owner(); int invitation=next.Invitations.FindIndex(i=>i.Id==edit.Target&&Pending(i)); Need(invitation>=0,"邀请已失效",409);
                next.Invitations[invitation]=next.Invitations[invitation] with { Cancelled=true }; break;
            case "removeMember":
                Owner(); var member=next.People.Find(p=>p.Id==edit.Target && !p.Owner && p.Active)??throw new Problem(409,"成员已移除或不能移除负责人"); member.Active=false; break;
            case "image": Need(!next.Blocks.Any(b=>b.Id==edit.Id),"参考图身份已存在",409); next.Blocks.Add(await native.Import(next,edit)); break;
            case "text": case "delete":
                Need(target!=null,"位置不存在",404);
                var submitted=edit.Action=="delete"?"移除原位置":Short(edit.Text,12000);
                Need(!target!.ReadOnly,"富文本原稿保留格式，仅开放批注");
                if (target.Version!=edit.Base || target.Removed) Conflict(target.Removed?"原位置已移除":target.Text,submitted);
                else if (edit.Action=="delete") { target.Removed=true; target.Version++; if (next.Order.Remove(target.Id)) next.OrderVersion++; foreach(var item in next.Threads.Where(t=>t.Anchor.Target==target.Id)) item.Anchor.Detached=true; }
                else {Track(next,target,submitted);if(target.Kind=="shot" && next.Workbench?.ShotDetails.Find(s=>s.Id==target.Id) is {} detail) detail.Version++;} break;
            case "move":
                Need(edit.After!=edit.Target,"不能移到自身之后");
                if(next.OrderVersion!=edit.Base || !next.Order.Contains(edit.Target) || !next.Order.Contains(edit.After)) Conflict(string.Join(",",next.Order),$"{edit.Target} → {edit.After}");
                else {next.Order.Remove(edit.Target);next.Order.Insert(next.Order.IndexOf(edit.After)+1,edit.Target);next.OrderVersion++;} break;
            case "comment":
                var anchor=edit.Anchor??throw new Problem(422,"请选择批注位置"); if (next.Workbench is {} workspace && anchor.PageId is not null)
                { workspace.PrepareAnchor(next,anchor,edit.Base);next.Threads.Add(new(){Id=edit.Id, Anchor=anchor, Notes=[note()]});break; }
                Need(anchor.PageId==null && anchor.ObjectId==null,"批注位置缺少工作台页面");
                var block=next.Blocks.Find(b=>b.Id==anchor.Target)??throw new Problem(404,"位置不存在");
                anchor.Detached=block.Removed;
                if(block.Kind=="text") { Need(anchor.Quote is { Length: > 0 and <= 4000 } && anchor.Start>=0,"请选中需要批注的文字"); anchor.Detached |= block.Version!=edit.Base || anchor.Start>block.Text.Length-anchor.Quote.Length || !block.Text.AsSpan(anchor.Start,anchor.Quote.Length).SequenceEqual(anchor.Quote); anchor.Rect=null; }
                else if(block.Kind=="image") { var rect=anchor.Rect; Need(rect is { Length:4 } && rect.All(double.IsFinite) && rect[0]>=0 && rect[1]>=0 && rect[2]>0 && rect[3]>0 && rect[0]+rect[2]<=1.000001 && rect[1]+rect[3]<=1.000001 && block.Hash==anchor.Hash,"图片或框选区域无效"); anchor.Quote=""; }
                else {anchor.Quote="";anchor.Rect=null;}
                next.Threads.Add(new(){Id=edit.Id, Anchor=anchor, Notes=[note()]});break;
            case "reply": Need(thread!=null,"讨论不存在",404); thread!.Notes.Add(note()); break;
            case "status": Need(thread!=null,"讨论不存在",404); if(thread!.StatusVersion!=edit.Base) Conflict(thread.Resolved?"已解决":"待处理",edit.Flag?"已解决":"待处理"); else { thread.Resolved=edit.Flag;thread.StatusVersion++; } break;
            case "ack":
                var index=next.Conflicts.FindIndex(c=>c.Id==edit.Target&&!c.Closed); Need(index>=0,"冲突已处理",409); var clash=next.Conflicts[index];
                Need(actor.Owner||clash.Author==actor.Id,"仅本人或负责人可处理冲突",403);
                if(edit.Flag) {var current=next.Blocks.Find(b=>b.Id==clash.Target&&!b.Removed);Need(clash.Action=="text"&&current!=null&&current.Version==edit.Base,"内容再次变化，请重新核对",409); Track(next,current!,Short(edit.Text,12000));if(current!.Kind=="shot"&&next.Workbench?.ShotDetails.Find(s=>s.Id==current.Id) is {} sd)sd.Version++;}
                next.Conflicts[index]=clash with { Closed=true }; break;
            default: throw new Problem(422,"未知的策划操作");
        }
        next.Receipts.Add(receiptKey,new(digest,outcome));next.Revision++;Commit(next);return new { outcome, replay=false,next.Revision };
    });
}

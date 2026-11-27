using System.Buffers.Binary;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Projects;
using static PlanningReview.Rules;

namespace PlanningReview;

public sealed class NativePlan(string root)
{
    public static void StageCopy(string source, string destination, Guid project)
    {
        Need(!Directory.Exists(destination),"导入目标必须是全新的隔离目录");
        source=Path.GetFullPath(source);var planningRoot=Path.Combine(source,"ProjectPlanning");var plan=Path.Combine(planningRoot,$"{project:N}.planning.json");
        PlanningImageStorage.RejectLinks(source,plan);Need(File.Exists(plan),"未找到指定旧策划副本");
        Need(!File.Exists(plan+".draft"),"此旧副本还有未恢复的草稿；请先在单机副本中恢复或确认，再导入。");
        var stage=destination+".import-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
        void Copy(string from,string relative){PlanningImageStorage.RejectLinks(source,from);var into=Path.Combine(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(into)!);if(!File.Exists(into))File.Copy(from,into);}
        Copy(plan,Path.Combine("ProjectPlanning",Path.GetFileName(plan)));
        var raw=JsonSerializer.Deserialize<PlanningProjectState>(File.ReadAllBytes(Path.Combine(stage,"ProjectPlanning",Path.GetFileName(plan))))??throw new InvalidDataException("旧策划为空");
        Need(raw.ProjectId==project,"旧策划身份不匹配");var doc=raw.Document?.EnsureIdentity(project)??throw new InvalidDataException("无策划正文");
        foreach(var item in PlanningReferenceLinks.Copies(doc)){item.Validate();Need(item.ProjectId==project && item.DocumentId==doc.Package!.DocumentId,"副本不属于该项目/文档");Copy(Path.Combine(planningRoot,item.RelativePath),Path.Combine("ProjectPlanning",item.RelativePath));}
        foreach(var image in doc.Package!.RichLayout?.Images??[]){var relative=PlanningImageStorage.RelativePath(doc.Package.DocumentId,image.DocumentImageId);Need(image.DocumentId==doc.Package.DocumentId && image.CopyPath.Replace('\\','/').Equals(relative,StringComparison.OrdinalIgnoreCase),"旧正文图片未归入此项目；停止导入");Copy(Path.Combine(planningRoot,relative),Path.Combine("ProjectPlanning",relative));}
        var shotFile=Path.Combine(source,"ProjectShots",$"{project:N}.shots.json");if(File.Exists(shotFile))Copy(shotFile,Path.Combine("ProjectShots",Path.GetFileName(shotFile)));
        Directory.Move(stage,destination);
    }
    private readonly PlanningProjectStore planning = new(Path.Combine(root, "ProjectPlanning"));
    private readonly ProjectShotStore shots = new(Path.Combine(root, "ProjectShots"));
    public async Task<Notebook> Open(Guid project,bool workbench=false,bool teamShare=false)
    {
        var existingNative=planning.Exists(project);
        Need(!teamShare || existingNative,"协作准备缺少已保存的原生策划。");
        if (!existingNative)
        {
            var body = "以秋冬外套为主角，拍出面料的厚度与衣领的层次。\n保留深色织纹，动作自然，背景不抢主体。";
            await planning.SaveAsync(new PlanningProjectState(project, Summary: new("秋冬系列平面型录")) { Document = new() { Title = "秋冬服装型录", Body = body, Location = "北窗摄影棚" } });
            string[] titles = ["开场 · 全身轮廓", "步行 · 正面动态", "细节 · 衣领纹理", "侧身 · 剪裁线条", "半身 · 面料叠穿", "收尾 · 背面轮廓"];
            var now = DateTimeOffset.UtcNow;
            await shots.SaveCatalogAsync(new(project, titles.Select((name, index) => new ProjectShot(Guid.NewGuid(), project, index, name, ProjectShotStatus.NotStarted, null, null, [], now, now)).ToArray()));
        }
        var state = await planning.LoadAsync(project); var doc = state.Document ?? throw new InvalidDataException("缺少策划文档");
        var rich = teamShare ? ReadRich(project, doc) : null;
        var book = new Notebook { Project = project, Document = doc.Package!.DocumentId, Title = doc.Title };
        book.People.Add(new() { Name = "负责人 A", Owner = true });
        book.Blocks.AddRange(doc.Body.Replace("\r\n", "\n").Split('\n').Select(line => new Block { Text = line, Field = "body", ReadOnly = doc.Package.RichTextPackageBase64 is not null }));
        var catalog=await shots.LoadAsync(project);
        if (teamShare) Need(catalog.Shots.Count(s=>!s.IsArchived)<=2000,"活动镜头数量超过团队工作台上限。");
        foreach (var shot in catalog.Shots.Where(s => !s.IsArchived).OrderBy(s => s.Order))
        { book.Blocks.Add(new() { Id = shot.ShotId, Kind = "shot", Text = shot.Name }); book.Order.Add(shot.ShotId); }
        foreach (var copy in PlanningReferenceLinks.Copies(doc).Where(c=>c.Kind=="reference").DistinctBy(c=>c.ReferenceId))
        {
            using var stream = PlanningProjectCopyStorage.OpenVerifiedRead(copy);
            var mime = Path.GetExtension(copy.OriginalPath).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", _ => null };
            if (mime is not null) book.Blocks.Add(new() { Id = copy.ReferenceId, Kind = "image", Text = doc.References.FirstOrDefault(r=>r.Source.ReferenceId==copy.ReferenceId)?.Source.Title ?? catalog.Shots.SelectMany(s=>s.References).FirstOrDefault(r=>r.ReferenceId==copy.ReferenceId)?.Title ?? "分镜参考图", Hash = copy.Sha256, Mime = mime });
        }
        foreach(var image in doc.Package.RichLayout?.Images??[]){PlanningImageStorage.Resolve(image);book.Blocks.Add(new(){Id=image.DocumentImageId,Kind="image",Text=image.Title,Hash=image.CopyHash,Mime="image/png",Field="documentImage",ReadOnly=true});}
        if (teamShare)
        {
            PlanningReferenceLinks.ValidateCatalog(doc,catalog);
            foreach(var revision in doc.Package.CanvasRevisions.Where(r=>!string.IsNullOrEmpty(r.PreviewPngBase64)))
            {
                var bytes=Convert.FromBase64String(revision.PreviewPngBase64!);
                Need(bytes.Length is >=24 and <=24*1024*1024 && bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"固定画布快照格式无效。");
                book.Blocks.Add(new(){Id=revision.RevisionId,Kind="image",Text=revision.EditableSnapshot.Name,Hash=Hash(bytes),Mime="image/png",Field="canvasSnapshot",ReadOnly=true});
            }
            Need(book.Blocks.Select(b=>b.Id).Distinct().Count()==book.Blocks.Count,"策划资料身份有冲突。");
        }
        if (teamShare)
        {
            Need(workbench,"共享策划需要团队工作台。");
            book.Workbench=NativeWorkbenchProjection.Create(book,state,catalog,Workbench.Seed(book,catalog.Shots.Where(s=>!s.IsArchived).Select(NativeShot).ToList()),rich,teamShare:true);
            ImportShareDiscussions(book,doc);
            return book;
        }
        if(doc.Extensions?.TryGetValue("PixelTartReviewV1",out var data)==true)
        {
            var restored=data.Deserialize<Archive>(Json)??throw new InvalidDataException("审稿扩展无效");
            Need(restored.People.Count(p=>p.Owner)==1 && restored.Blocks.Select(b=>b.Id).Distinct().Count()==restored.Blocks.Count,"审稿扩展身份重复或缺失");
            Need(restored.Blocks.Where(b=>!b.Removed && b.Kind!="text").All(b=>book.Blocks.Any(old=>old.Id==b.Id&&old.Kind==b.Kind&&old.Hash==b.Hash)),"审稿扩展与单机材料不匹配");
            Need(restored.Threads.All(t=>restored.Blocks.Any(b=>b.Id==t.Anchor.Target)),"批注锚点缺少历史目标");
            book.Blocks=restored.Blocks;
            book.Threads=restored.Threads;book.Conflicts=restored.Conflicts;book.Revision=restored.Revision;
            book.People=restored.People;foreach(var person in book.People)person.Active=person.Owner;
        }
        if(workbench)
        {
            if(doc.Extensions?.TryGetValue("PixelTartWorkbenchV1",out var snapshot)==true)
            {
                var restored=snapshot.Deserialize<WorkbenchArchive>(Json)??throw new InvalidDataException("工作台扩展无效");
                var wb=restored.State; Need(wb.Pages.Count<=200 && wb.Pages.All(p=>p.Id!=Guid.Empty&&p.Version>0) && wb.Pages.Select(p=>p.Id).Distinct().Count()==wb.Pages.Count,"工作台页面身份重复或无效");
                Need(wb.ShotDetails.Select(d=>d.Id).Distinct().Count()==wb.ShotDetails.Count&&wb.ShotDetails.All(d=>book.Blocks.Any(b=>b.Id==d.Id&&b.Kind=="shot")),"工作台分镜身份不匹配");
                // Validate live page structures against retained assets, including historic removed assets.
                foreach(var page in wb.Pages)WorkSchema.Page(book,page.Kind,page.Data,allowRemovedAssets:true);
                Need(restored.Threads.All(t=>t.Anchor.PageId is {} pageId && wb.Pages.Any(p=>p.Id==pageId)) && !restored.Threads.Select(t=>t.Id).Concat(book.Threads.Select(t=>t.Id)).GroupBy(id=>id).Any(g=>g.Count()>1),"工作台批注身份无效");
                book.Workbench=wb;book.Threads.AddRange(restored.Threads);
            }
            else
            {
                var basic=Workbench.Seed(book,catalog.Shots.Where(s=>!s.IsArchived).Select(NativeShot).ToList());
                book.Workbench=existingNative?NativeWorkbenchProjection.Create(book,state,catalog,basic):basic;
            }
        }
        return book;
    }
    private static void ImportShareDiscussions(Notebook book,PlanningDocument document)
    {
        Archive? review = null;
        WorkbenchArchive? workbench = null;
        if(document.Extensions?.TryGetValue("PixelTartReviewV1",out var reviewJson)==true)
            review=reviewJson.Deserialize<Archive>(Json)??throw new InvalidDataException("审稿历史为空。");
        if(document.Extensions?.TryGetValue("PixelTartWorkbenchV1",out var workbenchJson)==true)
            workbench=workbenchJson.Deserialize<WorkbenchArchive>(Json)??throw new InvalidDataException("工作台历史为空。");
        var threads=(review?.Threads??[]).Concat(workbench?.Threads??[]).ToArray();
        if(threads.Length==0)return;
        Need(threads.All(t=>t.Id!=Guid.Empty&&t.Anchor is not null&&t.Notes is not null&&t.StatusVersion>0) && threads.Select(t=>t.Id).Distinct().Count()==threads.Length,"历史批注身份不完整或重复。");
        var oldBlocks=review?.Blocks??[];
        var oldPages=workbench?.State?.Pages??[];
        Need(oldBlocks.Select(b=>b.Id).Distinct().Count()==oldBlocks.Count && oldPages.Select(p=>p.Id).Distinct().Count()==oldPages.Count,"历史批注锚点身份重复。");
        if(review is { People.Count: >0 } && review.People.Count(p=>p.Owner)==1 && review.People.Select(p=>p.Id).Distinct().Count()==review.People.Count)
        {
            book.People=review.People;
            foreach(var person in book.People)person.Active=person.Owner;
        }
        book.Threads.AddRange(threads);
        NativeShareHistory.Reattach(book,oldBlocks,oldPages);
    }
    private PlanningShareRichSnapshot? ReadRich(Guid project, PlanningDocument document)
    {
        var path = Path.Combine(root,PlanningShareRichSnapshot.FileName);
        if (document.Package!.RichTextPackageBase64 is null)
        {
            Need(!File.Exists(path),"普通正文含未声明的富文本投影。");
            return null;
        }
        Need(File.Exists(path),"分享包缺少由桌面生成的富文本投影。");
        var snapshot=JsonSerializer.Deserialize<PlanningShareRichSnapshot>(File.ReadAllBytes(path),PlanningShareRichSnapshot.Json)??throw new InvalidDataException("富文本投影为空。");
        var plan=File.ReadAllBytes(Path.Combine(planning.StorageRoot,$"{project:N}.planning.json"));
        Need(snapshot.ProjectId==project && snapshot.DocumentId==document.Package.DocumentId && string.Equals(snapshot.PlanSha256,Hash(plan),StringComparison.OrdinalIgnoreCase),"富文本投影不属于此策划版本。");
        Need(snapshot.Blocks is not null && snapshot.Losses is not null,"富文本投影结构缺失。");
        return snapshot;
    }
    private sealed record WorkbenchArchive(Workbench State,List<Discussion> Threads);
    static ShotDetail NativeShot(ProjectShot shot)=>new(){Id=shot.ShotId,Requirements=shot.Action,Fields=new(){["scene"]=shot.Scene??"",["shotSize"]=shot.ShotSize,["cameraAndLens"]=shot.CameraAndLens,["props"]=shot.Props,["lighting"]=shot.Lighting,["remarks"]=shot.Remarks,["estimatedMinutes"]=shot.EstimatedMinutes.ToString()}};
    private sealed record Archive(List<Block> Blocks,List<Discussion> Threads,List<Clash> Conflicts,long Revision,List<Person> People);
    public async Task<Block> Import(Notebook book, Edit edit)
    {
        Need(edit.Image is { Length: > 0 and <= 7000000 }, "图片过大或为空");
        byte[] bytes; try { bytes = Convert.FromBase64String(edit.Image!); } catch (FormatException) { throw new Problem(422, "图片编码无效"); }
        Need(bytes.Length is > 24 and <= ImageBytes, "试点每张图片上限为 5 MiB");
        bool png = bytes.AsSpan(0, 8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
        bool jpeg = bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
        Need(png || jpeg, "试点支持 JPEG 或 PNG 参考图");
        int width = 0, height = 0;
        if (png) { width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)); height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)); }
        else for (var at = 2; at + 9 < bytes.Length && bytes[at] == 255;)
        {
            if (bytes[at+1] == 255) { at++; continue; }
            int tag = bytes[at+1], size = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at+2));
            if (size < 2 || at + 2 + size > bytes.Length || tag == 218) break;
            if (tag is 192 or 193 or 194) { height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at+5)); width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at+7)); break; }
            at += size + 2;
        }
        Need(width > 0 && height > 0 && (long)width * height <= 24000000, "图片尺寸无效或超过 2400 万像素");
        string title = Short(edit.Text, 100), digest = Hash(bytes);
        string folder = Path.Combine(root, "uploads", edit.Id.ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, digest + (png ? ".png" : ".jpg"));
        if (!File.Exists(path)) Atomic(path, bytes);
        var source = new ProjectShotReference(edit.Id, ShotReferenceKind.Styling, ExternalReference: path, Title: title);
        await planning.ImportProjectCopiesAsync(book.Project, [new(source)], []);
        var saved = (await planning.LoadAsync(book.Project)).Document!.References.Single(r => r.Source.ReferenceId == edit.Id).ProjectCopy!;
        Need(saved.Sha256 == digest, "项目副本摘要不符");
        return new() { Id = edit.Id, Kind = "image", Text = title, Mime = png ? "image/png" : "image/jpeg", Hash = digest };
    }
    public async Task<Stream> Read(Notebook book, Block block)
    {
        var document=(await planning.LoadAsync(book.Project)).Document!;
        if(block.Field=="canvasSnapshot") {var revision=document.Package!.CanvasRevisions.Single(r=>r.RevisionId==block.Id);var bytes=Convert.FromBase64String(revision.PreviewPngBase64!);Need(Hash(bytes)==block.Hash,"固定画布快照摘要不匹配",409);return new MemoryStream(bytes,writable:false);}
        if(block.Field=="documentImage") { var image=document.Package!.RichLayout!.Images.Single(i=>i.DocumentImageId==block.Id);Need(image.CopyHash==block.Hash,"正文图片摘要不匹配",409);return File.OpenRead(PlanningImageStorage.Resolve(image)); }
        var copy = PlanningReferenceLinks.Copies(document).First(r=>r.ReferenceId==block.Id && r.Kind=="reference");
        Need(copy.DocumentId == book.Document && copy.Sha256 == block.Hash, "项目副本身份或摘要不符", 409);
        return PlanningProjectCopyStorage.OpenVerifiedRead(copy);
    }
    public async Task Export(Notebook book, string destination)
    {
        Need(!Directory.Exists(destination), "恢复目录已存在"); Directory.CreateDirectory(destination);
        string planningRoot = Path.Combine(destination, "ProjectPlanning");
        foreach (var file in Directory.EnumerateFiles(planning.StorageRoot, "*", SearchOption.AllDirectories))
        {
            PlanningImageStorage.RejectLinks(planning.StorageRoot, file);
            var path = Path.Combine(planningRoot, Path.GetRelativePath(planning.StorageRoot, file)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(file, path);
        }
        var output = new PlanningProjectStore(planningRoot); var state = await output.LoadAsync(book.Project); var doc = state.Document!;
        var text = string.Join("\n", book.Blocks.Where(b => b.Kind == "text" && !b.Removed).Select(b => b.Text));
        if(doc.Package!.RichTextPackageBase64 is null && book.Workbench?.Pages.FirstOrDefault(p=>p.Kind=="doc" && p.Section=="brief" && !p.Archived) is {} brief)
            text=string.Join("\n",brief.Data.GetProperty("paragraphs").EnumerateArray().Select(p=>p.GetProperty("text").GetString()));
        var liveRefs = book.Blocks.Where(b => b.Kind == "image" && b.Removed).Select(b => b.Id).ToHashSet();
        var extensions = doc.Extensions is null ? new() : new Dictionary<string, JsonElement>(doc.Extensions);
        extensions["PixelTartReviewV1"] = JsonSerializer.SerializeToElement(new Archive(book.Blocks,book.Threads.Where(t=>t.Anchor.PageId==null).ToList(),book.Conflicts,book.Revision,book.People), Json);
        if(book.Workbench is {} wb)extensions["PixelTartWorkbenchV1"]=JsonSerializer.SerializeToElement(new WorkbenchArchive(wb,book.Threads.Where(t=>t.Anchor.PageId!=null).ToList()),Json);
        await output.SaveAsync(state with { Document = doc with { Body = text, Package = doc.Package! with { Body = text }, Extensions = extensions, References = doc.References.Where(r => !liveRefs.Contains(r.Source.ReferenceId)).ToArray() } });
        var original = await shots.LoadAsync(book.Project); var active = book.Order.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var merged=original.Shots.ToList();var now=DateTimeOffset.UtcNow;
        if(book.Workbench!=null)foreach(var b in book.Blocks.Where(b=>b.Kind=="shot" && merged.All(s=>s.ShotId!=b.Id)))
            merged.Add(new ProjectShot(b.Id,book.Project,merged.Count,b.Text,ProjectShotStatus.NotStarted,null,null,[],now,now));
        ProjectShot ConvertShot(ProjectShot s)
        {
            if(!active.TryGetValue(s.ShotId,out var index))return s with { IsArchived=true };
            var next=s with {Name=book.Blocks.Single(b=>b.Id==s.ShotId).Text,Order=index};
            if(book.Workbench?.ShotDetails.Find(d=>d.Id==s.ShotId) is {} detail)
            {var f=detail.Fields;return next with {PlanningFieldsVersion=1,Action=detail.Requirements,Scene=f.TryGetValue("scene",out var scene)?scene:s.Scene,ShotSize=f.GetValueOrDefault("shotSize",s.ShotSize),CameraAndLens=f.GetValueOrDefault("cameraAndLens",s.CameraAndLens),Props=f.GetValueOrDefault("props",s.Props),Lighting=f.GetValueOrDefault("lighting",s.Lighting),Remarks=f.GetValueOrDefault("remarks",s.Remarks),EstimatedMinutes=f.TryGetValue("estimatedMinutes",out var ms)&&int.TryParse(ms,out var m)&&m is >=0 and <=1440?m:s.EstimatedMinutes};}
            return next;
        }
        await new ProjectShotStore(Path.Combine(destination,"ProjectShots")).SaveCatalogAsync(original with {Shots=merged.Select(ConvertShot).ToArray()});
        var reopened = (await output.LoadAsync(book.Project)).Document!;
        foreach (var item in reopened.References.Where(r => r.ProjectCopy is not null)) using (PlanningProjectCopyStorage.OpenVerifiedRead(item.ProjectCopy!)) { }
    }
    public static void Atomic(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); } File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

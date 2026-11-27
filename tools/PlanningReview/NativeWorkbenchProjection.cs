using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RAWSelectionAssistant.Core.Services.Projects;
using static PlanningReview.Rules;
namespace PlanningReview;

// One-way views on an already isolated native document. Original project structures remain intact.
internal static class NativeWorkbenchProjection
{
    public static Workbench Create(Notebook book,PlanningProjectState state,ProjectShotCatalog catalog,Workbench wb,PlanningShareRichSnapshot? rich=null,bool teamShare=false)
    {
        var doc=state.Document!;var resources=doc.Resources.Items;var summary=state.Summary??new();
        var notices = teamShare && doc.Extensions?.TryGetValue(PlanningShareSectionNotice.Extension, out var savedNotices)==true
            ? System.Text.Json.JsonSerializer.Deserialize<List<PlanningShareSectionNotice>>(savedNotices,PlanningShareRichSnapshot.Json) ?? [] : [];
        Need(notices.Count<=5000&&notices.All(n=>n.Section.Length<=30&&n.Item.Length<=300&&n.Message.Length<=1000),"页面资料说明无效。");
        string NoticeSection(string key)=>key switch {"overview"=>"项目概览","brief"=>"文字","shots"=>"镜头清单与分镜","reference"=>"参考图","mood"=>"情绪板","lighting"=>"灯位图","props"=>"服化道与道具","gear"=>"设备","crew"=>"人员","venue"=>"场地","schedule"=>"档期","timeline"=>"当天时间表","budget"=>"预算","files"=>"文件","document"=>"文档内容",_=>""};
        Guid Key(string label)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(book.Document.ToString("N")+":"+label)).AsSpan(0,16));
        string Value(object? value)=>value switch {null=>"",DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),IFormattable v=>v.ToString(null,CultureInfo.InvariantCulture)??"",_=>value.ToString()??""};
        object Row(Guid id,params (string k,object? v)[] cells)=>new {id,cells=cells.ToDictionary(x=>x.k,x=>Value(x.v))};
        object R(PlanningResource r,params (string,object?)[] cells)=>Row(r.Id,[("name",r.Name),("notes",r.Notes),("shots",string.Join(", ",r.ShotIds)),("archive",r.Archived?"已归档":"活动"),..cells]);
        static string Leaf(string? s)=>Path.GetFileName((s??"").Replace('\\','/'));
        static string SafeSource(string s)=>Path.IsPathFullyQualified(s)||s.Contains('\\')||s.Contains('/')?Leaf(s):s;
        void Add(string key,string title,string kind,object data){var p=new WorkPage{Id=Key("page:"+key),Kind=kind,Title=title,Section=key,Data=Workbench.Enc(data),Notices=notices.Where(n=>n.Section==NoticeSection(key)).ToList()};WorkSchema.Page(book,kind,p.Data);wb.Pages.Add(p);}
        void Table(string key,string title,string names,IEnumerable<object> rows){var cols=names.Split(' ').Select(c=>c.Split('=')).Select(c=>new {key=c[0],label=c[1]}).ToArray();Add(key,title,"table",new {columns=cols,rows=rows.ToArray()});}
        object Para(string key,string text)=>new {id=Key(key),type="p",text};
        object RichPara(PlanningShareRichBlock block,string key)
        {
            var entry=System.Text.Json.JsonSerializer.SerializeToElement(block,PlanningShareRichSnapshot.Json).EnumerateObject().ToDictionary(p=>p.Name,p=>(object)p.Value);
            entry["id"]=Key(key);
            if(block.NativeTable is { } table)entry["nativeTable"]=new {table.RepeatHeader,rows=table.Rows.Select((r,ri)=>new {cells=r.Cells.Select((c,ci)=>new {c.ColSpan,c.RowSpan,paragraphs=c.Paragraphs.Select((p,pi)=>RichPara(p,$"{key}:r{ri}:c{ci}:p{pi}"))})})};
            return entry;
        }
        static IEnumerable<Guid> RichAssets(IEnumerable<PlanningShareRichBlock> blocks)=>blocks.SelectMany(b=>b.NativeImage is {} image ? [image.AssetId] : b.NativeTable is {} table ? RichAssets(table.Rows.SelectMany(r=>r.Cells).SelectMany(c=>c.Paragraphs)) : []);
        wb.Pages.Clear();var owned=book.Blocks.Where(b=>!b.Removed&&b.Kind=="image").ToDictionary(b=>b.Id);
        var files=doc.Attachments;var activeShots=catalog.Shots.Where(s=>!s.IsArchived).ToDictionary(s=>s.ShotId);
        var nativeImages=doc.Package!.RichLayout?.Images??[];
        Guid? ReferenceAsset(ProjectShotReference source)
        {
            if(owned.ContainsKey(source.ReferenceId))return source.ReferenceId;
            if(source.LibraryId is not Guid library||source.AssetId is not Guid asset)return null;
            return nativeImages.Where(i=>i.Source is { } src&&src.LibraryId==library&&src.AssetId==asset&&owned.ContainsKey(i.DocumentImageId))
                .OrderBy(i=>i.Order).Select(i=>(Guid?)i.DocumentImageId).FirstOrDefault();
        }
        IEnumerable<Guid> DocumentAssets(Func<PlanningDocumentReference,bool> test)=>doc.References.Where(test).Select(r=>ReferenceAsset(r.Source)).OfType<Guid>();
        foreach(var d in wb.ShotDetails){var s=activeShots[d.Id]; d.Fields["status"]=s.Status switch{ProjectShotStatus.Completed=>"已拍",ProjectShotStatus.InProgress=>"准备中",ProjectShotStatus.Skipped=>"跳过",_=>"待拍"};
            var selected=s.References.Select(r=>r.ReferenceId).Concat(doc.ShotExternalLinks.Where(l=>l.ShotIds.Contains(s.ShotId)).Select(l=>l.ReferenceId)).Distinct().ToArray();
            d.AssetId=s.References.Select(ReferenceAsset).OfType<Guid>().Concat(selected.Where(owned.ContainsKey)).Concat(nativeImages.Where(i=>i.ShotId==s.ShotId).OrderBy(i=>i.Order).Select(i=>i.DocumentImageId)).Where(owned.ContainsKey).Select(id=>(Guid?)id).FirstOrDefault();
            if(selected.Length>0) d.Fields["references"]=string.Join(", ",selected.Select(id=>owned.TryGetValue(id,out var refImage)?refImage.Text:id.ToString("D")));
            foreach(var col in doc.ShotColumns.Where(c=>!c.Archived))if(s.CustomValues.TryGetValue("custom:"+col.Id.ToString("N"),out var val)&&!d.Fields.ContainsKey(col.Name))d.Fields[col.Name]=val;
            if(teamShare)WorkSchema.Shot(book,d.Id,Workbench.Enc(new {requirements=d.Requirements,assetId=d.AssetId,fields=d.Fields}),d.Version);
        }
        Table("overview","项目概览","name=项目 subtitle=副标题 status=状态 aim=拍摄目标 keywords=关键词 requirements=客户需求 must=必拍内容 purpose=输出用途 notes=备注 location=地点 people=人员 date=拍摄日期 booking=关联档期",
            [Row(Key("overview"),("name",doc.Title),("subtitle",doc.Subtitle),("status",doc.Status),("aim",summary.ShootGoal),("keywords",string.Join("、",summary.Keywords??[])),("requirements",summary.ClientRequirements),("must",summary.MustCapture),("purpose",summary.OutputPurpose),("notes",summary.Notes),("location",doc.Location),("people",doc.People),("date",doc.ShootDate?.ToString("yyyy-MM-dd")),("booking",state.BookingId))]);
        var usedInline=rich is null ? new HashSet<Guid>() : RichAssets(rich.Blocks).ToHashSet();
        if(rich is not null)Need(usedInline.All(id=>owned.TryGetValue(id,out var b)&&b.Field is "documentImage" or "canvasSnapshot"),"富文本引用了不属于本文档正文的图片身份。");
        var hero=DocumentAssets(p=>p.IsHero).Concat(!teamShare?owned.Values.Where(b=>b.Field=="documentImage").Select(b=>b.Id):[]).Distinct().ToArray();
        object[] plain=doc.Body.Replace("\r\n","\n").Split('\n').Select((line,i)=>Para("brief:"+i,line)).ToArray();
        var paragraphs=rich?.Blocks.Select((p,i)=>RichPara(p,"brief:"+i)).ToArray()??plain;
        Add("brief","策划正文","doc",new {paragraphs,assetIds=hero});
        Add("shots","镜头清单与分镜","shots",new {});
        void Board(string key,string title,IEnumerable<Guid> ids){var objs=ids.Distinct().Select((id,index)=>new {id=Key(key+":"+id),kind="image",assetId=id,x=30+(index%5)*190,y=24+(index/5)*140,w=175,h=125}).ToArray();Add(key,title,"canvas",new {width=1000,height=Math.Clamp((objs.Length+4)/5*140+40,670,8000),objects=objs});}
        var references=owned.Values.Where(b=>b.Field is not ("documentImage" or "canvasSnapshot")).Select(b=>b.Id).Concat(nativeImages.Where(i=>i.Placement=="参考图"&&owned.ContainsKey(i.DocumentImageId)).Select(i=>i.DocumentImageId));
        var mood=DocumentAssets(r=>r.IsMoodboard)
            .Concat(nativeImages.Where(i=>i.Placement=="情绪板"&&owned.ContainsKey(i.DocumentImageId)).Select(i=>i.DocumentImageId))
            .Concat((state.VisualLinks??[]).Where(l=>l.Kind==PlanningVisualLinkKind.FreeCanvas&&l.Role!=PlanningCanvasRole.Lighting).SelectMany(l=>doc.Package.CanvasRevisions.Where(r=>r.CanvasId==l.TargetId&&owned.ContainsKey(r.RevisionId)).Select(r=>r.RevisionId)));
        Board("reference","参考图",teamShare?references:owned.Keys);Board("mood","情绪板",mood);
        var lights=resources.OfType<PlanningLight>().ToList();var objects=new List<object>();foreach(var l in lights.Where(x=>!x.Archived)){double x=l.X*900,y=l.Y*600;objects.Add(new {id=l.Id,kind="arrow",x,y,w=80,h=10,rotate=l.DirectionDegrees%360,fill="#4E6657"});objects.Add(new {id=Key("light-label:"+l.Id),kind="text",x,y=y+15,w=145,h=45,text=l.Name+" · "+Value(l.PowerPercent)+"%"});        }
        var lightImages=DocumentAssets(r=>r.Category=="灯光")
            .Concat(nativeImages.Where(i=>i.Placement is "灯位图" or "灯光图" && owned.ContainsKey(i.DocumentImageId)).Select(i=>i.DocumentImageId))
                        .Concat(lights.Where(l=>!l.Archived&&l.CanvasRevisionId is Guid id&&owned.ContainsKey(id)).Select(l=>l.CanvasRevisionId!.Value))
            .Concat((state.VisualLinks??[]).Where(l=>l.Kind==PlanningVisualLinkKind.FreeCanvas&&l.Role==PlanningCanvasRole.Lighting).SelectMany(l=>doc.Package.CanvasRevisions.Where(r=>r.CanvasId==l.TargetId&&owned.ContainsKey(r.RevisionId)).Select(r=>r.RevisionId))).Distinct().ToArray();
        var offset=0;foreach(var id in lightImages)objects.Add(new {id=Key("lighting:"+id),kind="image",assetId=id,x=30+offset%3*320,y=690+offset++/3*225,w=300,h=205});
        Add("lighting","灯位图","canvas",new {width=1000,height=Math.Min(8000,670+(lightImages.Length+2)/3*225),objects});
        const string shared=" name=名称 notes=备注 shots=关联镜头ID archive=归档";
        Table("props","服化道与道具","category=分类 qty=数量 preparation=准备状态 source=来源"+shared,resources.OfType<PlanningProp>().Select(r=>R(r,("category",r.Category),("qty",r.Quantity),("preparation",r.Preparation),("source",SafeSource(r.Source)))));
        Table("gear","设备","model=型号 qty=数量 catalog=来源目录 source=来源ID"+shared,resources.OfType<PlanningGearItem>().Select(r=>R(r,("model",r.Model),("qty",r.Quantity),("catalog",SafeSource(r.SourceCatalog)),("source",SafeSource(r.SourceId)))));
        Table("crew","人员","role=职责 arrival=到场 contact=联系方式"+shared,resources.OfType<PlanningCrewMember>().Select(r=>R(r,("role",r.Role),("arrival",r.ArrivalLocal),("contact",r.Contact))));
        Table("venue","场地","address=地址 hours=可用时间 permit=许可 contact=联系方式 date=日期 zone=时区 sunset=日落 tide=潮汐 source=观测来源"+shared,resources.OfType<PlanningVenue>().Select(r=>R(r,("address",r.Address),("hours",r.AvailableHours),("permit",r.Permit),("contact",r.Contact),("date",r.Date),("zone",r.TimeZoneId),("sunset",r.SunsetLocal),("tide",r.TideLocal),("source",r.ObservationSource))));
        Table("schedule","档期","name=项目 date=拍摄日期 place=地点 booking=关联档期ID",doc.ShootDate==null&&state.BookingId==null?[]:[Row(Key("schedule"),("name",doc.Title),("date",doc.ShootDate?.ToString("yyyy-MM-dd")),("place",doc.Location),("booking",state.BookingId))]);
        Table("timeline","当天时间表","date=日期 start=开始 end=结束 zone=时区 place=地点 owner=负责人"+shared,resources.OfType<PlanningScheduleSlot>().Select(r=>R(r,("date",r.Date),("start",r.StartLocal),("end",r.EndLocal),("zone",r.TimeZoneId),("place",r.Location),("owner",r.Owner))));
        Table("budget","预算","amount=预算金额 currency=币种"+shared,resources.OfType<PlanningBudgetLine>().Select(r=>R(r,("amount",r.Planned),("currency",r.Currency))));
        Table("files","文件","name=名称 source=来源 status=状态",files.Select(f=>Row(f.Id,("name",Leaf(f.Name)),("source","原生项目附件"),("status",teamShare?"仅同步附件名称":f.ProjectCopy==null?"外链元数据":"受管副本元数据"))));
        var custom=doc.CustomBody.Replace("\r\n","\n").Split('\n').Select((text,i)=>Para("custom:"+i,text)).ToList();
        static double Aspect(PlanningCanvasRevision r)=>Math.Max(1,r.EditableSnapshot.PixelHeight)*600d/Math.Max(1,r.EditableSnapshot.PixelWidth);
        if(teamShare)foreach(var r in doc.Package.CanvasRevisions.Where(r=>owned.ContainsKey(r.RevisionId)&&!usedInline.Contains(r.RevisionId)))
            custom.Add(RichPara(new("caption",NativeImage:new(r.RevisionId,$"{r.EditableSnapshot.Name} · {r.CapturedAt:yyyy-MM-dd HH:mm zzz} · 固定快照",600,Math.Clamp(Aspect(r),1,12000))),"revision:"+r.RevisionId));
        var remainder=teamShare?nativeImages.Where(i=>owned.ContainsKey(i.DocumentImageId)&&!usedInline.Contains(i.DocumentImageId)&&PlanningReadableSummaryBuilder.PlacementSection(i.Placement) is 0 or 1 or 6 or 14).OrderBy(i=>i.Order).Select(i=>i.DocumentImageId).ToArray():[];
        Add("document","文档内容","doc",new {paragraphs=custom,assetIds=remainder});
        if(!teamShare&&lights.Count>0)Table("lights","灯具清单","x=横向位置 y=纵向位置 angle=方向 power=功率 modifier=修饰器 canvas=快照ID"+shared,lights.Select(r=>R(r,("x",r.X),("y",r.Y),("angle",r.DirectionDegrees),("power",r.PowerPercent),("modifier",r.Modifier),("canvas",r.CanvasRevisionId))));
        return wb;
    }
}

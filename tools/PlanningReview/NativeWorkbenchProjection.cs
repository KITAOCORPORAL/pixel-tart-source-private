using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RAWSelectionAssistant.Core.Services.Projects;
using static PlanningReview.Rules;
namespace PlanningReview;

// One-way views on an already isolated native document. Original project structures remain intact.
internal static class NativeWorkbenchProjection
{
    public static Workbench Create(Notebook book,PlanningProjectState state,ProjectShotCatalog catalog,Workbench wb)
    {
        var doc=state.Document!;var resources=doc.Resources.Items;var summary=state.Summary??new();
        Guid Key(string label)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(book.Document.ToString("N")+":"+label)).AsSpan(0,16));
        string Value(object? value)=>value switch {null=>"",DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),IFormattable v=>v.ToString(null,CultureInfo.InvariantCulture)??"",_=>value.ToString()??""};
        object Row(Guid id,params (string k,object? v)[] cells)=>new {id,cells=cells.ToDictionary(x=>x.k,x=>Value(x.v))};
        object R(PlanningResource r,params (string,object?)[] cells)=>Row(r.Id,[("name",r.Name),("notes",r.Notes),("shots",string.Join(", ",r.ShotIds)),("archive",r.Archived?"已归档":"活动"),..cells]);
        static string Leaf(string? s)=>Path.GetFileName((s??"").Replace('\\','/'));
        static string SafeSource(string s)=>Path.IsPathFullyQualified(s)||s.Contains('\\')||s.Contains('/')?Leaf(s):s;
        void Add(string key,string title,string kind,object data){var p=new WorkPage{Id=Key("page:"+key),Kind=kind,Title=title,Section=key,Data=Workbench.Enc(data)};WorkSchema.Page(book,kind,p.Data);wb.Pages.Add(p);}
        void Table(string key,string title,string names,IEnumerable<object> rows){var cols=names.Split(' ').Select(c=>c.Split('=')).Select(c=>new {key=c[0],label=c[1]}).ToArray();Add(key,title,"table",new {columns=cols,rows=rows.ToArray()});}
        object Para(string key,string text)=>new {id=Key(key),type="p",text};
        var oldBrief=wb.Pages.Single(p=>p.Section=="brief");wb.Pages.Clear();var owned=book.Blocks.Where(b=>!b.Removed&&b.Kind=="image").ToDictionary(b=>b.Id);
        var files=doc.Attachments;var pics=doc.References.ToDictionary(r=>r.Source.ReferenceId,r=>r);var activeShots=catalog.Shots.Where(s=>!s.IsArchived).ToDictionary(s=>s.ShotId);
        foreach(var d in wb.ShotDetails){var s=activeShots[d.Id]; d.Fields["status"]=s.Status switch{ProjectShotStatus.Completed=>"已完成",ProjectShotStatus.InProgress=>"进行中",ProjectShotStatus.Skipped=>"跳过",_=>"待拍"};
            var selected=s.References.Select(r=>r.ReferenceId).Concat(doc.ShotExternalLinks.Where(l=>l.ShotIds.Contains(s.ShotId)).Select(l=>l.ReferenceId)).Distinct().ToArray();
            d.AssetId=selected.Where(owned.ContainsKey).Select(id=>(Guid?)id).FirstOrDefault();if(selected.Length>0)d.Fields["references"]=string.Join(", ",selected);}
        Table("overview","项目概览","name=项目 subtitle=副标题 status=状态 aim=拍摄目标 keywords=关键词 requirements=客户需求 must=必拍内容 purpose=输出用途 notes=备注 location=地点 people=人员 date=拍摄日期 booking=关联档期",
            [Row(Key("overview"),("name",doc.Title),("subtitle",doc.Subtitle),("status",doc.Status),("aim",summary.ShootGoal),("keywords",string.Join("、",summary.Keywords??[])),("requirements",summary.ClientRequirements),("must",summary.MustCapture),("purpose",summary.OutputPurpose),("notes",summary.Notes),("location",doc.Location),("people",doc.People),("date",doc.ShootDate?.ToString("yyyy-MM-dd")),("booking",state.BookingId))]);
        var paragraphs=oldBrief.Data.GetProperty("paragraphs");Add("brief","策划正文","doc",new {paragraphs,assetIds=owned.Values.Where(b=>b.Field=="documentImage"||pics.TryGetValue(b.Id,out var p)&&p.IsHero).Select(b=>b.Id).ToArray()});
        Add("shots","镜头清单与分镜","shots",new {});
        void Board(string key,string title,IEnumerable<Guid> ids){var objs=ids.Distinct().Select((id,index)=>new {id=Key(key+":"+id),kind="image",assetId=id,x=30+(index%5)*190,y=24+(index/5)*140,w=175,h=125}).ToArray();Add(key,title,"canvas",new {width=1000,height=Math.Clamp((objs.Length+4)/5*140+40,670,8000),objects=objs});}
        Board("reference","参考图",owned.Keys);Board("mood","情绪板",doc.References.Where(r=>r.IsMoodboard&&owned.ContainsKey(r.Source.ReferenceId)).Select(r=>r.Source.ReferenceId));
        var lights=resources.OfType<PlanningLight>().ToList();var objects=new List<object>();foreach(var l in lights.Where(x=>!x.Archived)){double x=l.X*900,y=l.Y*600;objects.Add(new {id=l.Id,kind="arrow",x,y,w=80,h=10,rotate=l.DirectionDegrees%360,fill="#4E6657"});objects.Add(new {id=Key("light-label:"+l.Id),kind="text",x,y=y+15,w=145,h=45,text=l.Name+" · "+Value(l.PowerPercent)+"%"});}Add("lighting","灯位图","canvas",new {width=1000,height=670,objects});
        const string shared=" name=名称 notes=备注 shots=关联镜头ID archive=归档";
        Table("props","服化道与道具","category=分类 qty=数量 preparation=准备状态 source=来源"+shared,resources.OfType<PlanningProp>().Select(r=>R(r,("category",r.Category),("qty",r.Quantity),("preparation",r.Preparation),("source",SafeSource(r.Source)))));
        Table("gear","设备","model=型号 qty=数量 catalog=来源目录 source=来源ID"+shared,resources.OfType<PlanningGearItem>().Select(r=>R(r,("model",r.Model),("qty",r.Quantity),("catalog",SafeSource(r.SourceCatalog)),("source",SafeSource(r.SourceId)))));
        Table("crew","人员","role=职责 arrival=到场 contact=联系方式"+shared,resources.OfType<PlanningCrewMember>().Select(r=>R(r,("role",r.Role),("arrival",r.ArrivalLocal),("contact",r.Contact))));
        Table("venue","场地","address=地址 hours=可用时间 permit=许可 contact=联系方式 date=日期 zone=时区 sunset=日落 tide=潮汐 source=观测来源"+shared,resources.OfType<PlanningVenue>().Select(r=>R(r,("address",r.Address),("hours",r.AvailableHours),("permit",r.Permit),("contact",r.Contact),("date",r.Date),("zone",r.TimeZoneId),("sunset",r.SunsetLocal),("tide",r.TideLocal),("source",r.ObservationSource))));
        Table("schedule","档期","name=项目 date=拍摄日期 place=地点 booking=关联档期ID",doc.ShootDate==null&&state.BookingId==null?[]:[Row(Key("schedule"),("name",doc.Title),("date",doc.ShootDate?.ToString("yyyy-MM-dd")),("place",doc.Location),("booking",state.BookingId))]);
        Table("timeline","当天时间表","date=日期 start=开始 end=结束 zone=时区 place=地点 owner=负责人"+shared,resources.OfType<PlanningScheduleSlot>().Select(r=>R(r,("date",r.Date),("start",r.StartLocal),("end",r.EndLocal),("zone",r.TimeZoneId),("place",r.Location),("owner",r.Owner))));
        Table("budget","预算","amount=预算金额 currency=币种"+shared,resources.OfType<PlanningBudgetLine>().Select(r=>R(r,("amount",r.Planned),("currency",r.Currency))));
        Table("files","文件","name=名称 source=来源 status=状态",files.Select(f=>Row(f.Id,("name",Leaf(f.Name)),("source","原生项目附件"),("status",f.ProjectCopy==null?"外链元数据":"受管副本元数据"))));
        var custom=doc.CustomBody.Replace("\r\n","\n").Split('\n').Select((text,i)=>Para("custom:"+i,text));Add("document","文档内容","doc",new {paragraphs=custom});
        if(lights.Count>0)Table("lights","灯具清单","x=横向位置 y=纵向位置 angle=方向 power=功率 modifier=修饰器 canvas=快照ID"+shared,lights.Select(r=>R(r,("x",r.X),("y",r.Y),("angle",r.DirectionDegrees),("power",r.PowerPercent),("modifier",r.Modifier),("canvas",r.CanvasRevisionId))));
        return wb;
    }
}

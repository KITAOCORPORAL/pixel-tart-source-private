using System.Globalization;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Read-only export projection. Does not own files or create a second planning document.</summary>
public sealed record PlanningSummaryInput(Guid ProjectId,string ProjectName,PlanningDocument Document,
    PlanningSummary Summary,IReadOnlyList<ProjectShot> Shots,ShootBooking? Booking=null,
    IReadOnlyList<FinanceTransaction>? Expenses=null,string ExpenseState="未读取账本",IReadOnlyList<PlanningVisualLink>? VisualLinks=null);
public sealed record PlanningSummaryEntry(string Title,IReadOnlyList<KeyValuePair<string,string>> Fields)
{
    public IReadOnlyList<PlanningDocumentImage> OwnedImages {get;init;}=[];
}
public sealed record PlanningSummarySection(string Name,IReadOnlyList<PlanningSummaryEntry> Entries)
{
    public IReadOnlyList<PlanningDocumentImage> OwnedImages => Entries.SelectMany(e=>e.OwnedImages).OrderBy(i=>i.Order).DistinctBy(i=>i.DocumentImageId).ToArray();
}
public sealed record PlanningReadableSummary(Guid ProjectId,Guid? DocumentId,IReadOnlyList<PlanningSummarySection> Sections);

public static class PlanningReadableSummaryBuilder
{
    private static readonly JsonSerializerOptions ReadableJson=new(){Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    public static IReadOnlyList<string> SectionOrder { get; }=Array.AsReadOnly(new[]{"项目概览","文字","镜头清单与分镜","参考图","情绪板","灯位图","服化道与道具","设备","人员","场地","档期","当天时间表","预算","文件","文档内容"});
    public static PlanningReadableSummary Build(PlanningSummaryInput input)
    {
        if(input.ProjectId==Guid.Empty)throw new ArgumentException("请先打开策划项目。");
        var d=input.Document;
        if(d.Package is{} identity&&identity.ProjectId!=input.ProjectId)throw new InvalidDataException("策划文档不属于本项目，未汇总。");
        if(input.Shots.Any(s=>s.ProjectId!=input.ProjectId))throw new InvalidDataException("镜头不属于本项目，未汇总。");
        if(input.Booking?.ProjectId is Guid bookingProject&&bookingProject!=input.ProjectId)throw new InvalidDataException("档期不属于本项目，未汇总。");
        var sections=SectionOrder.Select(n=>new List<PlanningSummaryEntry>()).ToArray();
        var owned=(d.Package?.RichLayout?.Images??[]).OrderBy(i=>i.Order).ToArray();
        var placed=new HashSet<(int,Guid)>();
        void Attach(int section,IEnumerable<PlanningDocumentImage> images)
        {
            var values=images.ToArray();if(values.Length==0)return;var entry=sections[section][^1];sections[section][^1]=entry with{OwnedImages=values};
            foreach(var image in values)placed.Add((section,image.DocumentImageId));
        }
        void Add(int section,string title,params (string Key,object? Value)[] fields)=>sections[section].Add(new(title,fields.Select(x=>new KeyValuePair<string,string>(x.Key,Format(x.Value))).ToArray()));
        Add(0,input.ProjectName,("项目ID",input.ProjectId),("文档ID",d.Package?.DocumentId),("标题",d.Title),("副标题",d.Subtitle),("状态",d.Status),("地点",d.Location),("人员",d.People),("拍摄日期",d.ShootDate),
            ("拍摄目标",input.Summary.ShootGoal),("关键词",input.Summary.Keywords),("客户要求",input.Summary.ClientRequirements),("必拍内容",input.Summary.MustCapture),("注意事项",input.Summary.Notes),("交付用途",input.Summary.OutputPurpose),("扩展字段",d.Extensions));
        Add(1,"正文",("内容",d.Body),("正文格式",d.Package?.RichTextPackageBase64 is null?"普通正文":"持久富文本；汇总保留支持的段落/图片/基础表格，缺图在原位置标记"));
        foreach(var shot in input.Shots.OrderBy(s=>s.Order))
        {
            Add(2,shot.Name,("镜头ID",shot.ShotId),("原顺序",shot.Order),("状态",shot.Status),("已归档",shot.IsArchived),("场景",shot.Scene),("景别",shot.ShotSize),("相机与镜头",shot.CameraAndLens),("人物动作",shot.Action),("道具",shot.Props),("灯光",shot.Lighting),("预计分钟",shot.EstimatedMinutes),("备注",shot.Remarks),("原备注",shot.Notes),("色彩方案ID",shot.ReferenceLookId),("自定义列",shot.CustomValues),("扩展字段",shot.Extensions));
            foreach(var reference in shot.References)AddReference(2,reference,"镜头参考",("镜头ID",shot.ShotId));
        }
        foreach(var column in d.ShotColumns)Add(2,"自定义列定义",("列ID",column.Id),("名称",column.Name),("已归档",column.Archived),("扩展字段",column.Extensions));
        var references=d.References.Concat(input.Shots.OrderBy(s=>s.Order).SelectMany(s=>s.References).Select(source=>new PlanningDocumentReference(source,source.Kind switch{ShotReferenceKind.Lighting=>"灯光",ShotReferenceKind.Styling=>"造型",ShotReferenceKind.Pose=>"姿势",ShotReferenceKind.Storyboard=>"场景",_=>"其他"}))).DistinctBy(r=>r.Source.ReferenceId);
        foreach(var reference in references)
        {
            var fields=new (string,object?)[]{("分类",reference.Category),("主参考",reference.IsHero),("情绪板",reference.IsMoodboard),("分组",reference.Group),("关联扩展",reference.Extensions)};
            AddReference(3,reference.Source,"参考图",fields);
            if(reference.IsMoodboard)AddReference(4,reference.Source,"情绪板参考",fields);
            if(reference.Category=="灯光")AddReference(5,reference.Source,"灯光参考",fields);
            if(reference.Category=="造型")AddReference(6,reference.Source,"造型参考",fields);
        }
        foreach(var link in input.VisualLinks??[])
            Add(4,"项目视觉引用",("链接ID",link.LinkId),("名称",link.Title),("类型",link.Kind),("目标ID",link.TargetId),("角色",link.Role),("关联时间",link.LinkedAt),("引用状态","关联元数据；未解析外部版面或打包原文件"),("扩展字段",link.Extensions));
        foreach(var resource in d.Resources.Items)
        {
            var section=resource switch{PlanningLight=>5,PlanningProp=>6,PlanningGearItem=>7,PlanningCrewMember=>8,PlanningVenue=>9,PlanningScheduleSlot=>11,PlanningBudgetLine=>12,_=>throw new NotSupportedException("未知策划资源类型，未导出。")};
            var fields=new List<(string,object?)>();
            foreach(var property in JsonSerializer.SerializeToElement(resource,resource.GetType()).EnumerateObject())fields.Add((ResourceLabel(property.Name),property.Value));
            if(resource is PlanningLight light&&!string.IsNullOrWhiteSpace(light.ReferencePath))fields.Add(("灯位原图状态",ReferenceState(light.ReferencePath)));
            Add(section,resource.Name,fields.ToArray());
            if(resource is PlanningLight imageLight)Attach(section,owned.Where(i=>OwnPathMatches(i,imageLight.ReferencePath)));
        }
        if(input.Booking is{} booking)
            Add(10,booking.Title,("档期ID",booking.Id),("项目ID",booking.ProjectId),("起始UTC",booking.StartAtUtc),("结束UTC",booking.EndAtUtc),("时区",booking.TimeZoneId),("本地时间",BookingTime(booking)),("状态",booking.Status),("地点",booking.Location),("拍摄类型",booking.ShootingType),("拍摄要求",booking.ShootingRequirements),("准备事项",booking.PreparationNotes),("备注",booking.Notes));
        else Add(10,"档期引用",("状态","未关联或当前快照未载入档期；不推断不存在"));
        Add(12,"实际支出（只读）",("来源状态",input.ExpenseState),("说明","预算与实际支出不混算、不跨币种换算；仅包含本次读取成功的项目支出"));
        foreach(var expense in input.Expenses??[])
            if(expense.ProjectId==input.ProjectId&&expense.Kind==FinanceTransactionKind.Expense)
                Add(12,"实际支出",("交易ID",expense.Id),("日期",expense.OccurredOn),("金额最小单位",expense.AmountMinor),("币种",expense.CurrencyCode),("小数位",expense.CurrencyScale),("付款状态",expense.PaymentStatus),("类别ID",expense.CategoryId),("交易方",expense.Counterparty),("方式",expense.PaymentMethod),("备注",expense.Note),("附件路径",expense.AttachmentPaths));
        foreach(var expense in (input.Expenses??[]).Where(x=>x.ProjectId==input.ProjectId&&x.Kind==FinanceTransactionKind.Expense))foreach(var path in expense.AttachmentPaths)Add(12,"支出附件引用",("交易ID",expense.Id),("原位置",path),("引用状态",ReferenceState(path)),("范围","仅引用，不复制附件"));
        foreach(var attachment in d.Attachments)Add(13,attachment.Name,("附件ID",attachment.Id),("原位置",attachment.Path),("引用状态",ReferenceState(attachment.Path)),("交付方式","仅列出引用，不复制或打包附件，不保证跨机可用"),("扩展字段",attachment.Extensions));
        Add(14,"文档内容",("内容",d.CustomBody));
        if(d.Package is{} package)
        {
            foreach(var image in package.RichLayout?.Images??[])Add(14,"文档自有图片记录",("图片ID",image.DocumentImageId),("标题",image.Title),("图注",image.Caption),("原顺序",image.Order),("正文位置",image.Position),("副本相对路径",image.CopyPath),("副本Hash",image.CopyHash),("关联镜头ID",image.ShotId),("说明","正文原位置保留可用图片；图片缺失/变化的说明见文字区与导出警告；此记录不代表文件已打包"));
            foreach(var revision in package.CanvasRevisions)Add(14,"已有画布快照引用",("修订ID",revision.RevisionId),("画布ID",revision.CanvasId),("快照时间",revision.CapturedAt),("范围","元数据引用；汇总DOCX不还原可编辑自由画布"));
            foreach(var asset in package.Assets)Add(14,"文档素材引用",("库ID",asset.LibraryId),("素材ID",asset.AssetId),("内容Hash",asset.ContentHash),("引用状态","库引用未解析；不伪称已嵌入原文件"));
        }
        foreach(var image in owned)
        {
            var section=PlacementSection(image.Placement);
            if(section==1&&d.Package?.RichTextPackageBase64 is null)section=14;
            if(section==2&&image.ShotId is Guid shot&&input.Shots.All(s=>s.ShotId!=shot))section=14;
            // Body keeps its original positions; other placements are materialized only once per section
            // unless distinct saved references deliberately refer to the same owned image identity.
            if(section==1||placed.Contains((section,image.DocumentImageId)))continue;
            Add(section,image.Title,("文档自有图片ID",image.DocumentImageId),("原位置",image.Placement),("图注",image.Caption),("来源",image.OriginLabel),("副本Hash",image.CopyHash),("位置说明",image.Placement is "光标" or "文字"?"没有持久富文本插入位置；保留在文档内容区，不猜测正文坐标":"按已保存图片位置归属"));Attach(section,[image]);
        }
        return new(input.ProjectId,d.Package?.DocumentId,SectionOrder.Select((name,i)=>new PlanningSummarySection(name,sections[i].ToArray())).ToArray());
        void AddReference(int section,ProjectShotReference r,string role,params (string Key,object? Value)[] extra)
        {
            Add(section,r.Title??role,new[]{("引用ID",(object?)r.ReferenceId),("类型",r.Kind),("库ID",r.LibraryId),("素材ID",r.AssetId),("原位置",r.ExternalReference),("引用状态",r.LibraryId is not null?"库引用未解析；保留身份，未复制原文件":ReferenceState(r.ExternalReference)),("备注",r.Note),("固定",r.IsPinned),("执行状态",r.PoseStatus),("详情",r.Details),("扩展字段",r.Extensions)}.Concat(extra).ToArray());
            Attach(section,owned.Where(i=>(r.LibraryId is Guid library&&r.AssetId is Guid asset&&i.Source is{} source&&source.LibraryId==library&&source.AssetId==asset)||OwnPathMatches(i,r.ExternalReference)));
        }
    }
    public static int PlacementSection(string placement)=>placement switch
    {"封面"=>0,"光标"=>1,"文字"=>1,"指定镜头"=>2,"参考图"=>3,"情绪板"=>4,"灯位图" or "灯光图"=>5,"服化道与道具" or "服化道"=>6,_=>14};
    private static bool OwnPathMatches(PlanningDocumentImage image,string? reference)
    {
        if(string.IsNullOrWhiteSpace(reference)||image.StorageRoot is null||!Path.IsPathFullyQualified(reference))return false;
        try{return string.Equals(Path.GetFullPath(reference),Path.GetFullPath(Path.Combine(image.StorageRoot,PlanningImageStorage.RelativePath(image.DocumentId,image.DocumentImageId))),StringComparison.OrdinalIgnoreCase);}
        catch(Exception e)when(e is ArgumentException or NotSupportedException or PathTooLongException){return false;}
    }
    public static string ReferenceState(string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return "未提供路径";
        if(Uri.TryCreate(path,UriKind.Absolute,out var uri)&&!uri.IsFile)return "外部URL；未访问、未打包";
        if(!Path.IsPathFullyQualified(path))return "相对外链；未解析、未打包";
        try{var attributes=File.GetAttributes(path);return attributes.HasFlag(FileAttributes.ReparsePoint)?"重解析引用；未跟随、未打包":attributes.HasFlag(FileAttributes.Directory)?"目录引用；未打包":"本机文件存在；外链未打包、跨机可能缺失";}
        catch(FileNotFoundException){return "缺失文件；仅保留引用";}catch(DirectoryNotFoundException){return "缺失文件；仅保留引用";}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return "引用不可读："+e.GetType().Name;}
    }
    private static string Format(object? value)=>value switch
    {null=>"未填写",bool b=>b?"是":"否",DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),string s=>string.IsNullOrEmpty(s)?"未填写":s,JsonElement j=>j.ValueKind switch{JsonValueKind.Null=>"未填写",JsonValueKind.True=>"是",JsonValueKind.False=>"否",JsonValueKind.String=>string.IsNullOrEmpty(j.GetString())?"未填写":j.GetString()!,_=>JsonSerializer.Serialize(j,ReadableJson)},DateTimeOffset date=>date.ToString("O",CultureInfo.InvariantCulture),IEnumerable<string> values=>string.Join(" / ",values),System.Collections.IEnumerable=>JsonSerializer.Serialize(value,value.GetType(),ReadableJson),_=>Convert.ToString(value,CultureInfo.InvariantCulture)??""};
    private static string BookingTime(ShootBooking b){try{var zone=TimeZoneInfo.FindSystemTimeZoneById(b.TimeZoneId);return $"{TimeZoneInfo.ConvertTime(b.StartAtUtc,zone):yyyy-MM-dd HH:mm} — {TimeZoneInfo.ConvertTime(b.EndAtUtc,zone):yyyy-MM-dd HH:mm}";}catch(Exception e)when(e is TimeZoneNotFoundException or InvalidTimeZoneException){return "时区不可用；请核对UTC";}}
    private static string ResourceLabel(string key)=>key switch
    {"Id"=>"资源ID","Name"=>"名称","ShotIds"=>"关联镜头ID","Archived"=>"已归档","Notes"=>"备注","Category"=>"分类","Preparation"=>"准备状态","Source"=>"来源","Quantity"=>"数量","Model"=>"型号","Role"=>"角色","Contact"=>"联系方式","ArrivalLocal"=>"到场时间","Address"=>"地址","AvailableHours"=>"可用时段","Permit"=>"许可","Date"=>"日期","TimeZoneId"=>"时区","SunsetLocal"=>"日落（手动）","TideLocal"=>"潮汐（手动）","ObservationSource"=>"数据来源","DataStatus"=>"数据状态","StartLocal"=>"开始","EndLocal"=>"结束","Location"=>"地点","Owner"=>"负责人","Planned"=>"预算金额","Currency"=>"币种","X"=>"横向位置","Y"=>"纵向位置","DirectionDegrees"=>"方向","PowerPercent"=>"功率百分比","Modifier"=>"修饰器","ReferencePath"=>"参考原位置","CanvasRevisionId"=>"画布修订ID","SourceCatalog"=>"目录来源","SourceId"=>"来源ID",_=>key};
}

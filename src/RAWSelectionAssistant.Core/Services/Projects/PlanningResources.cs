using System.Text.Json;
using System.Text.Json.Serialization;
namespace RAWSelectionAssistant.Core.Services.Projects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(PlanningProp), "prop")]
[JsonDerivedType(typeof(PlanningGearItem), "gear")]
[JsonDerivedType(typeof(PlanningCrewMember), "crew")]
[JsonDerivedType(typeof(PlanningVenue), "venue")]
[JsonDerivedType(typeof(PlanningScheduleSlot), "schedule")]
[JsonDerivedType(typeof(PlanningBudgetLine), "budget")]
[JsonDerivedType(typeof(PlanningLight), "light")]
public abstract record PlanningResource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "新项目";
    public Guid[] ShotIds { get; init; } = [];
    public bool Archived { get; init; }
    public string Notes { get; init; } = "";
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
    public virtual void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || ShotIds is null || ShotIds.Any(id => id == Guid.Empty) || ShotIds.Distinct().Count() != ShotIds.Length)
            throw new InvalidDataException("名称和关联镜头身份无效。");
    }
}
public sealed record PlanningProp : PlanningResource
{
    public string Category { get; init; } = "道具";
    public string Preparation { get; init; } = "待准备";
    public string Source { get; init; } = "自有";
    public int Quantity { get; init; } = 1;
    public override void Validate() { base.Validate(); if (Quantity < 0) throw new InvalidDataException("数量不能为负。"); }
}
public sealed record PlanningGearItem : PlanningResource
{
    public string Model { get; init; } = "";
    public int Quantity { get; init; } = 1;
    public string SourceCatalog { get; init; } = "Manual";
    public string SourceId { get; init; } = "";
    public override void Validate() { base.Validate(); if (Quantity < 0) throw new InvalidDataException("数量不能为负。"); }
}
public sealed record PlanningCrewMember : PlanningResource
{
    public string Role { get; init; } = "";
    public string Contact { get; init; } = "";
    public string ArrivalLocal { get; init; } = "";
}
public sealed record PlanningVenue : PlanningResource
{
    public string Address { get; init; } = "";
    public string Contact { get; init; } = "";
    public string AvailableHours { get; init; } = "";
    public string Permit { get; init; } = "";
    public DateOnly Date { get; init; } = new(2026, 1, 1);
    public string TimeZoneId { get; init; } = "UTC";
    public string SunsetLocal { get; init; } = "";
    public string TideLocal { get; init; } = "";
    public string ObservationSource { get; init; } = "Manual";
    public string DataStatus { get; init; } = "OfflineManual";
    public override void Validate()
    {
        base.Validate();
        try { TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); } catch (Exception e) when(e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new InvalidDataException("请填写有效时区ID。", e); }
        if (DataStatus != "OfflineManual") throw new InvalidDataException("本版本没有自动日落/潮汐数据服务。");
    }
}
public sealed record PlanningScheduleSlot : PlanningResource
{
    public string StartLocal { get; init; } = "09:00";
    public string EndLocal { get; init; } = "10:00";
    public string Location { get; init; } = "";
    public string Owner { get; init; } = "";
    public DateOnly Date { get; init; } = new(2026, 1, 1);
    public string TimeZoneId { get; init; } = "UTC";
    public override void Validate()
    {
        base.Validate();
        if (!TimeOnly.TryParse(StartLocal, out var a) || !TimeOnly.TryParse(EndLocal, out var b) || b < a) throw new InvalidDataException("请填写同一天有效起止时间；跨日请拆分时间段。");
        try { TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); } catch(Exception e) when(e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new InvalidDataException("时区无效。", e); }
    }
}
public sealed record PlanningBudgetLine : PlanningResource
{
    public decimal Planned { get; init; }
    public string Currency { get; init; } = "CNY";
    public override void Validate() { base.Validate(); if (Planned < 0 || Currency.Length != 3) throw new InvalidDataException("预算或币种无效。"); }
}
public sealed record PlanningLight : PlanningResource
{
    public double X { get; init; } = .5;
    public double Y { get; init; } = .5;
    public double DirectionDegrees { get; init; }
    public double PowerPercent { get; init; } = 50;
    public string Modifier { get; init; } = "柔光箱";
    public Guid? CanvasRevisionId { get; init; }
    public string ReferencePath { get; init; } = "";
    public override void Validate()
    {
        base.Validate();
        if (!double.IsFinite(X) || !double.IsFinite(Y) || X is < 0 or > 1 || Y is < 0 or > 1 || !double.IsFinite(DirectionDegrees) || !double.IsFinite(PowerPercent) || PowerPercent is < 0 or > 100)
            throw new InvalidDataException("灯位坐标0–1、功率0–100，方向必须为有限数。");
    }
}
public sealed record PlanningResources
{
    public int Version { get; init; } = 1;
    public IReadOnlyList<PlanningResource> Items { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
    public PlanningResources Validate()
    {
        if (Version != 1 || Items is null || Items.Count > 10000 || Items.Select(i => i.Id).Distinct().Count() != Items.Count) throw new InvalidDataException("策划资源版本/身份无效。");
        foreach (var item in Items) item.Validate(); return this;
    }
    public PlanningResources Upsert(PlanningResource value) { value.Validate(); return this with { Items = Items.Where(i => i.Id != value.Id).Append(value).ToArray() }; }
    public PlanningResources ImportGear(IEnumerable<PlanningGearItem> values)
    {
        var result = this;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value.SourceId) || value.SourceCatalog == "Manual") throw new InvalidDataException("导入设备必须携带目录和稳定来源ID。");
            if (result.Items.OfType<PlanningGearItem>().Any(i => i.SourceCatalog == value.SourceCatalog && i.SourceId == value.SourceId)) continue;
            result = result.Upsert(value with { Id = Guid.NewGuid() });
        }
        return result;
    }
}

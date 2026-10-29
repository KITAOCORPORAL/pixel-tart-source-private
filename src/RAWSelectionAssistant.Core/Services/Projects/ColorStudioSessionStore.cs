using System.Text.Json;

namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Portable edit session: links to source files, never a second asset metadata database.</summary>
public sealed record ColorStudioSessionTarget(Guid Id, string Path, Guid? AssetId, bool Selected,
    ReferenceLook? Look, ColorAdjustmentStack? Stack, PixelTartFilmSettings? Film,
    ColorStudioMatchEngine Engine = ColorStudioMatchEngine.Stable, MatchV4ExecutionMode ExecutionMode = MatchV4ExecutionMode.Auto,
    int? SessionRating = null, string? SessionColorLabel = null, Guid? LibraryId = null, string? SourceHash = null, IReadOnlyList<StudioEditVersion>? EditVersions = null, Guid? ActiveEditVersionId = null,long? CatalogRevision=null,ProfessionalRawDefinition? RawDefinition=null,ProfessionalRawContract? RawInput=null)
{public bool VariantsExpanded {get;init;}}


public sealed record ColorStudioLayout(double AuxiliaryWidth = 290, double EditingWidth = 340,
    bool AuxiliaryOpen = true, bool EditingOpen = true, int AuxiliaryMode = 0, int ToolMode = 0,
    string FilmstripDock = "Bottom", string FilmstripView = "Grid", bool FilmstripOpen = true)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]public bool ExplicitSdrDisplayColor{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public RAWSelectionAssistant.Core.Services.Color.ControlledIcc.RgbIccIdentityV1? ManualMonitorProfile{get;init;}
    public DarkroomPanelLayout? Darkroom { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ViewingTableState? ViewingTable {get;init;}
    public IReadOnlyList<StudioStillSnapshot> R54Stills {get;init;}=[];
    public double R54GraphHeight {get;init;}=216;
    public bool? R54GraphCollapsed {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? R54ScopesOpen {get;init;}
    public double R54GraphZoom {get;init;}=1;
    public double R54GraphPanX {get;init;}=8;
    public double R54GraphPanY {get;init;}=12;
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public InstantFilmDefinition? FilmDraft{get;init;}
    public string? FilmDraftFrameId{get;init;}
    public string? FilmDraftInputMode{get;init;}
    public bool R53Professional {get;init;}
    public IReadOnlyList<string> R53Favorites {get;init;}=[];
    public IReadOnlyList<string> R53Recents {get;init;}=[];
    public ColorStudioLayout Normalize() {ViewingTable?.Validate();ManualMonitorProfile?.Bytes();FilmDraft?.Validate();return this with
    {
        R54Stills=(R54Stills??[]).Select(s=>s.Freeze()).TakeLast(8).ToArray(),
        R53Favorites=(R53Favorites??[]).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().Take(100).ToArray(),R53Recents=(R53Recents??[]).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().Take(20).ToArray(),
        Darkroom = Darkroom?.Normalize(),
        R54GraphHeight=double.IsFinite(R54GraphHeight)?Math.Clamp(R54GraphHeight,100,500):216,
        R54GraphZoom=double.IsFinite(R54GraphZoom)?Math.Clamp(R54GraphZoom,.25,2):1,
        R54GraphPanX=double.IsFinite(R54GraphPanX)?Math.Clamp(R54GraphPanX,-100000,100000):8,
        R54GraphPanY=double.IsFinite(R54GraphPanY)?Math.Clamp(R54GraphPanY,-100000,100000):12,
        AuxiliaryWidth = double.IsFinite(AuxiliaryWidth) ? Math.Clamp(AuxiliaryWidth, 240, 440) : 290,
        EditingWidth = double.IsFinite(EditingWidth) ? Math.Clamp(EditingWidth, 312, 440) : 340,
        AuxiliaryMode = Math.Clamp(AuxiliaryMode, 0, 3), ToolMode = Math.Clamp(ToolMode, 0, 4),
        FilmstripDock = FilmstripDock == "Right" ? "Right" : "Bottom",
        FilmstripView = FilmstripView is "List" or "Slideshow" ? FilmstripView : "Grid"
    };}
}


/// <summary>Presentation only; independent of the legacy editor and all image definitions.</summary>
public sealed record DarkroomPanelLayout(double Width = 304, bool Open = true, int Tab = 0,
    bool Solo = false, string[]? SectionOrder = null, string[]? CollapsedSections = null)
{
    public DarkroomPanelLayout Normalize() => this with
    {
        Width = double.IsFinite(Width) ? Math.Clamp(Width, 280, 460) : 304,
        Tab = Math.Clamp(Tab, 0, 4),
        SectionOrder = Clean(SectionOrder), CollapsedSections = Clean(CollapsedSections)
    };
    private static string[]? Clean(string[]? values) => values?.Where(value =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 80).Distinct(StringComparer.Ordinal).Take(128).ToArray();
}

public sealed record ColorStudioSessionDocument(IReadOnlyList<ColorStudioSessionTarget> Targets, Guid? ActiveTargetId,
    string FilterScope = "All", int MinimumRating = 0, string ColorLabelFilter = "All", int Version = 1,
    ColorStudioLayout? Layout = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool EmbeddedVersionsAuthoritative {get;init;}
    public ColorStudioSessionDocument Normalize()
    {
        if (Version != 1 || Targets is null || Targets.Count > 10000) throw new InvalidDataException("不支持的 Color Studio 工作文件版本或照片数量。");
        if (Targets.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Path)) || Targets.Select(x => x.Id).Distinct().Count() != Targets.Count)
            throw new InvalidDataException("工作文件中包含无效或重复的照片身份。");
        if (ActiveTargetId is Guid active && Targets.All(x => x.Id != active)) throw new InvalidDataException("当前照片不在工作文件中。");
        if (Targets.Any(x => x.LibraryId == Guid.Empty || (x.LibraryId.HasValue && !x.AssetId.HasValue) ||
            (x.SourceHash is not null && (x.SourceHash.Trim().Length != 64 || !x.SourceHash.Trim().All(Uri.IsHexDigit)))))
            throw new InvalidDataException("素材身份或 SHA-256 无效。");
        var targets = Targets.Select(x => x with
        {
            Path = System.IO.Path.GetFullPath(x.Path), Look = x.Look?.Normalize(), Stack = x.Stack?.DeepClone(),
            SessionRating = x.AssetId is null ? Math.Clamp(x.SessionRating ?? 0, 0, 5) : null,
            SessionColorLabel = x.AssetId is null ? x.SessionColorLabel : null,
            Engine = Enum.IsDefined(x.Engine) ? x.Engine : throw new InvalidDataException("不支持的处理引擎。"),
            ExecutionMode = Enum.IsDefined(x.ExecutionMode) ? x.ExecutionMode : throw new InvalidDataException("不支持的执行模式。")
        }).ToArray();
        foreach (var target in targets)
        {
            if(target.EditVersions is {} variants)
            {
                if(variants.Count(v=>v.IsOriginal)>1||variants.Count(v=>v.IsMain&&!v.Archived)>1||variants.Any(v=>v.Rating is <0 or >5 || v.IsOriginal&&v.Archived))
                    throw new InvalidDataException("原始/主变体或评分身份无效。");
            }
            target.Film?.Validate();target.RawDefinition?.Validate();
            if(target.RawDefinition is not null && (target.Look is not null || target.Stack is not null || target.Film is not null))throw new InvalidDataException("专业 RAW 定义不能混合旧调整栈。");
            if(target.EditVersions?.FirstOrDefault(v=>v.Id==target.ActiveEditVersionId) is {} selected && System.Text.Json.JsonSerializer.Serialize(selected.RawDefinition)!=System.Text.Json.JsonSerializer.Serialize(target.RawDefinition))throw new InvalidDataException("会话活动版本与 RAW 定义不一致。");
            if (target.EditVersions is { } versions && (versions.Select(v=>v.Id).Distinct().Count()!=versions.Count || versions.Any(v=>v.Id==Guid.Empty || v.LibraryId!=target.LibraryId || v.AssetId!=target.AssetId || v.SourceHash!=target.SourceHash) || (target.ActiveEditVersionId is Guid activeVersion && !versions.Any(v=>v.Id==activeVersion)))) throw new InvalidDataException("编辑版本身份无效。");
            foreach(var version in target.EditVersions ?? []) { if(string.IsNullOrWhiteSpace(version.Name) || !Enum.IsDefined(version.Engine) || !Enum.IsDefined(version.Mode))throw new InvalidDataException("编辑版本参数无效。");version.FilmBackend?.Validate();if(version.FilmBackend is{} backend&&(backend.SourceSha256!=version.SourceHash||backend.SourceVariantId!=version.OriginVersionId||version.Stack?.Nodes.Any(n=>n.Id==backend.RequestId&&n.Type==ColorStudioNodeType.Film&&n.SystemLocked&&n.FilmSettings is{} settings&&System.Text.Json.JsonSerializer.Serialize(settings)==backend.ParametersJson)!=true))throw new InvalidDataException("胶片结果变体与冻结后端契约不一致。");version.Stack?.Normalize(); version.Film?.Validate();version.RawDefinition?.Validate();if(version.RawDefinition is not null && (version.Look is not null || version.Stack is not null || version.Film is not null))throw new InvalidDataException("专业 RAW 版本包含旧调整。"); }
        }
        return this with { Targets = targets, MinimumRating = Math.Clamp(MinimumRating, 0, 5), Layout = Layout?.Normalize() };
    }
}

public static class ColorStudioSessionStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task<string?> FingerprintAsync(string path,CancellationToken token=default)
    {
        if(!File.Exists(path))return null;
        await using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(input,token));
    }
    public static async Task<string> SaveCheckedAsync(string path,ColorStudioSessionDocument document,string? expectedFingerprint,CancellationToken token=default)
    {
        path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Cross-process lock plus exact file content identity. Dialog overwrite is not concurrency control.
        await using var gate=new FileStream(path+".session.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var current=await FingerprintAsync(path,token);
        if(!string.Equals(current,expectedFingerprint,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("工作文件已被其他实例修改；未覆盖。请另存副本或重新打开后合并。");
        await SaveAsync(path,document,token);return (await FingerprintAsync(path,token))!;
    }
    public static async Task SaveAsync(string path, ColorStudioSessionDocument document, CancellationToken token = default)
    {
        document = document.Normalize(); path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await JsonSerializer.SerializeAsync(stream, document, Options, token); await stream.FlushAsync(token); stream.Flush(true); }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static async Task<ColorStudioSessionDocument> LoadAsync(string path, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        return (await JsonSerializer.DeserializeAsync<ColorStudioSessionDocument>(stream, Options, token) ?? throw new InvalidDataException("工作文件为空。")).Normalize();
    }
}

public sealed record StudioStillSnapshot(Guid Id,string Name,string PngBase64,ColorAdjustmentStack? Stack,int Version=1)
{
    public Guid? SourceTargetId{get;init;} public Guid? SourceVersionId{get;init;} public string? SourceHash{get;init;} public string InputDomain{get;init;}="UnknownLegacy";
    public StudioStillSnapshot Freeze(){if(InputDomain is not("UnknownLegacy" or "EncodedSrgbRasterStack" or "RenderedProfessionalNoTransfer" or "RawNoTransfer")||SourceHash is{} hash&&(hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c)))||Version!=1||Id==Guid.Empty||string.IsNullOrWhiteSpace(Name)||Name.Length>120||PngBase64 is null||PngBase64.Length>12*1024*1024)throw new InvalidDataException("剧照数据无效或超限。");var bytes=Convert.FromBase64String(PngBase64);if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4))>1200||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4))>1200)throw new InvalidDataException("剧照PNG尺寸无效。");return this with{Stack=Stack?.DeepClone()};}
}

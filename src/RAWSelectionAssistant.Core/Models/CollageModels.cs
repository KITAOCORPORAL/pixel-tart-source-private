namespace RAWSelectionAssistant.Core.Models;

public enum CollageMode { Template, VerticalStrip, HorizontalStrip }
public enum CollageFitMode { FillCrop, Fit, OriginalRatio }

public sealed record CollageSlot(string Id, double X, double Y, double Width, double Height);

public sealed class CollageTemplate
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required int ImageCount { get; init; }
    public required IReadOnlyList<CollageSlot> Slots { get; init; }
    public override string ToString() => DisplayName;
}

public sealed class CollageImageState
{
    public string SourcePath { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public double Zoom { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public int Rotation { get; set; }
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }
    public CollageFitMode FitMode { get; set; } = CollageFitMode.FillCrop;
}

public sealed class CollageExportOptions
{
    public string Format { get; set; } = "JPG";
    public int JpegQuality { get; set; } = 95;
    public int PixelWidth { get; set; } = 2400;
    public int PixelHeight { get; set; } = 2400;
    public bool TransparentBackground { get; set; }
    public string BackgroundColor { get; set; } = "#18191C";
    public string BorderColor { get; set; } = "#000000";
    public double OuterMargin { get; set; } = 24;
    public double Spacing { get; set; } = 16;
    public double CornerRadius { get; set; }
    public double BorderWidth { get; set; }
    public bool Shadow { get; set; }
}

public sealed class CollageProject
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "未命名拼图";
    public CollageMode Mode { get; set; } = CollageMode.Template;
    public string TemplateId { get; set; } = "2-left-right";
    public List<CollageImageState> Images { get; init; } = [];
    public CollageExportOptions Export { get; init; } = new();
}

public static class CollageTemplateCatalog
{
    private static CollageSlot S(string id, double x, double y, double w, double h) => new(id, x, y, w, h);

    public static IReadOnlyList<CollageTemplate> All { get; } =
    [
        T("2-left-right", "左右均分", 2, S("1",0,0,.5,1), S("2",.5,0,.5,1)),
        T("2-top-bottom", "上下均分", 2, S("1",0,0,1,.5), S("2",0,.5,1,.5)),
        T("2-left-large", "左大右小", 2, S("1",0,0,.66,1), S("2",.66,0,.34,1)),
        T("2-top-large", "上大下小", 2, S("1",0,0,1,.66), S("2",0,.66,1,.34)),
        T("3-columns", "三列", 3, S("1",0,0,.333,1), S("2",.333,0,.334,1), S("3",.667,0,.333,1)),
        T("3-rows", "三行", 3, S("1",0,0,1,.333), S("2",0,.333,1,.334), S("3",0,.667,1,.333)),
        T("3-left-large", "左大右二小", 3, S("1",0,0,.66,1), S("2",.66,0,.34,.5), S("3",.66,.5,.34,.5)),
        T("3-top-large", "上大下二小", 3, S("1",0,0,1,.66), S("2",0,.66,.5,.34), S("3",.5,.66,.5,.34)),
        T("3-right-large", "左二小右大", 3, S("1",0,0,.34,.5), S("2",0,.5,.34,.5), S("3",.34,0,.66,1)),
        T("3-bottom-large", "上二小下大", 3, S("1",0,0,.5,.34), S("2",.5,0,.5,.34), S("3",0,.34,1,.66)),
        T("4-grid", "2×2", 4, S("1",0,0,.5,.5), S("2",.5,0,.5,.5), S("3",0,.5,.5,.5), S("4",.5,.5,.5,.5)),
        T("4-columns", "四列", 4, S("1",0,0,.25,1), S("2",.25,0,.25,1), S("3",.5,0,.25,1), S("4",.75,0,.25,1)),
        T("4-rows", "四行", 4, S("1",0,0,1,.25), S("2",0,.25,1,.25), S("3",0,.5,1,.25), S("4",0,.75,1,.25)),
        T("4-left-large", "左大右三小", 4, S("1",0,0,.66,1), S("2",.66,0,.34,.333), S("3",.66,.333,.34,.334), S("4",.66,.667,.34,.333)),
        T("4-top-large", "上大下三小", 4, S("1",0,0,1,.66), S("2",0,.66,.333,.34), S("3",.333,.66,.334,.34), S("4",.667,.66,.333,.34)),
        T("5-two-three", "上二下三", 5, S("1",0,0,.5,.5), S("2",.5,0,.5,.5), S("3",0,.5,.333,.5), S("4",.333,.5,.334,.5), S("5",.667,.5,.333,.5)),
        T("5-three-two", "上三下二", 5, S("1",0,0,.333,.5), S("2",.333,0,.334,.5), S("3",.667,0,.333,.5), S("4",0,.5,.5,.5), S("5",.5,.5,.5,.5)),
        T("5-left-large", "左大右四小", 5, S("1",0,0,.6,1), S("2",.6,0,.2,.5), S("3",.8,0,.2,.5), S("4",.6,.5,.2,.5), S("5",.8,.5,.2,.5)),
        T("5-center", "中间大图加四角小图", 5, S("1",.25,.25,.5,.5), S("2",0,0,.25,.25), S("3",.75,0,.25,.25), S("4",0,.75,.25,.25), S("5",.75,.75,.25,.25)),
        T("6-2x3", "2×3", 6, Grid(3,2)),
        T("6-3x2", "3×2", 6, Grid(2,3)),
        T("6-columns", "左右各三", 6, Grid(2,3)),
        T("6-rows", "上下各三", 6, Grid(3,2))
    ];

    public static CollageTemplate Get(string id) => All.FirstOrDefault(x => x.Id == id) ?? All[0];
    private static CollageTemplate T(string id, string name, int count, params CollageSlot[] slots) => new() { Id=id, DisplayName=name, ImageCount=count, Slots=slots };
    private static CollageSlot[] Grid(int columns, int rows) => Enumerable.Range(0, columns * rows).Select(i => S((i+1).ToString(), (double)(i%columns)/columns, (double)(i/columns)/rows, 1d/columns, 1d/rows)).ToArray();
}

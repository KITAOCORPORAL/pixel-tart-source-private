namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningPageLayout(double Width=794,double Height=1123,double Left=48,double Top=48,double Right=48,double Bottom=48)
{
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
    public PlanningPageLayout Validate(){if(new[]{Width,Height,Left,Top,Right,Bottom}.Any(x=>!double.IsFinite(x)||x<0)||Width<200||Height<200||Left+Right>=Width||Top+Bottom>=Height)throw new ArgumentException("页面布局无效。");return this;}
}
public sealed record PlanningBlockSemantic(int BlockIndex,string Style="Body",bool PageBreakBefore=false,Guid? CanvasId=null,Guid? CanvasRevisionId=null)
{
    public bool RepeatHeader { get; init; }
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
}
public sealed record PlanningRichLayout(PlanningPageLayout Page,IReadOnlyList<PlanningBlockSemantic> Blocks,IReadOnlyList<string> ImportLosses,int Version=1)
{
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? Extensions { get; init; }
    public void Validate(){if(Version!=1||Page is null||Blocks is null||ImportLosses is null)throw new ArgumentException("布局版本无效。");Page.Validate();if(Blocks.Count>100000||ImportLosses.Count>10000)throw new ArgumentException("布局对象过多。");if(Blocks.Any(b=>b.BlockIndex<0||b.Style is not("Body" or "Heading1" or "Heading2" or "Heading3"))||Blocks.Select(b=>b.BlockIndex).Distinct().Count()!=Blocks.Count)throw new ArgumentException("段落语义无效。");}
}

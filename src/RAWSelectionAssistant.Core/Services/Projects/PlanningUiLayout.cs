namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningUiLayout(double ListWidth=248,double InspectorWidth=300,bool InspectorOpen=true,string[]? CollapsedGroups=null,string[]? InspectorGroupOrder=null,double DocumentZoom=100)
{
    public PlanningUiLayout Normalize()=>this with { ListWidth=double.IsFinite(ListWidth)?Math.Clamp(ListWidth,200,380):248,
        InspectorWidth=double.IsFinite(InspectorWidth)?Math.Clamp(InspectorWidth,260,440):300,CollapsedGroups=CollapsedGroups??[],
        InspectorGroupOrder=(InspectorGroupOrder??[]).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(32).ToArray(),DocumentZoom=double.IsFinite(DocumentZoom)?Math.Clamp(DocumentZoom,50,200):100 };
}

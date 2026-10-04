using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.ViewModels;
public sealed partial class TetherReferenceModeViewModel
{
    private double DevelopValue(string key) => AdjustmentStack.Nodes.FirstOrDefault(x=>x.Type==ColorStudioNodeType.Develop)?.NumericParameters.GetValueOrDefault(key) ?? 0;
    private void SetDevelop(string key,double value)
    {
        if (DevelopValue(key)==value) return;
        var node=AdjustmentStack.Nodes.FirstOrDefault(x=>x.Type==ColorStudioNodeType.Develop) ?? new ColorAdjustmentStackNode(Guid.NewGuid(),ColorStudioNodeType.Develop,"影调与细节");
        var changed=node with { NumericParameters=new Dictionary<string,double>(node.NumericParameters){[key]=value} };
        Enabled=true;
        ChangeStack(nodes=>nodes.Any(x=>x.Id==node.Id)?nodes.Select(x=>x.Id==node.Id?changed:x).ToArray():[..nodes,changed]);
    }
    public void ResetDevelopGroup(string group)
    {
        string[] keys=group switch {"明度"=>["exposure","brightness","highlights","midtones","shadows"],"对比与端点"=>["contrast","whites","blacks"],_=>["structure","detail"]};
        ChangeStack(nodes=>nodes.Select(node=>node.Type==ColorStudioNodeType.Develop ? node with {NumericParameters=node.NumericParameters.Where(x=>!keys.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value)}:node).ToArray());
    }
    private void NotifyDevelop()
    {
        OnPropertyChanged(nameof(Exposure));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(Highlights));
        OnPropertyChanged(nameof(Midtones));
        OnPropertyChanged(nameof(Shadows));
        OnPropertyChanged(nameof(DevelopContrast));
        OnPropertyChanged(nameof(Whites));
        OnPropertyChanged(nameof(Blacks));
        OnPropertyChanged(nameof(Structure));
        OnPropertyChanged(nameof(Detail));
    }
    public double Exposure { get => DevelopValue("exposure"); set => SetDevelop("exposure",Math.Clamp(value,-3,3)); }
    public double Brightness { get => DevelopValue("brightness"); set => SetDevelop("brightness",Math.Clamp(value,-100,100)); }
    public double Highlights { get => DevelopValue("highlights"); set => SetDevelop("highlights",Math.Clamp(value,-100,100)); }
    public double Midtones { get => DevelopValue("midtones"); set => SetDevelop("midtones",Math.Clamp(value,-100,100)); }
    public double Shadows { get => DevelopValue("shadows"); set => SetDevelop("shadows",Math.Clamp(value,-100,100)); }
    public double DevelopContrast { get => DevelopValue("contrast"); set => SetDevelop("contrast",Math.Clamp(value,-100,100)); }
    public double Whites { get => DevelopValue("whites"); set => SetDevelop("whites",Math.Clamp(value,-100,100)); }
    public double Blacks { get => DevelopValue("blacks"); set => SetDevelop("blacks",Math.Clamp(value,-100,100)); }
    public double Structure { get => DevelopValue("structure"); set => SetDevelop("structure",Math.Clamp(value,-100,100)); }
    public double Detail { get => DevelopValue("detail"); set => SetDevelop("detail",Math.Clamp(value,-100,100)); }
}

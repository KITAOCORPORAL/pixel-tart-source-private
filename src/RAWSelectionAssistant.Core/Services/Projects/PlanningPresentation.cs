using System.Text.Json;
using System.Text.Json.Serialization;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record PlanningStructureBlock(Guid Id, string Kind, Guid[] SourceIds)
{
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extensions { get; init; }
}
public sealed record PlanningPresentation
{
    public int Version { get; init; } = 1;
    public string Mode { get; init; } = "Simple";
    public bool ProposalInitialized { get; init; }
    public IReadOnlyList<PlanningStructureBlock> Blocks { get; init; } = [];
    [JsonExtensionData] public Dictionary<string,JsonElement>? Extensions { get; init; }
    public void Validate()
    {
        if(Version!=1 || Mode is not("Simple" or "Advanced") || Blocks is null || Blocks.Select(b=>b.Id).Distinct().Count()!=Blocks.Count
            || Blocks.Any(b=>b.Id==Guid.Empty || b.Kind is not("Shots" or "Resources") || b.SourceIds is null || b.SourceIds.Any(id=>id==Guid.Empty)))throw new InvalidDataException("文档展示结构无效。");
    }
    public PlanningPresentation Initialize(IEnumerable<Guid> shots,IEnumerable<Guid> resources)
        => ProposalInitialized ? this : this with { ProposalInitialized=true, Blocks=[new(Guid.NewGuid(),"Shots",shots.Distinct().ToArray()),new(Guid.NewGuid(),"Resources",resources.Distinct().ToArray())] };
}

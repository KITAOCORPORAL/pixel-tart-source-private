using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;
public sealed record StudioVersionConflict(StudioVersionCatalog Repository,IReadOnlyList<StudioEditVersion> Local,Guid? LocalSelected,string Reason);
public static class StudioVersionConflictResolution
{
    public static IReadOnlyList<StudioEditVersion> PreserveLocalAsNew(StudioVersionConflict conflict)
    {
        var result=conflict.Repository.Versions.ToList();
        foreach(var local in conflict.Local)
        {
            var already=result.FirstOrDefault(v=>v.OriginIdentity==conflict.Repository.Identity&&v.OriginVersionId==local.Id&&v.Name==local.Name+" · 本地保留"&&JsonSerializer.Serialize(new{v.Look,v.Stack,v.Film,v.Engine,v.Mode,v.Archived})==JsonSerializer.Serialize(new{local.Look,local.Stack,local.Film,local.Engine,local.Mode,local.Archived}));if(already is not null)continue;
            var remote=result.FirstOrDefault(v=>v.Id==local.Id);
            if(remote is not null&&JsonSerializer.Serialize(remote)==JsonSerializer.Serialize(local))continue;
            result.Add(local with{Id=Guid.NewGuid(),Name=local.Name+" · 本地保留",CreatedAt=DateTimeOffset.UtcNow,OriginVersionId=local.Id,OriginIdentity=conflict.Repository.Identity});
        }
        return result;
    }
}

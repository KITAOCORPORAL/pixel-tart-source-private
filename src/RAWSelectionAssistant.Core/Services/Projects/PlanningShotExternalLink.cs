namespace RAWSelectionAssistant.Core.Services.Projects;

/// <summary>Approval/ownership belongs to this planning document, not to the execution source.
/// Shot IDs are explicit recipients. Source records in .shots.json are never rewritten.</summary>
public sealed record PlanningShotExternalLink(Guid ReferenceId, string OriginalPath, IReadOnlyList<Guid> ShotIds,
    PlanningExternalFile? ReconnectedFile = null, PlanningProjectCopy? ProjectCopy = null)
{
    public void Validate()
    {
        if(ReferenceId==Guid.Empty || !Path.IsPathFullyQualified(OriginalPath) || ShotIds is null || ShotIds.Count==0 ||
            ShotIds.Any(id=>id==Guid.Empty) || ShotIds.Distinct().Count()!=ShotIds.Count || (ReconnectedFile is null && ProjectCopy is null))
            throw new InvalidDataException("镜头外链收据身份无效。");
        ReconnectedFile?.Validate(ReferenceId,"reference",OriginalPath);
    }
}

public static class PlanningReferenceLinks
{
    public static IEnumerable<PlanningProjectCopy> Copies(PlanningDocument document) => document.References.Select(r=>r.ProjectCopy)
        .Concat(document.Attachments.Select(a=>a.ProjectCopy)).Concat(document.ShotExternalLinks.Select(s=>s.ProjectCopy)).OfType<PlanningProjectCopy>();

    public static bool SameSource(ProjectShotReference a,ProjectShotReference b) => a.ReferenceId==b.ReferenceId &&
        a.LibraryId==b.LibraryId && a.AssetId==b.AssetId && string.Equals(a.ExternalReference,b.ExternalReference,StringComparison.Ordinal);

    public static bool HasConflict(PlanningDocument document,IEnumerable<ProjectShot> shots,Guid referenceId)
    {
        var docs=document.References.Where(r=>r.Source.ReferenceId==referenceId).ToArray();
        var sources=docs.Select(r=>r.Source).Concat(shots.SelectMany(s=>s.References).Where(r=>r.ReferenceId==referenceId)).ToArray();
        return docs.Length>1 || document.Attachments.Any(a=>a.Id==referenceId) || sources.Length==0 || sources.Any(s=>!SameSource(sources[0],s));
    }

    public static void ValidateCatalog(PlanningDocument document,ProjectShotCatalog catalog)
    {
        foreach(var link in document.ShotExternalLinks)
        {
            var occurrences=catalog.Shots.Where(s=>link.ShotIds.Contains(s.ShotId)).ToArray();
            if(occurrences.Length!=link.ShotIds.Count || occurrences.Any(s=>!s.References.Any(r=>r.ReferenceId==link.ReferenceId && r.LibraryId is null && r.AssetId is null && r.ExternalReference==link.OriginalPath)) ||
                document.References.Any(r=>r.Source.ReferenceId==link.ReferenceId)||document.Attachments.Any(a=>a.Id==link.ReferenceId))throw new InvalidDataException("镜头外链收据与镜头来源不符。");
        }
    }

    public static PlanningDocumentReference ForShot(PlanningDocument document,ProjectShot shot,ProjectShotReference source,string category="其他")
    {
        var documentLink=document.References.FirstOrDefault(r=>r.Source.ReferenceId==source.ReferenceId);
        if(documentLink is not null)return SameSource(documentLink.Source,source)?documentLink with{Source=source}:new(source,category);
        var receipt=document.ShotExternalLinks.SingleOrDefault(r=>r.ReferenceId==source.ReferenceId);
        return new(source,category){ProjectCopy=source.LibraryId is null && source.AssetId is null && receipt is not null && receipt.ShotIds.Contains(shot.ShotId) && receipt.OriginalPath==source.ExternalReference?receipt.ProjectCopy:null,
            ReconnectedFile=source.LibraryId is null && source.AssetId is null && receipt is not null && receipt.ShotIds.Contains(shot.ShotId) && receipt.OriginalPath==source.ExternalReference?receipt.ReconnectedFile:null};
    }
}

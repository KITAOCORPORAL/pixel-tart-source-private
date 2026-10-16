using System.Text.Json;
namespace RAWSelectionAssistant.Core.Services.Projects;

public enum InstantPermissionStatus { Unknown, Allowed, Denied }
public enum InstantEvidenceGrade { A, B, C, D }
public sealed record InstantUseAssessment(string Use, InstantEvidenceGrade Grade, string Reason);
public sealed record InstantReferenceRecord(string Id, string Provider, string Kind, JsonElement Original,
    string ReviewStatus, IReadOnlyList<string> ReviewReasons, IReadOnlyList<InstantUseAssessment> UseAssessments);
public sealed record InstantReferenceSource(string Id, string RecordId, string Url, string Kind, string Verification);
public sealed record InstantIccObservation(string Status, int Bytes, string? Sha256, string Interpretation);
public sealed record InstantReferenceImage(string Id, string RecordId, string SourceId, string LocalRelativePath,
    long Bytes, string Sha256, string? ClaimedSha256, bool HashMatchesProvider, int Width, int Height,
    string Mode, InstantIccObservation Icc, string Software, string ScanWorkflow, string ReleaseBundling);
public sealed record InstantReferenceRelationship(string Id, string Kind, string Left, string Right, bool VerifiedScenePair);
public sealed record InstantReferenceObservation(string Id, string RecordId, string Claim, string Authority,
    bool QuantitativeFitAllowed, string ContraryEvidence);
public sealed record InstantReferencePermission(string RecordId, InstantPermissionStatus Status,
    InstantPermissionStatus ImageRedistribution, string CodeLicense, string ClaimedLicenseUrl,
    string ProviderClaim, string Reason);

/// <summary>Research evidence, not a physical calibration dataset. Original provider fields never overwritten.</summary>
public sealed record InstantReferenceLibrary(int SchemaVersion, string AuditDate, bool CalibrationEligible,
    IReadOnlyList<InstantReferenceRecord> Records, IReadOnlyList<InstantReferenceSource> Sources,
    IReadOnlyList<InstantReferenceImage> Images, IReadOnlyList<InstantReferenceRelationship> Relationships,
    IReadOnlyList<InstantReferenceObservation> Observations, IReadOnlyList<InstantReferencePermission> Permissions,
    IReadOnlyList<string> Gaps)
{
    public static InstantReferenceLibrary Read(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var library = JsonSerializer.Deserialize<InstantReferenceLibrary>(json, options)
            ?? throw new InvalidDataException("Missing instant reference library.");
        library.Validate(); return library;
    }
    public void Validate()
    {
        if (SchemaVersion != 1 || CalibrationEligible || !DateOnly.TryParseExact(AuditDate,"yyyy-MM-dd",out _))
            throw new InvalidDataException("Uncontrolled intake cannot assert calibration eligibility.");
        if (Records is null || Sources is null || Images is null || Relationships is null || Observations is null || Permissions is null || Gaps is null)
            throw new InvalidDataException("Missing evidence collections.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Records)
            if (r is null || r.Provider is not ("GROK" or "MUSE") || r.Id is null || !r.Id.StartsWith(r.Provider+"-POL-",StringComparison.Ordinal)
                || !ids.Add(r.Id) || r.Original.ValueKind != JsonValueKind.Object || r.UseAssessments is null
                || r.UseAssessments.Any(a=>a is null || !Enum.IsDefined(a.Grade) || string.IsNullOrWhiteSpace(a.Reason)))
                throw new InvalidDataException("Invalid provider identity, duplicate ID or missing evidence assessment.");
        void Unique(IEnumerable<string> values) { var set = new HashSet<string>(StringComparer.Ordinal); foreach(var v in values) if(string.IsNullOrWhiteSpace(v)||!set.Add(v))throw new InvalidDataException("Duplicate evidence identity."); }
        Unique(Sources.Select(s=>s.Id)); Unique(Images.Select(s=>s.Id)); Unique(Relationships.Select(s=>s.Id)); Unique(Observations.Select(s=>s.Id)); Unique(Permissions.Select(s=>s.RecordId));
        var sourceIds = Sources.ToDictionary(s=>s.Id,s=>s.RecordId,StringComparer.Ordinal);
        if (Sources.Any(s=>!ids.Contains(s.RecordId)) || Images.Any(i=>!ids.Contains(i.RecordId) || !sourceIds.TryGetValue(i.SourceId,out var owner) || owner!=i.RecordId || i.Width<=0 || i.Height<=0 || i.Bytes<=0 || !IsSha(i.Sha256)
            || i.Icc is null || i.Icc.Bytes<0 || i.Icc.Sha256 is {} hash&&!IsSha(hash))
            || Observations.Any(o=>!ids.Contains(o.RecordId)||o.QuantitativeFitAllowed)
            || Permissions.Any(p=>!ids.Contains(p.RecordId)||!Enum.IsDefined(p.Status)||!Enum.IsDefined(p.ImageRedistribution))
            || ids.Any(id=>!Permissions.Any(p=>p.RecordId==id)))
            throw new InvalidDataException("Dangling evidence reference, invalid hash or unsupported quantitative assertion.");
        foreach(var relation in Relationships)
        {
            if(relation.Kind is not("SourceGroupOnly" or "SameScenePair") || relation.Left==relation.Right)throw new InvalidDataException("Unknown or self relation.");
            if(relation.Kind!="SameScenePair"&&!ids.Contains(relation.Left))throw new InvalidDataException("Unknown relation owner.");
            // Source-group identity is NOT an image pair. Pair endpoints must be actual scan versions.
            if(relation.Kind=="SourceGroupOnly" && relation.VerifiedScenePair)throw new InvalidDataException("Source association is not a verified scene pair.");
            if(relation.Kind=="SameScenePair" && (!Images.Any(i=>i.Id==relation.Left)||!Images.Any(i=>i.Id==relation.Right)))
                throw new InvalidDataException("A pair must reference two actual scan versions.");
        }
    }
    private static bool IsSha(string? s)=>s is { Length:64 } && s.All(Uri.IsHexDigit);
}

using System.Text.Json;
using System.Text.Json.Nodes;
using RAWSelectionAssistant.Core.Services.Projects;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class InstantEvidenceTests
{
    private static string Intake()=>File.ReadAllText(Path.Combine(TestResources.Root,"docs/research/instant-film-v3-20261016/LIBRARY_AUDIT.json"));
    [TestMethod]
    public void ObservedIntakePreservesOriginalFieldsAndSeparatesImageLiteratureAndPermissions()
    {
        var library=InstantReferenceLibrary.Read(Intake());
        Assert.AreEqual(22,library.Records.Count);Assert.AreEqual(10,library.Images.Count);
        Assert.AreEqual(12,library.Records.Count(r=>r.Kind=="WebOrLiterature"));
        Assert.IsTrue(library.Records.All(r=>r.Provider=="GROK"));
        Assert.IsTrue(library.Records.All(r=>r.Original.TryGetProperty("reference_grade",out _)));
        Assert.IsTrue(library.Images.All(i=>i.HashMatchesProvider));
        Assert.IsTrue(library.Permissions.All(p=>p.Status==InstantPermissionStatus.Unknown));
        Assert.IsTrue(library.Records.All(r=>r.UseAssessments.Single(a=>a.Use=="ModernFilmCalibration").Grade==InstantEvidenceGrade.D));
        Assert.IsFalse(library.CalibrationEligible);Assert.IsTrue(library.Gaps.Any(g=>g.Contains("MUSE")));
        Assert.IsFalse(library.Relationships.Any(r=>r.VerifiedScenePair));
        Assert.AreEqual(3,library.Images.Count(i=>i.Icc.Status=="BytesRead"));
    }
    [TestMethod]
    public void CalibrationQuantitativeFitDuplicateIdentityAndFalsePairsFailClosed()
    {
        var n=JsonNode.Parse(Intake())!; n["calibrationEligible"]=true;
        Assert.Throws<InvalidDataException>(()=>InstantReferenceLibrary.Read(n.ToJsonString()));
        n=JsonNode.Parse(Intake())!;n["observations"]![0]!["quantitativeFitAllowed"]=true;
        Assert.Throws<InvalidDataException>(()=>InstantReferenceLibrary.Read(n.ToJsonString()));
        n=JsonNode.Parse(Intake())!;n["relationships"]![0]!["verifiedScenePair"]=true;
        Assert.Throws<InvalidDataException>(()=>InstantReferenceLibrary.Read(n.ToJsonString()));
        n=JsonNode.Parse(Intake())!;n["records"]![1]!["id"]=n["records"]![0]!["id"]!.GetValue<string>();
        Assert.Throws<InvalidDataException>(()=>InstantReferenceLibrary.Read(n.ToJsonString()));
        n=JsonNode.Parse(Intake())!;n["images"]![0]!["sha256"]="unread-file-not-hashed";
        Assert.Throws<InvalidDataException>(()=>InstantReferenceLibrary.Read(n.ToJsonString()));
    }
}


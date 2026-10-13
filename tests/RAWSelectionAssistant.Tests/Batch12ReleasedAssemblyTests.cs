using RAWSelectionAssistant.Core.Services.Projects;
using RAWSelectionAssistant.Core.Services.ColorValidation;
namespace RAWSelectionAssistant.Tests;
[TestClass]
public sealed class Batch12ReleasedAssemblyTests
{
    [TestMethod]
    public void ReleasedCoreContainsStrictImporterAndKeepsCreativeFilmEvidence()
    {
        var path=Environment.GetEnvironmentVariable("PIXEL_TART_BATCH12_RELEASE_CORE");
        if(string.IsNullOrWhiteSpace(path))Assert.Inconclusive("Set PIXEL_TART_BATCH12_RELEASE_CORE to test Release payload.");
        Assert.AreEqual(Path.GetFullPath(path!),Path.GetFullPath(typeof(ClassicCgatsImporter).Assembly.Location));
        Assert.AreEqual("ClassicCgatsV1",ClassicCgatsImporter.ParserVersion);
        Assert.IsTrue(InstantFilmProfiles.Available.All(p=>p.Evidence=="CreativeApproximationV1"));
    }
}

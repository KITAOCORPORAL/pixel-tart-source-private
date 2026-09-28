using RAWSelectionAssistant.Core.Services.Projects;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class PhotographyEvidencePortabilityTests
{
    [TestMethod]
    public void LfAndCrlfEvidenceHaveTheSameCanonicalHashAndLength()
    {
        var path = Path.Combine(Path.GetTempPath(), "PixelTart-Evidence-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{\n  \"key\": \"value\"\n}\n");
            var hash = PhotographyEvidenceManifest.HashFile(path);
            var bytes = PhotographyEvidenceManifest.CanonicalEvidenceBytes(path).Length;
            File.WriteAllText(path, "{\r\n  \"key\": \"value\"\r\n}\r\n");
            Assert.AreEqual(hash, PhotographyEvidenceManifest.HashFile(path));
            Assert.HasCount(bytes, PhotographyEvidenceManifest.CanonicalEvidenceBytes(path));
            File.WriteAllText(path, "{\r\n  \"key\": \"changed\"\r\n}\r\n");
            Assert.AreNotEqual(hash, PhotographyEvidenceManifest.HashFile(path));
        }
        finally { File.Delete(path); }
    }
}

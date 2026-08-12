using System.Text.Json;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class CoreReliabilityRc3ContractTests
{
    [TestMethod]
    public void RealFileHarness_IsLocalOnlyAndReportsSanitizedPaths()
    {
        var program = Text("tools/real-file-validation/Program.cs");
        var readme = Text("tools/real-file-validation/README.md");
        StringAssert.Contains(program, "<USER_PATH>");
        StringAssert.Contains(program, "<VALIDATION_PATH>");
        StringAssert.Contains(program, "source_sha256_unchanged");
        StringAssert.Contains(program, "source_last_write_time_unchanged");
        Assert.DoesNotContain("HttpClient", program, StringComparison.Ordinal);
        StringAssert.Contains(readme, "never uploads");
        StringAssert.Contains(readme, "reviewed before");
    }

    [TestMethod]
    public void RealFileHarness_ReferencesOnlyProductAndContainsNoRealUserPath()
    {
        var project = XDocument.Load(Full("tools/real-file-validation/PixelTart.RealFileValidation.csproj"));
        var references = project.Descendants("ProjectReference").Select(element => element.Attribute("Include")?.Value).ToArray();
        Assert.HasCount(1, references);
        StringAssert.Contains(references[0]!, "src\\RAWSelectionAssistant\\RAWSelectionAssistant.csproj");
        foreach (var file in Directory.GetFiles(Full("tools/real-file-validation"), "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file) is not (".cs" or ".md" or ".ps1" or ".csproj")) continue;
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("C:\\Users\\Administrator", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DSC09403.ARW", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public void InstalledChecklist_BlocksRcNameUntilAllFourPathsPass()
    {
        var checklist = Text("docs/audit/InstalledGoldenPathChecklist_RC3.md");
        foreach (var path in new[] { "A. Local Split", "B. RAW → JPG", "C. Batch Compress", "D. Collage" })
            StringAssert.Contains(checklist, path);
        Assert.AreEqual(4, Count(checklist, "NOT_RUN"));
        StringAssert.Contains(checklist, "UserVerified=false");
    }

    [TestMethod]
    public void VisibleFeatureAudit_UsesOnlyApprovedReadinessStates()
    {
        var audit = Text("docs/audit/PixelTart_VisibleFeatureAudit_RC3.md");
        StringAssert.Contains(audit, "ProductionReady");
        StringAssert.Contains(audit, "NeedsVerification");
        StringAssert.Contains(audit, "PreviewDisabled");
        StringAssert.Contains(audit, "Hidden");
        StringAssert.Contains(audit, "InstalledUiVerified");
        Assert.DoesNotContain("| Implemented |", audit, StringComparison.Ordinal);
    }

    [TestMethod]
    public void FailurePayload_IsValidJsonAfterProtocolPrefix()
    {
        var detail = new MediaTaskFailureDetail("photo.ARW", MediaTaskStages.RawDecode, "DecodeFailed",
            "无法完成 RAW 解码。", "LibRawCode=-2", true, false);
        var payload = MediaTaskFailurePayload.Serialize(detail);
        var json = payload[(payload.IndexOf(':') + 1)..];
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual("photo.ARW", document.RootElement.GetProperty("FileName").GetString());
    }

    private static int Count(string value, string token) =>
        (value.Length - value.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

    private static string Text(string relative) => File.ReadAllText(Full(relative));
    private static string Full(string relative) => Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

using System.Text.Json;
using System.IO;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class CapabilityConsistencyTests
{
    [TestMethod]
    public void CapabilityDefinitionAndGeneratedArtifactsStayConsistent()
    {
        var root = Root();
        using var definition = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/current/capabilities.definition.json")));
        using var generated = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/current/PIXEL_TART_CURRENT_CAPABILITIES.json")));
        var expected = definition.RootElement.GetProperty("capabilities").EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!, x => x);
        var actual = generated.RootElement.GetProperty("capabilities").EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!, x => x);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var id in expected.Keys)
        {
            foreach (var property in new[] { "module", "implementation_status", "verification_status", "next_gate" })
                Assert.AreEqual(expected[id].GetProperty(property).GetString(), actual[id].GetProperty(property).GetString(), $"Generated capability drift: {id}.{property}");
            foreach (var property in new[] { "evidence", "required_tests", "acceptance_artifacts" })
                CollectionAssert.AreEqual(expected[id].GetProperty(property).EnumerateArray().Select(x => x.GetString()).ToArray(), actual[id].GetProperty(property).EnumerateArray().Select(x => x.GetString()).ToArray(), $"Generated capability drift: {id}.{property}");
        }
        var markdown = File.ReadAllText(Path.Combine(root, "docs/current/PIXEL_TART_CURRENT_STATE.md"));
        foreach (var id in expected.Keys)
        {
            var row = markdown.Split('\n').FirstOrDefault(line => line.StartsWith($"| {id} |", StringComparison.Ordinal));
            Assert.IsNotNull(row, $"Missing generated MD row: {id}");
            StringAssert.Contains(row, $"| {expected[id].GetProperty("implementation_status").GetString()} |", $"MD implementation drift: {id}");
            StringAssert.Contains(row, $"| {expected[id].GetProperty("verification_status").GetString()} |", $"MD verification drift: {id}");
        }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln");
    }
}

using System.IO;
using System.Text.Json;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UiVisualRegressionTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln");
    }

    [TestMethod]
    public void BaselineGuard_RequiresExplicitApproval()
    {
        var root = Path.Combine(Root(), "tests", "UIVisual");
        Directory.CreateDirectory(Path.Combine(root, "Baselines")); Directory.CreateDirectory(Path.Combine(root, "Received")); Directory.CreateDirectory(Path.Combine(root, "Diff")); Directory.CreateDirectory(Path.Combine(root, "Reports"));
        var approved = Environment.GetEnvironmentVariable("PIXEL_TART_APPROVE_VISUAL_BASELINE") == "1";
        var baselineCount = Directory.EnumerateFiles(Path.Combine(root, "Baselines"), "*.png", SearchOption.AllDirectories).Count();
        var status = baselineCount == 0 ? "BASELINE_MISSING" : approved ? "BASELINE_APPROVAL_REQUESTED" : "BASELINE_LOCKED";
        File.WriteAllText(Path.Combine(root, "Reports", "baseline-guard.json"), JsonSerializer.Serialize(new { Status = status, BaselineCount = baselineCount, ApprovalEnvironment = "PIXEL_TART_APPROVE_VISUAL_BASELINE", Approved = approved }, new JsonSerializerOptions { WriteIndented = true }));
        if (baselineCount == 0) Assert.Inconclusive("BASELINE_MISSING: capture current UI before requesting approval.");
        Assert.IsFalse(approved, "Baseline approval is an explicit human action and must not be enabled in the default test run.");
    }

    [TestMethod]
    public void RequiredScreenMatrix_IsDeclaredWithoutPretendingCapture()
    {
        var matrix = new[] { "1180x720", "1600x920", "1920x1080" }
            .SelectMany(size => new[] { "Workbench", "AssetLibrary", "Tether", "Planning", "OnlineSelection", "ColorStudio", "Publishing", "Settings", "ColorStudio-3D-closed", "ColorStudio-3D-open", "ColorStudio-3D-model" }.Select(view => new { View = view, Size = size, Status = "NOT_RUN" }))
            .ToArray();
        var root = Path.Combine(Root(), "docs", "evidence", "ui-guardian"); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "SCREENSHOT_MATRIX.json"), JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = true }));
        Assert.HasCount(33, matrix);
    }
}

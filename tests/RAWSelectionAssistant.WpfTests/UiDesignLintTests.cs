using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class UiDesignLintTests
{
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("RAWSelectionAssistant.sln");
    }

    [TestMethod]
    public void ProductionXaml_HasNoUnapprovedP0Patterns()
    {
        var findings = ScanProductionXaml();
        var p0 = findings.Where(f => f.Severity == "P0").ToArray();
        var reportRoot = Environment.GetEnvironmentVariable("PIXEL_TART_UI_GUARDIAN_REPORT_ROOT")
            ?? Path.Combine(Root(), "docs", "evidence", "ui-guardian");
        Directory.CreateDirectory(reportRoot);
        File.WriteAllText(Path.Combine(reportRoot, "static-xaml-lint.json"), System.Text.Json.JsonSerializer.Serialize(findings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Assert.IsEmpty(p0, "UI Guardian P0 static XAML findings: " + string.Join("; ", p0.Select(f => f.ToString())));
    }

    internal static IReadOnlyList<LintFinding> ScanProductionXaml()
    {
        var root = Root();
        var source = Path.Combine(root, "src");
        var results = new List<LintFinding>();
        foreach (var file in Directory.EnumerateFiles(source, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var isDesignSystem = file.Contains(Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar + "DesignSystem" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                var canvasAllowlisted = file.EndsWith("AssetLibraryPage.xaml", StringComparison.OrdinalIgnoreCase) && line.Contains("AssetSelectionMarqueeLayer", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith("AssetQueryComposerView.xaml", StringComparison.OrdinalIgnoreCase) && line.Contains("ColorPlaneCursor", StringComparison.OrdinalIgnoreCase);
                if (canvasAllowlisted)
                    results.Add(new("EXEMPT", "INTENTIONAL_DRAWING_SURFACE", file, lineNumber, line.Trim()));
                if (!isDesignSystem && !canvasAllowlisted && (line.Contains("Margin=\"-", StringComparison.OrdinalIgnoreCase) || line.Contains("Padding=\"-", StringComparison.OrdinalIgnoreCase)))
                    results.Add(new("P0", "NEGATIVE_SPACING", file, lineNumber, line.Trim()));
                if (line.Contains("ScaleTransform", StringComparison.OrdinalIgnoreCase) && !file.Contains("DesignSystem", StringComparison.OrdinalIgnoreCase))
                    results.Add(new("P1", "PAGE_SCALE_TRANSFORM", file, lineNumber, line.Trim()));
                if (line.Contains("<Canvas", StringComparison.OrdinalIgnoreCase) && !file.EndsWith("MainWindow.xaml", StringComparison.OrdinalIgnoreCase) && !canvasAllowlisted)
                    results.Add(new("P1", "CANVAS_IN_PAGE", file, lineNumber, line.Trim()));
                if (System.Text.RegularExpressions.Regex.IsMatch(line, "FontSize=\\\"(?:[0-9]|1[0-3])(?:\\\"|\\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && !file.Contains("DesignSystem", StringComparison.OrdinalIgnoreCase))
                    results.Add(new("P2", "HARDCODED_SMALL_FONTSIZE", file, lineNumber, line.Trim()));
                if (System.Text.RegularExpressions.Regex.IsMatch(line, "(?:Background|Foreground|BorderBrush)=\\\"#[0-9A-Fa-f]{6,8}\\\""))
                    results.Add(new("P2", "HARDCODED_UI_COLOR", file, lineNumber, line.Trim()));
            }
        }
        return results;
    }

    internal sealed record LintFinding(string Severity, string Kind, string File, int Line, string Source)
    {
        public override string ToString() => $"{Severity} {Kind} {Path.GetFileName(File)}:{Line}";
    }
}

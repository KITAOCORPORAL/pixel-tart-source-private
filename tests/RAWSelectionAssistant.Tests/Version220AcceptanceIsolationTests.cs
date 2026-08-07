namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220AcceptanceIsolationTests
{
    private static readonly string Root = FindRepositoryRoot();

    [TestMethod]
    public void AcceptanceExecutable_UsesExplicitIsolatedAppDataRootOnly()
    {
        var paths = File.ReadAllText(Path.Combine(Root, "src", "RAWSelectionAssistant.Core", "Utilities", "AppDataPaths.cs"));
        StringAssert.Contains(paths, "IsAcceptanceBuild");
        StringAssert.Contains(paths, "PIXEL_TART_ACCEPTANCE_ROOT");
        StringAssert.Contains(paths, "Path.IsPathFullyQualified");
        StringAssert.Contains(paths, "Path.GetFullPath(RootOverride)");

        var worker = File.ReadAllText(Path.Combine(Root, "tools", "ReleaseSmoke", "Invoke-IsolatedDesktopWorker.ps1"));
        StringAssert.Contains(worker, "$env:PIXEL_TART_ACCEPTANCE_ROOT = $acceptanceSettingsRoot");
        StringAssert.Contains(worker, "Acceptance settings escaped the isolated LocalAppData root.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

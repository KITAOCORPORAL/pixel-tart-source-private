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
        StringAssert.Contains(paths, "Acceptance builds require an explicit PIXEL_TART_ACCEPTANCE_ROOT.");

        var worker = File.ReadAllText(Path.Combine(Root, "tools", "ReleaseSmoke", "Invoke-IsolatedDesktopWorker.ps1"));
        StringAssert.Contains(worker, "$env:PIXEL_TART_ACCEPTANCE_ROOT = $acceptanceSettingsRoot");
        StringAssert.Contains(worker, "Acceptance settings escaped the isolated LocalAppData root.");
    }

    [TestMethod]
    public void InputRoutingInstaller_CannotLaunchWithoutIsolationOrDeleteProductionData()
    {
        var installer = File.ReadAllText(Path.Combine(Root, "installer", "RAWSelectionAssistant.iss"));
        StringAssert.Contains(installer, "#ifndef InputRoutingHotfixDevValidation");
        StringAssert.Contains(installer, "Filename: \"{app}\\{#MyAppExeName}\"");
        StringAssert.Contains(installer, "DeleteUserDataCheckBox := nil;");
        StringAssert.Contains(installer, "DelTree(ExpandConstant('{localappdata}\\KitaoPhotoSelector')");

        var inputDefinition = installer.IndexOf("#ifdef InputRoutingHotfixDevValidation", StringComparison.Ordinal);
        var runSection = installer.IndexOf("[Run]", StringComparison.Ordinal);
        var guardedRun = installer.IndexOf("#ifndef InputRoutingHotfixDevValidation", runSection, StringComparison.Ordinal);
        var runEntry = installer.IndexOf("Filename: \"{app}\\{#MyAppExeName}\"", guardedRun, StringComparison.Ordinal);
        var guardEnd = installer.IndexOf("#endif", runEntry, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, inputDefinition);
        Assert.IsTrue(runSection < guardedRun && guardedRun < runEntry && runEntry < guardEnd,
            "The InputRouting installer must not offer an unisolated post-install launch.");

        var deleteData = installer.IndexOf("DelTree(ExpandConstant('{localappdata}\\KitaoPhotoSelector')", StringComparison.Ordinal);
        var deleteGuard = installer.LastIndexOf("#ifndef InputRoutingHotfixDevValidation", deleteData, StringComparison.Ordinal);
        var deleteGuardEnd = installer.IndexOf("#endif", deleteData, StringComparison.Ordinal);
        Assert.IsTrue(deleteGuard >= 0 && deleteGuard < deleteData && deleteData < deleteGuardEnd,
            "The InputRouting uninstaller must never delete the production LocalAppData root.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

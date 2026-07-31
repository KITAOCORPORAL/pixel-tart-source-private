using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class UiFix141Tests
{
    [TestMethod]
    public void MenuStyles_ProvideHighDpiSafeMetrics()
    {
        var text = Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Menu.xaml");
        Contains(text, "Height\" Value=\"36", "Padding\" Value=\"14,7", "MinWidth\" Value=\"76", "UseLayoutRounding", "SnapsToDevicePixels", "VerticalContentAlignment");
    }

    [TestMethod]
    public void Sidebar_UsesPackagedVectorGeometriesInsteadOfFontGlyphs()
    {
        var main = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Contains(main, "IconWorkbench", "IconLocalSplit", "IconWorkspace", "IconHistory", "IconLicense", "IconSettings", "IconHelp", "IconCollapse");
        Assert.IsFalse(main.Contains("Segoe Fluent Icons", StringComparison.Ordinal));
        Assert.IsFalse(main.Contains("&#xE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod] public void NavigationIcons_AreThemeAwareLinearPaths() => Contains(Text("src/RAWSelectionAssistant/Resources/DesignSystem/Controls.Navigation.xaml"), "SidebarIcon", "StrokeThickness\" Value=\"1.6", "DynamicResource TextSecondaryBrush");
    [TestMethod] public void NavigationIcons_AreMergedIntoApplication() => Contains(Text("src/RAWSelectionAssistant/App.xaml"), "Icons.Navigation.xaml");
    [TestMethod] public void SidebarButtons_HaveTooltipsAndAutomationNames() => Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml"), "ToolTip=\"工作台\" AutomationProperties.Name=\"工作台\"", "ToolTip=\"本地分片\" AutomationProperties.Name=\"本地分片\"", "ToolTip=\"归片工作区\" AutomationProperties.Name=\"归片工作区\"", "ToolTip=\"帮助\" AutomationProperties.Name=\"帮助\"");

    [TestMethod]
    public void GlobalAppBar_IsRemovedWithoutRestoringBrokenSquareIcons()
    {
        var text = Text("src/RAWSelectionAssistant/MainWindow.xaml");
        Assert.IsFalse(text.Contains("IconButton", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("TopBarBadge", StringComparison.Ordinal));
        Contains(text, "EditionStatusArea", "SidebarEditionCard", "TaskCenterPanel", "CancelButton");
    }

    [TestMethod] public void EditionAndTaskActions_ArePlacedInTheirOwnContexts() => Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml"), "x:Name=\"EditionStatusArea\"", "x:Name=\"TaskCenterPanel\"", "x:Name=\"CancelButton\" Content=\"取消当前任务\"");
    [TestMethod] public void MainOperationArea_UsesTaskSemanticModules() => Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml"), "项目概览", "处理任务", "最近项目", "任务中心");

    [TestMethod]
    public async Task LegacySettings_DefaultReportExportIsOff()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        await File.WriteAllTextAsync(path, "{}");
        var settings = await new SettingsService(new TestLogService(), path).LoadAsync();
        Assert.IsFalse(settings.ReportSettings.DefaultExportEnabled);
        Assert.IsTrue(settings.ReportSettings.DefaultExportCsv);
        Assert.IsFalse(settings.ReportSettings.DefaultExportJson);
        Assert.IsFalse(settings.ReportSettings.DefaultExportLog);
    }

    [TestMethod]
    public async Task ReportSettings_RoundTripWithCamelCaseStructure()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("settings.json");
        var service = new SettingsService(new TestLogService(), path);
        await service.SaveAsync(new AppSettings { ReportSettings = new ReportSettings { DefaultExportEnabled = true, DefaultExportCsv = true, DefaultExportJson = true, DefaultExportLog = true } });
        var json = await File.ReadAllTextAsync(path);
        Contains(json, "\"reportSettings\"", "\"defaultExportEnabled\": true", "\"defaultExportJson\": true");
        Assert.IsTrue((await service.LoadAsync()).ReportSettings.DefaultExportLog);
    }

    [TestMethod]
    public async Task ReportService_CanExportCsvOnly()
    {
        using var temp = new TempDirectory();
        await new MediaReportService(new TestLogService()).ExportAsync(temp.Path, CollectionCategory.JpegOnly, [], default, new ReportExportOptions(true, false, false));
        Assert.IsTrue(File.Exists(temp.Combine("匹配报告.csv")));
        Assert.IsFalse(File.Exists(temp.Combine("匹配报告.json")));
        Assert.IsFalse(File.Exists(temp.Combine("操作日志.txt")));
    }

    [TestMethod]
    public async Task ReportService_CanSelectJsonWithoutLog()
    {
        using var temp = new TempDirectory();
        await new MediaReportService(new TestLogService()).ExportAsync(temp.Path, CollectionCategory.JpegOnly, [], default, new ReportExportOptions(false, true, false));
        Assert.IsFalse(File.Exists(temp.Combine("匹配报告.csv")));
        Assert.IsTrue(File.Exists(temp.Combine("匹配报告.json")));
        Assert.IsFalse(File.Exists(temp.Combine("操作日志.txt")));
    }

    [TestMethod] public void OutputPage_HasPerProjectReportControls() => Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml"), "ExportReportsForCurrentProject", "ExportCsvForCurrentProject", "ExportJsonForCurrentProject", "ExportLogForCurrentProject");
    [TestMethod] public void SettingsPage_HasReportDefaults() => Contains(Text("src/RAWSelectionAssistant/MainWindow.xaml"), "输出与报告", "DefaultExportReports", "DefaultExportCsv", "DefaultExportJson", "DefaultExportLog");
    [TestMethod] public void CopyOnlyAutoExportsWhenCurrentProjectSwitchEnabled() => Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "IsOnboardingActive || ExportReportsForCurrentProject", "CreateReportExportOptions()");
    [TestMethod] public void FreeEditionStillFallsBackToCsv() => Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "if (!CanExportAdvancedReports) return ReportExportOptions.Free");
    [TestMethod] public void ReleaseProviderRemainsNone() => Contains(Text("src/RAWSelectionAssistant/appsettings.license.json"), "\"Provider\": \"None\"");

    private static void Contains(string text, params string[] values)
    {
        foreach (var value in values) StringAssert.Contains(text, value);
    }

    private static string Text(string relativePath) => File.ReadAllText(Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}

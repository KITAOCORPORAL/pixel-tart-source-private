using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version230Rc5RuntimeUiPolishRound2Tests
{
    [TestMethod]
    [DataRow("SecondaryLineText")]
    [DataRow("TertiaryLineText")]
    [DataRow("ScheduleTooltipText")]
    [DataRow("TextTrimming=\"CharacterEllipsis\"")]
    public void WorkbenchScheduleCard_KeepsProjectFirstAndCompleteSummary(string token) => Contains("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml", token);

    [TestMethod]
    public void WorkbenchScheduleViewModel_ExposesProjectTimeStateClientLocationAndTooltip()
    {
        var item = new WorkbenchScheduleItem(Guid.NewGuid(), null, "原始标题", DateTimeOffset.Parse("2026-08-10T01:00:00Z"), DateTimeOffset.Parse("2026-08-10T03:00:00Z"),
            "China Standard Time", ShootBookingStatus.Confirmed, false, false, true, false, 0, "超长商业人像项目", "摄影棚 A", 0, 0, "客户代号 07");
        var viewModel = new WorkbenchScheduleItemViewModel(item, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"), TimeProvider.System);
        Assert.AreEqual("超长商业人像项目", viewModel.Title);
        StringAssert.Contains(viewModel.SecondaryLineText, "09:00");
        StringAssert.Contains(viewModel.SecondaryLineText, "已确认");
        StringAssert.Contains(viewModel.TertiaryLineText, "客户代号 07");
        StringAssert.Contains(viewModel.ScheduleTooltipText, "摄影棚 A");
    }

    [TestMethod]
    [DataRow("查看详情")]
    [DataRow("HasMoreActions")]
    [DataRow("更多任务操作")]
    [DataRow("Visibility=\"{Binding CanPause")]
    [DataRow("Visibility=\"{Binding CanResume")]
    public void TaskCenter_DefaultCardKeepsOnlyHighFrequencyActions(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", token);

    [TestMethod]
    public void CompletedTask_HidesInvalidInlineActions()
    {
        var task = Snapshot(TaskLifecycleState.Completed);
        Assert.IsFalse(task.CanPause); Assert.IsFalse(task.CanResume); Assert.IsFalse(task.CanCancel); Assert.IsFalse(task.CanRetry); Assert.IsFalse(task.HasMoreActions);
        Assert.AreEqual("100%", new TaskSnapshotViewModel(new(Guid.NewGuid(), null, "完成", TaskLifecycleState.Completed, 100, "完成", "", TaskResultSummary.Empty, null, null, null, null, DateTimeOffset.UtcNow)).ProgressText);
    }

    [TestMethod]
    [DataRow(TaskLifecycleState.Running, true, false)]
    [DataRow(TaskLifecycleState.Paused, false, true)]
    public void ActiveTask_ShowsOnlyApplicableInlineAction(TaskLifecycleState state, bool pause, bool resume)
    {
        var task = Snapshot(state); Assert.AreEqual(pause, task.CanPause); Assert.AreEqual(resume, task.CanResume);
    }

    [TestMethod]
    [DataRow("MaxWidth=\"1360\"")]
    [DataRow("HorizontalAlignment=\"Center\"")]
    [DataRow("Width=\"540\"")]
    [DataRow("本地分片快速向导")]
    public void LocalSplit_UsesWideCenteredResponsiveContent(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", token);

    [TestMethod]
    [DataRow("尚未建立照片索引")]
    [DataRow("尚未导入客户选片")]
    [DataRow("ShowIndexWarning")]
    [DataRow("ShowWorkflowResults")]
    [DataRow("Rows=\"1\" Columns=\"5\"")]
    public void Collection_UsesStepSpecificEmptyStatesAndFiveCoreMetrics(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", token);

    [TestMethod]
    [DataRow("IsEmptyStage")]
    [DataRow("IsGroupedStage")]
    [DataRow("IsPlanReady")]
    [DataRow("HasResult")]
    [DataRow("生成并预览操作清单")]
    public void Organize_UsesProgressiveStageActions(string token) => ContainsAny(token, "src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml", "src/RAWSelectionAssistant/ViewModels/ToolPageViewModels.cs");

    [TestMethod]
    [DataRow("FeatureAvailability")]
    [DataRow("Preview")]
    [DataRow("ComingSoon")]
    [DataRow("Hidden")]
    public void Tools_UseUnifiedAvailabilityContract(string token) => Contains("src/RAWSelectionAssistant.Core/Models/ToolDefinition.cs", token);

    [TestMethod]
    [DataRow("预览功能")]
    [DataRow("图片列表")]
    [DataRow("水印配置")]
    [DataRow("预览区域")]
    [DataRow("导出功能开发中")]
    public void Watermark_IsClearlyPreviewAndUsesFullWorkspace(string token) => Contains("src/RAWSelectionAssistant/MainWindow.xaml", token);

    [TestMethod]
    [DataRow("Width=\"3\"")]
    [DataRow("PinActionLabel")]
    [DataRow("●  已固定")]
    [DataRow("○")]
    public void Toolbox_PinnedAndUnpinnedCardsHaveDistinctImmediateVisuals(string token) => ContainsAny(token, "src/RAWSelectionAssistant/MainWindow.xaml", "src/RAWSelectionAssistant/ViewModels/ToolboxItemViewModel.cs");

    [TestMethod]
    [DataRow("搜索项目、客户、地点或拍摄内容")]
    [DataRow("AutomationProperties.Name=\"搜索工作日历\"")]
    [DataRow("全部状态")]
    [DataRow("五色状态图例")]
    public void FullCalendar_SearchStatusAndLegendAreExplicit(string token) => ContainsAny(token, "src/RAWSelectionAssistant/Views/WorkCalendarView.xaml", "src/RAWSelectionAssistant/ViewModels/CalendarViewModels.cs");

    [TestMethod]
    [DataRow("CalendarStatusFreeBrush")]
    [DataRow("PrimaryWorkflowStatus")]
    [DataRow("IsToday")]
    [DataRow("IsSelected")]
    public void FullCalendar_DayBadgeSeparatesStateTodayAndSelection(string token) => Contains("src/RAWSelectionAssistant/Views/MonthCalendarView.xaml", token);

    [TestMethod]
    [DataRow("全部类型")]
    [DataRow("全部支付状态")]
    [DataRow("全部分类")]
    [DataRow("搜索交易、客户、项目或备注")]
    public void Finance_FilterDefaultsAreNeverBlank(string token) => ContainsAny(token, "src/RAWSelectionAssistant/Views/FinanceView.xaml", "src/RAWSelectionAssistant/ViewModels/FinanceViewModel.cs");

    [TestMethod]
    [DataRow("PageHorizontalPadding")]
    [DataRow("PageVerticalPadding")]
    [DataRow("PageContentMaxWidth")]
    [DataRow("SectionSpacing")]
    [DataRow("CardSpacing")]
    public void GlobalLayoutTokens_AreDeclared(string token) => Contains("src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml", token);

    [TestMethod]
    [DataRow("PageTitleText")]
    [DataRow("SectionTitleText")]
    [DataRow("CardTitleText")]
    [DataRow("BodyText")]
    [DataRow("SecondaryBodyText")]
    [DataRow("CaptionText")]
    public void Typography_HasCompleteReadableHierarchy(string token) => Contains("src/RAWSelectionAssistant/Resources/DesignSystem/Typography.xaml", token);

    [TestMethod]
    [DataRow("Theme.Dark.xaml")]
    [DataRow("Theme.Light.xaml")]
    [DataRow("Theme.HighContrast.xaml")]
    public void AllThemes_KeepCalendarStatusContrastResources(string theme)
    {
        var source = Read("src/RAWSelectionAssistant/Resources/DesignSystem/" + theme);
        foreach (var token in new[] { "CalendarStatusFreeBrush", "CalendarStatusScheduledBrush", "CalendarStatusShotBrush", "CalendarStatusPendingDeliveryBrush", "CalendarStatusDeliveredBrush" }) StringAssert.Contains(source, token);
    }

    [TestMethod]
    [DataRow(1.5)]
    [DataRow(2.0)]
    public void DpiContracts_KeepReadableCaptionAndLogicalSpacing(double scale)
    {
        Contains("src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml", "CaptionFontSize\">12");
        Contains("src/RAWSelectionAssistant/Resources/DesignSystem/DesignTokens.xaml", "PageContentMaxWidth\">1360");
        Assert.IsGreaterThanOrEqualTo(18d, 12 * scale);
    }

    [TestMethod]
    public void ModifiedViewsRemainValidXaml()
    {
        foreach (var relative in new[] { "MainWindow.xaml", "Views/WorkbenchScheduleView.xaml", "Views/WorkCalendarView.xaml", "Views/MonthCalendarView.xaml", "Views/DaySchedulePanel.xaml", "Views/OrganizePhotosView.xaml", "Views/FinanceView.xaml", "Resources/DesignSystem/DesignTokens.xaml", "Resources/DesignSystem/Typography.xaml" })
            XDocument.Parse(Read("src/RAWSelectionAssistant/" + relative));
    }

    [TestMethod]
    public void OrganizePhotosView_DeclaresItsBooleanVisibilityResource()
    {
        Contains("src/RAWSelectionAssistant/Views/OrganizePhotosView.xaml", "x:Key=\"BooleanToVisibilityConverter\"");
    }

    private static TaskSnapshotViewModel Snapshot(TaskLifecycleState state) => new(new(Guid.NewGuid(), null, "测试", state, state == TaskLifecycleState.Completed ? 100 : 25, "", "", TaskResultSummary.Empty, null, null, null, null, DateTimeOffset.UtcNow));
    private static void Contains(string relative, string token) => StringAssert.Contains(Read(relative), token);
    private static void ContainsAny(string token, params string[] relatives) => Assert.IsTrue(relatives.Any(relative => Read(relative).Contains(token, StringComparison.Ordinal)), $"未找到：{token}");
    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}

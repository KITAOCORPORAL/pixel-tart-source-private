using System.IO;
using System.Xml.Linq;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class Version220StageDUiTests
{
    [TestMethod] public void ReminderPanel_DeclaresDefaultOffMessageAndAllActions()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/BookingRemindersPanel.xaml");
        foreach (var value in new[] { "新提醒默认关闭", "SaveButtonText", "编辑", "启用/关闭", "移除", "自定义提醒时间" }) StringAssert.Contains(xaml, value);
    }

    [TestMethod] public void ReminderPanel_IsEmbeddedInSharedBookingDetails()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml");
        StringAssert.Contains(xaml, "BookingRemindersPanel");
        StringAssert.Contains(xaml, "HasRemindersPanel");
    }

    [TestMethod] public void Workbench_HasTodayFutureSevenAndOpenDetailsRoute()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml");
        foreach (var value in new[] { "TodayCountText", "FutureCountText", "OpenBookingCommand", "从工作台打开拍摄排期详情" }) StringAssert.Contains(xaml, value);
        StringAssert.Contains(Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs"), "WorkbenchSchedule.OpenBookingRequested");
    }

    [TestMethod] public void WorkbenchSchedule_IsIndependentFromMainViewModelBusinessLogic()
    {
        var main = Text("src/RAWSelectionAssistant/ViewModels/MainViewModel.cs");
        var schedule = Text("src/RAWSelectionAssistant/ViewModels/StageDViewModels.cs");
        StringAssert.Contains(main, "WorkbenchScheduleViewModel? WorkbenchSchedule");
        StringAssert.Contains(schedule, "IWorkbenchScheduleService");
        Assert.IsFalse(main.Contains("QueryCurrentViewAsync(new ShootBookingQuery", StringComparison.Ordinal));
    }

    [TestMethod] public void StageDViewModels_DoNotAccessSqliteOrDirectFileOperations()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/StageDViewModels.cs");
        foreach (var forbidden in new[] { "Microsoft.Data.Sqlite", "SqliteConnection", "File.Copy", "File.Move", "File.Delete" }) Assert.IsFalse(source.Contains(forbidden, StringComparison.Ordinal), forbidden);
    }

    [TestMethod] public void ReminderNotifications_ExposeOpenLaterAndAcknowledgeWithAccessibilityNames()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/ReminderNotificationHost.xaml");
        foreach (var value in new[] { "稍后查看", "知道了", "打开排期", "AutomationProperties.Name" }) StringAssert.Contains(xaml, value);
    }

    [TestMethod] public void StageDViews_UseDynamicThemesAndNoScaleTransforms()
    {
        var text = string.Join("\n", new[] { "BookingRemindersPanel.xaml", "WorkbenchScheduleView.xaml", "ReminderNotificationHost.xaml" }.Select(file => Text($"src/RAWSelectionAssistant/Views/{file}")));
        StringAssert.Contains(text, "DynamicResource");
        Assert.IsFalse(text.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("LayoutTransform", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("#FFFFFF", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod] public void StageDXaml_IsWellFormed()
    {
        foreach (var file in new[] { "BookingRemindersPanel.xaml", "WorkbenchScheduleView.xaml", "ReminderNotificationHost.xaml" }) XDocument.Parse(Text($"src/RAWSelectionAssistant/Views/{file}"));
    }

    [TestMethod] public void ReminderAndWorkbench_DeclareKeyboardFocusableButtonsAndLiveStatus()
    {
        var text = Text("src/RAWSelectionAssistant/Views/BookingRemindersPanel.xaml") + Text("src/RAWSelectionAssistant/Views/WorkbenchScheduleView.xaml");
        StringAssert.Contains(text, "AutomationProperties.LiveSetting=\"Polite\"");
        StringAssert.Contains(text, "Command=\"{Binding");
        Assert.IsFalse(text.Contains("Focusable=\"False\"", StringComparison.Ordinal));
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

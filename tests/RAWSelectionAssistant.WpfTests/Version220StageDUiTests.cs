using System.IO;
using System.Xml.Linq;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.ViewModels;

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

    [TestMethod]
    public async Task PersistedUnreadReminder_IsRestoredIntoNotificationUiAfterRestart()
    {
        var bookingId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();
        var notification = new NotificationMessage(Guid.NewGuid(), NotificationType.Toast, NotificationSeverity.Information,
            "拍摄排期提醒", "已脱敏", bookingId, null, [], false, DateTimeOffset.UtcNow, DeduplicationKey: $"booking-reminder:{reminderId:D}");
        var center = new PersistedNotificationCenter(notification);
        var viewModel = new ReminderNotificationCenterViewModel(new SilentReminderPublisher(), center, new SilentReminderService());

        await viewModel.InitializeAsync();

        Assert.HasCount(1, viewModel.Items);
        Assert.AreEqual(bookingId, viewModel.Items[0].BookingId);
        Assert.AreEqual(reminderId, viewModel.Items[0].ReminderId);
        Assert.AreEqual(notification.Id, viewModel.Items[0].NotificationId);
    }

    private static string Text(string relative) => File.ReadAllText(Path.Combine(Root(), relative.Replace('/', Path.DirectorySeparatorChar)));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private sealed class PersistedNotificationCenter(NotificationMessage notification) : INotificationCenter
    {
        public event EventHandler<NotificationMessage>? Published;
        public Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken = default) { Published?.Invoke(this, message); return Task.CompletedTask; }
        public void NotifyPersisted(NotificationMessage message) => Published?.Invoke(this, message);
        public Task<IReadOnlyList<NotificationMessage>> GetHistoryAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NotificationMessage>>([notification]);
        public Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SilentReminderPublisher : IBookingReminderNotificationService
    {
        public event EventHandler<ReminderPublishedEvent>? ReminderPublished { add { } remove { } }
        public NotificationMessage CreateNotification(ReminderDispatch dispatch) => throw new NotSupportedException();
        public Task PublishAsync(ReminderDispatch dispatch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PublishPersisted(ReminderDispatch dispatch, NotificationMessage notification) { }
    }

    private sealed class SilentReminderService : IBookingReminderService
    {
        public Task<IReadOnlyList<ReminderDefinition>> ListAsync(Guid bookingId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ReminderDefinition>>([]);
        public Task<ReminderDefinition> SaveAsync(ReminderDefinition reminder, CancellationToken cancellationToken = default) => Task.FromResult(reminder);
        public Task<bool> SetEnabledAsync(Guid reminderId, bool enabled, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DeleteAsync(Guid reminderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DismissAsync(Guid reminderId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}

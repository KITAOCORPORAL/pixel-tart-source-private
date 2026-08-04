using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;
using RAWSelectionAssistant.Core.Services.Database;

#pragma warning disable MSTEST0037

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version210NotificationAuditTests
{
    [TestMethod]
    public async Task NotificationCenter_PersistsHistory()
    {
        using var setup=await SetupAsync();var message=new NotificationMessage(Guid.NewGuid(),NotificationType.Toast,NotificationSeverity.Information,"标题","内容",null,null,[],false,DateTimeOffset.UtcNow);await setup.Center.PublishAsync(message);var history=await setup.Center.GetHistoryAsync();Assert.AreEqual(message.Id,history.Single().Id);
    }

    [TestMethod]
    public async Task NotificationCenter_ThrottlesDuplicateMessages()
    {
        using var setup=await SetupAsync(TimeSpan.FromMinutes(1));for(var i=0;i<100;i++)await setup.Center.PublishAsync(new NotificationMessage(Guid.NewGuid(),NotificationType.Toast,NotificationSeverity.Information,"扫描","进度",null,null,[],false,DateTimeOffset.UtcNow,DeduplicationKey:"scan"));Assert.AreEqual(1,(await setup.Center.GetHistoryAsync()).Count);
    }

    [TestMethod]
    public async Task NotificationCenter_MarksMessagesRead()
    {
        using var setup=await SetupAsync();var message=new NotificationMessage(Guid.NewGuid(),NotificationType.TaskNotification,NotificationSeverity.Warning,"部分完成","查看失败项",null,null,[],false,DateTimeOffset.UtcNow);await setup.Center.PublishAsync(message);await setup.Center.MarkReadAsync(message.Id);Assert.IsTrue((await setup.Center.GetHistoryAsync()).Single().IsRead);
    }

    [TestMethod]
    public void AuditSanitizer_HidesFullPathsAndSecrets()
    {
        var value=AuditLogService.Sanitize(@"copy C:\Customers\Alice\portrait.jpg token=abcdef license:123456");Assert.IsFalse(value.Contains("Customers",StringComparison.OrdinalIgnoreCase));Assert.IsFalse(value.Contains("abcdef",StringComparison.Ordinal));Assert.IsTrue(value.Contains("portrait.jpg",StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AuditLog_PersistsSanitizedMessage()
    {
        using var setup=await SetupAsync();await setup.Audit.WriteAsync("File","Copy","Information",@"C:\Private\Client\a.jpg secret=abc");await using var connection=await setup.Database.OpenConnectionAsync();await using var command=connection.CreateCommand();command.CommandText="SELECT SanitizedMessage FROM AuditLogs LIMIT 1;";var value=(string)(await command.ExecuteScalarAsync())!;Assert.IsFalse(value.Contains("Private",StringComparison.Ordinal));Assert.IsFalse(value.Contains("abc",StringComparison.Ordinal));
    }

    [TestMethod]
    public void ErrorCatalog_ProvidesActionableDescriptions()
    {
        Assert.IsTrue(ErrorCodeCatalog.Describe(ErrorCodeCatalog.HashMismatch).Contains("源文件保持不变",StringComparison.Ordinal));Assert.IsTrue(ErrorCodeCatalog.Describe(ErrorCodeCatalog.DiskSpaceInsufficient).Contains("空间不足",StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReminderScheduler_IsDisabledByDefault()
    {
        var scheduler=new DisabledLocalReminderScheduler();Assert.IsFalse(scheduler.IsEnabled);scheduler.ScheduleAsync(new ReminderDefinition(Guid.NewGuid(),null,"future","reserved",new(ReminderTriggerKind.AbsoluteTime,DateTimeOffset.UtcNow,null))).GetAwaiter().GetResult();
    }

    private static async Task<Setup> SetupAsync(TimeSpan? throttle=null){var temp=new TempDirectory();var db=new PixelTartDatabase(temp.Combine("db.sqlite"));await new DatabaseMigrator(db,new DatabaseBackupService(db,temp.Combine("backups"))).MigrateAsync();return new(temp,db,new NotificationCenter(db,throttle??TimeSpan.Zero),new AuditLogService(db));}
    private sealed class Setup(TempDirectory temp,PixelTartDatabase database,NotificationCenter center,AuditLogService audit):IDisposable{public PixelTartDatabase Database{get;}=database;public NotificationCenter Center{get;}=center;public AuditLogService Audit{get;}=audit;public void Dispose()=>temp.Dispose();}
}

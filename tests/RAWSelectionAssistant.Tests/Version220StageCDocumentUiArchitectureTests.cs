using System.Xml.Linq;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class Version220StageCDocumentUiArchitectureTests
{
    private static readonly string Root = FindRoot();

    [TestMethod] public void BookingDocumentsPanelExistsInsideBookingDetails()
    {
        Assert.IsTrue(File.Exists(Path("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml")));
        Contains(Text("src/RAWSelectionAssistant/Views/ShootBookingDetailsView.xaml"), "<views:BookingDocumentsPanel", "DataContext=\"{Binding Documents}\"");
        Contains(Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs"), "class BookingDocumentsViewModel");
    }

    [TestMethod] public void PanelListsMetadataAndAllRequiredActions()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml");
        foreach (var value in new[] { "DocumentTypeText", "DisplayName", "ExtensionText", "StateText", "LinkModeText", "AddedText", "VerifiedText", "添加文件（仅关联）", "复制到项目资料目录并关联", "打开", "打开所在位置", "检查", "重新定位", "移除关联" }) Contains(xaml, value);
    }

    [TestMethod] public void DragChoiceDefaultsToReferenceAndNeverAutoCopies()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/DocumentDropChoiceWindow.xaml");
        Contains(xaml, "仅关联原位置（默认）", "IsDefault=\"True\"", "复制到项目资料目录并关联", "取消");
        var code = Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml.cs");
        Contains(code, "DocumentDropChoiceWindow", "HandleDroppedFilesAsync");
        DoesNotContain(code, "File.Copy", "File.Move", "CopyAndAssociateAsync(");
    }

    [TestMethod] public void FolderDropIsRejectedWithoutEnumeration()
    {
        var code = Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml.cs") + Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        Contains(code, "paths.Any(Directory.Exists)", "当前版本只支持添加单个或多个文件");
        DoesNotContain(code, "SearchOption.AllDirectories", "EnumerateFiles");
    }

    [TestMethod] public void AllDocumentCategoriesAreAvailable()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        foreach (var category in new[] { "摄影策划", "拍摄协议", "模特授权书", "报价单", "场地资料", "服装参考", "灯光图", "其他" }) Contains(source, category);
    }

    [TestMethod] public void SupportedExtensionsMatchStageCScope()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs");
        foreach (var extension in new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".jpg", ".jpeg", ".png" }) Contains(source, $"\"{extension}\"");
        DoesNotContain(source, ".raw", ".arw", ".exe");
    }

    [TestMethod] public void WorkflowReusesAllExistingFileSafetyInfrastructure()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs") + Text("src/RAWSelectionAssistant/Services/ApplicationCompositionRoot.cs");
        foreach (var value in new[] { "TaskOperationBridge", "IFileOperationPlanner", "IFileOperationExecutor", "IFileVerificationService", "IUndoJournalService", "FileOperationType.Copy", "FileConflictPolicy.AutoNumber", "PartiallyCompleted", "ErrorCodeCatalog", "AuditLog", "NotificationCenter", "TaskEngine" }) Contains(source, value);
        DoesNotContain(source, "File.Copy", "File.Move");
    }

    [TestMethod] public void ExistingExecutorProvidesCreateNewFlushAndVerification()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/FileOperations/FileOperationExecutor.cs");
        Contains(source, "FileMode.CreateNew", "FlushAsync", "Flush(true)", "verification.VerifyAsync", "UndoJournalEntry", "DeleteCreatedOutput");
        DoesNotContain(source, "FileMode.Create,", "overwrite: true");
    }

    [TestMethod] public void CopyAssociationOrderIsOutputThenDatabaseRelation()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs");
        var execute = source.IndexOf("executor.ExecuteAsync", StringComparison.Ordinal);
        var add = source.IndexOf("repository.AddAsync(document", execute, StringComparison.Ordinal);
        Assert.IsGreaterThan(execute, add);
        Contains(source, "PendingDocumentAssociation", "DatabaseUnavailable", "PartiallyCompleted", "RetryAssociationAsync", "UndoCopiedFileAsync", "AbandonAssociationAsync");
    }

    [TestMethod] public void RemoveAssociationWarningIsFixedAndNoDeleteFileCallExists()
    {
        var vm = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        Contains(vm, "仅从当前拍摄中移除关联，不会删除电脑中的原文件。");
        DoesNotContain(vm, "File.Delete", "Directory.Delete", "Recycle");
        var workflow = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs");
        DoesNotContain(workflow, "File.Delete", "Directory.Delete");
    }

    [TestMethod] public void FullPathIsHiddenUntilUserExpandsIt()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml");
        Contains(xaml, "PathActionText", "FullPath", "Visibility=\"{Binding IsPathExpanded");
        var viewModel = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        Contains(viewModel, "显示完整路径", "隐藏完整路径");
    }

    [TestMethod] public void ViewModelDoesNotCopyFilesOrAccessSqlite()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs");
        Contains(source, "IBookingDocumentWorkflowService");
        DoesNotContain(source, "Microsoft.Data.Sqlite", "SqliteConnection", "File.Copy", "File.Move", "File.Delete");
    }

    [TestMethod] public void DocumentContentIsNeverParsedOrIndexed()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs") + Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentService.cs");
        DoesNotContain(source, "ReadAllText", "ReadAllLines", "StreamReader", "Pdf", "OpenXml", "DocumentFormat.OpenXml", "IndexContent", "ExtractText");
    }

    [TestMethod] public void AuditMessagesContainOnlyIdentifiersAndResultFields()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs");
        Contains(source, "BookingId={bookingId:D};DocumentType={type};Operation={operation};Result={result}");
        var auditMethod = Slice(source, "private Task WriteAuditAsync", "}", includeEnd: true);
        DoesNotContain(auditMethod, "FilePath", "DisplayName", "OptionalHash", "ClientDisplayName", "ContactPhone");
    }

    [TestMethod] public void PrivacySanitizerHidesFullPathFileNameAndHash()
    {
        var source = Text("src/RAWSelectionAssistant.Core/Services/AuditAndNotificationServices.cs");
        Contains(source, "[路径已隐藏]", "filename|documentname|optionalhash|documenthash", "[哈希已隐藏]");
        DoesNotContain(source, "Path.GetFileName(match.Value");
    }

    [TestMethod] public void PanelSupportsThemesDpiScrollingAndAccessibility()
    {
        var xaml = Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml");
        Contains(xaml, "DynamicResource", "VerticalScrollBarVisibility=\"Auto\"", "TextTrimming=\"CharacterEllipsis\"", "AutomationProperties.Name", "AutomationProperties.HelpText", "WrapPanel");
        DoesNotContain(xaml, "ScaleTransform", "LayoutTransform", "#FFFFFF");
        XDocument.Parse(xaml);
        XDocument.Parse(Text("src/RAWSelectionAssistant/Views/DocumentDropChoiceWindow.xaml"));
    }

    [TestMethod] public void SchemaAndMigrationsRemainUnchangedAtVersionTwo()
    {
        var migration = Text("src/RAWSelectionAssistant.Core/Services/Database/CalendarSchemaMigration.cs");
        Contains(migration, "Version => 2");
        Assert.AreEqual(4, Count(migration, "CREATE TABLE"));
        DoesNotContain(migration, "ProjectRelationships");
    }

    [TestMethod] public void StageCContainsNoForbiddenFutureFeaturesOrDemoDocuments()
    {
        var source = Text("src/RAWSelectionAssistant/ViewModels/BookingDocumentsViewModel.cs") + Text("src/RAWSelectionAssistant.Core/Services/Bookings/BookingDocumentWorkflowService.cs") + Text("src/RAWSelectionAssistant/Views/BookingDocumentsPanel.xaml");
        foreach (var forbidden in new[] { "ReminderScheduler", "错过提醒", "今日拍摄", "未来7天", "ProjectRelationships", "永久删除排期", "项目模板", "项目状态机", "项目健康检查", "本地选片", "精修片回匹配", "联系表", "交付包", "文件夹监听", "CRM", "在线预约", "支付", "云同步", "UI_REVIEW_BUILD", "演示文档" }) DoesNotContain(source, forbidden);
    }

    private static string Path(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
    private static string Text(string relative) => File.ReadAllText(Path(relative));
    private static void Contains(string text, params string[] values) { foreach (var value in values) StringAssert.Contains(text, value); }
    private static void DoesNotContain(string text, params string[] values) { foreach (var value in values) Assert.IsFalse(text.Contains(value, StringComparison.Ordinal), value); }
    private static int Count(string text, string value) { var result = 0; for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length) result++; return result; }
    private static string Slice(string text, string startValue, string endValue, bool includeEnd = false) { var start = text.IndexOf(startValue, StringComparison.Ordinal); var end = text.IndexOf(endValue, start + startValue.Length, StringComparison.Ordinal); return text[start..(includeEnd ? end + endValue.Length : end)]; }
    private static string FindRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(System.IO.Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName; throw new DirectoryNotFoundException(); }
}

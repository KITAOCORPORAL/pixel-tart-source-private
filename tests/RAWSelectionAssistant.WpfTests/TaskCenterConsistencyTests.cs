using System.IO;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.ViewModels;

namespace RAWSelectionAssistant.WpfTests;

[TestClass]
public sealed class TaskCenterConsistencyTests
{
    [TestMethod]
    public void TerminalDiagnostics_KeepTaskIdAndFailureReasonReadable()
    {
        var taskId = Guid.NewGuid();
        var item = new TaskSnapshotViewModel(new(taskId, null, "Batch", TaskLifecycleState.Failed, 0,
            "验证", null, new TaskResultSummary(1, 0, 1, 0, 0, 0, 0, 0), null, null,
            "VerificationFailed", "验证失败：输出文件无法解码。", DateTimeOffset.UtcNow));

        StringAssert.Contains(item.TaskIdText, taskId.ToString("N"));
        StringAssert.Contains(item.DiagnosticText, "VerificationFailed");
        StringAssert.Contains(item.DiagnosticText, "输出文件无法解码");
    }

    [TestMethod]
    public void TaskCenterSurface_ExposesStableIdAndCopyAction()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/MainWindow.xaml"));
        StringAssert.Contains(source, "TaskIdText");
        StringAssert.Contains(source, "CopyDiagnosticsCommand");
    }

    [TestMethod]
    public void StructuredFailure_IsLocalizedSanitizedAndRetryable()
    {
        var taskId = Guid.NewGuid();
        var detail = new MediaTaskFailureDetail("DSC09403.ARW", MediaTaskStages.RawDecode,
            "DecodeFailed", "无法完成 RAW 解码。",
            @"LibRawException: C:\Users\Example\Downloads\DSC09403.ARW failed", true, false);
        var payload = MediaTaskFailurePayload.Serialize(detail);
        var item = new TaskSnapshotViewModel(new(taskId, null, "RAW 转 JPG", TaskLifecycleState.Failed, 100,
            "RAW 解码", null, new TaskResultSummary(1, 0, 1, 0, 0, 0, 0, 0), null, null,
            "DecodeFailed", payload, DateTimeOffset.UtcNow));

        Assert.AreEqual("无法完成 RAW 解码。", item.PrimaryFailureReason);
        Assert.AreEqual("DSC09403.ARW", item.FailureFileName);
        Assert.AreEqual("RAW 解码", item.FailureStage);
        Assert.AreEqual("未修改", item.SourceSafetyText);
        Assert.AreEqual("未生成", item.OutputSafetyText);
        Assert.IsTrue(item.CanRetry);
        Assert.DoesNotContain(@"C:\Users\Example", item.DiagnosticText);
        StringAssert.Contains(item.DiagnosticText, "<PATH_REDACTED>");
    }

    [TestMethod]
    public void TaskCenterSurface_ExposesFailureCardActionsAndTechnicalDisclosure()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src/RAWSelectionAssistant/MainWindow.xaml"));
        StringAssert.Contains(source, "FailedFileText");
        StringAssert.Contains(source, "重试失败项");
        StringAssert.Contains(source, "展开技术信息");
        StringAssert.Contains(source, "复制诊断");
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

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

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RAWSelectionAssistant.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}

using System.IO;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.WpfTests;
public sealed partial class CanvasFavoriteAppAcceptanceTests
{
    [TestMethod]
    public void HttpConvergedRejectsWrongProjectReceiptOverHttp() => Run(async (window, main) =>
    {
        await HttpOpenExisting(window, main);
        var page = main.OnlineSelectionPage.ProjectPage;
        var before = page.FinalResult;
        await NativeInvoke(window, "OnlineRefreshResults");
        await Wait(() => !page.IsBusy && (page.StatusText.Contains("不匹配") || page.StatusText.Contains("无效")), "Reject real HTTP wrong-project result");
        Assert.AreEqual(before, page.FinalResult);
        var workspacePath = Path.Combine(Environment.GetEnvironmentVariable("PIXEL_TART_ISOLATED_RUNTIME_ROOT")!, "OnlineSelection", "workspace.json");
        var stored = JsonDocument.Parse(await File.ReadAllTextAsync(workspacePath));
        Assert.AreEqual(0, stored.RootElement.GetProperty("FinalResults").GetArrayLength());
        await File.WriteAllTextAsync(Path.Combine(Root, "HTTP_WRONG_PROJECT_REJECTED.json"), JsonSerializer.Serialize(new { processId=Environment.ProcessId, projectId=page.Project!.Id, rejected=true, previousResultRetained=true, page.StatusText }));
    });

    [TestMethod]
    public void HttpConvergedReopenReturnsToSameWorkflow() => Run(async (window, main) =>
    {
        var prior = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "HTTP_SYNC.json"))).RootElement;
        await HttpOpenExisting(window, main);
        var page = main.OnlineSelectionPage.ProjectPage;
        var selectionId = page.Project!.Id;
        var workflowId = prior.GetProperty("workflowId").GetGuid();
        Assert.AreNotEqual(prior.GetProperty("processId").GetInt32(), Environment.ProcessId);
        Assert.AreEqual(workflowId, page.Project.WorkflowProjectId);
        Assert.AreEqual(prior.GetProperty("revision").GetInt64(), page.FinalResult!.Revision);
        Assert.IsTrue(page.ConfirmationReceiptText.Contains($"r{page.FinalResult.Revision}"));
        await NativeButton(window, "交付结果");
        var archive = Path.Combine(Root, "archive"); Directory.CreateDirectory(archive);
        var invoke = NativeInvoke(window, "OnlineSyncWorkflow");
        await HttpFileDialog(window, "选择选片结果归档目录", archive, true);
        await invoke;
        await Wait(() => !page.IsBusy && main.IsWorkflowPage, "Reopen returns to linked workflow");
        var linked = main.ProjectHistory.Where(p => p.SourceOnlineSelectionProjectId == selectionId).ToArray();
        Assert.HasCount(1, linked); Assert.AreEqual(workflowId, linked[0].Id);
        Assert.HasCount(1, linked[0].SelectionInputs); Assert.HasCount(1, main.Selections);
        await File.WriteAllTextAsync(Path.Combine(Root, "HTTP_REOPEN_WORKFLOW.json"), JsonSerializer.Serialize(new { processId=Environment.ProcessId, projectId=selectionId, workflowId, revision=page.FinalResult.Revision, resultVersion=page.FinalResult.SelectionVersion, linkedRecords=linked.Length, selected=linked[0].SelectionInputs, returnedToWorkflow=main.IsWorkflowPage, persistedBeforeRefresh=true }));
        Capture(window, "OFFSCREEN-converged-reopened-workflow.png");
    });
}

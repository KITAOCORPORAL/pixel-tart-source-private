using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AcceptanceFlowTests
{
    [TestMethod]
    public async Task RequiredScenario_ScansMatchesResolvesCopiesAndReports()
    {
        using var temp = new TempDirectory();
        temp.CreateFile("TestRaw/ProjectA/DSC01234.ARW", [1]);
        temp.CreateFile("TestRaw/ProjectA/DSC01235.ARW", [2]);
        temp.CreateFile("TestRaw/ProjectB/DSC01236.ARW", [3]);
        temp.CreateFile("TestRaw/ProjectC/DSC01236.ARW", [4]);
        temp.CreateFile("TestRaw/ProjectC/IMG_3288.CR3", [5]);
        var log = new TestLogService();
        var normalizer = new FileNameNormalizer();
        var index = await new RawIndexService(normalizer, log, cacheFilePath: temp.Combine("index.json"))
            .ScanAsync([temp.Combine("TestRaw")], null, null, CancellationToken.None);
        var items = new[]
        {
            new SelectionItem { OriginalInput = "DSC01234.JPG" },
            new SelectionItem { OriginalInput = "1235" },
            new SelectionItem { OriginalInput = "1236" },
            new SelectionItem { OriginalInput = "IMG_3288.JPG" },
            new SelectionItem { OriginalInput = "9999" }
        };
        var decisions = await new RawMatchService(normalizer).MatchAsync(items, index, CancellationToken.None);
        Apply(items, decisions);

        CollectionAssert.AreEqual(
            new[] { MatchStatus.Matched, MatchStatus.Matched, MatchStatus.Conflict, MatchStatus.Matched, MatchStatus.NotFound },
            items.Select(x => x.Status).ToArray());

        var output = temp.Combine("验收输出");
        var copy = new RawCopyService(log);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => copy.CopyAsync(items, output, OutputMode.Flat, null, CancellationToken.None));

        items[2].SelectedRaw = items[2].Candidates[0];
        items[2].Status = MatchStatus.ManuallyConfirmed;
        var summary = await copy.CopyAsync(items, output, OutputMode.Flat, null, CancellationToken.None);
        foreach (var outcome in summary.Outcomes)
        {
            var item = items.Single(x => x.Id == outcome.ItemId);
            item.Status = outcome.Status;
            item.RawOutputPath = outcome.DestinationPath;
            item.OperationTime = outcome.OperationTime;
        }
        await new ReportService(log).ExportAsync(output, items);

        Assert.AreEqual(4, summary.CopiedCount);
        Assert.AreEqual(4, Directory.GetFiles(output, "*.*", SearchOption.AllDirectories).Count(path =>
            RawIndexService.DefaultExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)));
        Assert.IsTrue(File.Exists(Path.Combine(output, "匹配报告.csv")));
        Assert.IsTrue(File.Exists(Path.Combine(output, "匹配报告.json")));
        Assert.IsTrue(File.Exists(Path.Combine(output, "操作日志.txt")));
        var csvBytes = await File.ReadAllBytesAsync(Path.Combine(output, "匹配报告.csv"));
        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBB, 0xBF }, csvBytes.Take(3).ToArray());
    }

    private static void Apply(SelectionItem[] items, IReadOnlyList<MatchDecision> decisions)
    {
        foreach (var decision in decisions)
        {
            var item = items.Single(x => x.Id == decision.ItemId);
            item.NormalizedName = decision.NormalizedName;
            item.NumericId = decision.NumericId;
            item.Status = decision.Status;
            item.SelectedRaw = decision.SelectedRaw;
            item.Candidates = decision.Candidates;
            item.IsDuplicate = decision.IsDuplicate;
            item.IsSelected = !decision.IsDuplicate;
            item.Note = decision.Note;
        }
    }
}

using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawCopyServiceTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    public async Task Copy_ChinesePathWorksAndPreservesContent()
    {
        using var temp = new TempDirectory("中文 路径 " + Guid.NewGuid().ToString("N"));
        var sourcePath = temp.CreateFile("原片/项目甲/DSC01234.ARW", [1, 2, 3, 4]);
        var source = IndexFactory.Entry(sourcePath, temp.Combine("原片"), _normalizer);
        var item = MatchedItem("1234", source);
        var output = temp.Combine("输出 目录");

        var result = await new RawCopyService(new TestLogService()).CopyAsync([item], output, OutputMode.PreserveRelativeStructure, null, CancellationToken.None);

        Assert.AreEqual(1, result.CopiedCount);
        var destination = result.Outcomes.Single().DestinationPath;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(destination));
        Assert.IsTrue(File.Exists(sourcePath));
    }

    [TestMethod]
    public async Task Copy_ExistingDifferentFileIsNeverOverwritten()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.CreateFile("source/DSC01234.ARW", [1, 2, 3]);
        var existingPath = temp.CreateFile("output/DSC01234.ARW", [9, 9]);
        var source = IndexFactory.Entry(sourcePath, temp.Combine("source"), _normalizer);

        var result = await new RawCopyService(new TestLogService()).CopyAsync([MatchedItem("1234", source)], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 9, 9 }, await File.ReadAllBytesAsync(existingPath));
        Assert.AreEqual("DSC01234_2.ARW", Path.GetFileName(result.Outcomes.Single().DestinationPath));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(result.Outcomes.Single().DestinationPath));
    }

    [TestMethod]
    public async Task Copy_DuplicateSourceIsCopiedOnlyOnce()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.CreateFile("source/DSC01234.ARW", [1]);
        var source = IndexFactory.Entry(sourcePath, temp.Combine("source"), _normalizer);
        var items = new[] { MatchedItem("1234", source), MatchedItem("DSC01234.JPG", source) };
        var result = await new RawCopyService(new TestLogService()).CopyAsync(items, temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None);
        Assert.HasCount(1, result.Outcomes);
        Assert.HasCount(1, Directory.GetFiles(temp.Combine("output"), "*.ARW"));
    }

    [TestMethod]
    public async Task Copy_UnresolvedConflictIsRejected()
    {
        using var temp = new TempDirectory();
        var conflict = new SelectionItem { OriginalInput = "1236", Status = MatchStatus.Conflict, IsSelected = true };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new RawCopyService(new TestLogService()).CopyAsync([conflict], temp.Combine("output"), OutputMode.Flat, null, CancellationToken.None));
    }

    [TestMethod]
    public async Task Copy_CancellationRemovesOnlyNewPartialFileAndKeepsExistingTarget()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.CreateFile("source/DSC01234.ARW", new byte[4 * 1024 * 1024]);
        var existingPath = temp.CreateFile("output/DSC01234.ARW", [9, 8, 7]);
        var source = IndexFactory.Entry(sourcePath, temp.Combine("source"), _normalizer);
        using var cts = new CancellationTokenSource();
        var progress = new CancelingProgress(cts);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new RawCopyService(new TestLogService()).CopyAsync([MatchedItem("1234", source)], temp.Combine("output"), OutputMode.Flat, progress, cts.Token));

        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, await File.ReadAllBytesAsync(existingPath));
        Assert.IsFalse(File.Exists(temp.Combine("output", "DSC01234_2.ARW")));
        Assert.IsTrue(File.Exists(sourcePath));
    }

    private static SelectionItem MatchedItem(string input, RawFileEntry source) => new()
    {
        OriginalInput = input,
        NormalizedName = source.NormalizedName,
        NumericId = source.NumericId,
        Status = MatchStatus.Matched,
        SelectedRaw = source,
        Candidates = [source],
        IsSelected = true
    };

    private sealed class CancelingProgress(CancellationTokenSource cancellation) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => cancellation.Cancel();
    }
}

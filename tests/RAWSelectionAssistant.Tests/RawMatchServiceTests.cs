using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class RawMatchServiceTests
{
    private readonly FileNameNormalizer _normalizer = new();

    [TestMethod]
    public async Task FullNameMatch_MatchesCorrespondingRaw()
    {
        using var temp = new TempDirectory();
        var rawPath = temp.CreateFile("DSC01234.ARW");
        var raw = IndexFactory.Entry(rawPath, temp.Path, _normalizer);
        var item = new SelectionItem { OriginalInput = "DSC01234.JPG" };

        var decision = (await new RawMatchService(_normalizer).MatchAsync([item], IndexFactory.Snapshot(raw), CancellationToken.None)).Single();

        Assert.AreEqual(MatchStatus.Matched, decision.Status);
        Assert.AreEqual(rawPath, decision.SelectedRaw?.FullPath);
    }

    [TestMethod]
    [DataRow("1234")]
    [DataRow("01234")]
    public async Task NumericMatch_IgnoresLeadingZeros(string input)
    {
        using var temp = new TempDirectory();
        var raw = IndexFactory.Entry(temp.CreateFile("DSC01234.ARW"), temp.Path, _normalizer);
        var item = new SelectionItem { OriginalInput = input };
        var decision = (await new RawMatchService(_normalizer).MatchAsync([item], IndexFactory.Snapshot(raw), CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.Matched, decision.Status);
        Assert.AreEqual("1234", decision.NumericId);
    }

    [TestMethod]
    [DataRow("DSC01234 (1).JPG")]
    [DataRow("DSC01234-副本.JPG")]
    [DataRow("dsc01234.jpeg")]
    public async Task NormalizedFullName_MatchesCopyAndCaseVariants(string input)
    {
        using var temp = new TempDirectory();
        var raw = IndexFactory.Entry(temp.CreateFile("DSC01234.ARW"), temp.Path, _normalizer);
        var decision = (await new RawMatchService(_normalizer).MatchAsync([new SelectionItem { OriginalInput = input }], IndexFactory.Snapshot(raw), CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.Matched, decision.Status);
    }

    [TestMethod]
    public async Task SameNumberInTwoRawFiles_ReturnsConflict()
    {
        using var temp = new TempDirectory();
        var first = IndexFactory.Entry(temp.CreateFile("A/DSC01236.ARW"), temp.Combine("A"), _normalizer);
        var second = IndexFactory.Entry(temp.CreateFile("B/DSC01236.ARW"), temp.Combine("B"), _normalizer);
        var decision = (await new RawMatchService(_normalizer).MatchAsync([new SelectionItem { OriginalInput = "1236" }], IndexFactory.Snapshot(first, second), CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.Conflict, decision.Status);
        Assert.HasCount(2, decision.Candidates);
        Assert.IsNull(decision.SelectedRaw);
    }

    [TestMethod]
    public async Task MissingNumber_ReturnsNotFound()
    {
        var decision = (await new RawMatchService(_normalizer).MatchAsync([new SelectionItem { OriginalInput = "9999" }], new RawIndexSnapshot(), CancellationToken.None)).Single();
        Assert.AreEqual(MatchStatus.NotFound, decision.Status);
    }

    [TestMethod]
    public async Task RepeatedNumber_IsRetainedButMarkedDuplicate()
    {
        using var temp = new TempDirectory();
        var raw = IndexFactory.Entry(temp.CreateFile("DSC01234.ARW"), temp.Path, _normalizer);
        var items = new[]
        {
            new SelectionItem { OriginalInput = "1234" },
            new SelectionItem { OriginalInput = "DSC01234.JPG" },
            new SelectionItem { OriginalInput = "1234" }
        };
        var decisions = await new RawMatchService(_normalizer).MatchAsync(items, IndexFactory.Snapshot(raw), CancellationToken.None);
        Assert.HasCount(3, decisions);
        Assert.IsFalse(decisions[0].IsDuplicate);
        Assert.IsTrue(decisions[1].IsDuplicate);
        Assert.IsTrue(decisions[2].IsDuplicate);
    }
}

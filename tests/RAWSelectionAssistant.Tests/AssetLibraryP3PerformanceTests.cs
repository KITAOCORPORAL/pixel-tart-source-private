using System.Diagnostics;
using RAWSelectionAssistant.Core.Models;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3PerformanceTests
{
    private static readonly TimeSpan DefaultFirstPageLimit = TimeSpan.FromMilliseconds(1_500);
    private static readonly TimeSpan SuggestionLimit = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SingleRuleLimit = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan NestedRulesLimit = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan SmartFolderPreviewLimit = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan ScopeSwitchLimit = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Batch100Limit = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan Batch500Limit = TimeSpan.FromMilliseconds(2_000);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Performance")]
    public async Task TenThousandSyntheticItemsMeetP3QuerySuggestionSmartFolderAndBatchThresholds()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateAsync();
        var now = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);
        var seeds = Enumerable.Range(0, 10_000)
            .Select(index => new AssetLibraryP3Seed(
                GuidFromIndex(index),
                $"synthetic-{index:00000}-batch.jpg",
                ".jpg",
                "Image",
                1_000 + index,
                800 + (index % 4 * 200),
                600 + (index % 3 * 200),
                index % 3 == 0 ? "Landscape" : index % 3 == 1 ? "Portrait" : "Square",
                index % 10 == 0 ? null : now.AddDays(-(index % 365)),
                now.AddSeconds(-index),
                index % 6,
                index % 2 == 0 ? "batch 中文" : "batch metadata",
                false,
                false))
            .ToArray();
        await setup.InsertAssetsAsync(seeds);
        var ids = seeds.Select(seed => seed.Id).ToArray();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "性能当前范围"));
        await setup.Repository.AddToFolderAsync(ids.Take(100), folder.FolderId);
        var suggestionTag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "批量性能"));

        var defaultQuery = new AssetLibraryQuery(PageSize: 100);
        var (defaultWorst, defaultPage) = await WorstOfThreeAsync(() => setup.Repository.QueryAsync(defaultQuery));
        Assert.AreEqual(10_000, defaultPage.TotalCount);
        Assert.HasCount(100, defaultPage.Items);
        AssertWithin("10,000 项默认首屏", defaultWorst, DefaultFirstPageLimit);

        var (suggestionWorst, suggestions) = await WorstOfThreeAsync(() => setup.Repository.GetQuerySuggestionsAsync("批量", 20));
        Assert.IsTrue(suggestions.Any(item => item.Value.Contains(suggestionTag.TagId.ToString("D"), StringComparison.OrdinalIgnoreCase) || item.Label.Contains("批量", StringComparison.Ordinal)));
        AssertWithin("普通文本建议", suggestionWorst, SuggestionLimit);

        var singleDocument = new AssetQueryDocument
        {
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
            [AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"])])
        };
        var singleQuery = new AssetLibraryQuery(PageSize: 100) { Document = singleDocument };
        var (singleWorst, singlePage) = await WorstOfThreeAsync(() => setup.Repository.QueryAsync(singleQuery));
        Assert.AreEqual(4_999, singlePage.TotalCount);
        AssertWithin("单条件筛选更新", singleWorst, SingleRuleLimit);

        var nestedDocument = CreateEightRuleThreeLevelDocument();
        var nestedQuery = new AssetLibraryQuery(PageSize: 100) { Document = nestedDocument };
        var (nestedWorst, nestedPage) = await WorstOfThreeAsync(() => setup.Repository.QueryAsync(nestedQuery));
        Assert.IsTrue(string.IsNullOrWhiteSpace(nestedPage.RegexError), nestedPage.RegexError);
        Assert.IsGreaterThan(0, nestedPage.TotalCount);
        AssertWithin("8 条规则、3 层嵌套组合查询", nestedWorst, NestedRulesLimit);

        var smartFolder = await setup.Repository.SaveSmartFolderQueryDocumentAsync(
            new(Guid.NewGuid(), "10k 实时预览"),
            nestedDocument);
        var (smartWorst, smartPage) = await WorstOfThreeAsync(() => setup.Repository.QueryAsync(new(SmartFolderId: smartFolder.SmartFolderId, PageSize: 12)));
        Assert.AreEqual(nestedPage.TotalCount, smartPage.TotalCount);
        AssertWithin("智能文件夹预览", smartWorst, SmartFolderPreviewLimit);

        var currentDocument = singleDocument with { Scope = AssetQueryScope.Current };
        var allDocument = singleDocument with { Scope = AssetQueryScope.AllAssets };
        var currentQuery = new AssetLibraryQuery(FolderId: folder.FolderId, PageSize: 100) { Document = currentDocument };
        var currentPage = await setup.Repository.QueryAsync(currentQuery);
        var (scopeWorst, allPage) = await WorstOfThreeAsync(() => setup.Repository.QueryAsync(currentQuery with { Document = allDocument }));
        Assert.IsGreaterThan(
            currentPage.TotalCount,
            allPage.TotalCount,
            $"全库范围应比当前文件夹范围包含更多结果：all={allPage.TotalCount}, current={currentPage.TotalCount}。");
        Assert.AreEqual(4_999, allPage.TotalCount);
        AssertWithin("当前/全库范围切换", scopeWorst, ScopeSwitchLimit);

        var batch100Tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "batch-100"));
        var batch100Request = new AssetBatchMetadataRequest(
            ids.Take(100).ToArray(),
            AddTagIds: [batch100Tag.TagId]);
        var batch100Preview = await setup.Repository.PreviewBatchMetadataAsync(batch100Request);
        var batch100 = await MeasureOnceAsync(() => setup.Repository.ApplyBatchMetadataAsync(batch100Request, batch100Preview));
        Assert.AreEqual(100, batch100.Value.ChangedCount);
        AssertWithin("100 项批量标签操作", batch100.Elapsed, Batch100Limit);

        var batch500Tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "batch-500"));
        var batch500Request = new AssetBatchMetadataRequest(
            ids.Take(500).ToArray(),
            AddTagIds: [batch500Tag.TagId]);
        var batch500Preview = await setup.Repository.PreviewBatchMetadataAsync(batch500Request);
        var batch500 = await MeasureOnceAsync(() => setup.Repository.ApplyBatchMetadataAsync(batch500Request, batch500Preview));
        Assert.AreEqual(500, batch500.Value.ChangedCount);
        AssertWithin("500 项批量标签操作", batch500.Elapsed, Batch500Limit);
    }

    private static AssetQueryDocument CreateEightRuleThreeLevelDocument() => new()
    {
        RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
        [
            AssetQueryNode.Rule(AssetQueryField.MediaType, AssetQueryOperator.Equals, ["Image"]),
            AssetQueryNode.Rule(AssetQueryField.FileSize, AssetQueryOperator.GreaterThan, ["0"]),
            AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["2"]),
            AssetQueryNode.Rule(AssetQueryField.IsMissing, AssetQueryOperator.IsFalse),
            AssetQueryNode.Rule(AssetQueryField.Comment, AssetQueryOperator.Contains, ["batch"]),
            AssetQueryNode.Group(AssetQueryLogic.Any,
            [
                AssetQueryNode.Rule(AssetQueryField.Orientation, AssetQueryOperator.Equals, ["Landscape"]),
                AssetQueryNode.Group(AssetQueryLogic.All,
                [
                    AssetQueryNode.Rule(AssetQueryField.Width, AssetQueryOperator.GreaterThanOrEqual, ["1000"]),
                    AssetQueryNode.Rule(AssetQueryField.Height, AssetQueryOperator.GreaterThanOrEqual, ["600"])
                ])
            ])
        ])
    };

    private static Guid GuidFromIndex(int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[12..], index + 1);
        bytes[0] = 0x30;
        return new Guid(bytes);
    }

    private static async Task<(TimeSpan Worst, T Value)> WorstOfThreeAsync<T>(Func<Task<T>> action)
    {
        _ = await action();
        var worst = TimeSpan.Zero;
        T value = default!;
        for (var run = 0; run < 3; run++)
        {
            var measured = await MeasureOnceAsync(action);
            if (measured.Elapsed > worst) worst = measured.Elapsed;
            value = measured.Value;
        }
        return (worst, value);
    }

    private static async Task<(TimeSpan Elapsed, T Value)> MeasureOnceAsync<T>(Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var value = await action();
        stopwatch.Stop();
        return (stopwatch.Elapsed, value);
    }

    private void AssertWithin(string metric, TimeSpan actual, TimeSpan limit)
    {
        TestContext.WriteLine($"{metric}: {actual.TotalMilliseconds:F1} ms / {limit.TotalMilliseconds:F0} ms");
        Assert.IsTrue(actual <= limit, $"{metric} exceeded its P3 limit: {actual.TotalMilliseconds:F1} ms > {limit.TotalMilliseconds:F1} ms.");
    }
}

using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3ScopeOrderingRegressionTests
{
    [TestMethod]
    public async Task OrderedScopeQueryUsesExactOrderIndexAndPreservesRowsAndBytes()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "scope-order"));
        await setup.Repository.AddToFolderAsync([setup.A, setup.B], folder.FolderId);
        var document = new AssetQueryDocument { Scope = AssetQueryScope.Current,
            RootGroup = AssetQueryNode.Group(AssetQueryLogic.All,
                [AssetQueryNode.Rule(AssetQueryField.Rating, AssetQueryOperator.GreaterThanOrEqual, ["3"])]) };
        var query = new AssetLibraryQuery(FolderId: folder.FolderId, PageSize: 1) { Document = document };
        var current = await setup.ReadAllAsync(query);
        CollectionAssert.AreEqual(new[] { setup.B }, current.Select(x => x.AssetId).ToArray());
        Assert.AreEqual(2000L, await setup.Repository.QueryTotalBytesAsync(query));
        var all = query with { Document = document with { Scope = AssetQueryScope.AllAssets } };
        var observations = new List<AssetLibraryQueryDiagnostics.Query>();
        using (AssetLibraryQueryDiagnostics.Capture(observations.Add))
        {
            var rows = await setup.ReadAllAsync(all);
            CollectionAssert.AreEqual(new[] { setup.C, setup.B }, rows.Select(x => x.AssetId).ToArray());
            Assert.AreEqual(5000L, await setup.Repository.QueryTotalBytesAsync(all));
        }
        Assert.AreEqual(2, (await setup.Repository.QueryAsync(all)).TotalCount);
        await using var connection = new SqliteConnection($"Data Source={setup.DatabasePath}");
        await connection.OpenAsync();
        var observed = observations[0];
        await using var explain = connection.CreateCommand();
        explain.CommandText = "EXPLAIN QUERY PLAN " + observed.PageSql;
        foreach (var parameter in observed.PageParameters) explain.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
        var details = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
            while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.IsTrue(details.Any(x => x.Contains("IX_AssetItems_ActiveAddedOrder", StringComparison.Ordinal)), string.Join(" | ", details));
        Assert.IsFalse(details.Any(x => x.Contains("TEMP B-TREE", StringComparison.Ordinal)), string.Join(" | ", details));
        // Removing the optimization restores the old plan, not different query semantics.
        await using (var drop = connection.CreateCommand())
        { drop.CommandText = "DROP INDEX IX_AssetItems_ActiveAddedOrder;"; await drop.ExecuteNonQueryAsync(); }
        var oldPlanRows = await setup.ReadAllAsync(all);
        CollectionAssert.AreEqual(new[] { setup.C, setup.B }, oldPlanRows.Select(x => x.AssetId).ToArray());
        await setup.RestartAsync();
        Assert.AreEqual(5000L, await setup.Repository.QueryTotalBytesAsync(all));
        await using var index = connection.CreateCommand();
        index.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_AssetItems_ActiveAddedOrder';";
        Assert.AreEqual(1L, await index.ExecuteScalarAsync());
    }
}

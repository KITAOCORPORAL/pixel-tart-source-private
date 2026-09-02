using Microsoft.Data.Sqlite;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3QueryPlanTests
{
    [TestMethod]
    public async Task CommonMembershipRatingAndVisualQueriesUseDeclaredIndexes()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateAsync();
        await using var connection = new SqliteConnection($"Data Source={setup.DatabasePath}");
        await connection.OpenAsync();

        await AssertPlanUsesIndexAsync(
            connection,
            "SELECT AssetId FROM AssetTagMemberships WHERE TagId=$value ORDER BY AssetId;",
            "IX_AssetTagMemberships_Tag");
        await AssertPlanUsesIndexAsync(
            connection,
            "SELECT AssetId FROM AssetFolderMemberships WHERE FolderId=$value ORDER BY AssetId;",
            "IX_AssetFolderMemberships_Folder");
        await AssertPlanUsesIndexAsync(
            connection,
            "SELECT AssetId FROM AssetItems WHERE Rating>=$value ORDER BY Rating,AddedAt DESC,AssetId LIMIT 100;",
            "IX_AssetItems_Rating");
        await AssertPlanUsesIndexAsync(
            connection,
            "SELECT AssetId FROM AssetVisualFeatures WHERE AnalysisVersion=$value AND Outcome='Succeeded' ORDER BY AssetId;",
            "IX_AssetVisualFeatures_Outcome");
    }

    private static async Task AssertPlanUsesIndexAsync(
        SqliteConnection connection,
        string sql,
        string expectedIndex)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        command.Parameters.AddWithValue("$value", 1);
        var details = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));

        Assert.IsTrue(
            details.Any(detail => detail.Contains(expectedIndex, StringComparison.OrdinalIgnoreCase)),
            $"Expected query plan to use {expectedIndex}, actual plan: {string.Join(" | ", details)}");
    }
}

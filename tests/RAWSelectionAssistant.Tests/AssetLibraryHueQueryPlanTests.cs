using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryHueQueryPlanTests
{
    [TestMethod]
    public async Task MaterialHueFilterUsesOneIndexedPaletteRangeScan()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart_HuePlan", Guid.NewGuid().ToString("N"));
        var database = new AssetLibraryDatabase(Path.Combine(root, "asset-library.db"));
        try
        {
            await using var repository = new SqliteAssetLibraryRepository(database);
            await repository.InitializeAsync();
            await using var connection = await database.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXPLAIN QUERY PLAN
                SELECT COUNT(*)
                FROM AssetItems a
                LEFT JOIN AssetVisualFeatures f
                    ON f.AssetId=a.AssetId AND f.AnalysisVersion=$version
                WHERE f.AssetId IS NOT NULL
                  AND f.Outcome='Succeeded'
                  AND f.SourceContentHash IS NOT NULL
                  AND a.ContentHash IS NOT NULL
                  AND f.SourceContentHash=a.ContentHash
                  AND a.AssetId IN (
                      SELECT pc.AssetId
                      FROM AssetVisualPaletteColors pc
                      WHERE pc.AnalysisVersion=$version
                        AND pc.Weight>=0.15
                        AND pc.Saturation>=0.08
                        AND pc.Chroma>=8
                        AND pc.Hue BETWEEN $start AND $end);
                """;
            command.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
            command.Parameters.AddWithValue("$start", 30d);
            command.Parameters.AddWithValue("$end", 60d);

            var details = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) details.Add(reader.GetString(3));

            Assert.IsFalse(
                details.Any(detail => detail.Contains("CORRELATED", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", details));
            Assert.AreEqual(
                1,
                details.Count(detail => detail.Contains("IX_AssetVisualPaletteColors_MaterialHue", StringComparison.OrdinalIgnoreCase)),
                string.Join(" | ", details));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

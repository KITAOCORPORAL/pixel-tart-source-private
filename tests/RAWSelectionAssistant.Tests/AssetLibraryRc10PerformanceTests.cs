using System.Diagnostics;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;

namespace RAWSelectionAssistant.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AssetLibraryRc10PerformanceTests
{
    [TestMethod]
    [TestCategory("RC10Performance")]
    public async Task SearchAndFirstPage_On10K50K100K_RecordsThreeFreshSamples()
    {
        var root = Path.Combine(Path.GetTempPath(), "PixelTart_RC10_Performance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var measurements = new List<object>();
        try
        {
            foreach (var count in new[] { 10_000, 50_000, 100_000 })
            {
                var samples = new List<double>();
                for (var sample = 1; sample <= 3; sample++)
                {
                    var database = new AssetLibraryDatabase(Path.Combine(root, $"assets-{count}-{sample}.db"));
                    await using var repository = new SqliteAssetLibraryRepository(database);
                    await repository.InitializeAsync();
                    await SeedSyntheticMetadataAsync(database, count);

                    var timer = Stopwatch.StartNew();
                    var page = await repository.QueryAsync(new AssetLibraryQuery(SearchText: "asset", MinimumRating: 2, PageSize: 100));
                    timer.Stop();
                    Assert.AreEqual(count * 3 / 5, page.TotalCount);
                    Assert.HasCount(100, page.Items);
                    samples.Add(timer.Elapsed.TotalMilliseconds);
                }

                samples.Sort();
                measurements.Add(new { rows = count, min_ms = samples[0], median_ms = samples[1], max_ms = samples[2], samples_ms = samples });
                if (count == 100_000) Assert.IsLessThanOrEqualTo(27_000d, samples[1], "The RC10 100K median ceiling must not be relaxed.");
            }

            var output = Environment.GetEnvironmentVariable("PIXEL_TART_RC10_PERFORMANCE_PATH");
            if (!string.IsNullOrWhiteSpace(output))
            {
                var target = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new { schema = "pixel-tart-rc10-performance/v1", measurements }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            if (Directory.Exists(root) && Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetTempPath(), "PixelTart_RC10_Performance") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }

    private static async Task SeedSyntheticMetadataAsync(AssetLibraryDatabase database, int count)
    {
        await using var connection = await database.OpenConnectionAsync(write: true);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH RECURSIVE sequence(n) AS (
                SELECT 1 UNION ALL SELECT n + 1 FROM sequence WHERE n < $count
            )
            INSERT INTO AssetItems(
                AssetId,SourcePath,NormalizedSourcePath,DisplayName,Extension,MediaType,FileSize,
                AddedAt,ModifiedAt,Rating,Comment,IsMissing,IsArchived,ImportMode)
            SELECT
                printf('00000000-0000-0000-0000-%012x',n),
                printf('C:\synthetic\asset-%06d.jpg',n),
                printf('C:\SYNTHETIC\ASSET-%06d.JPG',n),
                printf('asset-%06d.jpg',n),'.JPG','Image',1024,
                '2026-09-11T00:00:00+00:00','2026-09-11T00:00:00+00:00',n % 5,'synthetic benchmark',0,0,'Reference'
            FROM sequence;
            """;
        command.Parameters.AddWithValue("$count", count);
        Assert.AreEqual(count, await command.ExecuteNonQueryAsync());
    }
}

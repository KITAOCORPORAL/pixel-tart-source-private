using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP3QuerySemanticSafetyTests
{
    [TestMethod]
    public async Task VisualRulesRequireCurrentSuccessfulFeatureAndNegativeFormsDoNotAdmitInvalidAssets()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await SeedVisualStatesAsync(setup);

        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(AssetQueryField.VisualHarmony, AssetQueryOperator.Equals, ["Complementary"]));
        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(AssetQueryField.VisualHarmony, AssetQueryOperator.NotEquals, ["Analogous"]));
        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(AssetQueryField.VisualHarmony, AssetQueryOperator.NoneOf, ["Analogous"]));
        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(AssetQueryField.VisualDominantColor, AssetQueryOperator.NotEquals, ["#FFFFFF"]));
        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(
                AssetQueryField.VisualHarmony,
                AssetQueryOperator.Equals,
                ["Analogous"],
                negated: true));
    }

    [TestMethod]
    public async Task VisualAnalysisStatusDistinguishesValidStaleFailedAndNotAnalyzedByCurrentContentHash()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await SeedVisualStatesAsync(setup);

        await AssertIdsAsync(setup, [setup.A], StatusRule("Valid"), includeArchived: true);
        await AssertIdsAsync(setup, [setup.B], StatusRule("Stale"), includeArchived: true);
        await AssertIdsAsync(setup, [setup.C], StatusRule("NotAnalyzed"), includeArchived: true);
        await AssertIdsAsync(setup, [setup.Archived], StatusRule("Failed"), includeArchived: true);
    }

    [TestMethod]
    public async Task AddedAtAndCaptureTimeCompareUtcInstantsInsteadOfOffsetTextOrder()
    {
        await using var setup = await AssetLibraryP3TestSetup.CreateCanonicalAsync();
        await using (var connection = new SqliteConnection($"Data Source={setup.DatabasePath}"))
        {
            await connection.OpenAsync();
            await SetDatesAsync(connection, setup.A, "2026-09-02T00:30:00+14:00"); // 2026-09-01 10:30Z
            await SetDatesAsync(connection, setup.B, "2026-09-01T20:00:00-10:00"); // 2026-09-02 06:00Z
            await SetDatesAsync(connection, setup.C, "2026-08-31T23:00:00Z");
        }

        const string cutoff = "2026-09-01T12:00:00Z";
        await AssertIdsAsync(
            setup,
            [setup.B],
            AssetQueryNode.Rule(AssetQueryField.AddedAt, AssetQueryOperator.GreaterThan, [cutoff]));
        await AssertIdsAsync(
            setup,
            [setup.B],
            AssetQueryNode.Rule(AssetQueryField.CaptureTime, AssetQueryOperator.GreaterThan, [cutoff]));
        await AssertIdsAsync(
            setup,
            [setup.A],
            AssetQueryNode.Rule(AssetQueryField.AddedAt, AssetQueryOperator.Equals, ["2026-09-01T10:30:00Z"]));
        await AssertIdsAsync(
            setup,
            [setup.A, setup.B],
            AssetQueryNode.Rule(
                AssetQueryField.CaptureTime,
                AssetQueryOperator.Between,
                ["2026-09-01T10:00:00Z", "2026-09-02T06:00:00Z"]));
    }

    private static AssetQueryNode StatusRule(string value) =>
        AssetQueryNode.Rule(AssetQueryField.VisualAnalysisStatus, AssetQueryOperator.Equals, [value]);

    private static async Task AssertIdsAsync(
        AssetLibraryP3TestSetup setup,
        IReadOnlyCollection<Guid> expected,
        AssetQueryNode rule,
        bool includeArchived = false)
    {
        var page = await setup.Repository.QueryAsync(new AssetLibraryQuery(PageSize: 100)
        {
            Document = new AssetQueryDocument
            {
                Scope = AssetQueryScope.AllAssets,
                IncludeArchived = includeArchived,
                RootGroup = AssetQueryNode.Group(AssetQueryLogic.All, [rule])
            }
        });
        Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
        CollectionAssert.AreEquivalent(expected.ToArray(), page.Items.Select(item => item.AssetId).ToArray());
        Assert.AreEqual(expected.Count, page.TotalCount);
    }

    private static async Task SeedVisualStatesAsync(AssetLibraryP3TestSetup setup)
    {
        await using var connection = new SqliteConnection($"Data Source={setup.DatabasePath}");
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        await SetContentHashAsync(connection, transaction, setup.A, "current-a");
        await SetContentHashAsync(connection, transaction, setup.B, "current-b");
        await SetContentHashAsync(connection, transaction, setup.C, "current-c");
        await SetContentHashAsync(connection, transaction, setup.Archived, "current-archived");

        await InsertFeatureAsync(connection, transaction, setup.A, "current-a", "Succeeded", "Complementary", 42, "#112233");
        await InsertFeatureAsync(connection, transaction, setup.B, "stale-b", "Succeeded", "Complementary", 42, "#112233");
        await InsertFeatureAsync(connection, transaction, setup.Archived, "current-archived", "Failed", null, null, null);

        await transaction.CommitAsync();
    }

    private static async Task SetContentHashAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid assetId,
        string contentHash)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE AssetItems SET ContentHash=$hash WHERE AssetId=$id;";
        command.Parameters.AddWithValue("$hash", contentHash);
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertFeatureAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid assetId,
        string sourceContentHash,
        string outcome,
        string? harmony,
        double? dominantHue,
        string? paletteHex)
    {
        await using (var feature = connection.CreateCommand())
        {
            feature.Transaction = transaction;
            feature.CommandText = """
                INSERT INTO AssetVisualFeatures(
                    AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,
                    AnalysisSource,SourceProfile,AnalysisProfile,Harmony,DominantHue,CreatedAt,UpdatedAt)
                VALUES($asset,$version,5,'Weight',$fingerprint,$source,$outcome,
                    'RasterOriginal','sRGB','sRGB',$harmony,$hue,$at,$at);
                """;
            feature.Parameters.AddWithValue("$asset", assetId.ToString("D"));
            feature.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
            feature.Parameters.AddWithValue("$fingerprint", $"visual-{assetId:N}");
            feature.Parameters.AddWithValue("$source", sourceContentHash);
            feature.Parameters.AddWithValue("$outcome", outcome);
            feature.Parameters.AddWithValue("$harmony", (object?)harmony ?? DBNull.Value);
            feature.Parameters.AddWithValue("$hue", (object?)dominantHue ?? DBNull.Value);
            feature.Parameters.AddWithValue("$at", "2026-09-02T00:00:00Z");
            await feature.ExecuteNonQueryAsync();
        }

        if (paletteHex is null) return;
        await using var palette = connection.CreateCommand();
        palette.Transaction = transaction;
        palette.CommandText = """
            INSERT INTO AssetVisualPaletteColors(
                AssetId,AnalysisVersion,ColorIndex,Red,Green,Blue,LabL,LabA,LabB,Hue,Saturation,Chroma,Weight,Hex)
            VALUES($asset,$version,0,17,34,51,12,0,0,42,0.5,20,1,$hex);
            """;
        palette.Parameters.AddWithValue("$asset", assetId.ToString("D"));
        palette.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
        palette.Parameters.AddWithValue("$hex", paletteHex);
        await palette.ExecuteNonQueryAsync();
    }

    private static async Task SetDatesAsync(SqliteConnection connection, Guid assetId, string value)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE AssetItems SET AddedAt=$value,CaptureTime=$value WHERE AssetId=$id;";
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$id", assetId.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}

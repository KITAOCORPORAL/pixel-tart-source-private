using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.AssetLibrary;
using RAWSelectionAssistant.Core.Services.AssetLibrary.VisualAnalysis;

namespace RAWSelectionAssistant.Tests;

[TestClass]
public sealed class AssetLibraryP2CoreTests
{
    [TestMethod]
    public async Task OrdinarySmartAndRegexQueriesShareStableBidirectionalSortAndCursorContract()
    {
        await using var setup = await TestSetup.CreateAsync();
        var assets = await setup.SeedSortAssetsAsync();

        foreach (var field in new[] { AssetLibrarySortField.AddedAt, AssetLibrarySortField.CaptureTime, AssetLibrarySortField.FileName, AssetLibrarySortField.FileSize, AssetLibrarySortField.Rating })
        foreach (var direction in Enum.GetValues<AssetLibrarySortDirection>())
        {
            var query = new AssetLibraryQuery(PageSize: 2, SortField: field, SortDirection: direction);
            var actual = await ReadAllAsync(setup.Repository, query);
            var expected = SortExpected(assets, field, direction).Select(asset => asset.AssetId).ToArray();
            CollectionAssert.AreEqual(expected, actual.Select(asset => asset.AssetId).ToArray(), $"{field} {direction}");
        }

        var visualSort = await setup.SeedVisualSortAsync(assets);
        foreach (var direction in Enum.GetValues<AssetLibrarySortDirection>())
        {
            var colorActual = await ReadAllAsync(setup.Repository, new(PageSize: 2, SortField: AssetLibrarySortField.Color, SortDirection: direction));
            var colorExpected = direction == AssetLibrarySortDirection.Ascending
                ? assets.OrderBy(asset => visualSort.Color[asset.AssetId] is null).ThenBy(asset => visualSort.Color[asset.AssetId]).ThenBy(asset => asset.AssetId.ToString("D"), StringComparer.Ordinal)
                : assets.OrderBy(asset => visualSort.Color[asset.AssetId] is null).ThenByDescending(asset => visualSort.Color[asset.AssetId]).ThenBy(asset => asset.AssetId.ToString("D"), StringComparer.Ordinal);
            CollectionAssert.AreEqual(colorExpected.Select(asset => asset.AssetId).ToArray(), colorActual.Select(asset => asset.AssetId).ToArray(), $"Color {direction}");

            var stateActual = await ReadAllAsync(setup.Repository, new(PageSize: 2, SortField: AssetLibrarySortField.VisualAnalysis, SortDirection: direction));
            var stateExpected = direction == AssetLibrarySortDirection.Ascending
                ? assets.OrderBy(asset => visualSort.State[asset.AssetId]).ThenBy(asset => asset.AssetId.ToString("D"), StringComparer.Ordinal)
                : assets.OrderByDescending(asset => visualSort.State[asset.AssetId]).ThenBy(asset => asset.AssetId.ToString("D"), StringComparer.Ordinal);
            CollectionAssert.AreEqual(stateExpected.Select(asset => asset.AssetId).ToArray(), stateActual.Select(asset => asset.AssetId).ToArray(), $"VisualAnalysis {direction}");
        }

        var common = new AssetLibraryQuery(PageSize: 2, SortField: AssetLibrarySortField.FileSize, SortDirection: AssetLibrarySortDirection.Descending);
        var ordinary = await ReadAllAsync(setup.Repository, common);
        var regex = await ReadAllAsync(setup.Repository, common with { FileNameRegex = ".*" });
        var smart = await setup.Repository.SaveSmartFolderAsync(
            new(Guid.NewGuid(), "全部评分"),
            [new(Guid.NewGuid(), Guid.Empty, SmartFolderField.Rating, SmartFolderOperator.GreaterThanOrEqual, "0")]);
        var smartItems = await ReadAllAsync(setup.Repository, common with { SmartFolderId = smart.SmartFolderId });
        CollectionAssert.AreEqual(ordinary.Select(item => item.AssetId).ToArray(), regex.Select(item => item.AssetId).ToArray());
        CollectionAssert.AreEqual(ordinary.Select(item => item.AssetId).ToArray(), smartItems.Select(item => item.AssetId).ToArray());

        var firstPage = await setup.Repository.QueryAsync(common);
        Assert.IsNotNull(firstPage.NextCursor);
        var wrongPlan = await setup.Repository.QueryAsync(common with { Cursor = firstPage.NextCursor, SortField = AssetLibrarySortField.Rating });
        Assert.IsEmpty(wrongPlan.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(wrongPlan.RegexError));
        var malformed = await setup.Repository.QueryAsync(common with { Cursor = "not-a-valid-cursor" });
        Assert.IsEmpty(malformed.Items);
        Assert.IsFalse(string.IsNullOrWhiteSpace(malformed.RegexError));
    }

    [TestMethod]
    public async Task FixedCollectionsSeparateActiveArchivedMissingAndDisabledRecycleBin()
    {
        await using var setup = await TestSetup.CreateAsync();
        var paths = new[] { setup.WriteFile("active.jpg", "active"), setup.WriteFile("archived.jpg", "archived"), setup.WriteFile("missing.jpg", "missing") };
        await setup.Repository.ImportAsync(paths.Select(path => new AssetImportRequest(path)));
        var assets = await ReadAllAsync(setup.Repository, new(PageSize: 10));
        var archived = assets.Single(asset => asset.DisplayName == "archived.jpg");
        var missing = assets.Single(asset => asset.DisplayName == "missing.jpg");
        await setup.Repository.SetAssetsArchivedAsync([archived.AssetId], true);
        await setup.Repository.SetAssetsMissingAsync([missing.AssetId], true);

        var activeItems = await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.AllAssets));
        Assert.HasCount(2, activeItems);
        var archivedItems = await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.Archived));
        Assert.HasCount(1, archivedItems);
        Assert.AreEqual(archived.AssetId, archivedItems[0].AssetId);
        var missingItems = await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.MissingFiles));
        Assert.HasCount(1, missingItems);
        Assert.AreEqual(missing.AssetId, missingItems[0].AssetId);
        Assert.IsEmpty((await setup.Repository.QueryAsync(AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.RecycleBin))).Items);
        Assert.HasCount(2, await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.Uncategorized)));
        Assert.HasCount(2, await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.Untagged)));

        var recent = await ReadAllAsync(setup.Repository, AssetLibrarySystemCollections.CreateQuery(AssetLibrarySystemCollection.RecentlyAdded) with { PageSize = 1 });
        var expectedRecent = activeItems.OrderByDescending(item => item.AddedAt).ThenBy(item => item.AssetId.ToString("D"), StringComparer.Ordinal).Select(item => item.AssetId).ToArray();
        CollectionAssert.AreEqual(expectedRecent, recent.Select(item => item.AssetId).ToArray());
    }

    [TestMethod]
    public async Task AssetFlagsAndFolderRestoreAreMetadataOnlyUndoableAndPersistentlyRedoable()
    {
        await using var setup = await TestSetup.CreateAsync();
        var firstPath = setup.WriteFile("one.jpg", "one-source");
        var secondPath = setup.WriteFile("two.jpg", "two-source");
        var beforeBytes = new[] { await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath) };
        await setup.Repository.ImportAsync([new(firstPath), new(secondPath)]);
        var assets = await ReadAllAsync(setup.Repository, new(PageSize: 10));

        var archived = await setup.Repository.SetAssetsArchivedAsync(assets.Select(asset => asset.AssetId), true);
        Assert.AreEqual(2, archived.ChangedCount);
        Assert.IsNotNull(archived.UndoToken);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(archived.UndoToken));
        Assert.HasCount(2, await ReadAllAsync(setup.Repository, new(PageSize: 10)));
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(archived.UndoToken));
        Assert.HasCount(2, await ReadAllAsync(setup.Repository, new(PageSize: 10, ArchiveScope: AssetLibraryArchiveScope.ArchivedOnly)));

        var restored = await setup.Repository.SetAssetsArchivedAsync(assets.Select(asset => asset.AssetId), false);
        Assert.AreEqual(2, restored.ChangedCount);
        var missing = await setup.Repository.SetAssetsMissingAsync([assets[0].AssetId], true);
        Assert.IsNotNull(missing.UndoToken);
        Assert.HasCount(1, await ReadAllAsync(setup.Repository, new(MissingOnly: true)));
        Assert.IsTrue(await setup.Repository.UndoAsync(missing.UndoToken));
        Assert.IsEmpty(await ReadAllAsync(setup.Repository, new(MissingOnly: true)));

        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "可恢复"));
        await setup.Repository.AddToFolderAsync([assets[0].AssetId], folder.FolderId);
        var folderArchive = await setup.Repository.SetFolderArchivedAsync(folder.FolderId, true);
        Assert.IsNotNull(folderArchive.UndoToken);
        var folderRestore = await setup.Repository.RestoreFolderAsync(folder.FolderId);
        Assert.IsNotNull(folderRestore.UndoToken);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(folderRestore.UndoToken));
        Assert.IsTrue((await setup.Repository.ListFoldersAsync(includeArchived: true)).Single(item => item.FolderId == folder.FolderId).IsArchived);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(folderRestore.UndoToken));
        Assert.IsFalse((await setup.Repository.ListFoldersAsync()).Single(item => item.FolderId == folder.FolderId).IsArchived);
        Assert.HasCount(1, await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));

        CollectionAssert.AreEqual(beforeBytes[0], await File.ReadAllBytesAsync(firstPath));
        CollectionAssert.AreEqual(beforeBytes[1], await File.ReadAllBytesAsync(secondPath));
        await using var connection = new SqliteConnection($"Data Source={setup.Repository.DatabasePath}");
        await connection.OpenAsync();
        await using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT MAX(Version) FROM AssetLibrarySchemaInfo;";
        Assert.AreEqual(6L, (long)(await schema.ExecuteScalarAsync())!);
        await using var journal = connection.CreateCommand();
        journal.CommandText = "SELECT JournalVersion FROM AssetLibraryUndoJournal WHERE OperationId=$id;";
        journal.Parameters.AddWithValue("$id", archived.UndoToken.OperationId.ToString("D"));
        Assert.AreEqual(2L, (long)(await journal.ExecuteScalarAsync())!);
    }

    [TestMethod]
    public async Task MetadataFolderAndTagMembershipsPersistRedoWhileLegacyV1RemainsUndoOnly()
    {
        await using var setup = await TestSetup.CreateAsync();
        var path = setup.WriteFile("redo.jpg", "redo-source");
        await setup.Repository.ImportAsync([new(path)]);
        var asset = (await ReadAllAsync(setup.Repository, new(PageSize: 10))).Single();

        var metadata = await setup.Repository.UpdateAssetsMetadataAsync([asset.AssetId], rating: 4, comment: "可重做");
        Assert.IsNotNull(metadata.UndoToken);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(metadata.UndoToken));
        var metadataUndone = await setup.Repository.GetAssetAsync(asset.AssetId);
        Assert.AreEqual(0, metadataUndone!.Rating);
        Assert.AreEqual(string.Empty, metadataUndone.Comment);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(metadata.UndoToken));
        var metadataRedone = await setup.Repository.GetAssetAsync(asset.AssetId);
        Assert.AreEqual(4, metadataRedone!.Rating);
        Assert.AreEqual("可重做", metadataRedone.Comment);

        var folder = await setup.Repository.SaveFolderAsync(new(Guid.NewGuid(), null, "目标文件夹"));
        var folderMembership = await setup.Repository.AddToFolderAsync([asset.AssetId], folder.FolderId);
        Assert.IsNotNull(folderMembership.UndoToken);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(folderMembership.UndoToken));
        Assert.IsEmpty(await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(folderMembership.UndoToken));
        Assert.HasCount(1, await setup.Repository.ListFolderMembershipsAsync(folderId: folder.FolderId));

        var tag = await setup.Repository.SaveTagAsync(new(Guid.NewGuid(), "可重做标签"));
        var tagMembership = await setup.Repository.AddTagsAsync([asset.AssetId], [tag.TagId]);
        Assert.IsNotNull(tagMembership.UndoToken);
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(tagMembership.UndoToken));
        Assert.IsEmpty(await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.RedoAsync(tagMembership.UndoToken));
        Assert.HasCount(1, await setup.Repository.ListTagMembershipsAsync(tagId: tag.TagId));

        var legacyToken = await setup.InsertLegacyV1MetadataJournalAsync(asset.AssetId, previousRating: 1, previousComment: "旧值", currentRating: 5, currentComment: "新值");
        await setup.RestartAsync();
        Assert.IsTrue(await setup.Repository.UndoAsync(legacyToken));
        var legacyUndone = await setup.Repository.GetAssetAsync(asset.AssetId);
        Assert.AreEqual(1, legacyUndone!.Rating);
        Assert.AreEqual("旧值", legacyUndone.Comment);
        await setup.RestartAsync();
        Assert.IsFalse(await setup.Repository.RedoAsync(legacyToken));
        var legacyStillUndone = await setup.Repository.GetAssetAsync(asset.AssetId);
        Assert.AreEqual(1, legacyStillUndone!.Rating);
        Assert.AreEqual("旧值", legacyStillUndone.Comment);
    }

    [TestMethod]
    public void WorkspaceStateNormalizesNewFieldsAndKeepsLegacySingleSelectionCompatible()
    {
        var selected = Guid.NewGuid();
        var expanded = Guid.NewGuid();
        var anchor = Guid.NewGuid();
        var settings = new AssetLibraryWorkspaceSettings
        {
            ViewMode = (AssetLibraryViewMode)999,
            SortField = (AssetLibrarySortField)999,
            SortDirection = (AssetLibrarySortDirection)999,
            ActiveCollection = (AssetLibrarySystemCollection)999,
            ExpandedFolderIds = [Guid.Empty, expanded, expanded],
            SelectedAssetIds = [Guid.Empty, selected, selected],
            ScrollAnchors = new() { ["grid"] = anchor, ["invalid-view"] = Guid.NewGuid() }
        };
        settings.Normalize();
        Assert.AreEqual(AssetLibraryViewMode.Grid, settings.ViewMode);
        Assert.AreEqual(AssetLibrarySortField.AddedAt, settings.SortField);
        Assert.AreEqual(AssetLibrarySortDirection.Descending, settings.SortDirection);
        Assert.AreEqual(AssetLibrarySystemCollection.AllAssets, settings.ActiveCollection);
        CollectionAssert.AreEqual(new[] { expanded }, settings.ExpandedFolderIds);
        CollectionAssert.AreEqual(new[] { selected }, settings.SelectedAssetIds);
        Assert.AreEqual(selected, settings.SelectedAssetId);
        Assert.AreEqual(anchor, settings.ScrollAnchors[AssetLibraryViewMode.Grid.ToString()]);
        Assert.HasCount(1, settings.ScrollAnchors);

        var legacy = JsonSerializer.Deserialize<AssetLibraryWorkspaceSettings>(JsonSerializer.Serialize(new AssetLibraryWorkspaceSettings { SelectedAssetId = selected }))!;
        legacy.Normalize();
        CollectionAssert.AreEqual(new[] { selected }, legacy.SelectedAssetIds);
        Assert.AreEqual(selected, legacy.SelectedAssetId);
    }

    private static async Task<IReadOnlyList<AssetItem>> ReadAllAsync(SqliteAssetLibraryRepository repository, AssetLibraryQuery query)
    {
        var result = new List<AssetItem>();
        var ids = new HashSet<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 100; pageNumber++)
        {
            var page = await repository.QueryAsync(query with { Cursor = cursor });
            Assert.IsTrue(string.IsNullOrWhiteSpace(page.RegexError), page.RegexError);
            foreach (var item in page.Items)
            {
                Assert.IsTrue(ids.Add(item.AssetId), $"Duplicate asset across pages: {item.AssetId}");
                result.Add(item);
            }
            cursor = page.NextCursor;
            if (cursor is null) return result;
        }
        Assert.Fail("Paging did not terminate.");
        return result;
    }

    private static IReadOnlyList<AssetItem> SortExpected(IReadOnlyList<AssetItem> items, AssetLibrarySortField field, AssetLibrarySortDirection direction)
    {
        IOrderedEnumerable<AssetItem> ordered = field switch
        {
            AssetLibrarySortField.CaptureTime => direction == AssetLibrarySortDirection.Ascending
                ? items.OrderBy(item => item.CaptureTime is null).ThenBy(item => item.CaptureTime)
                : items.OrderBy(item => item.CaptureTime is null).ThenByDescending(item => item.CaptureTime),
            AssetLibrarySortField.FileName => direction == AssetLibrarySortDirection.Ascending
                ? items.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                : items.OrderByDescending(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
            AssetLibrarySortField.FileSize => direction == AssetLibrarySortDirection.Ascending
                ? items.OrderBy(item => item.FileSize)
                : items.OrderByDescending(item => item.FileSize),
            AssetLibrarySortField.Rating => direction == AssetLibrarySortDirection.Ascending
                ? items.OrderBy(item => item.Rating)
                : items.OrderByDescending(item => item.Rating),
            _ => direction == AssetLibrarySortDirection.Ascending
                ? items.OrderBy(item => item.AddedAt)
                : items.OrderByDescending(item => item.AddedAt)
        };
        return ordered.ThenBy(item => item.AssetId.ToString("D"), StringComparer.Ordinal).ToArray();
    }

    private sealed class TestSetup : IAsyncDisposable
    {
        private readonly string _root;
        private TestSetup(string root, SqliteAssetLibraryRepository repository) { _root = root; Repository = repository; }
        public SqliteAssetLibraryRepository Repository { get; private set; }

        public static async Task<TestSetup> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "PixelTart-P2CoreTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var repository = new SqliteAssetLibraryRepository(Path.Combine(root, "asset-library.db"));
            await repository.InitializeAsync();
            return new(root, repository);
        }

        public async Task RestartAsync()
        {
            var path = Repository.DatabasePath;
            await Repository.DisposeAsync();
            Repository = new(path);
            await Repository.InitializeAsync();
        }

        public string WriteFile(string name, string content)
        {
            var path = Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, Encoding.UTF8);
            return path;
        }

        public async Task<IReadOnlyList<AssetItem>> SeedSortAssetsAsync()
        {
            var paths = Enumerable.Range(0, 7).Select(index => WriteFile($"sort/{index}.jpg", new string((char)('a' + index), index + 1))).ToArray();
            await Repository.ImportAsync(paths.Select(path => new AssetImportRequest(path)));
            var items = await ReadAllAsync(Repository, new(PageSize: 20));
            var names = new[] { "same.jpg", "Alpha.jpg", "bravo.jpg", "same.jpg", "delta.jpg", "Echo.jpg", "foxtrot.jpg" };
            var added = new[] { 3, 1, 1, 5, 2, 5, 4 };
            var captures = new int?[] { 3, null, 1, 2, null, 1, 4 };
            var sizes = new long[] { 30, 10, 10, 50, 20, 50, 40 };
            var ratings = new[] { 3, 1, 1, 5, 2, 5, 4 };
            await using var connection = new SqliteConnection($"Data Source={Repository.DatabasePath}");
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            for (var index = 0; index < items.Count; index++)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = "UPDATE AssetItems SET DisplayName=$name,AddedAt=$added,CaptureTime=$capture,FileSize=$size,Rating=$rating,IsMissing=0 WHERE AssetId=$id;";
                command.Parameters.AddWithValue("$name", names[index]);
                command.Parameters.AddWithValue("$added", new DateTimeOffset(2026, 1, added[index], 12, 0, 0, TimeSpan.Zero).ToString("O"));
                command.Parameters.AddWithValue("$capture", captures[index] is null ? DBNull.Value : new DateTimeOffset(2025, 1, captures[index]!.Value, 12, 0, 0, TimeSpan.Zero).ToString("O"));
                command.Parameters.AddWithValue("$size", sizes[index]);
                command.Parameters.AddWithValue("$rating", ratings[index]);
                command.Parameters.AddWithValue("$id", items[index].AssetId.ToString("D"));
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return await ReadAllAsync(Repository, new(PageSize: 20));
        }

        public async Task<(Dictionary<Guid, double?> Color, Dictionary<Guid, int> State)> SeedVisualSortAsync(IReadOnlyList<AssetItem> items)
        {
            var colors = new double?[] { 120, 30, null, null, null, 300, 30 };
            var states = new[] { 3, 3, 2, 1, 0, 3, 3 };
            var outcomes = new[] { "Succeeded", "Succeeded", "Failed", "Succeeded", null, "Succeeded", "Succeeded" };
            await using var connection = new SqliteConnection($"Data Source={Repository.DatabasePath}");
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            for (var index = 0; index < items.Count; index++)
            {
                var contentHash = $"hash-{index}";
                await using (var asset = connection.CreateCommand())
                {
                    asset.Transaction = (SqliteTransaction)transaction;
                    asset.CommandText = "UPDATE AssetItems SET ContentHash=$hash WHERE AssetId=$id;";
                    asset.Parameters.AddWithValue("$hash", contentHash);
                    asset.Parameters.AddWithValue("$id", items[index].AssetId.ToString("D"));
                    await asset.ExecuteNonQueryAsync();
                }
                if (outcomes[index] is null) continue;
                await using var feature = connection.CreateCommand();
                feature.Transaction = (SqliteTransaction)transaction;
                feature.CommandText = """
                    INSERT INTO AssetVisualFeatures(
                        AssetId,AnalysisVersion,PaletteSize,PaletteSort,ContentFingerprint,SourceContentHash,Outcome,FailureReason,
                        AnalysisSource,SourceProfile,AnalysisProfile,DominantHue,CreatedAt,UpdatedAt)
                    VALUES($id,$version,5,'Weight',$fingerprint,$sourceHash,$outcome,NULL,'RasterOriginal','sRGB','sRGB',$hue,$at,$at);
                    """;
                feature.Parameters.AddWithValue("$id", items[index].AssetId.ToString("D"));
                feature.Parameters.AddWithValue("$version", AssetVisualFeatureContract.AnalysisVersion);
                feature.Parameters.AddWithValue("$fingerprint", $"fingerprint-{index}");
                feature.Parameters.AddWithValue("$sourceHash", index == 3 ? "stale-hash" : contentHash);
                feature.Parameters.AddWithValue("$outcome", outcomes[index]!);
                feature.Parameters.AddWithValue("$hue", colors[index] is null ? DBNull.Value : colors[index]!.Value);
                feature.Parameters.AddWithValue("$at", new DateTimeOffset(2026, 2, index + 1, 12, 0, 0, TimeSpan.Zero).ToString("O"));
                await feature.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return (
                items.Select((item, index) => (item.AssetId, Value: states[index] == 3 ? colors[index] : null)).ToDictionary(pair => pair.AssetId, pair => pair.Value),
                items.Select((item, index) => (item.AssetId, Value: states[index])).ToDictionary(pair => pair.AssetId, pair => pair.Value));
        }

        public async Task<AssetLibraryUndoToken> InsertLegacyV1MetadataJournalAsync(
            Guid assetId,
            int previousRating,
            string previousComment,
            int currentRating,
            string currentComment)
        {
            var token = new AssetLibraryUndoToken(Guid.NewGuid(), "Legacy v1 metadata", DateTimeOffset.UtcNow);
            await using var connection = new SqliteConnection($"Data Source={Repository.DatabasePath}");
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE AssetItems SET Rating=$rating,Comment=$comment WHERE AssetId=$id;";
                update.Parameters.AddWithValue("$rating", currentRating);
                update.Parameters.AddWithValue("$comment", currentComment);
                update.Parameters.AddWithValue("$id", assetId.ToString("D"));
                await update.ExecuteNonQueryAsync();
            }
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO AssetLibraryUndoJournal(OperationId,Description,OperationKind,PayloadJson,CreatedAt,UndoneAt,JournalVersion) VALUES($id,$description,'asset-metadata',$payload,$created,NULL,1);";
                insert.Parameters.AddWithValue("$id", token.OperationId.ToString("D"));
                insert.Parameters.AddWithValue("$description", token.Description);
                insert.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(new { AssetId = assetId, Rating = previousRating, Comment = previousComment }));
                insert.Parameters.AddWithValue("$created", token.CreatedAt.ToString("O"));
                await insert.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return token;
        }

        public async ValueTask DisposeAsync()
        {
            await Repository.DisposeAsync();
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
